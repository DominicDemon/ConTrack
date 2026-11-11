using System.Globalization;
using System.Text.RegularExpressions;
using ContractManagement.Application.Features.Internal;
using ContractManagement.Domain.Internal;

namespace ContractManagement.Infrastructure.Persistence.Master;

public sealed record WordDraftDetails(string Name, Dictionary<string, string> Fields, DateOnly? EffectiveDate,
    DateOnly? ExpireDate, decimal? TotalAmount, List<DraftItem> Items, List<string> Notes);

public static class InternalWordDetails
{
    public static WordDraftDetails Extract(byte[] document, string fallbackName)
    {
        var blocks = InternalWordDocument.Blocks(document).Where(b => b.Location.StartsWith("Nội dung")).ToList();
        var lines = blocks.SelectMany(b => b.Text.Split('\n')).Select(Clean).Where(t => t.Length > 0).ToList();
        var fields = new Dictionary<string, string>();
        var names = new List<string>();
        var titleIndex = lines.FindIndex(t => Regex.IsMatch(t, @"^HỢP ĐỒNG(?:\s|$)"));
        if (titleIndex >= 0)
            foreach (var line in lines.Skip(titleIndex).Take(5))
            {
                if (line != line.ToUpperInvariant() || line.StartsWith("SỐ") || line.Length > 160) break;
                names.Add(line);
            }
        var name = names.Count > 0 ? string.Join(" ", names) : fallbackName;
        var party = "";
        for (var i = 0; i < lines.Count; i++)
        {
            var match = Regex.Match(lines[i], @"^Bên\s*([AB])\s*[:：]\s*(.*)$", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                party = match.Groups[1].Value.ToUpperInvariant() == "A" ? "CUSTOMER" : "PROVIDER";
                var value = match.Groups[2].Value;
                if (value.Length == 0 && i + 1 < lines.Count) value = lines[++i];
                Put(party == "CUSTOMER" ? "CUSTOMER_COMPANY" : "PROVIDER_LEGAL_NAME", value);
                continue;
            }
            if (Regex.IsMatch(lines[i], @"^(Điều\s*\d|Hai bên|Sau khi|Căn cứ)", RegexOptions.IgnoreCase)) party = "";
            if (party.Length == 0) continue;
            var label = Regex.Match(lines[i], @"^(Địa chỉ|Điện thoại|Đại diện|Người đại diện|Mã số thuế|Email)\s*[:：]?\s*(.*)$", RegexOptions.IgnoreCase);
            if (!label.Success) continue;
            var text = label.Groups[2].Value;
            if (text.Length == 0 && i + 1 < lines.Count) text = lines[++i];
            text = Regex.Split(text, @"\s+(?:Fax|Chức vụ)\s*:", RegexOptions.IgnoreCase)[0].Trim();
            var key = label.Groups[1].Value.ToLowerInvariant() switch {
                "địa chỉ" => party + "_ADDRESS", "mã số thuế" => party + "_TAX_CODE",
                "đại diện" or "người đại diện" => party == "CUSTOMER" ? "CUSTOMER_CONTACT_NAME" : "PROVIDER_REPRESENTATIVE_NAME",
                "điện thoại" => party == "CUSTOMER" ? "CUSTOMER_CONTACT_PHONE" : "",
                "email" => party == "CUSTOMER" ? "CUSTOMER_EMAIL" : "", _ => "" };
            Put(key, text);
        }
        var all = string.Join("\n", lines);
        var totals = Regex.Matches(all, @"(?:Tổng giá trị hợp đồng|Tổng giá trị của hợp đồng|Tổng cộng)\s*[:：]?\s*([\d][\d., ]*)", RegexOptions.IgnoreCase)
            .Select(m => Amount(m.Groups[1].Value)).Where(n => n.HasValue).Select(n => n!.Value).Distinct().ToList();
        decimal? total = totals.Count == 1 ? totals[0] : null;
        DateOnly? effective = DateAfter(@"(?:Ngày hiệu lực|Có hiệu lực từ ngày)\s*[:：]?\s*");
        if (effective is null && Regex.IsMatch(all, @"hiệu lực (?:kể )?từ ngày ký", RegexOptions.IgnoreCase))
            effective = DateAfter(@"Hôm nay,?\s*ngày\s*");
        var expiry = DateAfter(@"(?:Ngày hết hạn|Có hiệu lực đến ngày)\s*[:：]?\s*");
        var items = new List<DraftItem>();
        foreach (var table in blocks.Where(b => b.TableId != null).GroupBy(b => b.TableId))
        {
            var rows = table.GroupBy(b => b.Row).Select(row => row.GroupBy(b => b.Cell).Select(cell => Clean(string.Join(" ", cell.Select(b => b.Text)))).ToList()).ToList();
            var headers = rows.FindIndex(row => row.Any(c => c.Contains("Đơn giá", StringComparison.OrdinalIgnoreCase)) && row.Any(c => c.Contains("Số lượng", StringComparison.OrdinalIgnoreCase)));
            if (headers < 0) continue;
            var heading = rows[headers];
            int qty = heading.FindIndex(c => c.Contains("Số lượng", StringComparison.OrdinalIgnoreCase)), price = heading.FindIndex(c => c.Contains("Đơn giá", StringComparison.OrdinalIgnoreCase));
            foreach (var row in rows.Skip(headers + 1))
            {
                if (row.Count != heading.Count || row.Any(c => Regex.IsMatch(c, @"Tổng|Thuế|VAT", RegexOptions.IgnoreCase))) continue;
                if (decimal.TryParse(row[qty], NumberStyles.Number, CultureInfo.InvariantCulture, out var quantity) && quantity > 0 && quantity <= 1000000 && Amount(row[price]) is { } unit && unit <= 1000000000000)
                    items.Add(new(null, row[0], quantity, unit, 0, 0));
            }
        }
        var notes = new List<string>();
        if (total is null || items.Sum(i => i.Quantity * i.UnitPrice) != total) {
            items.Clear(); notes.Add("Chưa đối chiếu được hạng mục với tổng tiền; cần rà soát phần giá trị.");
        }
        if (effective is null) notes.Add("Chưa xác định rõ ngày hiệu lực trong tài liệu.");
        if (expiry is null) notes.Add("Chưa xác định rõ ngày hết hạn trong tài liệu.");
        return new(name, fields, effective, expiry, total, items, notes);

        void Put(string key, string value) { if (key.Length > 0 && value.Length is > 0 and <= 4000 && !value.Contains("{{")) fields.TryAdd(key, value); }
        DateOnly? DateAfter(string prefix) {
            var matches = Regex.Matches(all, prefix + @"(\d{1,2})(?:\s*tháng\s*|/)(\d{1,2})(?:\s*năm\s*|/)(\d{4})", RegexOptions.IgnoreCase);
            var dates = matches.Select(m => DateOnly.TryParseExact($"{m.Groups[1].Value.PadLeft(2,'0')}/{m.Groups[2].Value.PadLeft(2,'0')}/{m.Groups[3].Value}", "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? (DateOnly?)d : null).Where(d => d.HasValue).Distinct().ToList();
            return dates.Count == 1 ? dates[0] : null;
        }
    }
    private static string Clean(string value) => Regex.Replace(value, @"\s+", " ").Trim();
    private static decimal? Amount(string text) {
        text = text.Trim().Replace(" ", "");
        if (!Regex.IsMatch(text, @"^(?:\d+|\d{1,3}(?:[.,]\d{3})+)$")) return null;
        return decimal.TryParse(text.Replace(".", "").Replace(",", ""), out var value) && value >= 0 && value <= 9999999999999999.99m ? value : null;
    }
}
