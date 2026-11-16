using ERP.BuildingBlocks.Exceptions;
using ERP.BuildingBlocks.Results;
using ERP.Modules.Accounting.Application.Commands;
using ERP.Modules.Accounting.Application.DTOs;
using ERP.Modules.Accounting.Domain.Enums;
using FluentValidation;

namespace ERP.Modules.Accounting.Application.Validators;

public class CreateDocumentRequestValidator : AbstractValidator<CreateDocumentRequest>
{
    public CreateDocumentRequestValidator()
    {
        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("شرح سند حسابداری الزامی است.")
            .MaximumLength(500).WithMessage("شرح سند حداکثر می‌تواند ۵۰۰ کاراکتر باشد.");

        RuleFor(x => x.DocumentDate)
            .NotEmpty().WithMessage("تاریخ سند الزامی است.")
            .Must(d => d != default).WithMessage("تاریخ سند نامعتبر است.");

        RuleFor(x => x.Lines)
            .NotNull().WithMessage("فهرست ردیف‌های سند الزامی است.");

        RuleForEach(x => x.Lines)
            .SetValidator(new VoucherLineInputValidator());

        RuleFor(x => x.ReferenceNumber)
            .MaximumLength(100).WithMessage("شماره ارجاع حداکثر ۱۰۰ کاراکتر است.");

        RuleFor(x => x.IdempotencyKey)
            .MaximumLength(128).WithMessage("کلید ایدمپوتنسی حداکثر ۱۲۸ کاراکتر است.")
            .When(x => !string.IsNullOrEmpty(x.IdempotencyKey));

        RuleFor(x => x)
            .Must(x => x.SourceModule is null || DocumentSourceModules.IsKnown(x.SourceModule))
            .WithMessage($"منشأ سند باید یکی از مقادیر استاندارد باشد: {string.Join(", ", DocumentSourceModules.All)}");
    }
}

public class UpdateDocumentRequestValidator : AbstractValidator<UpdateDocumentRequest>
{
    public UpdateDocumentRequestValidator()
    {
        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("شرح سند حسابداری الزامی است.")
            .MaximumLength(500).WithMessage("شرح سند حداکثر می‌تواند ۵۰۰ کاراکتر باشد.");

        RuleFor(x => x.Lines)
            .NotNull().WithMessage("فهرست ردیف‌های سند الزامی است.");

        RuleForEach(x => x.Lines)
            .SetValidator(new VoucherLineInputValidator());

        RuleFor(x => x.ReferenceNumber)
            .MaximumLength(100).WithMessage("شماره ارجاع حداکثر ۱۰۰ کاراکتر است.");
    }
}

public class VoucherLineInputValidator : AbstractValidator<VoucherLineInput>
{
    public VoucherLineInputValidator()
    {
        RuleFor(x => x.AccountId)
            .NotEmpty().WithMessage("شناسه حساب برای هر ردیف سند الزامی است.");

        RuleFor(x => x.Side)
            .IsInEnum().WithMessage("سمت ردیف سند باید بدهکار یا بستانکار باشد.");

        RuleFor(x => x.Amount)
            .GreaterThan(0m).WithMessage("مبلغ ردیف سند باید عددی مثبت و بزرگتر از صفر باشد.")
            .LessThanOrEqualTo(999_999_999_999_999m).WithMessage("مبلغ ردیف سند از سقف مجاز عبور کرده است.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("شرح ردیف سند الزامی است.")
            .MaximumLength(500).WithMessage("شرح ردیف سند حداکثر ۵۰۰ کاراکتر است.");

        RuleFor(x => x.ExchangeRate)
            .GreaterThan(0m).WithMessage("نرخ تسعیر باید مثبت باشد.")
            .When(x => x.ExchangeRate != default);

        RuleFor(x => x.ForeignAmount)
            .GreaterThan(0m).WithMessage("مبلغ ارزی باید مثبت باشد.")
            .When(x => x.ForeignAmount.HasValue);

        RuleFor(x => x.ForeignCurrencyCode)
            .NotEmpty().WithMessage("ثبت مبلغ ارزی بدون کد ارز مجاز نیست.")
            .When(x => x.ForeignAmount.HasValue)
            .MaximumLength(3).WithMessage("کد ارز باید ۳ حرف باشد.")
            .When(x => !string.IsNullOrEmpty(x.ForeignCurrencyCode));
    }
}

public class RejectDocumentRequestValidator : AbstractValidator<RejectDocumentRequest>
{
    public RejectDocumentRequestValidator()
    {
        RuleFor(x => x.RejectionReason)
            .NotEmpty().WithMessage("دلیل رد سند الزامی است.")
            .MaximumLength(500).WithMessage("دلیل رد سند حداکثر ۵۰۰ کاراکتر است.");
    }
}

public class ReverseDocumentRequestValidator : AbstractValidator<ReverseDocumentRequest>
{
    public ReverseDocumentRequestValidator()
    {
        RuleFor(x => x.ReversalDate)
            .Must(d => d != default).WithMessage("تاریخ سند معکوس الزامی است.");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("شرح سند معکوس حداکثر ۵۰۰ کاراکتر است.")
            .When(x => !string.IsNullOrEmpty(x.Description));

        RuleFor(x => x.IdempotencyKey)
            .MaximumLength(128).WithMessage("کلید ایدمپوتنسی حداکثر ۱۲۸ کاراکتر است.")
            .When(x => !string.IsNullOrEmpty(x.IdempotencyKey));
    }
}

public static class DocumentRequestValidatorExtensions
{
    /// <summary>اعتبارسنجی برنامه‌نویسی‌شده درخواست‌ها (برای فراخوانی از سرویس‌ها، خارج از Controller).</summary>
    public static async Task ValidateAndThrowIfInvalidAsync<T>(this IValidator<T> validator, T instance, CancellationToken cancellationToken = default)
    {
        var result = await validator.ValidateAsync(instance, cancellationToken);
        if (result.IsValid)
            return;

        var errors = result.Errors
            .Select(f => new Error(f.PropertyName, f.ErrorMessage))
            .ToList();

        throw new global::ERP.BuildingBlocks.Exceptions.ValidationException(errors);
    }
}
