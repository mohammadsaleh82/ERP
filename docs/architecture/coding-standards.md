# استانداردهای کدنویسی و توسعه (Coding Standards & Engineering Guidelines)

این سند مجموعه اصول مهندسی، قوانین استایل کد، الگوهای برنامه‌نویسی و استانداردهای کیفی بک‌اند و فرانت‌اند سیستم ERP را مشخص می‌کند.

---

## ۱. اصول عمومی طراحی نرم‌افزار

توسعه‌دهندگان موظف به رعایت اصول زیر در تمام اجزای سیستم هستند:
1. **SOLID:** تفکیک مسئولیت‌ها (Single Responsibility)، باز بودن برای توسعه و بسته بودن برای تغییر (Open/Closed)، جایگزینی لیسکوف، تفکیک اینترفیس‌ها و وارونگی وابستگی.
2. **DRY (Don't Repeat Yourself):** اجتناب از تکرار منطق تجاری و محاسبات مالی (اما تفکیک آگاهانه DTOهای دو ماژول مختلف نباید به نام DRY نقض شود).
3. **KISS & YAGNI:** اجتناب از پیچیده‌سازی زودهنگام و ساخت لایه‌های تجریدی بدون نیاز واقعی (No Over-Abstraction).
4. **Fail Fast:** هرگونه ورودی نامعتبر یا نقض کانتکست باید در اولین نقطه ورودی (میدل‌ویر یا اعتبارسنج) متوقف شود.
5. **Secure by Default:** عدم باز بودن دسترسی‌ها به صورت پیش‌فرض؛ تمامی اندپوینت‌ها نیازمند احراز هویت و مجوز صریح هستند مگر مواردی که با پرچم `[AllowAnonymous]` مشخص شده باشند.

---

## ۲. استانداردهای بک‌اند (.NET 10 & C# 14)

### ۲.۱. تنظیمات کامپایلر و زبان
- فعال بودن دائم بررسی مقادیر تهی: `<Nullable>enable</Nullable>`.
- استفاده از نیم‌اسپیس‌های فایلی (File-Scoped Namespaces):
  ```csharp
  namespace ERP.Modules.Accounting.Application.Commands;
  ```
- استفاده از Primary Constructors در کلاس‌ها و رکوردها:
  ```csharp
  public sealed class CreateVoucherHandler(
      IAccountingRepository repository,
      IDateTimeProvider clock)
  {
      // تزریق مستقیم و خوانا
  }
  ```
- استفاده از `record` برای تمامی DTOها، Commandها، Queryها و رویدادها (تغییرناپذیری داده‌ها و مقایسه بر اساس مقدار).

### ۲.۲. الگوی مدیریت خطا با Result Pattern (نه Exception)
استفاده از Exception برای سناریوهای قابل‌پیش‌بینی کسب‌وکار (مانند: ناکافی بودن موجودی انبار، عدم تراز سند، بسته بودن دوره مالی) **اکیداً ممنوع** است. Exception فقط برای رخدادهای غیرمنتظره زیرساختی (قطعی دیتابیس، خطای سخت‌افزاری) مجاز است.

```csharp
// نمونه تعریف الگو
public class Result<TValue>
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public TValue Value { get; }
    public Error Error { get; }

    protected Result(TValue value) { ... }
    protected Result(Error error) { ... }

    public static Result<TValue> Success(TValue value) => new(value);
    public static Result<TValue> Failure(Error error) => new(error);
}

public sealed record Error(string Code, string Message, ErrorType Type = ErrorType.Failure);

public enum ErrorType
{
    Failure,
    Validation,
    NotFound,
    Conflict,
    Unauthorized,
    Forbidden
}
```

کنترلرها نتیجه `Result<T>` را به سادگی به پاسخ متناظر HTTP (مانند `200 OK`، `400 BadRequest`، `404 NotFound` یا `409 Conflict`) نگاشت می‌کنند.

### ۲.۳. برنامه‌نویسی ناهمگام (Async / Await)
- تمام توابع I/O (دیتابیس، شبکه، فایل) باید به صورت ناهمگام (`async Task`) نوشته شوند.
- ارسال پارامتر `CancellationToken` به تمام متدهای ناهمگام الزامی است تا در صورت قطع درخواست توسط کلاینت، منابع پردازشی سرور آزاد شوند.

### ۲.۴. اعتبارسنجی با FluentValidation
- قوانین اعتبارسنجی مدل‌ها در لایه Application به صورت کلاس‌های مستقل تعریف می‌شوند:
  ```csharp
  public sealed class CreateAccountValidator : AbstractValidator<CreateAccountCommand>
  {
      public CreateAccountValidator()
      {
          RuleFor(x => x.Code)
              .NotEmpty().WithMessage("کد سرفصل حساب الزامی است.")
              .MaximumLength(20).WithMessage("طول کد حساب نمی‌تواند بیش از ۲۰ کاراکتر باشد.");
              
          RuleFor(x => x.Title)
              .NotEmpty().WithMessage("عنوان حساب الزامی است.");
      }
  }
  ```

---

## ۳. استانداردهای فرانت‌اند (React + TypeScript + Tailwind)

### ۳.۱. قوانین تایپ‌اسکریپت و ری‌اکت
- تنظیمات Strict در `tsconfig.json` همواره فعال است؛ استفاده از تایپ `any` ممنوع است (به جای آن از `unknown` یا تایپ‌های دقیق استفاده شود).
- تمامی کامپوننت‌ها به صورت تابعی (Functional Components) و با هوک‌های استاندارد ری‌اکت نوشته می‌شوند.
- تفکیک وضعیت سرور و کلاینت:
  - داده‌های دریافتی از سرور فقط با **TanStack Query (React Query)** مدیریت و کش می‌شوند.
  - وضعیت‌های محلی فرم با `react-hook-form` و اعتبارسنجی با `zod` مدیریت می‌گردد.
  - وضعیت‌های سراسری محدود (مانند تم، شرکت انتخاب‌شده، منو) با `Context` یا `zustand` نگهداری می‌شوند.

### ۳.۲. چیدمان راست‌به‌چپ (RTL) و ویژگی‌های منطقی در Tailwind
- تمام صفحات و المان‌ها باید در حالت راست‌به‌چپ (`dir="rtl"`) استاندارد باشند.
- **استفاده از خواص منطقی (Logical Properties):** به جای استفاده از کلاس‌های جهت‌دار فیزیکی (`ml-`, `mr-`, `pl-`, `pr-`, `text-left`, `text-right`)، **الزاماً** از کلاس‌های جهت‌دار منطقی Tailwind استفاده شود:
  - `ms-4` (Margin Start) به جای `mr-4` یا `ml-4`
  - `me-2` (Margin End)
  - `ps-3` (Padding Start)
  - `pe-3` (Padding End)
  - `text-start` و `text-end`
- فونت پیش‌فرض سیستم قلم استاندارد سازمانی فارسی (وزیرمتن / Vazirmatn) است که ارقام فارسی و وزن‌های متنوع را به زیبایی پشتیبانی می‌کند.

### ۳.۳. مدیریت تاریخ و تقویم
- در تمامی فرم‌ها از کامپوننت تقویم و انتخاب‌گر تاریخ شمسی (`JalaliDatePicker`) استفاده می‌شود.
- در کلاینت تاریخ با تقویم شمسی به کاربر نمایش داده می‌شود و هنگام ارسال به API، به رشته استاندارد زمان جهانی (`ISO 8601 UTC`) تبدیل می‌شود:
  ```typescript
  // تاریخ انتخاب‌شده توسط کاربر: 1403/08/25
  // داده ارسالی به API: "2024-11-15T00:00:00.000Z"
  ```

---

## ۴. قراردادهای نام‌گذاری (Casing Conventions)

| مؤلفه | زبان / بستر | قرارداد | مثال |
| :--- | :--- | :--- | :--- |
| نام کلاس‌ها، اینترفیس‌ها و متدها | C# | `PascalCase` | `JournalVoucher`, `GetAccountBalance()` |
| نام متغیرها و فیلدهای محلی | C# | `camelCase` | `totalAmount`, `cancellationToken` |
| فیلدهای خصوصی | C# | `_camelCase` | `_dateTimeProvider`, `_repository` |
| نام جداول و ستون‌های پایگاه داده | PostgreSQL | `snake_case` | `acc_voucher_lines`, `company_id` |
| نام کامپوننت‌های فرانت‌اند | TypeScript / React | `PascalCase` | `VoucherListTable.tsx`, `SidebarNav.tsx` |
| نام هوک‌های فرانت‌اند | TypeScript | `camelCase` | `useCurrentCompany.ts`, `useVoucherQuery.ts` |
| ویژگی‌های JSON در تبادلات API | JSON | `camelCase` | `{ "voucherDate": "2024-11-15T...", "totalDebit": 150000 }` |

---

## ۵. استانداردهای گیت و پیام‌های کامیت (Conventional Commits)

پیام‌های کامیت باید با فرمت استاندارد Conventional Commits نوشته شوند:
- `feat(accounting): add journal voucher approval command handler`
- `fix(inventory): resolve negative balance edge case in warehouse transfer`
- `refactor(tenancy): optimize tenant resolution middleware query`
- `docs(architecture): add adr for in-process module communication`
- `test(sales): add unit test for order tax calculation`
