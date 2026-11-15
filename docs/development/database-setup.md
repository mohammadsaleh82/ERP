# راهنمای پایگاه‌داده و مدیریت Migrationها (Database Setup & Migrations)

این سند معماری پایگاه‌داده PostgreSQL، سیاست‌های EF Core 10 و راهنمای ساخت و اجرای Migrationها را برای سیستم ERP تشریح می‌کند.

---

## ۱. مشخصات و سیاست‌های داده‌ای

- **Database Engine:** PostgreSQL 17 (با استفاده از پکیج `Npgsql.EntityFrameworkCore.PostgreSQL`)
- **Naming Convention:** اسامی جداول و ستون‌ها تماماً به صورت `snake_case` تولید می‌شوند (`EFCore.NamingConventions`).
- **UTC DateTime Policy:** تمامی ستون‌های زمان به صورت صریح با `DateTimeKind.Utc` ذخیره و خوانده می‌شوند.
- **Financial Decimal Policy:** تمامی ستون‌های مالی از نوع `numeric(28, 4)` تنظیم شده‌اند تا دقت ریالی و ارزی دقیق حفظ شود.
- **Audit Columns:** موجودیت‌های `IAuditableEntity` به صورت خودکار مقادیر `created_at_utc`, `created_by`, `last_modified_at_utc`, `last_modified_by` دریافت می‌کنند.
- **Optimistic Concurrency:** برای جلوگیری از رونویسی همزمان رکوردهای مالی و اسناد.

---

## ۲. جداسازی ماژولار DbContextها و تفکیک جدول Migration

هر ماژول مالک انحصاری DbContext و جداول خود است:

| ماژول | نام DbContext | پیشوند جداول | جدول تاریخچه Migration |
| :--- | :--- | :--- | :--- |
| **Identity** | `IdentityDbContext` | `sec_` | `__EFMigrationsHistory_Identity` |
| **Tenancy** | `TenancyDbContext` | `org_` | `__EFMigrationsHistory_Tenancy` |
| **Accounting** | `AccountingDbContext` | `acc_` | `__EFMigrationsHistory_Accounting` |
| **Treasury** | `TreasuryDbContext` | `trs_` | `__EFMigrationsHistory_Treasury` |
| **Inventory** | `InventoryDbContext` | `inv_` | `__EFMigrationsHistory_Inventory` |

> **قانون طلایی:** هیچ ماژولی حق ایجاد کلید خارجی فیزیکی (Foreign Key) به جداول ماژول دیگر را ندارد. ارتباطات بین‌ماژولی صرفاً بر مبنای `Guid` یا رویدادها است.

---

## ۳. دستورات ایجاد و اجرای Migration

برای ایجاد Migration برای هر ماژول، پروژه `ERP.Api` به عنوان پروژه Startup مشخص می‌شود:

### الف) ماژول Tenancy
```bash
dotnet ef migrations add InitialTenancy \
  --project src/ERP.Modules.Tenancy \
  --startup-project src/ERP.Api \
  --context TenancyDbContext \
  --output-dir Infrastructure/Persistence/Migrations
```

### ب) ماژول Identity
```bash
dotnet ef migrations add InitialIdentity \
  --project src/ERP.Modules.Identity \
  --startup-project src/ERP.Api \
  --context IdentityDbContext \
  --output-dir Infrastructure/Persistence/Migrations
```

### ج) اعمال Migrationها در محیط Production
> **نکته امنیتی بسیار مهم:** در محیط‌های Production اجرای `Database.Migrate()` در زمان Startup اپلیکیشن ممنوع است زیرا در سناریوهای Multi-Instance منجر به تداخل و Deadlock می‌شود. Migrationها باید از طریق خطوط CI/CD، اسکریپت‌های SQL تولیدشده (`dotnet ef migrations script --idempotent`) یا ابزارهای مجزا اجرا شوند.

---

## ۴. ارتباط محلی پایگاه‌داده

کانکشن استرینگ پیش‌فرض در فایل `appsettings.Development.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=erp_db;Username=erp_user;Password=erp_secure_password_change_in_prod"
  }
}
```
جهت بالا آوردن نمونه PostgreSQL در محیط لوکال:
```bash
docker compose up -d postgres
```
