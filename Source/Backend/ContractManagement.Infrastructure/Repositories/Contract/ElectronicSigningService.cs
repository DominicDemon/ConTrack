using ContractManagement.API.Domains.CustomerAccess;
using ContractManagement.Application.Features.Contracts.Signing;
using ContractManagement.Domains.Interfaces.Contract;
using ContractManagement.API.Domains.Policies.Contract;
using ContractManagement.Domains.Interfaces.File;
using ContractManagement.Domains.Services.File;
using ContractManagement.Infrastructure.MultiTenancy.Interfaces;
using ContractManagement.Infrastructure.Persistence.Application;
using ContractManagement.Infrastructure.Persistence.Application.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace ContractManagement.Domains.Services.Contract;

public sealed class ElectronicSigningService : IElectronicSigningService
{
    private const byte DirectorRole = (byte)ElectronicSignerRole.Director;
    private const byte CustomerRole = (byte)ElectronicSignerRole.Customer;
    private const byte PendingStatus = 0;

    private readonly DbDtctechContext _dbContext;
    private readonly CustomerAccessCryptography _cryptography;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IContractDocumentPreviewService _previewService;
    private readonly IPrivateFileStorage _privateFileStorage;
    private readonly ICurrentTenant _currentTenant;
    private readonly IElectronicSigningPdfService _pdfService;
    private readonly IContractResourceAuthorizationService _contractAuthorization;

    public ElectronicSigningService(
        DbDtctechContext dbContext,
        CustomerAccessCryptography cryptography,
        IHttpContextAccessor httpContextAccessor,
        IContractDocumentPreviewService previewService,
        IPrivateFileStorage privateFileStorage,
        ICurrentTenant currentTenant,
        IElectronicSigningPdfService pdfService,
        IContractResourceAuthorizationService contractAuthorization)
    {
        _dbContext = dbContext;
        _cryptography = cryptography;
        _httpContextAccessor = httpContextAccessor;
        _previewService = previewService;
        _privateFileStorage = privateFileStorage;
        _currentTenant = currentTenant;
        _pdfService = pdfService;
        _contractAuthorization = contractAuthorization;
    }

    public async Task<RequestElectronicSigningOtpResponse> RequestOtpAsync(
        long sessionId,
        RequestElectronicSigningOtpRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Role is not ElectronicSignerRole.Customer)
        {
            throw new InvalidOperationException("OTP is only used for customer identity verification.");
        }

        var signer = await FindSignerAsync(sessionId, request.Role, cancellationToken);
        if ((ElectronicSigningStatus)signer.Session.Status is not ElectronicSigningStatus.CustomerOtpPending)
        {
            throw new InvalidOperationException("Customer OTP is unavailable until the Director has signed.");
        }

        var now = DateTime.UtcNow;
        var expiresAt = now.AddMinutes(5);
        if (signer.OtpWindowStartedAt is null || signer.OtpWindowStartedAt <= now.AddHours(-1))
        {
            signer.OtpWindowStartedAt = now;
            signer.OtpRequestCount = 0;
        }

        if (signer.OtpRequestCount >= 5)
        {
            throw new InvalidOperationException("The OTP request limit has been reached. Try again later.");
        }

        if (signer.OtpLastRequestedAt > now.AddSeconds(-60))
        {
            throw new InvalidOperationException("Wait before requesting another OTP.");
        }

        var otp = _cryptography.CreateOtp();
        var challengeId = _cryptography.CreatePublicChallengeId();

        var activeOtps = await _dbContext.ContractSigningOtps
            .Where(x => x.SignerId == signer.Id && x.UsedAt == null && x.ExpiresAt > now)
            .ToListAsync(cancellationToken);
        foreach (var activeOtp in activeOtps)
        {
            activeOtp.UsedAt = now;
        }

        _dbContext.ContractSigningOtps.Add(new ContractSigningOtp
        {
            SignerId = signer.Id,
            ChallengeId = challengeId,
            OtpHash = _cryptography.HashSecret(challengeId + ":" + otp),
            OtpCiphertext = _cryptography.EncryptScalar(otp),
            ExpiresAt = expiresAt,
            DeliveryStatus = "Pending",
            DeliveryNextAttemptAt = now,
            CreatedAt = now
        });
        signer.OtpRequestCount++;
        signer.OtpLastRequestedAt = now;
        _dbContext.ContractSigningAuditLogs.Add(CreateAudit(
            sessionId, signer.Id, "CustomerOtpQueued", now));
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new RequestElectronicSigningOtpResponse(challengeId, expiresAt, "GsmModem");
    }

    public async Task VerifyOtpAsync(
        long sessionId,
        VerifyElectronicSigningOtpRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Role is not ElectronicSignerRole.Customer)
        {
            throw new InvalidOperationException("OTP is only used for customer identity verification.");
        }

        var signer = await FindSignerAsync(sessionId, request.Role, cancellationToken);
        if ((ElectronicSigningStatus)signer.Session.Status is not ElectronicSigningStatus.CustomerOtpPending)
        {
            throw new InvalidOperationException("Customer OTP is unavailable until the Director has signed.");
        }
        var challenge = await _dbContext.ContractSigningOtps
            .SingleOrDefaultAsync(x => x.SignerId == signer.Id && x.ChallengeId == request.ChallengeId, cancellationToken)
            ?? throw new UnauthorizedAccessException("OTP không hợp lệ hoặc đã hết hạn.");
        var now = DateTime.UtcNow;
        if (challenge.UsedAt.HasValue
            || challenge.ExpiresAt <= now
            || challenge.FailedAttemptCount >= 5
            || challenge.DeliveryStatus is not "Sent")
        {
            throw new UnauthorizedAccessException("OTP không hợp lệ hoặc đã hết hạn.");
        }

        var suppliedHash = _cryptography.HashSecret(request.ChallengeId + ":" + request.Otp);
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(challenge.OtpHash),
                Encoding.ASCII.GetBytes(suppliedHash)))
        {
            challenge.FailedAttemptCount++;
            if (challenge.FailedAttemptCount >= 5) challenge.UsedAt = now;
            _dbContext.ContractSigningAuditLogs.Add(CreateAudit(
                sessionId, signer.Id, "CustomerOtpVerificationFailed", now));
            await _dbContext.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedAccessException("OTP không hợp lệ hoặc đã hết hạn.");
        }

        challenge.VerifiedAt = now;
        challenge.UsedAt = now;
        var current = ToWorkflowSession(signer.Session);
        var customerSigning = ElectronicSigningWorkflow.MarkCustomerOtpVerified(current, now);
        signer.Session.Status = (byte)customerSigning.Status;
        _dbContext.ContractSigningAuditLogs.Add(CreateAudit(sessionId, signer.Id, "CustomerOtpVerified", now));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<RequestElectronicSigningOtpResponse> RequestCustomerOtpAsync(
        string linkToken,
        CancellationToken cancellationToken = default)
    {
        var link = await ResolveCustomerLinkAsync(linkToken, cancellationToken);
        return await RequestOtpAsync(
            link.SessionId,
            new RequestElectronicSigningOtpRequest(ElectronicSignerRole.Customer),
            cancellationToken);
    }

    public async Task VerifyCustomerOtpAsync(
        string linkToken,
        VerifyElectronicSigningOtpRequest request,
        CancellationToken cancellationToken = default)
    {
        var link = await ResolveCustomerLinkAsync(linkToken, cancellationToken);
        await VerifyOtpAsync(
            link.SessionId,
            new VerifyElectronicSigningOtpRequest(
                ElectronicSignerRole.Customer, request.ChallengeId, request.Otp),
            cancellationToken);
    }

    public async Task<CustomerSigningPdfResponse> GetCustomerSigningPdfAsync(
        string linkToken,
        string challengeId,
        CancellationToken cancellationToken = default)
    {
        var link = await ResolveCustomerLinkAsync(linkToken, cancellationToken);
        await EnsureCustomerOtpVerifiedAsync(link.SignerId, challengeId, cancellationToken);
        if ((ElectronicSigningStatus)link.Session.Status is not ElectronicSigningStatus.CustomerSigning)
        {
            throw new InvalidOperationException("The customer contract is not available for signing.");
        }

        var signedPdf = link.Session.Artifacts.SingleOrDefault(
            x => x.Kind == (byte)ElectronicSigningArtifactKind.DirectorSignedPdf)
            ?? throw new InvalidDataException("Director-signed PDF artifact is missing.");
        var bytes = await ReadArtifactAsync(signedPdf, cancellationToken);
        return new CustomerSigningPdfResponse(signedPdf.FileName, bytes);
    }

    public async Task<CustomerSigningPdfResponse> SignAsCustomerAsync(
        string linkToken,
        CustomerSignElectronicContractRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.Confirmed)
        {
            throw new InvalidOperationException("The customer must explicitly confirm the signature.");
        }

        var link = await ResolveCustomerLinkAsync(linkToken, cancellationToken);
        await EnsureCustomerOtpVerifiedAsync(link.SignerId, request.ChallengeId, cancellationToken);
        var session = link.Session;
        if ((ElectronicSigningStatus)session.Status is not ElectronicSigningStatus.CustomerSigning)
        {
            throw new InvalidOperationException("The customer signing session is not active.");
        }

        var customer = link.Signer;
        var contract = await _dbContext.TblContracts
            .SingleAsync(x => x.ContractId == session.ContractId, cancellationToken);
        if (contract.CurrentVersionId != session.VersionId)
        {
            throw new InvalidOperationException("The contract version changed after the signing link was issued.");
        }
        var directorPdf = session.Artifacts.SingleOrDefault(
            x => x.Kind == (byte)ElectronicSigningArtifactKind.DirectorSignedPdf)
            ?? throw new InvalidDataException("Director-signed PDF artifact is missing.");
        var sourcePdf = await ReadArtifactAsync(directorPdf, cancellationToken);
        var signatureBytes = DecodeSignature(request.SignaturePngBase64);
        var finalPdf = _pdfService.StampSignature(sourcePdf, signatureBytes, ReadPlacement(customer));
        var now = DateTime.UtcNow;
        var tenant = _currentTenant.GetRequiredTenant();
        StoredPrivateFile? signatureFile = null;
        StoredPrivateFile? finalPdfFile = null;
        try
        {
            signatureFile = await SavePrivateArtifactAsync(
                signatureBytes, $"signing-{session.Id}-customer.png", "image/png",
                "ElectronicSigningSignature", session.ContractId, tenant.TenantCode,
                isSignature: true, cancellationToken);
            finalPdfFile = await SavePrivateArtifactAsync(
                finalPdf, $"contract-{session.ContractId}-version-{session.VersionId}-final.pdf",
                "application/pdf", "ElectronicSigningFinalPdf", session.ContractId,
                tenant.TenantCode, isSignature: false, cancellationToken);

            customer.Status = 1;
            customer.SignedAt = now;
            customer.SignatureStorageKey = signatureFile.StorageKey;
            customer.SignatureSha256 = signatureFile.Sha256;
            customer.IpAddress = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
            customer.UserAgent = _httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString();
            session.Status = (byte)ElectronicSigningStatus.Completed;
            session.CompletedAt = now;
            ContractLifecyclePolicy.EnsureCanTransition(
                (ContractManagement.API.Common.Enums.ContractStatus)contract.Status,
                ContractManagement.API.Common.Enums.ContractStatus.Signed);
            contract.Status = (byte)ContractManagement.API.Common.Enums.ContractStatus.Signed;
            contract.SignDate ??= now.Date;
            contract.UpdateDate = now;
            link.UsedAt = now;
            _dbContext.ContractSigningArtifacts.Add(CreateArtifact(
                session, ElectronicSigningArtifactKind.CustomerSignatureImage, signatureFile, null, now));
            _dbContext.ContractSigningArtifacts.Add(CreateArtifact(
                session, ElectronicSigningArtifactKind.FinalSignedPdf, finalPdfFile, null, now));
            _dbContext.ContractSigningAuditLogs.Add(CreateAudit(
                session.Id, customer.Id, "CustomerSigned", now));
            await _dbContext.SaveChangesAsync(cancellationToken);
            return new CustomerSigningPdfResponse(finalPdfFile.OriginalFileName, finalPdf);
        }
        catch
        {
            if (signatureFile is not null)
            {
                await _privateFileStorage.DeleteAsync(tenant.TenantCode, signatureFile.StorageKey, cancellationToken);
            }

            if (finalPdfFile is not null)
            {
                await _privateFileStorage.DeleteAsync(tenant.TenantCode, finalPdfFile.StorageKey, cancellationToken);
            }

            throw;
        }
    }

    private async Task<ContractSigningLink> ResolveCustomerLinkAsync(
        string token,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 200)
        {
            throw new UnauthorizedAccessException("Customer signing link is invalid or expired.");
        }

        var tokenHash = _cryptography.HashSecret(token);
        var link = await _dbContext.ContractSigningLinks
            .Include(x => x.Signer)
            .Include(x => x.Session).ThenInclude(x => x.Signers)
            .Include(x => x.Session).ThenInclude(x => x.Artifacts)
            .SingleOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);
        var now = DateTime.UtcNow;
        if (link is null
            || link.Signer.Role != CustomerRole
            || link.ExpiresAt <= now
            || link.RevokedAt.HasValue
            || link.UsedAt.HasValue
            || link.Session.Status is not ((byte)ElectronicSigningStatus.CustomerOtpPending)
                and not ((byte)ElectronicSigningStatus.CustomerSigning))
        {
            throw new UnauthorizedAccessException("Customer signing link is invalid or expired.");
        }

        return link;
    }

    private async Task EnsureCustomerOtpVerifiedAsync(
        long signerId,
        string challengeId,
        CancellationToken cancellationToken)
    {
        var verified = await _dbContext.ContractSigningOtps.AnyAsync(
            x => x.SignerId == signerId
                && x.ChallengeId == challengeId
                && x.VerifiedAt != null,
            cancellationToken);
        if (!verified)
        {
            throw new UnauthorizedAccessException("Customer OTP verification is required.");
        }
    }

    private async Task<ContractSigningSigner> FindSignerAsync(
        long sessionId,
        ElectronicSignerRole role,
        CancellationToken cancellationToken)
    {
        return await _dbContext.ContractSigningSigners
            .Include(x => x.Session)
            .SingleOrDefaultAsync(x => x.SessionId == sessionId && x.Role == (byte)role, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy người ký.");
    }

    public async Task<ElectronicSigningSessionResponse> StartAsync(
        StartElectronicSigningRequest request,
        int employeeId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        Validate(request);
        ValidatePlacement(request.DirectorPlacement, nameof(request.DirectorPlacement));
        ValidatePlacement(request.CustomerPlacement, nameof(request.CustomerPlacement));

        var contract = await _dbContext.TblContracts.AsNoTracking()
            .SingleOrDefaultAsync(x => x.ContractId == request.ContractId, cancellationToken);
        var version = await _dbContext.TblContractVersions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.VersionId == request.VersionId
                && x.ContractId == request.ContractId, cancellationToken);
        if (contract is null || version is null)
        {
            throw new KeyNotFoundException("Không tìm thấy version hợp lệ của hợp đồng.");
        }

        if (contract.CurrentVersionId != request.VersionId
            || !version.IsLocked
            || contract.Status != (byte)ContractManagement.API.Common.Enums.ContractStatus.PendingSignature)
        {
            throw new InvalidOperationException("Only the approved, frozen current contract version can be submitted for signing.");
        }

        var existing = await _dbContext.ContractSigningSessions
            .AnyAsync(x => x.ContractId == request.ContractId && x.VersionId == request.VersionId, cancellationToken);
        if (existing)
        {
            throw new InvalidOperationException("Version này đã có phiên ký điện tử.");
        }

        var now = DateTime.UtcNow;
        var preview = await _previewService.GeneratePdfAsync(
            request.ContractId, employeeId, cancellationToken);
        if (!string.Equals(preview.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The original signing artifact must be a PDF.");
        }

        var tenantCode = _currentTenant.GetRequiredTenant().TenantCode;
        StoredPrivateFile originalPdf;
        await using (var originalStream = new MemoryStream(preview.Content, writable: false))
        {
            originalPdf = await _privateFileStorage.SaveAsync(
                new PrivateFileSaveRequest(
                    originalStream,
                    preview.FileName,
                    "application/pdf",
                    preview.Content.Length,
                    tenantCode,
                    "ElectronicSigningOriginal",
                    request.ContractId,
                    PrivateFileUploadPolicies.SubmittedContractPdf()),
                cancellationToken);
        }

        var session = new ContractSigningSession
        {
            ContractId = request.ContractId,
            VersionId = request.VersionId,
            Status = (byte)ElectronicSigningStatus.AwaitingDirector,
            IsLocked = true,
            LinkLifetimeHours = request.LinkLifetimeHours,
            LockedAt = now,
            CreatedAt = now
        };
        var director = new ContractSigningSigner
        {
            Session = session,
            Role = DirectorRole,
            Name = request.DirectorName.Trim(),
            Phone = request.DirectorPhone.Trim(),
            Status = PendingStatus,
            SigningOrder = 1,
            PageNumber = request.DirectorPlacement.PageNumber,
            PositionX = request.DirectorPlacement.X,
            PositionY = request.DirectorPlacement.Y,
            Width = request.DirectorPlacement.Width,
            Height = request.DirectorPlacement.Height
        };
        var customer = new ContractSigningSigner
        {
            Session = session,
            Role = CustomerRole,
            Name = request.CustomerName.Trim(),
            Phone = request.CustomerPhone.Trim(),
            Status = PendingStatus,
            SigningOrder = 2,
            PageNumber = request.CustomerPlacement.PageNumber,
            PositionX = request.CustomerPlacement.X,
            PositionY = request.CustomerPlacement.Y,
            Width = request.CustomerPlacement.Width,
            Height = request.CustomerPlacement.Height
        };
        var audit = new ContractSigningAuditLog
        {
            Session = session,
            Action = "SigningStarted",
            IpAddress = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(),
            UserAgent = _httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString(),
            EventDataJson = $"{{\"employeeId\":{employeeId},\"versionId\":{request.VersionId}}}",
            CreatedAt = now
        };

        _dbContext.ContractSigningSessions.Add(session);
        _dbContext.ContractSigningSigners.AddRange(director, customer);
        _dbContext.ContractSigningArtifacts.Add(new ContractSigningArtifact
        {
            Session = session,
            Kind = (byte)ElectronicSigningArtifactKind.OriginalPdf,
            TenantCode = originalPdf.TenantCode,
            StorageKey = originalPdf.StorageKey,
            FileName = originalPdf.OriginalFileName,
            ContentType = "application/pdf",
            Sha256 = originalPdf.Sha256,
            FileSize = originalPdf.FileSize,
            CreatedByEmployeeId = employeeId,
            CreatedAt = now
        });
        _dbContext.ContractSigningAuditLogs.Add(audit);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await _privateFileStorage.DeleteAsync(tenantCode, originalPdf.StorageKey, cancellationToken);
            throw;
        }

        return new ElectronicSigningSessionResponse(
            session.Id,
            session.ContractId,
            session.VersionId,
            ElectronicSigningStatus.AwaitingDirector,
            null,
            null);
    }

    public async Task<ElectronicSigningSessionResponse?> GetSessionStatusAsync(
        int contractId,
        int versionId,
        int employeeId,
        CancellationToken cancellationToken = default)
    {
        await _contractAuthorization.EnsureCanReadAsync(contractId, employeeId, cancellationToken);
        return await _dbContext.ContractSigningSessions
            .AsNoTracking()
            .Where(x => x.ContractId == contractId && x.VersionId == versionId)
            .Select(x => new ElectronicSigningSessionResponse(
                x.Id,
                x.ContractId,
                x.VersionId,
                (ElectronicSigningStatus)x.Status,
                null,
                null,
                x.RejectionReason))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<CustomerSigningPdfResponse> GetFinalSignedPdfAsync(
        int contractId,
        int versionId,
        int employeeId,
        CancellationToken cancellationToken = default)
    {
        await _contractAuthorization.EnsureCanReadAsync(contractId, employeeId, cancellationToken);
        var artifact = await _dbContext.ContractSigningArtifacts
            .Include(x => x.Session)
            .SingleOrDefaultAsync(x => x.Session.ContractId == contractId
                && x.Session.VersionId == versionId
                && x.Kind == (byte)ElectronicSigningArtifactKind.FinalSignedPdf,
                cancellationToken)
            ?? throw new KeyNotFoundException("Final signed PDF was not found.");
        var bytes = await ReadArtifactAsync(artifact, cancellationToken);
        return new CustomerSigningPdfResponse(artifact.FileName, bytes);
    }

    public async Task<DirectorSignElectronicContractResponse> SignAsDirectorAsync(
        long sessionId,
        DirectorSignElectronicContractRequest request,
        int employeeId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.Confirmed)
        {
            throw new InvalidOperationException("The Director must explicitly confirm each signature.");
        }

        var session = await _dbContext.ContractSigningSessions
            .Include(x => x.Signers)
            .Include(x => x.Artifacts)
            .SingleOrDefaultAsync(x => x.Id == sessionId, cancellationToken)
            ?? throw new KeyNotFoundException("Signing session was not found.");
        var state = ToWorkflowSession(session);
        await _contractAuthorization.EnsureCanManageSigningAsync(
            session.ContractId, employeeId, cancellationToken);
        if (state.Status is not ElectronicSigningStatus.AwaitingDirector)
        {
            throw new InvalidOperationException("The signing session is not awaiting the Director.");
        }

        var director = session.Signers.Single(x => x.Role == DirectorRole);
        var original = session.Artifacts.Single(x => x.Kind == (byte)ElectronicSigningArtifactKind.OriginalPdf);
        var originalBytes = await ReadArtifactAsync(original, cancellationToken);
        var signatureBytes = DecodeSignature(request.SignaturePngBase64);
        var placement = ReadPlacement(director);
        var signedPdf = _pdfService.StampSignature(originalBytes, signatureBytes, placement);
        var now = DateTime.UtcNow;
        var tenant = _currentTenant.GetRequiredTenant();
        StoredPrivateFile? signatureFile = null;
        StoredPrivateFile? signedPdfFile = null;
        try
        {
            signatureFile = await SavePrivateArtifactAsync(
                signatureBytes,
                $"signing-{session.Id}-director.png",
                "image/png",
                "ElectronicSigningSignature",
                session.ContractId,
                tenant.TenantCode,
                isSignature: true,
                cancellationToken);
            signedPdfFile = await SavePrivateArtifactAsync(
                signedPdf,
                $"contract-{session.ContractId}-version-{session.VersionId}-director-signed.pdf",
                "application/pdf",
                "ElectronicSigningDirectorPdf",
                session.ContractId,
                tenant.TenantCode,
                isSignature: false,
                cancellationToken);

            var token = _cryptography.CreateToken();
            var linkExpiresAt = now.AddHours(session.LinkLifetimeHours);
            var customer = session.Signers.Single(x => x.Role == CustomerRole);
            director.Status = 1;
            director.SignedAt = now;
            director.SignatureStorageKey = signatureFile.StorageKey;
            director.SignatureSha256 = signatureFile.Sha256;
            director.IpAddress = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
            director.UserAgent = _httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString();
            session.Status = (byte)ElectronicSigningStatus.CustomerOtpPending;
            _dbContext.ContractSigningLinks.Add(new ContractSigningLink
            {
                SessionId = session.Id,
                SignerId = customer.Id,
                TokenHash = _cryptography.HashSecret(token),
                ExpiresAt = linkExpiresAt,
                CreatedAt = now
            });
            _dbContext.ContractSigningArtifacts.Add(CreateArtifact(
                session, ElectronicSigningArtifactKind.DirectorSignatureImage, signatureFile,
                employeeId, now));
            _dbContext.ContractSigningArtifacts.Add(CreateArtifact(
                session, ElectronicSigningArtifactKind.DirectorSignedPdf, signedPdfFile,
                employeeId, now));
            _dbContext.ContractSigningAuditLogs.Add(CreateAudit(
                sessionId, director.Id, "DirectorSigned", now));
            await _dbContext.SaveChangesAsync(cancellationToken);

            return new DirectorSignElectronicContractResponse(
                session.Id,
                ElectronicSigningStatus.CustomerOtpPending,
                $"/public/electronic-signing/{Uri.EscapeDataString(tenant.TenantCode)}/{Uri.EscapeDataString(token)}",
                linkExpiresAt);
        }
        catch
        {
            if (signatureFile is not null)
            {
                await _privateFileStorage.DeleteAsync(tenant.TenantCode, signatureFile.StorageKey, cancellationToken);
            }

            if (signedPdfFile is not null)
            {
                await _privateFileStorage.DeleteAsync(tenant.TenantCode, signedPdfFile.StorageKey, cancellationToken);
            }

            throw;
        }
    }

    public async Task RejectAsDirectorAsync(
        long sessionId,
        RejectElectronicSigningRequest request,
        int employeeId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var session = await _dbContext.ContractSigningSessions
            .Include(x => x.Signers)
            .SingleOrDefaultAsync(x => x.Id == sessionId, cancellationToken)
            ?? throw new KeyNotFoundException("Signing session was not found.");
        await _contractAuthorization.EnsureCanManageSigningAsync(
            session.ContractId, employeeId, cancellationToken);
        var rejected = ElectronicSigningWorkflow.RejectByDirector(ToWorkflowSession(session), request.Reason);
        session.Status = (byte)rejected.Status;
        session.RejectionReason = rejected.RejectionReason;
        session.RejectedByEmployeeId = employeeId;
        session.RejectedAt = DateTime.UtcNow;
        session.Signers.Single(x => x.Role == DirectorRole).Status = 2;
        var contract = await _dbContext.TblContracts
            .SingleAsync(x => x.ContractId == session.ContractId, cancellationToken);
        var currentContractStatus = (ContractManagement.API.Common.Enums.ContractStatus)contract.Status;
        ContractLifecyclePolicy.EnsureCanTransition(
            currentContractStatus,
            ContractManagement.API.Common.Enums.ContractStatus.Negotiating);
        contract.Status = (byte)ContractManagement.API.Common.Enums.ContractStatus.Negotiating;
        _dbContext.ContractSigningAuditLogs.Add(CreateAudit(
            sessionId, session.Signers.Single(x => x.Role == DirectorRole).Id, "DirectorRejected", DateTime.UtcNow));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DirectorSigningQueueItem>> GetDirectorQueueAsync(
        CancellationToken cancellationToken = default)
    {
        var pending = await _dbContext.ContractSigningSessions
            .AsNoTracking()
            .Where(x => x.Status == (byte)ElectronicSigningStatus.AwaitingDirector)
            .Select(x => new
            {
                x.Id,
                x.ContractId,
                x.VersionId,
                x.CreatedAt,
                CustomerName = x.Signers
                    .Where(signer => signer.Role == CustomerRole)
                    .Select(signer => signer.Name)
                    .FirstOrDefault()
            })
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        if (pending.Count == 0) return [];

        var contractIds = pending.Select(x => x.ContractId).Distinct().ToArray();
        var contracts = await _dbContext.TblContracts.AsNoTracking()
            .Where(x => contractIds.Contains(x.ContractId))
            .Select(x => new { x.ContractId, x.ContractCode, x.ContractName })
            .ToDictionaryAsync(x => x.ContractId, cancellationToken);

        return pending
            .Where(x => contracts.ContainsKey(x.ContractId) && x.CustomerName is not null)
            .Select(x => new DirectorSigningQueueItem(
                x.Id,
                x.ContractId,
                x.VersionId,
                contracts[x.ContractId].ContractCode,
                contracts[x.ContractId].ContractName,
                x.CustomerName!,
                x.CreatedAt))
            .ToArray();
    }

    public async Task<CustomerSigningPdfResponse> GetDirectorPdfAsync(
        long sessionId,
        int employeeId,
        CancellationToken cancellationToken = default)
    {
        var session = await _dbContext.ContractSigningSessions
            .AsNoTracking()
            .Include(x => x.Artifacts)
            .SingleOrDefaultAsync(x => x.Id == sessionId, cancellationToken)
            ?? throw new KeyNotFoundException("Signing session was not found.");
        await _contractAuthorization.EnsureCanManageSigningAsync(
            session.ContractId, employeeId, cancellationToken);

        var sourcePdf = session.Artifacts.SingleOrDefault(
            x => x.Kind == (byte)ElectronicSigningArtifactKind.OriginalPdf)
            ?? throw new InvalidDataException("Original PDF artifact is missing.");
        var bytes = await ReadArtifactAsync(sourcePdf, cancellationToken);
        return new CustomerSigningPdfResponse(sourcePdf.FileName, bytes);
    }

    private async Task<byte[]> ReadArtifactAsync(
        ContractSigningArtifact artifact,
        CancellationToken cancellationToken)
    {
        var currentTenant = _currentTenant.GetRequiredTenant();
        if (!string.Equals(artifact.TenantCode, currentTenant.TenantCode, StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(artifact.StorageKey))
        {
            throw new UnauthorizedAccessException("The artifact does not belong to the current tenant.");
        }

        await using var stream = await _privateFileStorage.OpenReadAsync(
            artifact.TenantCode, artifact.StorageKey, cancellationToken);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();
        var actualHash = SHA256.HashData(bytes);
        byte[] expectedHash;
        try
        {
            expectedHash = Convert.FromHexString(artifact.Sha256);
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException("Stored artifact hash metadata is invalid.", exception);
        }

        if (!CryptographicOperations.FixedTimeEquals(actualHash, expectedHash))
        {
            throw new InvalidDataException("Stored signing artifact integrity check failed.");
        }

        return bytes;
    }

    private async Task<StoredPrivateFile> SavePrivateArtifactAsync(
        byte[] bytes,
        string fileName,
        string contentType,
        string objectType,
        int contractId,
        string tenantCode,
        bool isSignature,
        CancellationToken cancellationToken)
    {
        await using var stream = new MemoryStream(bytes, writable: false);
        var policy = isSignature
            ? PrivateFileUploadPolicies.ProfileImage(1_000_000)
            : PrivateFileUploadPolicies.SubmittedContractPdf();
        return await _privateFileStorage.SaveAsync(
            new PrivateFileSaveRequest(
                stream, fileName, contentType, bytes.Length, tenantCode, objectType, contractId, policy),
            cancellationToken);
    }

    private static ContractSigningArtifact CreateArtifact(
        ContractSigningSession session,
        ElectronicSigningArtifactKind kind,
        StoredPrivateFile stored,
        long? employeeId,
        DateTime createdAt) => new()
    {
        Session = session,
        Kind = (byte)kind,
        TenantCode = stored.TenantCode,
        StorageKey = stored.StorageKey,
        FileName = stored.OriginalFileName,
        ContentType = stored.ContentType,
        Sha256 = stored.Sha256,
        FileSize = stored.FileSize,
        CreatedByEmployeeId = employeeId,
        CreatedAt = createdAt
    };

    private static byte[] DecodeSignature(string base64)
    {
        if (string.IsNullOrWhiteSpace(base64) || base64.Length > 1_400_000)
        {
            throw new ArgumentException("Signature image must be a PNG of at most 1 MB.", nameof(base64));
        }

        try
        {
            var bytes = Convert.FromBase64String(base64);
            if (bytes.Length > 1_000_000)
            {
                throw new ArgumentException("Signature image must be a PNG of at most 1 MB.", nameof(base64));
            }

            return bytes;
        }
        catch (FormatException exception)
        {
            throw new ArgumentException("Signature image must be base64-encoded PNG data.", nameof(base64), exception);
        }
    }

    private static PdfSignaturePlacement ReadPlacement(ContractSigningSigner signer)
    {
        if (signer.PageNumber is not { } page
            || signer.PositionX is not { } x
            || signer.PositionY is not { } y
            || signer.Width is not { } width
            || signer.Height is not { } height)
        {
            throw new InvalidOperationException("Signature placement is not configured for this signer.");
        }

        return new PdfSignaturePlacement(page, x, y, width, height);
    }

    private static ElectronicSigningSession ToWorkflowSession(ContractSigningSession session) => new(
        session.ContractId,
        session.VersionId,
        (ElectronicSigningStatus)session.Status,
        session.IsLocked,
        session.LockedAt,
        session.CompletedAt);

    private ContractSigningAuditLog CreateAudit(long sessionId, long? signerId, string action, DateTime now) => new()
    {
        SessionId = sessionId,
        SignerId = signerId,
        Action = action,
        IpAddress = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(),
        UserAgent = _httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString(),
        CreatedAt = now
    };

    private static void Validate(StartElectronicSigningRequest request)
    {
        if (request.ContractId <= 0 || request.VersionId <= 0)
        {
            throw new ArgumentException("ContractId và VersionId phải lớn hơn 0.");
        }

        ValidatePerson(request.DirectorName, request.DirectorPhone, "Giám đốc");
        ValidatePerson(request.CustomerName, request.CustomerPhone, "Khách hàng");
        if (request.LinkLifetimeHours is < 1 or > 168)
        {
            throw new ArgumentException("Thời hạn link phải từ 1 đến 168 giờ.");
        }
    }

    private static void ValidatePlacement(PdfSignaturePlacement placement, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(placement);
        if (placement.PageNumber <= 0
            || placement.X < 0
            || placement.Y < 0
            || placement.Width <= 0
            || placement.Height <= 0
            || placement.Width > 500
            || placement.Height > 300)
        {
            throw new ArgumentException("The configured signature area is invalid.", parameterName);
        }
    }

    private static void ValidatePerson(string name, string phone, string role)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 300)
        {
            throw new ArgumentException($"Tên {role} không hợp lệ.");
        }

        if (string.IsNullOrWhiteSpace(phone) || phone.Trim().Length > 30)
        {
            throw new ArgumentException($"Số điện thoại {role} không hợp lệ.");
        }
    }
}
