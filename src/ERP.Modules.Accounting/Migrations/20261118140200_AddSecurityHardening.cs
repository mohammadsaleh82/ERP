using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Modules.Accounting.Migrations
{
    /// <inheritdoc />
    public partial class AddSecurityHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "require_separate_approver",
                table: "acc_accounting_settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "require_separate_poster",
                table: "acc_accounting_settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "acc_audit_trail",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    user_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    timestamp_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    correlation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    before_snapshot = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    after_snapshot = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    last_modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_acc_audit_trail", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_acc_audit_trail_tenant_id_company_id_action_entity_id",
                table: "acc_audit_trail",
                columns: new[] { "tenant_id", "company_id", "action", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "ix_acc_audit_trail_tenant_id_company_id_correlation_id",
                table: "acc_audit_trail",
                columns: new[] { "tenant_id", "company_id", "correlation_id" });

            migrationBuilder.CreateIndex(
                name: "ix_acc_audit_trail_tenant_id_company_id_timestamp_utc",
                table: "acc_audit_trail",
                columns: new[] { "tenant_id", "company_id", "timestamp_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "acc_audit_trail");

            migrationBuilder.DropColumn(
                name: "require_separate_approver",
                table: "acc_accounting_settings");

            migrationBuilder.DropColumn(
                name: "require_separate_poster",
                table: "acc_accounting_settings");
        }
    }
}
