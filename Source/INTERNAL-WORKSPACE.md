# Không gian hợp đồng nội bộ

## Chạy local

API mặc định `ApplicationMode=Internal`, dùng riêng `ConnectionStrings:MasterDatabase`.
Máy hiện tại đã cấu hình Windows Authentication tới `localhost/ContractManagement_Master`
trong `Backend/ContractManagement.API/appsettings.Development.json` (Git bỏ qua).
Máy khác có thể cấu hình biến `ConnectionStrings__MasterDatabase`.

Từ thư mục `Source`, sau khi có schema MASTER:

```powershell
.\scripts\Initialize-InternalWorkspace.ps1
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --project Backend/ContractManagement.API --no-launch-profile --urls http://127.0.0.1:5005
```

Mở terminal thứ hai tại `Source/Frontend`:

```powershell
npm install
npm run dev
```

Truy cập http://localhost:3000. Tài khoản `sale.local` và `office.local` cùng mật khẩu
ngẫu nhiên nằm ở `.local/internal-accounts.json`, không được commit. Script chạy lại
không đổi mật khẩu đã có. Không xóa file này để tạo lại mật khẩu của tài khoản hiện hữu.
Swagger (Development): http://localhost:5005/swagger.
API được proxy cùng origin qua Next; đổi đích bằng `INTERNAL_API_ORIGIN` nếu cần.

## Luồng sử dụng

1. Đăng nhập → Hợp đồng nháp → Tạo hợp đồng nháp.
2. Chọn mẫu minh họa, chọn khách hoặc tạo nhanh bằng tên và SĐT.
3. Nhập tên hợp đồng, thông tin hai bên, hạng mục, VAT và điều khoản.
4. Xem trước bên phải; kéo thanh giữa để đổi chiều rộng. Mobile có tab Soạn thảo/Xem trước.
5. Lưu nháp, quay về danh sách rồi mở lại để sửa.

Chỉ mẫu, khách hàng và tên là bắt buộc khi lưu. Thông tin thiếu được đánh dấu.
Mẫu demo chưa được công ty phê duyệt; thông tin pháp nhân phải do người dùng nhập.
Khách và mẫu được chụp vào nháp, không tự cập nhật theo dữ liệu gốc về sau.
Sale chỉ truy cập nháp của mình; văn phòng có quyền toàn công ty. Cả hai được tạo khách.
Chưa có OTP ký, màn hành chính duyệt từng bước, thanh toán, ký hoặc xuất DOCX/PDF.

## Gửi khách xem

Nhân viên làm việc ở SystemAdmin (`npm run dev` tại `Source/SystemAdmin`, http://localhost:3001, proxy `/api/internal`).
Khách dùng cổng `/portal` của Frontend (proxy `/api/customer`). Luồng đăng nhập mật khẩu dưới đây là luồng cũ; luồng đang dùng xem mục "Luồng hiện tại".
Khu workspace cũ ở Frontend vẫn còn song song, sẽ gỡ sau.

1. Nháp đủ thông tin → **Gửi khách xem**: khóa phiên bản (hash SHA-256 của `__draft.v1`), tạo CONTRACT_REVIEW_REQUEST `SENT`,
   hồ sơ → 1. Khách chưa đổi mật khẩu tạm (`customer_personal_phone_verified_at` NULL) nhận SMS mật khẩu tạm 24 giờ;
   khách đã có mật khẩu nhận SMS báo có hợp đồng.
2. Khách đăng nhập bằng SĐT, bị bắt đổi mật khẩu tạm (`PASSWORD_CHANGE_REQUIRED`), xem đúng phiên bản đã gửi,
   góp ý theo điều khoản, rồi Đồng ý (hồ sơ 2, tạo CONTRACT_APPROVAL_REQUEST theo workflow `CONTRACT_INTERNAL_CHECK`),
   Yêu cầu sửa (hồ sơ giữ 1) hoặc Từ chối (hồ sơ 7). Mỗi phản hồi tạo NOTIFICATION cho sale (và hành chính khi đồng ý).
3. Sale trả lời/đánh dấu đã xử lý góp ý, **Tạo phiên bản mới** rồi gửi lại (lần trước `CHANGES_REQUESTED` → `SUPERSEDED`).
   Sau khi khách từ chối: **Thương lượng tiếp** (về 1) hoặc **Đóng draft** (6). Lần bị từ chối giữ trạng thái `REJECTED`
   vì CHECK `CK_ReviewRequest_Followup` chỉ cho quyết định trên dòng `REJECTED`.
4. **Cấp lại mật khẩu tạm**: chỉ khi khách chưa đổi mật khẩu tạm, tối đa 3 lần/giờ.

Session khách dùng cookie riêng `.ConTrack.Customer` (chọn theo đường dẫn `/api/customer`), không dùng chung `.ConTrack.Internal`.
Đăng nhập khách: giới hạn theo IP và 5 lần sai/15 phút theo SĐT. Mật khẩu khách tối thiểu 8 ký tự, có chữ và số.

### Luồng hiện tại: link email + OTP (thay cho mật khẩu tạm)

1. Sale bấm **Gửi khách xem** và chọn **Link hết hạn lúc** (mặc định 7 ngày). Hệ thống khóa phiên bản và gửi email,
   không gửi SMS. Email nhận: trường `CUSTOMER_EMAIL` trong hợp đồng, nếu trống hoặc sai định dạng thì lấy email của khách.
2. Link có dạng `http://localhost:3000/portal/review/<token>`. Khách nhập SĐT trùng `CUSTOMER_CONTACT_PHONE` của hợp đồng
   (trống hoặc sai định dạng thì lấy SĐT của khách) rồi nhập OTP 6 số.
3. Sau OTP, phiên chỉ mở được đúng lần gửi đó, để xem, góp ý, đồng ý, yêu cầu sửa hoặc từ chối. Link hết hạn thì phiên cũng hết.
4. **Gửi lại link**: chỉ khi khách chưa phản hồi. Link cũ bị thu hồi; dùng khi email lỗi, khách mất mail hoặc link hết hạn.

Giới hạn: OTP 5 phút, gửi lại sau 60 giây, tối đa 5 lần gửi/giờ, 5 lần nhập sai/mã, 5 lần sai SĐT thì khóa 15 phút.
Dữ liệu: `CONTRACT_ACCESS_LINK` (hash token, hạn, thu hồi, `activated_at`) và `CONTRACT_PHONE_VERIFY` (SĐT đối chiếu); không đổi schema.
OTP chỉ nằm trong bộ nhớ API, restart thì phải gửi lại mã.

Test local: `CustomerPortal:ConsoleDelivery=true` trong `appsettings.Development.json` in link và OTP ra terminal của API
(dòng `[DEV]`), không gọi eSMS. Cờ này chỉ có tác dụng ở môi trường Development; tắt cờ thì OTP gửi qua `ISmsSender`
với mẫu `Esms:ReviewOtpTemplate`. Trang `/portal/login`, `/portal/change-password` và API cấp lại mật khẩu tạm vẫn còn nhưng không nằm trong luồng này.

### eSMS

Cấu hình `Esms` trong `appsettings.json`; `ApiKey`/`SecretKey` chỉ đặt ở `appsettings.Development.json` hoặc biến môi trường
(`Esms__ApiKey`, `Esms__SecretKey`). `Esms:Enabled=false` dùng FakeSmsSender (không gọi mạng, chỉ log SĐT đã che và độ dài).
`Sandbox=true` gửi `Sandbox: "1"`: eSMS kiểm tra key, brandname và trả 100 (không kiểm template) nhưng không gửi tới máy, không trừ tiền; gửi thật cần `Sandbox=false` cùng brandname/template đã đăng ký (xem Backend/docs/CONTRACT-REVIEW-CHANGES.md). Template tin nhắn nằm ở `Esms:TemporaryPasswordTemplate` / `Esms:ReviewNoticeTemplate`
và phải khớp template CSKH đã đăng ký. `CustomerPortal:BaseUrl` là link trong SMS.
SMS_OUTBOX mã hóa SĐT/nội dung bằng Data Protection (`ConTrack.SmsOutbox.v1`); worker gửi lại 1/5/15 phút, tối đa 5 lần,
xóa ciphertext khi rời hàng đợi. Callback `GET /api/sms/esms/callback` chỉ nhận IP trong `AllowedCallbackIps`
(`AllowAnyCallbackIp=true` chỉ để thử local). Khóa Data Protection phải được giữ ổn định giữa các lần chạy để gửi được tin đang chờ.

## Dữ liệu và kỹ thuật

### Tải mẫu Word/PDF tại màn tạo nháp

- Nút “Tải lên hợp đồng mẫu” nằm dưới ô chọn mẫu, chỉ ở nháp mới vì nháp đã lưu giữ nguyên snapshot.
- DOCX/PDF tối đa 10 MB, PDF tối đa 100 trang; Word .doc cần lưu lại thành .docx.
- Đọc nội dung chữ, cho người dùng sửa và xác nhận trước khi tạo mẫu dùng chung.
  Không tự nhận diện các bên, không tự thay tên khách trong văn bản và không giữ nguyên bố cục file gốc.
- PDF không có lớp chữ được cảnh báo; người soạn phải tự bổ sung nội dung. Chưa có OCR.
- `POST /api/internal/contract-templates/extract`: `{fileName, base64}` trả `{name, content, warning}`.
- `POST /api/internal/contract-templates/import`: thêm `{name, content}` đã kiểm tra, trả `TemplateDefinition` có `isImported=true`.
  Cả hai endpoint dùng session/CSRF và quyền `draft.write`; mẫu nhập vẫn chưa được coi là đã phê duyệt.
- File gốc và định nghĩa mẫu có checksum được lưu private storage, liên kết `FILE_STORAGE`;
  mẫu, trường và audit được tạo trong transaction. Không thay schema DB.
- Màn nháp kế thừa token màu/font sáng/tối từ giao diện chung và sử dụng `SummaryCards` của các màn cũ.

- Domain tính tiền/làm sạch nội dung; Application kiểm tra quyền và điều phối;
  Infrastructure lưu EF/storage; API ánh xạ request/response.
- API `/api/internal` dùng session HttpOnly riêng, CSRF và hạn chế tốc độ đăng nhập.
  ID BIGINT là chuỗi JSON. API legacy không được ánh xạ trong chế độ Internal.
- `001-internal-drafts.sql` nâng current_version_id lên BIGINT, thêm unique username,
  sửa index email khách thành filtered unique để cho phép nhiều email NULL.
- Bootstrap seed quyền/vai trò/mẫu có thể chạy lặp, dùng mật khẩu từ cấu hình local.
- File mẫu riêng nằm ở `Backend/private-storage/internal-templates`; metadata/hash nằm
  trong FILE_STORAGE. Có thể đổi đường dẫn bằng `InternalStorage__Root`.
- Tạo/cập nhật document, version, items, terms, dynamic values và audit trong transaction.
  Nháp giữ version 1 chưa khóa; `__draft.v1` lưu snapshot đầy đủ, các trường được ghi riêng.
- Hai rowversion của document/version chống ghi đè; HTTP 409 yêu cầu tải lại.
- Tiền VND do backend tính lại; giảm theo phần trăm hoặc số tiền, VAT chung.
  Nội dung xem trước là text được escape, không render HTML thô.
- Session hiện lưu trong bộ nhớ: khởi động lại API cần đăng nhập lại. Triển khai nhiều
  instance cần cấu hình distributed session store và khóa Data Protection dùng chung.

Legacy chỉ bật rõ ràng bằng `ApplicationMode=Legacy` ở API và `APPLICATION_MODE=Legacy`
khi chạy/build frontend, với cấu hình DB legacy riêng. Không trỏ DbContext legacy vào MASTER.

## Kiểm tra

Từ `Source/Backend`:

```powershell
$env:CONTRACK_MASTER_TEST_CONNECTION = 'Server=localhost;Database=ContractManagement_Master;Integrated Security=True;TrustServerCertificate=True'
dotnet test ContractManagement.Tests --filter 'FullyQualifiedName~InternalDraftTests|FullyQualifiedName~MasterDatabaseTests|FullyQualifiedName~ContractReview'
```

Live tests tạo và dọn dữ liệu thử; gồm lưu/mở lại, snapshot khách, quyền sở hữu,
xung đột rowversion, tính tiền và rollback lỗi SQL sau khi đã tạo document/version.
Không có biến kết nối thì các live tests được đánh dấu skip.
Frontend: `npm run typecheck` và `npm run build`.
