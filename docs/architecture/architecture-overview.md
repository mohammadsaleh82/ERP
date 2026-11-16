# سند معماری سیستم: بررسی اجمالی معماری (Architecture Overview)

## ۱. معرفی و هدف سیستم

پروژه **ERP** یک سیستم جامع مدیریت منابع سازمانی (Enterprise Resource Planning) با تمرکز بر مدیریت مالی، بازرگانی، انبارداری و منابع انسانی برای شرکت‌های چندشعبه‌ای و هلدینگ‌ها در قالب یک سرویس ابری چندمشتریه (Multi-Tenant SaaS) است.

هدف از این سند، ترسیم خطوط راهنما، اصول مهندسی نرم‌افزار، ساختار کلان و تصمیمات پایه‌ای معماری سیستم است تا تضمین شود نرم‌افزار ضمن حفظ حداکثر سادگی در استقرار و نگهداری، دارای بیشترین قابلیت اطمینان، امنیت داده، آزمون‌پذیری و انعطاف در توسعه تیمی باشد.

---

## ۲. مشخصات قطعی و اصول راهبردی

| شاخص | مقدار مصوب |
| :--- | :--- |
| **نام محصول** | ERP |
| **نوع معماری** | مدولار مونولیت (Modular Monolith) |
| **مدل ارائه‌دهی** | نرم‌افزار به عنوان خدمت (Multi-Tenant SaaS) |
| **بک‌اند** | .NET 10 / C# 14 / ASP.NET Core 10 |
| **فرانت‌اند** | React 19 / TypeScript / Vite / Tailwind CSS / shadcn/ui |
| **پایگاه داده** | PostgreSQL 16+ به همراه Npgsql و EF Core 10 |
| **جداسازی مستأجرها** | پایگاه داده مشترک با تفکیک منطقی و حفاظتی (Shared Database + Shared Schema) با آمادگی برای Database-Per-Tenant |
| **جهت و زبان UI** | زبان فارسی، کاملاً راست‌به‌چپ (RTL)، تقویم شمسی (Jalali) |
| **استاندارد داده در Backend** | زمان‌ها کاملاً استاندارد بر پایه UTC و اعداد مالی به صورت با دقت بالا (High Precision Decimal) |

---

## ۳. الگوی معماری: مدولار مونولیت (Modular Monolith)

### ۳.۱. چرا مدولار مونولیت و نه مایکروسرویس؟
در پروژه‌های ERP سازمانی، به‌ویژه در بخش‌های مالی، انبار و فروش، **یکپارچگی تراکنشی (Transactional Consistency)** و صحت روابط داده‌ای اولویت مطلق دارند. معماری مایکروسرویس (Microservices) چالش‌های سنگینی مانند تراکنش‌های توزیع‌شده (2PC / Sagas)، تأخیر شبکه (Network Latency)، پیچیدگی شکست و مانیتورینگ توزیع‌شده، و هزینه سرسام‌آور DevOps و زیرساخت را تحمیل می‌کند.

در مقابل، **Modular Monolith**:
1. **استقرار یکپارچه (Single Deployment Unit):** هزینه عملیاتی نگهداری و استقرار پایین است (یک کانتینر Web API + کانتینر Frontend + پایگاه داده PostgreSQL).
2. **مرزهای شفاف ماژولار (Strict Module Boundaries):** هر دامنه تجاری (Accounting، Inventory، Sales و ...) مانند یک مینی‌اپلیکیشن با پایگاه داده منطقی، قوانین اعتبارسنجی و لایه‌بندی اختصاصی خود عمل می‌کند.
3. **تراکنش‌های محلی ACID:** در داخل مرزهای معین می‌توان از تراکنش‌های دیتابیس استفاده کرد بدون نیاز به سازگاری نهایی پیچیده در تراکنش‌های حساس مالی.
4. **قابلیت تفکیک آتی (Extractable):** اگر در آینده ماژولی (مانند Reporting یا Inventory) نیاز به مقیاس‌پذیری مستقل پیدا کند، به دلیل عدم وابستگی مستقیم و ارتباط بر پایه قراردادها (Contracts)، با کمترین تغییر به یک میکروسرویس مجزا تبدیل خواهد شد.

### ۳.۲. نمودار کلان سیستم (High-Level Architecture)

```mermaid
flowchart TD
    Client["مرورگر کاربر (React + Vite + shadcn/ui - RTL)"]
    
    subgraph Infrastructure_Host ["محیط میزبانی و شبکه (Docker & Nginx)"]
        Nginx["Nginx Reverse Proxy / SSL Termination / Static Files"]
        
        subgraph Web_Application ["برنامه کاربردی واحد (Modular Monolith - ASP.NET Core 10)"]
            CompositionRoot["Composition Root / Web Host (ERP.Web)"]
            Middleware["Middlewares (Auth, Multi-Tenant Resolver, Serilog, ErrorHandling)"]
            
            subgraph Core_Modules ["ماژول‌های مستقل تجاری (Business Modules)"]
                IAM["Identity & Access"]
                Tenancy["Tenancy & Organization"]
                Acc["Accounting"]
                Treasury["Treasury"]
                Inv["Inventory"]
                Purchasing["Purchasing"]
                Sales["Sales"]
                Parties["Contacts & Parties"]
                Payroll["Payroll"]
                Reporting["Reporting"]
                Sub["Subscription & Licensing"]
                Audit["Audit & Logging"]
            end
            
            subgraph Shared_Kernel ["هسته مشترک (بدون منطق تجاری)"]
                Kernel["ERP.SharedKernel (Abstractions, Result, Domain Events, IDs)"]
            end
            
            subgraph In_Process_Bus ["ارتباطات درون‌برنامه‌ای"]
                Mediation["In-Memory Mediator & Domain Event Dispatcher"]
                Outbox["Transactional Outbox Processor"]
            end
        end
        
        subgraph Storage ["لایه ذخیره‌سازی و پایداری داده"]
            PostgreSQL[(PostgreSQL 16+\nRow Level Security + Shared Schema)]
        end
    end

    Client -->|HTTPS / REST API| Nginx
    Nginx -->|Proxy Pass| CompositionRoot
    CompositionRoot --> Middleware
    Middleware --> Core_Modules
    
    Core_Modules -.->|References Abstractions| Kernel
    Core_Modules -->|Publishes Events / Outbox| In_Process_Bus
    In_Process_Bus -.->|Dispatches| Core_Modules
    
    IAM -->|Dedicated Schema/Tables| PostgreSQL
    Tenancy -->|Dedicated Schema/Tables| PostgreSQL
    Acc -->|Dedicated Schema/Tables| PostgreSQL
    Inv -->|Dedicated Schema/Tables| PostgreSQL
    Sales -->|Dedicated Schema/Tables| PostgreSQL
    Purchasing -->|Dedicated Schema/Tables| PostgreSQL
    Treasury -->|Dedicated Schema/Tables| PostgreSQL
    Parties -->|Dedicated Schema/Tables| PostgreSQL
    Payroll -->|Dedicated Schema/Tables| PostgreSQL
```

---

## ۴. ویژگی‌های کیفی معماری (Quality Attributes)

### ۴.۱. امنیت و ایزولاسیون اطلاعات (Security & Isolation)
- ایزولاسیون کامل سطح مستأجر (Tenant) و شرکت (Company) با روش **Defense in Depth**:
  1. فیلترهای سراسری در سطح ORM (`EF Core Global Query Filters`).
  2. محافظت قطعی در سطح پایگاه داده با `PostgreSQL Row Level Security (RLS)`.
- احراز هویت توکن‌محور بدون وضعیت (Stateless JWT) به همراه اعتبارسنجی مداوم کوکی Refresh Token.
- تفکیک نقش‌ها و دسترسی‌های ریزدانه‌ای (Fine-Grained Permissions) به ازای هر شرکت.

### ۴.۲. آزمون‌پذیری (Testability)
- تفکیک کامل منطق دامنه (Domain) از زیرساخت (Infrastructure).
- قابلیت اجرای تست‌های واحد (Unit Tests) برای صد درصد منطق‌های مالی و کسب‌وکار بدون نیاز به دیتابیس.
- قابلیت اجرای تست‌های یکپارچگی (Integration Tests) بر روی کانتینر واقعی پایگاه داده با استفاده از `Testcontainers`.
- اجرای خودکار تست‌های قوانین معماری (`Architecture Tests` با NetArchTest) در خط CI/CD جهت جلوگیری از نقض مرزهای ماژول‌ها.

### ۴.۳. قابلیت نگهداری و مقیاس‌پذیری (Maintainability & Scalability)
- عدم ایجاد کلاس‌های بزرگ نامتقارن (God Objects) و جلوگیری از تجمع منطق تجاری در لایه‌های اشتراکی (`ERP.SharedKernel`).
- استقرار به کمک Docker Compose و پشتیبانی از ابزارهای مانیتورینگ متمرکز (Structured Logging با Serilog).
- پشتیبانی از سیستم صف تراکنشی داخلی (Transactional Outbox) جهت آماده‌سازی سیستم برای رویدادهای ناهمگام و پردازش‌های پس‌زمینه.

---

## ۵. نقشه راه تکاملی معماری

```mermaid
flowchart LR
    Phase1["فاز ۱: مدولار مونولیت پایه\n- تک دیتابیس با تفکیک فیلدی\n- استقرار با داکر کامپوز\n- ارتباط درون‌حافظه‌ای"]
    Phase2["فاز ۲: ارتقای کارایی و امنیت\n- فعال‌سازی PostgreSQL RLS\n- کش توزیع‌شده Redis\n- صف پردازش پس‌زمینه"]
    Phase3["فاز ۳: مقیاس چندبانکی\n- پایگاه داده مجزا برای مستأجرهای سازمانی (DB-Per-Tenant)\n- استخراج ماژول‌های پربار در صورت نیاز"]

    Phase1 --> Phase2
    Phase2 --> Phase3
```

این سند اساس تمام تصمیمات فنی و مهندسی بعدی سیستم ERP را تشکیل می‌دهد و هرگونه تغییر در آن نیازمند ثبت سند تصمیم معماری (ADR) خواهد بود.
