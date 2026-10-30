using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StockCore.Application.Alerts;
using StockCore.Application.Auth;
using StockCore.Application.Categories;
using StockCore.Application.Common;
using StockCore.Application.Movements;
using StockCore.Application.Products;
using StockCore.Application.Reports;
using StockCore.Application.Suppliers;
using StockCore.Application.Users;
using StockCore.Infrastructure.Persistence;
using StockCore.Infrastructure.Security;
using StockCore.Infrastructure.Services;

namespace StockCore.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddDbContext<AppDbContext>((provider, options) =>
        {
            var connectionString = provider.GetRequiredService<IConfiguration>().GetConnectionString("Default");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "Connection string ausente. Defina ConnectionStrings:Default (ou a variável ConnectionStrings__Default).");
            }

            DbContextOptionsSetup.Configure(options, connectionString);
        });

        services.AddOptions<JwtOptions>()
            .BindConfiguration(JwtOptions.SectionName)
            .Validate(
                o => !string.IsNullOrWhiteSpace(o.Key) && o.Key.Length >= JwtOptions.MinimumKeyLength,
                $"Jwt:Key precisa ter pelo menos {JwtOptions.MinimumKeyLength} caracteres. Defina a variável Jwt__Key ou use dotnet user-secrets.")
            .Validate(o => o.ExpirationMinutes > 0, "Jwt:ExpirationMinutes deve ser maior que zero.")
            .ValidateOnStart();

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<ISupplierService, SupplierService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IMovementService, MovementService>();
        services.AddScoped<IAlertService, AlertService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<DataSeeder>();

        services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");

        return services;
    }
}
