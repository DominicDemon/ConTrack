# Đợt 1: duyệt nội bộ và quản lý mẫu

Nhánh triển khai: `feature-hoang`. Kiểm thử local ngày 17/11/2026.

## Chạy thử

- Nội bộ: http://localhost:3001 — ba vai trò dùng chung giao diện.
- Khách hàng: http://localhost:3000.
- API local: http://127.0.0.1:5005.
- Tài khoản test ba vai trò nằm trong `Source/.local/approval-accounts.json` (không đưa vào Git). `sale`, `office`, `director` là các khóa tương ứng.
- Hồ sơ kiểm chứng qua UI: `/workspace/contracts/drafts/41`. Đã qua Hành chính, Sếp và gửi khách; xem lịch sử trên hồ sơ.
- Phiên chạy nghiệm thu tắt gửi SMTP/SMS thật. Yêu cầu gửi vẫn được lưu; kiểm thử mail dùng transport giả. Cần cấu hình SMTP/SMS hợp lệ khi kiểm tra giao nhận thực tế.

## Quy trình

1. Sale chọn mẫu hoạt động, lưu nháp, gửi nhận xét và bấm **Trình Hành chính**.
2. Hành chính xem Word và thông tin, duyệt hoặc nhập lý do trả lại. Khi trả lại, Hành chính sửa Word của hồ sơ bằng **Sửa nội dung Word của hợp đồng**, xem trước rồi lưu. Mẫu chung giữ nguyên.
3. Sale trình lại; luôn bắt đầu lại từ Hành chính. Hành chính duyệt mới tới Sếp.
4. Sếp duyệt hoặc trả lại. Đủ hai lượt duyệt, Sale mới gửi được khách.
5. Khách yêu cầu sửa: Sale tạo phiên bản mới, xử lý nội dung và duyệt lại hai cấp.

API kiểm tra quyền, rowversion hợp đồng/phiên bản/yêu cầu duyệt, hash snapshot và SHA-256 Word. Hồ sơ cũ không được dùng lịch sử duyệt cũ để bỏ qua luồng mới.

## Database

Chạy các script Master trước đó (đặc biệt `005-department-roles.sql`), sau đó `006-internal-approval.sql`. Script 006 đã chạy hai lần trên `ContractManagement_Master`. Giữ nguyên lịch sử, bổ sung hai cột nullable vào yêu cầu duyệt, workflow `CONTRACT_PRE_SEND`, hai bước bắt buộc và quyền Hành chính.

## Kết quả kiểm thử

- Build API thành công, 0 warning/error; TypeScript Frontend và SystemAdmin đạt.
- Kiểm thử SQL local `InternalApprovalTests`: đạt. Bao gồm vượt cấp, khóa sửa, trả lại, trình lại, hai người duyệt đồng thời, stale rowversion, snapshot thay đổi, gửi lặp, khách yêu cầu sửa và duyệt lại phiên bản tiếp theo.
- DOCX phía khách trùng byte bản đã duyệt; PDF chứa nội dung đã duyệt; khách khác bị chặn.
- 66 kiểm thử hồi quy hợp đồng/Word đạt, 3 ca phụ thuộc fixture riêng bị bỏ qua.
- Smoke API với Word BASAO thật đạt: sửa bản hợp đồng riêng, mẫu chung không đổi, preview/download thành công, gửi sau đủ hai lượt duyệt, gửi lặp bị chặn.
- UI ba vai trò đạt: menu mẫu chỉ có ở Hành chính; Hành chính → Sếp → Sale gửi. Ảnh local: `.local/approval-flow.jpg`.
- Bộ backend đầy đủ: 1025 đạt, 320 lỗi, 19 bỏ qua. Có lỗi ánh xạ EF `CustomerCode`/`EmployeeType` và recovery trong các phân hệ ngoài luồng mới; chưa coi toàn bộ repo đạt hồi quy. Không sửa rộng các phân hệ đó trong đợt này.
- `graphify update .` không chạy được vì máy chưa có lệnh `graphify`.

Lệnh kiểm thử riêng (từ `Source/Backend`, PowerShell):

```powershell
$env:CONTRACK_MASTER_TEST_CONNECTION='Server=localhost;Database=ContractManagement_Master;Trusted_Connection=True;TrustServerCertificate=True'
dotnet test ContractManagement.Tests/ContractManagement.Tests.csproj --filter FullyQualifiedName~InternalApprovalTests
```

Chưa triển khai số hợp đồng theo năm, cảnh báo nội dung nâng cao và menu Định nghĩa/Phụ lục; đây là các đợt tiếp theo.
