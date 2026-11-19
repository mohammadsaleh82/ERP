# Design System ERP — فاز ۲ (Enterprise Design System & UI/UX)

مسیر: `frontend/erp-client` — نمایش زنده همه کامپوننت‌ها: `/design-system` (Showcase).

## ۱. اصول طراحی (مناسب استفاده روزانه حسابداران)

1. **خوانایی اول**: پس‌زمینه روشن، کنتراست بالا، ارقام با عرض ثابت (`tabular-nums`) تا ستون‌های مالی تراز بمانند.
2. **کاهش خطای انسانی**: خطای هر فیلد دقیقاً کنار همان فیلد (`role="alert"`)، تأیید صریح برای عملیات خطرناک (`ConfirmDialog/AlertDialog`)، منفی‌ها با قرمز مالی.
3. **تفکیک نمایش از مقدار**: همه فرمت‌های نمایشی (ارقام فارسی، جداکننده، تاریخ شمسی) فقط UI هستند؛ مقدار واقعی همیشه `number`/ISO انگلیسی است. قانون طلایی: **فرمت هرگز مقدار ذخیره‌شده را تغییر نمی‌دهد**.
4. **رسمی و آرام**: آبی سازمانی عمیق (oklch)، بدون gradient و انیمیشن افراطی؛ motion فقط `--duration-fast/normal` (۱۲۰/۲۰۰ms).
5. **RTL واقعی**: `dir="rtl"` از `index.html`؛ کدها و شناسه‌ها با `.font-code` (LTR isolate) تا متن ترکیبی فارسی/انگلیسی به‌هم نریزد.

## ۲. Token System — `src/styles/theme.css`

| خانواده | Tokenها | توضیح |
| --- | --- | --- |
| رنگ پایه | `--background / --foreground / --surface / --muted / --border / --input / --ring` | Light + Dark (کلاس `.dark`) در oklch |
| برند | `--primary / --primary-foreground / --primary-soft / --secondary` | آبی سازمانی عمیق؛ soft برای Selected/Active |
| وضعیت | `--success / --warning / --danger / --info (+foreground)` | مستقل از برند |
| مالی | `--debit / --debit-soft / --credit / --credit-soft / --neutral-balance` | بدهکار قرمز مالی، بستانکار سبز مالی |
| وضعیت اسناد | `--status-draft / pending / posted / reversed / closed` | نگاشت در `utils/status.ts` |
| سایه | `--shadow-xs / sm / md / lg` | کنترل‌شده، بدون blur افراطی |
| فونت | `--font-sans` (Vazirmatn) / `--font-code` | کد حساب/شناسه با فونت mono-like و LTR isolate |
| شکل/حرکت | `--radius (0.625rem)` + `sm/md` مشتق / `--duration-fast/normal` | radius هماهنگ همه‌جا |

Utility classes در `globals.css`: `.num-en` (ارقام انگلیسی عرض‌ثابت)، `.font-code`، `.bidi-safe`، `.thin-scroll`. نگاشت به Tailwind 4 با بلوک `@theme`.

**تغییر تم آینده**: فقط مقادیر `:root`/`.dark` را عوض کنید؛ هیچ رنگ هاردکد در کامپوننت‌ها نیست.

## ۳. Utils مالی — `src/shared/utils/`

| فایل | API کلیدی |
| --- | --- |
| `number.ts` | `digitsToEnglish/Persian`، `formatNumber(Persian)`، `groupPersian`، `parseNumberInput` (ارقام فارسی/عربی، جداکننده `, ، ٬ / ٫`)، `isNumericInput` (strict) |
| `money.ts` | `formatMoney(amount, {withCurrency, persianDigits, fractionDigits, zeroAsDash})`، `parseMoneyInput`، `moneyParts` (رندر رنگی)، `roundMoney`، `currencyLabel` |
| `date.ts` | `formatToPersianDate/DateTime`، `formatJalaliLong` (نام ماه)، `formatGregorian`، `formatDualDate` (شمسی + میلادی)، `formatJalaliRange`، `toIsoUtc` |
| `status.ts` | `VOUCHER_STATUS_MAP` / `FISCAL_PERIOD_STATUS_MAP` + fallback امن برای وضعیت ناشناخته |

## ۴. کامپوننت‌ها

### پایه — `shared/components/ui/`
Button (+loading)، IconButton (aria-label الزامی)، Input، Textarea، Checkbox/Radio/Switch (`role="switch"`)، Select، Badge، Avatar، Skeleton، Tabs، Pagination (ارقام فارسی)، Tooltip، Popover، Modal، Drawer، AlertDialog، ConfirmDialog، Breadcrumb، Card، DropdownMenu.

### فرم — `shared/components/forms/`
FormField (label + required + error + hint، aria-invalid)، PasswordInput، NumberInput (min/max/clamp)، CurrencyInput (grouping زنده، پارس فارسی، commit در blur)، SearchableSelect، MultiSelect (badges + پاک کردن)، Combobox (allowCreate)، DatePickerJalali، DateRangePickerJalali (اعتبارسنجی ترتیب).

### مالی — `shared/components/financial/`
MoneyDisplay، DebitCreditDisplay (بدهکار/بستانکار رنگی)، BalanceDisplay (مانده با ماهیت)، VoucherStatusBadge، FiscalPeriodStatusBadge + FiscalPeriodPicker (دوره یا بازه سفارشی)، AccountTreeSelect (درخت parentId محور، انتخاب سطح تفصیلی)، DetailAccountSelect، VoucherInfoCard (شماره LTR + تاریخ دوگانه + وضعیت)، JalaliDateDisplay (short/long/dual).

### بازخورد — `shared/components/feedback/`
LoadingState، EmptyState، ErrorState (با تلاش مجدد).

## ۵. UX Playbook

- **فرم کم‌خطا**: هر فیلد در FormField؛ خطا زیر فیلد؛ مقدار عددی با `*` و hint؛ commit پول فقط در blur.
- **عملیات خطرناک**: حذف سند/بستن دوره → ConfirmDialog با توضیح پیامد.
- **عملیات پرتکرار**: اکشن اصلی Button primary بالای جدول؛ ردیف‌ها با IconButton ghost + Tooltip.
- **جدول مالی**: ستون‌های عددی با `.num-en` و align انتهایی؛ مبالغ با MoneyDisplay؛ مانده با BalanceDisplay.
- **واکنش‌گرایی**: Gridهای فرم `sm:grid-cols-2`؛ Drawer در موبایل جایگزین Modal؛ Sidebar در breakpoint کوچک جمع می‌شود (AppLayout).

## ۶. تست‌ها (۸۳ سبز)

- utils: digits fa/en، پارس مبلغ/عدد (منفی، ۲^۵۳، جداکننده‌ها)، money inverse (format→parse = مقدار اصلی)، تاریخ جلالی/میلادی (۱۶ دی = ۵ ژانویه)، status fallback.
- کامپوننت: CurrencyInput (فارسی تایپ، منفی ممنوع مگر allowNegative، خالی→null)، NumberInput (clamp min، grouping در blur)، MoneyDisplay/DebitCredit/Balance (رنگ ماهیت)، FormField (for/id، role=alert)، Switch (click+Space، کنترل‌شده)، Pagination (ارقام فارسی، disabled لبه‌ها)، AccountTreeSelect (باز/انتخاب برگ/collapse)، VoucherStatusBadge (tone + fallback).

## ۷. نکات فنی و بدهی

- فونت Vazirmatn فعلاً از Google Fonts CDN؛ برای استقرار بدون اینترنت self-host شود (backlog).
- `@radix-ui/react-alert-dialog` در این فاز اضافه شد.
- Select پایه native است (دسترس‌پذیری کامل)؛ خانواده Searchable/Multi/Combobox روی Popover+listbox با role های ARIA.
- Showcase (`/design-system`) نقش قرارداد بصری دارد؛ تغییر هر کامپوننت باید آنجا به‌روز و با تست پشتیبانی شود.
