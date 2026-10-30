using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace StockCore.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    /// <summary>
    /// Aplica as migrations e o seed conforme a configuração
    /// (Database:ApplyMigrationsOnStartup e Database:Seed). Ambos ficam desligados por padrão.
    /// </summary>
    public static async Task InitializeDatabaseAsync(
        this IServiceProvider services, IConfiguration configuration, CancellationToken cancellationToken = default)
    {
        var applyMigrations = configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup");
        var seed = configuration.GetValue<bool>("Database:Seed");
        if (!applyMigrations && !seed)
        {
            return;
        }

        await using var scope = services.CreateAsyncScope();
        if (applyMigrations)
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync(cancellationToken);
        }

        if (seed)
        {
            await scope.ServiceProvider.GetRequiredService<DataSeeder>().SeedAsync(cancellationToken);
        }
    }
}
