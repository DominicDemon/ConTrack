# Luồng nháp và khách xem — snapshot v2

Web nhân viên: SystemAdmin, cổng 3001. Portal khách: Frontend, cổng 3000.
API chạy ApplicationMode=Internal, sử dụng MasterDatabase. Không chạy script tạo lại DB.

## Dữ liệu và tương thích

Không thêm cột/bảng trong đợt này. Dùng CUSTOMER_BUSINESS, CONTRACT_PAYMENT_MILESTONE và các cột chuyển tiếp góp ý đã có trong MASTER. Yêu cầu đã áp dụng 001-internal-drafts.sql và 002-contract-review.sql của luồng trước.

Snapshot `__draft.v1` giữ nguyên tên khóa để đọc tương thích; thuộc tính `formatVersion=2` phân biệt định dạng nội dung mới. Snapshot chứa `partyType`, `customerBusinessId`, thông tin bên giao dịch trong `fields`, `paymentMode`, `milestones`, mẫu và các giá trị đã chụp. ID nguồn không dùng để tự cập nhật thông tin khi đọc lại.

Phiên bản đã gửi trước nâng cấp giữ nguyên snapshot/hash và cách hiển thị cũ. Nháp cũ phải lưu lại sau khi bổ sung loại bên giao dịch và lịch thu trước khi gửi. Phiên bản mới của hồ sơ cũ cũng phải hoàn thiện những mục này.

## Quy ước thanh toán

- paymentMode: PERCENT hoặc AMOUNT, tính trên tổng sau VAT.
- dueanchor: 1 = ngày ký đủ; 2 = ngày bàn giao; 3 = ngày cụ thể.
- daycountmode: 1 = ngày lịch. Chưa hỗ trợ ngày làm việc.
- paymentstatus: 0 = chưa thu. Không tự xác nhận thu tiền hoặc tạo hóa đơn.
- Mốc sự kiện chưa xảy ra: anchordate/duedate để trống. Ngày cụ thể lưu ở duedate.
- Nháp lưu đầy đủ hàng chưa hoàn thiện trong snapshot. Chỉ ghi hàng milestone có liên kết điều khoản hợp lệ vào bảng quan hệ; gửi khách yêu cầu mọi hàng hợp lệ và tổng đủ 100% nghĩa vụ.
- Khi tạo phiên bản mới, ánh xạ lại term ID; xóa liên kết hóa đơn, paidat, paidby và trạng thái thu. Không thay đổi các phiên bản cũ.

## Mẫu và trường

Mẫu upload được chuyển sang văn bản hệ thống; không giữ bố cục gốc hay OCR. Placeholder dùng `{{KEY}}`. Trường lõi TOTAL_AMOUNT, SUBTOTAL, DISCOUNT, VAT, PAYMENT_SCHEDULE, CONTRACT_NAME, EFFECTIVE_DATE, EXPIRE_DATE lấy từ dữ liệu cấu trúc. Không khai báo lại chúng thành trường tùy ý.

Trường mở rộng hỗ trợ TEXT/NUMBER/DATE/SELECT. NUMBER dùng dấu chấm thập phân trong JSON, DATE dùng yyyy-MM-dd, SELECT khớp lựa chọn đã định nghĩa. extra_options lưu options/min/max. Mỗi lần nhập mẫu tạo mẫu mới; các bản nháp giữ snapshot mẫu.

## Kiểm thử

Đặt CONTRACK_MASTER_TEST_CONNECTION tới MASTER local và CONTRACK_INTERNAL_TEMPLATE_ROOT tới Backend/private-storage/internal-templates. Chạy các lớp DraftCompletionTests, InternalDraftTests, InternalTemplateImportTests, ContractReviewPolicyTests và ContractReviewTests.

Test DB tạo dữ liệu có tên TEST và dọn theo ID ở finally. Dừng API local trong khi chạy test gửi review để worker thật không nhận hàng đợi test. Test dùng ISmsSender giả, không gửi SMS ra ngoài. Khi test xong khởi động lại API với cấu hình local hiện có.

Không triển khai duyệt hành chính, OTP theo link, chữ ký, DOCX/PDF xuất bản hoặc thu tiền trong đợt này.
