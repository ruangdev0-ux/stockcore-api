using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockCore.Api.Configuration;
using StockCore.Application.Common;
using StockCore.Application.Movements;
using StockCore.Application.Products;

namespace StockCore.Api.Controllers;

/// <summary>Produtos. Leitura para todos; cadastro, edição e ativação só Admin.</summary>
[Route("api/products")]
public sealed class ProductsController(IProductService products, IMovementService movements) : ApiControllerBase
{
    /// <summary>
    /// Lista com filtros: search (nome ou SKU), categoryId, supplierId, isActive, lowStock,
    /// sort (name, sku, stock, price, createdAt; prefixo "-" para decrescente), page e pageSize.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<ProductResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<PagedResult<ProductResponse>> List([FromQuery] ProductQuery query, CancellationToken cancellationToken) =>
        products.ListAsync(query, cancellationToken);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ProductResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<ProductResponse> Get(Guid id, CancellationToken cancellationToken) =>
        products.GetAsync(id, cancellationToken);

    /// <summary>Cadastra um produto com saldo zero. O saldo muda só por entradas e saídas.</summary>
    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<ProductResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ProductResponse>> Create(CreateProductRequest request, CancellationToken cancellationToken)
    {
        var product = await products.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = product.Id }, product);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<ProductResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<ProductResponse> Update(Guid id, UpdateProductRequest request, CancellationToken cancellationToken) =>
        products.UpdateAsync(id, request, cancellationToken);

    [HttpPatch("{id:guid}/activate")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<ProductResponse>(StatusCodes.Status200OK)]
    public Task<ProductResponse> Activate(Guid id, CancellationToken cancellationToken) =>
        products.SetActiveAsync(id, true, cancellationToken);

    /// <summary>Inativa o produto: ele continua nas consultas, mas não aceita entradas nem saídas.</summary>
    [HttpPatch("{id:guid}/deactivate")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<ProductResponse>(StatusCodes.Status200OK)]
    public Task<ProductResponse> Deactivate(Guid id, CancellationToken cancellationToken) =>
        products.SetActiveAsync(id, false, cancellationToken);

    /// <summary>Exclui um produto que nunca teve movimentação. Com histórico, responde 409 (use a inativação).</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await products.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>Histórico de movimentações do produto, do mais recente para o mais antigo.</summary>
    [HttpGet("{id:guid}/movements")]
    [ProducesResponseType<PagedResult<MovementResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<PagedResult<MovementResponse>> Movements(
        Guid id, [FromQuery] MovementQuery query, CancellationToken cancellationToken)
    {
        await products.GetAsync(id, cancellationToken);
        query.ProductId = id;
        return await movements.ListAsync(query, cancellationToken);
    }
}
