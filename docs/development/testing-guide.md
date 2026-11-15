# راهنمای اجرای آزمون‌های خودکار و کنترل کیفیت (Testing Guide)

این سند انواع آزمون‌ها، ابزارها و دستورالعمل‌های تست را در سیستم ERP تشریح می‌کند.

---

## ۱. هرم آزمون‌ها در سیستم ERP

```text
               /\
              /  \     Security & Integration Tests (21 Tests)
             /    \    - Multi-Tenant Isolation
            /      \   - Token Rotation & Compromise Detection
           /        \  - RFC 7807 ProblemDetails & Membership Guards
          /----------\
         /            \ Architecture Tests (17 Tests)
        /              \ - NetArchTest Rules
       /                \ - Zero Cross-Module Dependencies
      /------------------\
     /                    \ Unit Tests (2 Tests)
    /                      \ - Core BuildingBlocks & Result Types
   /------------------------\
```

---

## ۲. دسته‌بندی پروژه‌های آزمون

1. **[ERP.ArchitectureTests](file:///d:/ffff/MyProject/ERP/tests/ERP.ArchitectureTests/):**
   - سنجش قواعد معماری Clean و Modular Monolith به صورت خودکار با NetArchTest.
   - تضمین عدم رفرنس ماژول به ماژول دیگر.
   - تضمین عدم وابستگی لایه Domain به Infrastructure یا کتابخانه‌های دیتابیس.
   - تضمین استقلال کامل BuildingBlocks.

2. **[ERP.SecurityTests](file:///d:/ffff/MyProject/ERP/tests/ERP.SecurityTests/):**
   - سنجش ایزولاسیون امنیتی مستاجران (Scenarios A, B, C, D, E).
   - سنجش چرخش Refresh Token و کشف استفاده مجدد از توکن‌های باطل‌شده (Token Reuse Detection).
   - آزمون بازگرداندن خطای `403 Forbidden` در صورت ارسال شناسه شرکت نامعتبر توسط کاربر احراز هویت‌شده.
   - آزمون اعتبارسنجی سیستم مجوزدهی پویا `[HasPermission]`.

3. **[ERP.IntegrationTests](file:///d:/ffff/MyProject/ERP/tests/ERP.IntegrationTests/):**
   - اثبات ایزولاسیون کوئری و تفکیک ۲ مستاجر مستقل با پایگاه‌داده SQLite In-Memory.
   - آزمون سیاست‌های داده‌ای (UTC DateTime، دقت اعشار 28,4، ره‌گیری خودکار زمان).
   - آزمون خطاهای استاندارد RFC 7807 ProblemDetails در محیط‌های Development و Production.

4. **[ERP.UnitTests](file:///d:/ffff/MyProject/ERP/tests/ERP.UnitTests/):**
   - آزمون واحد بر روی المان‌های پایه‌ای BuildingBlocks (الگوی Result و Error handling).

---

## ۳. اجرای دستورات آزمون

```bash
# اجرای کل سوئیت آزمون‌ها
dotnet test ERP.sln

# اجرای اختصاصی آزمون‌های معماری
dotnet test tests/ERP.ArchitectureTests/

# اجرای اختصاصی آزمون‌های امنیت
dotnet test tests/ERP.SecurityTests/

# اجرای اختصاصی آزمون‌های یکپارچگی
dotnet test tests/ERP.IntegrationTests/
```
