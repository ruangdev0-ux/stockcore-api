using FluentValidation;

namespace StockCore.Application.Categories;

public sealed class CategoryRequestValidator : AbstractValidator<CategoryRequest>
{
    public CategoryRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Informe o nome da categoria.")
            .Length(2, 100).WithMessage("O nome deve ter entre 2 e 100 caracteres.");
        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("A descrição deve ter no máximo 500 caracteres.");
    }
}
