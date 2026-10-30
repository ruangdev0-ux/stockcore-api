using System.Security.Claims;
using StockCore.Infrastructure.Security;

namespace StockCore.Api.Configuration;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(JwtClaimNames.Subject), out var id)
            ? id
            : throw new InvalidOperationException("Token sem a claim 'sub'.");
}
