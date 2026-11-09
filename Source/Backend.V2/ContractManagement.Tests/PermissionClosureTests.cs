using ContractManagement.Application.Common.Security;
using Xunit;

namespace ContractManagement.Tests;

public class PermissionClosureTests
{
    [Fact]
    public void APermissionBringsWhatItDependsOn_AllTheWayDown()
    {
        // 3 needs 2, 2 needs 1.
        var result = PermissionClosure.Expand([3], [(3, 2), (2, 1)]);

        Assert.Equal([1, 2, 3], result.Order());
    }

    [Fact]
    public void ACycleInTheTableDoesNotLoopForever()
    {
        var result = PermissionClosure.Expand([1], [(1, 2), (2, 1)]);

        Assert.Equal([1, 2], result.Order());
    }

    [Fact]
    public void APermissionThatOthersDependOnBringsNothingElse()
    {
        var result = PermissionClosure.Expand([1], [(3, 1)]);

        Assert.Equal([1], result.Order());
    }
}
