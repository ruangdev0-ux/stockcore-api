using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace StockCore.Api.Controllers;

/// <summary>Base dos controllers: exige login e documenta os erros comuns.</summary>
[ApiController]
[Authorize]
[Produces("application/json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
public abstract class ApiControllerBase : ControllerBase
{
}
