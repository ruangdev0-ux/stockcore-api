using FluentValidation;

namespace StockCore.Application.Common;

/// <summary>Parâmetros de paginação lidos da query string (?page=1&amp;pageSize=20).</summary>
public abstract class PageQuery
{
    public const int MaxPageSize = 100;

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}

/// <summary>Regras de paginação compartilhadas pelos validadores de consulta.</summary>
public abstract class PageQueryValidator<T> : AbstractValidator<T>
    where T : PageQuery
{
    protected PageQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("A página deve ser maior ou igual a 1.");
        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, PageQuery.MaxPageSize)
            .WithMessage($"O tamanho da página deve ficar entre 1 e {PageQuery.MaxPageSize}.");
    }
}
