using System.Globalization;
using System.Text.RegularExpressions;

namespace ContractManagement.Domain.Internal;

public sealed record DraftMilestone(string Key, string Title, string TermKey, decimal Percent = 0, decimal Amount = 0,
    string Anchor = "SIGNED", int OffsetDays = 0, DateOnly? DueDate = null, string Condition = "");

public static class DraftCompletionPolicy
{
    public static readonly DraftField[] PartyFields = [new("CUSTOMER_CONTACT_NAME", "Người đại diện khách hàng", true),
        new("CUSTOMER_COMPANY", "Tên bên khách hàng", true), new("CUSTOMER_CONTACT_PHONE", "SĐT khách hàng", true),
        new("CUSTOMER_ADDRESS", "Địa chỉ khách hàng", true), new("CUSTOMER_EMAIL", "Email"), new("CUSTOMER_TAX_CODE", "Mã số thuế"),
        new("PROVIDER_LEGAL_NAME", "Tên pháp lý công ty", true), new("PROVIDER_REPRESENTATIVE_NAME", "Đại diện công ty", true),
        new("PROVIDER_ADDRESS", "Địa chỉ công ty", true), new("PROVIDER_TAX_CODE", "Mã số thuế công ty", true)];
    public static readonly string[] ComputedKeys = ["SUBTOTAL", "DISCOUNT", "VAT", "TOTAL_AMOUNT", "PAYMENT_SCHEDULE", "CONTRACT_NAME", "EFFECTIVE_DATE", "EXPIRE_DATE"];
    public static TemplateDefinition UpgradeTemplate(TemplateDefinition template) => template.IsImported
        ? template with { Fields = template.Fields.Concat(PartyFields.Where(f => template.Fields.All(x => x.Key != f.Key))).ToList() } : template;
    public static void ValidateTemplate(TemplateDefinition template)
    {
        if (template.Fields.Count > 100 || template.Terms.Count is 0 or > 100 || template.Fields.Select(f => f.Key).Distinct().Count() != template.Fields.Count)
            throw new ArgumentException("Mẫu cần 1–100 điều khoản và tối đa 100 trường không trùng khóa.");
        foreach (var f in template.Fields)
        {
            if (!Regex.IsMatch(f.Key, "^[A-Z][A-Z0-9_]{0,99}$") || string.IsNullOrWhiteSpace(f.Label) || f.Label.Length > 200 || ComputedKeys.Contains(f.Key))
                throw new ArgumentException("Khóa trường không hợp lệ hoặc trùng trường lõi: " + f.Key);
            if (f.DataType is not ("TEXT" or "NUMBER" or "DATE" or "SELECT") || f.Min > f.Max || (f.DataType == "SELECT" && (f.Options is not { Count: > 0 and <= 100 } || f.Options.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 200) || f.Options.Distinct().Count() != f.Options.Count)))
                throw new ArgumentException("Kiểu hoặc giới hạn trường không hợp lệ: " + f.Label);
        }
        if (!template.ManualWordContent) ValidatePlaceholders(template.Terms, template.Fields);
    }
    public static void ValidatePlaceholders(IEnumerable<DraftTerm> terms, IEnumerable<DraftField> fields)
    {
        var keys = fields.Select(f => f.Key).Concat(ComputedKeys).ToHashSet();
        foreach (var term in terms)
        foreach (var text in new[] { term.Title, term.Content })
        {
            foreach (Match match in Regex.Matches(text, @"\{\{([^{}]*)\}\}"))
                if (!keys.Contains(match.Groups[1].Value)) throw new ArgumentException("Placeholder chưa được ánh xạ: " + match.Value);
            var remaining = Regex.Replace(text, @"\{\{[^{}]*\}\}", "");
            if (remaining.Contains("{{") || remaining.Contains("}}")) throw new ArgumentException("Placeholder không đúng cú pháp {{KEY}}.");
        }
    }
    public static void Prepare(DraftContent c, TemplateDefinition template)
    {
        c.FormatVersion = 2;
        if (c.PartyType is not ("" or "PERSON" or "BUSINESS")) throw new ArgumentException("Loại bên giao dịch không hợp lệ.");
        if (c.PartyType == "") c.MissingFields.Add("Chọn cá nhân hoặc doanh nghiệp");
        if (c.PartyType == "BUSINESS" && c.CustomerBusinessId is null) c.MissingFields.Add("Chọn pháp nhân khách hàng");
        if (c.PartyType == "BUSINESS" && string.IsNullOrWhiteSpace(c.Fields.GetValueOrDefault("CUSTOMER_TAX_CODE"))) c.MissingFields.Add("Mã số thuế pháp nhân");
        if (c.PartyType == "PERSON") c.CustomerBusinessId = null;
        foreach (var f in template.Fields)
        {
            var value = c.Fields.GetValueOrDefault(f.Key, "");
            if (string.IsNullOrWhiteSpace(value)) continue;
            if (f.DataType == "NUMBER" && (!decimal.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number) || number < f.Min || number > f.Max))
                throw new ArgumentException("Số không hợp lệ: " + f.Label);
            if (f.DataType == "DATE" && !DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)) throw new ArgumentException("Ngày không hợp lệ: " + f.Label);
            if (f.DataType == "SELECT" && !(f.Options?.Contains(value) ?? false)) throw new ArgumentException("Lựa chọn không hợp lệ: " + f.Label);
        }
        if (!template.ManualWordContent) ValidatePlaceholders(c.Terms, template.Fields);
        if (c.Milestones is null || c.Milestones.Count > 100 || c.Milestones.Select(x => x.Key).Distinct().Count() != c.Milestones.Count)
            throw new ArgumentException("Các đợt thanh toán phải có mã riêng, tối đa 100 đợt.");
        if (c.PaymentMode is not ("PERCENT" or "AMOUNT")) throw new ArgumentException("Cách phân bổ thanh toán không hợp lệ.");
        if (c.Milestones.Count == 0) c.MissingFields.Add("Lịch thanh toán");
        decimal allocated = 0;
        var percentTotal = c.Milestones.Sum(m => m.Percent);
        c.Milestones = c.Milestones.Select((m, i) =>
        {
            if (string.IsNullOrWhiteSpace(m.Key) || m.Key.Length > 50 || m.Percent < 0 || m.Percent > 100 || m.Amount < 0 || m.Amount > 999999999999999.99m || m.OffsetDays is < 0 or > 36500 || m.Anchor is not ("DATE" or "SIGNED" or "HANDOVER"))
                throw new ArgumentException($"Đợt thanh toán {i + 1}: dữ liệu không hợp lệ.");
            if (InternalDraftPolicy.Round(m.Percent) != m.Percent || InternalDraftPolicy.Round(m.Amount) != m.Amount) throw new ArgumentException("Thanh toán hỗ trợ tối đa 2 số thập phân.");
            var title = InternalDraftPolicy.Text(m.Title, 200);
            var condition = InternalDraftPolicy.Text(m.Condition, 2000);
            if (title.Length == 0) c.MissingFields.Add($"Đợt {i + 1}: tên đợt");
            if (!c.Terms.Any(t => t.Key == m.TermKey)) c.MissingFields.Add($"Đợt {i + 1}: điều khoản thanh toán");
            if (m.Anchor == "DATE" && m.DueDate is null) c.MissingFields.Add($"Đợt {i + 1}: ngày đến hạn");
            var amount = c.PaymentMode == "PERCENT" ? InternalDraftPolicy.Round(c.Totals.Total * m.Percent / 100) : m.Amount;
            if (c.PaymentMode == "PERCENT" && percentTotal == 100 && i == c.Milestones.Count - 1) amount = c.Totals.Total - allocated;
            if (amount < 0 || amount > 9999999999999999.99m) throw new ArgumentException("Phân bổ thanh toán vượt giới hạn lưu trữ; điều chỉnh các đợt.");
            allocated += amount;
            return m with { Title = title, Condition = condition, Amount = amount, DueDate = m.Anchor == "DATE" ? m.DueDate : null,
                Percent = c.PaymentMode == "PERCENT" ? m.Percent : c.Totals.Total == 0 ? 0 : InternalDraftPolicy.Round(amount / c.Totals.Total * 100) };
        }).ToList();
        if (c.Milestones.Count > 0 && (allocated != c.Totals.Total || (c.PaymentMode == "PERCENT" && percentTotal != 100)))
            c.MissingFields.Add("Lịch thanh toán phải đủ 100% tổng sau VAT");
    }
}
