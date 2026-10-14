namespace Contrack.Domain.Common;

/// <summary>Lớp cơ sở cho mọi entity: khóa chính và thời điểm tạo/cập nhật.</summary>
public abstract class AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}
