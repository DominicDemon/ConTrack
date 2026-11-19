using ContractManagement.Domain.Internal;

namespace ContractManagement.Tests.Domain;

public class DraftContractDateTests
{
    [Fact]
    public void MissingExpiryReportsTheExpiryField()
    {
        var error = Assert.Throws<DraftValidationException>(() => DraftValidation.RequireContractDates(new DraftContent()));
        Assert.Equal("expireDate", Assert.Single(error.Issues).Path);
    }

    [Fact]
    public void ExpiryBeforeEffectiveDateIsRejected()
    {
        var content = new DraftContent { EffectiveDate = new(2026, 11, 19), ExpireDate = new(2026, 11, 18) };
        Assert.Throws<DraftValidationException>(() => DraftValidation.RequireContractDates(content));
    }

    [Fact]
    public void CorrectedExpiryAndEqualDatesAreAccepted()
    {
        var content = new DraftContent { EffectiveDate = new(2026, 11, 19), ExpireDate = new(2026, 11, 19) };
        DraftValidation.RequireContractDates(content);
        content.EffectiveDate = null;
        DraftValidation.RequireContractDates(content);
    }
}
