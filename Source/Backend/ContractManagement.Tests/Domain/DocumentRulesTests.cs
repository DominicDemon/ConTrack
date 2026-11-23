using ContractManagement.Domain.Internal;

namespace ContractManagement.Tests.Domain;

public class DocumentRulesTests
{
    private static TemplateDefinition Template(int type, List<DraftField>? fields = null) => new(1, "Mẫu", "v1", false,
        fields ?? [], [new("BODY", "Nội dung", "Nội dung văn bản")], DocumentType: type);
    private static DraftContent Draft(int type) => new()
    {
        TemplateId = 1, CustomerId = 1, PartyType = "PERSON", Name = "Văn bản", Terms = Template(type).Terms.ToList(),
        Items = [new(null, "Máy chủ", 2, 0, 0, 0)]
    };

    [Fact]
    public void HandoverNeedsDateAndListButNoScheduleEndDateOrPrice()
    {
        var c = Draft(DocumentRules.Handover);
        c.ExpireDate = new(2027, 1, 1);
        c.Milestones = [new("1", "Đợt 1", "BODY", 100)];
        InternalDraftPolicy.Prepare(c, Template(DocumentRules.Handover));
        Assert.Equal(["Ngày bàn giao"], c.MissingFields);
        Assert.Null(c.ExpireDate);
        Assert.Empty(c.Milestones);

        c.EffectiveDate = new(2026, 12, 1);
        c.Items = [];
        InternalDraftPolicy.Prepare(c, Template(DocumentRules.Handover));
        Assert.Equal(["Danh mục bàn giao"], c.MissingFields);
    }

    [Fact]
    public void ReceiptNeedsAnAmountAboveZero()
    {
        var c = Draft(DocumentRules.Receipt);
        c.EffectiveDate = new(2026, 12, 1);
        InternalDraftPolicy.Prepare(c, Template(DocumentRules.Receipt));
        Assert.Contains("Số tiền phải lớn hơn 0", c.MissingFields);
        c.Items = [new(null, "Thu đợt 1", 1, 5000000, 0, 0)];
        InternalDraftPolicy.Prepare(c, Template(DocumentRules.Receipt));
        Assert.Empty(c.MissingFields);
    }

    [Fact]
    public void AppendixNeedsAScheduleOnlyWhenItChangesTheValue()
    {
        var c = Draft(DocumentRules.Appendix);
        c.EffectiveDate = new(2026, 12, 1);
        c.Items = [];
        InternalDraftPolicy.Prepare(c, Template(DocumentRules.Appendix));
        Assert.Empty(c.MissingFields);
        c.Items = [new(null, "Thêm người dùng", 1, 1000, 0, 0)];
        InternalDraftPolicy.Prepare(c, Template(DocumentRules.Appendix));
        Assert.Equal(["Lịch thanh toán"], c.MissingFields);
    }

    [Fact]
    public void PaymentRequestNeedsDueDateAndTheContractInstalmentWhenTheContractHasASchedule()
    {
        var c = Draft(DocumentRules.PaymentRequest);
        c.Items = [new(null, "Thanh toán đợt 1", 1, 1000, 0, 0)];
        var dates = Assert.Throws<DraftValidationException>(() => DraftValidation.RequireContractDates(c, Template(DocumentRules.PaymentRequest)));
        Assert.Equal("expireDate", Assert.Single(dates.Issues).Path);
        Assert.Contains("hạn thanh toán", dates.Message);

        c.ExpireDate = new(2026, 12, 31);
        c.ParentMilestoneRequired = true;
        InternalDraftPolicy.Prepare(c, Template(DocumentRules.PaymentRequest));
        Assert.Equal(["Đợt thanh toán của hợp đồng"], c.MissingFields);
        c.ParentMilestoneKey = "M1";
        InternalDraftPolicy.Prepare(c, Template(DocumentRules.PaymentRequest));
        Assert.Empty(c.MissingFields);
    }

    [Fact]
    public void ContractRulesAreUnchangedAndTemplateFlagsDecideRequiredFields()
    {
        Assert.Throws<DraftValidationException>(() => DraftValidation.RequireContractDates(Draft(DocumentRules.Contract), Template(DocumentRules.Contract)));
        var c = Draft(DocumentRules.Contract);
        InternalDraftPolicy.Prepare(c, Template(DocumentRules.Contract));
        Assert.Contains("Lịch thanh toán", c.MissingFields);

        // A handover template that does not need the customer's address, but needs the place of handover.
        var fields = new List<DraftField> { new("CUSTOMER_ADDRESS", "Địa chỉ khách hàng", false), new("HANDOVER_PLACE", "Địa điểm bàn giao", true) };
        var h = Draft(DocumentRules.Handover);
        h.EffectiveDate = new(2026, 12, 1);
        h.TemplateSnapshot = DraftCompletionPolicy.UpgradeTemplate(Template(DocumentRules.Handover, fields));
        var issues = DraftValidation.SendIssues(InternalDraftPolicy.Prepare(h, h.TemplateSnapshot)).Select(i => i.Message).ToList();
        Assert.Contains("Địa điểm bàn giao", issues);
        Assert.DoesNotContain("Địa chỉ khách hàng", issues);
    }
}
