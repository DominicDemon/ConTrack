using ContractManagement.API.Domains.Email;
using ContractManagement.Application.Features.Internal;
using ContractManagement.Application.Features.Internal.Scans;
using ContractManagement.Domain.Internal.Scans;
using ContractManagement.Infrastructure.Persistence.Master.Scans;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ContractManagement.Tests.Application.Internal.Scans;

public sealed class ScanHardeningTests
{
    private static string[] Values(IEnumerable<ScanKeyword> keywords, ScanKeywordType type) =>
        keywords.Where(keyword => keyword.Type == type).Select(keyword => keyword.Value).ToArray();

    [Fact]
    public void Email_IsReadLowerCaseOnceAndNotExpected()
    {
        var keywords = ContractScanKeywordExtractor.Extract(
            "DTC., Ltd | Website: www.dtctech.vn | Email: Info@DTCtech.vn\nLiên hệ: info@dtctech.vn. Bảng: @ oO");

        Assert.Equal(["info@dtctech.vn"], Values(keywords, ScanKeywordType.Email));
        Assert.DoesNotContain(ScanKeywordType.Email, ContractScanKeywordExtractor.ExpectedTypes);
    }

    [Theory]
    [InlineData(" Ke.Toan@Congty.com.vn ", "ke.toan@congty.com.vn")]
    [InlineData("not-an-email", null)]
    [InlineData("a@b", null)]
    [InlineData("two@a.vn three@b.vn", null)]
    public void TypedEmail_IsStoredLowerCaseOrRefused(string typed, string? expected) =>
        Assert.Equal(expected, ContractScanKeywordExtractor.NormaliseTyped(ScanKeywordType.Email, typed));

    [Theory]
    [InlineData("PARTY A _ WANEK FURNITURE CO., LTD :", "WANEK FURNITURE CO., LTD")]
    [InlineData("BÊN A ' CÔNG TY TNHH KỸ NGHỆ GỖ HOA NÉT", "CÔNG TY TNHH KỸ NGHỆ GỖ HOA NÉT")]
    [InlineData("BENA * CONG TY TNHH ABC", "CONG TY TNHH ABC")]
    [InlineData("PARTY B : DTC MEDIA AND TECHNOLOGY APPLICATION COMPANY", "DTC MEDIA AND TECHNOLOGY APPLICATION COMPANY")]
    public void PartyName_IsReadWhenOcrMisreadsTheColon(string line, string expected) =>
        Assert.Equal([expected], Values(ContractScanKeywordExtractor.Extract(line), ScanKeywordType.CustomerName));

    [Fact]
    public void LabelAndValueOnOneLine_AsReadInSingleColumnMode_YieldBothParties()
    {
        var keywords = ContractScanKeywordExtractor.Extract(
            "Tel / Điện thoại : 02743.655 200 Fax: 02743.655 192\nTax code / MST : 3700937241\n"
            + "Tel / Điện thoại : (+84-24) 3 557-3636 Fax: (+84-24) 3 569-0588\nTax code / MST : 0105801335");

        Assert.Equal(["3700937241", "0105801335"], Values(keywords, ScanKeywordType.TaxCode));
        Assert.Equal(["02743655200", "02435573636"], Values(keywords, ScanKeywordType.PhoneNumber));
    }

    [Fact]
    public void Compose_SetsReplyToTheSender_AndTheSmtpMessageCarriesIt()
    {
        var content = new ScanEmailContent("HD-1", [], "hd.pdf", "application/pdf", [1]);
        var options = new SmtpEmailOptions { Username = "system@example.com", FromAddress = "system@example.com", AppPassword = "x" };

        var message = ScanEmail.Compose("to@example.com", content, "A", null, "staff@example.com");
        using var mail = SmtpEmailTransport.BuildMailMessage(message, options, null);
        using var plain = SmtpEmailTransport.BuildMailMessage(ScanEmail.Compose("to@example.com", content, "A", null, "not an email"), options, null);

        Assert.Equal("staff@example.com", Assert.Single(mail.ReplyToList).Address);
        Assert.Empty(plain.ReplyToList);
    }

    [Fact]
    public void Queue_RefusesRecipientsOverTheHourlyLimit_WithoutCountingThem()
    {
        var queue = new ScanEmailQueue(new NoTransport(), new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new ScanEmailOptions { MaxRecipientsPerHour = 5 }), NullLogger<ScanEmailQueue>.Instance);

        queue.Reserve(7, 3);
        var refused = Assert.Throws<InternalAppException>(() => queue.Reserve(7, 3));
        queue.Reserve(7, 2);
        queue.Reserve(8, 5);

        Assert.Equal(429, refused.Status);
        Assert.Contains("tối đa 5 email", refused.Message);
    }

    [Fact]
    public void Queue_ShowsProgressOnlyToTheEmployeeWhoStartedIt()
    {
        var queue = new ScanEmailQueue(new NoTransport(), new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new ScanEmailOptions()), NullLogger<ScanEmailQueue>.Instance);
        var content = new ScanEmailContent("HD-1", [], "hd.pdf", "application/pdf", [1]);

        var job = queue.Enqueue(7, "OCR_JOB", 1, "hd.pdf", false, [ScanEmail.Compose("to@example.com", content, "A", null)]);

        Assert.False(job.Done);
        Assert.Equal(ScanEmailStates.Waiting, Assert.Single(queue.Find(job.JobId, 7)!.Recipients).Status);
        Assert.Null(queue.Find(job.JobId, 8));
    }

    [Fact]
    public void KeyLock_ReportsTheTimeLeft()
    {
        var tracker = new FileKeyAttemptTracker();
        for (var attempt = 0; attempt < FileKeyAttemptTracker.MaxFailures - 1; attempt++) tracker.RecordFailure(3);

        Assert.Null(tracker.RemainingLock(3));
        tracker.RecordFailure(3);

        Assert.InRange(tracker.RemainingLock(3)!.Value, FileKeyAttemptTracker.LockDuration - TimeSpan.FromSeconds(5), FileKeyAttemptTracker.LockDuration);
        Assert.Null(tracker.RemainingLock(4));
    }

    private sealed class NoTransport : IEmailTransport
    {
        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
