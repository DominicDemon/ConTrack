# Không gian hợp đồng nội bộ

## Hai cổng

- **3001 – nội bộ công ty** (`Source/SystemAdmin`, `/workspace`): nhân viên đăng nhập bằng username; menu theo phòng ban + chức vụ.
- **3000 – khách hàng** (`Source/Frontend`, `/portal`): chỉ "Hợp đồng của tôi" và "Hỗ trợ" (sắp có).
  Mọi đường dẫn khác của Frontend (trang tenant / nội bộ cũ) được `next.config.mjs` chuyển về `/portal/contracts`;
  code cũ vẫn còn nhưng không truy cập được. Giữ lại `/portal`, `/public` (link hợp đồng cũ), `/api` và file tĩnh.

## Chạy local

API mặc định `ApplicationMode=Internal`, dùng riêng `ConnectionStrings:MasterDatabase`
(`appsettings.json` trên main đặt `Legacy`; máy dev ghi đè `"ApplicationMode": "Internal"` trong
`Backend/ContractManagement.API/appsettings.Development.json`, Git bỏ qua). Máy khác có thể cấu hình
biến `ConnectionStrings__MasterDatabase` và `ApplicationMode`.

Database là nguồn schema (database first); app không tự tạo bảng hay dữ liệu. Trước khi chạy,
database MASTER phải có đủ schema (gồm `Artifacts/Database/Master/001`–`005`), tài khoản
và quy trình duyệt `CONTRACT_INTERNAL_CHECK`, tất cả tạo trực tiếp bằng SQL.

Từ thư mục `Source`:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --project Backend/ContractManagement.API --no-launch-profile --urls http://127.0.0.1:5005
```

Mở thêm terminal: `npm run dev` tại `Source/SystemAdmin` (http://localhost:3001, nhân viên) và
tại `Source/Frontend` (http://localhost:3000, khách hàng).

Tài khoản nhân viên và khách hàng được tạo sẵn trong database. Mật khẩu lưu dạng hash ASP.NET Identity (`PasswordHasher`).
Chưa có mẫu hợp đồng nào: tải mẫu lên qua nút “Tải lên hợp đồng mẫu” ở màn tạo nháp.
Swagger (Development): http://localhost:5005/swagger.
API được proxy cùng origin qua Next; đổi đích bằng `INTERNAL_API_ORIGIN` nếu cần.

## Phòng ban & màn hình

`005-department-roles.sql`: 5 phòng ban (mã và id giống `Artifacts/Sql/seed-masterdb.sql`). Mỗi phòng có một role
nhân viên và một role quản lý (`DEPARTMENT.department_staff_role_id` / `department_manager_role_id`), đúng một quản lý
(`department_manager_employee_id`, unique có lọc); mỗi nhân viên thuộc đúng một phòng (`EMPLOYEE_DEPARTMENT`, unique theo nhân viên).
Role của phòng là role nội bộ duy nhất của nhân viên, nên quyền vẫn đọc qua `EMPLOYEE_ROLE` → `SYSTEM_ROLE_PERMISSION`.

| Phòng | Nhân viên | Quản lý có thêm |
|---|---|---|
| BGD – Ban Giám Đốc (CEO, `INTERNAL_ADMIN`) | — (chỉ có quản lý) | Duyệt & ký hợp đồng, Nhân viên & phân quyền, Phòng ban, Nhật ký bảo mật |
| KD – Kinh Doanh (`INTERNAL_SALE[_MANAGER]`) | Hợp đồng nháp của mình, Khách hàng, Sản phẩm (xem) | Nháp của cả phòng, Quản lý sản phẩm, Nhật ký hợp đồng |
| KTHUAT – Kỹ Thuật & Hỗ Trợ (`INTERNAL_TECHNICAL[_MANAGER]`) | Ticket được giao, Sản phẩm (xem) | Mọi ticket của phòng, phân công |
| KTTC – Kế Toán - Tài Chính (`INTERNAL_FINANCE[_MANAGER]`) | Hóa đơn & thanh toán, Hợp đồng (xem) | Duyệt thanh toán / công nợ |
| HCNS – Hành Chính - Pháp Chế (`INTERNAL_LEGAL[_MANAGER]`) | Mẫu hợp đồng (xem), Hồ sơ pháp lý, Hợp đồng (xem) | Quản lý mẫu hợp đồng |

Mọi người có **Tổng quan** (`/workspace`). Menu lấy từ `SystemAdmin/components/internal/internal-nav.ts`; màn chưa có thì không hiện trong menu
(quyền đã có trong `005`, thêm vào `NAV_ITEMS` khi làm xong màn). Đã chạy: Tổng quan, Hợp đồng nháp, Nhân viên & phân quyền, Phòng ban (`/org`),
Khách hàng (`/crm`, quyền `customer.manage`), Sản phẩm và dịch vụ (`/catalog`: xem với `product.read`, thêm / sửa sản phẩm với `product.manage`).
Hỗ trợ khách hàng: code nghiệp vụ ticket của main (`SupportTicketService`) giữ nguyên, nối qua `/api/internal/tickets` (3001, `/workspace/support-tickets`)
và `/api/customer/support` (3000, `/portal/support`). Ticket gửi tới 4 phòng KTHUAT / KD / KTTC / HCNS; trưởng phòng (`ticket.manage.all`)
thấy ticket của phòng, phân công, trao đổi với khách và đóng; nhân viên (`ticket.handle`) thấy ticket được giao và báo hoàn thành.
Trưởng phòng lấy từ `department_manager_employee_id` (không đoán theo tên), `customer_id` là khách đăng nhập portal, app không tự tạo bảng `TICKET_*`.
Trang ticket cũ của main (`(protected)/support-tickets` ở cả hai app) và controller `/api/support/tickets`, `/api/admin/tickets` vẫn còn nhưng chỉ chạy ở chế độ Legacy.
`INTERNAL_OFFICE` cũ bị ngừng; bước duyệt `CONTRACT_INTERNAL_CHECK` và thông báo "khách đã đồng ý" chuyển sang Ban Giám Đốc.
`draft.manage.all` hiện vẫn là toàn công ty (chỉ phòng KD có nháp).

## Luồng sử dụng

1. Đăng nhập → Hợp đồng nháp → Tạo hợp đồng nháp.
2. Chọn mẫu đã tải lên, chọn khách hoặc tạo nhanh bằng tên và SĐT.
3. Nhập tên hợp đồng, thông tin hai bên, hạng mục, VAT và điều khoản.
4. Xem trước bên phải; kéo thanh giữa để đổi chiều rộng. Mobile có tab Soạn thảo/Xem trước.
5. Lưu nháp, quay về danh sách rồi mở lại để sửa.

Chỉ mẫu, khách hàng và tên là bắt buộc khi lưu. Thông tin thiếu được đánh dấu.
Mẫu tải lên chưa được coi là đã phê duyệt; thông tin pháp nhân phải do người dùng nhập.
Khách và mẫu được chụp vào nháp, không tự cập nhật theo dữ liệu gốc về sau.
Nhân viên kinh doanh chỉ truy cập nháp của mình; quản lý kinh doanh xem được tất cả. Cả hai được tạo khách.
Chưa có OTP ký, màn Ban Giám Đốc duyệt / ký, thanh toán hoặc xuất DOCX/PDF.

## Nhân viên & phân quyền

Ban Giám Đốc (quyền `employee.manage`) thấy menu **Nhân viên & phân quyền**
(`/workspace/admin/employees`, API `/api/internal/admin/*`):

- Tạo tài khoản: mã, tên đăng nhập, họ tên, email/SĐT tùy chọn, **phòng ban** và **chức vụ** (nhân viên / quản lý).
  Hộp thoại hiện các màn hình chức vụ đó được dùng. Phòng đã có quản lý thì không chọn được chức vụ quản lý (API trả 409).
  Hệ thống sinh mật khẩu tạm 12 ký tự, chỉ hiện một lần, và bật `employee_must_change_password`.
- Đổi phòng ban / chức vụ (`PUT /admin/employees/{id}/position`), khóa / mở khóa (`employee_status` `ACTIVE` / `LOCKED`), cấp lại mật khẩu tạm.
  Quyền được đọc lại ở mỗi request nên thay đổi có hiệu lực ngay; tài khoản bị khóa bị đăng xuất ở thao tác kế tiếp.
- Admin không thao tác được trên tài khoản của chính mình, nên luôn còn ít nhất một admin.
- Khi còn mật khẩu tạm, mọi API trừ `/auth/me`, `/auth/change-password`, `/auth/logout` trả 403
  `PASSWORD_CHANGE_REQUIRED`, và giao diện chỉ hiện màn đổi mật khẩu. Mật khẩu mới: tối thiểu 8 ký tự, có chữ và số.
- Mỗi thao tác ghi `AUDIT_LOG` (`CREATE`, `CHANGE_POSITION`, `LOCK`, `UNLOCK`, `RESET_PASSWORD`, `CHANGE_PASSWORD`).

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
- `002-contract-review.sql` thêm luồng gửi khách xem và hàng đợi SMS; `003-draft-content.sql` thêm
  3 bảng nội dung nháp. Các script idempotent, chạy tay (SSMS hoặc `sqlcmd -I`); app không tự chạy.
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

Live tests cần database có tài khoản `sale` và `admin` cùng ít nhất một mẫu hợp đồng; chúng tạo và dọn dữ liệu thử; gồm lưu/mở lại, snapshot khách, quyền sở hữu,
xung đột rowversion, tính tiền và rollback lỗi SQL sau khi đã tạo document/version.
Không có biến kết nối thì các live tests được đánh dấu skip.
Frontend: `npm run typecheck` và `npm run build`.
