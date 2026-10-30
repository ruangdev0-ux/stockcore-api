using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace StockCore.Infrastructure.Security;

/// <summary>Seção "Jwt" da configuração. A chave nunca fica no repositório: vem de variável de ambiente ou user-secrets.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public const int MinimumKeyLength = 32;

    public string Issuer { get; set; } = "StockCore.Api";

    public string Audience { get; set; } = "StockCore.Client";

    public string Key { get; set; } = string.Empty;

    public int ExpirationMinutes { get; set; } = 60;

    public SymmetricSecurityKey GetSigningKey() => new(Encoding.UTF8.GetBytes(Key));
}

/// <summary>Nomes das claims gravadas no token.</summary>
public static class JwtClaimNames
{
    public const string Subject = "sub";
    public const string Email = "email";
    public const string Name = "name";
    public const string Role = "role";
    public const string TokenId = "jti";
}
