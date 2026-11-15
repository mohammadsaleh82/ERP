# ساختار پوشه‌ها و پروژه‌های ریپازیتوری (Project & Repository Structure)

این سند درخت ساختار قطعی ریپازیتوری، محل قرارگیری هر مؤلفه، نقش هر لایه و قوانین جای‌گذاری فایل‌ها را برای معماری Modular Monolith سیستم ERP تعیین می‌کند.

> [!NOTE]
> **به‌روزرسانی معماری (Refinement Note):** بر اساس نیازمندی‌های فاز دوم و جهت بهینه‌سازی مرزهای اجرایی:
> 1. پروژه نقطه ورود وب از `ERP.Web` به `ERP.Api` تغییر نام یافت تا نشان‌دهنده ماهیت Headless RESTful API بک‌اند برای اتصال به کلاینت وب React باشد.
> 2. پروژه‌های پراکنده `SharedKernel` و ابسترکشن‌های مشترک در پروژه منسجم و مستقل `ERP.BuildingBlocks` متمرکز شدند.
> 3. پروژه‌های ماژول‌ها تحت ساختار متمرکز `src/ERP.Modules.{Name}` ایجاد شدند که درون هر ماژول ساختار لایه‌ای استاندارد (`Domain`, `Application`, `Infrastructure`, `Contracts`, `DependencyInjection.cs`) پیاده‌سازی شده است.
> 4. به پروژه‌های تست، پروژه اختصاصی `ERP.SecurityTests` جهت ارزیابی مداوم هدرهای امنیتی و کانتکست‌های چندمستأجری اضافه شد.

---

## ۱. درخت ساختار کلان ریپازیتوری

```text
ERP/
├── .editorconfig                  # استانداردهای استایل کد و نام‌گذاری
├── .gitignore                     # نادیده‌گیری فایل‌های بیلد و حساس
├── Directory.Build.props          # تنظیمات متمرکز کامپایلر دات‌نت (net10.0, Nullable, LangVersion)
├── Directory.Packages.props       # مدیریت متمرکز نسخه‌های NuGet (Central Package Management)
├── ERP.sln                        # فایل Solution جامع سیستم
├── global.json                    # نسخه قفل‌شده دات‌نت SDK (10.0.401)
├── README.md                      # راهنمای کلی سیستم و نحوه اجرا
│
├── docs/                          # اسناد معماری، تصمیمات و راهنماها
│   ├── architecture/
│   │   ├── adr/                   # Architecture Decision Records (ADR 0001 - 0006)
│   │   ├── architecture-overview.md
│   │   ├── technology-stack.md
│   │   ├── project-structure.md
│   │   ├── module-boundaries.md
│   │   ├── dependency-rules.md
│   │   ├── multi-tenancy-strategy.md
│   │   ├── security-architecture.md
│   │   ├── database-strategy.md
│   │   ├── accounting-architecture-principles.md
│   │   └── coding-standards.md
│   └── development/
│       └── backend-setup.md       # راهنمای اجرای خط فرمان و توسعه
│
├── src/                           # کدهای بک‌اند (.NET 10 / C# 14)
│   ├── ERP.Api/                   # Composition Root / نقطه ورود HTTP API
│   │   ├── Authentication/        # تنظیمات احراز هویت JWT
│   │   ├── Configuration/         # مدل‌های آپشن‌ها (JwtOptions)
│   │   ├── Controllers/           # کنترلرهای پایه و سلامت سیستم (BaseApiController, HealthController)
│   │   ├── Extensions/            # رجیستر کردن ماژول‌های دوازده‌گانه
│   │   ├── Middleware/            # ExceptionHandler و TenantResolutionMiddleware
│   │   ├── OpenApi/               # تنظیمات Swagger / OpenAPI
│   │   ├── Program.cs             # نقطه ورود هاست و خط لوله HTTP
│   │   ├── appsettings.json
│   │   ├── appsettings.Development.json
│   │   └── ERP.Api.csproj
│   │
│   ├── ERP.BuildingBlocks/        # هسته مشترک و مستقل (بدون هیچ وابستگی خارجی)
│   │   ├── Abstractions/          # اینترفیس‌های IEntity, IAggregateRoot, IClock
│   │   ├── Common/                # ابزارهای بررسی و تضمین مقادیر (Ensure)
│   │   ├── Events/                # اینترفیس‌های IDomainEvent, IIntegrationEvent, IEventDispatcher
│   │   ├── Exceptions/            # استثناهای استاندارد (AppException, ValidationException, NotFoundException)
│   │   ├── Observability/         # ثابت‌ها و فیلدهای لاگ‌برداری Serilog
│   │   ├── Persistence/           # پیاده‌سازی Entity, AggregateRoot, IAuditableEntity
│   │   ├── Results/               # الگوی نتیجه Result, Result<T>, Error, ErrorType
│   │   ├── Security/              # ثابت‌های کلیم‌ها (SecurityConstants) و اینترفیس ICurrentUser
│   │   ├── Tenancy/               # اینترفیس‌های مارکر ITenantScoped, ICompanyScoped, TenantContext
│   │   └── ERP.BuildingBlocks.csproj
│   │
│   ├── ERP.Modules.Identity/      # ماژول احراز هویت و دسترسی‌ها
│   ├── ERP.Modules.Tenancy/       # ماژول سازمان، هلدینگ، شرکت‌ها و شعب
│   ├── ERP.Modules.Accounting/    # ماژول حسابداری مالی، کدینگ و اسناد
│   ├── ERP.Modules.Treasury/      # ماژول خزانه‌داری، دریافت و پرداخت
│   ├── ERP.Modules.Inventory/     # ماژول انبار و کالا
│   ├── ERP.Modules.Purchasing/    # ماژول خرید و تدارکات
│   ├── ERP.Modules.Sales/         # ماژول فروش و بازرگانی
│   ├── ERP.Modules.Parties/       # ماژول اشخاص حقیقی و حقوقی
│   ├── ERP.Modules.Payroll/       # ماژول احکام و حقوق‌ودستمزد
│   ├── ERP.Modules.Reporting/     # ماژول گزارش‌گیری مالی
│   ├── ERP.Modules.Subscriptions/ # ماژول مدیریت لایسنس و اشتراک
│   └── ERP.Modules.Audit/         # ماژول رخدادنگاری و تاریخچه تغییرات
│
└── tests/                         # پروژه‌های آزمون نرم‌افزار
    ├── ERP.UnitTests/             # تست‌های واحد سریع با FluentAssertions
    ├── ERP.IntegrationTests/      # تست‌های یکپارچگی وب با WebApplicationFactory
    ├── ERP.ArchitectureTests/     # اعتبارسنجی قوانین معماری با NetArchTest.Rules
    └── ERP.SecurityTests/         # تست‌های امنیت، کوکی‌ها و هدرهای چندمستأجری
```

---

## ۲. کالبدشکافی استاندارد یک ماژول (Internal Module Architecture)

هر ماژول تجاری (مانند `ERP.Modules.Accounting`) دارای ساختار پوشه‌ای زیر است:

```text
src/ERP.Modules.Accounting/
├── Domain/                        # مدل‌ها، موجودیت‌ها، انوم‌ها، رویدادها، خطاهای دامنه
├── Application/                   # دستورات و پرس‌وجوها، اعتبارسنجی، سرویس‌های دامنه
├── Infrastructure/                # DbContext اختصاصی، پیکربندی جداول، مایگریشن‌ها
├── Contracts/                     # تنها بخش عمومی مورد استفاده سایر ماژول‌ها
│   ├── DTOs/                      # اشیاء انتقال داده اشتراکی
│   ├── Events/                    # رویدادهای عمومی یکپارچگی (Integration Events)
│   └── Interfaces/                # اینترفیس‌های قرارداد عمومی (مانند IAccountingModuleApi)
├── DependencyInjection.cs         # متد افزودن ماژول به کانتینر DI: AddAccountingModule()
├── README.md                      # مستندات و شناسنامه ماژول
└── ERP.Modules.Accounting.csproj
```

---

## ۳. نقش و مسئولیت لایه‌ها و پروژه‌ها

### ۳.۱. پروژه `ERP.Api` (Composition Root)
- وظیفه: اجرای هاست وب، بارگذاری تنظیمات، اجرای Middlewareها (خطاگیری، تعیین کانتکست مستأجر)، نگاشت کنترلرها، و رجیستر کردن سرویس‌های ماژول‌ها با فراخوانی متدهای `Add{Module}Module()`.
- فاقد هرگونه منطق تجاری یا انتیتی پایگاه داده است.

### ۳.۲. پروژه `ERP.BuildingBlocks`
- هسته سبک، مستقل و عاری از هرگونه منطق تجاری.
- تنها ارائه‌دهنده الگوهای پایه طراحی (`Result<T>`, `Entity<T>`, `ITenantScoped`, `Ensure`) برای استفاده در تمامی پروژه‌ها.

### ۳.۳. پروژه `ERP.ArchitectureTests`
- ضامن سلامت معماری با استفاده از پکیج `NetArchTest.Rules`.
- در هر بار اجرای تست، تضمین می‌کند که `ERP.BuildingBlocks` به هیچ ماژولی وابسته نباشد، کنترلرها در جای صحیح قرار گرفته باشند و مرزبندی ماژول‌ها نقض نگردد.
