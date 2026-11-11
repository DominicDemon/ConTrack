# MASTER database integration

## Local database

- SQL Server: `localhost`
- Database: `ContractManagement_Master`
- Authentication: Windows integrated authentication
- Configuration key: `ConnectionStrings:MasterDatabase`
- Schema: 60 tables and 659 columns, without seed data or physical foreign keys.

The local connection is configured in the ignored API `appsettings.Development.json`.
For another machine, set that key locally or supply the environment variable
`ConnectionStrings__MasterDatabase`. The tracked `appsettings.json` leaves it empty.
No SQL password is needed for this local connection.

## Source files

`MasterDB-ConTrack.sql` is the supplied full schema for `ConTrackMaster`, including
customer review and SMS tables. It contains its own CREATE DATABASE / USE statements.
For the existing local `ContractManagement_Master` database, run `001`–`003` manually
to add that functionality while preserving drafts and accounts.
The application connection name remains `ConnectionStrings:MasterDatabase`.

`MasterDB-Optimize.original.sql` is the exact supplied SQL file.
`Master_Database_HuongDan.docx` is the supplied reference document. Its proposed
workflows and example statuses are guidance, not seeded database values.

`MasterDB-Optimize.sql` is the executable copy. Explicit primary-key constraint
names that originally equalled their table names are prefixed with `PK_`.
SQL Server rejects the original names because table and constraint names share
the schema object namespace. Table names, columns, defaults and types are unchanged.

## Initialize a new database

From `Source` in PowerShell:

```powershell
.\scripts\Initialize-MasterDatabase.ps1
```

The importer creates a separate database if absent, refuses a database containing
tables, executes the DDL in one transaction, and verifies the 60-table count.
It neither upgrades an old database nor copies legacy data. A failed import rolls
back the schema transaction; a newly created empty database may remain.

## Application integration

- Domain entities: `ContractManagement.Domain/Entities/Master`.
- SQL mapping: `ContractManagement.Infrastructure/Persistence/Master/MasterDbContext.cs`.
- Registration: `AddMasterDatabase(builder.Configuration)` in API `Program.cs`.
- Design-time factory: `MasterDbContextFactory`.
- Readiness: `GET /health/master` returns 200 with `Ready`, or 503 with
  `Unavailable` when the configuration is absent or the checked schema is unavailable.
  It checks `CONTRACT_DOCUMENTS`, `EMPLOYEE` and `TENANT` without exposing credentials.

Mapping was reverse engineered from the initialized SQL Server database, with
no connection string embedded in generated code. The one keyless join table,
`EMPLOYEE_DEPARTMENT`, has a unique, non-null `(employee_id, department_id)` pair.
`MasterDbContext.cs` (`OnModelCreating`) uses that pair as its EF tracking key; it does not add a
physical primary key. No foreign-key relationships have been invented.

Do not run legacy migrations, `EnsureCreated` or `Migrate` against MASTER.
The supplied SQL is the schema baseline. Future schema changes need a deliberate
upgrade script or a separately reviewed migration baseline.

## Internal workspace

The internal draft workflow now runs directly on MASTER. See
[INTERNAL-WORKSPACE.md](../../../INTERNAL-WORKSPACE.md) for initialization,
credentials, run commands, permissions, storage and verification.
`001-internal-drafts.sql` upgrades the version pointer to BIGINT, adds a unique
employee username index, and filters the unique customer email index to non-null values.
`002-contract-review.sql` adds the customer review flow (CONTRACT_REVIEW_REQUEST, SMS_OUTBOX,
SMS_PROVIDER_CALLBACK, review columns, document status 0..7 and foreign keys); 63 tables / 724 columns after it.
`003-template-fields.sql` (owned by Hoang) adds CONTRACT_TEMPLATE_FIELDS when missing. CONTRACT_DOCUMENT_ITEMS and CONTRACT_DYNAMIC_VALUES
are already in `MasterDB-Optimize.sql`; an older database without them needs them created from that file.
`004-employee-admin.sql` makes employee email / phone / personal identifier filtered-unique, drops the
display-name unique constraint, adds `employee_must_change_password`, and adds the `employee.manage`
permission with the `INTERNAL_ADMIN` role for account administration.
`005-department-roles.sql` seeds the 5 departments of `Artifacts/Sql/seed-masterdb.sql` (same codes and ids), adds
`DEPARTMENT.department_staff_role_id` / `department_manager_role_id` / `department_manager_employee_id` (one manager per
department, one department per employee), the per-page permissions and a staff + manager role per department, and moves
the `CONTRACT_INTERNAL_CHECK` step from the retired `INTERNAL_OFFICE` role to Ban Giám Đốc (`INTERNAL_ADMIN`).
`010-demo-template.sql` inserts the demo template `INTERNAL_DEMO_V1` (Hoang's former `--seed-internal` bootstrap content):
CONTRACT_TEMPLATE plus 18 fields and 3 empty terms in CONTRACT_TEMPLATE_FIELDS (data type `TERM`, label = title,
extra_options = default content). A template with no stored files is read straight from these rows.
`011-customer-electronic-signing-otp.sql` (Nhat's branch, formerly `003-customer-electronic-signing-otp.sql`) adds
CONTRACT_CUSTOMER_OTP_CHALLENGE, the OTP a customer confirms when signing a contract electronically.
The schema is database first: the application never applies these scripts. Run them manually and
idempotently (SSMS, or `sqlcmd -I` so QUOTED_IDENTIFIER is on for the filtered indexes).
Employees, roles, permissions, customers and the `CONTRACT_INTERNAL_CHECK` approval workflow
are also inserted directly in the database.

## Legacy scope

Existing legacy controllers,
services, login/RBAC, central database and tenant provisioning still use their
legacy contexts and schema. They are not compatible merely by changing their
connection strings. Their migration requires implementing the new workflows,
including document/version handling, permissions and tenant database resolution.
Legacy data has not been migrated.

MASTER currently contains sensitive records for multiple business areas and has
no database foreign keys. New endpoints must enforce authorization and data scope
explicitly; entity mapping alone supplies neither.

## Verification

From `Source/Backend`:

```powershell
$env:CONTRACK_MASTER_TEST_CONNECTION = 'Server=localhost;Database=ContractManagement_Master;Integrated Security=True;TrustServerCertificate=True'
dotnet test ContractManagement.Tests/ContractManagement.Tests.csproj --filter FullyQualifiedName~MasterDatabaseTests
```

Tests verify the 60-table/659-column EF model, missing configuration behavior,
live column names, SQL reads for every table and a department insert with database
defaults followed by rollback. Without the environment variable, the live test is
explicitly skipped. The live test was executed locally and all three tests passed.
The running API also returned `{"status":"Ready"}` from `/health/master`.
