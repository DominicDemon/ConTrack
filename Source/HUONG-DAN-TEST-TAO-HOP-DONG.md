# Hướng dẫn test tạo hợp đồng ConTrack

Mục tiêu là tạo một hợp đồng từ mẫu Word có sẵn và kiểm tra luồng **Sale → Hành chính → Sếp → Sale gửi khách** trên môi trường test.

## Chuẩn bị

- Mở cổng nội bộ do team cung cấp. Nếu tự chạy trên máy, dùng [localhost:3001](http://localhost:3001). Khi chạy trên máy chung, dùng địa chỉ test do người phụ trách cung cấp.
- Dùng ba tài khoản test thuộc vai trò **Sale**, **Hành chính** và **Sếp**. Có thể dùng ba hồ sơ trình duyệt riêng để đổi vai trò dễ hơn.
- Chuẩn bị một khách hàng thử, một mẫu Word loại **Hợp đồng** đã kích hoạt và một ảnh PNG/JPEG thử. Nếu thiếu mẫu, đăng nhập Hành chính, vào **Mẫu hợp đồng → Tải mẫu Word**, chọn checklist, cập nhật bản xem trước, xác nhận và kích hoạt.
- Nếu test bước gửi khách, dùng khách hàng thử và môi trường đã cấu hình gửi giả lập.

## Test tạo mẫu Word bằng tài khoản Hành chính

Mở **Mẫu hợp đồng → Tải mẫu Word**. Dùng file Word thử có tiếng Việt, bảng và ảnh sẵn để dễ đối chiếu. Đặt tên mẫu riêng, ví dụ `TEST – Mẫu Word – [tên người test]`.

| # | Thao tác | Kết quả mong đợi |
|---|---|---|
| 1 | Chọn file `.doc` hoặc `.docx` dưới 10 MB, nhập tên và chọn loại **Hợp đồng**. | Màn hình hiển thị vùng sửa nội dung và bản in xem trước. Đối chiếu tiếng Việt, bảng, ảnh và bố cục với file gốc. |
| 2 | Sửa một câu dễ nhận biết. Chọn vị trí rồi dùng **Thêm mục** để chèn **Định nghĩa** và **Phụ lục**. | Hai mục có tiêu đề chỉnh được và nằm sau vị trí đã chọn. |
| 3 | Chèn bảng 2×2 và điền bốn ô. Chèn một ảnh PNG/JPEG, chỉnh chiều rộng và căn lề. Bấm **Cập nhật bản xem trước**. | Bản in hiện câu đã sửa, hai mục mới, bảng và ảnh đúng vị trí. |
| 4 | Mở **Điều khoản cần rà soát**, chọn các mục như **Giá trị và thanh toán**, **Bảo hành**, rồi xác nhận lựa chọn. | Có thể chọn nhiều mục hoặc xác nhận không áp dụng mục nào. |
| 5 | Sửa thêm một câu sau khi xem trước. Sau đó cập nhật bản xem trước, kiểm tra bản in, đánh dấu xác nhận và bấm **Kích hoạt mẫu**. | Bản xem trước cũ không dùng để kích hoạt được. Sau khi kích hoạt, mẫu có tên đã nhập và checklist lưu đúng lựa chọn. |
| 6 | Đăng nhập Sale, vào **Tạo hợp đồng nháp**, chọn mẫu vừa kích hoạt rồi bấm **Xem trước Word**. | Sale thấy mẫu trong bộ chọn hợp đồng và xem được nội dung đã lưu. Sale không có thao tác tải hoặc kích hoạt mẫu. |
| 7 | Thử một file sai định dạng đổi đuôi thành `.docx` và một file lớn hơn 10 MB. | Hệ thống báo lỗi và không tạo mẫu. |
| 8 | Nếu có tài liệu thử thuộc loại khác, lặp lại với **Phụ lục**, **Biên bản bàn giao**, **Biên lai** hoặc **Đề nghị thanh toán**. | Chọn được đúng loại, xem trước và kích hoạt. Các loại này không xuất hiện trong bộ chọn khi Sale tạo hợp đồng. |

Nếu gặp lỗi, ghi tên mẫu, loại mẫu, định dạng file, bước đang test và thông báo trên màn hình.

## Test tạo hợp đồng từ mẫu đã kích hoạt

| # | Vai trò | Thao tác | Kết quả mong đợi |
|---|---|---|---|
| 1 | Sale | Vào **Hợp đồng nháp → Tạo hợp đồng nháp**. Chọn mẫu đang hoạt động, chọn khách hàng thử và đặt tên để dễ tìm, ví dụ `TEST – Tạo hợp đồng – [tên người test]`. | Sale dùng được mẫu có sẵn. Giao diện Sale không có thao tác tải hay kích hoạt mẫu. |
| 2 | Sale | Thêm một hạng mục số lượng **2**, đơn giá **500.000 ₫**, giảm giá **0**, VAT **10%**. Chọn **Chuyển khoản** và phân bổ một đợt thanh toán **100%**. Điền các mục bắt buộc còn trống. | Tạm tính **1.000.000 ₫**, VAT **100.000 ₫**, tổng cộng và lịch thanh toán đều **1.100.000 ₫**. |
| 3 | Sale | Bấm **Lưu nháp**, quay về danh sách rồi mở lại đúng hợp đồng. Ghi lại số hợp đồng. | Số có dạng `HD-yyyyMMdd-xxxxxx`. Khách hàng, phương thức và lịch thanh toán vẫn còn sau khi mở lại. |
| 4 | Sale | Trong **Duyệt nội bộ**, gửi nhận xét “Đề nghị bổ sung định nghĩa và phụ lục thử”. | Nhận xét hiện trong lịch sử. Hành chính đọc được nội dung, người gửi và thời điểm. |
| 5 | Hành chính | Khi hợp đồng còn là nháp, mở **Sửa nội dung Word của hợp đồng**. Chọn đoạn làm mốc, dùng **Thêm mục** để chèn **Định nghĩa** và **Phụ lục**, rồi chèn bảng 2×2 và một ảnh. Cập nhật bản xem trước, kiểm tra bố cục, lưu và mở lại. | Các nội dung mới nằm đúng chỗ trong PDF xem trước và còn khi mở lại DOCX. Thay đổi nằm trên hợp đồng thử đang sửa. |
| 6 | Hành chính | Mở **Rà soát điều khoản**. Để kiểm tra cảnh báo, dùng mẫu thử có checklist **Bảo hành** và chỉ có tiêu đề bảo hành hoặc thiếu mục này. Có thể áp dụng checklist hiện tại của mẫu cho nháp còn sửa được. | Hiện cảnh báo **Chưa tìm thấy nội dung đầy đủ**. Hành chính có thể ghi lý do rồi chọn **Đã rà soát** hoặc **Không áp dụng**. |
| 7 | Sale, Sếp | Sale bấm **Trình Hành chính**. Mở cùng hợp đồng bằng tài khoản Sếp trước khi Hành chính duyệt. | Hợp đồng ở bước **Chờ Hành chính kiểm tra**. Sếp chưa thể duyệt và Sale chưa thể gửi khách. |
| 8 | Hành chính, Sale | Hành chính trả lại kèm lý do. Kiểm tra lịch sử, chỉnh nội dung thử rồi dùng Sale trình lại. | Lý do được lưu. Hợp đồng quay lại Hành chính để kiểm tra. |
| 9 | Hành chính, Sếp | Hành chính duyệt. Chuyển sang tài khoản Sếp và duyệt cùng phiên bản. | Sau lượt Hành chính, hồ sơ chuyển sang Sếp. Sau lượt Sếp, hồ sơ chuyển sang **Chờ Sale gửi**. |
| 10 | Sale | Đối chiếu PDF và DOCX với phiên bản đã duyệt, rồi chọn **Gửi khách xem** trên môi trường gửi giả lập. | Hồ sơ chuyển sang **Chờ khách phản hồi**. Bản gửi khớp phiên bản đã duyệt. |
| 11 | Cả ba | Vào **Hợp đồng nháp** sau mỗi bước. Lọc theo bước, **Cần tôi xử lý** và **Có cảnh báo**. So sánh thời điểm vào bước trước và sau khi thêm nhận xét. | Bước, bộ phận và người phụ trách khớp hồ sơ. Nhận xét không đặt lại thời gian chờ. |

Mẫu Word nhập tay giữ chữ đã lưu trong bản Word. Kiểm tra số tiền và phương thức trên hồ sơ nháp trước. Nếu muốn kiểm tra dữ liệu này tự điền vào bản in, dùng mẫu đã bố trí các trường tương ứng.

Để kiểm tra số thứ tự trong năm, lưu thêm một nháp trong cùng năm và đối chiếu phần số cuối. Việc kiểm tra năm mới cần chạy trên DB thử nghiệm bằng hai năm cấp số thử. Kết quả mong đợi là cấp `1, 2` ở năm đầu và `1` ở năm tiếp theo.

Khi báo lỗi, ghi **số hợp đồng, vai trò, bước trong bảng, kết quả thực tế** và đính kèm ảnh màn hình nếu có.
