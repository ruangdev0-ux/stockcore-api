using Microsoft.EntityFrameworkCore;

namespace StockCore.Infrastructure.Persistence;

/// <summary>
/// Configuração única do provedor, usada pela API e pelo dotnet-ef,
/// para que as migrations sejam geradas com o mesmo modelo usado em execução.
/// </summary>
public static class DbContextOptionsSetup
{
    public const string LocalConnectionString =
        "Host=localhost;Port=5432;Database=stockcore;Username=stockcore;Password=stockcore";

    public static DbContextOptionsBuilder Configure(DbContextOptionsBuilder options, string connectionString) =>
        options
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention();
}
