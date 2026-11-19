using ContractManagement.Domain.Internal;

namespace ContractManagement.Tests.Domain;

public class DraftSendCompletenessTests
{
    [Fact]
    public void WordDraftRequiresBothPartiesAndNumberedClausesBeforeSending()
    {
        var template = new TemplateDefinition(1, "Mẫu", "1", false, [], [], WordStorageKey: "source.docx");
        var draft = new DraftContent { TemplateSnapshot = template, Terms = [
            new("heading", "ĐẠI DIỆN BÊN A", "", false), new("article", "Điều 1. Phạm vi", " ", false)] };
        var issues = DraftValidation.SendIssues(draft);
        Assert.Contains(issues, i => i.Path == "fields.CUSTOMER_ADDRESS");
        Assert.Contains(issues, i => i.Path == "fields.PROVIDER_REPRESENTATIVE_NAME");
        Assert.Single(issues.Where(i => i.Path == "terms"));
        foreach (var field in DraftCompletionPolicy.PartyFields.Where(f => f.Required))
            draft.Fields[field.Key] = "Đã bổ sung";
        draft.Terms[1] = draft.Terms[1] with { Content = "Cung cấp phần mềm theo phụ lục." };
        Assert.Empty(DraftValidation.SendIssues(draft));
    }
}
