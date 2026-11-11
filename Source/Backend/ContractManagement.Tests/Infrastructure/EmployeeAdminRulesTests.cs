using ContractManagement.Application.Features.Internal;

namespace ContractManagement.Tests.Infrastructure;

public class EmployeeAdminRulesTests
{
    [Fact]
    public void NormalizesFormAndKeepsOptionalContactsNull()
    {
        var form = EmployeeAdminRules.Normalize(new(" nv01 ", " Nguyen.A ", " Nguyễn Văn A ", " ", "+84 912 345 678", 3, true));
        Assert.Equal("NV01", form.Code);
        Assert.Equal("nguyen.a", form.Username);
        Assert.Equal("Nguyễn Văn A", form.Name);
        Assert.Null(form.Email);
        Assert.Equal("0912345678", form.Phone);
        Assert.Equal(3L, form.DepartmentId); Assert.True(form.Manager);
    }

    [Theory]
    [InlineData("NV01", "ab", "Tên", null, null)]          // username too short
    [InlineData("NV 01", "nguyen.a", "Tên", null, null)]  // space in code
    [InlineData("NV01", "nguyen.a", " ", null, null)]     // blank name
    [InlineData("NV01", "nguyen.a", "Tên", "not-mail", null)]
    [InlineData("NV01", "nguyen.a", "Tên", null, "123")]
    public void RejectsInvalidForm(string code, string username, string name, string? email, string? phone) =>
        Assert.Throws<ArgumentException>(() => EmployeeAdminRules.Normalize(new(code, username, name, email, phone, 1, false)));

    [Fact]
    public void RequiresDepartment()
    {
        Assert.Throws<ArgumentException>(() => EmployeeAdminRules.DepartmentId(null));
        Assert.Throws<ArgumentException>(() => EmployeeAdminRules.DepartmentId(0));
    }

    [Fact]
    public void AdminCannotActOnOwnAccount()
    {
        var admin = new InternalUser(5, "Admin", "admin", [EmployeeAdminRules.ManagePermission]);
        Assert.True(admin.CanManageEmployees);
        Assert.Throws<InternalAppException>(() => EmployeeAdminRules.RequireOther(admin, 5));
        EmployeeAdminRules.RequireOther(admin, 6);
    }

    [Fact]
    public void TemporaryPasswordsMeetThePolicyAndDiffer()
    {
        var passwords = Enumerable.Range(0, 50).Select(_ => EmployeeAdminRules.TemporaryPassword()).ToList();
        Assert.All(passwords, p => Assert.True(EmployeeAdminRules.IsValidPassword(p)));
        Assert.Equal(passwords.Count, passwords.Distinct().Count());
        Assert.False(EmployeeAdminRules.IsValidPassword("12345678"));
        Assert.False(EmployeeAdminRules.IsValidPassword("abc123"));
    }
}
