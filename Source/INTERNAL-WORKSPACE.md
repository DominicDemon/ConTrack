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
Nháp dùng mẫu Word hỗ trợ xem trước PDF và tải DOCX.

## Gửi khách xem

Nhân viên làm việc ở SystemAdmin (`npm run dev` tại `Source/SystemAdmin`, http://localhost:3001, proxy `/api/internal`).
Khách dùng cổng `/portal` của Frontend (http://localhost:3000/portal/login, proxy `/api/customer`).
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

### eSMS

Cấu hình `Esms` trong `appsettings.json`; `ApiKey`/`SecretKey` chỉ đặt ở `appsettings.Development.json` hoặc biến môi trường
(`Esms__ApiKey`, `Esms__SecretKey`). `Esms:Enabled=false` dùng FakeSmsSender (không gọi mạng, chỉ log SĐT đã che và độ dài).
`Sandbox=true` gửi `Sandbox: "1"`: eSMS kiểm tra và trả 100 nhưng không gửi tới máy, không trừ tiền; gửi thật cần `Sandbox=false` cùng brandname/template đã đăng ký (xem CONTRACT-REVIEW-CHANGES.md). Template tin nhắn nằm ở `Esms:TemporaryPasswordTemplate` / `Esms:ReviewNoticeTemplate`
và phải khớp template CSKH đã đăng ký. `CustomerPortal:BaseUrl` là link trong SMS.
SMS_OUTBOX mã hóa SĐT/nội dung bằng Data Protection (`ConTrack.SmsOutbox.v1`); worker gửi lại 1/5/15 phút, tối đa 5 lần,
xóa ciphertext khi rời hàng đợi. Callback `GET /api/sms/esms/callback` chỉ nhận IP trong `AllowedCallbackIps`
(`AllowAnyCallbackIp=true` chỉ để thử local). Khóa Data Protection phải được giữ ổn định giữa các lần chạy để gửi được tin đang chờ.

## Dữ liệu và kỹ thuật

### Tải mẫu Word tại màn tạo nháp

- Nút **Tải mẫu Word** nhận DOC/DOCX tối đa 10 MB. Cài LibreOffice hoặc cấu hình `TemplatePdfRendering:ExecutablePath` trên máy API.
- Sửa tay nội dung văn bản và ô bảng; giao diện không có chọn/quản lý trường dữ liệu. Mẫu mới dùng `ManualContent`, giữ nguyên chữ trong DOCX/PDF. Mẫu cũ tiếp tục xử lý theo cấu hình đã lưu.
- Chọn đoạn gốc rồi chèn đoạn văn, điều khoản (tiêu đề + nội dung), bảng hoặc ảnh PNG/JPEG. Điều khoản không tự đánh lại số của các điều cũ.
- Ảnh mới tối đa 5 MB/ảnh và 10 MB tổng; chỉnh rộng 1–16 cm, giữ tỷ lệ và căn trái/giữa/phải. PNG/JPEG có sẵn trong Word được hiển thị ở khung chỉnh sửa; PDF là bản đối chiếu bố cục.
- Màn tạo nháp giữ các thông tin khách hàng, ngày và số tiền; mẫu sửa tay không tự thay các thông tin đó vào chữ của Word.
- Chọn loại hợp đồng (1), phụ lục (2), bàn giao (3), biên lai (4), đề nghị thanh toán (5). Bộ chọn hợp đồng chỉ lấy mẫu loại 1 đang hoạt động.
- Tạo PDF, kiểm tra rồi xác nhận kích hoạt. Sửa nội dung yêu cầu xem trước lại.
- Các API mới là `/api/internal/contract-templates/word/extract`, `/word/preview`, `/word/activate`, cùng session/CSRF/quyền nội bộ hiện có.
- File gốc, DOCX cố định và JSON được lưu private storage với SHA-256. Snapshot nháp giữ khóa và hash DOCX, hỗ trợ xem trước PDF và tải DOCX.
- `003-template-fields.sql` bổ sung bảng trường mẫu trong bootstrap, có thể chạy lại trên DB đã cài. Schema chính là `MasterDB-Optimize_v1.sql`.
- Chạy `InternalWordSampleTests` với `CONTRACK_WORD_SAMPLE_ROOT` và `CONTRACK_MASTER_TEST_CONNECTION` để nghiệm thu năm file. Dữ liệu kiểm thử trong DB được tự dọn.

### API trích chữ cũ

- API trích chữ trước đây tiếp tục nhận DOCX/PDF tối đa 10 MB và PDF tối đa 100 trang để tương thích với caller hiện hữu.
- `POST /api/internal/contract-templates/extract`: `{fileName, base64}` trả `{name, content, warning}`.
- `POST /api/internal/contract-templates/import`: thêm `{name, content}` đã kiểm tra, trả `TemplateDefinition` có `isImported=true`.
  Cả hai endpoint dùng session/CSRF và quyền `draft.write`; mẫu nhập vẫn chưa được coi là đã phê duyệt.
- File gốc và định nghĩa mẫu cũ có checksum được lưu private storage, liên kết `FILE_STORAGE`;
  mẫu, trường và audit được tạo trong transaction.
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

### Chèn bảng khi tải mẫu Word

- Chọn một đoạn trong nội dung chính, nhập số hàng/cột rồi chọn **Chèn bảng sau vị trí chọn**.
  Nếu chọn ô trong bảng gốc, bảng mới nằm sau toàn bộ bảng đó; không chèn vào đầu/chân trang.
- Bảng mới hỗ trợ thêm hàng dưới hàng chọn, xóa hàng chọn, thêm/xóa cột cuối,
  gộp với ô bên phải, tách ô đã gộp và bật/tắt viền in.
  Các thao tác cấu trúc này chỉ áp dụng cho bảng mới. Tối đa 20 bảng, mỗi bảng 50 hàng × 12 cột.
- Chọn **Cập nhật bản xem trước** sau khi sửa. PDF cũ không được dùng để kích hoạt.
- `WordTemplateInput.Tables` lưu các bảng theo vị trí paragraph gốc; máy chủ tạo OpenXML,
  quét placeholder trong tài liệu sau khi chèn và đưa bảng vào DOCX cố định của mẫu/snapshot.

### Chạy kiểm thử

Từ `Source/Backend`:

```powershell
$env:CONTRACK_MASTER_TEST_CONNECTION = 'Server=localhost;Database=ContractManagement_Master;Integrated Security=True;TrustServerCertificate=True'
dotnet test ContractManagement.Tests --filter 'FullyQualifiedName~InternalDraftTests|FullyQualifiedName~MasterDatabaseTests|FullyQualifiedName~ContractReview'
```

Live tests tạo và dọn dữ liệu thử; gồm lưu/mở lại, snapshot khách, quyền sở hữu,
xung đột rowversion, tính tiền và rollback lỗi SQL sau khi đã tạo document/version.
Không có biến kết nối thì các live tests được đánh dấu skip.
Frontend: `npm run typecheck` và `npm run build`.

### Tự điền hồ sơ từ Word đã sửa

- Sau khi kích hoạt hoặc chọn mẫu Word, `/contract-templates/{id}/word-details` đọc DOCX đã lưu và kiểm tra SHA-256, rồi nhận diện tiêu đề, các nhãn Bên A/Bên B, ngày hiệu lực/hết hạn rõ ràng, tổng tiền và bảng hạng mục.
- Chỉ đưa hạng mục vào nháp khi số lượng × đơn giá khớp tổng tiền; dữ liệu không rõ được để trống và có ghi chú. Không dùng ngày hiệu lực của văn bản pháp luật làm ngày hiệu lực hợp đồng.
- Nhận diện chạy trên nội dung sau chỉnh sửa, không lấy lại dữ liệu của tệp gốc. Chọn khách nhận vẫn là bước đối chiếu tài khoản, không tự tạo hoặc gửi hợp đồng.
- Mẫu sửa tay chưa lưu nháp xem/tải file qua `/contract-templates/{id}/word-preview`, không gửi toàn bộ dữ liệu nháp chưa hoàn thiện. Nháp đã lưu vẫn dùng snapshot và kiểm tra quyền sở hữu.
