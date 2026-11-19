# معماری احراز هویت، مجوزها و Application Shell (فاز ۳)

## ۱. قرارداد واقعی Backend (منبع حقیقت)

`src/ERP.Api/Controllers/AuthController.cs` — مسیر پایه `api/v1/Auth`:

| Endpoint | ورودی | خروجی / خطا |
| --- | --- | --- |
| `POST /Auth/login` | `{email, password, tenantId?, companyId?}` | `200 {accessToken, email, fullName, tenantId, companyId, permissions[]}` + Cookie `refreshToken` (HttpOnly, Secure, SameSite=Strict, 7d) · `401 {code:"Auth.InvalidCredentials", detail:"نام کاربری یا کلمه عبور اشتباه است."}` |
| `POST /Auth/refresh-token` | فقط Cookie + هدرهای زمینه اختیاری `X-Tenant-Id`/`X-Company-Id` (تعویض شرکت) | `200 {accessToken, tenantId, companyId, permissions}` · `401 Auth.NoRefreshToken / Auth.SessionExpired` · `403 Auth.CompanyAccessDenied` |
| `POST /Auth/logout` | Cookie | ابطال refresh token + حذف Cookie |
| `GET /Auth/me` | JWT | `{userId, email, activeTenantId, activeCompanyId, roles[], permissions[]}` |
| `GET /Auth/companies` | JWT | `[{tenantId, tenantName, companies:[{companyId, name, activeFiscalYear, isActive, membershipActive}]}]` |

- توکن: **JWT Bearer**؛ claims شامل `tenantId/companyId/permissions/roles`.
- هدرهای زمینه هر درخواست: `X-Tenant-Id`, `X-Company-Id` (+ `X-Branch-Id`) — `TenantResolutionMiddleware` عضویت را با `TenancyMembershipValidator` اعتبارسنجی می‌کند (مرجع نهایی امنیت).
- **مجوزها**: رشته‌های `<module>:<resource>:<action>` — ماتریس رسمی: `docs/development/permission-matrix.md` (۲۰ مجوز حسابداری + `system:admin`).

## ۲. تغییرات Backend این فاز (ضرورت مستندشده)

قرارداد قبلی برای refresh **شکسته بود**: درخواست refresh بدون JWT → `_currentUserContext` خالی → `Guid.NewGuid()` به‌عنوان tenant/company → access token بی‌اعتبار.

1. `RefreshToken` entity: `TenantId?/CompanyId?` ذخیره می‌شود (زمینه لحظه صدور).
2. `ITokenService.ResolveActiveRefreshTokenAsync(raw)` → `(UserId, TenantId, CompanyId)` از DB؛ revoked/expired/بدون زمینه → null.
3. `AuthController.RefreshToken`: resolve از DB → عضویت فعال (`HasActiveMembershipAsync`) → permissions/roles واقعی → `GenerateAccessToken` → چرخش refresh (reuse detection حفظ شد: revoked → ابطال همه توکن‌های کاربر).
4. تعویض شرکت بدون رمز: هدرهای `X-Tenant-Id/X-Company-Id` در refresh → فقط با عضویت فعال؛ وگرنه `403 Auth.CompanyAccessDenied`.
5. `GET /Auth/companies` جدید با `ITenancyModuleApi.GetUserCompaniesAsync` (implement در `TenancyModuleApi`).
6. ⚠️ dotnet در sandbox اجرا نیست — **`dotnet build` باید در محیط dev اجرا شود**؛ کالرهای تست (`AuthenticationAndTokenTests`) به امضای جدید به‌روز شدند. ستون‌های جدید RefreshToken migration نیاز دارند (`dotnet ef migrations add` برای Identity DbContext — فاقد migration قبلی).

## ۳. معماری فرانت

### مدیریت نشست ( امنیت)
- **Refresh Token فقط Cookie HttpOnly** — جاوااسکریپت هرگز به آن دسترسی ندارد؛ از LocalStorage برای refresh استفاده نشد.
- **Access Token فقط در حافظه** (Zustand) — بدون persist؛ بعد از reload با silent restore برمی‌گردد (`BootGate` → `restoreSession()`).
- **ضد-درخواست تکراری**: mutex `isRefreshing` + صف `failedQueue` — همه 401های هم‌زمان فقط یک refresh می‌گیرند (تست‌شده).
- **ضد-حلقه**: refresh با axios خام (خارج از interceptor)؛ `_retry` per-request (حداکثر یک retry)؛ 401 روی `/Auth/login` refresh نمی‌گیرد؛ 401 روی refresh → `terminateSession()` (clearAuth + clearCompanyContext + `queryClient.clear`) و پیام «نشست منقضی».
- **خروج**: `POST /Auth/logout` (ابطال سمت سرور) در `finally` پاک‌سازی محلی؛ حتی با شکست شبکه نشست فرانت بسته می‌شود.

### مجوزها (3 لایه نمایشی، 1 لایه حقیقت)
| لایه | ابزار |
| --- | --- |
| Route | `PermissionGuard` (هدایت به `/access-denied` با لیست مجوزهای لازم) |
| منو | `menuItems[].permissions` → آیتم بدون مجوز حذف می‌شود |
| دکمه/عملیات | `<RequirePermission mode="hide|disable">` |
| حقیقت | Backend (Policyهای ASP.NET + middleware عضویت) |

- CRUD + Approve/Reject/Post/Reverse: `PERMISSIONS` در `core/auth/permissions.ts` دقیقاً مطابق ماتریس؛ مثال: Post = `accounting:documents:post`.
- دسترسی وابسته به شرکت: مجوزها از membership شرکت فعال می‌آیند؛ هدرهای زمینه همه درخواست‌ها؛ refresh با شرکت جدید → permissions جدید.

### تعویض شرکت و جلوگیری از نشت داده
`refreshSession({tenantId, companyId})` → `setSession` → `setCompanyContext` → **`queryClient.clear()`** → `navigate('/')` (پارامترهای مسیرِ شرکت قبلی معنا ندارد). تست نشت: پس از تعویض، `queryClient.getQueryData(['fiscal-years','cA'])` undefined و هدرهای درخواست بعدی شرکت جدید.

### Shell
Sidebar (فیلتر مجوز + active با NavLink + collapsed پایدار در localStorage فقط برای UI)، Header (Company switcher + Badge «سال مالی» از `activeFiscalYear` واقعی companies + منوی کاربری با نقش‌ها/پروفایل/خروج + تم)، Breadcrumb از مسیر + نقشه عناوین، BootGate (Loading اولیه بازیابی نشست)، `/profile` (نقش‌ها و مجوزهای فعال شرکت)، `/access-denied` (403 با مجوزهای لازم).

## ۴. تست‌ها (۱۸ سناریوی جدید؛ مجموع ۱۰۱ سبز)

ورود موفق/ناموفق (پیام امن backend) · refresh هم‌زمان دقیقاً یک بار · انقضای نشست + پاک‌سازی کامل · عدم retry دوم (ضد-حلقه) · 403 بدون retry · خروج حتی با خطای شبکه · تعویض شرکت با هدرها و پاک‌سازی cache و هدرهای بعدی · رد تعویض بدون عضویت (403) · hide/disable دکمه با مجوز · بازیابی silent boot (+Loading) · دسترسی مستقیم بدون نشست → login با `from` · بدون شرکت فعال → select-company · عبور کامل با شرکت فعال · mapping me/companies.

## ۵. قرارداد ناقص (Gaps مستندشده)

1. «دوره مالی فعال» endpoint مشخصی ندارد (`GET /fiscal-years` لیست سال‌ها را می‌دهد؛ فیلد active period نیاز به تصمیم backend) — فعلاً Header سال مالی را از `activeFiscalYear` شرکت (companies API) نشان می‌دهد و دوره فعال در شفاف‌سازی بعدی اضافه می‌شود.
2. `X-Branch-Id` قرارداد هدر دارد ولی مدل انتخاب شعبه در UI تعریف نشده (مرحله بعدی).
3. Notification Center به SignalR وابسته است — دکمه غیرفعال با Tooltip.
