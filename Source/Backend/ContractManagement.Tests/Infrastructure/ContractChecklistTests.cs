using ContractManagement.Infrastructure.Persistence.Master;

namespace ContractManagement.Tests.Infrastructure;

public class ContractChecklistTests
{
    [Fact]
    public void EmptyAndHeadingOnlyRemainAdvisoryFindings()
    {
        var result = ContractChecklistService.Analyze(["payment", "warranty"],
            [("p1", "Điều 5: Thanh toán"), ("p2", "Điều 6: Trách nhiệm các bên")], []);
        Assert.All(result, finding => Assert.False(finding.Found));
        Assert.Equal("p1", result[0].Location);
        Assert.Null(result[1].Location);
        Assert.False(Assert.Single(ContractChecklistService.Analyze(["payment"],
            [("empty", "Giá trị hợp đồng, phương thức thanh toán và thời hạn thanh toán theo thỏa thuận của các bên\n")], [])).Found);
    }

    [Fact]
    public void ReadsVietnameseBodyAndCellsAndSkipsEarlierEmptyHeading()
    {
        var result = ContractChecklistService.Analyze(["payment"],
            [("toc", "Thanh toán"), ("other", "Bảo hành"), ("cell1", "THANH TOAN"),
             ("cell2", "Bên A chuyển khoản toàn bộ giá trị cho Bên B trong mười ngày kể từ nghiệm thu.")], []);
        var finding = Assert.Single(result);
        Assert.True(finding.Found);
        Assert.Equal("cell1", finding.Location);
        Assert.Contains("chuyển khoản", finding.Excerpt);
    }

    [Fact]
    public void MissingContentCanBeReviewedWithRecordedReasonWithoutPretendingItWasFound()
    {
        var resolution = new ChecklistResolution("NOT_APPLICABLE", "Không cung cấp thiết bị", "Hành chính", DateTime.UtcNow);
        var finding = Assert.Single(ContractChecklistService.Analyze(["warranty"], [], new() { ["warranty"] = resolution }));
        Assert.False(finding.Found);
        Assert.Equal(resolution, finding.Resolution);
    }

    [Fact]
    public void ConfigurationRequiresExplicitValidSelectionButAllowsNoApplicableRules()
    {
        Assert.Throws<ArgumentException>(() => ContractChecklistService.ValidateRules(null));
        Assert.Throws<ArgumentException>(() => ContractChecklistService.ValidateRules(["payment", "payment"]));
        Assert.Throws<ArgumentException>(() => ContractChecklistService.ValidateRules(["unknown"]));
        ContractChecklistService.ValidateRules([]);
        ContractChecklistService.ValidateRules(["payment", "effect"]);
    }
}
