using ContractManagement.Application.Features.Internal.Scans;
using ContractManagement.Domain.Entities.Master;
using ContractManagement.Domain.Internal.Scans;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Infrastructure.Persistence.Master.Scans;

/// <summary>
/// One search over every contract: by the keywords read from scans (or added by hand) and, for contracts made online,
/// by their own data (OnlineContractSearch). Paper-signing evidence is listed for those who see signed contracts,
/// scanned old paper contracts for those who see scanned contracts, online contracts by the contract screens' rules.
/// </summary>
public sealed class ScanSearchService(
    MasterDbContext db,
    SignedEvidenceService evidence,
    ScannedContractService scanned,
    ICurrentEmployee currentEmployee,
    OnlineContractSearch online)
{
    public const string AllTypes = "All";
    private const int MaxResults = 50;

    /// <summary>Names are typed without care for accents or case: "cong ty abc" finds "CÔNG TY ABC".</summary>
    // Vietnamese_CI_AI keeps Ư, Ơ, Â, Đ apart from U, O, A, D; this one folds them all.
    private const string LooseCollation = "Latin1_General_100_CI_AI";

    /// <summary><paramref name="from"/> and <paramref name="to"/> (yyyy-MM-dd, inclusive, Vietnam time) narrow scans by upload date and evidence by signing date.</summary>
    public async Task<ScanSearchResponseDto> SearchAsync(string? q, string? type, string? from, string? to, CancellationToken cancellationToken)
    {
        var first = Day(from, "Từ ngày");
        var last = Day(to, "Đến ngày");
        if (first > last)
        {
            throw new ArgumentException("Từ ngày phải trước hoặc bằng Đến ngày.");
        }

        var typed = q?.Trim() ?? "";
        if (typed.Length < 3)
        {
            throw new ArgumentException("Nhập ít nhất 3 ký tự để tìm.");
        }

        var filter = string.IsNullOrWhiteSpace(type) ? AllTypes : type;
        ScanKeywordType? only = null;
        if (filter != AllTypes)
        {
            if (!Enum.TryParse<ScanKeywordType>(filter, out var parsed))
            {
                throw new ArgumentException("Loại tìm kiếm không hợp lệ.");
            }

            only = parsed;
        }

        var found = new List<ContractScanKeyword>();
        // Codes, tax codes and phone numbers are stored normalised; match the normalised forms of what was typed.
        var codeTypes = new[] { ScanKeywordType.ContractCode, ScanKeywordType.TaxCode, ScanKeywordType.PhoneNumber }
            .Where(candidate => only is null || only == candidate)
            .Select(candidate => (int)candidate)
            .ToList();
        if (codeTypes.Count > 0)
        {
            foreach (var term in ContractScanKeywordExtractor.SearchTerms(typed))
            {
                found.AddRange(await db.ContractScanKeywords.AsNoTracking()
                    .Where(keyword => codeTypes.Contains(keyword.ContractScanKeywordType)
                        && keyword.ContractScanKeywordValue.Contains(term))
                    .Take(MaxResults * 4)
                    .ToListAsync(cancellationToken));
            }
        }

        if (only is null or ScanKeywordType.CustomerName)
        {
            var name = string.Join(' ', typed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            found.AddRange(await db.ContractScanKeywords.AsNoTracking()
                .Where(keyword => keyword.ContractScanKeywordType == (int)ScanKeywordType.CustomerName
                    && EF.Functions.Collate(keyword.ContractScanKeywordValue, LooseCollation).Contains(name))
                .Take(MaxResults * 4)
                .ToListAsync(cancellationToken));
        }

        if (only is null or ScanKeywordType.Email)
        {
            // Emails are stored lower case, without spaces.
            var email = string.Concat(typed.Where(character => !char.IsWhiteSpace(character))).ToLowerInvariant();
            found.AddRange(await db.ContractScanKeywords.AsNoTracking()
                .Where(keyword => keyword.ContractScanKeywordType == (int)ScanKeywordType.Email
                    && keyword.ContractScanKeywordValue.Contains(email))
                .Take(MaxResults * 4)
                .ToListAsync(cancellationToken));
        }

        // The date range is applied before the page of results is cut.
        var ranged = first is not null || last is not null;
        var limit = ranged ? int.MaxValue : MaxResults;
        bool Within(DateOnly day) => (first is null || day >= first) && (last is null || day <= last);
        var items = currentEmployee.HasPermission(SigningPermissions.View)
            ? await evidence.SearchResultsAsync(found, limit, cancellationToken)
            : [];
        var scans = await scanned.SearchResultsAsync(found, limit, cancellationToken);
        if (ranged)
        {
            items = items.Where(item => Within(DateOnly.ParseExact(item.SignDate, "yyyy-MM-dd"))).Take(MaxResults).ToList();
            scans = scans.Where(scan => Within(DateOnly.FromDateTime(scan.UploadedAt.ToUniversalTime().AddHours(7)))).Take(MaxResults).ToList();
        }

        var contracts = await online.SearchAsync(typed, only, first, last, cancellationToken);
        return new ScanSearchResponseDto(typed, items, scans, contracts);
    }

    private static DateOnly? Day(string? typed, string label) =>
        string.IsNullOrWhiteSpace(typed) ? null
        : DateOnly.TryParseExact(typed.Trim(), "yyyy-MM-dd", out var day) ? day
        : throw new ArgumentException($"{label} không hợp lệ (định dạng yyyy-MM-dd).");
}
