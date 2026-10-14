-- Chạy một lần khi container PostgreSQL được tạo lần đầu.
-- contrack_master được tạo sẵn bởi POSTGRES_DB.
-- Database của tenant mẫu, dùng cho dev trước khi có job cấp tenant tự động (UC 2.3).
CREATE DATABASE contrack_tenant_demo;
