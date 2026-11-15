# استراتژی و ممیزی ایزولاسیون مستاجران (Tenant Isolation Security)

این سند سازوکار دفاع در عمق (Defense in Depth) سیستم ERP را برای تفکیک کامل داده‌های مشتریان (Tenant Isolation) و شرکت‌های زیرمجموعه (Company Isolation) تشریح می‌کند.

---

## ۱. مدل چندمستاجره سازمانی (Hierarchical Multi-Tenancy)

ساختار سازمانی سیستم به شکل زیر است:
```text
Tenant (سازمان / مشتری SaaS)
  └── Company (شرکت حقوقی با دفاتر مالی مستقل)
        └── Branch (شعبه فیزیکی / عملیاتی)
              └── Warehouse (انبار یا مرکز هزینه)
```

---

## ۲. لایه‌های سه‌گانه تفکیک داده (Defense in Depth)

### لایه اول: اعتبارسنجی عضویت در خط لوله (`TenantResolutionMiddleware`)
- کلاینت شناسه‌های `X-Tenant-Id` و `X-Company-Id` را ارسال می‌کند.
- میدل‌ویر این شناسه‌ها را با داده‌های ذخیره‌شده عضویت کاربر (`UserCompanyMembership`) تطبیق می‌دهد:
  - اگر کاربر عضو فعال شرکت درخواستی نباشد و نقش `Admin` نداشته باشد، دسترسی او بلافاصله با خطای **403 Forbidden** و کد `Tenancy.ForbiddenMembership` مسدود می‌شود.
  - حتی اگر کاربر هدرها را دستکاری کند، کانتکست سرور به او اجازه عبور نمی‌دهد.

### لایه دوم: فیلترهای سراسری پایگاه‌داده (EF Core Global Query Filters)
- در کلاس پایه `BaseDbContext`، کوئری‌ها به صورت خودکار مقید به `CurrentTenantId` و `CurrentCompanyId` می‌شوند.
- در صورتی که کاربر Tenant A بخواهد اطلاعات رکوردهای Tenant B را با کوئری مستقیم شناسه (Id) دریافت کند، خروجی `null` خواهد بود (پنهان‌سازی کامل داده‌ها).

### لایه سوم: محافظ جهش‌های داده‌ای (Mutation Guards در `SaveChangesAsync`)
- حتی اگر در کدهای Application فیلتر کوئری دور زده شود (`IgnoreQueryFilters`) یا کدی تلاش کند یک موجودیت متعلق به Tenant B را متصل (Attach) کرده و تغییر (`Update`) یا حذف (`Delete`) کند:
- متد `SaveChangesAsync` در `BaseDbContext` تفاوت شناسه با `CurrentTenantId` فعال را کشف کرده و بلافاصله استثنای امنیتی `SecurityException` پرتاب می‌کند:
  ```text
  System.Security.SecurityException: Tenant violation: cannot Modified entity with TenantId '...' under active tenant '...'
  ```

---

## ۳. سناریوهای ممیزی امنیتی (Security Audit Scenarios)

| سناریو | تشریح حمله / رویداد | نتیجه مورد انتظار | وضعیت آزمون خودکار |
| :--- | :--- | :--- | :--- |
| **Scenario A** | کاربر مستاجر A درخواست داده‌های مستاجر B را ارسال کند | بازگشت `null` / خطای ۴۰۴ و عدم مشاهده اطلاعات | ✅ تایید شده در `ScenarioA_TenantIsolation_TenantACannotReadTenantBData` |
| **Scenario B** | کاربر شرکت A هدر شرکت B را ارسال کند | مسدودسازی با خطای ۴۰۳ Forbidden | ✅ تایید شده در `ScenarioB_CompanyIsolation_UserSendingUnauthorizedCompanyId_IsBlockedByMiddleware` |
| **Scenario C** | کاربر شعبه A تلاش کند به انبارهای شعبه B دسترسی یابد | پنهان‌سازی کامل انبارهای شعبه B | ✅ تایید شده در `ScenarioC_BranchIsolation_UserInBranchACannotSeeWarehousesOfBranchB` |
| **Scenario D** | دستکاری Route یا QueryString جهت تغییر Tenant | نادیده‌گرفتن و اولویت کانتکست امنیتی سرور | ✅ تایید شده در `ScenarioD_TamperResistance_QueryStringOrRouteTamperingCannotOverrideTenantContext` |
| **Scenario E** | تلاش برای دستکاری، ویرایش یا حذف رکورد مستاجر دیگر | توقف عملیات با پرتاب خطای `SecurityException` | ✅ تایید شده در `ScenarioE_MutationSecurityGuard_ModifyingOrDeletingAnotherTenantEntityThrowsSecurityException` |

---

## ۴. ایندکس‌های یکتا و Foreign Keyها
تمامی محدودیت‌های یکتا بودن داده‌ها (مانند کد حساب، کد کالا، شناسه ملی مشتری، کد شعبه) به صورت ترکیبی با `TenantId` و `CompanyId` تعریف می‌شوند تا شرکت‌ها بتوانند کدهای مشابه بدون تداخل در پایگاه‌داده اشتراکی داشته باشند.
