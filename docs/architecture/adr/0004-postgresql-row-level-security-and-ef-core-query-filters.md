# ADR-0004: استراتژی دو لایه ایزولاسیون: فیلترهای EF Core و PostgreSQL RLS

## وضعیت
**پذیرفته‌شده (Accepted)**

## زمینه و مسئله (Context)
در مدل چندمستأجری با پایگاه داده مشترک، ایزولاسیون داده‌ها حیاتی‌ترین مسئله امنیتی و قانونی است. اتکای صرف به توسعه‌دهنده برای نوشتن `WHERE tenant_id = @tenant_id` خطای انسانی را قطعی می‌کند. استفاده از فیلترهای سراسری EF Core (`Global Query Filters`) گام بزرگی برای حل این مشکل است، اما توسعه‌دهندگان ممکن است برای برخی کوئری‌ها متد `.IgnoreQueryFilters()` را فراخوانی کرده و فیلتر را به صورت ناخواسته دور بزنند، یا در کوئری‌های خام Dapper/SQL مستقیم این فیلترها اعمال نشوند.

## تصمیم (Decision)
تصمیم گرفته شد که استراتژی **دفاع در عمق (Defense in Depth)** متشکل از دو لایه حفاظتی مستقل پیاده‌سازی شود:

1. **لایه اپلیکیشن (EF Core Global Query Filters):**
   - تمامی انتیتی‌های چندمستأجری از اینترفیس‌های `ITenantScoped` و `ICompanyScoped` ارث‌بری می‌کنند.
   - در متد `OnModelCreating` هر `DbContext`، فیلتر به صورت خودکار به مدل تزریق می‌شود.
2. **لایه پایگاه داده (PostgreSQL Row-Level Security - RLS):**
   - روی تمامی جداول مربوط به اطلاعات کسب‌وکار، قابلیت RLS فعال شده و پالیسی‌های محدودکننده (Restrictive Policies) تعریف می‌شود:
     ```sql
     CREATE POLICY tenant_isolation_policy ON acc_journal_vouchers
     FOR ALL
     USING (
         tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
         AND
         company_id = NULLIF(current_setting('app.current_company_id', true), '')::uuid
     );
     ```
   - در سطح دات‌نت، یک `DbConnectionInterceptor` به محض باز شدن هر کانکشن دیتابیس، مقادیر جاری را از کانتکست استخراج کرده و با دستورات زیر در سشن محلی ست می‌کند:
     ```sql
     SET LOCAL app.current_tenant_id = '<current_tenant_guid>';
     SET LOCAL app.current_company_id = '<current_company_guid>';
     ```

## پیامدها و مصالحه‌ها (Consequences)

### پیامدهای مثبت (Pros):
- **امنیت قطعی داده‌ها:** حتی اگر کدی با SQL تزریق‌شده یا فراخوانی اشتباه `.IgnoreQueryFilters()` اجرا شود، موتور دیتابیس PostgreSQL از بازگرداندن رکوردهای مستأجر دیگر ممانعت به عمل می‌آورد.
- استقلال امنیت از خطای انسانی برنامه‌نویس در لایه Backend.

### مصالحه‌ها و معایب (Cons / Risks):
- سربار بسیار اندک در زمان اجرای کوئری در دیتابیس (کمتر از ۱ تا ۲ درصد به دلیل بهینه‌سازی موتور RLS در نسخه‌های جدید Postgres).
- نیاز به مدیریت دقیق کانکشن‌ها در تست‌های یکپارچگی جهت اطمینان از مقداردهی درست متغیرهای محلی `SET LOCAL`.
