using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockCore.Api.Configuration;
using StockCore.Application.Common;
using StockCore.Application.Movements;

namespace StockCore.Api.Controllers;

/// <summary>
/// Movimentações de estoque. Não existe DELETE: um lançamento errado é cancelado,
/// o que gera um estorno e mantém o histórico.
/// </summary>
[Route("api/movements")]
public sealed class MovementsController(IMovementService movements) : ApiControllerBase
{
    /// <summary>Lista com filtros: productId, type, status, from, to, page e pageSize.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<MovementResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<PagedResult<MovementResponse>> List([FromQuery] MovementQuery query, CancellationToken cancellationToken) =>
        movements.ListAsync(query, cancellationToken);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<MovementResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<MovementResponse> Get(Guid id, CancellationToken cancellationToken) =>
        movements.GetAsync(id, cancellationToken);

    /// <summary>Registra uma entrada (Admin ou Operator).</summary>
    [HttpPost("entries")]
    [Authorize(Roles = Roles.AdminOrOperator)]
    [ProducesResponseType<StockOperationResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<StockOperationResponse>> Entry(EntryRequest request, CancellationToken cancellationToken) =>
        CreatedMovement(await movements.RegisterEntryAsync(request, User.GetUserId(), cancellationToken));

    /// <summary>
    /// Registra uma saída (Admin ou Operator). Saída maior que o saldo responde 422 com
    /// details.available e details.requested. lowStockAlert indica saldo no mínimo ou abaixo.
    /// </summary>
    [HttpPost("exits")]
    [Authorize(Roles = Roles.AdminOrOperator)]
    [ProducesResponseType<StockOperationResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<StockOperationResponse>> Exit(ExitRequest request, CancellationToken cancellationToken) =>
        CreatedMovement(await movements.RegisterExitAsync(request, User.GetUserId(), cancellationToken));

    /// <summary>
    /// Cancela uma entrada ou saída (só Admin): marca como cancelada, cria o estorno e
    /// ajusta o saldo na mesma transação. O motivo é obrigatório.
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<StockOperationResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<StockOperationResponse>> Cancel(
        Guid id, CancelMovementRequest request, CancellationToken cancellationToken) =>
        CreatedMovement(await movements.CancelAsync(id, request, User.GetUserId(), cancellationToken));

    private CreatedAtActionResult CreatedMovement(StockOperationResponse result) =>
        CreatedAtAction(nameof(Get), new { id = result.Movement.Id }, result);
}
