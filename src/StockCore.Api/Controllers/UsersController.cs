using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockCore.Api.Configuration;
using StockCore.Application.Users;

namespace StockCore.Api.Controllers;

/// <summary>Gestão de usuários (somente Admin).</summary>
[Route("api/users")]
[Authorize(Roles = Roles.Admin)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public sealed class UsersController(IUserService users) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<UserResponse>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<UserResponse>> List(CancellationToken cancellationToken) =>
        users.ListAsync(cancellationToken);

    /// <summary>Cria um usuário com papel Admin, Operator ou Viewer.</summary>
    [HttpPost]
    [ProducesResponseType<UserResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserResponse>> Create(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await users.CreateAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, user);
    }
}
