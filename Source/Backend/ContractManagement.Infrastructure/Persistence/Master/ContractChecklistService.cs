using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ContractManagement.Application.Features.Internal;
using ContractManagement.Domain.Entities.Master;
using ContractManagement.Domain.Internal;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Infrastructure.Persistence.Master;

public sealed record ChecklistInput(string? RowVersion, string[] Rules);
public sealed record ChecklistDecision(string RowVersion, string VersionRowVersion, string? ChecklistRowVersion, string ContentHash, string Rule, string Decision, string Reason);
public sealed record ChecklistRule(string Key, string Label, string[] Phrases);
public sealed record ChecklistResolution(string Decision, string Reason, string Actor, DateTime At);
public sealed record ChecklistFinding(string Key, string Label, bool Found, string? Location, string? Excerpt, ChecklistResolution? Resolution);
public sealed record ChecklistView(bool Configured, string[] Rules, string? RowVersion, string? ContentHash, List<ChecklistFinding> Findings);

public sealed class ContractChecklistService(MasterDbContext db, InternalWordTemplates word)
{
    public static readonly ChecklistRule[] Catalog = [
        new("scope", "Phạm vi công việc", ["phạm vi công việc", "công việc theo hợp đồng", "đối tượng hợp đồng"]),
        new("payment", "Giá trị và thanh toán", ["thanh toán", "giá trị hợp đồng"]),
        new("duration", "Thời hạn thực hiện", ["thời hạn thực hiện", "tiến độ thực hiện", "thời gian thực hiện"]),
        new("acceptance", "Nghiệm thu hoặc bàn giao", ["nghiệm thu", "bàn giao"]),
        new("warranty", "Bảo hành", ["bảo hành"]),
        new("responsibility", "Trách nhiệm các bên", ["trách nhiệm", "quyền và nghĩa vụ"]),
        new("termination", "Chấm dứt hợp đồng", ["chấm dứt", "hủy hợp đồng"]),
        new("disputes", "Giải quyết tranh chấp", ["tranh chấp"]),
        new("effect", "Hiệu lực hợp đồng", ["hiệu lực"])];

    public static async Task Capture(MasterDbContext db, long templateId, long versionId, long? previousVersion, CancellationToken ct)
    {
        var source = previousVersion.HasValue
            ? await db.Set<ContractChecklist>().AsNoTracking().SingleOrDefaultAsync(x => x.VersionId == previousVersion, ct)
            : await db.Set<ContractChecklist>().AsNoTracking().SingleOrDefaultAsync(x => x.TemplateId == templateId && x.VersionId == null, ct);
        db.Set<ContractChecklist>().Add(new() { TemplateId = templateId, VersionId = versionId, Configured = source?.Configured ?? false, Rules = source?.Rules ?? "[]" });
    }

    public async Task<ChecklistView> Template(long id, InternalUser actor, CancellationToken ct)
    {
        if (!actor.CanManageTemplates) throw new InternalAppException(403, "Chỉ Hành chính quản lý danh sách rà soát.");
        if (!await db.ContractTemplates.AnyAsync(x => x.ContractTemplateId == id && x.ContractTemplateDocumentType == 1, ct))
            throw new InternalAppException(404, "Không tìm thấy mẫu hợp đồng.");
        var row = await db.Set<ContractChecklist>().AsNoTracking().SingleOrDefaultAsync(x => x.TemplateId == id && x.VersionId == null, ct);
        return new(row?.Configured ?? false, Rules(row), Token(row), null, []);
    }

    public async Task SaveTemplate(long id, ChecklistInput input, InternalUser actor, CancellationToken ct)
    {
        await ReviewData.InTransaction(db, async () =>
        {
            await Template(id, actor, ct);
            ValidateRules(input.Rules);
            // Serialize initial configuration as well as subsequent updates for the same template.
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT contract_template_id FROM CONTRACT_TEMPLATE WITH (UPDLOCK,HOLDLOCK) WHERE contract_template_id={id}", ct);
            var row = await db.Set<ContractChecklist>().SingleOrDefaultAsync(x => x.TemplateId == id && x.VersionId == null, ct);
            if (row is not null) ReviewData.Expect(row.RowVersion, input.RowVersion);
            else if (input.RowVersion is not null) throw new InternalAppException(409, "Cấu hình đã thay đổi.");
            row ??= new() { TemplateId = id };
            if (row.Id == 0) db.Add(row);
            row.Rules = JsonSerializer.Serialize(input.Rules); row.Configured = true;
            Audit(id, "CHECKLIST_TEMPLATE", actor, new { rules = input.Rules });
            await db.SaveChangesAsync(ct);
            return true;
        }, ct);
    }

    public async Task<ChecklistView> Inspect(long id, InternalUser actor, CancellationToken ct)
    {
        var doc = await db.ContractDocuments.AsNoTracking().SingleOrDefaultAsync(x => x.ContractDocumentId == id && x.TenantId == null, ct)
            ?? throw new InternalAppException(404, "Không tìm thấy hợp đồng.");
        InternalDraftService.RequireAccess(actor, doc.EmployeeId);
        var raw = await db.ContractDynamicValues.Where(x => x.ContractDocumentId == id && x.ContractVersionId == doc.ContractDocumentCurrentVersionId && x.FieldKey == InternalRepository.SnapshotKey).Select(x => x.FieldValue).SingleAsync(ct);
        var row = await db.Set<ContractChecklist>().AsNoTracking().SingleOrDefaultAsync(x => x.VersionId == doc.ContractDocumentCurrentVersionId, ct);
        return await InspectSnapshot(raw, row, ct);
    }

    internal async Task<ChecklistView> InspectSnapshot(string raw, ContractChecklist? row, CancellationToken ct)
    {
        var hash = SnapshotHash.Compute(raw);
        if (row?.Configured != true || Rules(row).Length == 0)
            return new(row?.Configured ?? false, Rules(row), Token(row), hash, []);
        var content = JsonSerializer.Deserialize<DraftContent>(raw, InternalRepository.Json)!;
        var pieces = content.TemplateSnapshot?.WordStorageKey is not null
            ? InternalWordDocument.Blocks(await word.ReadWord(content.TemplateSnapshot, ct)).Where(b => b.Location.StartsWith("Nội dung")).Select(b => (b.Id, b.Text)).ToList()
            : content.Terms.Select(t => (t.Key, t.Title + "\n" + t.Content)).ToList();
        var resolutions = row?.ContentHash == hash ? JsonSerializer.Deserialize<Dictionary<string, ChecklistResolution>>(row.Resolutions)! : [];
        return new(row?.Configured ?? false, Rules(row), Token(row), hash, Analyze(Rules(row), pieces, resolutions));
    }

    public static List<ChecklistFinding> Analyze(string[] rules, List<(string Id, string Text)> pieces, Dictionary<string, ChecklistResolution> resolutions)
    {
        var results = new List<ChecklistFinding>();
        foreach (var rule in Catalog.Where(rule => rules.Contains(rule.Key)))
        {
            var index = -1;
            string? excerpt = null;
            var found = false;
            for (var candidate = 0; candidate < pieces.Count; candidate++)
            {
                var text = pieces[candidate].Text;
                if (!rule.Phrases.Any(phrase => Normalize(text).Contains(Normalize(phrase)))) continue;
                if (index < 0) { index = candidate; excerpt = text; }
                var bodyStart = text.IndexOf('\n');
                var body = bodyStart >= 0 ? text[(bodyStart + 1)..] : text;
                var substantive = Regex.Matches(body, @"\p{L}+").Count >= 12 && !Heading(text);
                // A heading can introduce the following paragraph/cell; another checklist heading is not its body.
                if (!substantive && bodyStart < 0 && candidate + 1 < pieces.Count)
                {
                    var next = pieces[candidate + 1].Text;
                    var heading = Heading(next)
                        || Catalog.Any(other => other.Phrases.Any(phrase => Normalize(next.Trim()).TrimEnd(':', '.') == Normalize(phrase)));
                    substantive = !heading && Regex.Matches(next, @"\p{L}+").Count >= 12;
                    if (substantive) text += "\n" + next;
                }
                if (substantive) { index = candidate; excerpt = text; found = true; break; }
            }
            results.Add(new(rule.Key, rule.Label, found, index < 0 ? null : pieces[index].Id, excerpt?[..Math.Min(excerpt.Length, 600)], resolutions.GetValueOrDefault(rule.Key)));
        }
        return results;
    }

    public async Task Decide(long id, ChecklistDecision input, InternalUser actor, CancellationToken ct)
    {
        if (!actor.CanCheck) throw new InternalAppException(403, "Chỉ Hành chính xác nhận rà soát.");
        await ReviewData.InTransaction(db, async () =>
        {
            // Serialize decisions even for legacy versions without a checklist row.
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT contract_document_id FROM CONTRACT_DOCUMENTS WITH (UPDLOCK,HOLDLOCK) WHERE contract_document_id={id}", ct);
            var view = await Inspect(id, actor, ct);
            var doc = await db.ContractDocuments.SingleAsync(x => x.ContractDocumentId == id, ct);
            var version = await db.ContractVersions.SingleAsync(x => x.ContractVersionId == doc.ContractDocumentCurrentVersionId, ct);
            ReviewData.Expect(doc.RowVersion, input.RowVersion);
            ReviewData.Expect(version.ContractVersionRowversion, input.VersionRowVersion);
            if (view.ContentHash != input.ContentHash) throw new InternalAppException(409, "Nội dung đã thay đổi. Rà soát lại phiên bản mới.");
            var row = await db.Set<ContractChecklist>().SingleOrDefaultAsync(x => x.VersionId == version.ContractVersionId, ct);
            if (row is not null) ReviewData.Expect(row.RowVersion, input.ChecklistRowVersion);
            else if (input.ChecklistRowVersion is not null) throw new InternalAppException(409, "Danh sách đã thay đổi.");
            row ??= new() { VersionId = version.ContractVersionId, TemplateId = doc.ContractTemplateId };
            if (row.Id == 0) db.Add(row);
            if (input.Decision == "APPLY_TEMPLATE")
            {
                if (!ContractReviewPolicy.CanEdit(doc.ContractDocumentStatus, version.ContractVersionIslocked)) throw new InternalAppException(409, "Chỉ áp dụng cấu hình mới khi đang chỉnh sửa nháp.");
                var source = await db.Set<ContractChecklist>().AsNoTracking().SingleOrDefaultAsync(x => x.TemplateId == row.TemplateId && x.VersionId == null, ct);
                if (source?.Configured != true) throw new ArgumentException("Mẫu chưa cấu hình rà soát.");
                row.Rules = source.Rules; row.Configured = true; row.Resolutions = "{}";
            }
            else
            {
                if (!Rules(row).Contains(input.Rule) || input.Decision is not ("REVIEWED" or "NOT_APPLICABLE")) throw new ArgumentException("Thao tác rà soát không hợp lệ.");
                var reason = InternalDraftPolicy.Text(input.Reason, 2000);
                if (reason.Length == 0) throw new ArgumentException("Nhập lý do xác nhận.");
                var resolutions = row.ContentHash == view.ContentHash ? JsonSerializer.Deserialize<Dictionary<string, ChecklistResolution>>(row.Resolutions)! : [];
                resolutions[input.Rule] = new(input.Decision, reason, actor.Name, DateTime.UtcNow);
                row.Resolutions = JsonSerializer.Serialize(resolutions);
            }
            row.ContentHash = view.ContentHash;
            doc.UpdatedDate = DateTime.UtcNow;
            Audit(id, "CHECKLIST_REVIEW", actor, new { version = version.ContractVersionVersionNo, input.Rule, input.Decision, input.Reason, hash = view.ContentHash });
            await db.SaveChangesAsync(ct);
            return true;
        }, ct);
    }

    private static string Normalize(string value) => string.Concat(value.Normalize(NormalizationForm.FormD).Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)).ToLowerInvariant().Replace('đ', 'd');
    public static void ValidateRules(string[]? rules)
    {
        if (rules is null || rules.Distinct().Count() != rules.Length || rules.Any(key => !Catalog.Any(rule => rule.Key == key)))
            throw new ArgumentException("Chọn danh sách điều khoản cần rà soát cho mẫu hợp đồng (có thể chọn không áp dụng mục nào).");
    }
    private static bool Heading(string text) => !text.Contains('\n') && text.Length < 180
        && Regex.IsMatch(Normalize(text.Trim()), @"^(dieu\s+\d|phu luc)");
    private static string[] Rules(ContractChecklist? row) => row is null ? [] : JsonSerializer.Deserialize<string[]>(row.Rules)!;
    private static string? Token(ContractChecklist? row) => row is null ? null : Convert.ToBase64String(row.RowVersion);
    private void Audit(long id, string action, InternalUser actor, object data) => db.AuditLogs.Add(new() { EntityName = "CONTRACT_CHECKLIST", EntityId = id.ToString(), ActorId = actor.Id, ActorType = "EMPLOYEE", ActionType = action, CreatedAt = DateTime.UtcNow, NewData = JsonSerializer.Serialize(data) });
}
