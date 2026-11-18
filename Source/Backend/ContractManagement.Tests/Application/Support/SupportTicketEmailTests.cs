using ContractManagement.API.Domains.Support;
using Xunit;

namespace ContractManagement.Tests.Application.Support;

public class SupportTicketEmailTests
{
    private static SupportTicketDto CreateSampleTicket()
    {
        return new SupportTicketDto
        {
            TicketSupportId = 100,
            TicketCode = "TK-20261118-001",
            TicketSubject = "Lỗi xuất chữ ký số khi ký phụ lục",
            TicketDescription = "Không thể tạo mã OTP chữ ký số qua email.",
            TicketPriority = "High",
            Department = "Phòng Kinh Doanh",
            TenantName = "Công ty Cổ phần Công nghệ ABC",
            TenantCode = "0912345001",
            CreatedByName = "Nguyễn Văn Khách",
            CreatedAt = DateTime.UtcNow
        };
    }

    [Fact]
    public void Case1_NewTicketEmail_BuildsExpectedSubjectAndRecipients()
    {
        var ticket = CreateSampleTicket();
        var email = SupportTicketEmailTemplate.BuildNewTicketEmail(
            recipient: "vulongnhatabc@gmail.com",
            ticket: ticket,
            senderDisplayName: "Nguyễn Văn Khách",
            attachmentSummary: "anh-loi-ky-so.png",
            systemAdminTicketUrl: "http://localhost:3001/workspace/support-tickets/100");

        Assert.Equal("vulongnhatabc@gmail.com", email.Recipient);
        Assert.Contains(ticket.TicketCode, email.Subject);
        Assert.Contains("Cao", email.Subject);
        Assert.Contains(ticket.TicketSubject, email.TextBody);
        Assert.Contains("http://localhost:3001/workspace/support-tickets/100", email.HtmlBody);
        Assert.Contains("anh-loi-ky-so.png", email.TextBody);
    }

    [Fact]
    public void Case2_StaffAssignmentEmail_BuildsExpectedSubjectAndDetails()
    {
        var ticket = CreateSampleTicket();
        var email = SupportTicketEmailTemplate.BuildStaffAssignmentEmail(
            recipient: "hungnguyennn119@gmail.com",
            ticket: ticket,
            staffDisplayName: "Trần Kỹ Thuật",
            headName: "Vũ Long Nhật",
            note: "Kiểm tra lại service OTP và báo lại cho tôi trước 15h.",
            systemAdminTicketUrl: "http://localhost:3001/workspace/support-tickets/100");

        Assert.Equal("hungnguyennn119@gmail.com", email.Recipient);
        Assert.Contains(ticket.TicketCode, email.Subject);
        Assert.Contains("phân công", email.Subject);
        Assert.Contains("Vũ Long Nhật", email.HtmlBody);
        Assert.Contains("Kiểm tra lại service OTP", email.TextBody);
        Assert.Contains("http://localhost:3001/workspace/support-tickets/100", email.HtmlBody);
    }

    [Fact]
    public void Case3_ResolutionNoticeEmail_BuildsExpectedNoticeForCustomer()
    {
        var ticket = CreateSampleTicket();
        var email = SupportTicketEmailTemplate.BuildResolutionNoticeEmail(
            recipient: "contact@abc-corp.vn",
            ticket: ticket,
            resolutionNotice: "Sự cố dịch vụ OTP chữ ký số đã được khắc phục hoàn tất.",
            senderName: "Vũ Long Nhật",
            customerPortalTicketUrl: "http://localhost:3000/portal/support/100",
            isClosed: true);

        Assert.Equal("contact@abc-corp.vn", email.Recipient);
        Assert.Contains(ticket.TicketCode, email.Subject);
        Assert.Contains("đã có kết quả", email.Subject);
        Assert.Contains("Vũ Long Nhật", email.HtmlBody);
        Assert.Contains("Sự cố dịch vụ OTP", email.TextBody);
        Assert.Contains("http://localhost:3000/portal/support/100", email.HtmlBody);
    }
}
