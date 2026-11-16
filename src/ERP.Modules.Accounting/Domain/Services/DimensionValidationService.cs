using ERP.Modules.Accounting.Domain.Enums;
using ERP.Modules.Accounting.Domain.Exceptions;

namespace ERP.Modules.Accounting.Domain.Services;

/// <summary>
/// موتور اعتبارسنجی ابعاد مالی ردیف‌های سند (Pure و بدون وابستگی به زیرساخت).
/// قواعد اعمال‌شده در همین لایه دامنه:
///   ۱. بعد باید وجود داشته باشد و نوع اعلام‌شده با نوع بعد منطبق باشد.
///   ۲. بعد باید متعلق به همان شرکت سند باشد (نه شرکت دیگر).
///   ۳. بعد غیرفعال قابل استفاده در سند نیست.
///   ۴. انواع Required حساب باید حتماً روی ردیف حاضر باشند («تفصیلی اجباری»).
///   ۵. انواع Forbidden حساب هرگز روی ردیف پذیرفته نمی‌شوند.
///   ۶. اگر حساب لیست Allowed دارد، فقط انواع لیست‌شده (به‌همراه Required) پذیرفته می‌شوند.
///   ۷. ترکیب‌های ممنوع (سطح نوع یا سطح مقدار) در یک ردیف رد می‌شوند.
/// پرچم‌های قدیمی حساب (RequireDetailAccount/RequireCostCenter/RequireProject) به‌عنوان
/// قواعد Required هم‌زمان با سیاست‌های جدید اعمال می‌شوند.
/// </summary>
public static class DimensionValidationService
{
    public sealed record DimensionMeta(Guid Id, FinancialDimensionType Type, Guid CompanyId, bool IsActive, string Code);

    public sealed record AccountDimensionMeta(
        Guid AccountId,
        string FullCode,
        bool RequireCostCenter,
        bool RequireProject,
        bool RequireDetailAccount,
        IReadOnlyList<(FinancialDimensionType Type, DimensionPolicyMode Mode)> Policies);

    public sealed record LineDimensionInput(
        int LineNumber,
        Guid AccountId,
        IReadOnlyList<(FinancialDimensionType Type, Guid DimensionId)> Assignments);

    public sealed record CombinationRuleInput(
        FinancialDimensionType FirstType,
        Guid? FirstDimensionId,
        FinancialDimensionType SecondType,
        Guid? SecondDimensionId);

    public static void ValidateLines(
        IReadOnlyList<LineDimensionInput> lines,
        IReadOnlyDictionary<Guid, AccountDimensionMeta> accounts,
        IReadOnlyDictionary<Guid, DimensionMeta> dimensions,
        Guid companyId,
        IReadOnlyList<CombinationRuleInput> combinationRules)
    {
        foreach (var line in lines)
        {
            ValidateLine(line, accounts, dimensions, companyId);

            ValidateForbiddenCombinations(line, combinationRules);
        }
    }

    private static void ValidateLine(
        LineDimensionInput line,
        IReadOnlyDictionary<Guid, AccountDimensionMeta> accounts,
        IReadOnlyDictionary<Guid, DimensionMeta> dimensions,
        Guid companyId)
    {
        if (!accounts.TryGetValue(line.AccountId, out var account))
        {
            throw new AccountingDomainException("Accounting.DimensionAccountNotFound",
                $"حساب ردیف {line.LineNumber} برای اعتبارسنجی ابعاد مالی یافت نشد.");
        }

        // --- ۱ تا ۴: اعتبار هر اختصاص ---
        foreach (var assignment in line.Assignments)
        {
            if (!dimensions.TryGetValue(assignment.DimensionId, out var dimension))
            {
                throw new AccountingDomainException("Accounting.DimensionNotFound",
                    $"بعد مالی با شناسه '{assignment.DimensionId}' (نوع '{assignment.Type}') در ردیف {line.LineNumber} یافت نشد.");
            }

            if (dimension.CompanyId != companyId)
            {
                throw new AccountingDomainException("Accounting.DimensionForeignCompany",
                    $"بعد مالی '{dimension.Code}' (نوع '{assignment.Type}') در ردیف {line.LineNumber} متعلق به شرکت دیگری است و روی این سند قابل استفاده نیست.");
            }

            if (!dimension.IsActive)
            {
                throw new AccountingDomainException("Accounting.DimensionInactive",
                    $"بعد مالی غیرفعال '{dimension.Code}' (نوع '{assignment.Type}') در ردیف {line.LineNumber} قابل استفاده نیست.");
            }

            if (dimension.Type != assignment.Type)
            {
                throw new AccountingDomainException("Accounting.DimensionTypeMismatch",
                    $"بعد مالی '{dimension.Code}' در ردیف {line.LineNumber} از نوع '{dimension.Type}' است اما با نوع '{assignment.Type}' ارسال شده.");
            }
        }

        // --- ۴: انواع اجباری (پرچم‌های قدیمی + سیاست‌های جدید) ---
        var presentTypes = line.Assignments.Select(a => a.Type).ToHashSet();

        var requiredTypes = new List<FinancialDimensionType>();
        if (account.RequireDetailAccount)
            requiredTypes.Add(FinancialDimensionType.DetailAccount);
        if (account.RequireCostCenter)
            requiredTypes.Add(FinancialDimensionType.CostCenter);
        if (account.RequireProject)
            requiredTypes.Add(FinancialDimensionType.Project);
        requiredTypes.AddRange(account.Policies
            .Where(p => p.Mode == DimensionPolicyMode.Required)
            .Select(p => p.Type));

        foreach (var requiredType in requiredTypes.Distinct())
        {
            if (!presentTypes.Contains(requiredType))
            {
                throw new AccountingDomainException("Accounting.DimensionRequired",
                    $"حساب '{account.FullCode}' نیازمند بعد مالی نوع '{requiredType}' است؛ ردیف {line.LineNumber} بدون آن قابل ثبت نیست.");
            }
        }

        // --- ۵: انواع ممنوع ---
        var forbiddenTypes = account.Policies
            .Where(p => p.Mode == DimensionPolicyMode.Forbidden)
            .Select(p => p.Type)
            .ToHashSet();

        foreach (var assignment in line.Assignments)
        {
            if (forbiddenTypes.Contains(assignment.Type))
            {
                throw new AccountingDomainException("Accounting.DimensionNotAllowed",
                    $"حساب '{account.FullCode}' نوع بعد مالی '{assignment.Type}' را نمی‌پذیرد؛ ردیف {line.LineNumber} نامعتبر است.");
            }
        }

        // --- ۶: لیست سفید انواع مجاز ---
        var allowedTypes = account.Policies
            .Where(p => p.Mode == DimensionPolicyMode.Allowed)
            .Select(p => p.Type)
            .ToHashSet();

        if (allowedTypes.Count > 0)
        {
            foreach (var presentType in presentTypes)
            {
                if (presentType != FinancialDimensionType.DetailAccount && !allowedTypes.Contains(presentType) && !requiredTypes.Contains(presentType))
                {
                    throw new AccountingDomainException("Accounting.DimensionNotInAllowedSet",
                        $"حساب '{account.FullCode}' فقط انواع بعد مالی مشخص‌شده را می‌پذیرد؛ نوع '{presentType}' در ردیف {line.LineNumber} مجاز نیست.");
                }
            }
        }
    }

    private static void ValidateForbiddenCombinations(
        LineDimensionInput line,
        IReadOnlyList<CombinationRuleInput> combinationRules)
    {
        if (combinationRules.Count == 0 || line.Assignments.Count < 2)
            return;

        var assignments = line.Assignments;
        for (var i = 0; i < assignments.Count; i++)
        {
            for (var j = i + 1; j < assignments.Count; j++)
            {
                foreach (var rule in combinationRules)
                {
                    // تطبیق در سطح نوع (بدون شناسه) یا سطح مقدار (با شناسه)، در هر دو ترتیب
                    if (MatchesRule(rule, assignments[i], assignments[j]) ||
                        MatchesRule(rule, assignments[j], assignments[i]))
                    {
                        throw new AccountingDomainException("Accounting.DimensionCombinationForbidden",
                            $"ترکیب ابعاد '{assignments[i].Type}/{assignments[i].DimensionId}' با '{assignments[j].Type}/{assignments[j].DimensionId}' در ردیف {line.LineNumber} ممنوع است.");
                    }
                }
            }
        }
    }

    private static bool MatchesRule(
        CombinationRuleInput rule,
        (FinancialDimensionType Type, Guid DimensionId) first,
        (FinancialDimensionType Type, Guid DimensionId) second)
    {
        var firstMatches = rule.FirstType == first.Type
                           && (!rule.FirstDimensionId.HasValue || rule.FirstDimensionId.Value == first.DimensionId);
        var secondMatches = rule.SecondType == second.Type
                            && (!rule.SecondDimensionId.HasValue || rule.SecondDimensionId.Value == second.DimensionId);
        return firstMatches && secondMatches;
    }
}
