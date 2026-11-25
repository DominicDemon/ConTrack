using ContractManagement.Application.Features.Internal.Scans;
using ContractManagement.Domain.Internal.Scans;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Infrastructure.Persistence.Master.Scans;

/// <summary>
/// The online half of "Tìm hợp đồng": contracts drafted and signed in the system have no scan and no OCR keywords, but
/// their data is already structured. A contract matches by its code or name, by its customer (name, phone, email, tax
/// code, or the tax code of one of the customer's businesses), or by the party details written in its current version
/// (CUSTOMER_TAX_CODE, CUSTOMER_CONTACT_PHONE, CUSTOMER_EMAIL, CUSTOMER_COMPANY, CUSTOMER_CONTACT_NAME).
/// Who sees what follows the contract screens: the contract workflow (Hành chính, the board, the sales manager) and
/// readers (contract.read) see every contract; a salesperson sees their own; anyone else none.
/// </summary>
public sealed class OnlineContractSearch(MasterDbContext db, ICurrentEmployee currentEmployee)
{
    public const string ContractNameType = "ContractName";
    private const int MaxResults = 50;
    private const int PerQuery = MaxResults * 4;
    private const string LooseCollation = "Latin1_General_100_CI_AI";
    private const string ContractSource = "Contract", CustomerSource = "Customer";

    private static readonly string[] SeeAll = ["draft.manage.all", "contract.check", "contract.approve", "legal.manage", "contract.read"];
    private static readonly string[] TaxFields = ["CUSTOMER_TAX_CODE"], PhoneFields = ["CUSTOMER_CONTACT_PHONE"], EmailFields = ["CUSTOMER_EMAIL"];
    private static readonly string[] NameFields = ["CUSTOMER_COMPANY", "CUSTOMER_CONTACT_NAME"];

    private sealed record Hit(long DocumentId, string Type, string Value, string Source);

    /// <param name="typed">At least 3 characters (checked by the caller).</param>
    /// <param name="only">A keyword type to search by, or null for every kind.</param>
    public async Task<IReadOnlyList<OnlineContractSearchResultDto>> SearchAsync(string typed, ScanKeywordType? only,
        DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var all = SeeAll.Any(currentEmployee.HasPermission);
        if (!all && !currentEmployee.HasPermission("draft.write")) return [];
        var me = currentEmployee.EmployeeId;
        var documents = db.ContractDocuments.AsNoTracking().Where(d => d.TenantId == null && (all || d.EmployeeId == me));
        bool Wants(ScanKeywordType type) => only is null || only == type;

        var name = string.Join(' ', typed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        var email = string.Concat(typed.Where(c => !char.IsWhiteSpace(c))).ToLowerInvariant();
        var terms = ContractScanKeywordExtractor.SearchTerms(typed);
        var digits = terms.Where(t => t.All(char.IsDigit)).ToList();
        var hits = new List<Hit>();

        // Values written in the contract itself, on its current version.
        var values = from d in documents
                     join v in db.ContractDynamicValues on new { d.ContractDocumentId, VersionId = d.ContractDocumentCurrentVersionId }
                         equals new { v.ContractDocumentId, VersionId = (long?)v.ContractVersionId }
                     select new { d.ContractDocumentId, v.FieldKey, v.FieldValue };

        if (Wants(ScanKeywordType.ContractCode))
        {
            foreach (var term in terms)
            {
                hits.AddRange(await documents.Where(d => d.ContractDocumentCode != null && d.ContractDocumentCode.Contains(term))
                    .Select(d => new Hit(d.ContractDocumentId, nameof(ScanKeywordType.ContractCode), d.ContractDocumentCode!, ContractSource))
                    .Take(PerQuery).ToListAsync(ct));
            }
        }

        if (only is null)
        {
            hits.AddRange(await documents.Where(d => EF.Functions.Collate(d.ContractDocumentName, LooseCollation).Contains(name))
                .Select(d => new Hit(d.ContractDocumentId, ContractNameType, d.ContractDocumentName, ContractSource))
                .Take(PerQuery).ToListAsync(ct));
        }

        if (Wants(ScanKeywordType.CustomerName))
        {
            hits.AddRange(await (from d in documents join c in db.Customers on d.CustomerId equals c.CustomerId
                                 where EF.Functions.Collate(c.CustomerDisplayName, LooseCollation).Contains(name)
                                 select new Hit(d.ContractDocumentId, nameof(ScanKeywordType.CustomerName), c.CustomerDisplayName, CustomerSource))
                .Take(PerQuery).ToListAsync(ct));
            hits.AddRange(await values.Where(v => NameFields.Contains(v.FieldKey) && v.FieldValue != null
                    && EF.Functions.Collate(v.FieldValue, LooseCollation).Contains(name))
                .Select(v => new Hit(v.ContractDocumentId, nameof(ScanKeywordType.CustomerName), v.FieldValue!, ContractSource))
                .Take(PerQuery).ToListAsync(ct));
        }

        foreach (var term in digits)
        {
            if (Wants(ScanKeywordType.PhoneNumber))
            {
                hits.AddRange(await (from d in documents join c in db.Customers on d.CustomerId equals c.CustomerId
                                     where c.CustomerPersonalPhone.Contains(term)
                                     select new Hit(d.ContractDocumentId, nameof(ScanKeywordType.PhoneNumber), c.CustomerPersonalPhone, CustomerSource))
                    .Take(PerQuery).ToListAsync(ct));
                hits.AddRange(await values.Where(v => PhoneFields.Contains(v.FieldKey) && v.FieldValue != null
                        && v.FieldValue.Replace(" ", "").Replace(".", "").Replace("-", "").Contains(term))
                    .Select(v => new Hit(v.ContractDocumentId, nameof(ScanKeywordType.PhoneNumber), v.FieldValue!, ContractSource))
                    .Take(PerQuery).ToListAsync(ct));
            }

            if (Wants(ScanKeywordType.TaxCode))
            {
                hits.AddRange(await (from d in documents join c in db.Customers on d.CustomerId equals c.CustomerId
                                     where c.CustomerPersonalTaxIdentifier != null && c.CustomerPersonalTaxIdentifier.Contains(term)
                                     select new Hit(d.ContractDocumentId, nameof(ScanKeywordType.TaxCode), c.CustomerPersonalTaxIdentifier!, CustomerSource))
                    .Take(PerQuery).ToListAsync(ct));
                hits.AddRange(await (from d in documents join b in db.CustomerBusinesses on d.CustomerId equals b.CustomerId
                                     where b.CustomerBusinessCompanyTaxIdentifier.Contains(term)
                                     select new Hit(d.ContractDocumentId, nameof(ScanKeywordType.TaxCode), b.CustomerBusinessCompanyTaxIdentifier, CustomerSource))
                    .Take(PerQuery).ToListAsync(ct));
                hits.AddRange(await values.Where(v => TaxFields.Contains(v.FieldKey) && v.FieldValue != null
                        && v.FieldValue.Replace(" ", "").Replace("-", "").Contains(term))
                    .Select(v => new Hit(v.ContractDocumentId, nameof(ScanKeywordType.TaxCode), v.FieldValue!, ContractSource))
                    .Take(PerQuery).ToListAsync(ct));
            }
        }

        if (Wants(ScanKeywordType.Email) && email.Length >= 3)
        {
            hits.AddRange(await (from d in documents join c in db.Customers on d.CustomerId equals c.CustomerId
                                 where c.CustomerEmail != null && c.CustomerEmail.Contains(email)
                                 select new Hit(d.ContractDocumentId, nameof(ScanKeywordType.Email), c.CustomerEmail!, CustomerSource))
                .Take(PerQuery).ToListAsync(ct));
            hits.AddRange(await values.Where(v => EmailFields.Contains(v.FieldKey) && v.FieldValue != null && v.FieldValue.Contains(email))
                .Select(v => new Hit(v.ContractDocumentId, nameof(ScanKeywordType.Email), v.FieldValue!, ContractSource))
                .Take(PerQuery).ToListAsync(ct));
        }

        if (hits.Count == 0) return [];
        var ids = hits.Select(h => h.DocumentId).Distinct().ToList();
        var rows = await (from d in db.ContractDocuments.AsNoTracking()
                          join c in db.Customers on d.CustomerId equals c.CustomerId
                          join v in db.ContractVersions on d.ContractDocumentCurrentVersionId equals (long?)v.ContractVersionId into versions
                          from v in versions.DefaultIfEmpty()
                          where ids.Contains(d.ContractDocumentId)
                          select new { d.ContractDocumentId, d.ContractDocumentCode, d.ContractDocumentName, c.CustomerDisplayName,
                              d.ContractDocumentStatus, VersionNo = (long?)v.ContractVersionVersionNo, d.CreatedDate, d.UpdatedDate,
                              // The draft screen opens a contract only with its current version and content snapshot;
                              // a record made another way (e.g. inserted for a test) is opened on its paper-signing page.
                              HasDraft = db.ContractDynamicValues.Any(x => x.ContractDocumentId == d.ContractDocumentId
                                  && x.ContractVersionId == d.ContractDocumentCurrentVersionId && x.FieldKey == InternalRepository.SnapshotKey) })
            .ToListAsync(ct);

        // Days are Vietnam time, as for scans.
        bool Within(DateTime at) { var day = DateOnly.FromDateTime(at.AddHours(7)); return (from is null || day >= from) && (to is null || day <= to); }
        return rows
            .Where(r => Within(r.CreatedDate))
            .OrderByDescending(r => r.UpdatedDate ?? r.CreatedDate)
            .Take(MaxResults)
            .Select(r => new OnlineContractSearchResultDto(r.ContractDocumentId, r.ContractDocumentCode, r.ContractDocumentName, r.CustomerDisplayName,
                Enum.IsDefined(typeof(ContractDocumentStatus), r.ContractDocumentStatus) ? ((ContractDocumentStatus)r.ContractDocumentStatus).ToString() : r.ContractDocumentStatus.ToString(),
                r.VersionNo, r.UpdatedDate ?? r.CreatedDate,
                hits.Where(h => h.DocumentId == r.ContractDocumentId)
                    .GroupBy(h => (h.Type, h.Value)).Select(g => new ScanSearchMatchDto(g.Key.Type, g.Key.Value, null, g.First().Source)).ToList(),
                r.HasDraft))
            .ToList();
    }
}
