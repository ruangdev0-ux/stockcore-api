using FluentValidation;
using StockCore.Application.Common;

namespace StockCore.Application.Suppliers;

public sealed class SupplierRequestValidator : AbstractValidator<SupplierRequest>
{
    /// <summary>CNPJ com ou sem máscara: 00.000.000/0000-00 ou 14 dígitos.</summary>
    public const string DocumentPattern = @"^(\d{2}\.\d{3}\.\d{3}/\d{4}-\d{2}|\d{14})$";

    public SupplierRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Informe o nome do fornecedor.")
            .Length(2, 150).WithMessage("O nome deve ter entre 2 e 150 caracteres.");
        RuleFor(x => x.Document)
            .Matches(DocumentPattern).WithMessage("CNPJ deve ter o formato 00.000.000/0000-00 ou 14 dígitos.")
            .When(x => !string.IsNullOrWhiteSpace(x.Document));
        RuleFor(x => x.Email)
            .MaximumLength(200).WithMessage("O e-mail deve ter no máximo 200 caracteres.")
            .EmailAddress().WithMessage("E-mail inválido.")
            .When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Phone)
            .MaximumLength(30).WithMessage("O telefone deve ter no máximo 30 caracteres.");
    }
}

public sealed class SupplierQueryValidator : PageQueryValidator<SupplierQuery>
{
    public SupplierQueryValidator()
    {
        RuleFor(x => x.Search).MaximumLength(100).WithMessage("A busca deve ter no máximo 100 caracteres.");
    }
}
