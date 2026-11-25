using System.Text.Json;
using ContractManagement.Application.Features.Internal;
using ContractManagement.Domain.Internal;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Infrastructure.Persistence.Master;

public sealed class ContractFormatSettings(MasterDbContext db)
{
    internal static ContractFormat Parse(string? json) =>
        json is null ? ContractFormat.Default : JsonSerializer.Deserialize<ContractFormat>(json, InternalRepository.Json)!.Validated();

    internal static async Task<ContractFormat> Read(MasterDbContext db, long id, CancellationToken ct) =>
        Parse(await db.ContractTemplates.Where(t => t.ContractTemplateId == id).Select(t => t.PrintFormatJson).FirstOrDefaultAsync(ct));

    internal static ContractFormat ForWord(string? json) =>
        Parse(json) is { WordOverrides: not null } configured ? configured : ContractFormat.Default with { WordOverrides = [] };

    private async Task<ContractManagement.Domain.Entities.Master.ContractTemplate> Find(long id, CancellationToken ct) =>
        await db.ContractTemplates.SingleOrDefaultAsync(t => t.ContractTemplateId == id, ct)
            ?? throw new InternalAppException(404, "Không tìm thấy mẫu.");

    private Task<bool> IsWord(long id, CancellationToken ct) =>
        db.FileStorages.AnyAsync(f => f.FileStorageObjectid == id && f.FileStorageObjecttype == "INTERNAL_TEMPLATE_WORD", ct);

    internal static ContractFormat ValidateWord(ContractFormat input)
    {
        var overrides = input.WordOverrides ?? [];
        if (overrides.Any(key => key is not ("font" or "size" or "title" or "margins" or "spacing")))
            throw new ArgumentException("Tham số Word không hợp lệ.");
        return input.Validated() with { WordOverrides = overrides.Distinct().ToArray() };
    }

    public async Task<object> Get(long id, CancellationToken ct)
    {
        var template = await Find(id, ct);
        var word = await IsWord(id, ct);
        return new { name = template.ContractTemplateName, word, format = word ? ForWord(template.PrintFormatJson) : Parse(template.PrintFormatJson), fonts = ContractFormat.Fonts };
    }

    public async Task<ContractFormat> Save(long id, ContractFormat input, InternalUser actor, CancellationToken ct)
    {
        if (!actor.CanManageTemplates) throw new InternalAppException(403, "Chỉ Hành chính được thiết lập định dạng hợp đồng.");
        var template = await Find(id, ct);
        var format = await IsWord(id, ct) ? ValidateWord(input) : input.Validated() with { WordOverrides = null };
        var json = JsonSerializer.Serialize(format, InternalRepository.Json);
        db.AuditLogs.Add(new() { ActionType = "UPDATE", EntityName = "CONTRACT_TEMPLATE", EntityId = id.ToString(),
            ActorType = "EMPLOYEE", ActorId = actor.Id, CreatedAt = DateTime.UtcNow, OldData = template.PrintFormatJson, NewData = json });
        template.PrintFormatJson = json;
        template.UpdatedDate = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return format;
    }
}
