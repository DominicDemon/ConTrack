# Triển khai quản lý gói dịch vụ

## Thay đổi
- System Admin dùng /api/admin/plans, giới hạn thời hạn trả phí 1–24 tháng.
- durationMonths dùng hiển thị; durationDays đã xác nhận dùng tính EndAt.
- Giá mới tạo phiên bản, giữ thời hạn và lịch sử. Revision ngăn ghi đè khi sửa đồng thời.
- PlanAudits lưu thao tác quản trị cùng transaction với dữ liệu.
- Snapshot quyền lợi được lưu tại thời điểm checkout. Hiện codebase chưa có bộ kiểm tra quota tập trung; khi triển khai quota phải đọc snapshot trên SUBSCRIPTION.
- Thuê bao hoạt động đếm tenant không trùng, ACTIVE và còn hạn, qua mọi phiên bản giá.

## Migration
Migration: 20261028062558_AdminPlanManagement.
Script idempotent: Artifacts/Sql/admin-plan-management-migrations.sql.

Thực hiện trong cửa sổ bảo trì, dừng nhận checkout mới và sao lưu Central DB trước khi cập nhật. Chạy từ Source/Backend với cấu hình CentralDatabase đúng môi trường:

    dotnet ef database update --project ContractManagement.Infrastructure --context CentralDbContext

Sau đó khởi động lại API bằng binary mới và triển khai hai frontend cùng phiên bản. Không phục vụ frontend mới với backend cũ vì API cũ chưa trả durationMonths.

Migration giữ ID PLAN_VERSION, SUBSCRIPTION và hóa đơn; sao chép cấu hình gói khi tách từng thời hạn. Backfill snapshot dùng quyền lợi hiện có vì dữ liệu lịch sử quyền lợi chưa tồn tại.

Chỉ ánh xạ thời hạn cũ được xác định rõ: 90 -> 3, 180 -> 6, 365 -> 12. Nếu có thời hạn trả phí khác, migration dừng với thông báo Unmapped existing paid duration. Cần bổ sung ánh xạ được xác nhận trong migration, không suy ngược bằng phép chia 30. Kiểm tra mã gói trùng trước khi thêm unique index.

SQL script bao gồm toàn bộ lịch sử migration dưới guard __EFMigrationsHistory. Kiểm tra migration còn thiếu của DB đích trước khi chạy.

## Kiểm tra
- Build Release BE thành công.
- 43 test thuộc nhóm Subscription (bao gồm 5 test AdminPlan) đạt. Các test mới dùng EF InMemory, không xác nhận transaction/locking trên SQL Server.
- TypeScript và lint cho phần FE thay đổi đạt.
- Chưa áp dụng hoặc kiểm thử migration trên Central DB thực tế; cần chạy thử trên bản sao DB trước rollout.
- Giao diện audit hiển thị 200 bản ghi gần nhất; bảng gói tìm kiếm, lọc và phân trang tại client.
