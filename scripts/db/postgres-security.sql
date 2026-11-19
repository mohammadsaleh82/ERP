-- ============================================================================
-- امنیت پایگاه‌داده PostgreSQL — ERP Accounting (PROMPT 09)
-- این اسکریپت پس از اجرای Migrationهای EF در محیط عملیاتی اجرا شود.
-- شامل: نقش‌های حداقل‌مجوز، RLS برای جداسازی Tenant، دفتر ممیزی Append-Only.
-- ============================================================================

-- ---------- ۱. نقش‌های محدود ----------
-- مهاجرت‌ها با نقش مالک/DDL اجرا می‌شوند؛ اپلیکیشن با نقش DML-محدود کار می‌کند.
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'erp_app') THEN
        CREATE ROLE erp_app LOGIN PASSWORD 'set-via-secret-store';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'erp_auditor') THEN
        -- فقط خواندن دفتر ممیزی برای ممیزان؛ حق تغییر ندارد
        CREATE ROLE erp_auditor LOGIN PASSWORD 'set-via-secret-store';
    END IF;
END $$;

-- ---------- ۲. RLS: جداسازی Tenant در سطح موتور ذخیره‌سازی ----------
-- زمینه هر درخواست با SET LOCAL app.tenant_id / app.company_id داخل تراکنش
-- ست می‌شود (Interceptor اپلیکیشن). Connection Pooling امن است چون SET LOCAL
-- با پایان تراکنش به‌طور خودکار ریست می‌شود (تست: RowLevelSecurityPostgresTests).
DO $$
DECLARE t text;
BEGIN
    FOREACH t IN ARRAY ARRAY[
        'acc_accounts','acc_account_templates','acc_documents','acc_document_lines',
        'acc_gl_entries','acc_gl_entry_dimensions','acc_fiscal_years','acc_fiscal_periods',
        'acc_fiscal_year_closings','acc_fiscal_year_reopenings','acc_financial_dimensions',
        'acc_dimension_policies','acc_reporting_classifications','acc_cash_flow_classifications',
        'acc_account_balance_snapshots','acc_audit_trail','acc_accounting_settings',
        'acc_accounting_settings_history','acc_voucher_sequences']
    LOOP
        IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = t) THEN
            EXECUTE format('ALTER TABLE %I ENABLE ROW LEVEL SECURITY', t);
            EXECUTE format($f$
                DROP POLICY IF EXISTS tenant_isolation ON %I;
                CREATE POLICY tenant_isolation ON %I
                    USING (tenant_id = current_setting('app.tenant_id', true)::uuid
                       AND company_id = current_setting('app.company_id', true)::uuid)
                    WITH CHECK (tenant_id = current_setting('app.tenant_id', true)::uuid
                            AND company_id = current_setting('app.company_id', true)::uuid)
            $f$, t, t);
        END IF;
    END LOOP;
END $$;

-- ---------- ۳. دفتر ممیزی Append-Only ----------
-- هیچ نقش کاربردی حق UPDATE/DELETE روی دفتر ممیزی ندارد + Trigger دفاع دوم.
CREATE OR REPLACE FUNCTION acc_audit_trail_block_mutation()
RETURNS trigger AS $$
BEGIN
    RAISE EXCEPTION 'acc_audit_trail is append-only: % is not allowed', TG_OP;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS acc_audit_trail_immutable ON acc_audit_trail;
CREATE TRIGGER acc_audit_trail_immutable
    BEFORE UPDATE OR DELETE ON acc_audit_trail
    FOR EACH ROW EXECUTE FUNCTION acc_audit_trail_block_mutation();

REVOKE UPDATE, DELETE ON acc_audit_trail FROM PUBLIC;
GRANT SELECT ON acc_audit_trail TO erp_auditor;

-- ---------- ۴. حداقل مجوز نقش اپلیکیشن ----------
-- (پس از RLS) اپلیکیشن فقط DML؛ DDL تنها برای نقش مهاجرت.
-- GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO erp_app;
-- GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO erp_app;
-- REVOKE UPDATE, DELETE ON acc_audit_trail FROM erp_app;
