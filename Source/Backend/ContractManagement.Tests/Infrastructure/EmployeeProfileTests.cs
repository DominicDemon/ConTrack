using ContractManagement.Application.Features.Internal;
using ContractManagement.Infrastructure.Persistence.Master;

namespace ContractManagement.Tests.Infrastructure;

public sealed class EmployeeProfileTests
{
    [Theory]
    [InlineData(" Lan.Nguyen@CongTy.vn ", "lan.nguyen@congty.vn")]
    [InlineData("a@b.co", "a@b.co")]
    public void NewEmail_IsStoredAsTheAdminFormStoresIt(string typed, string expected) =>
        Assert.Equal(expected, EmployeeProfileService.Normalise(typed));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("khong-phai-email")]
    [InlineData("a@b")]
    [InlineData("Ten <a@b.vn>")]
    [InlineData("a@b.vn\r\nBcc: x@y.vn")]
    public void BadEmail_IsRefused(string? typed) =>
        Assert.Throws<ArgumentException>(() => EmployeeProfileService.Normalise(typed));

    [Fact]
    public void Code_GivesBackTheAddressItWasSentTo_Once()
    {
        var codes = new EmailChangeCodes();
        var code = codes.Issue(5, "moi@congty.vn");

        Assert.Equal("moi@congty.vn", codes.Verify(5, code));
        Assert.Equal(400, Assert.Throws<InternalAppException>(() => codes.Verify(5, code)).Status);
    }

    [Fact]
    public void WrongCode_DoesNotChangeTheEmail()
    {
        var codes = new EmailChangeCodes();
        var code = codes.Issue(5, "moi@congty.vn");

        Assert.Throws<InternalAppException>(() => codes.Verify(5, code == "123123" ? "321321" : "123123"));
        Assert.Equal("moi@congty.vn", codes.Verify(5, code));
    }
}
