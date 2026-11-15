using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Modules.Accounting.Migrations
{
    /// <inheritdoc />
    public partial class AddLedgerQueriesAndBalanceSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "branch_id",
                table: "acc_gl_entries",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "acc_account_balance_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fiscal_year_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fiscal_period_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    period_debit = table.Column<decimal>(type: "numeric(28,4)", precision: 28, scale: 4, nullable: false),
                    period_credit = table.Column<decimal>(type: "numeric(28,4)", precision: 28, scale: 4, nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    last_modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_acc_account_balance_snapshots", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_acc_gl_entries_tenant_id_company_id_branch_id_entry_date",
                table: "acc_gl_entries",
                columns: new[] { "tenant_id", "company_id", "branch_id", "entry_date" });

            migrationBuilder.CreateIndex(
                name: "ix_acc_gl_entries_tenant_id_company_id_entry_date_document_num",
                table: "acc_gl_entries",
                columns: new[] { "tenant_id", "company_id", "entry_date", "document_number" });

            migrationBuilder.CreateIndex(
                name: "ix_acc_account_balance_snapshots_tenant_id_company_id_fiscal_p",
                table: "acc_account_balance_snapshots",
                columns: new[] { "tenant_id", "company_id", "fiscal_period_id", "account_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_acc_account_balance_snapshots_tenant_id_company_id_fiscal_y",
                table: "acc_account_balance_snapshots",
                columns: new[] { "tenant_id", "company_id", "fiscal_year_id", "account_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "acc_account_balance_snapshots");

            migrationBuilder.DropIndex(
                name: "ix_acc_gl_entries_tenant_id_company_id_branch_id_entry_date",
                table: "acc_gl_entries");

            migrationBuilder.DropIndex(
                name: "ix_acc_gl_entries_tenant_id_company_id_entry_date_document_num",
                table: "acc_gl_entries");

            migrationBuilder.DropColumn(
                name: "branch_id",
                table: "acc_gl_entries");
        }
    }
}
