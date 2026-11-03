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
Không có gửi khách, OTP, phê duyệt, thanh toán, ký hoặc xuất DOCX/PDF trong đợt này.

## Dữ liệu và kỹ thuật

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
dotnet test ContractManagement.Tests --filter 'FullyQualifiedName~InternalDraftTests|FullyQualifiedName~MasterDatabaseTests'
```

Live tests tạo và dọn dữ liệu thử; gồm lưu/mở lại, snapshot khách, quyền sở hữu,
xung đột rowversion, tính tiền và rollback lỗi SQL sau khi đã tạo document/version.
Không có biến kết nối thì các live tests được đánh dấu skip.
Frontend: `npm run typecheck` và `npm run build`.
