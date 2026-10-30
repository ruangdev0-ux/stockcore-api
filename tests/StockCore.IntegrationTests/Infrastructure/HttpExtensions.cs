using System.Text.Json.Serialization;
using StockCore.Application.Categories;
using StockCore.Application.Movements;
using StockCore.Application.Products;
using StockCore.Application.Suppliers;

namespace StockCore.IntegrationTests.Infrastructure;

public static class HttpExtensions
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static Task<HttpResponseMessage> PostJsonAsync(this HttpClient client, string url, object body) =>
        client.PostAsJsonAsync(url, body, Json);

    public static Task<HttpResponseMessage> PutJsonAsync(this HttpClient client, string url, object body) =>
        client.PutAsJsonAsync(url, body, Json);

    public static async Task<T> ReadAsAsync<T>(this HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(Json))!;

    public static async Task<T> GetAsAsync<T>(this HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        await response.ShouldHaveStatusAsync(HttpStatusCode.OK);
        return await response.ReadAsAsync<T>();
    }

    /// <summary>Compara o status e, se falhar, mostra o corpo da resposta na mensagem.</summary>
    public static async Task ShouldHaveStatusAsync(this HttpResponseMessage response, HttpStatusCode expected)
    {
        if (response.StatusCode != expected)
        {
            var body = await response.Content.ReadAsStringAsync();
            response.StatusCode.Should().Be(expected, "a resposta foi: {0}", body);
        }
    }

    /// <summary>Confere status, content-type e código do ProblemDetails e devolve o corpo.</summary>
    public static async Task<JsonElement> ShouldBeProblemAsync(
        this HttpResponseMessage response, HttpStatusCode status, string code)
    {
        await response.ShouldHaveStatusAsync(status);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("status").GetInt32().Should().Be((int)status);
        problem.GetProperty("code").GetString().Should().Be(code);
        return problem;
    }

    public static async Task<CategoryResponse> CreateCategoryAsync(this HttpClient admin, string? name = null)
    {
        var response = await admin.PostJsonAsync("/api/categories", new { name = name ?? $"Categoria {Guid.NewGuid():N}"[..20] });
        await response.ShouldHaveStatusAsync(HttpStatusCode.Created);
        return await response.ReadAsAsync<CategoryResponse>();
    }

    public static async Task<SupplierResponse> CreateSupplierAsync(this HttpClient admin, string? document = null)
    {
        var response = await admin.PostJsonAsync("/api/suppliers", new { name = "Fornecedor Teste", document });
        await response.ShouldHaveStatusAsync(HttpStatusCode.Created);
        return await response.ReadAsAsync<SupplierResponse>();
    }

    public static async Task<ProductResponse> CreateProductAsync(
        this HttpClient admin,
        Guid categoryId,
        string sku,
        int minimumStock = 0,
        decimal unitPrice = 10m,
        Guid? supplierId = null,
        string? name = null)
    {
        var response = await admin.PostJsonAsync("/api/products", new
        {
            sku,
            name = name ?? $"Produto {sku}",
            categoryId,
            supplierId,
            unitPrice,
            minimumStock,
        });
        await response.ShouldHaveStatusAsync(HttpStatusCode.Created);
        return await response.ReadAsAsync<ProductResponse>();
    }

    /// <summary>Cria uma categoria e um produto, já com saldo inicial quando informado.</summary>
    public static async Task<ProductResponse> CreateStockedProductAsync(
        this HttpClient admin, int stock, int minimumStock = 0, string? sku = null)
    {
        var category = await admin.CreateCategoryAsync();
        var product = await admin.CreateProductAsync(category.Id, sku ?? $"SKU-{Guid.NewGuid():N}"[..12], minimumStock);
        if (stock > 0)
        {
            await admin.EntryAsync(product.Id, stock);
        }

        return product;
    }

    public static Task<HttpResponseMessage> PostEntryAsync(this HttpClient client, Guid productId, int quantity) =>
        client.PostJsonAsync("/api/movements/entries", new { productId, quantity });

    public static Task<HttpResponseMessage> PostExitAsync(this HttpClient client, Guid productId, int quantity) =>
        client.PostJsonAsync("/api/movements/exits", new { productId, quantity });

    public static Task<HttpResponseMessage> PostCancelAsync(this HttpClient client, Guid movementId, string reason = "Lançamento incorreto") =>
        client.PostJsonAsync($"/api/movements/{movementId}/cancel", new { reason });

    public static async Task<StockOperationResponse> EntryAsync(this HttpClient client, Guid productId, int quantity)
    {
        var response = await client.PostEntryAsync(productId, quantity);
        await response.ShouldHaveStatusAsync(HttpStatusCode.Created);
        return await response.ReadAsAsync<StockOperationResponse>();
    }

    public static async Task<StockOperationResponse> ExitAsync(this HttpClient client, Guid productId, int quantity)
    {
        var response = await client.PostExitAsync(productId, quantity);
        await response.ShouldHaveStatusAsync(HttpStatusCode.Created);
        return await response.ReadAsAsync<StockOperationResponse>();
    }

    public static async Task<int> CurrentStockAsync(this HttpClient client, Guid productId) =>
        (await client.GetAsAsync<ProductResponse>($"/api/products/{productId}")).CurrentStock;
}
