using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ContractManagement.Domain.Internal;

public static class ContractDocumentStatus
{
    public const int Draft = 0, Negotiating = 1, PendingApproval = 2, PendingSignature = 3, Signed = 4, Completed = 5, Cancelled = 6, Rejected = 7;
}

public static class ContractReviewStatus
{
    public const string Sent = "SENT", Viewed = "VIEWED", Approved = "APPROVED", ChangesRequested = "CHANGES_REQUESTED",
        Rejected = "REJECTED", Revoked = "REVOKED", Superseded = "SUPERSEDED";
    public const string Negotiate = "NEGOTIATE", Close = "CLOSE";
    public static bool IsOpen(string? status) => status is Sent or Viewed;
}

/// <summary>Transition table 5.3 of CONTRACT-REVIEW-FLOW. Each check returns null when allowed, otherwise a Vietnamese reason.</summary>
public static class ContractReviewPolicy
{
    public static bool CanEdit(int documentStatus, bool versionLocked) =>
        documentStatus is ContractDocumentStatus.Draft or ContractDocumentStatus.Negotiating && !versionLocked;

    public static string? CanSend(int documentStatus, string? latestReviewStatus, bool versionLocked, int missingCount)
    {
        if (documentStatus == ContractDocumentStatus.Negotiating && ContractReviewStatus.IsOpen(latestReviewStatus)) return "Hợp đồng đang chờ khách phản hồi.";
        if (documentStatus is not (ContractDocumentStatus.Draft or ContractDocumentStatus.Negotiating)) return "Trạng thái hồ sơ không cho phép gửi khách.";
        if (versionLocked) return "Phiên bản hiện tại đã gửi khách. Tạo phiên bản mới để sửa và gửi lại.";
        if (missingCount > 0) return "Còn thiếu thông tin bắt buộc.";
        return null;
    }

    public static string? CanRespond(string reviewStatus) =>
        ContractReviewStatus.IsOpen(reviewStatus) ? null : "Lần gửi này đã được phản hồi hoặc không còn hiệu lực.";

    /// <returns>(review status, document status) after the customer's decision.</returns>
    public static (string Review, int Document) Respond(string reviewStatus, string decision, string? note)
    {
        if (CanRespond(reviewStatus) is { } error) throw new InvalidOperationException(error);
        var result = decision switch
        {
            "APPROVE" => (ContractReviewStatus.Approved, ContractDocumentStatus.PendingApproval),
            "REQUEST_CHANGES" => (ContractReviewStatus.ChangesRequested, ContractDocumentStatus.Negotiating),
            "REJECT" => (ContractReviewStatus.Rejected, ContractDocumentStatus.Rejected),
            _ => throw new ArgumentException("Quyết định không hợp lệ.")
        };
        if (decision != "APPROVE" && string.IsNullOrWhiteSpace(note)) throw new ArgumentException("Vui lòng nhập nội dung yêu cầu sửa hoặc lý do từ chối.");
        return result;
    }

    public static string? CanCreateVersion(int documentStatus, string? latestReviewStatus, string? followupDecision, bool currentVersionLocked)
    {
        if ((documentStatus is ContractDocumentStatus.Draft or ContractDocumentStatus.Negotiating) && !currentVersionLocked)
            return ContractReviewStatus.IsOpen(latestReviewStatus)
                ? "Hợp đồng đang chờ khách phản hồi; hãy chờ khách phản hồi trước khi tạo phiên bản mới."
                : null;
        if (documentStatus != ContractDocumentStatus.Negotiating || !currentVersionLocked) return "Hồ sơ không ở trạng thái cần tạo phiên bản mới.";
        return latestReviewStatus == ContractReviewStatus.ChangesRequested
            || (latestReviewStatus == ContractReviewStatus.Rejected && followupDecision == ContractReviewStatus.Negotiate)
            ? null : "Chỉ tạo phiên bản mới khi khách yêu cầu sửa hoặc đã chọn thương lượng tiếp.";
    }

    /// <returns>New document status for NEGOTIATE / CLOSE.</returns>
    public static int Followup(string reviewStatus, string? existingDecision, string decision)
    {
        if (reviewStatus != ContractReviewStatus.Rejected || existingDecision is not null) throw new InvalidOperationException("Lần gửi không ở trạng thái chờ quyết định sau khi từ chối.");
        return decision switch
        {
            ContractReviewStatus.Negotiate => ContractDocumentStatus.Negotiating,
            ContractReviewStatus.Close => ContractDocumentStatus.Cancelled,
            _ => throw new ArgumentException("Quyết định không hợp lệ.")
        };
    }
}

public static class SnapshotHash
{
    public static string Compute(string draftJson) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(draftJson))).ToLowerInvariant();
}

public static class PhoneNumber
{
    /// <returns>Vietnamese phone in 0xxxxxxxxx form, or null when invalid.</returns>
    public static string? Normalize(string? input)
    {
        var phone = Regex.Replace(input ?? "", @"[\s().-]", "");
        if (phone.StartsWith("+84")) phone = "0" + phone[3..];
        return Regex.IsMatch(phone, @"^0\d{9,10}$") ? phone : null;
    }

    public static string Mask(string phone) => phone.Length <= 6 ? new string('*', phone.Length) : phone[..3] + new string('*', phone.Length - 6) + phone[^3..];
}

public static class TemporaryPassword
{
    private const string Letters = "abcdefghijkmnopqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ", Digits = "23456789";

    public static string Generate()
    {
        const string all = Letters + Digits;
        while (true)
        {
            var value = string.Create(10, 0, (span, _) => { for (var i = 0; i < span.Length; i++) span[i] = all[RandomNumberGenerator.GetInt32(all.Length)]; });
            if (value.Any(char.IsLetter) && value.Any(char.IsDigit)) return value;
        }
    }
}

public static class CustomerPasswordRules
{
    public const string Message = "Mật khẩu mới cần tối thiểu 8 ký tự, có cả chữ và số.";
    public static bool IsValid(string? password) => password is { Length: >= 8 and <= 100 } && password.Any(char.IsLetter) && password.Any(char.IsDigit);
}
