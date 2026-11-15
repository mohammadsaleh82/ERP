using ERP.Modules.Accounting.Application.DTOs;
using FluentValidation;

namespace ERP.Modules.Accounting.Application.Validators;

public class CreateFiscalYearRequestValidator : AbstractValidator<CreateFiscalYearRequest>
{
    public CreateFiscalYearRequestValidator()
    {
        RuleFor(x => x.YearNumber)
            .GreaterThan(0).WithMessage("شماره سال مالی باید عددی مثبت باشد.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("عنوان سال مالی الزامی است.")
            .MaximumLength(100).WithMessage("عنوان سال مالی نمی‌تواند بیش از ۱۰۰ کاراکتر باشد.");

        When(x => x.StartDate.HasValue && x.EndDate.HasValue, () =>
        {
            RuleFor(x => x.EndDate!.Value)
                .GreaterThan(x => x.StartDate!.Value)
                .WithMessage("تاریخ پایان سال مالی باید پس از تاریخ شروع باشد.");
        });
    }
}

public class UpdateFiscalYearRequestValidator : AbstractValidator<UpdateFiscalYearRequest>
{
    public UpdateFiscalYearRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("عنوان سال مالی الزامی است.")
            .MaximumLength(100).WithMessage("عنوان سال مالی نمی‌تواند بیش از ۱۰۰ کاراکتر باشد.");
    }
}

public class CreateCustomPeriodRequestValidator : AbstractValidator<CreateCustomPeriodRequest>
{
    public CreateCustomPeriodRequestValidator()
    {
        RuleFor(x => x.PeriodNumber)
            .InclusiveBetween(1, 24).WithMessage("شماره دوره مالی باید بین ۱ تا ۲۴ باشد.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("عنوان دوره مالی الزامی است.")
            .MaximumLength(100).WithMessage("عنوان دوره مالی نمی‌تواند بیش از ۱۰۰ کاراکتر باشد.");

        RuleFor(x => x.EndDate)
            .GreaterThanOrEqualTo(x => x.StartDate)
            .WithMessage("تاریخ پایان دوره مالی نمی‌تواند قبل از تاریخ شروع باشد.");
    }
}

public class UpdateAccountingSettingsRequestValidator : AbstractValidator<UpdateAccountingSettingsRequest>
{
    public UpdateAccountingSettingsRequestValidator()
    {
        RuleFor(x => x.DefaultCurrencyCode)
            .NotEmpty().WithMessage("کد واحد پول الزامی است.")
            .Length(3).WithMessage("کد واحد پول باید دقیقاً ۳ کاراکتر استاندارد ایزو باشد.");

        RuleFor(x => x.DecimalPlaces)
            .InclusiveBetween(0, 4).WithMessage("تعداد ارقام اعشار باید بین ۰ تا ۴ باشد.");

        RuleFor(x => x.FiscalYearStartMonth)
            .InclusiveBetween(1, 12).WithMessage("ماه شروع سال مالی باید بین ۱ تا ۱۲ باشد.");

        RuleFor(x => x.FiscalYearStartDay)
            .InclusiveBetween(1, 31).WithMessage("روز شروع سال مالی باید بین ۱ تا ۳۱ باشد.");
    }
}
