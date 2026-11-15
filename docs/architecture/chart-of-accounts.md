# معماری کدینگ حساب‌ها و ساختار سلسله‌مراتبی (Chart of Accounts & Hierarchy)

## ۱. مقدمه و دامنه مالی

سیستم کدینگ حساب‌ها (Chart of Accounts - COA) شالوده اصلی دفترکل (General Ledger) در ERP چندمستاجره و چندشرکتی است. این زیرسیستم امکان تعریف، مدیریت و پایش درخت حساب‌ها را با استانداردهای دوبل، سلسله‌مراتب چندسطحی، تفکیک کامل داده‌های هر شرکت و پیشگیری از هرگونه بی‌نظمی ساختاری فراهم می‌آورد.

---

## ۲. سطوح سلسله‌مراتب حسابداری استاندارد ایران

سیستم به‌طور پیش‌فرض از مدل ۴ سطحی استاندارد بازرگانی ایران پیروی کرده و قابلیت توسعه به سطوح شناور و نامحدود را در هسته دامنه داراست:

| سطح (Level) | کد لاتین | عنوان فارسی | ماهیت ثبت سند (`AllowPosting`) | طول کد پیش‌فرض | توضیحات |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **سطح ۱** | `Group` | گروه حساب | ❌ ممنوع (`false`) | ۱ رقم | دسته‌بندی بنیادین (دارایی جاری، دارایی غیرجاری، بدهی، حقوق صاحبان سهام، درآمد، هزینه) |
| **سطح ۲** | `General` | حساب کل | ❌ ممنوع (`false`) | ۲ رقم | حساب‌های تجمیعی و کنترلی دفاتر قانونی و ترازنامه |
| **سطح ۳** | `Subsidiary`| حساب معین |  مجاز (`true` در صورت عدم وجود فرزند) | ۴ رقم | سطح ثبت اسناد حسابداری، دفتر روزنامه و کنترل اقلام |
| **سطح ۴** | `Detail` | حساب تفصیلی |  مجاز (`true`) | ۴ رقم | جزئی‌ترین سطح عملیاتی (بانک‌ها، صندوق‌ها، بدهکاران، بستانکاران) |

> [!IMPORTANT]
> **قانون کنترل سطوح تجمیعی در برابر سطوح ثبتی**:
> سطوح گروه (`Group`) و کل (`General`) صرفاً تجمیعی و کنترلی بوده و تحت هیچ شرایطی امکان صدور سند حسابداری مستقیم روی آن‌ها وجود ندارد (`AllowPosting = false`). تنها حساب‌های برگ (Leaf Accounts) در سطح معین یا تفصیلی مجاز به پذیرش ردیف‌های سند هستند.

---

## ۳. ویژگی‌ها و مشخصات موجودیت حساب (`Account`)

موجودیت `Account` به‌عنوان ریشه تجمیع (Aggregate Root) در دامنه مالی پیاده‌سازی شده و کلیه رفتارهای آن از طریق متدهای کپسوله‌شده هدایت می‌گردد:

```csharp
public class Account : AggregateRoot, ITenantScoped, ICompanyScoped, IAuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid? ParentAccountId { get; set; }
    public Account? ParentAccount { get; set; }

    public string Code { get; set; }          // کد نسبی در سطح خود (مثلاً "10")
    public string FullCode { get; set; }      // کد کامل سلسه‌مراتبی (مثلاً "110")
    public string Name { get; set; }          // عنوان حساب
    public string? Description { get; set; }

    public AccountLevel Level { get; set; }   // Group, General, Subsidiary, Detail
    public AccountNature Nature { get; set; } // Asset, Liability, Equity, Revenue, Expense
    public NormalBalance NormalBalance { get; set; } // Debit, Credit

    public bool IsActive { get; set; }
    public bool IsSystemAccount { get; set; }
    public bool AllowPosting { get; set; }

    public bool RequireCostCenter { get; set; }
    public bool RequireProject { get; set; }
    public bool RequireDetailAccount { get; set; }

    public uint Version { get; set; } // Optimistic Concurrency Token
}
```

---

## ۴. قوانین اکید درخت حساب (Tree Invariants)

تمام قوانین زیر در سطح لایه دامنه (`Domain Layer`) و لایه پایگاه‌داده اعتبارسنجی و تضمین می‌شوند:

1. **سازگاری سطح والد و فرزند (`Level Compatibility`)**:
   سطح حساب فرزند باید دقیقاً یک رده بالاتر از حساب والد باشد (`Child.Level = Parent.Level + 1`).
2. **پیشگیری از چرخه در درخت (`Cycle Detection`)**:
   هیچ حسابی نمی‌تواند والد خودش باشد و هیچ حسابی نمی‌تواند به یکی از نوادگان خود به‌عنوان والد جدید متصل گردد (`IsDescendantOf` check).
3. **تفکیک کامل چندشرکتی (`Company Isolation`)**:
   حساب والد و حساب فرزند حتماً باید متعلق به همان `TenantId` و `CompanyId` باشند. اتصال حساب شرکت A به والد شرکت B خطای دامنه ایجاد می‌کند.
4. **یکتایی کد حساب (`Composite Unique Index`)**:
   ترکیب `(TenantId, CompanyId, FullCode)` در سطح پایگاه‌داده و سرویس منحصربه‌فرد است.
5. **ممانعت از حذف حساب دارای گردش مالی (`Financial Immutability`)**:
   اگر حسابی در ردیف‌های اسناد مالی (`acc_document_lines`) استفاده شده باشد، حذف فیزیکی آن به دلیل یکپارچگی دفاتر قانونی ممنوع بوده و فقط امکان غیرفعال‌سازی (`Deactivate`) آن وجود دارد.
6. **ممانعت از حذف حساب دارای زیرمجموعه (`Delete Parent with Children`)**:
   حذف حسابی که دارای حساب‌های فرزند است ممنوع است. ابتدا باید فرزندان حذف یا جابه‌جا شوند.
7. **کنترل حساب‌های غیرفعال (`Inactive Guard`)**:
   حساب غیرفعال مجاز به پذیرش ردیف‌های سند جدید نیست (`EnsureCanPost` exception).
8. **تبدیل والد به حساب غیرثبتی**:
   به محض اینکه برای یک حساب معین فرزندی (تفصیلی) تعریف شود، فلگ `AllowPosting` والد خودکار غیرفعال می‌گردد.

---

## ۵. ماهیت حساب و مانده نرمال (Nature & Normal Balance)

ارتباط بین ماهیت حساب و مانده نرمال در هسته مالی تعریف شده است:

| ماهیت حساب (`AccountNature`) | مانده نرمال پیش‌فرض (`NormalBalance`) | افزایش با | کاهش با |
| :--- | :--- | :--- | :--- |
| **دارایی (Asset)** | بدهکار (Debit) | بدهکار | بستانکار |
| **بدهی (Liability)** | بستانکار (Credit) | بستانکار | بدهکار |
| **حقوق صاحبان سهام (Equity)** | بستانکار (Credit) | بستانکار | بدهکار |
| **درآمد (Revenue)** | بستانکار (Credit) | بستانکار | بدهکار |
| **هزینه (Expense)** | بدهکار (Debit) | بدهکار | بستانکار |

متد `CalculateNormalBalance(decimal debitTotal, decimal creditTotal)` مانده حساب را بر اساس مانده نرمال به صورت مثبت یا دارای انحراف محاسبه می‌نماید.

---

## ۶. الگوی کدینگ استاندارد ایران (`AccountTemplate`)

برای تسریع استقرار شرکت‌های جدید در ERP، قابلیت ایجاد و کپی الگوهای کدینگ (`AccountTemplate`) تعبیه شده است:
- **الگوی پیش‌فرض تجاری و خدماتی ایران (`DefaultIranianCommercialTemplate`)**:
  - گروه‌های ۱ تا ۸ استاندارد (دارایی‌های جاری، دارایی‌های غیرجاری، بدهی‌های جاری، بدهی‌های بلندمدت، حقوق صاحبان سهام، درآمدها، بهای تمام‌شده، هزینه‌های عمومی و اداری).
  - حساب‌های کل استاندارد (۱۰، ۱۲، ۱۴، ۱۵، ۲۰، ۳۰، ۳۱، ۴۰، ۵۰، ۵۱، ۶۰، ۷۰، ۷۱، ۸۰).
  - حساب‌های معین متداول (موجودی ریالی بانک‌ها، صندوق‌ها، چک‌های دریافتی، مشتریان، انبارها، پیش‌پرداخت‌ها، بستانکاران و...).
- **انتقال به شرکت‌های جدید (`SeedCompanyAccountsFromTemplateAsync`)**:
  - کپی کامل درخت الگو به شرکت مقصد بدون وابستگی متقابل.
  - تغییر یا شخصی‌سازی کدینگ در شرکت A هیچ اثری روی شرکت B یا الگوی اصلی ندارد.

---

## ۷. امنیت و تفکیک چندمستاجره

1. **Global Query Filters**:
   فیلترهای عمومی EF Core در `BaseDbContext` روی تمام موجودیت‌های `ITenantScoped` و `ICompanyScoped` فعال است.
2. **Mutation Guards**:
   در متد `SaveChangesAsync` تغییر یا ثبت موجودیت خارج از محدوده کانتکست فعال مسدود می‌شود.
3. **کنترل همزمانی خوش‌بینانه (`Optimistic Concurrency`)**:
   فیلد `Version` بر روی رکوردهای حساب مانع از بازنویسی ناهمگام در ویرایش‌های همزمان درختی می‌شود.

---

## ۸. تست‌های خودکار و تضمین کیفیت

کلیه سناریوهای الزامی زیر توسط ۳۹ تست در `tests/ERP.UnitTests` با پوشش ۱۰۰٪ پاس شده‌اند:
1. `Scenario01_CreateValidHierarchyTree_ShouldSucceed`
2. `Scenario02_CreateWithInvalidParent_ShouldThrowException`
3. `Scenario03_HierarchyCycle_SettingParentToOwnChild_ShouldThrowException`
4. `Scenario04_DuplicateCodeInSameCompany_ShouldThrowException`
5. `Scenario05_CrossCompanyParent_ShouldThrowException`
6. `Scenario06_InactiveAccount_CannotBeUsedForPosting`
7. `Scenario07_DeleteAccountWithFinancialActivity_ShouldBeBlocked`
8. `Scenario08_PostingOnSummaryAccount_ShouldBeBlocked`
9. `Scenario09_LevelDowngradeOnAccountWithChildren_ShouldBeBlocked`
10. `Scenario10_SearchAccountTreeAndBreadcrumbs_ShouldReturnOrderedHierarchy`
11. `Scenario11_SeedTemplateToNewCompany_ShouldGenerateFullTreeWithoutAffectingExistingCompany`
