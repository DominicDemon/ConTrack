using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ContractManagement.Infrastructure.Migrations.Central
{
    /// <inheritdoc />
    public partial class AddSaasSubscription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "OwnerCustomerId",
                table: "Tenants",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CUSTOMER",
                columns: table => new
                {
                    customer_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    customer_email = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: false),
                    customer_phone = table.Column<string>(type: "varchar(15)", unicode: false, maxLength: 15, nullable: true),
                    customer_hand_phone = table.Column<string>(type: "varchar(15)", unicode: false, maxLength: 15, nullable: true),
                    customer_password_hash = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    customer_display_name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    customer_type = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValue: "CUSTOMER"),
                    customer_status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValue: "ACTIVE"),
                    customer_email_verified_at = table.Column<DateTime>(type: "datetime", nullable: true),
                    customer_hand_phone_verified_at = table.Column<DateTime>(type: "datetime", nullable: true),
                    customer_last_login_at = table.Column<DateTime>(type: "datetime", nullable: true),
                    customer_dob = table.Column<DateTime>(type: "date", nullable: true),
                    customer_address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "GETDATE()"),
                    updated_at = table.Column<DateTime>(type: "datetime", nullable: true),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CUSTOMER", x => x.customer_id);
                });

            migrationBuilder.CreateTable(
                name: "PROMOTION",
                columns: table => new
                {
                    promotion_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    promotion_code = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    promotion_name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    promotion_description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    promotion_discount_type = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    promotion_discount_value = table.Column<decimal>(type: "decimal(19,4)", nullable: false),
                    promotion_max_discount_amount = table.Column<decimal>(type: "decimal(19,4)", nullable: true),
                    promotion_start_at = table.Column<DateTime>(type: "datetime", nullable: false),
                    promotion_end_at = table.Column<DateTime>(type: "datetime", nullable: true),
                    promotion_usage_limit = table.Column<int>(type: "int", nullable: true),
                    promotion_usage_count = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    promotion_status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValue: "ACTIVE"),
                    created_at = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "GETDATE()"),
                    updated_at = table.Column<DateTime>(type: "datetime", nullable: true),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PROMOTION", x => x.promotion_id);
                });

            migrationBuilder.CreateTable(
                name: "SAAS_TYPE",
                columns: table => new
                {
                    saas_type_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    saas_type_code = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    saas_type_name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    saas_type_description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    saas_type_status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValue: "ACTIVE"),
                    created_at = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "GETDATE()"),
                    updated_at = table.Column<DateTime>(type: "datetime", nullable: true),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SAAS_TYPE", x => x.saas_type_id);
                });

            migrationBuilder.CreateTable(
                name: "CUSTOMER_BUSINESS",
                columns: table => new
                {
                    customer_id = table.Column<long>(type: "bigint", nullable: false),
                    customer_business_legal_name = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    customer_business_company_tax_identifier = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    customer_business_phone = table.Column<string>(type: "varchar(15)", unicode: false, maxLength: 15, nullable: true),
                    customer_business_business_email = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    customer_business_business_office_address = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    customer_business_business_head_office_address = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    customer_business_business_type = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    customer_business_representative_name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    customer_business_operation_start_date = table.Column<DateTime>(type: "date", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "GETDATE()"),
                    updated_at = table.Column<DateTime>(type: "datetime", nullable: true),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CUSTOMER_BUSINESS", x => x.customer_id);
                    table.ForeignKey(
                        name: "FK_CUSTOMER_BUSINESS_CUSTOMER",
                        column: x => x.customer_id,
                        principalTable: "CUSTOMER",
                        principalColumn: "customer_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PLAN",
                columns: table => new
                {
                    plan_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    saas_type_id = table.Column<long>(type: "bigint", nullable: false),
                    plan_code = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    plan_name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    plan_description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    plan_status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValue: "ACTIVE"),
                    is_paid = table.Column<bool>(type: "bit", nullable: false),
                    allows_support_tickets = table.Column<bool>(type: "bit", nullable: false),
                    max_contracts = table.Column<int>(type: "int", nullable: false, defaultValue: 10),
                    max_scan_pages = table.Column<int>(type: "int", nullable: false, defaultValue: 20),
                    max_users = table.Column<int>(type: "int", nullable: false, defaultValue: 2),
                    created_at = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "GETDATE()"),
                    updated_at = table.Column<DateTime>(type: "datetime", nullable: true),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PLAN", x => x.plan_id);
                    table.ForeignKey(
                        name: "FK_PLAN_SAAS_TYPE",
                        column: x => x.saas_type_id,
                        principalTable: "SAAS_TYPE",
                        principalColumn: "saas_type_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PLAN_VERSION",
                columns: table => new
                {
                    plan_version_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    plan_id = table.Column<long>(type: "bigint", nullable: false),
                    plan_version_version_number = table.Column<int>(type: "int", nullable: false),
                    plan_version_purchase_type = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValue: "BUYMANY"),
                    plan_version_duration_days = table.Column<int>(type: "int", nullable: true),
                    plan_version_price = table.Column<decimal>(type: "decimal(19,4)", nullable: false, defaultValue: 0m),
                    plan_version_currency = table.Column<string>(type: "char(3)", unicode: false, fixedLength: true, maxLength: 3, nullable: false, defaultValue: "VND"),
                    plan_version_billing_cycle = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValue: "MONTHLY"),
                    plan_version_effective_from = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "GETDATE()"),
                    plan_version_effective_to = table.Column<DateTime>(type: "datetime", nullable: true),
                    plan_version_status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValue: "ACTIVE"),
                    created_at = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "GETDATE()"),
                    updated_at = table.Column<DateTime>(type: "datetime", nullable: true),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PLAN_VERSION", x => x.plan_version_id);
                    table.ForeignKey(
                        name: "FK_PLAN_VERSION_PLAN",
                        column: x => x.plan_id,
                        principalTable: "PLAN",
                        principalColumn: "plan_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SUBSCRIPTION",
                columns: table => new
                {
                    subscription_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    tenant_id = table.Column<int>(type: "int", nullable: true),
                    customer_id = table.Column<long>(type: "bigint", nullable: true),
                    plan_version_id = table.Column<long>(type: "bigint", nullable: false),
                    subscription_status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValue: "PENDING"),
                    subscription_start_at = table.Column<DateTime>(type: "datetime", nullable: true),
                    subscription_end_at = table.Column<DateTime>(type: "datetime", nullable: true),
                    subscription_price_snapshot = table.Column<decimal>(type: "decimal(19,4)", nullable: false, defaultValue: 0m),
                    subscription_currency = table.Column<string>(type: "char(3)", unicode: false, fixedLength: true, maxLength: 3, nullable: false, defaultValue: "VND"),
                    subscription_billing_cycle = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValue: "MONTHLY"),
                    subscription_auto_renew = table.Column<bool>(type: "bit", nullable: false),
                    is_paid = table.Column<bool>(type: "bit", nullable: false),
                    requested_tenant_code = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    requested_tenant_name = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    subscription_public_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    provisioning_attempt_count = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "GETDATE()"),
                    updated_at = table.Column<DateTime>(type: "datetime", nullable: true),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SUBSCRIPTION", x => x.subscription_id);
                    table.ForeignKey(
                        name: "FK_SUBSCRIPTION_CUSTOMER",
                        column: x => x.customer_id,
                        principalTable: "CUSTOMER",
                        principalColumn: "customer_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SUBSCRIPTION_PLAN_VERSION",
                        column: x => x.plan_version_id,
                        principalTable: "PLAN_VERSION",
                        principalColumn: "plan_version_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SUBSCRIPTION_Tenants",
                        column: x => x.tenant_id,
                        principalTable: "Tenants",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SAAS_INVOICE",
                columns: table => new
                {
                    invoice_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    invoice_code = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    customer_id = table.Column<long>(type: "bigint", nullable: false),
                    subscription_id = table.Column<long>(type: "bigint", nullable: true),
                    invoice_billing_name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    invoice_billing_tax_identifier = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    invoice_billing_address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    invoice_billing_email = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    invoice_billing_phone = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    invoice_subtotal = table.Column<decimal>(type: "decimal(19,4)", nullable: false),
                    invoice_discount_amount = table.Column<decimal>(type: "decimal(19,4)", nullable: false, defaultValue: 0m),
                    invoice_total_amount = table.Column<decimal>(type: "decimal(19,4)", nullable: false),
                    invoice_currency = table.Column<string>(type: "char(3)", unicode: false, fixedLength: true, maxLength: 3, nullable: false, defaultValue: "VND"),
                    invoice_status = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false, defaultValue: "DRAFT"),
                    invoice_issued_at = table.Column<DateTime>(type: "datetime", nullable: true),
                    invoice_due_at = table.Column<DateTime>(type: "datetime", nullable: true),
                    invoice_paid_at = table.Column<DateTime>(type: "datetime", nullable: true),
                    payos_order_code = table.Column<long>(type: "bigint", nullable: true),
                    payos_payment_link_id = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    payos_checkout_url = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "GETDATE()"),
                    updated_at = table.Column<DateTime>(type: "datetime", nullable: true),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SAAS_INVOICE", x => x.invoice_id);
                    table.ForeignKey(
                        name: "FK_SAAS_INVOICE_CUSTOMER",
                        column: x => x.customer_id,
                        principalTable: "CUSTOMER",
                        principalColumn: "customer_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SAAS_INVOICE_SUBSCRIPTION",
                        column: x => x.subscription_id,
                        principalTable: "SUBSCRIPTION",
                        principalColumn: "subscription_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SUBSCRIPTION_PROMOTION_DETAIL",
                columns: table => new
                {
                    subscription_promotion_detail_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    promotion_id = table.Column<long>(type: "bigint", nullable: false),
                    subscription_id = table.Column<long>(type: "bigint", nullable: false),
                    discount_type_applied = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    discount_value_applied = table.Column<decimal>(type: "decimal(19,4)", nullable: false),
                    actual_money_deducted = table.Column<decimal>(type: "decimal(19,4)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SUBSCRIPTION_PROMOTION_DETAIL", x => x.subscription_promotion_detail_id);
                    table.ForeignKey(
                        name: "FK_SUBSCRIPTION_PROMOTION_DETAIL_PROMOTION",
                        column: x => x.promotion_id,
                        principalTable: "PROMOTION",
                        principalColumn: "promotion_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SUBSCRIPTION_PROMOTION_DETAIL_SUBSCRIPTION",
                        column: x => x.subscription_id,
                        principalTable: "SUBSCRIPTION",
                        principalColumn: "subscription_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INVOICE_PROMOTION_DETAIL",
                columns: table => new
                {
                    invoice_promotion_detail_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    invoice_id = table.Column<long>(type: "bigint", nullable: false),
                    promotion_id = table.Column<long>(type: "bigint", nullable: false),
                    discount_type_applied = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    discount_value_applied = table.Column<decimal>(type: "decimal(19,4)", nullable: false),
                    actual_money_deducted = table.Column<decimal>(type: "decimal(19,4)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INVOICE_PROMOTION_DETAIL", x => x.invoice_promotion_detail_id);
                    table.ForeignKey(
                        name: "FK_INVOICE_PROMOTION_DETAIL_INVOICE",
                        column: x => x.invoice_id,
                        principalTable: "SAAS_INVOICE",
                        principalColumn: "invoice_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INVOICE_PROMOTION_DETAIL_PROMOTION",
                        column: x => x.promotion_id,
                        principalTable: "PROMOTION",
                        principalColumn: "promotion_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SAAS_PAYMENT",
                columns: table => new
                {
                    payment_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    invoice_id = table.Column<long>(type: "bigint", nullable: false),
                    payment_reference = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    payment_method = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    payment_provider = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    payment_provider_transaction_id = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    payment_amount = table.Column<decimal>(type: "decimal(19,4)", nullable: false),
                    payment_currency = table.Column<string>(type: "char(3)", unicode: false, fixedLength: true, maxLength: 3, nullable: false, defaultValue: "VND"),
                    payment_status = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false, defaultValue: "PENDING"),
                    payment_paid_at = table.Column<DateTime>(type: "datetime", nullable: true),
                    payment_failure_reason = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "GETDATE()"),
                    updated_at = table.Column<DateTime>(type: "datetime", nullable: true),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SAAS_PAYMENT", x => x.payment_id);
                    table.ForeignKey(
                        name: "FK_SAAS_PAYMENT_INVOICE",
                        column: x => x.invoice_id,
                        principalTable: "SAAS_INVOICE",
                        principalColumn: "invoice_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "PROMOTION",
                columns: new[] { "promotion_id", "created_at", "created_by", "promotion_discount_type", "promotion_discount_value", "promotion_end_at", "promotion_max_discount_amount", "promotion_code", "promotion_description", "promotion_name", "promotion_start_at", "promotion_status", "updated_at", "updated_by", "promotion_usage_limit" },
                values: new object[] { 1L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "PERCENTAGE", 10m, null, 500000m, "TEST10", null, "Voucher test giảm 10%", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "ACTIVE", null, null, 100 });

            migrationBuilder.InsertData(
                table: "SAAS_TYPE",
                columns: new[] { "saas_type_id", "created_at", "created_by", "saas_type_code", "saas_type_description", "saas_type_name", "saas_type_status", "updated_at", "updated_by" },
                values: new object[] { 1L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "CONTRACK", "Phần mềm quản lý hợp đồng", "ConTrack", "ACTIVE", null, null });

            migrationBuilder.InsertData(
                table: "PLAN",
                columns: new[] { "plan_id", "allows_support_tickets", "created_at", "created_by", "is_paid", "max_contracts", "max_scan_pages", "max_users", "plan_code", "plan_description", "plan_name", "plan_status", "saas_type_id", "updated_at", "updated_by" },
                values: new object[,]
                {
                    { 1L, false, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, 10, 20, 2, "TRIAL", "Trải nghiệm quy trình chính, không bao gồm Customer Support", "Dùng thử", "ACTIVE", 1L, null, null },
                    { 2L, true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, 1000, 1000, 20, "STANDARD", "Đầy đủ tính năng, có Customer Support", "Tiêu chuẩn", "ACTIVE", 1L, null, null }
                });

            migrationBuilder.InsertData(
                table: "PLAN_VERSION",
                columns: new[] { "plan_version_id", "plan_version_billing_cycle", "created_at", "created_by", "plan_version_currency", "plan_version_duration_days", "plan_version_effective_from", "plan_version_effective_to", "plan_id", "plan_version_purchase_type", "plan_version_status", "updated_at", "updated_by", "plan_version_version_number" },
                values: new object[] { 1L, "TRIAL", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "VND", 15, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 1L, "BUYMANY", "ACTIVE", null, null, 1 });

            migrationBuilder.InsertData(
                table: "PLAN_VERSION",
                columns: new[] { "plan_version_id", "plan_version_billing_cycle", "created_at", "created_by", "plan_version_currency", "plan_version_duration_days", "plan_version_effective_from", "plan_version_effective_to", "plan_id", "plan_version_price", "plan_version_purchase_type", "plan_version_status", "updated_at", "updated_by", "plan_version_version_number" },
                values: new object[,]
                {
                    { 2L, "QUARTERLY", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "VND", 90, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2L, 900000m, "BUYMANY", "ACTIVE", null, null, 1 },
                    { 3L, "SEMI_ANNUAL", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "VND", 180, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2L, 1620000m, "BUYMANY", "ACTIVE", null, null, 2 },
                    { 4L, "YEARLY", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "VND", 365, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2L, 2880000m, "BUYMANY", "ACTIVE", null, null, 3 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_OwnerCustomerId",
                table: "Tenants",
                column: "OwnerCustomerId");

            migrationBuilder.CreateIndex(
                name: "UX_CUSTOMER_customer_email",
                table: "CUSTOMER",
                column: "customer_email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INVOICE_PROMOTION_DETAIL_invoice_id",
                table: "INVOICE_PROMOTION_DETAIL",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "IX_INVOICE_PROMOTION_DETAIL_promotion_id",
                table: "INVOICE_PROMOTION_DETAIL",
                column: "promotion_id");

            migrationBuilder.CreateIndex(
                name: "IX_PLAN_saas_type_id",
                table: "PLAN",
                column: "saas_type_id");

            migrationBuilder.CreateIndex(
                name: "UX_PLAN_VERSION_Number",
                table: "PLAN_VERSION",
                columns: new[] { "plan_id", "plan_version_version_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_PROMOTION_code",
                table: "PROMOTION",
                column: "promotion_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SAAS_INVOICE_customer_id",
                table: "SAAS_INVOICE",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_SAAS_INVOICE_subscription_id",
                table: "SAAS_INVOICE",
                column: "subscription_id");

            migrationBuilder.CreateIndex(
                name: "UX_SAAS_INVOICE_code",
                table: "SAAS_INVOICE",
                column: "invoice_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_SAAS_INVOICE_payos_order_code",
                table: "SAAS_INVOICE",
                column: "payos_order_code",
                unique: true,
                filter: "[payos_order_code] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SAAS_PAYMENT_invoice_id",
                table: "SAAS_PAYMENT",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "UX_SAAS_PAYMENT_ref",
                table: "SAAS_PAYMENT",
                column: "payment_reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_SAAS_TYPE_code",
                table: "SAAS_TYPE",
                column: "saas_type_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SUBSCRIPTION_Customer",
                table: "SUBSCRIPTION",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_SUBSCRIPTION_plan_version_id",
                table: "SUBSCRIPTION",
                column: "plan_version_id");

            migrationBuilder.CreateIndex(
                name: "IX_SUBSCRIPTION_RequestedTenantCode",
                table: "SUBSCRIPTION",
                column: "requested_tenant_code");

            migrationBuilder.CreateIndex(
                name: "IX_SUBSCRIPTION_Status",
                table: "SUBSCRIPTION",
                columns: new[] { "subscription_status", "is_paid" });

            migrationBuilder.CreateIndex(
                name: "IX_SUBSCRIPTION_Tenant",
                table: "SUBSCRIPTION",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "UX_SUBSCRIPTION_public_id",
                table: "SUBSCRIPTION",
                column: "subscription_public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SUBSCRIPTION_PROMOTION_DETAIL_promotion_id",
                table: "SUBSCRIPTION_PROMOTION_DETAIL",
                column: "promotion_id");

            migrationBuilder.CreateIndex(
                name: "IX_SUBSCRIPTION_PROMOTION_DETAIL_subscription_id",
                table: "SUBSCRIPTION_PROMOTION_DETAIL",
                column: "subscription_id");

            migrationBuilder.AddForeignKey(
                name: "FK_Tenants_CUSTOMER_OwnerCustomerId",
                table: "Tenants",
                column: "OwnerCustomerId",
                principalTable: "CUSTOMER",
                principalColumn: "customer_id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tenants_CUSTOMER_OwnerCustomerId",
                table: "Tenants");

            migrationBuilder.DropTable(
                name: "CUSTOMER_BUSINESS");

            migrationBuilder.DropTable(
                name: "INVOICE_PROMOTION_DETAIL");

            migrationBuilder.DropTable(
                name: "SAAS_PAYMENT");

            migrationBuilder.DropTable(
                name: "SUBSCRIPTION_PROMOTION_DETAIL");

            migrationBuilder.DropTable(
                name: "SAAS_INVOICE");

            migrationBuilder.DropTable(
                name: "PROMOTION");

            migrationBuilder.DropTable(
                name: "SUBSCRIPTION");

            migrationBuilder.DropTable(
                name: "CUSTOMER");

            migrationBuilder.DropTable(
                name: "PLAN_VERSION");

            migrationBuilder.DropTable(
                name: "PLAN");

            migrationBuilder.DropTable(
                name: "SAAS_TYPE");

            migrationBuilder.DropIndex(
                name: "IX_Tenants_OwnerCustomerId",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "OwnerCustomerId",
                table: "Tenants");
        }
    }
}
