using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContractManagement.Infrastructure.Migrations.Central
{
    /// <inheritdoc />
    public partial class AdminPlanManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowsSupportTicketsSnapshot",
                table: "SUBSCRIPTION",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MaxContractsSnapshot",
                table: "SUBSCRIPTION",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaxScanPagesSnapshot",
                table: "SUBSCRIPTION",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaxUsersSnapshot",
                table: "SUBSCRIPTION",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "plan_version_duration_months",
                table: "PLAN_VERSION",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Revision",
                table: "PLAN",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "PlanAudits",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PlanId = table.Column<long>(type: "bigint", nullable: false),
                    ActorId = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BeforeJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AfterJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanAudits", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "PLAN",
                keyColumn: "plan_id",
                keyValue: 1L,
                column: "Revision",
                value: 0);

            migrationBuilder.UpdateData(
                table: "PLAN",
                keyColumn: "plan_id",
                keyValue: 2L,
                column: "Revision",
                value: 0);

            migrationBuilder.UpdateData(
                table: "PLAN_VERSION",
                keyColumn: "plan_version_id",
                keyValue: 1L,
                column: "plan_version_duration_months",
                value: null);

            migrationBuilder.UpdateData(
                table: "PLAN_VERSION",
                keyColumn: "plan_version_id",
                keyValue: 2L,
                column: "plan_version_duration_months",
                value: 3);

            migrationBuilder.UpdateData(
                table: "PLAN_VERSION",
                keyColumn: "plan_version_id",
                keyValue: 3L,
                column: "plan_version_duration_months",
                value: 6);

            migrationBuilder.UpdateData(
                table: "PLAN_VERSION",
                keyColumn: "plan_version_id",
                keyValue: 4L,
                column: "plan_version_duration_months",
                value: 12);

            migrationBuilder.Sql("""

UPDATE v SET plan_version_duration_months = CASE v.plan_version_duration_days WHEN 90 THEN 3 WHEN 180 THEN 6 WHEN 365 THEN 12 END
FROM PLAN_VERSION v JOIN [PLAN] p ON p.plan_id=v.plan_id WHERE p.is_paid=1 AND v.plan_version_duration_months IS NULL;
IF EXISTS(SELECT 1 FROM PLAN_VERSION v JOIN [PLAN] p ON p.plan_id=v.plan_id WHERE p.is_paid=1 AND v.plan_version_duration_months IS NULL)
 THROW 51000, 'Unmapped existing paid duration. Map duration_months explicitly before migration.', 1;
UPDATE s SET MaxUsersSnapshot=p.max_users, MaxContractsSnapshot=p.max_contracts, MaxScanPagesSnapshot=p.max_scan_pages, AllowsSupportTicketsSnapshot=p.allows_support_tickets
FROM SUBSCRIPTION s JOIN PLAN_VERSION v ON v.plan_version_id=s.plan_version_id JOIN [PLAN] p ON p.plan_id=v.plan_id;
DECLARE @old bigint, @months int, @new bigint;
DECLARE groups_cursor CURSOR LOCAL FAST_FORWARD FOR
 SELECT v.plan_id,v.plan_version_duration_months FROM PLAN_VERSION v JOIN [PLAN] p ON p.plan_id=v.plan_id WHERE p.is_paid=1
 GROUP BY v.plan_id,v.plan_version_duration_months HAVING v.plan_version_duration_months > (SELECT MIN(x.plan_version_duration_months) FROM PLAN_VERSION x WHERE x.plan_id=v.plan_id);
OPEN groups_cursor; FETCH NEXT FROM groups_cursor INTO @old,@months;
WHILE @@FETCH_STATUS=0 BEGIN
 INSERT INTO [PLAN](saas_type_id,plan_code,plan_name,plan_description,plan_status,is_paid,allows_support_tickets,max_contracts,max_scan_pages,max_users,created_at,Revision)
 SELECT saas_type_id,CONCAT(LEFT(plan_code,35),'_',@months,'M'),CONCAT(@months,N' tháng'),plan_description,plan_status,is_paid,allows_support_tickets,max_contracts,max_scan_pages,max_users,GETUTCDATE(),0 FROM [PLAN] WHERE plan_id=@old;
 SET @new=SCOPE_IDENTITY();
 UPDATE PLAN_VERSION SET plan_id=@new WHERE plan_id=@old AND plan_version_duration_months=@months;
 FETCH NEXT FROM groups_cursor INTO @old,@months;
END
CLOSE groups_cursor; DEALLOCATE groups_cursor;
UPDATE p SET plan_name=CONCAT(v.months,N' tháng') FROM [PLAN] p CROSS APPLY(SELECT MIN(plan_version_duration_months) months FROM PLAN_VERSION WHERE plan_id=p.plan_id) v WHERE p.is_paid=1;
""");

            migrationBuilder.CreateIndex(
                name: "IX_PLAN_plan_code",
                table: "PLAN",
                column: "plan_code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlanAudits");

            migrationBuilder.DropIndex(
                name: "IX_PLAN_plan_code",
                table: "PLAN");

            migrationBuilder.DropColumn(
                name: "AllowsSupportTicketsSnapshot",
                table: "SUBSCRIPTION");

            migrationBuilder.DropColumn(
                name: "MaxContractsSnapshot",
                table: "SUBSCRIPTION");

            migrationBuilder.DropColumn(
                name: "MaxScanPagesSnapshot",
                table: "SUBSCRIPTION");

            migrationBuilder.DropColumn(
                name: "MaxUsersSnapshot",
                table: "SUBSCRIPTION");

            migrationBuilder.DropColumn(
                name: "plan_version_duration_months",
                table: "PLAN_VERSION");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "PLAN");
        }
    }
}
