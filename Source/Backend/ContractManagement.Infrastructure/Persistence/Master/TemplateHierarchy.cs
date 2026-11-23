using ContractManagement.Application.Features.Internal;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Infrastructure.Persistence.Master;

/// <summary>
/// Document templates form one level: a contract template (type 1) may have child templates (types 2-5: appendix,
/// handover record, receipt, payment request) attached to it. Drafts made from a child template are attached to a
/// contract made from its parent.
/// </summary>
public static class TemplateHierarchy
{
    public const int Contract = 1;
    public static readonly string[] TypeNames = ["Hợp đồng", "Phụ lục", "Biên bản bàn giao", "Biên lai", "Đề nghị thanh toán"];

    public static void ValidateType(int documentType)
    {
        if (documentType < 1) throw new ArgumentException("Chọn loại văn bản hợp lệ.");
    }

    /// <summary>Throws unless <paramref name="documentType"/> and <paramref name="parentId"/> fit the hierarchy for the template <paramref name="id"/> (null for a new one).</summary>
    public static async Task Validate(MasterDbContext db, long? id, int documentType, long? parentId, CancellationToken ct)
    {
        ValidateType(documentType);
        // A type from the Loại văn bản page (DOCUMENT_CATEGORY); a template keeps a type that was hidden after it was made.
        if (!await db.DocumentCategories.AnyAsync(c => c.DocumentCategoryId == documentType
                && (c.IsActive || (id != null && db.ContractTemplates.Any(t => t.ContractTemplateId == id && t.ContractTemplateDocumentType == documentType))), ct))
            throw new ArgumentException("Loại văn bản không tồn tại hoặc đã ẩn.");
        if (parentId is long parent)
        {
            if (documentType == Contract)
                throw new ArgumentException("Mẫu hợp đồng không gắn vào mẫu khác; chỉ phụ lục, biên bản, biên lai, đề nghị thanh toán là mẫu con.");
            if (parent == id) throw new ArgumentException("Mẫu không thể là mẫu cha của chính nó.");
            if (!await db.ContractTemplates.AnyAsync(t => t.ContractTemplateId == parent && t.IsActive && t.ContractTemplateDocumentType == Contract, ct))
                throw new InternalAppException(404, "Không tìm thấy mẫu hợp đồng cha đang dùng.");
        }
        // A contract template keeps its children: it cannot become a child type while they point at it.
        if (id is long own && documentType != Contract
            && await db.ContractTemplates.AnyAsync(t => t.ParentTemplateId == own, ct))
            throw new ArgumentException("Mẫu này đang có mẫu con; bỏ gắn các mẫu con trước khi đổi sang loại khác Hợp đồng.");
    }
}
