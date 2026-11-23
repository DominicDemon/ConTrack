using System.Text.Json;
using ContractManagement.Application.Features.Internal;
using ContractManagement.Domain.Entities.Master;
using ContractManagement.Domain.Internal;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Infrastructure.Persistence.Master;

/// <summary>Hành chính's document types (DOCUMENT_CATEGORY): add, rename, change the sending rule, hide or delete an unused one.
/// The contract type (1) is fixed: signing, child templates and contract numbers depend on it.</summary>
public sealed class DocumentCategories(MasterDbContext db)
{
    public static DocumentRule ToRule(DocumentCategory c) => new(c.DocumentCategoryId, c.EffectiveLabel, c.EffectiveRequired, c.ExpireShown, c.ExpireLabel,
        c.ExpireRequired, c.ItemsLabel, c.ItemsRequired, c.ItemPrices, c.AmountRequired, c.Milestones, c.MilestonesRequired, c.ParentMilestone, c.DocumentCategoryName);

    /// <summary>The rule of every type, inactive ones included: templates and drafts of a hidden type still need theirs.</summary>
    public static async Task<Dictionary<int, DocumentRule>> Rules(MasterDbContext db, CancellationToken ct) =>
        (await db.DocumentCategories.AsNoTracking().ToListAsync(ct)).ToDictionary(c => c.DocumentCategoryId, ToRule);

    public async Task<List<DocumentCategoryRow>> List(CancellationToken ct)
    {
        var used = await db.ContractTemplates.AsNoTracking().GroupBy(t => t.ContractTemplateDocumentType)
            .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        return (await db.DocumentCategories.AsNoTracking().OrderBy(c => c.DocumentCategoryId).ToListAsync(ct))
            .Select(c => Row(c, used.GetValueOrDefault(c.DocumentCategoryId))).ToList();
    }

    public async Task<DocumentCategoryRow> Create(DocumentCategoryInput input, InternalUser actor, CancellationToken ct)
    {
        Require(actor);
        var category = new DocumentCategory { CreatedDate = DateTime.UtcNow };
        await Apply(category, input, ct);
        db.DocumentCategories.Add(category);
        await db.SaveChangesAsync(ct);
        Audit("CREATE", category, actor);
        await db.SaveChangesAsync(ct);
        return Row(category, 0);
    }

    public async Task<DocumentCategoryRow> Update(int id, DocumentCategoryInput input, InternalUser actor, CancellationToken ct)
    {
        Require(actor);
        var category = await Find(id, ct);
        await Apply(category, input, ct);
        category.UpdatedDate = DateTime.UtcNow;
        Audit("UPDATE", category, actor);
        await db.SaveChangesAsync(ct);
        return Row(category, await db.ContractTemplates.CountAsync(t => t.ContractTemplateDocumentType == id, ct));
    }

    /// <summary>Only a type no template uses: documents keep their type id, so a used type is hidden instead.</summary>
    public async Task Delete(int id, InternalUser actor, CancellationToken ct)
    {
        Require(actor);
        var category = await Find(id, ct);
        if (await db.ContractTemplates.AnyAsync(t => t.ContractTemplateDocumentType == id, ct) || await db.ContractDocuments.AnyAsync(d => d.ContractDocumentType == id, ct))
            throw new InternalAppException(409, "Loại văn bản đang có mẫu hoặc văn bản dùng. Hãy ẩn loại này thay vì xóa.");
        Audit("DELETE", category, actor);
        db.DocumentCategories.Remove(category);
        await db.SaveChangesAsync(ct);
    }

    private async Task<DocumentCategory> Find(int id, CancellationToken ct)
    {
        if (id == DocumentRules.Contract) throw new InternalAppException(409, "Loại Hợp đồng là loại gốc của luồng ký, không sửa hay xóa được.");
        return await db.DocumentCategories.SingleOrDefaultAsync(c => c.DocumentCategoryId == id, ct)
            ?? throw new InternalAppException(404, "Không tìm thấy loại văn bản.");
    }

    private async Task Apply(DocumentCategory c, DocumentCategoryInput input, CancellationToken ct)
    {
        string Label(string? value, string what, bool required = true)
        {
            var text = value?.Trim() ?? "";
            if (required && text.Length == 0) throw new ArgumentException($"Nhập {what}.");
            if (text.Length > 100) throw new ArgumentException($"{what} tối đa 100 ký tự.");
            return text;
        }
        var name = input.Name?.Trim() ?? "";
        if (name.Length is 0 or > 200) throw new ArgumentException("Tên loại văn bản phải có từ 1 đến 200 ký tự.");
        if (await db.DocumentCategories.AnyAsync(x => x.DocumentCategoryName == name && x.DocumentCategoryId != c.DocumentCategoryId, ct))
            throw new ArgumentException("Đã có loại văn bản cùng tên.");
        c.DocumentCategoryName = name;
        c.IsActive = input.Active;
        c.EffectiveLabel = Label(input.EffectiveLabel, "tên ngày thứ nhất");
        c.EffectiveRequired = input.EffectiveRequired;
        c.ExpireShown = input.ExpireShown;
        c.ExpireLabel = input.ExpireShown ? Label(input.ExpireLabel, "tên ngày thứ hai") : "";
        c.ExpireRequired = input.ExpireShown && input.ExpireRequired;
        c.ItemsLabel = Label(input.ItemsLabel, "tên bảng hạng mục");
        c.ItemsRequired = input.ItemsRequired;
        c.ItemPrices = input.ItemPrices;
        // An amount is the sum of the prices: without prices there is nothing to add up.
        c.AmountRequired = input.ItemPrices && input.AmountRequired;
        c.Milestones = input.ItemPrices && input.Milestones;
        c.MilestonesRequired = c.Milestones ? input.MilestonesRequired : false;
        c.ParentMilestone = input.ParentMilestone;
    }

    private static void Require(InternalUser actor)
    {
        if (!actor.CanManageTemplates) throw new InternalAppException(403, "Chỉ Hành chính được quản lý loại văn bản.");
    }

    private void Audit(string action, DocumentCategory c, InternalUser actor) => db.AuditLogs.Add(new()
    {
        ActionType = action, EntityName = "DOCUMENT_CATEGORY", EntityId = c.DocumentCategoryId.ToString(), ActorType = "EMPLOYEE", ActorId = actor.Id,
        CreatedAt = DateTime.UtcNow, NewData = action == "DELETE" ? null : JsonSerializer.Serialize(ToRule(c), InternalRepository.Json)
    });

    private static DocumentCategoryRow Row(DocumentCategory c, int templates) =>
        new(c.DocumentCategoryId, c.DocumentCategoryName, c.IsActive, c.DocumentCategoryId <= DocumentRules.PaymentRequest,
            c.DocumentCategoryId == DocumentRules.Contract, templates, ToRule(c));
}
