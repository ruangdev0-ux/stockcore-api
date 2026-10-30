using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StockCore.Application.Auth;
using StockCore.Application.Common;
using StockCore.Domain.Entities;
using StockCore.Domain.Enums;
using StockCore.Infrastructure.Persistence;

namespace StockCore.IntegrationTests.Infrastructure;

/// <summary>
/// Sobe a API inteira em memória apontando para um PostgreSQL real (o do CI ou o local).
/// As migrations do EF são aplicadas na inicialização, então os testes também validam as migrations.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string DefaultPassword = "senha-de-teste-123";
    private const string JwtKey = "chave-usada-somente-nos-testes-automatizados-0123456789";

    private string? passwordHash;

    public static string ConnectionString =>
        Environment.GetEnvironmentVariable("TEST_CONNECTION_STRING")
        ?? "Host=localhost;Port=5432;Database=stockcore_test;Username=stockcore;Password=stockcore";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", ConnectionString);
        builder.UseSetting("Jwt:Key", JwtKey);
        builder.UseSetting("Database:ApplyMigrationsOnStartup", "true");
        builder.UseSetting("Database:Seed", "false");
        builder.UseSetting("Logging:LogLevel:Default", "Warning");
    }

    Task IAsyncLifetime.InitializeAsync()
    {
        // Força a inicialização do servidor (e das migrations) uma única vez.
        _ = Server;
        return Task.CompletedTask;
    }

    Task IAsyncLifetime.DisposeAsync() => DisposeAsync().AsTask();

    public async Task ResetDatabaseAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE stock_movements, products, suppliers, categories, users CASCADE");
    }

    /// <summary>Cria um usuário direto no banco (senha <see cref="DefaultPassword"/>).</summary>
    public async Task<User> CreateUserAsync(UserRole role, string? email = null)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        passwordHash ??= scope.ServiceProvider.GetRequiredService<IPasswordHasher>().Hash(DefaultPassword);

        var user = new User(
            $"Usuário {role}",
            email ?? $"{role.ToString().ToLowerInvariant()}-{Guid.NewGuid():N}@teste.com",
            passwordHash,
            role,
            DateTime.UtcNow);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    /// <summary>Cliente HTTP já autenticado com um usuário novo do papel informado.</summary>
    public async Task<HttpClient> CreateClientAsAsync(UserRole role)
    {
        var user = await CreateUserAsync(role);
        var token = Services.GetRequiredService<ITokenService>().CreateToken(user);
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token.Token);
        return client;
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
