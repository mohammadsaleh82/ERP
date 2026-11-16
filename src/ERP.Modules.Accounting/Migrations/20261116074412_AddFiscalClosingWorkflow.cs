using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Modules.Accounting.Migrations
{
    /// <inheritdoc />
    public partial class AddFiscalClosingWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "reference_document_id",
                table: "acc_documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "acc_fiscal_year_closings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fiscal_year_id = table.Column<Guid>(type: "uuid", nullable: false),
                    retained_earnings_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    net_income = table.Column<decimal>(type: "numeric(28,4)", precision: 28, scale: 4, nullable: true),
                    total_revenue = table.Column<decimal>(type: "numeric(28,4)", precision: 28, scale: 4, nullable: true),
                    total_expense = table.Column<decimal>(type: "numeric(28,4)", precision: 28, scale: 4, nullable: true),
                    closing_document_id = table.Column<Guid>(type: "uuid", nullable: true),
                    closing_document_number = table.Column<long>(type: "bigint", nullable: true),
                    failed_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    started_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    closed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    started_by = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    closed_by = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    last_modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_acc_fiscal_year_closings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "acc_fiscal_year_reopenings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fiscal_year_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    requested_by = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    approved_by = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    reopened_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    next_opening_impact = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    changes_summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    last_modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_acc_fiscal_year_reopenings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "acc_fiscal_year_closing_steps",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fiscal_year_closing_id = table.Column<Guid>(type: "uuid", nullable: false),
                    step_number = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    output = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    started_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    finished_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_acc_fiscal_year_closing_steps", x => x.id);
                    table.ForeignKey(
                        name: "fk_acc_fiscal_year_closing_steps_acc_fiscal_year_closings_fisc",
                        column: x => x.fiscal_year_closing_id,
                        principalTable: "acc_fiscal_year_closings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_acc_fiscal_year_closing_steps_fiscal_year_closing_id_step_n",
                table: "acc_fiscal_year_closing_steps",
                columns: new[] { "fiscal_year_closing_id", "step_number" });

            migrationBuilder.CreateIndex(
                name: "ix_acc_fiscal_year_closings_tenant_id_company_id_fiscal_year_id",
                table: "acc_fiscal_year_closings",
                columns: new[] { "tenant_id", "company_id", "fiscal_year_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_acc_fiscal_year_reopenings_tenant_id_company_id_fiscal_year",
                table: "acc_fiscal_year_reopenings",
                columns: new[] { "tenant_id", "company_id", "fiscal_year_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "acc_fiscal_year_closing_steps");

            migrationBuilder.DropTable(
                name: "acc_fiscal_year_reopenings");

            migrationBuilder.DropTable(
                name: "acc_fiscal_year_closings");

            migrationBuilder.DropColumn(
                name: "reference_document_id",
                table: "acc_documents");
        }
    }
}
