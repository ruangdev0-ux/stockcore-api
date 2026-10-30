using FluentValidation;
using StockCore.Application.Common;

namespace StockCore.Application.Products;

public sealed class CreateProductRequestValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductRequestValidator()
    {
        RuleFor(x => x.Sku)
            .NotEmpty().WithMessage("Informe o SKU.")
            .MaximumLength(50).WithMessage("O SKU deve ter no máximo 50 caracteres.")
            .Matches("^[A-Za-z0-9._-]+$").WithMessage("O SKU aceita letras, números, ponto, hífen e sublinhado.");
        RuleFor(x => x.Name).ProductName();
        RuleFor(x => x.Description).ProductDescription();
        RuleFor(x => x.CategoryId).NotEmpty().WithMessage("Informe a categoria.");
        RuleFor(x => x.UnitPrice).Price();
        RuleFor(x => x.MinimumStock).MinimumStock();
    }
}

public sealed class UpdateProductRequestValidator : AbstractValidator<UpdateProductRequest>
{
    public UpdateProductRequestValidator()
    {
        RuleFor(x => x.Name).ProductName();
        RuleFor(x => x.Description).ProductDescription();
        RuleFor(x => x.CategoryId).NotEmpty().WithMessage("Informe a categoria.");
        RuleFor(x => x.UnitPrice).Price();
        RuleFor(x => x.MinimumStock).MinimumStock();
    }
}

public sealed class ProductQueryValidator : PageQueryValidator<ProductQuery>
{
    public ProductQueryValidator()
    {
        RuleFor(x => x.Search).MaximumLength(100).WithMessage("A busca deve ter no máximo 100 caracteres.");
        RuleFor(x => x.Sort)
            .Must(sort => sort is null || ProductQuery.SortOptions.Contains(sort))
            .WithMessage($"Ordenação inválida. Use: {string.Join(", ", ProductQuery.SortOptions)}.");
    }
}

internal static class ProductRules
{
    public static IRuleBuilderOptions<T, string> ProductName<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage("Informe o nome do produto.")
            .Length(2, 150).WithMessage("O nome deve ter entre 2 e 150 caracteres.");

    public static IRuleBuilderOptions<T, string?> ProductDescription<T>(this IRuleBuilder<T, string?> rule) =>
        rule.MaximumLength(1000).WithMessage("A descrição deve ter no máximo 1000 caracteres.");

    public static IRuleBuilderOptions<T, decimal> Price<T>(this IRuleBuilder<T, decimal> rule) =>
        rule.GreaterThanOrEqualTo(0m).WithMessage("O preço não pode ser negativo.")
            .PrecisionScale(12, 2, true).WithMessage("O preço aceita no máximo 10 dígitos inteiros e 2 casas decimais.");

    public static IRuleBuilderOptions<T, int> MinimumStock<T>(this IRuleBuilder<T, int> rule) =>
        rule.GreaterThanOrEqualTo(0).WithMessage("O estoque mínimo não pode ser negativo.");
}
