using ContractManagement.API.Domains.Email;
using ContractManagement.Application.Features.Internal.Scans;
namespace ContractManagement.Tests.Application.Internal.Scans;

public sealed class ScanEmailTests
{
    private static readonly ScanEmailContent Content = new(
        "HD-20261026-000001",
        [("Số hợp đồng", "HD-20261026-000001"), ("Khách hàng", "Công ty <ABC> & Co")],
        "hop-dong.pdf",
        "application/pdf",
        [0x25, 0x50, 0x44, 0x46]);

    [Fact]
    public void Recipients_SplitsPastedListsTrimsAndDropsDuplicates()
    {
        var addresses = ScanEmail.Recipients(["a@example.com, b@example.com", " c@example.com ;A@EXAMPLE.COM", "d@example.com\ne@example.com"]);

        Assert.Equal(["a@example.com", "b@example.com", "c@example.com", "d@example.com", "e@example.com"], addresses);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Recipients_RequiresAtLeastOne(string typed) =>
        Assert.Contains("ít nhất một email", Assert.Throws<ArgumentException>(() => ScanEmail.Recipients([typed])).Message);

    [Fact]
    public void Recipients_NamesEveryInvalidAddress()
    {
        var error = Assert.Throws<ArgumentException>(() => ScanEmail.Recipients(["ok@example.com", "khong-phai-email", "x@"]));

        Assert.Contains("khong-phai-email", error.Message);
        Assert.Contains("x@", error.Message);
        Assert.DoesNotContain("ok@example.com", error.Message);
    }

    [Fact]
    public void Recipients_RefusesMoreThanTheLimit()
    {
        var many = Enumerable.Range(1, ScanEmail.MaxRecipients + 1).Select(i => (string?)$"u{i}@example.com").ToList();

        Assert.Contains($"tối đa {ScanEmail.MaxRecipients}", Assert.Throws<ArgumentException>(() => ScanEmail.Recipients(many)).Message);
        Assert.Equal(ScanEmail.MaxRecipients, ScanEmail.Recipients(many.Take(ScanEmail.MaxRecipients)).Count);
    }

    [Fact]
    public void Note_IsOptionalAndLimited()
    {
        Assert.Null(ScanEmail.Note("   "));
        Assert.Equal("Nhờ anh xem", ScanEmail.Note("  Nhờ anh xem  "));
        Assert.Throws<ArgumentException>(() => ScanEmail.Note(new string('a', ScanEmail.MaxNoteLength + 1)));
    }

    [Fact]
    public void Compose_AttachesTheFileAndEscapesTheHtml()
    {
        var message = ScanEmail.Compose("to@example.com", Content, "Nguyễn <Văn> A", "Xem giúp <b>gấp</b>");

        Assert.Equal("to@example.com", message.Recipient);
        Assert.Equal("Hợp đồng: HD-20261026-000001", message.Subject);
        var attachment = Assert.Single(message.Attachments!);
        Assert.Equal("hop-dong.pdf", attachment.FileName);
        Assert.Equal(Content.Content, attachment.Content);
        Assert.Contains("Nguyễn &lt;Văn&gt; A", message.HtmlBody);
        Assert.Contains("Xem giúp &lt;b&gt;gấp&lt;/b&gt;", message.HtmlBody);
        Assert.Contains("Công ty &lt;ABC&gt; &amp; Co", message.HtmlBody);
        Assert.DoesNotContain("<b>gấp</b>", message.HtmlBody);
        Assert.Contains("Xem giúp <b>gấp</b>", message.TextBody);
        Assert.Contains("Khách hàng: Công ty <ABC> & Co", message.TextBody);
    }

    [Fact]
    public void Compose_LeavesOutTheNoteWhenThereIsNone()
    {
        var message = ScanEmail.Compose("to@example.com", Content, "A", null);

        Assert.DoesNotContain("Lời nhắn", message.TextBody);
        Assert.DoesNotContain("Lời nhắn", message.HtmlBody);
    }

    [Fact]
    public void SmtpMessage_CarriesTheAttachmentWithItsName()
    {
        var options = new SmtpEmailOptions { Username = "sender@example.com", FromAddress = "sender@example.com", AppPassword = "x" };
        var content = Content with { FileName = "Hợp đồng ký.pdf" };

        using var mail = SmtpEmailTransport.BuildMailMessage(ScanEmail.Compose("to@example.com", content, "A", null), options, null);

        var attachment = Assert.Single(mail.Attachments);
        Assert.Equal("Hợp đồng ký.pdf", attachment.Name);
        Assert.Equal("application/pdf", attachment.ContentType.MediaType);
        using var copy = new MemoryStream();
        attachment.ContentStream.CopyTo(copy);
        Assert.Equal(content.Content, copy.ToArray());
    }

    [Fact]
    public void SmtpMessage_WithoutAttachmentsIsUnchanged()
    {
        var options = new SmtpEmailOptions { Username = "sender@example.com", FromAddress = "sender@example.com", AppPassword = "x" };

        using var mail = SmtpEmailTransport.BuildMailMessage(new EmailMessage("to@example.com", "S", "T", "<p>H</p>"), options, null);

        Assert.Empty(mail.Attachments);
    }
}
