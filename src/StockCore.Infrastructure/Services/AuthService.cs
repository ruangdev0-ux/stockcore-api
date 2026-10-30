using Microsoft.EntityFrameworkCore;
using StockCore.Application.Auth;
using StockCore.Application.Common;
using StockCore.Application.Users;
using StockCore.Domain.Entities;
using StockCore.Domain.Exceptions;
using StockCore.Infrastructure.Persistence;

namespace StockCore.Infrastructure.Services;

public sealed class AuthService(AppDbContext db, IPasswordHasher hasher, ITokenService tokens) : IAuthService
{
    private static string? dummyHash;

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var email = User.NormalizeEmail(request.Email);
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (user is null)
        {
            // Verifica um hash qualquer para o tempo de resposta não revelar se o e-mail existe.
            dummyHash ??= hasher.Hash("senha-ficticia-para-comparacao");
            hasher.Verify(dummyHash, request.Password);
            throw InvalidCredentials();
        }

        if (!hasher.Verify(user.PasswordHash, request.Password))
        {
            throw InvalidCredentials();
        }

        var token = tokens.CreateToken(user);
        return new LoginResponse(token.Token, "Bearer", token.ExpiresAt, UserResponse.From(user));
    }

    public async Task<UserResponse> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new UnauthorizedException("O usuário do token não existe mais.");
        return UserResponse.From(user);
    }

    private static UnauthorizedException InvalidCredentials() => new("E-mail ou senha inválidos.");
}
