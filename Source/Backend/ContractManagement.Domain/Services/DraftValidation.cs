namespace ContractManagement.Domain.Internal;

public sealed record DraftIssue(string Code, string Path, string Message);
public sealed class DraftValidationException(List<DraftIssue> issues) : ArgumentException(string.Join("; ", issues.Select(i => i.Message)))
{
    public List<DraftIssue> Issues { get; } = issues;
}
public static class DraftValidation
{
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
