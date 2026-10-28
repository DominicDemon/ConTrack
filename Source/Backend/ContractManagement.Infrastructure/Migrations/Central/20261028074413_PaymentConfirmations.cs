using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContractManagement.Infrastructure.Migrations.Central
{
    /// <inheritdoc />
    public partial class PaymentConfirmations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProvisioningLeaseId",
                table: "SUBSCRIPTION",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProvisioningLeaseUntil",
                table: "SUBSCRIPTION",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PurchaserEmployeeId",
                table: "SUBSCRIPTION",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BuyerNameSnapshot",
                table: "SAAS_INVOICE",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DurationDaysSnapshot",
                table: "SAAS_INVOICE",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DurationMonthsSnapshot",
                table: "SAAS_INVOICE",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlanNameSnapshot",
                table: "SAAS_INVOICE",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PublicId",
                table: "SAAS_INVOICE",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWID()");

            migrationBuilder.Sql("""
                UPDATE i SET i.DurationMonthsSnapshot = v.plan_version_duration_months,
                    i.DurationDaysSnapshot = v.plan_version_duration_days
                FROM SAAS_INVOICE i JOIN SUBSCRIPTION s ON s.subscription_id = i.subscription_id
                JOIN PLAN_VERSION v ON v.plan_version_id = s.plan_version_id;
                """);

            migrationBuilder.CreateTable(
                name: "INVOICE_EMAIL_OUTBOX",
                columns: table => new
                {
                    OutboxId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InvoiceId = table.Column<long>(type: "bigint", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LeaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LeaseUntil = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailureCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Revision = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INVOICE_EMAIL_OUTBOX", x => x.OutboxId);
                    table.ForeignKey(
                        name: "FK_INVOICE_EMAIL_OUTBOX_SAAS_INVOICE_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "SAAS_INVOICE",
                        principalColumn: "invoice_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SAAS_INVOICE_PublicId",
                table: "SAAS_INVOICE",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INVOICE_EMAIL_OUTBOX_InvoiceId_Kind",
                table: "INVOICE_EMAIL_OUTBOX",
                columns: new[] { "InvoiceId", "Kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INVOICE_EMAIL_OUTBOX_Status_NextAttemptAt",
                table: "INVOICE_EMAIL_OUTBOX",
                columns: new[] { "Status", "NextAttemptAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "INVOICE_EMAIL_OUTBOX");

            migrationBuilder.DropIndex(
                name: "IX_SAAS_INVOICE_PublicId",
                table: "SAAS_INVOICE");

            migrationBuilder.DropColumn(
                name: "ProvisioningLeaseId",
                table: "SUBSCRIPTION");

            migrationBuilder.DropColumn(
                name: "ProvisioningLeaseUntil",
                table: "SUBSCRIPTION");

            migrationBuilder.DropColumn(
                name: "PurchaserEmployeeId",
                table: "SUBSCRIPTION");

            migrationBuilder.DropColumn(
                name: "BuyerNameSnapshot",
                table: "SAAS_INVOICE");

            migrationBuilder.DropColumn(
                name: "DurationDaysSnapshot",
                table: "SAAS_INVOICE");

            migrationBuilder.DropColumn(
                name: "DurationMonthsSnapshot",
                table: "SAAS_INVOICE");

            migrationBuilder.DropColumn(
                name: "PlanNameSnapshot",
                table: "SAAS_INVOICE");

            migrationBuilder.DropColumn(
                name: "PublicId",
                table: "SAAS_INVOICE");
        }
    }
}
