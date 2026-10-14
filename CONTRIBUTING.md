# Quy ước làm việc

## Nhánh

- `main`: luôn build và chạy được. Không push thẳng.
- Mỗi task một nhánh: `feature/<mã>-<mô-tả-ngắn>`, `fix/<mã>-<mô-tả>`.
  Mã lấy theo SRS: use case (`uc6.1`), màn hình (`iii-8.7`) hoặc business rule (`br-11`).
  Ví dụ: `feature/uc6.1-create-contract`, `fix/br-11-otp-lockout`.

## Commit

```
<loại>(<module>): <mô tả ngắn>

Refs: UC 6.1, III.8.7
```

`loại`: feat, fix, refactor, test, docs, chore. `module`: contracts, approvals, esign, documents, partners, support, subscriptions, identity, infra, web.

## Pull Request

- Ít nhất 1 người review, CI xanh mới được merge.
- API thay đổi → cập nhật Swagger và sinh lại client frontend trong cùng PR.
- Business rule mới → kèm unit test, tên test nói rõ rule.
- Ghi rõ UC / màn hình / BR liên quan để truy vết khi viết Report 5.

## Code

- Backend: kiểm tra quyền ở API cho mọi endpoint, không chỉ ẩn nút trên giao diện.
- Dịch vụ ngoài (OCR, SMS, email, thanh toán) đặt sau interface, có bản giả lập để chạy local và demo.
- Không lưu mật khẩu, OTP hay mật khẩu database dạng rõ (BR-18).
- File đã gửi ký không sửa trực tiếp; thay đổi tạo phiên bản mới (BR-12).
