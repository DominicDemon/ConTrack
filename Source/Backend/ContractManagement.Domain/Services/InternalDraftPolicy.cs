using System.Text.RegularExpressions;

namespace ContractManagement.Domain.Internal;

public sealed record DraftField(string Key, string Label, bool Required = false);
public sealed record DraftTerm(string Key, string Title, string Content, bool Editable = true);
public sealed record TemplateDefinition(long Id, string Name, string Revision, bool IsDemo,
    List<DraftField> Fields, List<DraftTerm> Terms);
public sealed record DraftItem(long? ProductId, string Name, decimal Quantity, decimal UnitPrice,
    decimal DiscountPercent, decimal DiscountAmount);
public sealed record DraftTotals(decimal Subtotal, decimal Discount, decimal Vat, decimal Total);
public sealed class DraftContent
{
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
}

public static class InternalDraftPolicy
{
    public static string Text(string? value, int max)
    {
        var text = value?.Trim() ?? "";
        if (text.Length > max) throw new ArgumentException($"Nội dung vượt quá {max} ký tự.");
        // Draft terms are plain text. No active HTML is accepted or rendered.
        text = Regex.Replace(text, @"<(script|style)\b[^>]*>.*?</\1\s*>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return Regex.Replace(text, "<[^>]*>", "");
    }

    public static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    public static DraftContent Prepare(DraftContent input, TemplateDefinition template)
    {
        input.Name = Text(input.Name, 1000);
        if (input.Name.Length == 0 || input.CustomerId <= 0 || input.TemplateId != template.Id)
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
        input.Terms = input.Terms.Select(t => t with { Key = Text(t.Key, 100), Title = Text(t.Title, 500), Content = Text(t.Content, 20000) }).ToList();
        if (input.Terms.Any(t => string.IsNullOrWhiteSpace(t.Key) || string.IsNullOrWhiteSpace(t.Title)) || input.Terms.Select(t => t.Key).Distinct().Count() != input.Terms.Count)
            throw new ArgumentException("Điều khoản cần mã duy nhất và tiêu đề.");
        foreach (var locked in template.Terms.Where(t => !t.Editable))
        {
            var actual = input.Terms.SingleOrDefault(t => t.Key == locked.Key);
            if (actual is null || actual.Title != locked.Title || actual.Content != locked.Content)
                throw new ArgumentException("Không được thay đổi hoặc xóa điều khoản cố định của mẫu.");
        }
        input.Terms = input.Terms.Select(t => t with { Editable = template.Terms.FirstOrDefault(x => x.Key == t.Key)?.Editable ?? true }).ToList();
        var vat = Round((subtotal - discount) * input.VatPercent / 100);
        if (subtotal - discount + vat > 9999999999999999.99m) throw new ArgumentException("Tổng giá trị vượt giới hạn lưu trữ.");
        input.Totals = new(subtotal, discount, vat, subtotal - discount + vat);
        input.TemplateSnapshot = template;
        input.MissingFields = template.Fields.Where(f => f.Required && string.IsNullOrWhiteSpace(input.Fields.GetValueOrDefault(f.Key))).Select(f => f.Label).ToList();
        if (input.Items.Count == 0) input.MissingFields.Add("Hạng mục hợp đồng");
        if (input.Terms.Count == 0 || input.Terms.Any(t => string.IsNullOrWhiteSpace(t.Content))) input.MissingFields.Add("Nội dung điều khoản");
        return input;
    }
}
