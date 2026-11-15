# سوابق تصمیمات معماری (Architecture Decision Records - ADR)

این بخش شامل مستندات ثبت تصمیمات مهم و استراتژیک معماری سیستم ERP است. هر تصمیم فنی عمده که بر ساختار کلی، عملکرد، امنیت یا هزینه نگهداری سیستم اثرگذار است، در قالب یک سند ساختاریافته ADR ثبت می‌گردد.

---

## فهرست تصمیمات ثبت‌شده (ADR Index)

| شناسه | عنوان تصمیم | وضعیت | تاریخ |
| :--- | :--- | :--- | :--- |
| [ADR-0001](0001-modular-monolith-architecture.md) | انتخاب الگوی معماری مدولار مونولیت (Modular Monolith) به جای مایکروسرویس | پذیرفته‌شده (Accepted) | 2024-11-14 |
| [ADR-0002](0002-multi-tenancy-shared-database-shared-schema.md) | راهبرد چندمستأجری: پایگاه داده مشترک با اسکیمای مشترک (Shared DB + Shared Schema) | پذیرفته‌شده (Accepted) | 2024-11-14 |
| [ADR-0003](0003-module-isolation-and-contracts.md) | ایزولاسیون کامل ماژول‌ها و ارتباط انحصاری بر پایه Contracts و Outbox | پذیرفته‌شده (Accepted) | 2024-11-14 |
| [ADR-0004](0004-postgresql-row-level-security-and-ef-core-query-filters.md) | استراتژی دو لایه ایزولاسیون: فیلترهای EF Core و PostgreSQL RLS | پذیرفته‌شده (Accepted) | 2024-11-14 |
| [ADR-0005](0005-utc-datetime-and-persian-calendar-strategy.md) | استاندارد ذخیره زمان بر پایه UTC و تقویم شمسی در لایه رابط کاربری | پذیرفته‌شده (Accepted) | 2024-11-14 |
| [ADR-0006](0006-accounting-immutability-and-double-entry.md) | تغییرناپذیری اسناد قطعی حسابداری و مسدودسازی فیزیکی حذف و ویرایش | پذیرفته‌شده (Accepted) | 2024-11-14 |

---

## ساختار و قالب استاندارد ADR

هر سند جدید باید از ساختار زیر پیروی کند:
- **Title (عنوان):** شناسه و نام تصمیم
- **Status (وضعیت):** Proposed / Accepted / Deprecated / Superseded
- **Context (زمینه و مساله):** تشریح چالش و چرایی نیاز به تصمیم‌گیری
- **Decision (تصمیم اتخاذشده):** راه‌حل انتخاب‌شده با جزئیات شفاف
- **Consequences (پیامدها):** نتایج مثبت، معایب و مصالحه‌های صورت گرفته (Trade-offs)
- **Compliance (نحوه تضمین رعایت):** روش بررسی رعایت تصمیم در کد (مثلاً Architecture Tests یا Code Review)
