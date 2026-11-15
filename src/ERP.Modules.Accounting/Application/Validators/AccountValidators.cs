using ERP.Modules.Accounting.Application.DTOs;
using FluentValidation;

namespace ERP.Modules.Accounting.Application.Validators;

public class CreateAccountValidator : AbstractValidator<CreateAccountRequest>
{
    public CreateAccountValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("کد حساب الزامی است.")
            .MaximumLength(20).WithMessage("کد حساب نمی‌تواند بیش از ۲۰ کاراکتر باشد.")
            .Matches("^[0-9]+$").WithMessage("کد حساب باید صرفاً شامل ارقام عددی باشد.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("نام حساب الزامی است.")
            .MaximumLength(200).WithMessage("نام حساب نمی‌تواند بیش از ۲۰۰ کاراکتر باشد.");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("توضیحات نمی‌تواند بیش از ۵۰۰ کاراکتر باشد.");
    }
}

public class UpdateAccountValidator : AbstractValidator<UpdateAccountRequest>
{
    public UpdateAccountValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("نام حساب الزامی است.")
            .MaximumLength(200).WithMessage("نام حساب نمی‌تواند بیش از ۲۰۰ کاراکتر باشد.");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("توضیحات نمی‌تواند بیش از ۵۰۰ کاراکتر باشد.");
    }
}
