# ConTrack – Contract Management SaaS

Nền tảng SaaS đa tenant giúp doanh nghiệp tạo, duyệt, ký điện tử, số hóa (OCR), tìm kiếm và theo dõi hợp đồng.
Yêu cầu chi tiết: Report 3 – SRS. Kế hoạch code: tài liệu "ConTrack – Kế hoạch bắt đầu code (.NET + React)".

| Thành phần | Công nghệ | Thư mục |
| --- | --- | --- |
| Backend API | ASP.NET Core, .NET 10, EF Core, PostgreSQL | `backend/` |
| Worker (job nền) | .NET Worker Service (Hangfire sau) | `backend/src/Contrack.Worker` |
| Frontend | React 18 + TypeScript + Vite, Ant Design, TanStack Query | `frontend/` |
| Hạ tầng dev | PostgreSQL 17, MinIO (lưu file), Mailpit (bắt email) | `docker-compose.yml` |

## Cần cài trên máy

- .NET SDK 10 (`dotnet --version` ≥ 10.0.100)
- Node.js 22 LTS
- Docker Desktop
- (tùy chọn) công cụ EF: `dotnet tool install --global dotnet-ef`

## Chạy lần đầu

```bash
# 1. Hạ tầng: PostgreSQL, MinIO, Mailpit
cp .env.example .env
docker compose up -d

# 2. Backend
cd backend
dotnet restore Contrack.slnx
dotnet build Contrack.slnx
dotnet test Contrack.slnx
dotnet run --project src/Contrack.Api        # http://localhost:5080/swagger

# 3. Frontend (terminal khác)
cd frontend
cp .env.example .env.local
npm install                                    # commit package-lock.json sau lần chạy đầu tiên
npm run dev                                    # http://localhost:5173
```

Mở http://localhost:5173: trang "Trạng thái môi trường" hiện **API = ok** và **Master DB = connected** là môi trường đã chạy đúng.

| Dịch vụ | Địa chỉ | Tài khoản dev |
| --- | --- | --- |
| API + Swagger | http://localhost:5080/swagger | – |
| Web | http://localhost:5173 | – |
| PostgreSQL | localhost:5432, DB `contrack_master`, `contrack_tenant_demo` | contrack / contrack_dev |
| MinIO console | http://localhost:9001 (bucket `contrack-documents`) | contrack / contrack_dev_secret |
| Mailpit | http://localhost:8025 | – |

Chạy cả API và Web trong Docker (không cần cài .NET/Node): `docker compose --profile app up -d --build`.

## Cấu trúc

```text
backend/
  Contrack.slnx
  src/
    Contrack.Api/             # endpoint, middleware, Program.cs
    Contrack.Application/     # use case theo module, interface (ITenantContext...)
    Contrack.Domain/          # entity, enum trạng thái, business rule thuần
    Contrack.Infrastructure/  # MasterDbContext, TenantDbContext, storage, email, SMS, OCR
    Contrack.Worker/          # job nền
  tests/
    Contrack.UnitTests/       # test business rule (BR-xx)
    Contrack.IntegrationTests/
frontend/src/
  app/        # router, layout, trang 403/404
  api/        # axios + client API (sẽ sinh từ /openapi/v1.json)
  features/   # auth, platform, saas-portal, organization, partners, contracts, documents, support, customer-portal
  lib/        # tiện ích (xác định tenant từ subdomain...)
infra/postgres/   # script khởi tạo database dev
```

## Multi-tenant khi chạy local

- Mỗi tenant một database riêng (BR-21). Bản dev có sẵn `contrack_tenant_demo`.
- Trên production tenant lấy từ subdomain (`acme.contrack.vn`). Trên localhost frontend gửi header `X-Tenant` lấy từ `VITE_DEV_TENANT`.

## Việc tiếp theo (Sprint 0)

- [x] Repo và môi trường
- [ ] Master DB: bảng tenant, tenant_databases, employees, roles + migration + seed Super Admin
- [ ] Tenant DB: users, roles, permissions, departments + tạo tenant mẫu
- [ ] Tenant resolver + ITenantContext
- [ ] Đăng nhập JWT, phân quyền theo Permission Matrix, audit log
- [ ] Frontend: client API sinh từ Swagger, lưu token, chặn route theo quyền

Quy ước nhánh, commit và Pull Request: xem [CONTRIBUTING.md](CONTRIBUTING.md).
