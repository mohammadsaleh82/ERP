using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Modules.Accounting.Migrations
{
    /// <inheritdoc />
    public partial class AddFinancialDimensions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "acc_account_dimension_policies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dimension_type = table.Column<int>(type: "integer", nullable: false),
                    mode = table.Column<int>(type: "integer", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    last_modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_acc_account_dimension_policies", x => x.id);
                    table.ForeignKey(
                        name: "fk_acc_account_dimension_policies_acc_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "acc_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "acc_dimension_combination_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    first_dimension_type = table.Column<int>(type: "integer", nullable: false),
                    first_dimension_id = table.Column<Guid>(type: "uuid", nullable: true),
                    second_dimension_type = table.Column<int>(type: "integer", nullable: false),
                    second_dimension_id = table.Column<Guid>(type: "uuid", nullable: true),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    last_modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_acc_dimension_combination_rules", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "acc_document_line_dimensions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dimension_type = table.Column<int>(type: "integer", nullable: false),
                    dimension_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_acc_document_line_dimensions", x => x.id);
                    table.ForeignKey(
                        name: "fk_acc_document_line_dimensions_acc_document_lines_document_li",
                        column: x => x.document_line_id,
                        principalTable: "acc_document_lines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "acc_financial_dimensions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dimension_type = table.Column<int>(type: "integer", nullable: false),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    external_reference_id = table.Column<Guid>(type: "uuid", nullable: true),
                    owner_module = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    last_modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_acc_financial_dimensions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "acc_gl_entry_dimensions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    general_ledger_entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dimension_type = table.Column<int>(type: "integer", nullable: false),
                    dimension_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_acc_gl_entry_dimensions", x => x.id);
                    table.ForeignKey(
                        name: "fk_acc_gl_entry_dimensions_acc_gl_entries_general_ledger_entry",
                        column: x => x.general_ledger_entry_id,
                        principalTable: "acc_gl_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_acc_account_dimension_policies_account_id",
                table: "acc_account_dimension_policies",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_acc_account_dimension_policies_tenant_id_company_id_account",
                table: "acc_account_dimension_policies",
                columns: new[] { "tenant_id", "company_id", "account_id", "dimension_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_acc_dimension_combination_rules_tenant_id_company_id",
                table: "acc_dimension_combination_rules",
                columns: new[] { "tenant_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "ix_acc_document_line_dimensions_document_line_id_dimension_type",
                table: "acc_document_line_dimensions",
                columns: new[] { "document_line_id", "dimension_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_acc_document_line_dimensions_tenant_id_company_id_dimension",
                table: "acc_document_line_dimensions",
                columns: new[] { "tenant_id", "company_id", "dimension_type", "dimension_id" });

            migrationBuilder.CreateIndex(
                name: "ix_acc_financial_dimensions_tenant_id_company_id_dimension_typ",
                table: "acc_financial_dimensions",
                columns: new[] { "tenant_id", "company_id", "dimension_type", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_acc_financial_dimensions_tenant_id_company_id_dimension_typ1",
                table: "acc_financial_dimensions",
                columns: new[] { "tenant_id", "company_id", "dimension_type", "external_reference_id" });

            migrationBuilder.CreateIndex(
                name: "ix_acc_financial_dimensions_tenant_id_company_id_dimension_typ2",
                table: "acc_financial_dimensions",
                columns: new[] { "tenant_id", "company_id", "dimension_type", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_acc_gl_entry_dimensions_general_ledger_entry_id_dimension_t",
                table: "acc_gl_entry_dimensions",
                columns: new[] { "general_ledger_entry_id", "dimension_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_acc_gl_entry_dimensions_tenant_id_company_id_dimension_type",
                table: "acc_gl_entry_dimensions",
                columns: new[] { "tenant_id", "company_id", "dimension_type", "dimension_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "acc_account_dimension_policies");

            migrationBuilder.DropTable(
                name: "acc_dimension_combination_rules");

            migrationBuilder.DropTable(
                name: "acc_document_line_dimensions");

            migrationBuilder.DropTable(
                name: "acc_financial_dimensions");

            migrationBuilder.DropTable(
                name: "acc_gl_entry_dimensions");
        }
    }
}
