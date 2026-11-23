namespace ContractManagement.Domain.Internal;

public sealed record DraftIssue(string Code, string Path, string Message);
public sealed class DraftValidationException(List<DraftIssue> issues) : ArgumentException(string.Join("; ", issues.Select(i => i.Message)))
{
    public List<DraftIssue> Issues { get; } = issues;
}
public static class DraftValidation
{
    /// <summary>The dates a document of this type cannot be saved or sent without (a contract's end date, a payment request's due date…).</summary>
    public static void RequireContractDates(DraftContent content, TemplateDefinition? template = null)
    {
        var rule = DocumentRules.For((template ?? content.TemplateSnapshot)?.DocumentType);
        if (!rule.ExpireShown) content.ExpireDate = null;
        if (rule.ExpireRequired && content.ExpireDate is null)
            throw new DraftValidationException([new("required", "expireDate", rule.Type == DocumentRules.Contract
                ? "Vui lòng nhập ngày hết hạn hợp đồng." : $"Vui lòng nhập {rule.ExpireLabel.ToLowerInvariant()}.")]);
        if (content.EffectiveDate > content.ExpireDate)
            throw new DraftValidationException([new("invalid_date", "expireDate", $"{rule.ExpireLabel} phải sau hoặc bằng {rule.EffectiveLabel.ToLowerInvariant()}.")]);
    }

    public static List<DraftIssue> SendIssues(DraftContent content)
    {
        var issues = Missing(content);
        var word = content.TemplateSnapshot?.WordStorageKey is not null;
        // The template's own required flags decide, party fields included (UpgradeTemplate only adds the ones it lacks).
        var fields = content.TemplateSnapshot is { } snapshot ? DraftCompletionPolicy.UpgradeTemplate(snapshot).Fields : DraftCompletionPolicy.PartyFields.ToList();
        // Word templates carry Bên B in the file itself (same rule as InternalDraftPolicy.Prepare).
        foreach (var field in fields.Where(f => f.Required && !(word && f.Key.StartsWith("PROVIDER_", StringComparison.Ordinal))))
        {
            var path = "fields." + field.Key;
            if (string.IsNullOrWhiteSpace(content.Fields.GetValueOrDefault(field.Key)) && issues.All(i => i.Path != path))
                issues.Add(new("required", path, field.Label));
        }
        foreach (var term in content.Terms)
        {
            // Word also produces fixed headings for cover pages and signature blocks.
            var clause = content.TemplateSnapshot?.WordStorageKey is null || term.Editable ||
                System.Text.RegularExpressions.Regex.IsMatch(term.Title, @"^Điều\s*\d+", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (clause && string.IsNullOrWhiteSpace(term.Content))
                issues.Add(new("required", "terms", $"Thiếu nội dung điều khoản “{term.Title}”."));
        }
        return issues;
    }

    public static DraftContent Prepare(DraftContent content, TemplateDefinition template)
    {
        try { return InternalDraftPolicy.Prepare(content, template); }
        catch (DraftValidationException) { throw; }
        catch (ArgumentException e) { throw new DraftValidationException([new("invalid_value", Path(e.Message, template), e.Message)]); }
    }
    /// <summary>For reading a stored draft: rules added after it was saved become a listed issue, so the author can still open and fix it. Sending still re-checks strictly.</summary>
    public static void Recheck(DraftContent content)
    {
        try { InternalDraftPolicy.Prepare(content, DraftCompletionPolicy.UpgradeTemplate(content.TemplateSnapshot!)); }
        catch (ArgumentException e) { content.MissingFields.Add(e.Message); }
    }
    public static List<DraftIssue> Missing(DraftContent content) => content.MissingFields.Select(m => new DraftIssue("required", Path(m, content.TemplateSnapshot), m)).ToList();
    private static string Path(string message, TemplateDefinition? template)
    {
        var field = template?.Fields.FirstOrDefault(f => message.Contains(f.Label, StringComparison.Ordinal));
        if (field is not null) return "fields." + field.Key;
        var rule = DocumentRules.For(template?.DocumentType);
        if (rule.ExpireLabel.Length > 0 && message.StartsWith(rule.ExpireLabel, StringComparison.Ordinal)) return "expireDate";
        if (message.StartsWith(rule.EffectiveLabel, StringComparison.Ordinal)) return "effectiveDate";
        if (message == rule.ItemsLabel || message.StartsWith("Số tiền", StringComparison.Ordinal)) return "items";
        if (message.StartsWith("Đợt thanh toán của hợp đồng", StringComparison.Ordinal)) return "parentMilestoneKey";
        if (message.Contains("thanh toán", StringComparison.OrdinalIgnoreCase) || message.StartsWith("Đợt")) return "milestones";
        if (message.Contains("pháp nhân", StringComparison.OrdinalIgnoreCase)) return "customerBusinessId";
        if (message.Contains("cá nhân", StringComparison.OrdinalIgnoreCase)) return "partyType";
        if (message.Contains("Hạng mục", StringComparison.OrdinalIgnoreCase) || message.Contains("Giảm giá", StringComparison.OrdinalIgnoreCase)) return "items";
        if (message.Contains("điều khoản", StringComparison.OrdinalIgnoreCase) || message.Contains("Placeholder")) return "terms";
        if (message.Contains("VAT")) return "vatPercent";
        if (message.Contains("Ngày", StringComparison.OrdinalIgnoreCase)) return "effectiveDate";
        return "name";
    }
}
