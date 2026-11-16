using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Modules.Accounting.Migrations
{
    /// <inheritdoc />
    public partial class AddVoucherEngineAndGeneralLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "acc_account_groups",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    nature = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    last_modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_acc_account_groups", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "acc_account_templates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    is_system_template = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    last_modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_acc_account_templates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "acc_accounting_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    default_currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    calendar_type = table.Column<int>(type: "integer", nullable: false),
                    fiscal_year_start_month = table.Column<int>(type: "integer", nullable: false),
                    fiscal_year_start_day = table.Column<int>(type: "integer", nullable: false),
                    decimal_places = table.Column<int>(type: "integer", nullable: false),
                    rounding_mode = table.Column<int>(type: "integer", nullable: false),
                    voucher_numbering_policy = table.Column<int>(type: "integer", nullable: false),
                    allow_negative_balances_policy = table.Column<int>(type: "integer", nullable: false),
                    approval_policy = table.Column<int>(type: "integer", nullable: false),
                    default_account_template_id = table.Column<Guid>(type: "uuid", nullable: true),
                    active_fiscal_year_id = table.Column<Guid>(type: "uuid", nullable: true),
                    lock_date = table.Column<DateOnly>(type: "date", nullable: true),
                    allow_unbalanced_draft = table.Column<bool>(type: "boolean", nullable: false),
                    auto_numbering_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    last_modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_acc_accounting_settings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "acc_accounting_settings_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    settings_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    default_currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    calendar_type = table.Column<int>(type: "integer", nullable: false),
                    fiscal_year_start_month = table.Column<int>(type: "integer", nullable: false),
                    fiscal_year_start_day = table.Column<int>(type: "integer", nullable: false),
                    decimal_places = table.Column<int>(type: "integer", nullable: false),
                    rounding_mode = table.Column<int>(type: "integer", nullable: false),
                    voucher_numbering_policy = table.Column<int>(type: "integer", nullable: false),
                    allow_negative_balances_policy = table.Column<int>(type: "integer", nullable: false),
                    approval_policy = table.Column<int>(type: "integer", nullable: false),
                    lock_date = table.Column<DateOnly>(type: "date", nullable: true),
                    changed_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    changed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    change_reason = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_acc_accounting_settings_history", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "acc_document_audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    previous_status = table.Column<int>(type: "integer", nullable: true),
                    new_status = table.Column<int>(type: "integer", nullable: true),
                    details = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    performed_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    performed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    last_modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_acc_document_audit_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "acc_documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fiscal_year_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fiscal_period_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    document_number = table.Column<long>(type: "bigint", nullable: false),
                    daily_number = table.Column<int>(type: "integer", nullable: false),
                    document_date = table.Column<DateOnly>(type: "date", nullable: false),
                    register_date_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    document_type = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    reference_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    source_module = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    source_document_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reversed_document_id = table.Column<Guid>(type: "uuid", nullable: true),
                    total_debit = table.Column<decimal>(type: "numeric(28,4)", precision: 28, scale: 4, nullable: false),
                    total_credit = table.Column<decimal>(type: "numeric(28,4)", precision: 28, scale: 4, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    last_modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_modified_by = table.Column<string>(type: "text", nullable: true),
                    submitted_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    submitted_by = table.Column<string>(type: "text", nullable: true),
                    reviewed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reviewed_by = table.Column<string>(type: "text", nullable: true),
                    approved_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    approved_by = table.Column<string>(type: "text", nullable: true),
                    rejected_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    rejected_by = table.Column<string>(type: "text", nullable: true),
                    rejection_reason = table.Column<string>(type: "text", nullable: true),
                    posted_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    posted_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_acc_documents", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "acc_fiscal_years",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    year_number = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    calendar_type = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    closed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closed_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    last_modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_modified_by = table.Column<string>(type: "text", nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_acc_fiscal_years", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "acc_gl_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fiscal_year_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fiscal_period_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_number = table.Column<long>(type: "bigint", nullable: false),
                    line_number = table.Column<int>(type: "integer", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entry_date = table.Column<DateOnly>(type: "date", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    side = table.Column<int>(type: "integer", nullable: false),
                    debit_amount = table.Column<decimal>(type: "numeric(28,4)", precision: 28, scale: 4, nullable: false),
                    credit_amount = table.Column<decimal>(type: "numeric(28,4)", precision: 28, scale: 4, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    foreign_currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    foreign_currency_amount = table.Column<decimal>(type: "numeric(28,4)", precision: 28, scale: 4, nullable: true),
                    exchange_rate = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    source_module = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    source_document_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_reversal = table.Column<bool>(type: "boolean", nullable: false),
                    posted_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    posted_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    last_modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_acc_gl_entries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "acc_voucher_sequences",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fiscal_year_id = table.Column<Guid>(type: "uuid", nullable: false),
                    period_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sequence_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    current_value = table.Column<long>(type: "bigint", nullable: false),
                    prefix = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    format_pattern = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_acc_voucher_sequences", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "acc_accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    account_group_id = table.Column<Guid>(type: "uuid", nullable: true),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    full_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    level = table.Column<int>(type: "integer", nullable: false),
                    nature = table.Column<int>(type: "integer", nullable: false),
                    normal_balance = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_system_account = table.Column<bool>(type: "boolean", nullable: false),
                    allow_posting = table.Column<bool>(type: "boolean", nullable: false),
                    require_cost_center = table.Column<bool>(type: "boolean", nullable: false),
                    require_project = table.Column<bool>(type: "boolean", nullable: false),
                    require_detail_account = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    last_modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_acc_accounts", x => x.id);
                    table.ForeignKey(
                        name: "fk_acc_accounts_acc_accounts_parent_account_id",
                        column: x => x.parent_account_id,
                        principalTable: "acc_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_acc_accounts_account_groups_account_group_id",
                        column: x => x.account_group_id,
                        principalTable: "acc_account_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "acc_account_template_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    parent_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    level = table.Column<int>(type: "integer", nullable: false),
                    nature = table.Column<int>(type: "integer", nullable: false),
                    normal_balance = table.Column<int>(type: "integer", nullable: false),
                    allow_posting = table.Column<bool>(type: "boolean", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_acc_account_template_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_acc_account_template_items_acc_account_templates_template_id",
                        column: x => x.template_id,
                        principalTable: "acc_account_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "acc_account_template_levels",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    level = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    code_length = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_acc_account_template_levels", x => x.id);
                    table.ForeignKey(
                        name: "fk_acc_account_template_levels_acc_account_templates_template_",
                        column: x => x.template_id,
                        principalTable: "acc_account_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "acc_fiscal_periods",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fiscal_year_id = table.Column<Guid>(type: "uuid", nullable: false),
                    period_number = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    is_adjustment_period = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    last_modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_acc_fiscal_periods", x => x.id);
                    table.ForeignKey(
                        name: "fk_acc_fiscal_periods_fiscal_years_fiscal_year_id",
                        column: x => x.fiscal_year_id,
                        principalTable: "acc_fiscal_years",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "acc_document_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_number = table.Column<int>(type: "integer", nullable: false),
                    debit_credit = table.Column<int>(type: "integer", nullable: false),
                    debit_amount = table.Column<decimal>(type: "numeric(28,4)", precision: 28, scale: 4, nullable: false),
                    credit_amount = table.Column<decimal>(type: "numeric(28,4)", precision: 28, scale: 4, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    cost_center_id = table.Column<Guid>(type: "uuid", nullable: true),
                    project_id = table.Column<Guid>(type: "uuid", nullable: true),
                    detail_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    foreign_currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    foreign_currency_amount = table.Column<decimal>(type: "numeric(28,4)", precision: 28, scale: 4, nullable: true),
                    exchange_rate = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    last_modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_acc_document_lines", x => x.id);
                    table.ForeignKey(
                        name: "fk_acc_document_lines_acc_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "acc_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_acc_document_lines_acc_documents_document_id",
                        column: x => x.document_id,
                        principalTable: "acc_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_acc_account_groups_tenant_id_company_id_code",
                table: "acc_account_groups",
                columns: new[] { "tenant_id", "company_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_acc_account_template_items_template_id_code",
                table: "acc_account_template_items",
                columns: new[] { "template_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_acc_account_template_items_template_id_parent_code",
                table: "acc_account_template_items",
                columns: new[] { "template_id", "parent_code" });

            migrationBuilder.CreateIndex(
                name: "ix_acc_account_template_levels_template_id_level",
                table: "acc_account_template_levels",
                columns: new[] { "template_id", "level" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_acc_accounting_settings_tenant_id_company_id",
                table: "acc_accounting_settings",
                columns: new[] { "tenant_id", "company_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_acc_accounting_settings_history_tenant_id_company_id_settin",
                table: "acc_accounting_settings_history",
                columns: new[] { "tenant_id", "company_id", "settings_id", "version" });

            migrationBuilder.CreateIndex(
                name: "ix_acc_accounts_account_group_id",
                table: "acc_accounts",
                column: "account_group_id");

            migrationBuilder.CreateIndex(
                name: "ix_acc_accounts_parent_account_id",
                table: "acc_accounts",
                column: "parent_account_id");

            migrationBuilder.CreateIndex(
                name: "ix_acc_accounts_tenant_id_company_id_code",
                table: "acc_accounts",
                columns: new[] { "tenant_id", "company_id", "code" });

            migrationBuilder.CreateIndex(
                name: "ix_acc_accounts_tenant_id_company_id_full_code",
                table: "acc_accounts",
                columns: new[] { "tenant_id", "company_id", "full_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_acc_accounts_tenant_id_company_id_is_active_allow_posting",
                table: "acc_accounts",
                columns: new[] { "tenant_id", "company_id", "is_active", "allow_posting" });

            migrationBuilder.CreateIndex(
                name: "ix_acc_accounts_tenant_id_company_id_level",
                table: "acc_accounts",
                columns: new[] { "tenant_id", "company_id", "level" });

            migrationBuilder.CreateIndex(
                name: "ix_acc_accounts_tenant_id_company_id_parent_account_id",
                table: "acc_accounts",
                columns: new[] { "tenant_id", "company_id", "parent_account_id" });

            migrationBuilder.CreateIndex(
                name: "ix_acc_document_audit_logs_tenant_id_company_id_action",
                table: "acc_document_audit_logs",
                columns: new[] { "tenant_id", "company_id", "action" });

            migrationBuilder.CreateIndex(
                name: "ix_acc_document_audit_logs_tenant_id_company_id_document_id_pe",
                table: "acc_document_audit_logs",
                columns: new[] { "tenant_id", "company_id", "document_id", "performed_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_acc_document_lines_account_id",
                table: "acc_document_lines",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_acc_document_lines_document_id",
                table: "acc_document_lines",
                column: "document_id");

            migrationBuilder.CreateIndex(
                name: "ix_acc_document_lines_tenant_id_company_id_account_id_document",
                table: "acc_document_lines",
                columns: new[] { "tenant_id", "company_id", "account_id", "document_id" });

            migrationBuilder.CreateIndex(
                name: "ix_acc_document_lines_tenant_id_company_id_cost_center_id",
                table: "acc_document_lines",
                columns: new[] { "tenant_id", "company_id", "cost_center_id" });

            migrationBuilder.CreateIndex(
                name: "ix_acc_document_lines_tenant_id_company_id_detail_account_id",
                table: "acc_document_lines",
                columns: new[] { "tenant_id", "company_id", "detail_account_id" });

            migrationBuilder.CreateIndex(
                name: "ix_acc_document_lines_tenant_id_company_id_document_id_line_nu",
                table: "acc_document_lines",
                columns: new[] { "tenant_id", "company_id", "document_id", "line_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_acc_document_lines_tenant_id_company_id_project_id",
                table: "acc_document_lines",
                columns: new[] { "tenant_id", "company_id", "project_id" });

            migrationBuilder.CreateIndex(
                name: "ix_acc_documents_tenant_id_company_id_document_date_status",
                table: "acc_documents",
                columns: new[] { "tenant_id", "company_id", "document_date", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_acc_documents_tenant_id_company_id_fiscal_year_id_document_",
                table: "acc_documents",
                columns: new[] { "tenant_id", "company_id", "fiscal_year_id", "document_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_acc_documents_tenant_id_company_id_idempotency_key",
                table: "acc_documents",
                columns: new[] { "tenant_id", "company_id", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_acc_documents_tenant_id_company_id_source_module_source_doc",
                table: "acc_documents",
                columns: new[] { "tenant_id", "company_id", "source_module", "source_document_id" });

            migrationBuilder.CreateIndex(
                name: "ix_acc_fiscal_periods_fiscal_year_id",
                table: "acc_fiscal_periods",
                column: "fiscal_year_id");

            migrationBuilder.CreateIndex(
                name: "ix_acc_fiscal_periods_tenant_id_company_id_fiscal_year_id_peri",
                table: "acc_fiscal_periods",
                columns: new[] { "tenant_id", "company_id", "fiscal_year_id", "period_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_acc_fiscal_periods_tenant_id_company_id_fiscal_year_id_star",
                table: "acc_fiscal_periods",
                columns: new[] { "tenant_id", "company_id", "fiscal_year_id", "start_date", "end_date" });

            migrationBuilder.CreateIndex(
                name: "ix_acc_fiscal_years_tenant_id_company_id_start_date_end_date",
                table: "acc_fiscal_years",
                columns: new[] { "tenant_id", "company_id", "start_date", "end_date" });

            migrationBuilder.CreateIndex(
                name: "ix_acc_fiscal_years_tenant_id_company_id_year_number",
                table: "acc_fiscal_years",
                columns: new[] { "tenant_id", "company_id", "year_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_acc_gl_entries_tenant_id_company_id_account_id_entry_date",
                table: "acc_gl_entries",
                columns: new[] { "tenant_id", "company_id", "account_id", "entry_date" });

            migrationBuilder.CreateIndex(
                name: "ix_acc_gl_entries_tenant_id_company_id_document_id",
                table: "acc_gl_entries",
                columns: new[] { "tenant_id", "company_id", "document_id" });

            migrationBuilder.CreateIndex(
                name: "ix_acc_gl_entries_tenant_id_company_id_document_id_document_li",
                table: "acc_gl_entries",
                columns: new[] { "tenant_id", "company_id", "document_id", "document_line_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_acc_gl_entries_tenant_id_company_id_fiscal_year_id_fiscal_p",
                table: "acc_gl_entries",
                columns: new[] { "tenant_id", "company_id", "fiscal_year_id", "fiscal_period_id" });

            migrationBuilder.CreateIndex(
                name: "ix_acc_gl_entries_tenant_id_company_id_source_module_source_do",
                table: "acc_gl_entries",
                columns: new[] { "tenant_id", "company_id", "source_module", "source_document_id" });

            migrationBuilder.CreateIndex(
                name: "ix_acc_voucher_sequences_tenant_id_company_id_fiscal_year_id_s",
                table: "acc_voucher_sequences",
                columns: new[] { "tenant_id", "company_id", "fiscal_year_id", "sequence_type", "period_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "acc_account_template_items");

            migrationBuilder.DropTable(
                name: "acc_account_template_levels");

            migrationBuilder.DropTable(
                name: "acc_accounting_settings");

            migrationBuilder.DropTable(
                name: "acc_accounting_settings_history");

            migrationBuilder.DropTable(
                name: "acc_document_audit_logs");

            migrationBuilder.DropTable(
                name: "acc_document_lines");

            migrationBuilder.DropTable(
                name: "acc_fiscal_periods");

            migrationBuilder.DropTable(
                name: "acc_gl_entries");

            migrationBuilder.DropTable(
                name: "acc_voucher_sequences");

            migrationBuilder.DropTable(
                name: "acc_account_templates");

            migrationBuilder.DropTable(
                name: "acc_accounts");

            migrationBuilder.DropTable(
                name: "acc_documents");

            migrationBuilder.DropTable(
                name: "acc_fiscal_years");

            migrationBuilder.DropTable(
                name: "acc_account_groups");
        }
    }
}
