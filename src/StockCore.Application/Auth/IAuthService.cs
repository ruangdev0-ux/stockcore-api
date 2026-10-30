using StockCore.Application.Users;
using StockCore.Domain.Entities;

namespace StockCore.Application.Auth;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    Task<UserResponse> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken);
}

public interface ITokenService
{
    AccessToken CreateToken(User user);
}
