using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockCore.Api.Configuration;
using StockCore.Application.Common;
using StockCore.Application.Suppliers;

namespace StockCore.Api.Controllers;

/// <summary>Fornecedores. Leitura para todos; escrita só Admin.</summary>
[Route("api/suppliers")]
public sealed class SuppliersController(ISupplierService suppliers) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<SupplierResponse>>(StatusCodes.Status200OK)]
    public Task<PagedResult<SupplierResponse>> List([FromQuery] SupplierQuery query, CancellationToken cancellationToken) =>
        suppliers.ListAsync(query, cancellationToken);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<SupplierResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<SupplierResponse> Get(Guid id, CancellationToken cancellationToken) =>
        suppliers.GetAsync(id, cancellationToken);

    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<SupplierResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SupplierResponse>> Create(SupplierRequest request, CancellationToken cancellationToken)
    {
        var supplier = await suppliers.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = supplier.Id }, supplier);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<SupplierResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<SupplierResponse> Update(Guid id, SupplierRequest request, CancellationToken cancellationToken) =>
        suppliers.UpdateAsync(id, request, cancellationToken);

    /// <summary>Inativa o fornecedor. Ele deixa de poder ser vinculado a novos produtos.</summary>
    [HttpPatch("{id:guid}/deactivate")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<SupplierResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<SupplierResponse> Deactivate(Guid id, CancellationToken cancellationToken) =>
        suppliers.DeactivateAsync(id, cancellationToken);
}
