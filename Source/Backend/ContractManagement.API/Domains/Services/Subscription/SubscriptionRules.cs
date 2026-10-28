using System.Text.RegularExpressions;
using ContractManagement.Infrastructure.Persistence.Central.Saas;

namespace ContractManagement.API.Domains.Services.Subscription;

/// <summary>
/// Quy tắc thuần (không truy cập DB) cho đăng ký gói: mã tổ chức và voucher.
/// </summary>
public static partial class SubscriptionRules
{
    public const string TenantCodeInvalid = "TENANT_CODE_INVALID";
    public const string TenantCodeTaken = "TENANT_CODE_TAKEN";
    public const string PlanNotFound = "SUBSCRIPTION_PLAN_NOT_FOUND";
    public const string VoucherNotFound = "VOUCHER_NOT_FOUND";
    public const string VoucherInactive = "VOUCHER_INACTIVE";
    public const string VoucherNotStarted = "VOUCHER_NOT_STARTED";
    public const string VoucherExpired = "VOUCHER_EXPIRED";
    public const string VoucherUsageLimitReached = "VOUCHER_USAGE_LIMIT_REACHED";
    public const string VoucherNotApplicable = "VOUCHER_NOT_APPLICABLE";
    public const string FullNameInvalid = "SIGNUP_FULL_NAME_INVALID";
    public const string TenantNameInvalid = "SIGNUP_TENANT_NAME_INVALID";
    public const string EmailInvalid = "SIGNUP_EMAIL_INVALID";
    public const string PhoneInvalid = "SIGNUP_PHONE_INVALID";
    public const string EmailTaken = "SIGNUP_EMAIL_TAKEN";
    public const string PaymentGatewayError = "PAYMENT_GATEWAY_ERROR";
    public const string SubscriptionNotFound = "SUBSCRIPTION_NOT_FOUND";

    // Tên đăng nhập = email, cột tbl_Employee.EmployeeAccount chỉ VARCHAR(50).
    public const int MaxEmailLength = 50;
    public const int MaxFullNameLength = 100;
    public const int MaxTenantNameLength = 255;

    // Trạng thái SUBSCRIPTION / SAAS_INVOICE / SAAS_PAYMENT.
    public const string Pending = "PENDING";
    public const string Active = "ACTIVE";
    public const string PaymentFailed = "PAYMENT_FAILED";
    public const string Cancelled = "CANCELLED";
    public const string ProvisioningFailed = "PROVISIONING_FAILED";
    public const string Unpaid = "UNPAID";
    public const string Paid = "PAID";
    public const string Expired = "EXPIRED";
    public const string Success = "SUCCESS";

    public const string TenantCodeMessage =
        "Mã tổ chức dài 3–30 ký tự, chỉ gồm chữ thường a–z, số và dấu gạch ngang, không bắt đầu/kết thúc bằng dấu gạch ngang.";

    // Chữ thường để dùng thẳng làm TenantCode và hậu tố tên database tenant.
    [GeneratedRegex("^[a-z0-9][a-z0-9-]{1,28}[a-z0-9]$")]
    private static partial Regex TenantCodePattern();

    public static string NormalizeTenantCode(string? code) => (code ?? string.Empty).Trim().ToLowerInvariant();

    public static bool IsValidTenantCode(string normalizedCode) => TenantCodePattern().IsMatch(normalizedCode);

    [GeneratedRegex("^0[0-9]{9}$")]
    private static partial Regex VietnamPhonePattern();

    /// <summary>Bỏ khoảng trắng, dấu chấm, gạch; trả null nếu không phải SĐT VN 10 số.</summary>
    public static string? NormalizePhone(string? phone)
    {
        var digits = new string((phone ?? string.Empty).Where(c => !char.IsWhiteSpace(c) && c is not ('.' or '-')).ToArray());
        return VietnamPhonePattern().IsMatch(digits) ? digits : null;
    }

    public static string StatusMessage(string status, bool isPaid) => status switch
    {
        Pending when isPaid => "Đã thanh toán, hệ thống đang khởi tạo tài khoản.",
        Pending => "Đang chờ thanh toán.",
        Active => "Đăng ký thành công. Vui lòng kiểm tra email để nhận tài khoản.",
        PaymentFailed => "Thanh toán không thành công.",
        Cancelled => "Đơn đăng ký đã bị hủy hoặc hết hạn thanh toán.",
        ProvisioningFailed => "Đã thanh toán, hệ thống đang gặp lỗi khi tạo tài khoản, vui lòng liên hệ hỗ trợ.",
        _ => status
    };

    /// <summary>Số ngày sử dụng: theo plan_version_duration_days, không có thì theo billing_cycle.</summary>
    public static int DurationDays(int? durationDays, string billingCycle) => durationDays ?? billingCycle switch
    {
        "QUARTERLY" => 90,
        "SEMI_ANNUAL" => 180,
        "YEARLY" => 365,
        _ => 30
    };

    public static string NormalizeVoucherCode(string? code) => (code ?? string.Empty).Trim().ToUpperInvariant();

    /// <summary>Trả mã lỗi nếu voucher không dùng được cho subtotal tại thời điểm now; null nếu hợp lệ.</summary>
    public static string? ValidatePromotion(SaasPromotion promotion, decimal subtotal, DateTime now)
    {
        if (promotion.Status != "ACTIVE") return VoucherInactive;
        if (promotion.StartAt > now) return VoucherNotStarted;
        if (promotion.EndAt is { } endAt && endAt <= now) return VoucherExpired;
        if (promotion.UsageLimit is { } limit && promotion.UsageCount >= limit) return VoucherUsageLimitReached;
        if (subtotal <= 0) return VoucherNotApplicable;
        if (promotion.DiscountType is not ("PERCENTAGE" or "FIXED_AMOUNT")) return VoucherNotApplicable;
        return null;
    }

    /// <summary>Số tiền giảm, làm tròn xuống tới đồng (PayOS chỉ nhận số nguyên) và không vượt subtotal.</summary>
    public static decimal CalculateDiscount(decimal subtotal, SaasPromotion promotion)
    {
        var discount = promotion.DiscountType == "PERCENTAGE"
            ? subtotal * promotion.DiscountValue / 100m
            : promotion.DiscountValue;
        if (promotion.DiscountType == "PERCENTAGE" && promotion.MaxDiscountAmount is { } max)
            discount = Math.Min(discount, max);
        return Math.Floor(Math.Clamp(discount, 0m, subtotal));
    }

    public static string Message(string errorCode) => errorCode switch
    {
        TenantCodeInvalid => TenantCodeMessage,
        TenantCodeTaken => "Mã tổ chức đã được sử dụng.",
        PlanNotFound => "Gói dịch vụ không tồn tại hoặc đã ngừng áp dụng.",
        VoucherNotFound => "Mã giảm giá không tồn tại.",
        VoucherInactive => "Mã giảm giá đã ngừng áp dụng.",
        VoucherNotStarted => "Mã giảm giá chưa đến thời gian áp dụng.",
        VoucherExpired => "Mã giảm giá đã hết hạn.",
        VoucherUsageLimitReached => "Mã giảm giá đã hết lượt sử dụng.",
        VoucherNotApplicable => "Mã giảm giá không áp dụng cho gói này.",
        FullNameInvalid => $"Họ tên không được để trống và tối đa {MaxFullNameLength} ký tự.",
        TenantNameInvalid => $"Tên tổ chức không được để trống và tối đa {MaxTenantNameLength} ký tự.",
        EmailInvalid => $"Email không đúng định dạng hoặc dài quá {MaxEmailLength} ký tự.",
        PhoneInvalid => "Số điện thoại phải gồm 10 chữ số và bắt đầu bằng 0.",
        EmailTaken => "Email này đã được dùng để đăng ký một tổ chức. Vui lòng dùng email khác.",
        PaymentGatewayError => "Không tạo được liên kết thanh toán PayOS, vui lòng thử lại sau.",
        SubscriptionNotFound => "Không tìm thấy đơn đăng ký.",
        _ => "Yêu cầu không hợp lệ."
    };
}
