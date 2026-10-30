using StockCore.Domain.Enums;

namespace StockCore.IntegrationTests.Infrastructure;

[Collection(ApiCollection.Name)]
public abstract class IntegrationTestBase(ApiFactory factory) : IAsyncLifetime
{
    protected ApiFactory Factory { get; } = factory;

    protected HttpClient Admin { get; private set; } = null!;

    protected HttpClient Operator { get; private set; } = null!;

    protected HttpClient Viewer { get; private set; } = null!;

    protected HttpClient Anonymous { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await Factory.ResetDatabaseAsync();
        Admin = await Factory.CreateClientAsAsync(UserRole.Admin);
        Operator = await Factory.CreateClientAsAsync(UserRole.Operator);
        Viewer = await Factory.CreateClientAsAsync(UserRole.Viewer);
        Anonymous = Factory.CreateClient();
    }

    public Task DisposeAsync()
    {
        Admin.Dispose();
        Operator.Dispose();
        Viewer.Dispose();
        Anonymous.Dispose();
        return Task.CompletedTask;
    }
}
