using System.Globalization;
using System.Net;
using ContractManagement.API.Domains.CustomerAccess;
using ContractManagement.API.Domains.Email;
using ContractManagement.Infrastructure.Branding;

namespace ContractManagement.API.Domains.Support;

public static class SupportTicketEmailTemplate
{
    public const string LogoContentId = CustomerOtpEmailTemplate.LogoContentId;

    public static EmailMessage BuildNewTicketEmail(
        string recipient,
        SupportTicketDto ticket,
        string senderDisplayName,
        string? attachmentSummary,
        string systemAdminTicketUrl)
    {
        var priorityLabel = GetPriorityLabel(ticket.TicketPriority);
        var subject = $"[{ApplicationBranding.Name} Support] Yêu cầu hỗ trợ mới #{ticket.TicketCode} ({priorityLabel}) - {ticket.TenantName}";
        var (vnTime, utcTime) = FormatTimestamps(ticket.CreatedAt);

        var textBody = $"""
            THÔNG BÁO YÊU CẦU HỖ TRỢ KHÁCH HÀNG MỚI
            Hệ thống Quản lý & Số hóa Hợp đồng Điện tử {ApplicationBranding.Name}

            Xin chào Người xử lý hỗ trợ,

            Hệ thống vừa tiếp nhận một yêu cầu hỗ trợ mới từ khách hàng/tổ chức Tenant:

            - Mã Ticket: {ticket.TicketCode}
            - Tổ chức / Khách hàng: {ticket.TenantName} ({ticket.TenantCode})
            - Người gửi: {senderDisplayName}
            - Phòng ban tiếp nhận: {ticket.Department}
            - Phân hệ: {ticket.Module ?? "Chung"}
            - Mức độ ưu tiên: {priorityLabel}
            - Thời gian gửi: {vnTime} GMT+7 ({utcTime} UTC)
            - Tiêu đề: {ticket.TicketSubject}
            {(string.IsNullOrWhiteSpace(attachmentSummary) ? "" : $"- Tệp đính kèm: {attachmentSummary}\n")}
            Nội dung chi tiết:
            {ticket.TicketDescription}

            Vui lòng truy cập trang Quản trị Hỗ trợ & Ticket để tiếp nhận và phản hồi khách hàng:
            {systemAdminTicketUrl}

            Trân trọng,
            Hệ thống Thông báo Tự động {ApplicationBranding.Name}
            """;

        var htmlBody = BuildHtmlLayout(
            subject: subject,
            bannerTitle: "YÊU CẦU HỖ TRỢ KHÁCH HÀNG MỚI",
            bannerSubtitle: $"Mã Ticket: #{ticket.TicketCode} • Mức độ ưu tiên: {priorityLabel}",
            ticket: ticket,
            senderDisplayName: senderDisplayName,
            messageTitle: "Nội dung mô tả sự cố / yêu cầu hỗ trợ",
            messageContent: ticket.TicketDescription,
            attachmentSummary: attachmentSummary,
            vnTime: vnTime,
            utcTime: utcTime,
            systemAdminTicketUrl: systemAdminTicketUrl);

        return new EmailMessage(recipient, subject, textBody, htmlBody, LogoContentId);
    }

    public static EmailMessage BuildCustomerReplyEmail(
        string recipient,
        SupportTicketDto ticket,
        SupportTicketMessageDto message,
        string systemAdminTicketUrl)
    {
        var priorityLabel = GetPriorityLabel(ticket.TicketPriority);
        var subject = $"[{ApplicationBranding.Name} Support] Phản hồi mới từ khách hàng #{ticket.TicketCode} - {ticket.TenantName}";
        var (vnTime, utcTime) = FormatTimestamps(message.CreatedAt);

        var textBody = $"""
            THÔNG BÁO PHẢN HỒI MỚI TỪ KHÁCH HÀNG
            Hệ thống Quản lý & Số hóa Hợp đồng Điện tử {ApplicationBranding.Name}

            Xin chào Người xử lý hỗ trợ,

            Khách hàng vừa gửi thêm phản hồi mới trong Ticket hỗ trợ #{ticket.TicketCode}:

            - Mã Ticket: {ticket.TicketCode}
            - Tiêu đề Ticket: {ticket.TicketSubject}
            - Tổ chức / Khách hàng: {ticket.TenantName} ({ticket.TenantCode})
            - Người phản hồi: {message.SenderName}
            - Phòng ban tiếp nhận: {ticket.Department}
            - Mức độ ưu tiên: {priorityLabel}
            - Thời gian gửi: {vnTime} GMT+7 ({utcTime} UTC)

            Nội dung phản hồi mới:
            {message.TicketMessageContent}

            Vui lòng truy cập trang Quản trị Hỗ trợ & Ticket để xem và phản hồi:
            {systemAdminTicketUrl}

            Trân trọng,
            Hệ thống Thông báo Tự động {ApplicationBranding.Name}
            """;

        var htmlBody = BuildHtmlLayout(
            subject: subject,
            bannerTitle: "PHẢN HỒI MỚI TỪ KHÁCH HÀNG",
            bannerSubtitle: $"Mã Ticket: #{ticket.TicketCode} • {ticket.TenantName}",
            ticket: ticket,
            senderDisplayName: message.SenderName,
            messageTitle: "Nội dung phản hồi mới từ khách hàng",
            messageContent: message.TicketMessageContent,
            attachmentSummary: message.TicketMessageAttachmentName,
            vnTime: vnTime,
            utcTime: utcTime,
            systemAdminTicketUrl: systemAdminTicketUrl);

        return new EmailMessage(recipient, subject, textBody, htmlBody, LogoContentId);
    }

    private static string BuildHtmlLayout(
        string subject,
        string bannerTitle,
        string bannerSubtitle,
        SupportTicketDto ticket,
        string senderDisplayName,
        string messageTitle,
        string messageContent,
        string? attachmentSummary,
        string vnTime,
        string utcTime,
        string systemAdminTicketUrl)
    {
        var year = DateTime.UtcNow.Year;
        var priorityLabel = GetPriorityLabel(ticket.TicketPriority);
        var (priorityBg, priorityColor, priorityBorder) = GetPriorityColors(ticket.TicketPriority);

        var safeSubject = WebUtility.HtmlEncode(subject);
        var safeBannerTitle = WebUtility.HtmlEncode(bannerTitle);
        var safeBannerSubtitle = WebUtility.HtmlEncode(bannerSubtitle);
        var safeTicketCode = WebUtility.HtmlEncode(ticket.TicketCode);
        var safeTicketSubject = WebUtility.HtmlEncode(ticket.TicketSubject);
        var safeTenantName = WebUtility.HtmlEncode(ticket.TenantName);
        var safeTenantCode = WebUtility.HtmlEncode(ticket.TenantCode);
        var safeSender = WebUtility.HtmlEncode(senderDisplayName);
        var safeDepartment = WebUtility.HtmlEncode(ticket.Department);
        var safeModule = WebUtility.HtmlEncode(ticket.Module ?? "Hỗ trợ chung");
        var safeMessageTitle = WebUtility.HtmlEncode(messageTitle);
        var safeMessageContent = WebUtility.HtmlEncode(messageContent).Replace("\n", "<br>");
        var safeAttachment = string.IsNullOrWhiteSpace(attachmentSummary)
            ? null
            : WebUtility.HtmlEncode(attachmentSummary);
        var safeUrl = WebUtility.HtmlEncode(systemAdminTicketUrl);

        var attachmentHtml = safeAttachment is null
            ? string.Empty
            : $$"""
              <tr>
                <td style="padding: 8px 0; font-size: 13px; color: #64748b; width: 150px;">Tệp đính kèm:</td>
                <td style="padding: 8px 0; font-size: 13px; color: #2563eb; font-weight: 600;">📎 {{safeAttachment}}</td>
              </tr>
              """;

        return $$"""
            <!DOCTYPE html>
            <html lang="vi">
            <head>
              <meta charset="UTF-8">
              <meta name="viewport" content="width=device-width, initial-scale=1.0">
              <title>{{safeSubject}}</title>
            </head>
            <body style="margin: 0; padding: 0; background-color: #f1f5f9; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;">
              <table role="presentation" border="0" cellpadding="0" cellspacing="0" width="100%" style="background-color: #f1f5f9; padding: 32px 12px;">
                <tr>
                  <td align="center">
                    <table role="presentation" border="0" cellpadding="0" cellspacing="0" width="100%" style="max-width: 600px; background-color: #ffffff; border-radius: 16px; overflow: hidden; box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.05); border: 1px solid #e2e8f0;">
                      
                      <!-- Header Banner -->
                      <tr>
                        <td style="background: linear-gradient(135deg, #0f172a 0%, #1e3a8a 100%); padding: 32px 28px; text-align: center;">
                          <img src="cid:{{LogoContentId}}" width="64" height="64" alt="{{ApplicationBranding.HtmlName}}" style="display: inline-block; width: 64px; height: 64px; object-fit: contain; margin-bottom: 12px; border: 0;">
                          <h1 style="margin: 0; font-size: 19px; font-weight: 700; color: #ffffff; letter-spacing: 0.5px; text-transform: uppercase;">
                            {{safeBannerTitle}}
                          </h1>
                          <p style="margin: 8px 0 0 0; font-size: 13px; color: #93c5fd; font-weight: 500;">
                            {{safeBannerSubtitle}}
                          </p>
                        </td>
                      </tr>

                      <!-- Main Content -->
                      <tr>
                        <td style="padding: 32px 28px 24px 28px;">
                          <p style="margin: 0 0 16px 0; font-size: 14px; line-height: 1.6; color: #334155;">
                            Xin chào <strong>Người xử lý hỗ trợ ({{ApplicationBranding.HtmlName}} Admin)</strong>,
                          </p>
                          <p style="margin: 0 0 20px 0; font-size: 14px; line-height: 1.6; color: #475569;">
                            Hệ thống vừa ghi nhận thông tin yêu cầu hỗ trợ từ khách hàng cần bạn kiểm tra và phản hồi:
                          </p>

                          <!-- Ticket Metadata Box -->
                          <div style="background-color: #f8fafc; border: 1px solid #e2e8f0; border-radius: 12px; padding: 18px 20px; margin-bottom: 20px;">
                            <div style="font-size: 15px; font-weight: 700; color: #0f172a; margin-bottom: 12px; padding-bottom: 10px; border-bottom: 1px solid #e2e8f0;">
                              {{safeTicketSubject}}
                            </div>
                            <table role="presentation" border="0" cellpadding="0" cellspacing="0" width="100%">
                              <tr>
                                <td style="padding: 6px 0; font-size: 13px; color: #64748b; width: 150px;">Mã Ticket:</td>
                                <td style="padding: 6px 0; font-size: 13px; color: #1d4ed8; font-family: monospace; font-weight: 700;">{{safeTicketCode}}</td>
                              </tr>
                              <tr>
                                <td style="padding: 6px 0; font-size: 13px; color: #64748b;">Tổ chức / Tenant:</td>
                                <td style="padding: 6px 0; font-size: 13px; color: #0f172a; font-weight: 600;">{{safeTenantName}} <span style="font-family: monospace; color: #64748b;">({{safeTenantCode}})</span></td>
                              </tr>
                              <tr>
                                <td style="padding: 6px 0; font-size: 13px; color: #64748b;">Người gửi:</td>
                                <td style="padding: 6px 0; font-size: 13px; color: #0f172a;">{{safeSender}}</td>
                              </tr>
                              <tr>
                                <td style="padding: 6px 0; font-size: 13px; color: #64748b;">Phòng ban tiếp nhận:</td>
                                <td style="padding: 6px 0; font-size: 13px; color: #0f172a; font-weight: 600;">{{safeDepartment}}</td>
                              </tr>
                              <tr>
                                <td style="padding: 6px 0; font-size: 13px; color: #64748b;">Phân hệ phần mềm:</td>
                                <td style="padding: 6px 0; font-size: 13px; color: #0f172a;">{{safeModule}}</td>
                              </tr>
                              <tr>
                                <td style="padding: 6px 0; font-size: 13px; color: #64748b;">Mức độ ưu tiên:</td>
                                <td style="padding: 6px 0; font-size: 13px;">
                                  <span style="display: inline-block; padding: 3px 10px; border-radius: 999px; font-size: 12px; font-weight: 700; background-color: {{priorityBg}}; color: {{priorityColor}}; border: 1px solid {{priorityBorder}};">
                                    {{priorityLabel}}
                                  </span>
                                </td>
                              </tr>
                              <tr>
                                <td style="padding: 6px 0; font-size: 13px; color: #64748b;">Thời gian ghi nhận:</td>
                                <td style="padding: 6px 0; font-size: 13px; color: #0f172a;">{{vnTime}} (GMT+7) <span style="color: #94a3b8; font-size: 11px;">/ {{utcTime}} UTC</span></td>
                              </tr>
                              {{attachmentHtml}}
                            </table>
                          </div>

                          <!-- Message Content Box -->
                          <div style="background-color: #fffbeb; border-left: 4px solid #f59e0b; border-radius: 8px; padding: 16px 18px; margin-bottom: 24px;">
                            <div style="font-size: 12px; font-weight: 700; color: #92400e; text-transform: uppercase; letter-spacing: 0.5px; margin-bottom: 8px;">
                              {{safeMessageTitle}}:
                            </div>
                            <div style="font-size: 14px; color: #1e293b; line-height: 1.6;">
                              {{safeMessageContent}}
                            </div>
                          </div>

                          <!-- CTA Button -->
                          <div style="text-align: center; margin: 28px 0 18px 0;">
                            <a href="{{safeUrl}}" target="_blank" style="display: inline-block; padding: 14px 32px; background: linear-gradient(135deg, #1e3a8a 0%, #2563eb 100%); color: #ffffff; font-size: 14px; font-weight: 600; text-decoration: none; border-radius: 8px;">
                              Mở Bàn Làm Việc Xử Lý Ticket
                            </a>
                          </div>

                          <p style="margin: 24px 0 0 0; font-size: 13px; line-height: 1.6; color: #64748b;">
                            Trân trọng,<br>
                            <strong style="color: #1e293b;">Hệ thống Thông báo Hỗ trợ {{ApplicationBranding.HtmlName}}</strong>
                          </p>
                        </td>
                      </tr>

                      <!-- Footer -->
                      <tr>
                        <td style="background-color: #f8fafc; padding: 18px 28px; text-align: center; border-top: 1px solid #e2e8f0;">
                          <p style="margin: 0; font-size: 12px; color: #64748b;">
                            Email này được gửi tự động từ hệ thống <strong>{{ApplicationBranding.HtmlName}}</strong> khi khách hàng gửi yêu cầu hỗ trợ.
                          </p>
                          <p style="margin: 6px 0 0 0; font-size: 11px; color: #94a3b8;">
                            &copy; {{year}} DTC.,Ltd. All rights reserved.
                          </p>
                        </td>
                      </tr>

                    </table>
                  </td>
                </tr>
              </table>
            </body>
            </html>
            """;
    }

    public static string GetPriorityLabel(string? priority) => (priority ?? "Medium").ToLowerInvariant() switch
    {
        "urgent" => "Khẩn cấp",
        "high" => "Cao",
        "low" => "Thấp",
        _ => "Bình thường"
    };

    private static (string bg, string text, string border) GetPriorityColors(string? priority) =>
        (priority ?? "Medium").ToLowerInvariant() switch
        {
            "urgent" => ("#fef2f2", "#be123c", "#fecdd3"),
            "high" => ("#fffbeb", "#b45309", "#fde68a"),
            "low" => ("#f1f5f9", "#475569", "#cbd5e1"),
            _ => ("#eff6ff", "#1d4ed8", "#bfdbfe")
        };

    private static (string vnTime, string utcTime) FormatTimestamps(DateTime time)
    {
        var utcTime = time.Kind == DateTimeKind.Utc
            ? time
            : time.ToUniversalTime();
        var vnTime = utcTime.AddHours(7).ToString("HH:mm:ss dd/MM/yyyy", CultureInfo.InvariantCulture);
        var utcTimeStr = utcTime.ToString("HH:mm:ss dd/MM/yyyy", CultureInfo.InvariantCulture);
        return (vnTime, utcTimeStr);
    }
}

