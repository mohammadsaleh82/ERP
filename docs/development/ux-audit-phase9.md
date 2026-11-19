# گزارش UX Audit — فاز ۹ (Advanced UX/UI Refinement & Accessibility)

نقش: Senior UX Auditor / Accessibility Specialist / Senior Frontend Engineer
دامنه: تمام Routeهای ساخته‌شده (ورود، انتخاب شرکت، داشبورد، حساب‌ها، سال مالی، دوره‌ها، اسناد، ثبت سند، تأیید، ۱۰ گزارش حسابداری، ۵ صورت مالی، تنظیمات، پروفایل، پوسته)
روش: ممیزی سطح کد (سفری کامل روی router، layout، کیت UI، فرم‌ها و صفحات) + اجرای تست‌های رفتاری. هیچ صفحه‌ای بدون بررسی نماند.

## ۱. ایرادهای کشف‌شده و وضعیت

| # | شدت | محل | ایراد | اصلاح |
| --- | --- | --- | --- | --- |
| A1 | بحرانی | app-layout | زیر `md` منو کاملاً حذف می‌شد و هیچ ناوبری‌ای در 360/390px نبود (کامنت «فاز بعد» معلق مانده بود) | دکمه منو در Header + Drawer موبایل با همان آیتم‌های مجوزدار (`SidebarNavItems` مشترک) |
| A2 | بحرانی | voucher-form | خروج از فرم سند نیمه‌کاره بدون هشدار — داده مالی از بین می‌رفت | hook `useUnsavedChangesGuard`: dirty detection (سریال‌سازی نرمال‌شده) + `beforeunload` + رهگیری `history.pushState/replaceState` با ConfirmDialog «خروج بدون ذخیره / ماندن در فرم» |
| A3 | بحرانی | global-search | «Ctrl K» در UI نوشته شده بود ولی handler نداشت | listener سراسری در Header (`erp:open-global-search`) + گوش‌دادن GlobalSearch؛ Escape هم مثل قبل با Radix می‌بندد |
| A4 | بحرانی | فرم‌ها | پس از خطای اعتبارسنجی فوکوس جابجا نمی‌شد | voucher: `focusFirstErrorField` (تاریخ→شرح→اولین ورودی ردیف ناقص)؛ login: فوکوس روی alert خطا با `tabIndex=-1`؛ خطاهای فیلدی RHF با shouldFocusError پیش‌فرض |
| A5 | بحرانی | router | همه ۳۰+ صفحه eager import می‌شدند — bundle واحد | `React.lazy` + `Suspense` با fallback مشترک `LoadingState` برای هر صفحه؛ 404/deny eager. نتیجه: ۴۳ chunk مستقل |
| B1 | بالا | app-layout | skip-link و landmark اصلی نبود | skip-link اولین focusable (`#main-content`) + `main` با id و `tabIndex=-1` |
| B2 | بالا | modal | مودال lg با فرم بلند از viewport بیرون می‌زد | `max-h-[calc(100vh-2rem)]` + `overflow-y-auto` |
| B3 | بالا | سراسری | عناصر سفارشی خارج از کیت UI حلقه فوکوس نداشتند | `:focus-visible` سراسری در globals.css (outline+offset، بدون حلقه برای کلیک موس) |
| B4 | بالا | sidebar | aria-current فقط روی مسیر دقیق؛ حالت فعال صرفاً رنگی | `aria-label` + متن sr-only «(صفحه جاری)» — تشخیص بدون اتکا به رنگ |
| B5 | متوسط | جداول گزارش‌ها (bs/pl/cf/aging) | در 360px جداول ۸ ستونه page-level scroll می‌گرفتند | wrapper `overflow-x-auto` روی همه جداول این صفحات |
| B6 | متوسط | header | `role="listbox/option"` روی دکمه‌های تعویض شرکت — ARIA نادرست (بدون رoving tabindex) | حذف نقش‌های نادرست؛ `aria-current="true"` برای شرکت فعال + Popover خود Radix مدیریت کیبورد دارد |
| B7 | متوسط | global-search | Popover با `w-96` در 360px بیرون می‌زد | `w-[min(24rem,calc(100vw-1.5rem))]` |
| C1 | کم | voucher-form | هشدار دوره بسته نام کد backend را نشان می‌داد (`DocumentDateOutsideOpenPeriod`) | عمداً حفظ شد — حسابدار باید علت دقیق رد شدن سند را ببیند (مستند شده) |
| C2 | کم | sidebar | chevron جمع‌کردن/باز‌کردن در RTL | بررسی شد: درست بود (`ChevronLeft` برای باز کردن = جهت رو به جلو در RTL) |

## ۲. موارد بررسی‌شده که ایراد نداشتند (سند ممیزی)

- **RTL واقعی**: کیت UI با logical properties (`ms/me`, `text-start/end`)، اعداد با `dir="ltr"` + `tabular-nums` داخل متن RTL، کدهای حساب با `font-code ltr`، تاریخ شمسی fa-IR — در همه صفحات گزارش و اسناد درست.
- **Dialogها**: Modal/Drawer بر پایه Radix (فوکوس‌ترپ، Escape، aria-labelledby) — سالم.
- **حالت‌ها**: Loading (`role=status` + skeleton)، Empty (EmptyState با راهنمای فیلتر)، Error (ErrorState با retry) در همه جدول‌ها/پنل‌ها — یکدست.
- **جداول مالی**: عنوان ستون با `scope="col"`، جمع‌ها `tfoot`، صفر → `—`، Direction با متن نه فقط رنگ، شماره سند قابل کپی/لینک — استاندارد.
- **Formها**: Enter در login/جست‌وجو submit می‌کند؛ Escape مودال/کشو می‌بندد؛ autoComplete درست (`username/current-password`)؛ `onFocus select()` در فیلدهای مبلغ.
- **عملکرد**: debounce ۳۰۰ms جست‌وجو، staleTimeهای مشخص، `queryClient.clear()` هنگام تعویض شرکت (ضد نشت داده)، cleanup تمام timer/listenerها — Memory leak یافت نشد. Lazy loading اضافه شد (۴۳ chunk).
- **صفحات placeholder** (خزانه/انبار/فروش/…): FeaturePlaceholder با landmark درست — بیرون از دامنه این فاز.

## ۳. تست‌های مرتبط (۹ مورد جدید — `tests/unit/ux-a11y.test.tsx`، مجموع ۲۲۱ سبز)

1. skip-link اولین focusable و مقصد `#main-content`
2. کشوی ناوبری موبایل با آیتم‌های مجوزدار
3. sidebar اعلام حالت فعال با sr-only (بدون اتکا به رنگ)
4. Ctrl+K پنل جست‌وجو را باز می‌کند و Escape می‌بندد
5. رهگیری pushState با dirty → `alertdialog` تأیید → ادامه ناوبری پس از تأیید
6. بدون dirty → ناوبری بدون دیالوگ
7. فرم سند: dirty → دیالوگ، انصراف → بستن
8. فرم سند: ذخیره با ردیف ناقص → فوکوس روی فیلد ردیف ناقص
9. لاگین: خطای ۴۰۱ → فوکوس روی پیام `role=alert`

## ۴. باقی‌مانده‌ها (پیشنهاد فاز بعد)

1. roving tabindex برای انتخابگر شرکت (الان Popover Radix + دکمه‌ها قابل Tab است؛ Arrow keys ندارد)
2. واحد `aria-live` سراسری برای toastها (الان toast visual است؛ role ندارد)
3. تست خودکار ۶ اندازه viewport با Playwright (اینجا با breakpointها و wrapperهای overflow در سطح کد پوشش داده شد؛ snapshot بصری نیاز به ابزار مرورگر دارد)
4. نسخه چاپی اختصاصی سند (voucher-print موجود است ولی برای همه گزارش‌ها استایل چاپ مشترک است)
