namespace ContractManagement.Application.Features.Internal;

public sealed record CustomerSigningOtpResult(string ChallengeId, DateTime ExpiresAt, string DeliveryChannel, string? DevelopmentOtp);
public sealed record CustomerSigningOtpVerification(string ChallengeId, string Otp);
public sealed record CustomerSigningRequest(string ChallengeId, string SignaturePngBase64, bool Confirmed);
public sealed record CustomerPortalSigningState(string Phase, DateTime? CompanySignedAt, DateTime? CustomerSignedAt);
public sealed record SignedContractEntry(long DocumentId, long VersionId, string Code, string Name, string CustomerName,
    string EmployeeName, long VersionNo, DateTime? SignedAt);
public sealed record SignedContractPage(List<SignedContractEntry> Items, bool HasMore);

public interface ICustomerSigningService
{
    Task<SignedContractPage> SignedContracts(InternalUser user, string? search, int page, CancellationToken ct);
    Task<byte[]> InternalFinalDocument(InternalUser user, long documentId, long versionId, CancellationToken ct);
    Task<CustomerPortalSigningState> PortalState(long customerId, long reviewId, CancellationToken ct);
    Task<CustomerSigningOtpResult> RequestPortalOtp(long customerId, long reviewId, CancellationToken ct);
    Task VerifyPortalOtp(long customerId, long reviewId, CustomerSigningOtpVerification request, CancellationToken ct);
    Task<byte[]> PortalDocument(long customerId, long reviewId, string challengeId, CancellationToken ct);
    Task<byte[]> PortalSign(long customerId, long reviewId, CustomerSigningRequest request, string? ipAddress, string? userAgent, CancellationToken ct);
    Task<byte[]> PortalFinalDocument(long customerId, long reviewId, CancellationToken ct);
}
