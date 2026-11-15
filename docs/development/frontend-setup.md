# راهنمای راه‌اندازی و معماری فرانت‌اند (Frontend Setup & Architecture Guide)

پروژه فرانت‌اند ERP با رویکرد **Feature-Based Architecture** و با استفاده از جدیدترین تکنولوژی‌های اکوسیستم React در مسیر `frontend/erp-client/` طراحی شده است.

---

## ۱. مشخصات فنی فرانت‌اند

- **Core Library:** React 19 (Hooks, Concurrent Features)
- **Language:** TypeScript 5.7+ (با حالت `strict: true`)
- **Build Tool:** Vite 6
- **Styling:** Tailwind CSS 4 + shadcn/ui
- **State Management:** Zustand 5 (عدم نگهداری توکن‌های حساس در `localStorage`؛ مدیریت توکن در حافظه)
- **Server Cache & Data Fetching:** TanStack Query v5 (React Query)
- **HTTP Client:** Axios با رهگیری متمرکز (Interceptors)، کنترل Mutex برای Refresh Token و فرمت خطای RFC 7807
- **Routing:** React Router v7 با Protected Routes و Permission Guards
- **Form Handling:** React Hook Form + Zod
- **Calendar & Dates:** تقویم جلالی (`date-fns-jalali`) و سازگاری کامل با جهت راست‌به‌چپ (RTL)

---

## ۲. ساختار پوشه‌ها و Feature Isolation

```text
frontend/erp-client/
├── src/
│   ├── app/                 # هسته نرم‌افزار (Routing, Providers, Root Layout)
│   │   ├── layouts/         # ساختار صفحات (Sidebar, Header, Main, RTL Wrapper)
│   │   ├── providers/       # ThemeProvider, QueryProvider, AuthProvider
│   │   └── router/          # تعریف مسیرها، ProtectedRoute، PermissionGuard
│   ├── core/                # زیرساخت مشترک هسته
│   │   ├── api/             # کلاینت مرکزی Axios، اینترسپتورها، مدیریت ProblemDetails
│   │   ├── auth/            # Zustand Auth Store (in-memory token)، توابع احراز هویت
│   │   ├── tenant/          # Tenant Context & Company Switcher State
│   │   └── permissions/     # Permission checking utilities
│   ├── shared/              # اجزای اشتراکی بدون وابستگی به فیچرها
│   │   ├── components/ui/   # کامپوننت‌های پایه (Button, Badge, Input, Card, Modal)
│   │   ├── hooks/           # useDebounce, useMediaQuery, useDisclosure
│   │   └── utils/           # فرمت‌کننده‌های ریال، تاریخ شمسی، ترکیب کلاس‌ها (cn)
│   └── features/            # ماژول‌های تجاری ایزوله
│       ├── identity/        # صفحات لاگین، پروفایل، تغییر رمز عبور
│       ├── organization/    # سوییچر سازمان، مدیریت شرکت‌ها و شعب
│       ├── accounting/      # ماژول حسابداری (آماده توسعه)
│       └── treasury/        # ماژول خزانه‌داری (آماده توسعه)
```

---

## ۳. دستورات اجرایی

```bash
cd frontend/erp-client

# ۱. نصب پکیج‌ها
npm install

# ۲. اجرای محیط توسعه
npm run dev

# ۳. کنترل انواع داده (TypeScript Strict Typecheck)
npm run typecheck

# ۴. اعتبارسنجی استانداردهای کدنویسی (ESLint)
npm run lint

# ۵. کامپایل نهایی پروداکشن
npm run build
```

---

## ۴. استانداردهای امنیت در کلاینت (Security Best Practices)

1. **عدم ذخیره توکن در LocalStorage:** کلید `accessToken` صرفاً در حافظه رم (Zustand state) نگهداری می‌شود تا از حملات XSS جلوگیری به عمل آید.
2. **Refresh Token ایمن:** تمدید نشست از طریق کوکی‌های HttpOnly/Secure یا کانال اختصاصی `withCredentials: true` انجام می‌گیرد.
3. **اعمال هدرهای سازمانی:** در تمام درخواست‌ها هدرهای `X-Tenant-Id` و `X-Company-Id` متناسب با شرکت انتخابی در سوییچر تزریق می‌شوند.
