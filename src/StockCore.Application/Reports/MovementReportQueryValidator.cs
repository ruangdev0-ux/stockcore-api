using FluentValidation;

namespace StockCore.Application.Reports;

public sealed class MovementReportQueryValidator : AbstractValidator<MovementReportQuery>
{
    public const int MaxDays = 366;

    public MovementReportQueryValidator()
    {
        RuleFor(x => x.To)
            .GreaterThanOrEqualTo(x => x.From).WithMessage("A data final deve ser igual ou posterior à inicial.")
            .When(x => x.From.HasValue && x.To.HasValue);
        RuleFor(x => x)
            .Must(x => (x.To!.Value - x.From!.Value).TotalDays <= MaxDays)
            .OverridePropertyName("to")
            .WithMessage($"O período do relatório pode ter no máximo {MaxDays} dias.")
            .When(x => x.From.HasValue && x.To.HasValue && x.To >= x.From);
    }
}
