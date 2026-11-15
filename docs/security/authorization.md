# مستندات سطوح دسترسی و مجوزها (Permission-Based Authorization)

این سند نحوه ارزیابی و اعمال مجوزهای عملیاتی بر اساس معماری مبتنی بر دسترسی (Permission-Based Access Control) را در سیستم ERP تبیین می‌کند.

---

## ۱. ساختار شناسه دسترسی‌ها (Permission Naming Standard)

مجوزها در سیستم بر اساس استاندارد سه‌بخشی `module:resource:action` نام‌گذاری می‌شوند:

- `accounting:vouchers:read` (مشاهده اسناد حسابداری)
- `accounting:vouchers:create` (صدور سند حسابداری)
- `accounting:vouchers:post` (ثبت قطعی سند حسابداری)
- `treasury:bank-accounts:manage` (مدیریت حساب‌های بانکی)
- `inventory:items:read` (مشاهده کاردکس و اقلام کالا)
- `system:admin` (مجوز مدیر ارشد سیستم با دسترسی فراگیر)

---

## ۲. پیاده‌سازی بک‌اند

### الف) ویژگی `[HasPermission]`
برای محافظت از کنترلرها یا اکشن‌ها از ویژگی زیر استفاده می‌شود:
```csharp
[ApiController]
[Route("api/v1/accounting/vouchers")]
public class VouchersController : ControllerBase
{
    [HttpPost]
    [HasPermission("accounting:vouchers:create")]
    public async Task<IActionResult> CreateVoucher(...)
    {
        // عملیات تجاری
    }
}
```

### ب) اعتبارسنجی پویا با `IAuthorizationPolicyProvider`
سیستم مجهز به `PermissionPolicyProvider` است؛ بدین معنی که هر نام مجوزی که در اتریبیوت `[HasPermission("...")]` قرار داده شود، به طور پویا به عنوان یک Policy معتبر با `PermissionRequirement` ثبت و توسط `PermissionAuthorizationHandler` ارزیابی می‌شود.

### ج) منبع استخراج دسترسی‌ها
دسترسی‌ها بر اساس **شرکت فعال** (`ActiveCompanyId`) از عضویت کاربر (`UserCompanyMembership`) استخراج شده و به عنوان Claim در JWT ثبت می‌گردند. اگر کاربر شرکت خود را تغییر دهد، جفت توکن جدید با دسترسی‌های متناظر با شرکت جدید صادر می‌شود.

---

## ۳. پیاده‌سازی و کنترل در فرانت‌اند

در فرانت‌اند کامپوننت‌های محافظ و گاردها بر اساس متد `hasPermission` در استور احراز هویت کار می‌کنند:

```tsx
import { useAuthStore } from '@/core/auth/auth.store';

export function CreateVoucherButton() {
  const hasPermission = useAuthStore((s) => s.hasPermission);

  if (!hasPermission('accounting:vouchers:create')) {
    return null; // عدم نمایش دکمه در UI برای کاربران فاقد مجوز
  }

  return <Button onClick={...}>سند جدید</Button>;
}
```
> **اصل امنیت در عمق:** پنهان کردن دکمه در رابط کاربری صرفاً برای تجربه کاربری (UX) است؛ کنترل قطعی و نفوذناپذیر امنیتی همیشه در بک‌اند و توسط `[HasPermission]` اعمال می‌گردد.
