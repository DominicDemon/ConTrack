# Kiến trúc tóm tắt

- Một API ASP.NET Core (monolith chia module) phục vụ mọi doanh nghiệp.
- Master DB: khách hàng SaaS, gói, đơn hàng, hóa đơn, tenant, `tenant_databases`.
- Mỗi tenant một database riêng (BR-21); API chọn connection theo tenant của request.
- Worker chạy job nền: cấp tenant (CREATE_DATABASE → RUN_MIGRATION → CREATE_ADMIN), OCR, nhắc hạn, gửi email/SMS.
- File lưu ở object storage (MinIO khi dev), kèm SHA-256 cho tài liệu đã ký.
- Dịch vụ ngoài: Google (OAuth, email), SMS gateway (OTP ký), OCR service, cổng thanh toán (webhook).
