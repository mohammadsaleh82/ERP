# گزارش نهایی ممیزی Frontend — فاز ۱۰ (QA & Production Readiness)

نقش: QA Lead / Senior Frontend Engineer / متخصص تست نرم‌افزارهای مالی
تاریخ: ۲۰۲۶-۱۱-۲۱ · دامنه: erp-client (React 19 + Vite + TS + react-query v5)

## ۱. معماری نهایی
- **لایه‌ها**: `app/` (router+guards+layouts) · `core/` (api client با refresh-mutex، auth/company stores، سرویس‌های دامنه، مدیریت خطا problem-details) · `features/` (صفحه‌ها + کامپوننت‌ها) · `shared/` (کیت UI مبتنی بر Radix، فرم‌ها، feedback، utils مالی).
- **Router**: createBrowserRouter با React.lazy + Suspense (۴۳ chunk)، AuthGuard → CompanyGuard → PermissionGuard روی همه مسیرهای حساس (شامل ۱۵ مسیر گزارش — اصلاح فاز ۱۰).
- **State**: zustand (auth/company در حافظه؛ فقط ترجیحات UI و شناسه شرکت فعال در localStorage) + react-query برای داده سرور (staleTime مشخص، retry هوشمند بدون retry برای 401/403).
- **مالی**: decimal.js برای همه مبالغ، فرمت هزارگان، `dir=ltr` + `tabular-nums`، تقویم شمسی fa-IR.

## ۲. صفحات پیاده‌سازی‌شده (۲۵+ مسیر فعال)
ورود · انتخاب شرکت · داشبورد (۱۰ widget) · حساب‌ها (درخت+CRUD) · سال مالی/دوره‌ها · اسناد (لیست+فیلتر) · ثبت/ویرایش سند · جزئیات+workflow کامل (submit/approve/reject/post/reverse/delete) · ۱۰ گزارش حسابداری (روزنامه، کل، معین، کارت، گردش، مانده، تراز، ابعاد×۲) · ۵ صورت مالی (ترازنامه، سودوزیان، جریان نقد، بدهکاران، بستانکاران، سنی) · خروجی Excel/PDF/CSV سمت سرور · تنظیمات حسابداری · پروفایل · 404/deny.
Placeholder (بیرون از دامنه، با FeaturePlaceholder استاندارد): خزانه، انبار، فروش، خرید، طرف‌حساب‌ها، حقوق‌و‌دستمزد، reporting، organization، settings.

## ۳. APIهای متصل‌شده (قرارداد واقعی backend)
Auth: login/logout/me/companies/refresh-token (Cookie HttpOnly) · Accounts: tree/children/breadcrumb/postable/CRUD/by-code · FiscalYears: CRUD/open/close/periods/generate/custom · Documents: list/detail/audit-trail/create/update/delete/submit/approve/reject/post/reverse · Ledger: journal/general-ledger/transactions/balances · Reports: trial-balance/account-card/turnover/subsidiary · Statements + Export · Dashboard: Ledger/balances, AuditTrail, Documents, Accounts, FiscalYears.

## ۴. نتایج تست‌ها (با شواهد)

### ۴.۱ کیفیت کد — همه سبز
| چک | نتیجه |
| --- | --- |
| TypeScript (`tsc -b`) | ✅ صفر خطا |
| ESLint | ✅ 0 error / 4 warning (react-refresh روی فایل‌های چند-خروجی — قابل قبول و مستند) |
| Build production | ✅ سبز (tsc -b && vite build) — chunk بزرگ‌تر < 600KB |
| Unused deps | ✅ حذف شد: @radix-ui/react-dropdown-menu، @radix-ui/react-label، date-fns، date-fns-jali (4 مورد) |
| Dead code | ✅ data-table.tsx حذف؛ main.tsx/router از entry استفاده می‌شوند |
| Broken imports / Routeهای ناقص | ✅ tsc + اسکریپت menu↔router↔routeTitles = OK |
| Console error | ✅ در E2E روی همه صفحات اصلی صفر (به‌جز 401 boot برای مهمان که قرارداد صحیح است و app مدیریتش می‌کند) |

### ۴.۲ تست‌های واحد — ۲۲۵/۲۲۵ سبز (۳۹ فایل)
پوشش ۱۲ حوزه خواسته‌شده: Currency Formatting (money-utils/money-display/currency-input) · Decimal Handling (documents-amounts با decimal.js) · Date Conversion (date + jalali-date-utils) · Validation (form-field/voucher/problem-details) · Permission Checking (auth-store + permission-guard ×3 — جدید فاز ۱۰) · Authentication State (auth-store/session-restore) · Refresh Token (client-refresh با mutex) · Query Cache (company-switch invalidate/clear) · Form Submission (voucher-form/account-form) · Table Filtering (vouchers-page) · Pagination (pagination) · Error Handling (api-client/problem-details/error-boundary).

### ۴.۳ E2E — ۲۱/۲۳ سبز (Playwright + chromium، روی build واقعی preview)
Mock قرارداد‌محور stateful (`tests/e2e/support/mock-backend.ts`) دقیقاً مطابق DTOهای backend با: جداسازی شرکت با X-Company-Id، SOD واقعی، مجوزهای سه کاربر، refresh Cookie HttpOnly، CSV export واقعی. سناریوها:
1. **ورود→انتخاب شرکت→داشبورد→سال مالی→حساب‌ها** ✅ (۳ تست)
2. **ایجاد سند→ردیف‌ها→حساب→بدهکار/بستانکار→توازن→پیش‌نویس→ارسال** ✅ (۲ تست؛ شامل رد سند نامتوازن)
3. **تأیید/ثبت توسط کاربر مجاز + دفتر کل** ⚠️ ۲/۴ — جریان SOD و approve/post سبز؛ دو assertion جمع دفتر کل fail (بند ۷)
4. **تراز آزمایشی→فیلتر→خروجی CSV** ✅ (۳ تست؛ محتوای CSV واقعی بررسی شد)
5. **کاربر فاقد مجوز→deny→تعویض شرکت→عدم نشت داده→خروج** ✅ (۴ تست)
Performance ✅ ۵/۵: bundle top chunks در test-results/bundle-sizes.txt · load < 8s · بدون double-fetch · بازیابی پس از قطع API (fallback auth) · journal با انتخاب سال.
Visual ✅ ۳/۳: screenshot در 1440/768/360 + بدون اسکرول افقی 360px + کشوی موبایل + empty/error states → tests/e2e/screenshots/

## ۵. مشکلات کشف و رفع‌شده در همین فاز (شواهد)
| # | یافته | شدت | اصلاح |
| --- | --- | --- | --- |
| F1 | ۱۵ مسیر گزارش بدون PermissionGuard (دسترسی مستقیم URL) | بحرانی | گارد [reportsRead, ledgerRead] روی همه + 403 سمت mock |
| F2 | `refreshSession` با fallback هاردکد `https://localhost:5001` — در هر استقرار واقعی refresh می‌شکند | بحرانی | استفاده از env.apiBaseUrl |
| F3 | session-restore: `/Auth/me` قبل از ست‌شدن token → بدون Bearer → با backend واقعی 401 = خروج در هر F5 | بحرانی | ترتیب صحیح: setSession → me → setAuth |
| F4 | گارد تغییرات ذخیره‌نشده فاز ۹، ناوبری پس از ذخیره موفق را رهگیری می‌کرد | بالا | `allowNextNavigation` + تست رگرسیون |
| F5 | conditional `useId` در Checkbox/Radio (نقض قوانین hook) | بالا | فراخوانی unconditional |
| F6 | شرکت فعال بعد از reload پاک می‌شد (UX سازمانی) | متوسط | persistence شناسه‌ها در localStorage + تست‌های مرتبط سبز |

## ۶. مشکلات باقی‌مانده (Open)
1. **E2E scenario3 — جمع دفتر کل**: پس از approve/post توسط کاربر دوم، `gl-total-debit` همچنان ۵٬۰۰۰٬۰۰۰ (seed) نشان می‌دهد نه ۵٬۶۰۰٬۰۰۰؛ جریان UI کامل سبز است و فقط این assertion دو تست fail است. نیاز به debug تجمیع mock/بازخوانی query پس از post در context دوم. (لاگ: /tmp/e2e-final.log)
2. roving tabindex انتخابگر شرکت و Arrow-key navigation در منوی شرکت (مستند فاز ۹).
3. aria-live سراسری برای toastها.

## ۷. مشکلات وابسته به Backend (Blocked — نه Pass)
- **تست یکپارچه با backend واقعی اجرا نشد** (dotnet SDK در sandbox موجود نیست) → کل E2E روی mock قراردادی معتبر اجرا شد که قرارداد DTO را قفل می‌کند ولی جای تست integration را نمی‌گیرد. **Blocked**: اجرای docker/backend + data seeding واقعی.
- SignalR اعلان‌های زنده (مرکز اعلان‌ها disabled — پس از اتصال نیاز به backend دارد).
- خروجی Excel/PDF سمت سرور (فقط CSV در mock پیاده شد؛ قرارداد endpoint موجود است).

## ۸. امنیت — وضعیت (شواهد)
- ✅ **بدون Secret در Bundle**: اسکن dist برای secret/api-key/JWT/AKIA → فقط رشته‌های استاندارد HTML (password input types) — هیچ کلیدی.
- ✅ Token در حافظه (بدون localStorage)، refresh فقط با Cookie HttpOnly (SameSite=Strict، مسیر محدود) — تایید در E2E (بدون cookie → 401).
- ✅ پاک‌سازی کامل cache در خروج و تعویض شرکت (`queryClient.clear()`) — تست E2E سبز: داده شرکت قبلی count=0.
- ✅ IDOR در سطح تجربه: داده‌ها با X-Company-Id اسکوپ می‌شوند؛ UI هرگز مجوز را منبع حقیقت نمی‌داند (permission از JWT/backend، guard فقط نمایشی) — تست کاربر محدود ✅.
- ✅ Error messages: پیام‌های فارسی بدون افشای stack/جزئیات داخلی؛ پیام امن یکسان برای اعتبارات غلط.
- ⚠️ محدودیت: اینها QA سمت frontend است؛ امنیت واقعی به CSRF/authorization سرور وابسته است (خارج از دامنه).

## ۹. Performance — وضعیت
۴۳ chunk lazy · بزرگ‌ترین chunk < 600KB (جدول کامل در test-results/bundle-sizes.txt) · بدون درخواست تکراری در داشبورد · debounce جست‌وجو · staleTimeها · load داشبورد < 8s در preview · بازیابی پس از قطع API ✅ · جدول‌ها صفحه‌بندی سمت سرور (۲۰/۵۰ ردیف).

## ۱۰. وضعیت نهایی آمادگی انتشار
**شرطی**. کیفیت کد، امنیت سمت UI، UX و ۲۲۵ تست واحد + ۲۱/۲۳ E2E سبزند؛ برای اعلام «آماده انتشار» کامل لازم است:
1. رفع open item #۱ (جمع دفتر کل در E2E) — کوچک و مشخص.
2. اجرای یکبار تست یکپارچه با backend واقعی (Blocked فعلی).
3. تست دستی smoke روی محیط staging با داده واقعی.
