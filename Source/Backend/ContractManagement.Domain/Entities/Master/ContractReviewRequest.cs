namespace ContractManagement.Domain.Entities.Master;

public partial class ContractReviewRequest
{
    public long ContractReviewRequestId { get; set; }
    public long ContractDocumentId { get; set; }
    public long ContractVersionId { get; set; }
    public string ContractReviewRequestDocumentHash { get; set; } = null!;
    public long? PreviousReviewRequestId { get; set; }
    public long CustomerId { get; set; }
    public string? ContractReviewRequestMessage { get; set; }
    public long SentByEmployeeId { get; set; }
    public DateTime SentAt { get; set; }
    public string ContractReviewRequestStatus { get; set; } = null!;
    public DateTime? FirstViewedAt { get; set; }
    public string? ContractReviewRequestCustomerNote { get; set; }
    public long? RespondedByCustomerId { get; set; }
    public DateTime? RespondedAt { get; set; }
    public string? RespondedIpAddress { get; set; }
    public string? RespondedUserAgent { get; set; }
    public string? ContractReviewRequestFollowupDecision { get; set; }
    public string? ContractReviewRequestFollowupNote { get; set; }
    public long? FollowupDecidedByEmployeeId { get; set; }
    public DateTime? FollowupDecidedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public byte[] Rowversion { get; set; } = null!;
}
