using Contrack.Domain.Tenancy;

namespace Contrack.UnitTests;

/// <summary>Quy ước: mỗi business rule có ít nhất một test, tên test nói rõ rule.</summary>
public class TenantAccessRuleTests
{
    [Theory]
    [InlineData(TenantStatus.Active, true)]
    [InlineData(TenantStatus.Provisioning, false)]
    [InlineData(TenantStatus.Suspended, false)]
    [InlineData(TenantStatus.Terminated, false)]
    public void Only_active_tenants_can_be_accessed(TenantStatus status, bool expected)
    {
        Assert.Equal(expected, TenantAccessRule.CanAccess(status));
    }
}
