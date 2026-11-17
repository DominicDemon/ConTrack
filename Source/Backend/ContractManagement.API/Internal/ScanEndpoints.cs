using System.Globalization;
using ContractManagement.Application.Features.Internal;
using ContractManagement.Application.Features.Internal.Scans;
using ContractManagement.Infrastructure.Persistence.Master;
using ContractManagement.Infrastructure.Persistence.Master.Scans;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.API.Internal;

public sealed record ScanUser(long EmployeeId, string Username, string DisplayName, IReadOnlyList<string> Permissions);

/// <summary>The signed-in employee of this request, loaded by the filter of the scan routes.</summary>
public sealed class ScanCurrentEmployee(IHttpContextAccessor accessor) : ICurrentEmployee
{
    private ScanUser User => accessor.HttpContext?.Items[ScanEndpoints.UserItem] as ScanUser
        ?? throw new InternalAppException(401, "Vui lòng đăng nhập.");

    public long EmployeeId => User.EmployeeId;

    public bool HasPermission(string permissionCode) => User.Permissions.Contains(permissionCode, StringComparer.Ordinal);
}

/// <summary>
/// Routes of the paper-scan features under /api/internal: paper-signing evidence (Master_Database_HuongDan F18, F19),
/// old paper contracts scanned in (CONTRACTS.source_type = SCANNED) and the keys that open encrypted scans.
/// They use the internal session and CSRF token, and check permissions from SYSTEM_ROLE_PERMISSION.
/// </summary>
public static class ScanEndpoints
{
    internal const string UserItem = "Internal.Scan.User.v1";
    private const string FileKeyHeader = "X-File-Key";
    private const long MaxSingleUploadBytes = 21 * 1024 * 1024;
    private const long MaxBatchUploadBytes = 200L * 1024 * 1024;
    private const int MaxReasonLength = 1000;

    public sealed record AcceptRequest(bool PartyASigned, bool PartyBSigned, bool ContentMatches, string? Note);

    public sealed record RejectRequest(string? Reason);

    public sealed record AddKeywordRequest(string? Type, string? Value);

    public sealed record ConfirmRequest(
        string? ContractCode,
        string? ContractName,
        long CustomerId,
        int? ContractTypeId,
        string? SignedDate,
        string? EffectiveDate,
        string? ExpirationDate,
        double? ContractValue,
        string? Currency,
        string? Description);

    public static void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentEmployee, ScanCurrentEmployee>();
        services.AddInternalScans(configuration);
    }

    public static void Map(RouteGroupBuilder api)
    {
        var routes = api.MapGroup("").AddEndpointFilter(async (invocation, next) =>
        {
            var context = invocation.HttpContext;
            context.Items[UserItem] = await LoadUserAsync(context);
            if (!HttpMethods.IsGet(context.Request.Method))
            {
                await context.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(context);
            }

            return await next(invocation);
        });

        routes.MapGet("/scan-access", (HttpContext context) => Results.Ok((ScanUser)context.Items[UserItem]!));
        MapSignedEvidence(routes);
        MapScannedContracts(routes);
        MapEncryptionKeys(routes);
    }

    private static void MapSignedEvidence(RouteGroupBuilder routes)
    {
        routes.MapGet("/contract-documents", async (SignedEvidenceService service, CancellationToken ct) =>
            Results.Ok(await service.ListContractsAsync(ct))).Need(SigningPermissions.View);

        routes.MapGet("/contract-documents/{documentId:long}/signed-evidence", async (long documentId, SignedEvidenceService service, CancellationToken ct) =>
            Results.Ok(await service.GetOverviewAsync(documentId, ct))).Need(SigningPermissions.View);

        routes.MapPost("/contract-documents/{documentId:long}/signed-evidence", async (
                long documentId, IFormFile? file, [FromForm] string? versionId, [FromForm] string? signDate, SignedEvidenceService service, CancellationToken ct) =>
            {
                if (!long.TryParse(versionId, out var version) || version <= 0)
                {
                    throw new ArgumentException("Hãy chọn phiên bản đã thống nhất.");
                }

                var date = ParseDate(signDate) ?? throw new ArgumentException("Hãy nhập ngày ký ghi trên tài liệu.");
                var upload = await ReadAsync(file, ct);
                await service.UploadAsync(documentId, version, date, upload.Name, upload.Content, ct);
                return Results.Ok(await service.GetOverviewAsync(documentId, ct));
            })
            .Need(SigningPermissions.View, SigningPermissions.Upload)
            .AcceptsUpload(MaxSingleUploadBytes);

        routes.MapGet("/signed-evidence/search", async (string? q, string? type, ScanSearchService service, CancellationToken ct) =>
            Results.Ok(await service.SearchAsync(q, type, ct))).NeedAny(SigningPermissions.View, ScannedContractPermissions.View);

        routes.MapGet("/signed-evidence/{evidenceId:long}/file", async (long evidenceId, HttpContext context, SignedEvidenceService service, CancellationToken ct) =>
        {
            var file = await service.GetFileAsync(evidenceId, FileKey(context), ct);
            return Results.File(file.Content, file.ContentType, file.FileName);
        }).Need(SigningPermissions.View);

        routes.MapPost("/signed-evidence/{evidenceId:long}/accept", async (long evidenceId, AcceptRequest request, SignedEvidenceService service, CancellationToken ct) =>
        {
            // F18 step 4: the business check of signatures and stamps is done by a person, and recorded.
            Require(request.PartyASigned, "Hãy xác nhận đã có chữ ký và dấu của bên A.");
            Require(request.PartyBSigned, "Hãy xác nhận đã có chữ ký và dấu của bên B.");
            Require(request.ContentMatches, "Hãy xác nhận nội dung khớp phiên bản đã chốt.");
            Require(request.Note is null || request.Note.Trim().Length <= MaxReasonLength, $"Ghi chú không được quá {MaxReasonLength} ký tự.");
            await service.AcceptAsync(evidenceId, request.Note, ct);
            return Results.Ok(await service.GetOverviewAsync(await service.DocumentOfAsync(evidenceId, ct), ct));
        }).Need(SigningPermissions.View, SigningPermissions.Review);

        routes.MapPost("/signed-evidence/{evidenceId:long}/reject", async (long evidenceId, RejectRequest request, SignedEvidenceService service, CancellationToken ct) =>
        {
            await service.RejectAsync(evidenceId, Reason(request.Reason, "Hãy nhập lý do bản ký không hợp lệ."), ct);
            return Results.Ok(await service.GetOverviewAsync(await service.DocumentOfAsync(evidenceId, ct), ct));
        }).Need(SigningPermissions.View, SigningPermissions.Review);

        routes.MapPost("/signed-evidence/{evidenceId:long}/supersede", async (
                long evidenceId, IFormFile? file, [FromForm] string? reason, SignedEvidenceService service, CancellationToken ct) =>
            {
                var upload = await ReadAsync(file, ct);
                var newId = await service.SupersedeAsync(evidenceId, upload.Name, upload.Content, Reason(reason, "Hãy nhập lý do thay bản ký."), ct);
                return Results.Ok(await service.GetOverviewAsync(await service.DocumentOfAsync(newId, ct), ct));
            })
            .Need(SigningPermissions.View, SigningPermissions.Upload)
            .AcceptsUpload(MaxSingleUploadBytes);

        routes.MapPost("/signed-evidence/{evidenceId:long}/keywords/retry", async (long evidenceId, HttpContext context, SignedEvidenceService service, CancellationToken ct) =>
            Results.Ok(await service.RetryKeywordsAsync(evidenceId, FileKey(context), ct))).Need(SigningPermissions.View, SigningPermissions.Upload);

        routes.MapPost("/signed-evidence/{evidenceId:long}/keywords", async (long evidenceId, AddKeywordRequest request, SignedEvidenceService service, CancellationToken ct) =>
            Results.Ok(await service.AddKeywordAsync(evidenceId, request.Type ?? "", request.Value ?? "", ct))).Need(SigningPermissions.View, SigningPermissions.Upload);

        routes.MapDelete("/signed-evidence/{evidenceId:long}/keywords/{keywordId:long}", async (long evidenceId, long keywordId, SignedEvidenceService service, CancellationToken ct) =>
        {
            await service.RemoveKeywordAsync(evidenceId, keywordId, ct);
            return Results.NoContent();
        }).Need(SigningPermissions.View, SigningPermissions.Upload);
    }

    private static void MapScannedContracts(RouteGroupBuilder routes)
    {
        routes.MapPost("/scanned-contracts/batches", async (IFormFileCollection? files, ScannedContractService service, CancellationToken ct) =>
            {
                Require(files is { Count: > 0 }, "Hãy chọn ít nhất một tệp bản scan.");
                Require(files!.Count <= ScannedContractService.MaxFilesPerBatch, $"Mỗi lượt tải tối đa {ScannedContractService.MaxFilesPerBatch} tệp.");
                var scans = new List<(string FileName, byte[] Content)>(files.Count);
                foreach (var file in files)
                {
                    var upload = await ReadAsync(file, ct);
                    scans.Add((upload.Name, upload.Content));
                }

                return Results.Ok(await service.GetBatchAsync(await service.UploadBatchAsync(scans, ct), ct));
            })
            .Need(ScannedContractPermissions.View, ScannedContractPermissions.Upload)
            .AcceptsUpload(MaxBatchUploadBytes);

        routes.MapGet("/scanned-contracts/batches", async (ScannedContractService service, CancellationToken ct) =>
            Results.Ok(await service.ListBatchesAsync(ct))).Need(ScannedContractPermissions.View);

        routes.MapGet("/scanned-contracts/batches/{batchId:long}", async (long batchId, ScannedContractService service, CancellationToken ct) =>
            Results.Ok(await service.GetBatchAsync(batchId, ct))).Need(ScannedContractPermissions.View);

        routes.MapGet("/scanned-contracts/jobs/{jobId:long}", async (long jobId, ScannedContractService service, CancellationToken ct) =>
            Results.Ok(await service.GetJobAsync(jobId, ct))).Need(ScannedContractPermissions.View);

        routes.MapGet("/scanned-contracts/jobs/{jobId:long}/file", async (long jobId, HttpContext context, ScannedContractService service, CancellationToken ct) =>
        {
            var file = await service.GetJobFileAsync(jobId, FileKey(context), ct);
            return Results.File(file.Content, file.ContentType, file.FileName);
        }).Need(ScannedContractPermissions.View);

        routes.MapPost("/scanned-contracts/jobs/{jobId:long}/retry", async (long jobId, HttpContext context, ScannedContractService service, CancellationToken ct) =>
            Results.Ok(await service.GetJobAsync(await service.RetryAsync(jobId, FileKey(context), ct), ct)))
            .Need(ScannedContractPermissions.View, ScannedContractPermissions.Upload);

        // A keyword OCR missed or read wrong; the scan is found by it at once.
        routes.MapPost("/scanned-contracts/jobs/{jobId:long}/keywords", async (long jobId, AddKeywordRequest request, ScannedContractService service, CancellationToken ct) =>
            Results.Ok(await service.AddKeywordAsync(jobId, request.Type ?? "", request.Value ?? "", ct)))
            .Need(ScannedContractPermissions.View, ScannedContractPermissions.Upload);

        routes.MapDelete("/scanned-contracts/jobs/{jobId:long}/keywords/{keywordId:long}", async (long jobId, long keywordId, ScannedContractService service, CancellationToken ct) =>
        {
            await service.RemoveKeywordAsync(jobId, keywordId, ct);
            return Results.NoContent();
        }).Need(ScannedContractPermissions.View, ScannedContractPermissions.Upload);

        // Optional: makes a CONTRACTS row from the scan, to manage it with the other contracts.
        routes.MapPost("/scanned-contracts/jobs/{jobId:long}/confirm", async (long jobId, ConfirmRequest request, ScannedContractService service, CancellationToken ct) =>
        {
            await service.ConfirmAsync(jobId, ToInput(request), ct);
            return Results.Ok(await service.GetJobAsync(jobId, ct));
        }).Need(ScannedContractPermissions.View, ScannedContractPermissions.Confirm);

        routes.MapGet("/scanned-contracts", async (ScannedContractService service, CancellationToken ct) =>
            Results.Ok(await service.ListContractsAsync(ct))).Need(ScannedContractPermissions.View);

        routes.MapGet("/scanned-contracts/{contractId:long}/file", async (long contractId, HttpContext context, ScannedContractService service, CancellationToken ct) =>
        {
            var file = await service.GetContractFileAsync(contractId, FileKey(context), ct);
            return Results.File(file.Content, file.ContentType, file.FileName);
        }).Need(ScannedContractPermissions.View);

        routes.MapGet("/scanned-contracts/customers", async (string? q, ScannedContractService service, CancellationToken ct) =>
            Results.Ok(await service.FindCustomersAsync(q, ct))).Need(ScannedContractPermissions.View);

        routes.MapGet("/scanned-contracts/contract-types", async (ScannedContractService service, CancellationToken ct) =>
            Results.Ok(await service.ListContractTypesAsync(ct))).Need(ScannedContractPermissions.View);
    }

    private static void MapEncryptionKeys(RouteGroupBuilder routes)
    {
        routes.MapGet("/encryption-keys", async (FileProtectionService protection, CancellationToken ct) =>
            Results.Ok(await protection.ListKeysAsync(ct))).Need(EncryptionKeyPermissions.Manage);

        // The answer holds the key, shown once; the internal host already sends no-store on every response.
        routes.MapPost("/encryption-keys/{employeeId:long}", async (long employeeId, FileProtectionService protection, CancellationToken ct) =>
            Results.Ok(await protection.IssueKeyAsync(employeeId, ct))).Need(EncryptionKeyPermissions.Manage);

        // Reopens the employee's old files for their current key with the company recovery private key: the file sent
        // with this request only and never kept, or, without a file, the key the server keeps.
        routes.MapPost("/encryption-keys/{employeeId:long}/recover-files", async (long employeeId, HttpRequest request, FileProtectionService protection, CancellationToken ct) =>
        {
            string? pem = null;
            if (request.HasFormContentType && (await request.ReadFormAsync(ct)).Files.GetFile("recoveryKey") is { } file)
            {
                Require(file.Length is > 0 and <= 32 * 1024, "Tệp khóa dự phòng không hợp lệ.");
                using var reader = new StreamReader(file.OpenReadStream());
                pem = await reader.ReadToEndAsync(ct);
            }

            return Results.Ok(await protection.RecoverFilesAsync(employeeId, pem, ct));
        }).AcceptsUpload(64 * 1024).Need(EncryptionKeyPermissions.Manage);

        routes.MapGet("/encryption-keys/me", async (FileProtectionService protection, ICurrentEmployee employee, CancellationToken ct) =>
            Results.Ok(new
            {
                hasKey = await protection.HasActiveKeyAsync(employee.EmployeeId, ct),
                encryptionEnabled = protection.Enabled,
                serverKeepsRecoveryKey = protection.ServerKeepsRecoveryKey,
            }));
    }

    /// <summary>The employee, active, with the permissions of their roles and all those they depend on (guide 2.1).</summary>
    private static async Task<ScanUser> LoadUserAsync(HttpContext context)
    {
        if (!long.TryParse(context.Session.GetString(InternalHost.UserKey), out var employeeId))
        {
            throw new InternalAppException(401, "Vui lòng đăng nhập.");
        }

        var db = context.RequestServices.GetRequiredService<MasterDbContext>();
        var ct = context.RequestAborted;
        var employee = await db.Employees.AsNoTracking()
            .Where(candidate => candidate.EmployeeId == employeeId && candidate.EmployeeStatus == "ACTIVE")
            .Select(candidate => new { candidate.EmployeeUsername, candidate.EmployeeDisplayName })
            .SingleOrDefaultAsync(ct)
            ?? throw new InternalAppException(401, "Phiên đăng nhập hết hạn hoặc tài khoản đã khóa.");

        var granted = await (
                from role in db.EmployeeRoles
                join systemRole in db.SystemRoles on role.SystemRoleId equals systemRole.SystemRoleId
                join rolePermission in db.SystemRolePermissions on role.SystemRoleId equals rolePermission.SystemRoleId
                where role.EmployeeId == employeeId && systemRole.SystemRoleStatus == "ACTIVE"
                select rolePermission.SystemPermissionId)
            .Distinct()
            .ToListAsync(ct);
        var dependencies = await db.SystemPermissionDependencies.AsNoTracking()
            .Select(dependency => new { dependency.SystemPermissionId, dependency.DependsOnSystemPermissionId })
            .ToListAsync(ct);
        var effective = PermissionClosure.Expand(
            granted,
            dependencies.Select(dependency => (dependency.SystemPermissionId, dependency.DependsOnSystemPermissionId)));
        var permissions = await db.SystemPermissions.AsNoTracking()
            .Where(permission => effective.Contains(permission.SystemPermissionId))
            .Select(permission => permission.SystemPermissionCode)
            .ToListAsync(ct);
        return new ScanUser(employeeId, employee.EmployeeUsername, employee.EmployeeDisplayName, permissions);
    }

    /// <summary>Needs every permission listed; the group filter has already loaded the employee.</summary>
    private static RouteHandlerBuilder Need(this RouteHandlerBuilder builder, params string[] permissions) =>
        builder.AddEndpointFilter(async (invocation, next) =>
        {
            var employee = invocation.HttpContext.RequestServices.GetRequiredService<ICurrentEmployee>();
            if (permissions.Any(permission => !employee.HasPermission(permission)))
            {
                throw new InternalAppException(403, "Bạn không có quyền thực hiện thao tác này.");
            }

            return await next(invocation);
        });

    /// <summary>Needs at least one of the permissions listed.</summary>
    private static RouteHandlerBuilder NeedAny(this RouteHandlerBuilder builder, params string[] permissions) =>
        builder.AddEndpointFilter(async (invocation, next) =>
        {
            var employee = invocation.HttpContext.RequestServices.GetRequiredService<ICurrentEmployee>();
            if (!permissions.Any(employee.HasPermission))
            {
                throw new InternalAppException(403, "Bạn không có quyền thực hiện thao tác này.");
            }

            return await next(invocation);
        });

    /// <summary>Form posts here are checked by the CSRF filter above, not by the antiforgery middleware.</summary>
    private static RouteHandlerBuilder AcceptsUpload(this RouteHandlerBuilder builder, long maxBytes) =>
        builder
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(maxBytes), new RequestFormLimitsAttribute { MultipartBodyLengthLimit = maxBytes });

    private static ConfirmScannedContractInput ToInput(ConfirmRequest request)
    {
        // What CONTRACTS needs (NOT NULL, widths) and the dates in order. Nothing about the scan itself is checked.
        Require(!string.IsNullOrWhiteSpace(request.ContractCode), "Hãy nhập số hợp đồng.");
        Require(request.ContractCode!.Trim().Length <= 50, "Số hợp đồng không được quá 50 ký tự.");
        Require(!string.IsNullOrWhiteSpace(request.ContractName), "Hãy nhập tên hợp đồng.");
        Require(request.ContractName!.Trim().Length <= 500, "Tên hợp đồng không được quá 500 ký tự.");
        Require(request.CustomerId > 0, "Hãy chọn khách hàng.");
        Require(request.ContractValue is null or >= 0, "Giá trị hợp đồng không được âm.");
        Require(request.Currency is null || request.Currency.Trim().Length <= 10, "Mã tiền tệ không được quá 10 ký tự.");
        Require(request.Description is null || request.Description.Trim().Length <= 2000, "Mô tả không được quá 2000 ký tự.");
        var signed = ParseDate(request.SignedDate) ?? throw new ArgumentException("Hãy nhập ngày ký ghi trên hợp đồng (yyyy-MM-dd).");
        var effective = OptionalDate(request.EffectiveDate, "Ngày hiệu lực không đúng dạng yyyy-MM-dd.");
        var expiration = OptionalDate(request.ExpirationDate, "Ngày hết hạn không đúng dạng yyyy-MM-dd.");
        Require(effective is null || expiration is null || effective <= expiration, "Ngày hết hạn phải sau ngày hiệu lực.");
        return new ConfirmScannedContractInput(
            request.ContractCode, request.ContractName, request.CustomerId, request.ContractTypeId,
            signed, effective, expiration, request.ContractValue, request.Currency, request.Description);
    }

    private static string Reason(string? reason, string missing)
    {
        Require(!string.IsNullOrWhiteSpace(reason), missing);
        Require(reason!.Trim().Length <= MaxReasonLength, $"Lý do không được quá {MaxReasonLength} ký tự.");
        return reason.Trim();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new ArgumentException(message);
        }
    }

    private static string? FileKey(HttpContext context) => context.Request.Headers[FileKeyHeader].FirstOrDefault();

    private static DateOnly? ParseDate(string? value) =>
        DateOnly.TryParseExact(value?.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;

    private static DateOnly? OptionalDate(string? value, string invalid) =>
        string.IsNullOrWhiteSpace(value) ? null : ParseDate(value) ?? throw new ArgumentException(invalid);

    /// <summary>The only checks on a stored scan: there is a file and it is not empty.</summary>
    private static async Task<(string Name, byte[] Content)> ReadAsync(IFormFile? file, CancellationToken ct)
    {
        Require(file is not null && !string.IsNullOrWhiteSpace(file.FileName), "Vui lòng chọn tệp bản quét.");
        Require(file!.Length > 0, "Tệp bản quét không có nội dung.");
        using var content = new MemoryStream();
        await file.CopyToAsync(content, ct);
        return (file.FileName, content.ToArray());
    }
}
