using ContractManagement.Application.Features.Internal.Scans;
using ContractManagement.Domain.Entities.Master;
using ContractManagement.Domain.Internal.Scans;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Infrastructure.Persistence.Master.Scans;

/// <summary>
/// Finds contracts by the keywords read from their scans (or added by hand): contract code, tax code, phone number or
/// customer name. Paper-signing evidence is listed for those who see signed contracts, scanned old paper contracts for
/// those who see scanned contracts.
/// </summary>
public sealed class ScanSearchService(
    MasterDbContext db,
    SignedEvidenceService evidence,
    ScannedContractService scanned,
    ICurrentEmployee currentEmployee)
{
    public const string AllTypes = "All";
    private const int MaxResults = 50;

    /// <summary>Names are typed without care for accents or case: "cong ty abc" finds "CÔNG TY ABC".</summary>
    // Vietnamese_CI_AI keeps Ư, Ơ, Â, Đ apart from U, O, A, D; this one folds them all.
    private const string LooseCollation = "Latin1_General_100_CI_AI";

    public async Task<ScanSearchResponseDto> SearchAsync(string? q, string? type, CancellationToken cancellationToken)
    {
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

        var items = currentEmployee.HasPermission(SigningPermissions.View)
            ? await evidence.SearchResultsAsync(found, MaxResults, cancellationToken)
            : [];
        var scans = await scanned.SearchResultsAsync(found, MaxResults, cancellationToken);
        return new ScanSearchResponseDto(typed, items, scans);
    }
}
