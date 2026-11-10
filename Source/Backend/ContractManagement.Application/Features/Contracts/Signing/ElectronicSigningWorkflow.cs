namespace ContractManagement.Application.Features.Contracts.Signing;

/// <summary>Signing lifecycle, independent from the existing contract lifecycle.</summary>
public enum ElectronicSigningStatus : byte
{
    Draft = 0,
    AwaitingDirector = 1,
    DirectorRejected = 2,
    AwaitingCustomer = 3,
    CustomerOtpPending = 4,
    CustomerSigning = 5,
    Completed = 6,
    Expired = 7,
    Cancelled = 8
}

public enum ElectronicSignerRole : byte
{
    Director = 1,
    Customer = 2
}

public enum ElectronicSigningArtifactKind : byte
{
    OriginalPdf = 1,
    DirectorSignedPdf = 2,
    FinalSignedPdf = 3,
    DirectorSignatureImage = 4,
    CustomerSignatureImage = 5
}

public sealed record ElectronicSigningSession(
    int ContractId,
    int VersionId,
    ElectronicSigningStatus Status,
    bool IsLocked,
    DateTime? LockedAt,
    DateTime? CompletedAt,
    string? RejectionReason = null);

/// <summary>Pure transition rules for an electronic signing session.</summary>
public static class ElectronicSigningWorkflow
{
    public static void EnsureCanStart(ElectronicSigningSession session, DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(session);
        EnsureUtc(utcNow, nameof(utcNow));
        if (session.IsLocked || session.Status is not ElectronicSigningStatus.Draft)
        {
            throw new InvalidOperationException("Only an unlocked draft can start signing.");
        }
    }

    public static ElectronicSigningSession Start(ElectronicSigningSession session, DateTime utcNow)
    {
        EnsureCanStart(session, utcNow);
        return session with
        {
            Status = ElectronicSigningStatus.AwaitingDirector,
            IsLocked = true,
            LockedAt = utcNow
        };
    }

    public static ElectronicSigningSession CompleteDirectorSignature(ElectronicSigningSession session, DateTime utcNow)
    {
        EnsureCanSign(session, ElectronicSignerRole.Director, utcNow);
        return session with { Status = ElectronicSigningStatus.CustomerOtpPending };
    }

    public static ElectronicSigningSession RejectByDirector(ElectronicSigningSession session, string reason)
    {
        ArgumentNullException.ThrowIfNull(session);
        EnsureActive(session);
        if (session.Status is not ElectronicSigningStatus.AwaitingDirector)
        {
            throw new InvalidOperationException("Only a session awaiting the Director can be rejected.");
        }

        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 2000)
        {
            throw new ArgumentException("A rejection reason of at most 2000 characters is required.", nameof(reason));
        }

        return session with
        {
            Status = ElectronicSigningStatus.DirectorRejected,
            IsLocked = true,
            RejectionReason = reason.Trim()
        };
    }

    public static ElectronicSigningSession MarkCustomerOtpVerified(ElectronicSigningSession session, DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(session);
        EnsureUtc(utcNow, nameof(utcNow));
        EnsureActive(session);
        if (session.Status is not ElectronicSigningStatus.CustomerOtpPending)
        {
            throw new InvalidOperationException("Customer OTP is only valid after Director signing.");
        }

        return session with { Status = ElectronicSigningStatus.CustomerSigning };
    }

    public static ElectronicSigningSession CompleteCustomerSignature(ElectronicSigningSession session, DateTime utcNow)
    {
        EnsureCanSign(session, ElectronicSignerRole.Customer, utcNow);
        return session with
        {
            Status = ElectronicSigningStatus.Completed,
            IsLocked = true,
            CompletedAt = utcNow
        };
    }

    public static ElectronicSigningSession Cancel(ElectronicSigningSession session, DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(session);
        EnsureUtc(utcNow, nameof(utcNow));
        EnsureActive(session);
        return session with { Status = ElectronicSigningStatus.Cancelled, IsLocked = true };
    }

    public static ElectronicSigningSession Expire(ElectronicSigningSession session, DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(session);
        EnsureUtc(utcNow, nameof(utcNow));
        EnsureActive(session);
        return session with { Status = ElectronicSigningStatus.Expired, IsLocked = true };
    }

    private static void EnsureCanSign(ElectronicSigningSession session, ElectronicSignerRole role, DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(session);
        EnsureUtc(utcNow, nameof(utcNow));
        EnsureActive(session);
        if (!session.IsLocked)
        {
            throw new InvalidOperationException("The session must be frozen before signing.");
        }

        var expected = role is ElectronicSignerRole.Director
            ? ElectronicSigningStatus.AwaitingDirector
            : ElectronicSigningStatus.CustomerSigning;
        if (session.Status != expected)
        {
            throw new InvalidOperationException("The signer is not allowed to sign at this workflow step.");
        }
    }

    private static void EnsureActive(ElectronicSigningSession session)
    {
        if (session.Status is ElectronicSigningStatus.Completed
            or ElectronicSigningStatus.Cancelled
            or ElectronicSigningStatus.DirectorRejected
            or ElectronicSigningStatus.Expired)
        {
            throw new InvalidOperationException("The signing session is no longer active.");
        }
    }

    private static void EnsureUtc(DateTime value, string parameterName)
    {
        if (value.Kind is not DateTimeKind.Utc)
        {
            throw new ArgumentException("Workflow timestamps must be UTC.", parameterName);
        }
    }
}
