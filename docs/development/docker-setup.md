# راهنمای استقرار کانتینری با Docker (Docker & DevOps Setup)

سیستم ERP به صورت بومی با کانتینرهای Docker و ارکستراسیون با Docker Compose طراحی شده است.

---

## ۱. فایل‌ها و نقش آن‌ها

- **[docker/Dockerfile.backend](file:///d:/ffff/MyProject/ERP/docker/Dockerfile.backend):**
  - چندمرحله‌ای (Multi-stage build) بر مبنای `mcr.microsoft.com/dotnet/sdk:10.0` و ران‌تایم `aspnet:10.0`.
  - اجرای امن تحت کاربر سیستمی غیر روت (`USER app`).
  - مجهز به HealthCheck توکار با `curl http://localhost:5000/api/health`.

- **[docker/Dockerfile.frontend](file:///d:/ffff/MyProject/ERP/docker/Dockerfile.frontend):**
  - چندمرحله‌ای با `node:22-alpine` جهت بیلد بهینه دارایی‌های استاتیک SPA.
  - سرویس‌دهی سبک با `nginx:1.27-alpine`.

- **[docker/nginx/nginx.conf](file:///d:/ffff/MyProject/ERP/docker/nginx/nginx.conf) و [conf.d/default.conf](file:///d:/ffff/MyProject/ERP/docker/nginx/conf.d/default.conf):**
  - نقطه ورود یکپارچه (API Gateway / Reverse Proxy).
  - مسیریابی `/api/` به سرور بک‌اند و سایر مسیرها به فرانت‌اند با Fallback به `index.html`.
  - اعمال فشرده‌سازی Gzip و هدرهای امنیتی (HSTS، X-Frame-Options، X-Content-Type-Options).

- **[docker-compose.yml](file:///d:/ffff/MyProject/ERP/docker-compose.yml):**
  - شامل سرویس‌های `postgres`, `erp-api`, `erp-frontend`, `nginx`.
  - اتصال سرویس‌ها از طریق شبکه داخلی `erp-internal`.
  - ذخیره‌سازی داده‌های دیتابیس در ولوم ماندگار `postgres_data`.

---

## ۲. دستورات اجرایی

### اعتبارسنجی ساختار فایل Compose
```bash
docker compose config
```

### اجرای کلیه سرویس‌ها
```bash
docker compose up -d --build
```

### مشاهده وضعیت کانتینرها
```bash
docker compose ps
```

### پایش لاگ‌ها
```bash
docker compose logs -f erp-api
docker compose logs -f nginx
```

### متوقف‌سازی
```bash
docker compose down
```
*(برای حذف ولوم داده‌ها: `docker compose down -v`)*
