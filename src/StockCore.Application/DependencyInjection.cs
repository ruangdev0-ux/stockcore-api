using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace StockCore.Application;

public static class DependencyInjection
{
    /// <summary>Registra todos os validadores desta camada.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        return services;
    }
}
