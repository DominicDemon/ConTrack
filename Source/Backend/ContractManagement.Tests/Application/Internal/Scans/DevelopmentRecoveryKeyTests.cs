using ContractManagement.API.Domains.Email;
using ContractManagement.Application.Features.Internal.Scans;
using ContractManagement.Infrastructure.Persistence.Master.Scans;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ContractManagement.Tests.Application.Internal.Scans;

/// <summary>The recovery key a developer machine makes for itself, and the mail parts added with the email queue.</summary>
public sealed class DevelopmentRecoveryKeyTests
{
    [Fact]
    public void Development_WithoutAConfiguredKey_MakesOnePairAndKeepsIt()
    {
        var root = Directory.CreateTempSubdirectory("dev-key-test");
        try
        {
            var api = Directory.CreateDirectory(Path.Combine(root.FullName, "api")).FullName;
            var first = new PemRecoveryPublicKey(Options.Create(new FileEncryptionOptions()), new Env("Development", api));
            var second = new PemRecoveryPublicKey(Options.Create(new FileEncryptionOptions()), new Env("Development", api));

            Assert.True(first.UsesDevelopmentKey);
            Assert.Equal(first.KeyId, second.KeyId);
            using var privateKey = first.CreatePrivateKey();
            Assert.NotNull(privateKey);
            Assert.True(File.Exists(Path.Combine(root.FullName, "private-storage", "dev-keys", "recovery-private.pem")));
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public void Production_WithoutAConfiguredKey_StillRefuses()
    {
        var key = new PemRecoveryPublicKey(Options.Create(new FileEncryptionOptions()), new Env("Production", Path.GetTempPath()));

        Assert.False(key.UsesDevelopmentKey);
        Assert.Throws<ContractManagement.Application.Features.Internal.InternalAppException>(() => key.KeyId);
    }

    [Fact]
    public void SmtpMessage_KeepsReplyToAndAttachmentTogether()
    {
        var options = new SmtpEmailOptions { Username = "s@example.com", FromAddress = "s@example.com", AppPassword = "x" };
        var message = new EmailMessage("to@example.com", "S", "T", "<p>H</p>", Attachments: [new EmailAttachment("a.pdf", "application/pdf", [1])], ReplyTo: "me@example.com");

        using var mail = SmtpEmailTransport.BuildMailMessage(message, options, null);

        Assert.Single(mail.Attachments);
        Assert.Single(mail.ReplyToList);
    }

    private sealed class Env(string name, string root) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "test";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
