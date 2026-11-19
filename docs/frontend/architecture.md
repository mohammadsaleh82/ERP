# معماری فرانت‌اند ERP — فاز ۱ (Frontend Foundation)

مسیر پروژه: `frontend/erp-client` — Stack: React 19 + Vite 6 + TypeScript 5 (strict) + Tailwind CSS 4 + shadcn/ui (Radix) + TanStack Query 5 + Zustand 5 + Axios + React Hook Form + Zod + Recharts 3 + Vitest/RTL + Playwright.

## ۱. نتیجه ممیزی وضعیت موجود (مرحله اول پرامپت)

پروژه پایه از پرامپت‌های قبلی موجود بود و بازنویسی نشد؛ ممیزی نشان داد:

| بند | وضعیت | اقدام فاز ۱ |
| --- | --- | --- |
| ساختار Feature-Based (app/core/shared/features) | ✅ موجود و سالم | حفظ کامل — هیچ بازنویسی ناعادلانه انجام نشد |
| Router مرکزی + سه ناحیه (public/pre-company/protected) + Guards | ✅ موجود | حفظ |
| Axios instance + Base URL از env + Bearer + هدرهای Tenant/Company/Branch + Refresh token صف‌شده | ✅ موجود | حفظ + تست |
| QueryProvider (TanStack Query) + Toaster (sonner) | ✅ موجود | حفظ |
| Error Boundary | ❌ نبود | پیاده‌سازی شد (`app/errors/error-boundary.tsx` + اتصال در `App.tsx`) |
| تست‌ها | ❌ صفر تست | Vitest + RTL + jsdom + Playwright نصب؛ ۲۸ تست واحد + ۲ تست E2E |
| کتابخانه نمودار | ❌ نبود | Recharts 3 + ظرف استاندارد `ChartContainer` (RTL) |
| مستندات معماری | ❌ نبود | همین سند |

## ۲. ساختار پوشه‌ها و نگاشت با ساختار پیشنهادی پرامپت

ساختار پیشنهادی پرامپت به یک تختِ (flat) لایه‌ای بود؛ ما نگاشت Feature-Based زیر را که همه نقش‌ها را پوشش می‌دهد نگه داشتیم (تصمیم: هر «سرویس/hook/store/type» در جای مسئول خودش است و کپی لایه تختِ بدون محتوا اضافه نشد):

| پیشنهاد پرامپت | محل واقعی | توضیح |
| --- | --- | --- |
| app | `src/app/` | Router، Guards، Layouts، Providers، config/env، errors |
| core | `src/core/` | api client، auth، company، notifications، errors — منطق چارچوبی مشترک |
| shared | `src/shared/` | ui (shadcn)، layout، forms، tables، feedback، charts، utils |
| features | `src/features/<module>/` | صفحات + index؛ در فازهای بعد: components/pages/hooks/services/schemas/types داخل هر feature |
| layouts | `src/app/layouts/` | AppLayout / AuthLayout |
| routes | `src/app/router/` + Guards | تعریف یکپارچه مسیرها با سه ناحیه |
| services / hooks / stores / types / utils / config | core + shared + داخل featureها |stores در `core/*/​*.store.ts`، utils در `shared/utils/`، env در `app/config/` |
| assets | `src/styles/` (theme/typography/globals) + `index.html` | فونت‌ها و متغیرهای CSS |
| tests | `tests/` | unit + e2e + setup |

## ۳. قواعد وابستگی (یک‌طرفه)

```
features → shared → core → app
features ⇸ features (فقط از طریق index عمومی و در صورت لزوم؛ ترجیح: صفر)
app → core + shared
core ⇸ features  (قانون سفت)
```

- `core` هیچ ایمپورتی از `features` ندارد (بررسی با ESLint در فاز بعد؛ فعلاً قرارداد).
- APIهای عمومی هر feature فقط از `index.ts` آن صادر می‌شود.
- منطق تجاری در کامپوننت‌ها نیست: فرم‌ها با RHF+Zod، داده با TanStack Query، وضعیت چارچوبی با Zustand.

## ۴. مدیریت خطا و وضعیت‌ها (جریان یکپارچه)

1. **خطای شبکه** → `apiClient` interceptor → `ApiError(0, پیام دوستانه)`.
2. **خطای ۴۰۱** → refresh صف‌شده (فقط یک درخواست refresh) → موفق: تکرار درخواست اصلی؛ ناموفق: `clearAuth` + `ApiError(Auth.SessionExpired)`.
3. **خطای بک‌اند (ProblemDetails)** → نگاشت به `ApiError(status, detail, code, payload)` با fallback `Http.<status>`.
4. **UI**: Loading/Empy/Error از `shared/components/feedback`، خطاهای رندر از `ErrorBoundary`، اطلاع‌رسانی کاربر با facade `core/notifications/toast` (sonner).

## ۵. امنیت

- هیچ Secret در فرانت نیست؛ فقط `VITE_API_BASE_URL/APP_NAME/APP_VERSION` (متغیرهای عمومی).
- توکن فقط در حافظه Zustand (بدون localStorage) — مستعد XSS نیست؛ refresh با httpOnly cookie (`withCredentials`).
- هر درخواست هدرهای `X-Tenant-Id / X-Company-Id / X-Branch-Id` را از store فعال می‌گیرد.

## ۶. تست‌ها

- **واحد (Vitest + RTL + jsdom، ۲۸ تست سبز)**: env، date (جلالی)، ProblemDetails/ApiError، auth store، interceptorهای api client (Bearer، نگاشت خطا، آفلاین)، ErrorBoundary (fallback/بازیابی)، ChartContainer.
- **E2E (Playwright)**: `tests/e2e/auth.spec.ts` (redir به login + رندر فرم)؛ اجرا: `npx playwright install chromium && npm run test:e2e`. در این سندباکس مرورگر نصب نشده (محدودیت شبکه/حجم) و config آماده است.
- اسکریپت‌ها: `npm run test` / `test:watch` / `test:e2e` / `typecheck` / `lint` / `build`.

## ۷. بدهی فنی شناخته‌شده و اقدامات فاز بعد

1. صفحات ۹ ماژول placeholder هستند — فازهای بعدی ماژول‌محور (Accounting اول) با RHF+Zod و Query hooks.
2. ESLint boundary rules (no-restricted-imports برای ممنوعیت core→features) اضافه شود.
3. Exportهای خارجی features از `index.ts` باید سفت‌تر شود (بررسی در PR).
4. Sentry/APM جای stub console.error در ErrorBoundary.
5. Playwright در CI با مرورگر نصب‌شده اجرا شود.
