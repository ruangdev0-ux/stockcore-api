using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace StockCore.Infrastructure.Persistence;

/// <summary>Usada só pelo dotnet-ef (migrations add / database update).</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? DbContextOptionsSetup.LocalConnectionString;
        var builder = new DbContextOptionsBuilder<AppDbContext>();
        DbContextOptionsSetup.Configure(builder, connectionString);
        return new AppDbContext(builder.Options);
    }
}
