using ContractManagement.Application.Features.Internal;
using ContractManagement.Infrastructure.Persistence.Master;

namespace ContractManagement.Tests.Infrastructure;

/// <summary>An activated Word template keeps each article as its own term instead of one combined body.</summary>
public class WordSectionTests
{
    [Fact]
    public void EachHeadingStartsItsOwnTerm()
    {
        var terms = InternalWordTemplates.Sections([
            new("0", "Nội dung", "HỢP ĐỒNG", Heading: true),
            new("0b", "Nội dung", "  KINH   TẾ ", Heading: true),
            new("1", "Nội dung", "Hôm nay chúng tôi gồm có:"),
            new("2", "Nội dung", "Bên A: Công ty X"),
            new("3", "Nội dung", "Điều 1: Công việc", Heading: true),
            new("4", "Nội dung", "Bên B cài đặt phần mềm."),
            new("5", "Nội dung", ""),
            new("6", "Nội dung", "ĐIỀU KHOẢN THANH TOÁN", Heading: true),
            new("7", "Nội dung · Ô bảng", "Đợt 1: 50%"),
            new("8", "Đầu trang", "CÔNG TY DTC"),
        ]);

        Assert.Equal(["HỢP ĐỒNG KINH TẾ", "Điều 1: Công việc", "ĐIỀU KHOẢN THANH TOÁN", "Đầu trang"], terms.Select(t => t.Title));
        Assert.Equal("Hôm nay chúng tôi gồm có:\nBên A: Công ty X", terms[0].Content);
        Assert.Equal("Bên B cài đặt phần mềm.", terms[1].Content);
        Assert.Equal("Đợt 1: 50%", terms[2].Content);
        Assert.Equal("CÔNG TY DTC", terms[3].Content);
        Assert.All(terms, t => Assert.False(t.Editable));
        Assert.Equal(terms.Count, terms.Select(t => t.Key).Distinct().Count());
    }

    [Fact]
    public void TextBeforeFirstHeadingIsTheOpening()
    {
        var terms = InternalWordTemplates.Sections([new WordBlock("0", "Nội dung", "Căn cứ Bộ luật Dân sự"), new("1", "Nội dung", "Điều 1. Phạm vi", Heading: true)]);
        Assert.Equal(["Phần mở đầu", "Điều 1. Phạm vi"], terms.Select(t => t.Title));
    }
}
