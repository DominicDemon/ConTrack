namespace ContractManagement.Application.Features.Contracts.Signing;

public sealed class StartElectronicSigningRequest
{
    public int ContractId { get; init; }
    public int VersionId { get; init; }
    public string DirectorName { get; init; } = string.Empty;
    public string DirectorPhone { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public string CustomerPhone { get; init; } = string.Empty;
    public int LinkLifetimeHours { get; init; } = 72;
    public PdfSignaturePlacement DirectorPlacement { get; init; } = new(1, 0, 0, 160, 55);
    public PdfSignaturePlacement CustomerPlacement { get; init; } = new(1, 0, 0, 160, 55);
}

public sealed record ElectronicSigningSessionResponse(
    long SessionId,
    int ContractId,
    int VersionId,
    ElectronicSigningStatus Status,
    string? CustomerSigningLink,
    DateTime? LinkExpiresAt,
    string? RejectionReason = null);

public sealed record RequestElectronicSigningOtpRequest(ElectronicSignerRole Role);

public sealed record RequestElectronicSigningOtpResponse(
    string ChallengeId,
    DateTime ExpiresAt,
    string DeliveryChannel);

public sealed record VerifyElectronicSigningOtpRequest(
    ElectronicSignerRole Role,
    string ChallengeId,
    string Otp);

public sealed record DirectorSignElectronicContractRequest(
    string SignaturePngBase64,
    bool Confirmed);

public sealed record RejectElectronicSigningRequest(string Reason);

public sealed record DirectorSignElectronicContractResponse(
    long SessionId,
    ElectronicSigningStatus Status,
    string CustomerSigningLink,
    DateTime LinkExpiresAt);

public sealed record CustomerSignElectronicContractRequest(
    string ChallengeId,
    string SignaturePngBase64,
    bool Confirmed);

public sealed record CustomerSigningPdfResponse(
    string FileName,
    byte[] Content);

public sealed record DirectorSigningQueueItem(
    long SessionId,
    int ContractId,
    int VersionId,
    string? ContractCode,
    string ContractName,
    string CustomerName,
    DateTime SubmittedAt);

public interface IGsmModemSmsProvider
{
    Task SendOtpAsync(
        string phoneNumber,
        string otp,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken = default);
}

public sealed class GsmSmsDeliveryUncertainException(string message, Exception innerException)
    : IOException(message, innerException);

public interface IElectronicSigningService
{
    Task<ElectronicSigningSessionResponse> StartAsync(
        StartElectronicSigningRequest request,
        int employeeId,
        CancellationToken cancellationToken = default);

    Task<ElectronicSigningSessionResponse?> GetSessionStatusAsync(
        int contractId,
        int versionId,
        int employeeId,
        CancellationToken cancellationToken = default);

    Task<CustomerSigningPdfResponse> GetFinalSignedPdfAsync(
        int contractId,
        int versionId,
        int employeeId,
        CancellationToken cancellationToken = default);

    Task<RequestElectronicSigningOtpResponse> RequestOtpAsync(
        long sessionId,
        RequestElectronicSigningOtpRequest request,
        CancellationToken cancellationToken = default);

    Task VerifyOtpAsync(
        long sessionId,
        VerifyElectronicSigningOtpRequest request,
        CancellationToken cancellationToken = default);

    Task<DirectorSignElectronicContractResponse> SignAsDirectorAsync(
        long sessionId,
        DirectorSignElectronicContractRequest request,
        int employeeId,
        CancellationToken cancellationToken = default);

    Task RejectAsDirectorAsync(
        long sessionId,
        RejectElectronicSigningRequest request,
        int employeeId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DirectorSigningQueueItem>> GetDirectorQueueAsync(
        CancellationToken cancellationToken = default);

    Task<CustomerSigningPdfResponse> GetDirectorPdfAsync(
        long sessionId,
        int employeeId,
        CancellationToken cancellationToken = default);

    Task<RequestElectronicSigningOtpResponse> RequestCustomerOtpAsync(
        string linkToken,
        CancellationToken cancellationToken = default);

    Task VerifyCustomerOtpAsync(
        string linkToken,
        VerifyElectronicSigningOtpRequest request,
        CancellationToken cancellationToken = default);

    Task<CustomerSigningPdfResponse> GetCustomerSigningPdfAsync(
        string linkToken,
        string challengeId,
        CancellationToken cancellationToken = default);

    Task<CustomerSigningPdfResponse> SignAsCustomerAsync(
        string linkToken,
        CustomerSignElectronicContractRequest request,
        CancellationToken cancellationToken = default);
}
