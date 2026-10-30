using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using StockCore.Api.Errors;
using StockCore.Domain.Exceptions;
using StockCore.Infrastructure.Security;

namespace StockCore.Api.Configuration;

public static class AuthenticationSetup
{
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        // Configurado de forma tardia para ler a chave já validada de JwtOptions.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((options, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = jwt.GetSigningKey(),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = JwtClaimNames.Name,
                    RoleClaimType = JwtClaimNames.Role,
                };
                options.Events = new JwtBearerEvents
                {
                    // 401 e 403 no mesmo formato ProblemDetails do resto da API.
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        await ProblemFactory.WriteAsync(
                            context.HttpContext,
                            ProblemFactory.Create(
                                context.HttpContext,
                                StatusCodes.Status401Unauthorized,
                                ErrorCodes.Unauthorized,
                                "Token ausente, inválido ou expirado."));
                    },
                    OnForbidden = context => ProblemFactory.WriteAsync(
                        context.HttpContext,
                        ProblemFactory.Create(
                            context.HttpContext,
                            StatusCodes.Status403Forbidden,
                            ErrorCodes.Forbidden,
                            "Seu papel não permite esta operação.")),
                };
            });

        services.AddAuthorization();
        return services;
    }
}
