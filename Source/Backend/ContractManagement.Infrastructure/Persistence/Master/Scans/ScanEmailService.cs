using System.Text.Json;
using ContractManagement.API.Domains.Email;
using ContractManagement.Application.Features.Internal;
using ContractManagement.Application.Features.Internal.Scans;
using ContractManagement.Domain.Entities.Master;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ContractManagement.Infrastructure.Persistence.Master.Scans;

/// <summary>
/// Sends a scan found by the search to one or more addresses, as a PDF attachment with a short summary. The file is
/// opened exactly as "View" opens it: the same scope check, the employee's own key, the same opening log; so only
/// someone who may read the scan can send it. Every sending is written to AUDIT_LOG with its recipients.
/// </summary>
public sealed class ScanEmailService(
    MasterDbContext db,
    SignedEvidenceService evidence,
    ScannedContractService scanned,
    IEmailTransport email,
    IOptions<SmtpEmailOptions> smtp,
    ICurrentEmployee currentEmployee,
    ILogger<ScanEmailService> logger)
{
    public const string EmailedAction = "ScanEmailed";
    public const string EvidenceEntity = "CONTRACT_SIGNED_EVIDENCE";
    public const string JobEntity = "OCR_JOB";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>A paper-signing evidence of a contract in the system.</summary>
    public async Task<ScanEmailResultDto> SendEvidenceAsync(
        long evidenceId, IReadOnlyList<string?>? recipients, string? note, string? fileKey, CancellationToken cancellationToken)
    {
        var (addresses, cleanNote) = Check(recipients, note);
        var file = await evidence.GetFileAsync(evidenceId, fileKey, cancellationToken);
        var overview = await evidence.GetOverviewAsync(await evidence.DocumentOfAsync(evidenceId, cancellationToken), cancellationToken);
        var shown = overview.Evidences.FirstOrDefault(item => item.EvidenceId == evidenceId);
        var details = new List<(string, string)>();
        Add(details, "Số hợp đồng", overview.DocumentCode);
        Add(details, "Tên hợp đồng", overview.DocumentName);
        Add(details, "Khách hàng", overview.CustomerName);
        Add(details, "Mã số thuế", overview.CustomerTaxCode);
        Add(details, "Ngày ký", shown?.SignDate);
        var content = new ScanEmailContent(
            overview.DocumentCode ?? overview.DocumentName, details, file.FileName, file.ContentType, file.Content);
        return await SendAsync(content, addresses, cleanNote, EvidenceEntity, evidenceId, cancellationToken);
    }

    /// <summary>A scan of an old paper contract (Hợp đồng giấy scan).</summary>
    public async Task<ScanEmailResultDto> SendScannedJobAsync(
        long jobId, IReadOnlyList<string?>? recipients, string? note, string? fileKey, CancellationToken cancellationToken)
    {
        var (addresses, cleanNote) = Check(recipients, note);
        var file = await scanned.GetJobFileAsync(jobId, fileKey, cancellationToken);
        var job = await scanned.GetJobAsync(jobId, cancellationToken);
        var details = new List<(string, string)>();
        Add(details, "Số hợp đồng", job.Contract?.ContractCode ?? First(job, "ContractCode"));
        Add(details, "Tên hợp đồng", job.Contract?.ContractName);
        Add(details, "Khách hàng", job.Contract?.CustomerName ?? First(job, "CustomerName"));
        Add(details, "Mã số thuế", First(job, "TaxCode"));
        Add(details, "Số điện thoại", First(job, "PhoneNumber"));
        var content = new ScanEmailContent(
            job.Contract?.ContractCode ?? First(job, "ContractCode") ?? job.FileName, details, file.FileName, file.ContentType, file.Content);
        return await SendAsync(content, addresses, cleanNote, JobEntity, jobId, cancellationToken);
    }

    /// <summary>Everything about the request is checked before the file is opened.</summary>
    private (IReadOnlyList<string> Addresses, string? Note) Check(IReadOnlyList<string?>? recipients, string? note)
    {
        var addresses = ScanEmail.Recipients(recipients);
        var cleanNote = ScanEmail.Note(note);
        if (!smtp.Value.IsConfigured())
        {
            throw new InternalAppException(503, "Máy chủ chưa cấu hình gửi email (Email:Smtp). Hãy báo quản trị.");
        }

        return (addresses, cleanNote);
    }

    private async Task<ScanEmailResultDto> SendAsync(
        ScanEmailContent content, IReadOnlyList<string> addresses, string? note, string entity, long entityId,
        CancellationToken cancellationToken)
    {
        if (content.Content.Length > ScanEmail.MaxAttachmentBytes)
        {
            throw new InternalAppException(413,
                $"Tệp quá lớn để gửi qua email (tối đa {ScanEmail.MaxAttachmentBytes / (1024 * 1024)} MB). Hãy tải về rồi gửi bằng cách khác.");
        }

        content = content with { Title = OneLine(content.Title), FileName = OneLine(content.FileName) };
        var senderName = await db.Employees.AsNoTracking()
            .Where(employee => employee.EmployeeId == currentEmployee.EmployeeId)
            .Select(employee => employee.EmployeeDisplayName)
            .FirstOrDefaultAsync(cancellationToken) ?? "Nhân viên";

        var sent = new List<string>();
        var failed = new List<ScanEmailFailureDto>();
        foreach (var address in addresses)
        {
            try
            {
                await email.SendAsync(ScanEmail.Compose(address, content, senderName, note), cancellationToken);
                sent.Add(address);
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(exception, "Sending a scan by email failed for one recipient");
                failed.Add(new ScanEmailFailureDto(address, exception is TimeoutException
                    ? "Máy chủ email không phản hồi kịp."
                    : "Máy chủ email từ chối gửi tới địa chỉ này."));
            }
        }

        db.AuditLogs.Add(new AuditLog
        {
            ActionType = EmailedAction,
            EntityName = entity,
            EntityId = entityId.ToString(),
            ActorType = "Employee",
            ActorId = currentEmployee.EmployeeId,
            NewData = JsonSerializer.Serialize(new
            {
                file = content.FileName,
                sent,
                failed = failed.Select(failure => failure.Email),
                withNote = note is not null,
            }, Json),
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(CancellationToken.None);

        if (sent.Count == 0)
        {
            throw new InternalAppException(502, "Không gửi được email nào. Kiểm tra cấu hình email của máy chủ rồi thử lại.");
        }

        return new ScanEmailResultDto(sent, failed);
    }

    private static void Add(List<(string, string)> details, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            details.Add((label, OneLine(value)));
        }
    }

    private static string? First(ScannedJobDetailDto job, string type) =>
        job.Keywords.Items.FirstOrDefault(keyword => keyword.Type == type)?.Value;

    /// <summary>A subject or file name never carries a line break into the mail headers.</summary>
    private static string OneLine(string value) =>
        string.Join(' ', value.Split(['\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries)).Trim();
}
