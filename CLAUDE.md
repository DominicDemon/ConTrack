# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

ConTrack is a multi-tenant contract-management SaaS (create, approve, e-sign, OCR, search, track contracts). Docs, comments and commit conventions are in Vietnamese; requirement codes (UC x.y, BR-xx, SRS section numbers) refer to the SRS and are used in comments, test names and branch names. The repo is at an early "Sprint 0" stage: most modules are scaffolding (`.gitkeep` folders, empty `AddApplication()`, DbContexts with no entities yet). `README.md` lists the remaining Sprint 0 tasks.

đây là SaaS quản lý hợp đồng đa tenant (ConTrack); mỗi tenant một database riêng, Master DB lưu tenant/gói/admin hệ thống; mọi truy vấn nghiệp vụ phải đi qua TenantDbContext của tenant hiện tại, không bao giờ truy vấn chéo tenant; kiến trúc modular monolith (Api / Application / Domain / Infrastructure / Worker); mỗi tính năng mới phải có unit test; trả lời tôi bằng tiếng Việt.

## Commands

Infrastructure (PostgreSQL 17, MinIO, Mailpit) — copy `.env.example` to `.env` first:

```bash
docker compose up -d                       # infra only
docker compose --profile app up -d --build # also run API + Web in containers
```

Backend (run from `backend/`; .NET SDK 10, solution file is `Contrack.slnx`):

```bash
dotnet build Contrack.slnx
dotnet test Contrack.slnx
dotnet test Contrack.slnx --filter "FullyQualifiedName~TenantAccessRuleTests"   # single test class/method
dotnet run --project src/Contrack.Api      # http://localhost:5080/swagger
```

Frontend (run from `frontend/`; Node 22; copy `.env.example` to `.env.local` first):

```bash
npm install
npm run dev                                # http://localhost:5173, proxies /api to :5080
npm run build                              # tsc -b && vite build (this is also the type check)
npm test                                   # vitest run
npx vitest run src/lib/tenant.test.ts      # single test file
```

No linter is configured. CI (`.github/workflows/ci.yml`) runs exactly: restore/build/test for backend (Release) and `npm install`, `npm run build`, `npm test` for frontend.

## Architecture

**Backend** is a layered .NET solution: `Api` → `Application` + `Infrastructure` → `Domain` (Domain has no dependencies; Application defines interfaces such as `ITenantContext`; Infrastructure implements them). `Worker` is a separate host that reuses `AddInfrastructure`; it currently only runs a heartbeat and is meant to become the Hangfire job host (CREATE_DATABASE, RUN_MIGRATION, OCR, reminders).

**Database-per-tenant (BR-21)** is the central design decision and spans several files:
- `MasterDbContext` holds platform data (tenants, tenant_databases, plans, orders). It is the only context registered in `AddInfrastructure` today, using connection string `ConnectionStrings:MasterDb`.
- `TenantDbContext` is one database per customer company. Its connection string must come from the per-request `ITenantContext` (to be filled by a not-yet-written `TenantResolutionMiddleware`), so it is deliberately not registered yet.
- Both contexts auto-load entity configurations via `ApplyConfigurationsFromAssembly`, filtered **by namespace**: configs must live in a namespace ending `.Persistence.Master` or `.Persistence.Tenant`, otherwise they are silently not applied.
- Tenant identity: production uses the subdomain (`acme.contrack.vn`); on localhost the frontend sends an `X-Tenant` header from `VITE_DEV_TENANT` (see `frontend/src/lib/tenant.ts` and `frontend/src/api/http.ts`, which also attaches the Bearer token from `localStorage` key `contrack.accessToken`). Only `TenantStatus.Active` tenants are accessible (`TenantAccessRule`).
- `infra/postgres/*.sql` is mounted into the Postgres container's init dir and creates the `contrack_tenant_demo` database; it only runs on a fresh `pgdata` volume.

**API surface**: `Program.cs` exposes OpenAPI at `/openapi/v1.json` (dev only) and Swagger UI at `/swagger`; the frontend API client is intended to be generated from that JSON. CORS origins come from `Cors:Origins`. `public partial class Program` exists so `WebApplicationFactory<Program>` works in integration tests; `/api/health` returns 200 even when Postgres is down (`masterDb: "unreachable"`), so integration tests pass without Docker.

**Frontend** is React 18 + TypeScript + Vite + Ant Design + TanStack Query + react-hook-form/zod. Source is organised as `app/` (router, layout, error pages), `api/` (axios), `features/<module>/` (one folder per SRS module), `lib/`. Vitest runs in the `node` environment (no DOM), so component tests need a config change first.

## Project conventions (from CONTRIBUTING.md)

- Branches `feature/<code>-<desc>` / `fix/<code>-<desc>` where code is a UC, screen or BR (e.g. `feature/uc6.1-create-contract`); `main` is never pushed to directly.
- Commit format: `<type>(<module>): <short description>` with a `Refs: UC 6.1, III.8.7` footer. Types: feat, fix, refactor, test, docs, chore. Modules: contracts, approvals, esign, documents, partners, support, subscriptions, identity, infra, web.
- New business rule → unit test whose name states the rule (see `TenantAccessRuleTests`). API change → update Swagger and regenerate the frontend client in the same PR.
- Enforce permissions in the API on every endpoint, not just by hiding UI.
- External services (OCR, SMS, email, payment) go behind an interface with a fake implementation for local/demo.
- Never store passwords, OTPs or DB passwords in plain text (BR-18). Files already sent for signing are immutable; changes create a new version (BR-12).
