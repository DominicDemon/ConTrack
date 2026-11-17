using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContractManagement.Infrastructure.Migrations.Application
{
    /// <inheritdoc />
    public partial class TenantElectronicSigning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tbl_ContractSigningSession",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContractId = table.Column<int>(type: "int", nullable: false),
                    VersionId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    IsLocked = table.Column<bool>(type: "bit", nullable: false),
                    LinkLifetimeHours = table.Column<int>(type: "int", nullable: false),
                    LockedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RejectedByEmployeeId = table.Column<long>(type: "bigint", nullable: true),
                    RejectedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_ContractSigningSession", x => x.Id);
                    table.CheckConstraint("CK_ContractSigningSession_Contract", "[ContractId] > 0");
                    table.CheckConstraint("CK_ContractSigningSession_LinkLifetime", "[LinkLifetimeHours] BETWEEN 1 AND 168");
                    table.CheckConstraint("CK_ContractSigningSession_Status", "[Status] BETWEEN 0 AND 8");
                    table.CheckConstraint("CK_ContractSigningSession_Version", "[VersionId] > 0");
                });

            migrationBuilder.CreateTable(
                name: "tbl_ContractSigningArtifact",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionId = table.Column<long>(type: "bigint", nullable: false),
                    Kind = table.Column<byte>(type: "tinyint", nullable: false),
                    TenantCode = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    StorageKey = table.Column<string>(type: "varchar(1000)", unicode: false, maxLength: 1000, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ContentType = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    Sha256 = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    CreatedByEmployeeId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_ContractSigningArtifact", x => x.Id);
                    table.CheckConstraint("CK_ContractSigningArtifact_Kind", "[Kind] BETWEEN 1 AND 5");
                    table.CheckConstraint("CK_ContractSigningArtifact_Size", "[FileSize] > 0");
                    table.ForeignKey(
                        name: "FK_tbl_ContractSigningArtifact_tbl_ContractSigningSession_SessionId",
                        column: x => x.SessionId,
                        principalTable: "tbl_ContractSigningSession",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tbl_ContractSigningAuditLog",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionId = table.Column<long>(type: "bigint", nullable: false),
                    SignerId = table.Column<long>(type: "bigint", nullable: true),
                    Action = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    IpAddress = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    EventDataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_ContractSigningAuditLog", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tbl_ContractSigningAuditLog_tbl_ContractSigningSession_SessionId",
                        column: x => x.SessionId,
                        principalTable: "tbl_ContractSigningSession",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tbl_ContractSigningSigner",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionId = table.Column<long>(type: "bigint", nullable: false),
                    Role = table.Column<byte>(type: "tinyint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    SigningOrder = table.Column<int>(type: "int", nullable: false),
                    OtpWindowStartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OtpLastRequestedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OtpRequestCount = table.Column<int>(type: "int", nullable: false),
                    SignedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SignatureStorageKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SignatureSha256 = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true),
                    PageNumber = table.Column<int>(type: "int", nullable: true),
                    PositionX = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PositionY = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Width = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Height = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    IpAddress = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_ContractSigningSigner", x => x.Id);
                    table.CheckConstraint("CK_ContractSigningSigner_OtpRequests", "[OtpRequestCount] >= 0");
                    table.CheckConstraint("CK_ContractSigningSigner_Role", "[Role] IN (1, 2)");
                    table.CheckConstraint("CK_ContractSigningSigner_Status", "[Status] BETWEEN 0 AND 2");
                    table.ForeignKey(
                        name: "FK_tbl_ContractSigningSigner_tbl_ContractSigningSession_SessionId",
                        column: x => x.SessionId,
                        principalTable: "tbl_ContractSigningSession",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tbl_ContractSigningLink",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionId = table.Column<long>(type: "bigint", nullable: false),
                    SignerId = table.Column<long>(type: "bigint", nullable: false),
                    TokenHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_ContractSigningLink", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tbl_ContractSigningLink_tbl_ContractSigningSession_SessionId",
                        column: x => x.SessionId,
                        principalTable: "tbl_ContractSigningSession",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_tbl_ContractSigningLink_tbl_ContractSigningSigner_SignerId",
                        column: x => x.SignerId,
                        principalTable: "tbl_ContractSigningSigner",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tbl_ContractSigningOtp",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SignerId = table.Column<long>(type: "bigint", nullable: false),
                    ChallengeId = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    OtpHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    OtpCiphertext = table.Column<byte[]>(type: "varbinary(128)", maxLength: 128, nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FailedAttemptCount = table.Column<int>(type: "int", nullable: false),
                    DeliveryStatus = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    DeliveryAttemptCount = table.Column<int>(type: "int", nullable: false),
                    DeliveryNextAttemptAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeliveryLeaseId = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true),
                    DeliveryLeaseUntil = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeliveryLastFailure = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VerifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UsedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_ContractSigningOtp", x => x.Id);
                    table.CheckConstraint("CK_ContractSigningOtp_DeliveryAttempts", "[DeliveryAttemptCount] >= 0");
                    table.CheckConstraint("CK_ContractSigningOtp_DeliveryStatus", "[DeliveryStatus] IN ('Pending', 'Leased', 'Sent', 'Failed', 'Unknown', 'Expired')");
                    table.CheckConstraint("CK_ContractSigningOtp_Expiry", "[ExpiresAt] > [CreatedAt]");
                    table.CheckConstraint("CK_ContractSigningOtp_FailedAttempts", "[FailedAttemptCount] BETWEEN 0 AND 5");
                    table.ForeignKey(
                        name: "FK_tbl_ContractSigningOtp_tbl_ContractSigningSigner_SignerId",
                        column: x => x.SignerId,
                        principalTable: "tbl_ContractSigningSigner",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tbl_ContractSigningArtifact_SessionId_Kind",
                table: "tbl_ContractSigningArtifact",
                columns: new[] { "SessionId", "Kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tbl_ContractSigningAuditLog_SessionId",
                table: "tbl_ContractSigningAuditLog",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_ContractSigningLink_SessionId",
                table: "tbl_ContractSigningLink",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_ContractSigningLink_SignerId",
                table: "tbl_ContractSigningLink",
                column: "SignerId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_ContractSigningLink_TokenHash",
                table: "tbl_ContractSigningLink",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContractSigningOtp_DeliveryQueue",
                table: "tbl_ContractSigningOtp",
                columns: new[] { "DeliveryStatus", "DeliveryNextAttemptAt", "DeliveryLeaseUntil" });

            migrationBuilder.CreateIndex(
                name: "IX_tbl_ContractSigningOtp_ChallengeId",
                table: "tbl_ContractSigningOtp",
                column: "ChallengeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tbl_ContractSigningOtp_SignerId",
                table: "tbl_ContractSigningOtp",
                column: "SignerId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_ContractSigningSession_ContractId_VersionId",
                table: "tbl_ContractSigningSession",
                columns: new[] { "ContractId", "VersionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tbl_ContractSigningSigner_SessionId_Role",
                table: "tbl_ContractSigningSigner",
                columns: new[] { "SessionId", "Role" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tbl_ContractSigningArtifact");

            migrationBuilder.DropTable(
                name: "tbl_ContractSigningAuditLog");

            migrationBuilder.DropTable(
                name: "tbl_ContractSigningLink");

            migrationBuilder.DropTable(
                name: "tbl_ContractSigningOtp");

            migrationBuilder.DropTable(
                name: "tbl_ContractSigningSigner");

            migrationBuilder.DropTable(
                name: "tbl_ContractSigningSession");
        }
    }
}
