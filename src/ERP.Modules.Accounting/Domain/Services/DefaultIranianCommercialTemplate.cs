using ERP.Modules.Accounting.Domain.Entities;
using ERP.Modules.Accounting.Domain.Enums;

namespace ERP.Modules.Accounting.Domain.Services;

public static class DefaultIranianCommercialTemplate
{
    public static readonly Guid DefaultTemplateId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    public static AccountTemplate Create()
    {
        var template = new AccountTemplate(
            title: "الگوی استاندارد حسابداری بازرگانی و خدماتی ایران",
            description: "کدینگ جامع چهارسطحی استاندارد سازمان حسابرسی ایران منطبق بر استانداردهای حسابداری دوبل و قوانین مالیاتی",
            isDefault: true,
            isSystemTemplate: true)
        {
            Id = DefaultTemplateId
        };

        // 1. Definition of Coding Levels
        template.AddLevel(AccountLevel.Group, "گروه", 1);
        template.AddLevel(AccountLevel.General, "کل", 2);
        template.AddLevel(AccountLevel.Subsidiary, "معین", 4);
        template.AddLevel(AccountLevel.Detail, "تفصیلی", 4);

        // 2. Definition of Standard Accounts Hierarchy
        var sort = 1;

        // ===== گروه ۱: دارایی‌های جاری =====
        template.AddItem("1", "دارایی‌های جاری", AccountLevel.Group, AccountNature.Asset, NormalBalance.Debit, parentCode: null, allowPosting: false, sortOrder: sort++);
        // کل ۱۰: موجودی نقد و بانک
        template.AddItem("10", "موجودی نقد و بانک", AccountLevel.General, AccountNature.Asset, NormalBalance.Debit, parentCode: "1", allowPosting: false, sortOrder: sort++);
        template.AddItem("1010", "موجودی ریالی نزد بانک‌ها", AccountLevel.Subsidiary, AccountNature.Asset, NormalBalance.Debit, parentCode: "10", allowPosting: true, sortOrder: sort++);
        template.AddItem("1020", "صندوق و تنخواه‌گردان‌ها", AccountLevel.Subsidiary, AccountNature.Asset, NormalBalance.Debit, parentCode: "10", allowPosting: true, sortOrder: sort++);
        template.AddItem("1030", "موجودی ارزی نزد بانک‌ها", AccountLevel.Subsidiary, AccountNature.Asset, NormalBalance.Debit, parentCode: "10", allowPosting: true, sortOrder: sort++);

        // کل ۱۲: حساب‌ها و اسناد دریافتنی تجاری
        template.AddItem("12", "حساب‌ها و اسناد دریافتنی تجاری", AccountLevel.General, AccountNature.Asset, NormalBalance.Debit, parentCode: "1", allowPosting: false, sortOrder: sort++);
        template.AddItem("1210", "اسناد دریافتنی تجاری (چک‌های نزد صندوق)", AccountLevel.Subsidiary, AccountNature.Asset, NormalBalance.Debit, parentCode: "12", allowPosting: true, sortOrder: sort++);
        template.AddItem("1220", "حساب‌های دریافتنی تجاری (مشتریان)", AccountLevel.Subsidiary, AccountNature.Asset, NormalBalance.Debit, parentCode: "12", allowPosting: true, sortOrder: sort++);

        // کل ۱۴: موجودی مواد و کالا
        template.AddItem("14", "موجودی مواد و کالا", AccountLevel.General, AccountNature.Asset, NormalBalance.Debit, parentCode: "1", allowPosting: false, sortOrder: sort++);
        template.AddItem("1410", "موجودی کالای تجاری", AccountLevel.Subsidiary, AccountNature.Asset, NormalBalance.Debit, parentCode: "14", allowPosting: true, sortOrder: sort++);
        template.AddItem("1420", "موجودی مواد اولیه و قطعات", AccountLevel.Subsidiary, AccountNature.Asset, NormalBalance.Debit, parentCode: "14", allowPosting: true, sortOrder: sort++);

        // کل ۱۵: پیش‌پرداخت‌ها
        template.AddItem("15", "پیش‌پرداخت‌ها و علی‌الحساب‌ها", AccountLevel.General, AccountNature.Asset, NormalBalance.Debit, parentCode: "1", allowPosting: false, sortOrder: sort++);
        template.AddItem("1510", "پیش‌پرداخت خرید کالا و خدمات", AccountLevel.Subsidiary, AccountNature.Asset, NormalBalance.Debit, parentCode: "15", allowPosting: true, sortOrder: sort++);
        template.AddItem("1520", "پیش‌پرداخت مالیات و بیمه", AccountLevel.Subsidiary, AccountNature.Asset, NormalBalance.Debit, parentCode: "15", allowPosting: true, sortOrder: sort++);

        // ===== گروه ۲: دارایی‌های غیرجاری =====
        template.AddItem("2", "دارایی‌های غیرجاری", AccountLevel.Group, AccountNature.Asset, NormalBalance.Debit, parentCode: null, allowPosting: false, sortOrder: sort++);
        template.AddItem("20", "دارایی‌های ثابت مشهود", AccountLevel.General, AccountNature.Asset, NormalBalance.Debit, parentCode: "2", allowPosting: false, sortOrder: sort++);
        template.AddItem("2010", "زمین و محوطه‌سازی", AccountLevel.Subsidiary, AccountNature.Asset, NormalBalance.Debit, parentCode: "20", allowPosting: true, sortOrder: sort++);
        template.AddItem("2020", "ساختمان و تاسیسات", AccountLevel.Subsidiary, AccountNature.Asset, NormalBalance.Debit, parentCode: "20", allowPosting: true, sortOrder: sort++);
        template.AddItem("2030", "ماشین‌آلات و تجهیزات", AccountLevel.Subsidiary, AccountNature.Asset, NormalBalance.Debit, parentCode: "20", allowPosting: true, sortOrder: sort++);
        template.AddItem("2040", "وسایط نقلیه", AccountLevel.Subsidiary, AccountNature.Asset, NormalBalance.Debit, parentCode: "20", allowPosting: true, sortOrder: sort++);
        template.AddItem("2090", "استهلاک انباشته دارایی‌های ثابت", AccountLevel.Subsidiary, AccountNature.Asset, NormalBalance.Credit, parentCode: "20", allowPosting: true, sortOrder: sort++);

        // ===== گروه ۳: بدهی‌های جاری =====
        template.AddItem("3", "بدهی‌های جاری", AccountLevel.Group, AccountNature.Liability, NormalBalance.Credit, parentCode: null, allowPosting: false, sortOrder: sort++);
        template.AddItem("30", "حساب‌ها و اسناد پرداختنی تجاری", AccountLevel.General, AccountNature.Liability, NormalBalance.Credit, parentCode: "3", allowPosting: false, sortOrder: sort++);
        template.AddItem("3010", "اسناد پرداختنی تجاری (چک‌های صادره)", AccountLevel.Subsidiary, AccountNature.Liability, NormalBalance.Credit, parentCode: "30", allowPosting: true, sortOrder: sort++);
        template.AddItem("3020", "حساب‌های پرداختنی تجاری (تامین‌کنندگان)", AccountLevel.Subsidiary, AccountNature.Liability, NormalBalance.Credit, parentCode: "30", allowPosting: true, sortOrder: sort++);

        template.AddItem("31", "سایر حساب‌ها و اسناد پرداختنی", AccountLevel.General, AccountNature.Liability, NormalBalance.Credit, parentCode: "3", allowPosting: false, sortOrder: sort++);
        template.AddItem("3110", "جاری شرکا و سهامداران", AccountLevel.Subsidiary, AccountNature.Liability, NormalBalance.Credit, parentCode: "31", allowPosting: true, sortOrder: sort++);
        template.AddItem("3120", "حقوق و دستمزد پرداختنی", AccountLevel.Subsidiary, AccountNature.Liability, NormalBalance.Credit, parentCode: "31", allowPosting: true, sortOrder: sort++);
        template.AddItem("3130", "بیمه و مالیات تکلیفی پرداختنی", AccountLevel.Subsidiary, AccountNature.Liability, NormalBalance.Credit, parentCode: "31", allowPosting: true, sortOrder: sort++);

        // ===== گروه ۴: بدهی‌های غیرجاری =====
        template.AddItem("4", "بدهی‌های غیرجاری", AccountLevel.Group, AccountNature.Liability, NormalBalance.Credit, parentCode: null, allowPosting: false, sortOrder: sort++);
        template.AddItem("40", "تسهیلات مالی بلندمدت پرداختنی", AccountLevel.General, AccountNature.Liability, NormalBalance.Credit, parentCode: "4", allowPosting: false, sortOrder: sort++);
        template.AddItem("4010", "تسهیلات مالی بلندمدت بانک‌ها", AccountLevel.Subsidiary, AccountNature.Liability, NormalBalance.Credit, parentCode: "40", allowPosting: true, sortOrder: sort++);

        // ===== گروه ۵: حقوق صاحبان سهام / سرمایه =====
        template.AddItem("5", "حقوق مالکانه و سرمایه", AccountLevel.Group, AccountNature.Equity, NormalBalance.Credit, parentCode: null, allowPosting: false, sortOrder: sort++);
        template.AddItem("50", "سرمایه ثبت‌شده", AccountLevel.General, AccountNature.Equity, NormalBalance.Credit, parentCode: "5", allowPosting: false, sortOrder: sort++);
        template.AddItem("5010", "سرمایه پرداخت‌شده سهامداران", AccountLevel.Subsidiary, AccountNature.Equity, NormalBalance.Credit, parentCode: "50", allowPosting: true, sortOrder: sort++);
        template.AddItem("51", "اندوخته‌ها و سود انباشته", AccountLevel.General, AccountNature.Equity, NormalBalance.Credit, parentCode: "5", allowPosting: false, sortOrder: sort++);
        template.AddItem("5110", "اندوخته قانونی", AccountLevel.Subsidiary, AccountNature.Equity, NormalBalance.Credit, parentCode: "51", allowPosting: true, sortOrder: sort++);
        template.AddItem("5120", "سود (زیان) انباشته", AccountLevel.Subsidiary, AccountNature.Equity, NormalBalance.Credit, parentCode: "51", allowPosting: true, sortOrder: sort++);

        // ===== گروه ۶: درآمدها =====
        template.AddItem("6", "درآمدها", AccountLevel.Group, AccountNature.Revenue, NormalBalance.Credit, parentCode: null, allowPosting: false, sortOrder: sort++);
        template.AddItem("60", "فروش و درآمد ارائه خدمات", AccountLevel.General, AccountNature.Revenue, NormalBalance.Credit, parentCode: "6", allowPosting: false, sortOrder: sort++);
        template.AddItem("6010", "فروش کالای تجاری", AccountLevel.Subsidiary, AccountNature.Revenue, NormalBalance.Credit, parentCode: "60", allowPosting: true, sortOrder: sort++);
        template.AddItem("6020", "درآمد ارائه خدمات فنی و مهندسی", AccountLevel.Subsidiary, AccountNature.Revenue, NormalBalance.Credit, parentCode: "60", allowPosting: true, sortOrder: sort++);
        template.AddItem("6090", "برگشت از فروش و تخفیفات", AccountLevel.Subsidiary, AccountNature.Revenue, NormalBalance.Debit, parentCode: "60", allowPosting: true, sortOrder: sort++);

        // ===== گروه ۷: بهای تمام‌شده و هزینه‌ها =====
        template.AddItem("7", "بهای تمام‌شده و هزینه‌های عملیاتی", AccountLevel.Group, AccountNature.Expense, NormalBalance.Debit, parentCode: null, allowPosting: false, sortOrder: sort++);
        template.AddItem("70", "بهای تمام‌شده کالای فروش‌رفته", AccountLevel.General, AccountNature.Expense, NormalBalance.Debit, parentCode: "7", allowPosting: false, sortOrder: sort++);
        template.AddItem("7010", "بهای تمام‌شده کالای تجاری فروش‌رفته", AccountLevel.Subsidiary, AccountNature.Expense, NormalBalance.Debit, parentCode: "70", allowPosting: true, sortOrder: sort++);

        template.AddItem("71", "هزینه‌های عمومی، اداری و تشکیلاتی", AccountLevel.General, AccountNature.Expense, NormalBalance.Debit, parentCode: "7", allowPosting: false, sortOrder: sort++);
        template.AddItem("7110", "هزینه حقوق، دستمزد و مزایای کارکنان", AccountLevel.Subsidiary, AccountNature.Expense, NormalBalance.Debit, parentCode: "71", allowPosting: true, sortOrder: sort++);
        template.AddItem("7120", "هزینه اجاره اماکن و شعب", AccountLevel.Subsidiary, AccountNature.Expense, NormalBalance.Debit, parentCode: "71", allowPosting: true, sortOrder: sort++);
        template.AddItem("7130", "هزینه استهلاک دارایی‌های ثابت", AccountLevel.Subsidiary, AccountNature.Expense, NormalBalance.Debit, parentCode: "71", allowPosting: true, sortOrder: sort++);
        template.AddItem("7140", "هزینه آب، برق، گاز، تلفن و اینترنت", AccountLevel.Subsidiary, AccountNature.Expense, NormalBalance.Debit, parentCode: "71", allowPosting: true, sortOrder: sort++);

        template.AddItem("80", "هزینه‌های مالی", AccountLevel.General, AccountNature.Expense, NormalBalance.Debit, parentCode: "7", allowPosting: false, sortOrder: sort++);
        template.AddItem("8010", "هزینه کارمزد بانکی و سود تسهیلات", AccountLevel.Subsidiary, AccountNature.Expense, NormalBalance.Debit, parentCode: "80", allowPosting: true, sortOrder: sort++);

        return template;
    }
}
