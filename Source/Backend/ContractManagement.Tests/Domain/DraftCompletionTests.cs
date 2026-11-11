using ContractManagement.Domain.Internal;

namespace ContractManagement.Tests.Domain;

public class DraftCompletionTests
{
    private static TemplateDefinition Template => new(1, "Test", "v2", false,
        [new("N", "Số", false, "NUMBER", null, 0, 10), new("D", "Ngày", false, "DATE"), new("S", "Loại", false, "SELECT", ["A", "B"])],
        [new("PAY", "Thanh toán", "Tổng {{TOTAL_AMOUNT}}")]);
    private static DraftContent Draft() => new() { TemplateId = 1, CustomerId = 1, PartyType = "PERSON", Name = "Test", Terms = Template.Terms.ToList(), Items = [new(null,"Dịch vụ",1,100.01m,0,0)] };
    [Fact] public void IncompleteScheduleCanSaveButCannotSend()
    {
        var c = InternalDraftPolicy.Prepare(Draft(), Template);
        Assert.Contains("Lịch thanh toán",c.MissingFields);
        c.Milestones = [new("1","Đợt 1","UNKNOWN",50,Anchor:"DATE")];
        InternalDraftPolicy.Prepare(c,Template);
        Assert.Contains(c.MissingFields,x => x.Contains("điều khoản"));
        Assert.Contains(c.MissingFields,x => x.Contains("ngày đến hạn"));
        Assert.Contains(c.MissingFields,x => x.Contains("100%"));
    }
    [Fact] public void RoundingAndEventDatesAreControlledByServer()
    {
        var c = Draft(); c.Milestones = [new("1","Một","PAY",50,999,DueDate:new(2026,1,1)),new("2","Hai","PAY",50)];
        InternalDraftPolicy.Prepare(c,Template);
        Assert.Empty(c.MissingFields); Assert.Equal(50.01m,c.Milestones[0].Amount); Assert.Equal(50m,c.Milestones[1].Amount); Assert.Null(c.Milestones[0].DueDate);
        c.PaymentMode="AMOUNT"; c.Milestones=[new("1","Một","PAY",Amount:100)];
        InternalDraftPolicy.Prepare(c,Template); Assert.NotEmpty(c.MissingFields);
    }
    [Theory] [InlineData("N","11")] [InlineData("N","abc")] [InlineData("D","2026-02-30")] [InlineData("S","C")]
    public void TypedFieldsRejectInvalidValues(string key,string value) { var c=Draft();c.Fields[key]=value;Assert.Throws<ArgumentException>(()=>InternalDraftPolicy.Prepare(c,Template)); }
    [Fact] public void TemplateRejectsUnknownPlaceholderAndCoreShadowing()
    {
        Assert.Throws<ArgumentException>(()=>DraftCompletionPolicy.ValidateTemplate(Template with { Terms=[new("X","X","{{UNKNOWN}}")]}));
        Assert.Throws<ArgumentException>(()=>DraftCompletionPolicy.ValidateTemplate(Template with { Fields=[new("TOTAL_AMOUNT","Sai")]}));
        Assert.Throws<ArgumentException>(()=>DraftCompletionPolicy.ValidateTemplate(Template with { Fields=[new("X","X"),new("X","Y")]}));
    }
    [Fact] public void ValidationReturnsFieldPathsForInputAndMissingSchedule()
    {
        var c=Draft(); c.Fields["N"]="wrong";
        var error=Assert.Throws<DraftValidationException>(()=>DraftValidation.Prepare(c,Template));
        Assert.Equal("fields.N",error.Issues[0].Path);
        c.Fields.Clear(); DraftValidation.Prepare(c,Template);
        Assert.Contains(DraftValidation.Missing(c),i=>i.Path=="milestones");
    }
    [Fact] public void NewHashIgnoresPropertyOrderAndDecimalScaleButLegacyRemainsUnchanged()
    {
        Assert.Equal(SnapshotHash.Compute("{\"formatVersion\":2,\"fields\":{\"A\":1.00,\"B\":2}}"),SnapshotHash.Compute("{\"fields\":{\"B\":2,\"A\":1},\"formatVersion\":2}"));
        Assert.NotEqual(SnapshotHash.Compute("{\"a\":1,\"b\":2}"),SnapshotHash.Compute("{\"b\":2,\"a\":1}"));
    }
}
