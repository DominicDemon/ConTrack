# Hướng dẫn test tạo hợp đồng ConTrack

Mục tiêu là tạo một hợp đồng từ mẫu Word có sẵn và kiểm tra luồng **Sale → Hành chính duyệt → Sale gửi khách → khách đồng ý → Hành chính duyệt bản chốt → Sếp ký** trên môi trường test.

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
| 7 | Sale | Trước khi trình, kiểm tra khu gửi khách. Sau đó bấm **Trình Hành chính**. | Chưa có nút **Gửi khách xem**; thay vào đó là dòng "Chưa trình Hành chính" rồi "Đang chờ Hành chính duyệt". |
| 8 | Hành chính, Sale | Hành chính trả lại kèm lý do. Kiểm tra lịch sử, chỉnh nội dung thử rồi dùng Sale trình lại. | Lý do được lưu. Hợp đồng quay lại Hành chính để kiểm tra. |
| 9 | Hành chính, Sếp | Hành chính bấm **Duyệt cho gửi khách**. Mở cùng hợp đồng bằng tài khoản Sếp. | Hồ sơ chuyển sang **Chờ Sale gửi**, Sale thấy nút **Gửi khách xem**. Sếp không có nút duyệt (Sếp chỉ ký ở bước sau). |
| 10 | Sale | Đối chiếu PDF và DOCX với phiên bản đã duyệt, rồi chọn **Gửi khách xem** trên môi trường gửi giả lập. | Hồ sơ chuyển sang **Chờ khách phản hồi**. Bản gửi khớp phiên bản đã duyệt. |
| 11 | Cả ba | Vào **Hợp đồng nháp** sau mỗi bước. Lọc theo bước, **Cần tôi xử lý** và **Có cảnh báo**. So sánh thời điểm vào bước trước và sau khi thêm nhận xét. | Bước, bộ phận và người phụ trách khớp hồ sơ. Nhận xét không đặt lại thời gian chờ. |

Mẫu Word nhập tay giữ chữ đã lưu trong bản Word. Kiểm tra số tiền và phương thức trên hồ sơ nháp trước. Nếu muốn kiểm tra dữ liệu này tự điền vào bản in, dùng mẫu đã bố trí các trường tương ứng.

Để kiểm tra số thứ tự trong năm, lưu thêm một nháp trong cùng năm và đối chiếu phần số cuối. Việc kiểm tra năm mới cần chạy trên DB thử nghiệm bằng hai năm cấp số thử. Kết quả mong đợi là cấp `1, 2` ở năm đầu và `1` ở năm tiếp theo.

Khi báo lỗi, ghi **số hợp đồng, vai trò, bước trong bảng, kết quả thực tế** và đính kèm ảnh màn hình nếu có.

## Test khách tiềm năng (chưa có tài khoản)

Khách tiềm năng là khách chưa từng được cấp mật khẩu (chưa có tài khoản Cổng khách hàng). Hệ thống tự nhận biết, không cần đánh dấu.

1. Tạo khách mới có SĐT và email, tạo nháp, Trình Hành chính, Hành chính duyệt.
2. Sale bấm `Gửi khách xem`: hộp thoại ghi "khách tiềm năng", có ô hạn link. Khách nhận email có link riêng.
3. Khách mở link, nhập SĐT và mã OTP (SMS). Khách xem, góp ý, sửa/xóa góp ý, `Đồng ý` hoặc `Gửi yêu cầu sửa` ngay trên link.
4. Sale và Hành chính thấy chuông báo "Khách tiềm năng … góp ý hợp đồng" trong vài giây; bấm vào mở hợp đồng.
5. Sau khi Hành chính duyệt bản chốt và Sếp ký, khách ký luôn trên link đó (xác minh OTP ký). Link hết hạn thì Sale bấm `Gửi lại link` (dùng được cả sau khi khách đã đồng ý).
6. Khi khách ký xong, chuông báo "Hai bên đã ký · tạo tài khoản cho khách". Mở hợp đồng, bấm `Tạo tài khoản khách`.
7. Khách nhận email gồm tên đăng nhập (SĐT) và mật khẩu tạm; đăng nhập Cổng khách hàng thì bị yêu cầu đổi mật khẩu ngay.
8. Hợp đồng sau gửi cho khách này: không còn link, khách nhận email mời đăng nhập Cổng khách hàng. `Gửi lại link` bị chặn; `Cấp lại mật khẩu tạm` gửi mật khẩu mới qua email nếu khách chưa đổi mật khẩu.

Khách doanh nghiệp ký giấy: sau khi bản ký giấy được xác nhận, nút `Tạo tài khoản khách` hiện trên hợp đồng như trên.
