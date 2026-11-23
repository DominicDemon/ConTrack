using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace ContractManagement.Domain.Internal;

public sealed record DraftField(string Key, string Label, bool Required = false, string DataType = "TEXT", List<string>? Options = null, decimal? Min = null, decimal? Max = null);
// English text is null for a Vietnamese-only contract and is then left out of the stored JSON, so earlier snapshots keep their bytes.
public sealed record DraftTerm(string Key, string Title, string Content, bool Editable = true,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TitleEn = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ContentEn = null);
public sealed record TemplateDefinition(long Id, string Name, string Revision, bool IsDemo,
    List<DraftField> Fields, List<DraftTerm> Terms, bool IsImported = false,
    int DocumentType = 1, string? WordStorageKey = null, string? WordSha256 = null, bool ManualWordContent = false);
public sealed record DraftItem(long? ProductId, string Name, decimal Quantity, decimal UnitPrice,
    decimal DiscountPercent, decimal DiscountAmount);
public sealed record DraftTotals(decimal Subtotal, decimal Discount, decimal Vat, decimal Total);
public sealed class DraftContent
{
    public const string ContractFont = "Times New Roman";
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool UseTimesNewRoman { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? OfficeOrigin { get; set; }
    public int FormatVersion { get; set; } = 1;
    public string PartyType { get; set; } = "";
    public long? CustomerBusinessId { get; set; }
    public string PaymentMode { get; set; } = "PERCENT";
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? ContractNumber { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? NumberingBusinessId { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? PaymentMethod { get; set; }
    public List<DraftMilestone> Milestones { get; set; } = [];
    public long TemplateId { get; set; }
    public long CustomerId { get; set; }
    public string Name { get; set; } = "";
    public Dictionary<string, string> Fields { get; set; } = [];
    public List<DraftItem> Items { get; set; } = [];
    public List<DraftTerm> Terms { get; set; } = [];
    public decimal VatPercent { get; set; }
    public DateOnly? EffectiveDate { get; set; }
    public DateOnly? ExpireDate { get; set; }
    public TemplateDefinition? TemplateSnapshot { get; set; }
    public DraftTotals Totals { get; set; } = new(0, 0, 0, 0);
    public List<string> MissingFields { get; set; } = [];
    /// <summary>null = Vietnamese only; <see cref="InternalDraftPolicy.Bilingual"/> = English and Vietnamese, English first.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? LanguageMode { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? NameEn { get; set; }
    /// <summary>English wording of the party details in <see cref="InternalDraftPolicy.EnglishFieldKeys"/>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Dictionary<string, string>? FieldsEn { get; set; }
}

public static class InternalDraftPolicy
{
    public const string Bilingual = "VI_EN";
    /// <summary>A person signs electronically (OTP); a business signs the printed contract and stamps it with its red seal.</summary>
    public const string Person = "PERSON", Business = "BUSINESS";
    /// <summary>The text a stored snapshot (web JSON) contains for a party type filter; null = no filter.</summary>
    public static string? PartyTypeMarker(string? partyType) => string.IsNullOrWhiteSpace(partyType) ? null
        : partyType is Person or Business ? $"\"partyType\":\"{partyType}\""
        : throw new ArgumentException("Loại khách hàng không hợp lệ.");
    /// <summary>Party details that read differently in English (names, addresses, positions, banks); numbers and codes are shared.</summary>
    public static readonly string[] EnglishFieldKeys = ["CUSTOMER_COMPANY", "CUSTOMER_ADDRESS", "CUSTOMER_CONTACT_TITLE", "CUSTOMER_BANK_NAME",
        "PROVIDER_LEGAL_NAME", "PROVIDER_ADDRESS", "PROVIDER_REPRESENTATIVE_TITLE", "PROVIDER_BANK_NAME"];

    public static string Text(string? value, int max)
    {
        var text = value?.Trim() ?? "";
        if (text.Length > max) throw new ArgumentException($"Nội dung vượt quá {max} ký tự.");
        // Draft terms are plain text. No active HTML is accepted or rendered.
        text = Regex.Replace(text, @"<(script|style)\b[^>]*>.*?</\1\s*>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return Regex.Replace(text, "<[^>]*>", "");
    }

    public static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    public static DraftContent Prepare(DraftContent input, TemplateDefinition template, bool forPreview = false)
    {
        input.Name = Text(input.Name, 1000);
        input.PaymentMethod = input.PaymentMethod switch
        {
            null or "" => null,
            "BANK_TRANSFER" or "CASH" or "CASH_OR_TRANSFER" => input.PaymentMethod,
            _ => throw new ArgumentException("Phương thức thanh toán không hợp lệ.")
        };
        if (input.TemplateId != template.Id || (!forPreview && (input.Name.Length == 0 || input.CustomerId <= 0)))
            throw new ArgumentException("Cần chọn mẫu, khách hàng và nhập tên hợp đồng.");
        if (input.Items is null || input.Terms is null || input.Fields is null || input.Items.Count > 100 || input.Terms.Count > 100)
            throw new ArgumentException("Dữ liệu nháp không hợp lệ hoặc vượt quá 100 hạng mục/điều khoản.");
        if (input.VatPercent < 0 || input.VatPercent > 100 || Round(input.VatPercent) != input.VatPercent)
            throw new ArgumentException("VAT phải từ 0 đến 100, tối đa 2 số thập phân.");
        if (input.EffectiveDate > input.ExpireDate)
            throw new ArgumentException("Ngày hết hạn phải sau hoặc bằng ngày hiệu lực.");
        var allowed = template.Fields.Select(f => f.Key).ToHashSet(StringComparer.Ordinal);
        if (input.Fields.Keys.Any(k => !allowed.Contains(k))) throw new ArgumentException("Trường nhập không thuộc mẫu.");
        input.Fields = input.Fields.ToDictionary(p => p.Key, p => Text(p.Value, 4000));
        decimal subtotal = 0, discount = 0;
        input.Items = input.Items.Select(item =>
        {
            var name = Text(item.Name, 500);
            if (name.Length == 0 || item.Quantity <= 0 || item.Quantity > 1000000 || decimal.Round(item.Quantity, 4) != item.Quantity
                || item.UnitPrice < 0 || item.UnitPrice > 1000000000000 || Round(item.UnitPrice) != item.UnitPrice
                || item.DiscountPercent < 0 || item.DiscountPercent > 100 || Round(item.DiscountPercent) != item.DiscountPercent
                || item.DiscountAmount < 0 || Round(item.DiscountAmount) != item.DiscountAmount
                || (item.DiscountPercent > 0 && item.DiscountAmount > 0))
                throw new ArgumentException("Hạng mục không hợp lệ. Chỉ dùng một hình thức giảm giá cho mỗi dòng.");
            var line = Round(item.Quantity * item.UnitPrice);
            var reduction = item.DiscountPercent > 0 ? Round(line * item.DiscountPercent / 100) : item.DiscountAmount;
            if (reduction > line) throw new ArgumentException("Giảm giá không được lớn hơn giá trị dòng.");
            subtotal += line; discount += reduction;
            return item with { Name = name };
        }).ToList();
        if (subtotal > 9999999999999999.99m) throw new ArgumentException("Tổng giá trị vượt giới hạn lưu trữ.");
        var bilingual = input.LanguageMode == Bilingual;
        if (input.LanguageMode is not (null or "VI" or Bilingual)) throw new ArgumentException("Ngôn ngữ hợp đồng không hợp lệ.");
        if (bilingual && template.WordStorageKey is not null) throw new ArgumentException("Mẫu Word giữ nguyên nội dung của file; hợp đồng song ngữ cần mẫu soạn trong hệ thống.");
        input.LanguageMode = bilingual ? Bilingual : null;
        input.NameEn = bilingual ? Text(input.NameEn, 1000) : null;
        input.FieldsEn = bilingual ? (input.FieldsEn ?? []).Where(p => EnglishFieldKeys.Contains(p.Key) && allowed.Contains(p.Key))
            .Select(p => (p.Key, Value: Text(p.Value, 4000))).Where(p => p.Value.Length > 0).ToDictionary(p => p.Key, p => p.Value) : null;
        input.Terms = input.Terms.Select(t => t with { Key = Text(t.Key, 100), Title = Text(t.Title, 500), Content = Text(t.Content, 20000),
            TitleEn = bilingual ? Text(t.TitleEn, 500) : null, ContentEn = bilingual ? Text(t.ContentEn, 20000) : null }).ToList();
        if (input.Terms.Any(t => string.IsNullOrWhiteSpace(t.Key) || string.IsNullOrWhiteSpace(t.Title)) || input.Terms.Select(t => t.Key).Distinct().Count() != input.Terms.Count)
            throw new ArgumentException("Điều khoản cần mã duy nhất và tiêu đề.");
        foreach (var locked in template.Terms.Where(t => !t.Editable))
        {
            var actual = input.Terms.SingleOrDefault(t => t.Key == locked.Key);
            if (actual is null || actual.Title != locked.Title || actual.Content != locked.Content)
                throw new ArgumentException("Không được thay đổi hoặc xóa điều khoản cố định của mẫu.");
        }
        if (template.WordStorageKey is not null && !input.Terms.SequenceEqual(template.Terms))
            throw new ArgumentException("Nội dung Word dùng các trường nhập liệu của mẫu. Tạo mẫu mới để thay đổi bố cục hoặc điều khoản.");
        input.Terms = input.Terms.Select(t => t with { Editable = template.Terms.FirstOrDefault(x => x.Key == t.Key)?.Editable ?? true }).ToList();
        var vat = Round((subtotal - discount) * input.VatPercent / 100);
        if (subtotal - discount + vat > 9999999999999999.99m) throw new ArgumentException("Tổng giá trị vượt giới hạn lưu trữ.");
        input.Totals = new(subtotal, discount, vat, subtotal - discount + vat);
        input.TemplateSnapshot = template;
        input.MissingFields = template.Fields.Where(f => f.Required
            && !(template.WordStorageKey is not null && f.Key.StartsWith("PROVIDER_", StringComparison.Ordinal))
            && string.IsNullOrWhiteSpace(input.Fields.GetValueOrDefault(f.Key))).Select(f => f.Label).ToList();
        if (input.Items.Count == 0) input.MissingFields.Add("Hạng mục hợp đồng");
        // A fixed term is the template's own text (checked above); a title-only one, e.g. a Word signature heading, is complete.
        if (input.Terms.Count == 0 || input.Terms.Any(t => t.Editable && string.IsNullOrWhiteSpace(t.Content))) input.MissingFields.Add("Nội dung điều khoản");
        if (bilingual && string.IsNullOrEmpty(input.NameEn)) input.MissingFields.Add("Tên hợp đồng tiếng Anh");
        if (bilingual && input.Terms.Any(t => t.Editable && (string.IsNullOrEmpty(t.TitleEn) || string.IsNullOrEmpty(t.ContentEn))))
            input.MissingFields.Add("Điều khoản tiếng Anh");
        DraftCompletionPolicy.Prepare(input, template);
        return input;
    }

    public static string PaymentMethodLabel(string? method) => method switch
    {
        "BANK_TRANSFER" => "Chuyển khoản",
        "CASH" => "Tiền mặt",
        "CASH_OR_TRANSFER" => "Tiền mặt hoặc chuyển khoản",
        _ => ""
    };
}
