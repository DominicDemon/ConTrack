# Xác nhận thanh toán

Chứng từ xác nhận thanh toán dịch vụ, không phải hóa đơn GTGT. PDF được lưu từ trình duyệt, không đính kèm email.

Theo yêu cầu bổ sung, menu avatar của tenant có **Gói dịch vụ & Thanh toán**, dẫn tới `/billing/invoices`. Trang hiển thị lịch sử giao dịch thực thu đã thanh toán, phân trang 20 giao dịch, thời hạn sử dụng và liên kết mở từng chứng từ. API `GET /api/billing/invoices?page=1` áp dụng cùng quyền tenant/người mua và yêu cầu đổi mật khẩu như trang chi tiết; không có dữ liệu của người mua khác. Gói dùng thử miễn phí không có trong danh sách. Cần restart API để tải endpoint danh sách mới; không cần migration bổ sung.

## Luồng và quyền

- Email/trang kết quả mở `/billing/receipts/{publicId}` để kiểm tra trạng thái. Khi sẵn sàng, mở `/billing/invoices/{publicId}`.
- `GET /api/public/invoices/{publicId}/readiness` chỉ có trạng thái, `isPaid` và dữ liệu điều hướng. Không có tên, email, giá hoặc nội dung chứng từ.
- `GET /api/billing/invoices/{publicId}` dùng `SessionAuthorize` kiểm tra phiên và cờ bắt buộc đổi mật khẩu trên từng request; truy vấn tiếp tục giới hạn theo tenant, ID người mua và trạng thái PAID. Không tìm người mua theo email hoặc vai trò Manager.
- Mã người mua lấy trực tiếp từ tài khoản được tạo lúc provisioning. Tenant và người mua được lưu trước khi trả về fulfillment, để lần thử lại tái sử dụng tenant đã tạo.
- `returnTo` luôn là đường dẫn chứng từ chuẩn hoặc pricing được cho phép; tenantCode đi riêng trong query. Đăng nhập sai, refresh, 401, 403 MustChangePassword và đổi mật khẩu đều giữ đích. Tài khoản khác không tự bị đăng xuất.
- Poll ngay, đợi 4 giây sau mỗi request, không chồng request; kiểm tra lại khi tab hiện lên. Giới hạn 15 phút, hủy request/timer khi rời trang và cho phép tiếp tục kiểm tra.

## Dữ liệu và migration

Migration `20261028074413_PaymentConfirmations` thêm public ID ngẫu nhiên duy nhất, snapshot tên gói/người mua/thời hạn, ID người mua, lease provisioning và bảng `INVOICE_EMAIL_OUTBOX`.

Local đã áp dụng ngày 28/10/2026. Backup COPY_ONLY + CHECKSUM đã RESTORE VERIFYONLY:

`C:\Program Files\Microsoft SQL Server\MSSQL17.MSSQLSERVER\MSSQL\Backup\ContractManagement_Central_before_PaymentConfirmations_E718E9ED489F4A449B0F34DA2E4CA3FA.bak`

Sau migration: 3 hóa đơn, 3 subscription; checksum `(invoice_id, invoice_total_amount, invoice_status)` giữ nguyên `1087686164`; 3 public ID khác nhau; không tạo outbox cho hóa đơn cũ.

Thời hạn cũ được backfill từ PLAN_VERSION đã mua. Không thể khôi phục chắc chắn tên gói/người mua tại thời điểm giao dịch từ các trường hiện tại, nên giữ null và hiển thị cần đối chiếu thay vì tự điền tên hiện tại.

Đối chiếu người mua cũ (chỉ DB local, phải nhập đúng catalog):

```powershell
dotnet run --project Backend/ContractManagement.RecoveryOps -c Release -- invoice-backfill Backend/ContractManagement.API ContractManagement_Central
```

Chỉ gán khi audit `TenantProvisioned` và một audit `EmployeeCreated` cùng correlation `subscription-{publicId}`, tenant/owner khớp, tài khoản tồn tại duy nhất và thời điểm tạo khớp audit. Tài khoản bị tạo lại cùng tên, thiếu audit hoặc nhiều kết quả đều giữ null. Không tạo email và có thể chạy lại. Đã chạy local: 2 subscription có tenant được xác định người mua; 0 subscription có tenant còn cần đối chiếu.

## Email

Activation và tạo outbox nằm trong một `SaveChanges` transaction. Unique `(InvoiceId, Kind)`; claim bằng concurrency token, lease 2 phút; timeout gửi 60 giây; tối đa 5 lần, chờ tăng dần 30/60/120/240/480 giây. Lỗi gửi không đảo tiền hoặc tạo tenant lại. Chỉ gửi giao dịch thực thu > 0 đến `BillingEmail` đã chốt. Không chứa mật khẩu tạm trong outbox.

Mặc định **tắt gửi email xác nhận** trong `appsettings.json`. Email tài khoản hiện có không đổi. Đã kiểm tra nội dung/recipient/HTML encoding qua `RecoveryTestMailbox` và lỗi SMTP bằng transport giả lập.

Để chạy kiểm thử tại Development:

```json
"InvoiceEmail": { "Enabled": true, "Provider": "Test" }
```

Provider Test giữ tối đa 20 thư trong mailbox bộ nhớ, không gửi ra ngoài và không cho bật ở Production. Các thư Test được đánh dấu SENT, không tự gửi lại khi đổi sang SMTP.

Sau khi kiểm tra hộp thư SMTP thực được chỉ định, bật cấu hình `InvoiceEmail:Enabled=true`, `InvoiceEmail:Provider=Smtp`, dùng cấu hình SMTP hiện có và URL tin cậy `AccountRecovery:TenantFrontendBaseUrl`, rồi restart API. Chưa thực hiện gửi SMTP thật trong lần triển khai này.

SMTP không đảm bảo exactly-once: nếu tiến trình dừng sau khi máy chủ SMTP nhận thư nhưng trước khi lưu SENT, lần thử lại có thể gửi lại email. Unique outbox vẫn ngăn tạo thêm hóa đơn/tenant/outbox. Email tài khoản cũ vẫn có giới hạn lưu mật khẩu chỉ trong bộ nhớ; nếu crash trước khi gửi email tài khoản, cần luồng khôi phục mật khẩu hiện có.

## Kiểm tra

- 67 kiểm thử BE liên quan qua: quyền/session, snapshot, webhook lặp, provisioning, backfill và cạnh tranh/retry outbox.
- `node --test Frontend/scripts/payment-receipt.test.mjs` (chạy từ thư mục Source hoặc điều chỉnh đường dẫn): 6 ca redirect/polling.
- `Frontend/scripts/payment-receipt.e2e.cjs`: Edge headless, API giả lập độc lập, không dùng tài khoản thật; kiểm tra đăng nhập sai, refresh từng bước, đổi mật khẩu, quyền sai tài khoản, interceptor, desktop/mobile/dark và CSS bản in. Chạy từ Frontend; cài Playwright hoặc đặt `PLAYWRIGHT_MODULE` tới package có sẵn.
- `npx tsc --noEmit` và ESLint trên các file thay đổi; cảnh báo legacy về unused/any còn ở form/auth API cũ.
- `graphify update .` chưa chạy được vì máy chưa có lệnh graphify.

Sau thay đổi BE cần restart tiến trình API đang chạy để tải endpoint/worker mới. Không có lệnh bật SMTP thật tự động trong migration hoặc backfill.

## Cập nhật vận hành local 28/10/2026

Sau khi người dùng báo chưa nhận link, kiểm tra thấy outbox đã tạo nhưng worker bị tắt bởi cấu hình mặc định. Đã bật `InvoiceEmail.Enabled=true`, `Provider=Smtp` riêng trong `appsettings.Development.json` (file bị gitignore). API đang chạy dưới Visual Studio cần restart để worker tự chạy với cấu hình mới.

Đã gửi thành công ở mức SMTP chấp nhận cho một chứng từ được chỉ định; outbox chuyển `SENT`, số lần thử 1. Không gửi lại email mật khẩu, không tạo thêm chứng từ. Không thể khẳng định thư nằm trong Inbox chỉ từ phản hồi SMTP.

Lệnh xử lý riêng một email đã có trong outbox:

```powershell
dotnet run --project Backend/ContractManagement.RecoveryOps -c Release -- invoice-deliver Backend/ContractManagement.API <invoice-public-id>
```

Lệnh yêu cầu SMTP đã bật, giữ lease/backoff/giới hạn lần thử và bỏ qua thư đã `SENT`. Không tự tạo outbox cho hóa đơn cũ hoặc xử lý thư của chứng từ khác. Đã thêm kiểm thử lọc public ID và chống gửi lại; 8 kiểm thử InvoiceTests đều qua.
