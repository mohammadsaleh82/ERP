using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Modules.Accounting.Migrations
{
    /// <inheritdoc />
    public partial class AddFinancialReporting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "acc_cash_flow_classifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category = table.Column<int>(type: "integer", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    last_modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_acc_cash_flow_classifications", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "acc_reporting_classifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    root_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    category = table.Column<int>(type: "integer", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    last_modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_acc_reporting_classifications", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_acc_gl_entries_tenant_id_company_id_fiscal_year_id_entry_da",
                table: "acc_gl_entries",
                columns: new[] { "tenant_id", "company_id", "fiscal_year_id", "entry_date" });

            migrationBuilder.CreateIndex(
                name: "ix_acc_cash_flow_classifications_tenant_id_company_id_account_",
                table: "acc_cash_flow_classifications",
                columns: new[] { "tenant_id", "company_id", "account_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_acc_reporting_classifications_tenant_id_company_id_root_code",
                table: "acc_reporting_classifications",
                columns: new[] { "tenant_id", "company_id", "root_code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "acc_cash_flow_classifications");

            migrationBuilder.DropTable(
                name: "acc_reporting_classifications");

            migrationBuilder.DropIndex(
                name: "ix_acc_gl_entries_tenant_id_company_id_fiscal_year_id_entry_da",
                table: "acc_gl_entries");
        }
    }
}
