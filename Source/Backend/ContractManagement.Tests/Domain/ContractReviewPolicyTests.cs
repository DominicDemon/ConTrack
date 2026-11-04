using ContractManagement.Domain.Internal;
using S = ContractManagement.Domain.Internal.ContractReviewStatus;
using D = ContractManagement.Domain.Internal.ContractDocumentStatus;

namespace ContractManagement.Tests.Domain;

public class ContractReviewPolicyTests
{
    [Theory]
    [InlineData(D.Draft, null, false, 0, true)]
    [InlineData(D.Negotiating, S.ChangesRequested, false, 0, true)]   // new version created after changes requested
    [InlineData(D.Negotiating, S.Rejected, false, 0, true)]           // NEGOTIATE then new version
    [InlineData(D.Negotiating, S.Sent, false, 0, false)]              // still open
    [InlineData(D.Negotiating, S.Viewed, false, 0, false)]
    [InlineData(D.Negotiating, S.ChangesRequested, true, 0, false)]   // version still locked
    [InlineData(D.Draft, null, false, 2, false)]                      // missing fields
    [InlineData(D.PendingApproval, S.Approved, false, 0, false)]
    [InlineData(D.Cancelled, S.Rejected, false, 0, false)]
    [InlineData(D.Rejected, S.Rejected, false, 0, false)]
    public void Send(int doc, string? latest, bool locked, int missing, bool allowed) =>
        Assert.Equal(allowed, ContractReviewPolicy.CanSend(doc, latest, locked, missing) is null);

    [Theory]
    [InlineData(S.Sent, "APPROVE", null, S.Approved, D.PendingApproval)]
    [InlineData(S.Viewed, "APPROVE", null, S.Approved, D.PendingApproval)]
    [InlineData(S.Viewed, "REQUEST_CHANGES", "Sửa giá", S.ChangesRequested, D.Negotiating)]
    [InlineData(S.Sent, "REJECT", "Không phù hợp", S.Rejected, D.Rejected)]
    public void Respond(string status, string decision, string? note, string review, int document) =>
        Assert.Equal((review, document), ContractReviewPolicy.Respond(status, decision, note));

    [Fact]
    public void RespondRequiresOpenReviewNoteAndKnownDecision()
    {
        foreach (var closed in new[] { S.Approved, S.ChangesRequested, S.Rejected, S.Superseded, S.Revoked })
            Assert.Throws<InvalidOperationException>(() => ContractReviewPolicy.Respond(closed, "APPROVE", null));
        Assert.Throws<ArgumentException>(() => ContractReviewPolicy.Respond(S.Sent, "REQUEST_CHANGES", " "));
        Assert.Throws<ArgumentException>(() => ContractReviewPolicy.Respond(S.Sent, "REJECT", null));
        Assert.Throws<ArgumentException>(() => ContractReviewPolicy.Respond(S.Sent, "MAYBE", "x"));
    }

    [Theory]
    [InlineData(D.Negotiating, S.ChangesRequested, null, true, true)]
    [InlineData(D.Negotiating, S.Rejected, S.Negotiate, true, true)]
    [InlineData(D.Negotiating, S.ChangesRequested, null, false, false)] // already has an unlocked version
    [InlineData(D.Rejected, S.Rejected, null, true, false)]            // no decision yet
    [InlineData(D.Cancelled, S.Rejected, S.Close, true, false)]
    [InlineData(D.Negotiating, S.Viewed, null, true, false)]
    [InlineData(D.PendingApproval, S.Approved, null, true, false)]
    public void CreateVersion(int doc, string latest, string? followup, bool locked, bool allowed) =>
        Assert.Equal(allowed, ContractReviewPolicy.CanCreateVersion(doc, latest, followup, locked) is null);

    [Fact]
    public void FollowupOnlyOnceAfterRejection()
    {
        Assert.Equal(D.Negotiating, ContractReviewPolicy.Followup(S.Rejected, null, S.Negotiate));
        Assert.Equal(D.Cancelled, ContractReviewPolicy.Followup(S.Rejected, null, S.Close));
        Assert.Throws<InvalidOperationException>(() => ContractReviewPolicy.Followup(S.Rejected, S.Close, S.Negotiate));
        Assert.Throws<InvalidOperationException>(() => ContractReviewPolicy.Followup(S.ChangesRequested, null, S.Close));
        Assert.Throws<ArgumentException>(() => ContractReviewPolicy.Followup(S.Rejected, null, "LATER"));
    }

    [Theory]
    [InlineData(D.Draft, false, true)]
    [InlineData(D.Negotiating, false, true)]
    [InlineData(D.Negotiating, true, false)]
    [InlineData(D.PendingApproval, false, false)]
    [InlineData(D.Cancelled, false, false)]
    [InlineData(D.Rejected, false, false)]
    public void Edit(int doc, bool locked, bool allowed) => Assert.Equal(allowed, ContractReviewPolicy.CanEdit(doc, locked));

    [Fact]
    public void SnapshotHashIsLowercaseSha256OfUtf8()
    {
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", SnapshotHash.Compute("abc"));
        Assert.NotEqual(SnapshotHash.Compute("Hợp đồng"), SnapshotHash.Compute("Hop dong"));
    }

    [Theory]
    [InlineData("0901 234 484", "0901234484")]
    [InlineData("+84 (90) 123-4484", "0901234484")]
    [InlineData("090.123.44845", "09012344845")]
    [InlineData("12345", null)]
    [InlineData("+1 555 0100 000", null)]
    [InlineData(null, null)]
    public void NormalizesVietnamesePhones(string? input, string? expected) => Assert.Equal(expected, PhoneNumber.Normalize(input));

    [Fact]
    public void MasksPhone() => Assert.Equal("090****484", PhoneNumber.Mask("0901234484"));

    [Fact]
    public void TemporaryPasswordsAreUnambiguousMixedAndRandom()
    {
        var all = Enumerable.Range(0, 200).Select(_ => TemporaryPassword.Generate()).ToList();
        Assert.All(all, p =>
        {
            Assert.Equal(10, p.Length);
            Assert.DoesNotContain(p, c => "0O1lI".Contains(c));
            Assert.Contains(p, char.IsLetter);
            Assert.Contains(p, char.IsDigit);
        });
        Assert.True(all.Distinct().Count() > 195);
    }

    [Fact]
    public void CustomerPasswordRules()
    {
        Assert.True(ContractManagement.Domain.Internal.CustomerPasswordRules.IsValid("abc12345"));
        Assert.False(ContractManagement.Domain.Internal.CustomerPasswordRules.IsValid("abcdefgh"));
        Assert.False(ContractManagement.Domain.Internal.CustomerPasswordRules.IsValid("1234567"));
    }
}
