using FluentValidation;

namespace StockCore.Application.Users;

public sealed class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Informe o nome.")
            .Length(2, 150).WithMessage("O nome deve ter entre 2 e 150 caracteres.");
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Informe o e-mail.")
            .MaximumLength(200).WithMessage("O e-mail deve ter no máximo 200 caracteres.")
            .EmailAddress().WithMessage("E-mail inválido.");
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Informe a senha.")
            .Length(8, 100).WithMessage("A senha deve ter entre 8 e 100 caracteres.");
        RuleFor(x => x.Role)
            .IsInEnum().WithMessage("Papel inválido. Use Admin, Operator ou Viewer.");
    }
}
