using ContractManagement.Application.Features.Contracts.Signing;
using Xunit;

namespace ContractManagement.Tests.Application.Services.Contract;

public sealed class ElectronicSigningWorkflowTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Start_LocksSession_AndWaitsForDirector()
    {
        var session = ElectronicSigningWorkflow.Start(CreateSession(), Now);

        Assert.Equal(ElectronicSigningStatus.AwaitingDirector, session.Status);
        Assert.True(session.IsLocked);
        Assert.Equal(Now, session.LockedAt);
    }

    [Fact]
    public void CustomerCannotSignBeforeDirector()
    {
        var session = ElectronicSigningWorkflow.Start(CreateSession(), Now);

        Assert.Throws<InvalidOperationException>(
            () => ElectronicSigningWorkflow.CompleteCustomerSignature(session, Now));
    }

    [Fact]
    public void CustomerCannotSignUntilDirectorAndCustomerOtpStepsComplete()
    {
        var started = ElectronicSigningWorkflow.Start(CreateSession(), Now);
        var directorSigned = ElectronicSigningWorkflow.CompleteDirectorSignature(started, Now.AddMinutes(1));

        Assert.Equal(ElectronicSigningStatus.CustomerOtpPending, directorSigned.Status);
        Assert.Throws<InvalidOperationException>(
            () => ElectronicSigningWorkflow.CompleteCustomerSignature(directorSigned, Now.AddMinutes(2)));

        var otpVerified = ElectronicSigningWorkflow.MarkCustomerOtpVerified(directorSigned, Now.AddMinutes(2));
        Assert.Equal(ElectronicSigningStatus.CustomerSigning, otpVerified.Status);
    }

    [Fact]
    public void DirectorCanRejectOnlyWithAReasonWhileAwaitingSignature()
    {
        var started = ElectronicSigningWorkflow.Start(CreateSession(), Now);
        var rejected = ElectronicSigningWorkflow.RejectByDirector(started, "Incorrect contract amount");

        Assert.Equal(ElectronicSigningStatus.DirectorRejected, rejected.Status);
        Assert.True(rejected.IsLocked);
        Assert.Equal("Incorrect contract amount", rejected.RejectionReason);
        Assert.Throws<InvalidOperationException>(
            () => ElectronicSigningWorkflow.MarkCustomerOtpVerified(rejected, Now.AddMinutes(1)));
    }

    [Fact]
    public void CompletingBothSignaturesLocksSessionPermanently()
    {
        var started = ElectronicSigningWorkflow.Start(CreateSession(), Now);
        var directorSigned = ElectronicSigningWorkflow.CompleteDirectorSignature(started, Now.AddMinutes(1));
        var customerOtpVerified = ElectronicSigningWorkflow.MarkCustomerOtpVerified(directorSigned, Now.AddMinutes(2));
        var completed = ElectronicSigningWorkflow.CompleteCustomerSignature(customerOtpVerified, Now.AddMinutes(3));

        Assert.Equal(ElectronicSigningStatus.Completed, completed.Status);
        Assert.True(completed.IsLocked);
        Assert.Equal(Now.AddMinutes(3), completed.CompletedAt);
    }

    [Fact]
    public void CompletedSessionCannotBeExpired()
    {
        var session = CreateSession() with
        {
            Status = ElectronicSigningStatus.Completed,
            IsLocked = true,
            CompletedAt = Now
        };

        Assert.Throws<InvalidOperationException>(
            () => ElectronicSigningWorkflow.Expire(session, Now.AddDays(1)));
    }

    private static ElectronicSigningSession CreateSession() =>
        new(42, 7, ElectronicSigningStatus.Draft, false, null, null);
}
