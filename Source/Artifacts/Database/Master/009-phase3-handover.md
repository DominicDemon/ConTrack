# Đợt 3 — Tiến độ và rà soát điều khoản

Triển khai local trên `feature-hoang`, ngày 17/11/2026. Không push/deploy.

## Cách thử

1. Mở `http://localhost:3001`, đăng nhập tài khoản Hành chính.
2. **Mẫu hợp đồng** → mở **Điều khoản cần rà soát** của một mẫu, chọn các mục rồi lưu. Khi tải mẫu Word mới, xác nhận lựa chọn và cập nhật bản xem trước trước khi kích hoạt. Có thể chọn không áp dụng mục nào.
3. Sale tạo nháp từ mẫu. Danh sách rà soát được chụp riêng cho phiên bản này.
4. Mở hợp đồng → **Rà soát điều khoản**. Hành chính có thể đi tới đoạn Word, ghi lý do và đánh dấu **Đã rà soát** hoặc **Không áp dụng**. Sale dùng nhận xét nội bộ.
5. Thay đổi cấu hình mẫu không tự thay cấu hình của hợp đồng. Khi nháp còn sửa được, Hành chính chọn **Áp dụng danh sách hiện tại của mẫu** nếu cần. Thao tác này xóa xác nhận rà soát cũ.
6. Vào **Hợp đồng nháp** để xem tiến độ, lọc bước, Sale, số ngày chờ, cảnh báo và **Cần tôi xử lý**. Danh sách tối đa 25 dòng/trang, ưu tiên chờ lâu, cập nhật mỗi 60 giây khi trang đang hiển thị.

Fixture giao diện: hợp đồng `HD-20261117-000005`, id `52`, tên `TEST3 — Rà soát và tiến độ`, đang chờ Hành chính và còn cảnh báo mục chấm dứt. Tài khoản test nằm trong file local đã có `Source/.local/approval-accounts.json` (không đưa mật khẩu vào báo cáo/Git).

## Quy tắc

- Chỉ Hành chính cấu hình checklist/đánh dấu rà soát; quyền đọc tiến độ giống quyền đọc hợp đồng, bao gồm số tổng hợp.
- Cảnh báo mang tính hỗ trợ. Không thêm điều kiện chặn duyệt/gửi; giữ nguyên quy trình duyệt hai cấp.
- Đọc đoạn văn và ô bảng trong thân DOCX đã lưu, kiểm tra SHA-256; không dùng bản HTML hay nội dung thử để suy luận.
- Nhận diện theo cụm từ tiếng Việt và mức nội dung tối thiểu. Đây không phải kiểm định pháp lý; đoạn quá ngắn, nội dung chỉ có trong ảnh hoặc cách diễn đạt khác có thể cần Hành chính xác nhận thủ công.
- Xác nhận ràng buộc với hash snapshot; sửa nội dung hoặc thay Word làm xác nhận cũ hết hiệu lực. Checklist nằm ở bảng riêng nên đánh dấu rà soát không sửa nội dung đã duyệt.
- Thời gian chờ dựa trên sự kiện chuyển bước, không dựa trên lần sửa/nhận xét gần nhất. Hồ sơ cũ không đủ lịch sử hiện **Chưa có dữ liệu**.
- Tệp nguồn lỗi không làm hỏng toàn bộ danh sách; hồ sơ đó hiện thông báo chưa kiểm tra được tài liệu.

## Database

`009-contract-checklist.sql` thêm `CONTRACT_CHECKLIST`, rowversion và hai unique filtered indexes. Chạy sau các migration 001–008. Không reset dữ liệu.

Đã chạy lặp trên DB thử nghiệm và DB local `ContractManagement_Master`. Đã đối chiếu toàn bộ EF với DB thử nghiệm đầy đủ `ConTrack_Phase3_20261117` và DB chính: 65 bảng, 741 cột. DB thử nghiệm cũ `ConTrack_Stabilization_20261117` chỉ chứa vài bảng nên không phù hợp chạy kiểm thử toàn bộ schema; dữ liệu cũ vẫn được giữ.

## Kiểm thử

- Bộ backend mặc định: 1.349 đạt, 0 lỗi, 20 bỏ qua do các cờ/cấu hình kiểm thử tích hợp chưa bật.
- Suite checklist + Master DB cấu hình riêng: 8/8 đạt, gồm truy cập ngoài phạm vi, cạnh tranh cập nhật (200/409), phân trang, hash thay đổi, cấu hình riêng theo phiên bản, cảnh báo tệp thiếu và schema thật.
- Suite Word mẫu + duyệt + Master DB: 6/6 đạt, gồm năm tài liệu, Word/PDF, trả lại/trình lại, chặn vượt cấp, duyệt đồng thời, gửi lặp, đúng phiên bản và nhận xét không đổi thời gian chờ.
- Năm ca vốn bị skip trong bộ mặc định đã chạy riêng qua hai suite trên. Các ca tích hợp tùy chọn còn lại không được tính là đạt: legacy tenant/account-security/account-recovery/handover/appendix/LibreOffice bilingual và các fixture/bootstrap không thuộc đợt này.
- TypeScript Frontend và SystemAdmin đạt. Build backend đạt.
- API qua proxy 3001: đăng nhập ba vai trò, upload/preview/activate Word có checklist, chặn Sale/Sếp quản lý checklist, nháp lưu cấu hình và hiện đúng tiến độ.
- Giao diện ba vai trò: lưu cấu hình, cấu hình cũ không tự thay, áp dụng cấu hình mới, cảnh báo thiếu, lưu người/lý do/thời điểm rà soát, đi đúng đoạn Word, Sale nhận xét và trình duyệt, Sếp không duyệt trước Hành chính; bộ lọc **Cần tôi xử lý** hiện đúng hồ sơ chờ Hành chính.

Log/TRX và ảnh nằm trong `Source/.local/phase3-*.log`, `Source/Backend/ContractManagement.Tests/TestResults/phase3-final.trx`, `Source/.local/phase3-progress.png`. Các lỗi thử nghiệm ban đầu đã xử lý gồm SET options khi tạo filtered index, fixture trùng khóa và tên bảng khóa cập nhật; không bỏ/nới test để đạt.

## Chạy lại từ Source (PowerShell)

```powershell
dotnet test Backend/ContractManagement.Tests --no-restore
$env:CONTRACK_MASTER_TEST_CONNECTION='Server=.;Database=ConTrack_Phase3_20261117;Integrated Security=True;TrustServerCertificate=True'
dotnet test Backend/ContractManagement.Tests --no-restore --filter 'FullyQualifiedName~ContractChecklist|FullyQualifiedName~MasterDatabaseTests'
$env:CONTRACK_MASTER_TEST_CONNECTION='Server=.;Database=ContractManagement_Master;Integrated Security=True;TrustServerCertificate=True'
$env:CONTRACK_WORD_SAMPLE_ROOT='C:\Users\ADMIN\Desktop\SEP490'
dotnet test Backend/ContractManagement.Tests --no-restore --filter 'FullyQualifiedName~InternalApprovalTests.Enforces|FullyQualifiedName~InternalWordSampleTests|FullyQualifiedName~MasterDatabaseTests'
```

Chạy `npx tsc --noEmit` riêng trong Frontend và SystemAdmin. API local ở 5005, chế độ `ApplicationMode=Internal`; giao diện nội bộ 3001, khách hàng 3000. Trong kiểm thử, OTP/SMS giả lập và SMTP trỏ loopback để không gửi thật.

Không cập nhật được knowledge graph vì lệnh `graphify` chưa được cài trong môi trường.
