using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using ContractManagement.Attributes;
using ContractManagement.Filter;
using ContractManagement.API.Common.Security;
using ContractManagement.Infrastructure.Persistence.Central;
using ContractManagement.Infrastructure.Persistence.Central.Saas;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Controllers.Admin;

[ApiController, Route("api/admin/plans"), AllowWithoutTenant, SystemAdminAuthorize]
public sealed class PlansController(CentralDbContext db) : ControllerBase
{
    public class PlanInput
    {
        [Required, StringLength(50), RegularExpression("^[A-Z0-9_-]+$")] public string PlanCode { get; set; } = "";
        [Required, StringLength(150)] public string PlanName { get; set; } = "";
        [StringLength(500)] public string? PlanDescription { get; set; }
        public bool IsPaid { get; set; }
        public bool AllowsSupportTickets { get; set; }
        [Range(1, int.MaxValue)] public int MaxUsers { get; set; }
        [Range(1, int.MaxValue)] public int MaxContracts { get; set; }
        [Range(1, int.MaxValue)] public int MaxScanPages { get; set; }
        [Range(1, 24)] public int? DurationMonths { get; set; }
        [Range(1, 730)] public int DurationDays { get; set; }
        [Range(0, 999999999999999)] public decimal Price { get; set; }
        public int Revision { get; set; }
    }
    public sealed class PriceInput
    {
        [Range(0, 999999999999999)] public decimal Price { get; set; }
        public int Revision { get; set; }
    }
    public sealed class StatusInput { public bool Active { get; set; } public int Revision { get; set; } }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var plans = await db.Plans.AsNoTracking().Include(p => p.Versions).OrderBy(p => p.PlanId).ToListAsync(ct);
        var counts = await db.Subscriptions.Where(s => s.Status == "ACTIVE" && s.TenantId != null && s.StartAt <= now && s.EndAt > now)
            .GroupBy(s => s.PlanVersion.PlanId).Select(g => new { Id = g.Key, Count = g.Select(s => s.TenantId).Distinct().Count() }).ToDictionaryAsync(x => x.Id, x => x.Count, ct);
        return Ok(plans.Select(p => Map(p, counts.GetValueOrDefault(p.PlanId))));
    }
    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
    {
        var p = await db.Plans.AsNoTracking().Include(x => x.Versions).SingleOrDefaultAsync(x => x.PlanId == id, ct);
        if (p == null) return NotFound();
        var now = DateTime.UtcNow;
        var count = await db.Subscriptions.Where(s => s.PlanVersion.PlanId == id && s.Status == "ACTIVE" && s.TenantId != null && s.StartAt <= now && s.EndAt > now).Select(s => s.TenantId).Distinct().CountAsync(ct);
        return Ok(Map(p, count));
    }
    [HttpGet("{id:long}/audits")]
    public async Task<IActionResult> Audits(long id, CancellationToken ct) => Ok(await db.PlanAudits.AsNoTracking().Where(a => a.PlanId == id).OrderByDescending(a => a.Id).Take(200).ToListAsync(ct));

    [HttpPost]
    public Task<IActionResult> Create(PlanInput input, CancellationToken ct) => db.Database.CreateExecutionStrategy().ExecuteAsync<IActionResult>(async () =>
    {
        db.ChangeTracker.Clear();
        if (string.IsNullOrWhiteSpace(input.PlanName) || !ValidPrice(input.IsPaid, input.Price) || (input.IsPaid && input.DurationMonths == null) || (!input.IsPaid && input.DurationMonths != null))
            return BadRequest(new { message = "Tên, giá hoặc thời hạn không hợp lệ." });
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        if (await db.Plans.AnyAsync(p => p.PlanCode == input.PlanCode, ct)) return Conflict(new { message = "Mã gói đã tồn tại." });
        if (input.IsPaid && await db.PlanVersions.AnyAsync(v => v.Plan.IsPaid && v.DurationMonths == input.DurationMonths && v.Status == "ACTIVE", ct))
            return Conflict(new { message = "Đã có gói với thời hạn này. Hãy cấu hình giá cho gói hiện có." });
        var typeId = await db.SaasTypes.Where(t => t.SaasTypeStatus == "ACTIVE").Select(t => t.SaasTypeId).FirstAsync(ct);
        var p = new SaasPlan { SaasTypeId = typeId, PlanCode = input.PlanCode, IsPaid = input.IsPaid, PlanStatus = "ACTIVE", CreatedAt = DateTime.UtcNow, CreatedBy = Actor() };
        Apply(p, input);
        p.Versions.Add(new SaasPlanVersion { VersionNumber = 1, Price = input.Price, Currency = "VND", DurationDays = input.DurationDays, DurationMonths = input.DurationMonths,
            BillingCycle = input.IsPaid ? Cycle(input.DurationMonths!.Value) : "TRIAL", EffectiveFrom = DateTime.UtcNow, Status = "ACTIVE", CreatedAt = DateTime.UtcNow, CreatedBy = Actor() });
        db.Plans.Add(p);
        await db.SaveChangesAsync(ct);
        Audit(p, "PLAN_CREATED", "null");
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Created($"/api/admin/plans/{p.PlanId}", Map(p, 0));
    });
    [HttpPut("{id:long}")]
    public Task<IActionResult> Update(long id, PlanInput input, CancellationToken ct) => Mutate(id, input.Revision, "PLAN_UPDATED", p =>
    {
        if (string.IsNullOrWhiteSpace(input.PlanName)) return "Tên gói không được để trống.";
        if (input.PlanCode != p.PlanCode || input.IsPaid != p.IsPaid) return "Không thể thay đổi mã hoặc loại gói.";
        Apply(p, input); return null;
    }, ct);
    [HttpPatch("{id:long}/status")]
    public Task<IActionResult> Status(long id, StatusInput input, CancellationToken ct) => Mutate(id, input.Revision, "PLAN_STATUS_CHANGED", p => { p.PlanStatus = input.Active ? "ACTIVE" : "INACTIVE"; return null; }, ct);
    [HttpPost("{id:long}/prices")]
    public Task<IActionResult> Price(long id, PriceInput input, CancellationToken ct) => Mutate(id, input.Revision, "PLAN_PRICE_CHANGED", p =>
    {
        if (!ValidPrice(p.IsPaid, input.Price)) return "Giá trả phí phải là số nguyên dương; gói miễn phí phải có giá 0.";
        var old = p.Versions.Single(v => v.Status == "ACTIVE" && v.EffectiveTo == null);
        var now = DateTime.UtcNow;
        old.Status = "INACTIVE"; old.EffectiveTo = now;
        p.Versions.Add(new SaasPlanVersion { VersionNumber = p.Versions.Max(v => v.VersionNumber) + 1, Price = input.Price, Currency = old.Currency,
            DurationDays = old.DurationDays, DurationMonths = old.DurationMonths, BillingCycle = old.BillingCycle, PurchaseType = old.PurchaseType,
            Status = "ACTIVE", EffectiveFrom = now, CreatedAt = now, CreatedBy = Actor() });
        return null;
    }, ct);
    private Task<IActionResult> Mutate(long id, int revision, string action, Func<SaasPlan, string?> change, CancellationToken ct) => db.Database.CreateExecutionStrategy().ExecuteAsync<IActionResult>(async () =>
    {
        db.ChangeTracker.Clear();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var p = await db.Plans.Include(p => p.Versions).SingleOrDefaultAsync(p => p.PlanId == id, ct);
        if (p == null) return NotFound();
        if (p.Revision != revision) return Conflict(new { message = "Gói đã thay đổi. Vui lòng tải lại." });
        var before = JsonSerializer.Serialize(Map(p, 0));
        var error = change(p);
        if (error != null) return BadRequest(new { message = error });
        p.Revision++; p.UpdatedAt = DateTime.UtcNow; p.UpdatedBy = Actor();
        try { await db.SaveChangesAsync(ct); Audit(p, action, before); await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { message = "Gói đã thay đổi. Vui lòng tải lại." }); }
        return Ok(new { message = "Đã lưu thay đổi." });
    });
    private int Actor() => HttpContext.Session.GetInt32("SystemAdminId") ?? throw new UnauthorizedAccessException();
    private static bool ValidPrice(bool paid, decimal price) => decimal.Truncate(price) == price && (paid ? price > 0 : price == 0);
    private static string Cycle(int months) => months switch { 3 => "QUARTERLY", 6 => "SEMI_ANNUAL", 12 => "YEARLY", _ => "CUSTOM" };
    private static void Apply(SaasPlan p, PlanInput i) { p.PlanName = i.PlanName.Trim(); p.PlanDescription = i.PlanDescription?.Trim(); p.MaxUsers = i.MaxUsers; p.MaxContracts = i.MaxContracts; p.MaxScanPages = i.MaxScanPages; p.AllowsSupportTickets = i.AllowsSupportTickets; }
    private void Audit(SaasPlan p, string action, string before) => db.PlanAudits.Add(new PlanAudit { PlanId = p.PlanId, ActorId = Actor(), Action = action, BeforeJson = before, AfterJson = JsonSerializer.Serialize(Map(p, 0)), OccurredAt = DateTime.UtcNow, IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(), CorrelationId = HttpContext.TraceIdentifier });
    private static object Map(SaasPlan p, int count) => new { p.PlanId, p.PlanCode, p.PlanName, p.PlanDescription, p.IsPaid, p.PlanStatus, p.AllowsSupportTickets, p.MaxUsers, p.MaxContracts, p.MaxScanPages, p.Revision, ActiveSubscribers = count,
        Versions = p.Versions.OrderBy(v => v.VersionNumber).Select(v => new { v.PlanVersionId, v.VersionNumber, v.Price, v.Currency, v.BillingCycle, v.DurationDays, v.DurationMonths, v.Status, v.EffectiveFrom, v.EffectiveTo }) };
}
