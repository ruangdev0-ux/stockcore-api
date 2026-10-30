namespace StockCore.IntegrationTests;

public class HealthAndDocsTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Health_responde_sem_login_e_checa_o_banco()
    {
        var response = await Anonymous.GetAsync("/health");

        await response.ShouldHaveStatusAsync(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetString().Should().Be("Healthy");
        body.GetProperty("checks").GetProperty("database").GetProperty("status").GetString().Should().Be("Healthy");
    }

    [Fact]
    public async Task Swagger_documenta_as_rotas_e_o_esquema_bearer()
    {
        var response = await Anonymous.GetAsync("/swagger/v1/swagger.json");

        await response.ShouldHaveStatusAsync(HttpStatusCode.OK);
        var spec = await response.Content.ReadFromJsonAsync<JsonElement>();
        var paths = spec.GetProperty("paths");
        foreach (var path in new[]
                 {
                     "/api/auth/login", "/api/products", "/api/movements/exits", "/api/movements/{id}/cancel",
                     "/api/alerts/low-stock", "/api/reports/stock-summary",
                 })
        {
            paths.TryGetProperty(path, out _).Should().BeTrue($"a rota {path} deve estar documentada");
        }

        spec.GetProperty("components").GetProperty("securitySchemes").TryGetProperty("Bearer", out _).Should().BeTrue();
    }
}
