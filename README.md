# سامانه جامع سازمانی ERP (Enterprise Resource Planning)

سیستم جامع ERP سازمانی مبتنی بر معماری **Modular Monolith** با رویکرد **Multi-Tenant SaaS**، طراحی‌شده برای سازمان‌های هلدینگی، شرکت‌های چندشعبه‌ای و موسسات تجاری و مالی.

---

## ۱. مشخصات کلیدی سیستم و استک فنی

- **Backend:** ASP.NET Core 10 / C# 14
- **Database:** PostgreSQL 17 به همراه EF Core 10 و درایور Npgsql
- **Architecture:** Modular Monolith با جداسازی ۱۲ ماژول مستقل
- **Multi-Tenancy:** ساختار سلسه‌مراتبی `Tenant -> Company -> Branch -> Warehouse` با فیلترهای سراسری و گارد‌های امنیتی جهش داده‌ها در `SaveChangesAsync`
- **Security & Auth:** ASP.NET Core Identity + JWT Bearer + Refresh Token امن با چرخش خودکار و کشف سرقت نشست (Reuse Detection) + مجوزدهی پویا `[HasPermission]`
- **Frontend:** React 19 + TypeScript 5.7+ (Strict) + Vite 6 + Tailwind CSS 4 + shadcn/ui + تقویم فارسی و RTL
- **Containerization & Gateway:** Docker Compose + Nginx Alpine Reverse Proxy

---

## ۲. ساختار سورس‌کد (Solution & Project Structure)

```text
ERP/
├── src/
│   ├── ERP.Api/                 # Composition Root و نقطه ورود HTTP API
│   ├── ERP.BuildingBlocks/      # هسته مشترک زیرساختی (بدون وابستگی به ماژول‌ها)
│   ├── ERP.Modules.Identity/    # احراز هویت، توکن‌ها و امنیت
│   ├── ERP.Modules.Tenancy/     # ساختار هلدینگ، شرکت‌ها، شعب و انبارها
│   ├── ERP.Modules.Accounting/  # حسابداری مالی، کدینگ و اسناد
│   ├── ERP.Modules.Treasury/    # خزانه‌داری، بانک و نقدینگی
│   ├── ERP.Modules.Inventory/   # انبار، کالا و گردش موجودی
│   ├── ERP.Modules.Purchasing/  # خرید و تدارکات
│   ├── ERP.Modules.Sales/       # فروش و بازرگانی
│   ├── ERP.Modules.Parties/     # طرف‌های تجاری (اشخاص حقیقی و حقوقی)
│   ├── ERP.Modules.Payroll/     # حقوق و دستمزد
│   ├── ERP.Modules.Reporting/   # گزارشات مالی و مدیریتی
│   ├── ERP.Modules.Subscriptions/# پلن‌ها و اشتراک‌های مستاجران
│   └── ERP.Modules.Audit/       # لاگ تغییرات و فعالیت‌ها
├── tests/
│   ├── ERP.UnitTests/           # تست‌های پایه BuildingBlocks
│   ├── ERP.ArchitectureTests/   # آزمون‌های معماری با NetArchTest (۱۷ تست)
│   ├── ERP.SecurityTests/       # تست‌های امنیتی و سناریوهای ۵گانه Multi-Tenant (۱۵ تست)
│   └── ERP.IntegrationTests/    # تست‌های یکپارچگی، ایزولاسیون و سیاست‌های دیتابیس (۶ تست)
├── frontend/
│   └── erp-client/              # اپلیکیشن React 19 + TypeScript + Vite + Tailwind 4
├── docker/                      # داکرفایل‌ها و تنظیمات پروکسی Nginx
└── docs/                        # مستندات کامل معماری، توسعه و امنیت
```

---

## ۳. نحوه راه‌اندازی و اجرای گام‌به‌گام (Step-by-Step Setup)

### روش اول: اجرای با Docker Compose (پیشنهادی برای شبیه‌سازی کامل Production)
```bash
# اعتبارسنجی کانفیگ
docker compose config

# بیلد و بالا آوردن کلیه سرویس‌ها (Postgres, Backend API, Frontend, Nginx)
docker compose up -d --build
```
دسترسی‌ها:
- درگاه وب و فرانت‌اند: [http://localhost](http://localhost)
- اندپوینت‌های بک‌اند: [http://localhost/api/v1/...](http://localhost/api/v1/)
- پایش سلامت: [http://localhost/api/health](http://localhost/api/health)

---

### روش دوم: اجرای دستی در محیط توسعه (Local Development)

#### ۱. بالا آوردن پایگاه داده PostgreSQL
```bash
docker compose up -d postgres
```

#### ۲. کامپایل و اجرای Backend API
```bash
dotnet restore ERP.sln
dotnet build ERP.sln --no-restore
dotnet run --project src/ERP.Api
```
> سرور API روی پورت `http://localhost:5000` آماده به کار خواهد بود و مستندات تعاملی Swagger در آدرس [http://localhost:5000/swagger](http://localhost:5000/swagger) در دسترس است.

#### ۳. کامپایل و اجرای Frontend
در یک پنجره ترمینال جدید:
```bash
cd frontend/erp-client
npm install
npm run dev
```
> اپلیکیشن کلاینت روی آدرس `http://localhost:5173` باز می‌شود.

---

## ۴. اجرای آزمون‌های خودکار کیفیت و امنیت

تمام ۴۰ آزمون خودکار به صورت تضمین‌شده پاس می‌شوند:

```bash
# اجرای کل آزمون‌های بک‌اند (Unit, Architecture, Security, Integration)
dotnet test ERP.sln

# اجرای کنترل کیفیت فرانت‌اند
cd frontend/erp-client
npm run typecheck
npm run lint
npm run build
```

---

## ۵. نقشه مستندات (Documentation Map)

برای مطالعه جزئیات بیشتر به مستندات موجود در پوشه `docs/` مراجعه کنید:
- **معماری:** [docs/architecture/architecture-overview.md](file:///d:/ffff/MyProject/ERP/docs/architecture/architecture-overview.md)
- **شروع سریع:** [docs/development/getting-started.md](file:///d:/ffff/MyProject/ERP/docs/development/getting-started.md)
- **راهنمای بک‌اند:** [docs/development/backend-setup.md](file:///d:/ffff/MyProject/ERP/docs/development/backend-setup.md)
- **راهنمای فرانت‌اند:** [docs/development/frontend-setup.md](file:///d:/ffff/MyProject/ERP/docs/development/frontend-setup.md)
- **پایگاه‌داده و Migration:** [docs/development/database-setup.md](file:///d:/ffff/MyProject/ERP/docs/development/database-setup.md)
- **راهنمای داکر:** [docs/development/docker-setup.md](file:///d:/ffff/MyProject/ERP/docs/development/docker-setup.md)
- **راهنمای تست:** [docs/development/testing-guide.md](file:///d:/ffff/MyProject/ERP/docs/development/testing-guide.md)
- **ایزولاسیون مستاجران:** [docs/security/tenant-isolation.md](file:///d:/ffff/MyProject/ERP/docs/security/tenant-isolation.md)
- **احراز هویت و توکن‌ها:** [docs/security/authentication.md](file:///d:/ffff/MyProject/ERP/docs/security/authentication.md)
- **سطوح دسترسی:** [docs/security/authorization.md](file:///d:/ffff/MyProject/ERP/docs/security/authorization.md)
- **وضعیت آمادگی پروژه:** [docs/project-status.md](file:///d:/ffff/MyProject/ERP/docs/project-status.md)
