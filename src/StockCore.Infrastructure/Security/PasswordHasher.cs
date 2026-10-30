using Microsoft.AspNetCore.Identity;
using StockCore.Domain.Entities;
using IAppPasswordHasher = StockCore.Application.Common.IPasswordHasher;

namespace StockCore.Infrastructure.Security;

/// <summary>Usa o PasswordHasher do ASP.NET Core Identity (PBKDF2 com salt), sem o Identity completo.</summary>
public sealed class PasswordHasher : IAppPasswordHasher
{
    private readonly PasswordHasher<User> hasher = new();

    public string Hash(string password) => hasher.HashPassword(null!, password);

    public bool Verify(string passwordHash, string password) =>
        hasher.VerifyHashedPassword(null!, passwordHash, password) != PasswordVerificationResult.Failed;
}
