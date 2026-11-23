using System.Text.Json;
using ContractManagement.Application.Features.Internal;
using ContractManagement.Domain.Internal;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Infrastructure.Persistence.Master;

/// <summary>The company's contract print format (Thiết lập tham số hợp đồng): read by everyone, changed by Hành chính.</summary>
public sealed class ContractFormatSettings(MasterDbContext db)
{
    public const int SettingId = 1;

    /// <summary>The saved format, or the default layout when none was saved (or the row cannot be read).</summary>
    public static async Task<ContractFormat> Read(MasterDbContext db, CancellationToken ct)
    {
        var json = await db.ContractFormatSettings.AsNoTracking().Where(s => s.SettingId == SettingId).Select(s => s.SettingValue).FirstOrDefaultAsync(ct);
        if (json is null) return ContractFormat.Default;
        try { return JsonSerializer.Deserialize<ContractFormat>(json, InternalRepository.Json)?.Validated() ?? ContractFormat.Default; }
        catch (Exception e) when (e is JsonException or ArgumentException) { return ContractFormat.Default; }
    }

    public Task<ContractFormat> Get(CancellationToken ct) => Read(db, ct);

    public async Task<ContractFormat> Save(ContractFormat input, InternalUser actor, CancellationToken ct)
    {
        if (!actor.CanManageTemplates) throw new InternalAppException(403, "Chỉ Hành chính được thiết lập định dạng hợp đồng.");
        var format = (input ?? throw new ArgumentException("Thiếu thiết lập.")).Validated();
        var json = JsonSerializer.Serialize(format, InternalRepository.Json);
        var row = await db.ContractFormatSettings.SingleOrDefaultAsync(s => s.SettingId == SettingId, ct);
        var old = row?.SettingValue;
        if (row is null) db.ContractFormatSettings.Add(row = new() { SettingId = SettingId, SettingValue = json });
        row.SettingValue = json; row.UpdatedDate = DateTime.UtcNow; row.UpdatedEmployeeId = actor.Id;
        db.AuditLogs.Add(new() { ActionType = "UPDATE", EntityName = "CONTRACT_FORMAT_SETTING", EntityId = SettingId.ToString(), ActorType = "EMPLOYEE",
            ActorId = actor.Id, CreatedAt = DateTime.UtcNow, OldData = old, NewData = json });
        await db.SaveChangesAsync(ct);
        return format;
    }
}
