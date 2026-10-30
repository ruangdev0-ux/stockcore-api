using FluentValidation;
using StockCore.Application.Common;

namespace StockCore.Application.Movements;

public sealed class EntryRequestValidator : AbstractValidator<EntryRequest>
{
    public EntryRequestValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty().WithMessage("Informe o produto.");
        RuleFor(x => x.Quantity).MovementQuantity();
        RuleFor(x => x.UnitCost)
            .GreaterThanOrEqualTo(0m).WithMessage("O custo unitário não pode ser negativo.")
            .PrecisionScale(12, 2, true).WithMessage("O custo aceita no máximo 10 dígitos inteiros e 2 casas decimais.")
            .When(x => x.UnitCost.HasValue);
        RuleFor(x => x.Note).MaximumLength(500).WithMessage("A observação deve ter no máximo 500 caracteres.");
    }
}

public sealed class ExitRequestValidator : AbstractValidator<ExitRequest>
{
    public ExitRequestValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty().WithMessage("Informe o produto.");
        RuleFor(x => x.Quantity).MovementQuantity();
        RuleFor(x => x.Note).MaximumLength(500).WithMessage("A observação deve ter no máximo 500 caracteres.");
    }
}

public sealed class CancelMovementRequestValidator : AbstractValidator<CancelMovementRequest>
{
    public CancelMovementRequestValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Informe o motivo do cancelamento.")
            .Length(3, 500).WithMessage("O motivo deve ter entre 3 e 500 caracteres.");
    }
}

public sealed class MovementQueryValidator : PageQueryValidator<MovementQuery>
{
    public MovementQueryValidator()
    {
        RuleFor(x => x.Type).IsInEnum().WithMessage("Tipo inválido. Use Entry, Exit ou Reversal.");
        RuleFor(x => x.Status).IsInEnum().WithMessage("Status inválido. Use Active ou Cancelled.");
        RuleFor(x => x.To)
            .GreaterThanOrEqualTo(x => x.From).WithMessage("A data final deve ser igual ou posterior à inicial.")
            .When(x => x.From.HasValue && x.To.HasValue);
    }
}

internal static class MovementRules
{
    public const int MaxQuantity = 1_000_000;

    public static IRuleBuilderOptions<T, int> MovementQuantity<T>(this IRuleBuilder<T, int> rule) =>
        rule.GreaterThan(0).WithMessage("A quantidade deve ser um número inteiro maior que zero.")
            .LessThanOrEqualTo(MaxQuantity).WithMessage($"A quantidade máxima por movimentação é {MaxQuantity}.");
}
