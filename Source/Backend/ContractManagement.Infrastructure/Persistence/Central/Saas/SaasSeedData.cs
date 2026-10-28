using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Infrastructure.Persistence.Central.Saas;

/// <summary>
/// Dữ liệu gói mặc định, khớp với trang /pricing của Frontend:
/// dùng thử 15 ngày + gói Tiêu chuẩn 3/6/12 tháng.
/// </summary>
internal static class SaasSeedData
{
    // Ngày cố định để HasData không sinh migration mới mỗi lần build.
    private static readonly DateTime SeedAt = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static void Apply(ModelBuilder b)
    {
        b.Entity<SaasType>().HasData(new SaasType
        {
            SaasTypeId = 1, SaasTypeCode = "CONTRACK", SaasTypeName = "ConTrack",
            SaasTypeDescription = "Phần mềm quản lý hợp đồng", SaasTypeStatus = "ACTIVE", CreatedAt = SeedAt
        });

        b.Entity<SaasPlan>().HasData(
            new SaasPlan
            {
                PlanId = 1, SaasTypeId = 1, PlanCode = "TRIAL", PlanName = "Dùng thử",
                PlanDescription = "Trải nghiệm quy trình chính, không bao gồm Customer Support",
                PlanStatus = "ACTIVE", IsPaid = false, AllowsSupportTickets = false,
                MaxContracts = 10, MaxScanPages = 20, MaxUsers = 2, CreatedAt = SeedAt
            },
            new SaasPlan
            {
                PlanId = 2, SaasTypeId = 1, PlanCode = "STANDARD", PlanName = "Tiêu chuẩn",
                PlanDescription = "Đầy đủ tính năng, có Customer Support",
                PlanStatus = "ACTIVE", IsPaid = true, AllowsSupportTickets = true,
                MaxContracts = 1000, MaxScanPages = 1000, MaxUsers = 20, CreatedAt = SeedAt
            });

        b.Entity<SaasPlanVersion>().HasData(
            Version(1, 1, 1, 15, 0m, "TRIAL"),
            Version(2, 2, 1, 90, 900_000m, "QUARTERLY"),
            Version(3, 2, 2, 180, 1_620_000m, "SEMI_ANNUAL"),
            Version(4, 2, 3, 365, 2_880_000m, "YEARLY"));

        b.Entity<SaasPromotion>().HasData(new SaasPromotion
        {
            PromotionId = 1, PromotionCode = "TEST10", PromotionName = "Voucher test giảm 10%",
            DiscountType = "PERCENTAGE", DiscountValue = 10m, MaxDiscountAmount = 500_000m,
            StartAt = SeedAt, EndAt = null, UsageLimit = 100, UsageCount = 0, Status = "ACTIVE", CreatedAt = SeedAt
        });
    }

    private static SaasPlanVersion Version(long id, long planId, int number, int days, decimal price, string cycle) => new()
    {
        PlanVersionId = id, PlanId = planId, VersionNumber = number, PurchaseType = "BUYMANY",
        DurationMonths = cycle == "TRIAL" ? null : cycle == "QUARTERLY" ? 3 : cycle == "SEMI_ANNUAL" ? 6 : 12,
        DurationDays = days, Price = price, Currency = "VND", BillingCycle = cycle,
        EffectiveFrom = SeedAt, EffectiveTo = null, Status = "ACTIVE", CreatedAt = SeedAt
    };
}
