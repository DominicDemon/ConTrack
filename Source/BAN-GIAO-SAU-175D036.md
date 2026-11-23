# Bàn giao thay đổi sau commit `175d036`

Nhánh `feature-hoang` · Mốc đối chiếu là commit sửa mẫu bằng trình soạn Word `175d036`.

## Các commit thay đổi mã nguồn

| Commit | Thay đổi |
| --- | --- |
| `e3a77af` | Hành chính có nút **Tạo hợp đồng nháp** trong **Tiến độ hợp đồng** và có thể mở mẫu Word khi tạo nháp. |
| `4783c63` | Luồng Hành chính gửi thẳng, lưu nguồn tạo, bộ lọc **Cần tôi xử lý**, bảng quy trình ký và gỡ chức năng rà soát điều khoản. |

Team pull `feature-hoang` để nhận cả hai thay đổi. Có thể đối chiếu bằng `git log --oneline 175d036..feature-hoang`.

## Luồng hợp đồng

| Người tạo | Trước khi gửi khách | Sau khi khách đồng ý |
| --- | --- | --- |
| Sale | Sale lưu nháp → trình Hành chính duyệt → Sale gửi khách xem. | Hành chính duyệt bản chốt → chuyển sang ký. |
| Hành chính | Hành chính lưu nháp đủ thông tin → người phụ trách bấm **Gửi khách xem**. | Hệ thống chuyển thẳng sang chờ ký. |

- Khách cá nhân ký điện tử sau khi Sếp ký phía công ty. Khách doanh nghiệp đi theo luồng ký giấy, đóng dấu và Hành chính xác nhận bản scan.
- Backend tự ghi nguồn tạo `officeOrigin` khi lưu lần đầu và giữ nguồn qua các lần sửa hoặc tạo phiên bản. Quyền hiện tại quyết định ai được thao tác. Khi thiếu nguồn trong snapshot, hệ thống đọc audit tạo ban đầu. Nếu cả hai đều thiếu thì áp dụng luồng duyệt. Trạng thái xử lý cũ vẫn tra cứu theo lịch sử của hồ sơ.
- Điều kiện gửi vẫn gồm kiểm tra dữ liệu bắt buộc, thời hạn, phiên bản và trạng thái. Hành chính tạo nháp vẫn phải bấm **Gửi khách xem** sau khi lưu.

## Giao diện và trình soạn mẫu

- **Tiến độ hợp đồng** nhận diện cùng nguồn tạo với màn chi tiết. Khi nháp Hành chính đủ điều kiện gửi, hồ sơ vào **Chờ Hành chính gửi** và xuất hiện trong **Cần tôi xử lý** của người phụ trách. Hồ sơ khách yêu cầu sửa cũng xuất hiện theo lượt xử lý.
- **Quy trình ký hợp đồng** nằm ngay dưới tiêu đề màn soạn của cả hai vai trò, kể cả nháp mới chưa lưu. Khi đủ điều kiện gửi, bước **Gửi khách xem** được tô sáng. Sau khi gửi, chỉ dẫn chuyển thành **Đang chờ khách xem và phản hồi**.
- Đã gỡ **Rà soát điều khoản**, ô ghi chú, cột rà soát và bộ lọc cảnh báo tương ứng. API rà soát đã được gỡ, trình soạn mẫu Word chỉ cần xem và xác nhận PDF trước khi lưu hoặc kích hoạt. Dữ liệu rà soát cũ vẫn nằm trong DB. Đợt này dùng schema hiện tại.

## Checklist test cho team

Dùng [cổng nội bộ 3001](http://localhost:3001/workspace/contracts/drafts), [cổng khách 3000](http://localhost:3000/portal/login) và API 5005. Dùng tài khoản test của từng vai trò, email và OTP giả lập.

| Ca test | Kết quả cần thấy |
| --- | --- |
| Hành chính tạo nháp đủ thông tin, có thể chọn mẫu Word | Thấy **Gửi khách xem** ngay sau khi lưu. Bộ lọc **Cần tôi xử lý** thấy nháp ở **Chờ Hành chính gửi**. |
| Sale tạo nháp và thử gửi trước khi duyệt | Bị chặn. Sau khi Hành chính duyệt, bước **Gửi khách xem** được tô sáng. |
| Khách đồng ý hợp đồng do Hành chính tạo | Hồ sơ chuyển chờ ký. Thử bằng khách cá nhân và doanh nghiệp để xem đúng nhánh ký. |
| Khách yêu cầu sửa hợp đồng do Hành chính tạo | Hồ sơ trở lại **Cần tôi xử lý** của người phụ trách. Phiên bản mới giữ nguồn Hành chính. |
| Đổi quyền người tạo sau khi tạo nháp | Nguồn hồ sơ giữ nguyên. API chặn người thao tác thiếu quyền. |
| Còn thiếu dữ liệu hoặc sửa chưa lưu | Bảng chỉ dẫn hoàn thiện nháp. Khi đã gửi, bảng báo chờ khách phản hồi. |
| Tải và sửa mẫu Word | Xem PDF, xác nhận bố cục, lưu rồi mở lại. Form đã bỏ bước rà soát điều khoản. |

**Đã kiểm tra local:** backend build, TypeScript và 8 test nguồn tạo/tiến độ đều đạt. Đã gửi thử với tài khoản Hành chính, xác nhận khách đồng ý thì sang chờ ký, Sale chưa duyệt bị chặn, và hồ sơ có nguồn trong audit xuất hiện đúng bộ lọc. Đã thử tải, xem PDF, kích hoạt và sửa mẫu Word sau khi gỡ rà soát.

**Còn cần kiểm tra:** trạng thái tô sáng trực tiếp trên trình duyệt và ký hoàn tất hai bên theo luồng mới.
