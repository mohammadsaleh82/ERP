# راهنمای شروع سریع (Getting Started Guide)

این سند راهنمای گام‌به‌گام راه‌اندازی سریع محیط توسعه سیستم ERP سازمانی برای توسعه‌دهندگان جدید است.

---

## ۱. نیازمندی‌های سیستم (Prerequisites)

برای اجرای محلی سیستم به ابزارهای زیر نیاز دارید:
- **.NET SDK:** نسخه 10.0 یا بالاتر (`dotnet --version`)
- **Node.js & npm:** نسخه Node.js v22.x LTS و npm 11.x (`node -v` و `npm -v`)
- **Docker & Docker Compose:** نسخه Docker Desktop یا Docker Engine سازگار با لینوکس/ویندوز
- **PostgreSQL Client (اختیاری):** ابزارهایی مانند pgAdmin یا DBeaver

---

## ۲. راه‌اندازی سریع در ۵ دقیقه (Quick Start)

### گام اول: شبیه‌سازی یا دریافت سورس پروژه
```bash
git clone <repository-url>
cd ERP
```

### گام دوم: بالا آوردن پایگاه‌داده و سرویس‌های پایه با Docker
```bash
docker compose up -d postgres
```
> پایگاه‌داده PostgreSQL 17 بر روی پورت `5432` با پایگاه‌داده `erp_db` و کاربر `erp_user` در دسترس قرار می‌گیرد.

### گام سوم: اجرای Backend
```bash
dotnet restore
dotnet build ERP.sln
dotnet run --project src/ERP.Api
```
> سرور API روی آدرس `http://localhost:5000` آماده به کار خواهد بود و مستندات OpenAPI / Swagger در آدرس زیر در دسترس است:
> [http://localhost:5000/swagger](http://localhost:5000/swagger)

### گام چهارم: اجرای Frontend
در ترمینال مجزا:
```bash
cd frontend/erp-client
npm install
npm run dev
```
> فرانت‌اند روی پورت `http://localhost:5173` باز شده و به صورت پیش‌فرض با پروکسی محلی به API بک‌اند متصل می‌گردد.

---

## ۳. اجرای کامل با Docker Compose (محیط شبیه‌ساز Production)

جهت اجرای کامل هر ۳ سرویس (PostgreSQL + Backend API + Frontend SPA + Nginx Gateway):
```bash
docker compose up --build
```
دسترسی‌ها:
- **درگاه اصلی سامانه (Nginx Reverse Proxy):** [http://localhost](http://localhost)
- **نقاط انتهایی API:** [http://localhost/api/v1/...](http://localhost/api/v1/)
- **بررسی سلامت سیستم (Health Check):** [http://localhost/api/health](http://localhost/api/health)

---

## ۴. اجرای آزمون‌های خودکار کیفیت و امنیت

جهت اطمینان از سلامت پروژه و عدم وجود هرگونه رگرسیون معماری:
```bash
# اجرای آزمون‌های بک‌اند (شامل Architecture, Security, Integration, Unit)
dotnet test ERP.sln

# بررسی تایپ‌اسکریپت و لینتر فرانت‌اند
cd frontend/erp-client
npm run typecheck
npm run lint
npm run build
```
