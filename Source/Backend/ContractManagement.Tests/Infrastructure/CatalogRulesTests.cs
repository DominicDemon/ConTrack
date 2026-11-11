using ContractManagement.Application.Features.Internal;

namespace ContractManagement.Tests.Infrastructure;

public class CatalogRulesTests
{
    [Fact]
    public void NormalizesCustomerAndKeepsBlankOptionalNull()
    {
        var form = CatalogRules.Customer(new(" Công ty A ", "+84 912 345 678", " A@Mail.vn ", " ", null));
        Assert.Equal("Công ty A", form.Name);
        Assert.Equal("0912345678", form.Phone);
        Assert.Equal("a@mail.vn", form.Email);
        Assert.Null(form.Address);
        Assert.Null(form.TaxIdentifier);
    }

    [Theory]
    [InlineData(" ", "0912345678", null, null)]        // blank name
    [InlineData("A", "123", null, null)]                // bad phone
    [InlineData("A", "0912345678", "not-mail", null)]
    [InlineData("A", "0912345678", null, "12-34")]      // tax with symbol
    public void RejectsInvalidCustomer(string name, string phone, string? email, string? tax) =>
        Assert.Throws<ArgumentException>(() => CatalogRules.Customer(new(name, phone, email, null, tax)));

    [Fact]
    public void ProductPriceAndNameAreChecked()
    {
        Assert.Equal("SP01", CatalogRules.Product(new(" sp01 ", "Máy chủ", null, null, 0, null)).Code);
        Assert.Throws<ArgumentException>(() => CatalogRules.Product(new(null, "Máy chủ", null, null, -1, null)));
        Assert.Throws<ArgumentException>(() => CatalogRules.Product(new(null, " ", null, null, 1, null)));
        Assert.Throws<ArgumentException>(() => CatalogRules.Department(new(new string('x', 501), null)));
    }
}
