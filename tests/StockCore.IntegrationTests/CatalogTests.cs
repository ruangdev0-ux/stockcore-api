using StockCore.Application.Categories;
using StockCore.Application.Common;
using StockCore.Application.Products;
using StockCore.Application.Suppliers;
using StockCore.Domain.Exceptions;

namespace StockCore.IntegrationTests;

public class CatalogTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Categoria_crud_completo()
    {
        var created = await Admin.PostJsonAsync("/api/categories", new { name = "Bebidas", description = "Sucos e águas" });
        await created.ShouldHaveStatusAsync(HttpStatusCode.Created);
        created.Headers.Location.Should().NotBeNull();
        var category = await created.ReadAsAsync<CategoryResponse>();

        var updated = await Admin.PutJsonAsync($"/api/categories/{category.Id}", new { name = "Bebidas frias" });
        await updated.ShouldHaveStatusAsync(HttpStatusCode.OK);

        var fetched = await Viewer.GetAsAsync<CategoryResponse>($"/api/categories/{category.Id}");
        fetched.Name.Should().Be("Bebidas frias");
        fetched.Description.Should().BeNull();
        fetched.ProductCount.Should().Be(0);

        (await Admin.DeleteAsync($"/api/categories/{category.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Admin.GetAsync($"/api/categories/{category.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Categoria_com_nome_repetido_devolve_409()
    {
        await Admin.CreateCategoryAsync("Limpeza");

        var response = await Admin.PostJsonAsync("/api/categories", new { name = "  limpeza " });

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, ErrorCodes.Conflict);
    }

    [Fact]
    public async Task Categoria_com_produtos_nao_e_excluida()
    {
        var category = await Admin.CreateCategoryAsync();
        await Admin.CreateProductAsync(category.Id, "CAT-1");

        var response = await Admin.DeleteAsync($"/api/categories/{category.Id}");

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, ErrorCodes.Conflict);
        problem.GetProperty("details").GetProperty("productCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Recurso_inexistente_devolve_problem_details_completo()
    {
        var response = await Admin.GetAsync($"/api/categories/{Guid.NewGuid()}");

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, ErrorCodes.NotFound);
        problem.GetProperty("title").GetString().Should().NotBeNullOrWhiteSpace();
        problem.GetProperty("detail").GetString().Should().Be("Categoria não encontrada.");
        problem.GetProperty("instance").GetString().Should().StartWith("/api/categories/");
        problem.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Fornecedor_com_documento_repetido_devolve_409_e_documento_invalido_400()
    {
        await Admin.CreateSupplierAsync("11.111.111/0001-11");

        var duplicated = await Admin.PostJsonAsync("/api/suppliers", new { name = "Outro", document = "11.111.111/0001-11" });
        await duplicated.ShouldBeProblemAsync(HttpStatusCode.Conflict, ErrorCodes.Conflict);

        var invalid = await Admin.PostJsonAsync("/api/suppliers", new { name = "Outro", document = "123" });
        var problem = await invalid.ShouldBeProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationError);
        problem.GetProperty("errors").TryGetProperty("document", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Fornecedor_inativo_nao_pode_ser_vinculado_a_produto()
    {
        var supplier = await Admin.CreateSupplierAsync();
        var category = await Admin.CreateCategoryAsync();

        var deactivated = await Admin.PatchAsync($"/api/suppliers/{supplier.Id}/deactivate", null);
        await deactivated.ShouldHaveStatusAsync(HttpStatusCode.OK);
        (await deactivated.ReadAsAsync<SupplierResponse>()).IsActive.Should().BeFalse();

        var inactiveList = await Viewer.GetAsAsync<PagedResult<SupplierResponse>>("/api/suppliers?isActive=false");
        inactiveList.Items.Should().ContainSingle(s => s.Id == supplier.Id);

        var response = await Admin.PostJsonAsync("/api/products", new
        {
            sku = "SUP-1",
            name = "Produto",
            categoryId = category.Id,
            supplierId = supplier.Id,
            unitPrice = 1m,
            minimumStock = 0,
        });
        await response.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.BusinessRuleViolation);
    }

    [Fact]
    public async Task Produto_novo_comeca_com_saldo_zero_e_sku_normalizado()
    {
        var category = await Admin.CreateCategoryAsync("Mercearia");

        var product = await Admin.CreateProductAsync(category.Id, "mer-001", minimumStock: 5, unitPrice: 27.90m);

        product.Sku.Should().Be("MER-001");
        product.CurrentStock.Should().Be(0);
        product.IsActive.Should().BeTrue();
        product.IsLowStock.Should().BeTrue();
        product.CategoryName.Should().Be("Mercearia");
        product.UnitPrice.Should().Be(27.90m);
    }

    [Fact]
    public async Task Sku_repetido_devolve_409_mesmo_com_outra_caixa()
    {
        var category = await Admin.CreateCategoryAsync();
        await Admin.CreateProductAsync(category.Id, "ABC-1");

        var response = await Admin.PostJsonAsync("/api/products", new
        {
            sku = "abc-1",
            name = "Outro",
            categoryId = category.Id,
            unitPrice = 1m,
            minimumStock = 0,
        });

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, ErrorCodes.Conflict);
        problem.GetProperty("details").GetProperty("sku").GetString().Should().Be("ABC-1");
    }

    [Fact]
    public async Task Produto_invalido_devolve_400_com_os_campos()
    {
        var response = await Admin.PostJsonAsync("/api/products", new
        {
            sku = "",
            name = "Produto",
            categoryId = Guid.NewGuid(),
            unitPrice = -5m,
            minimumStock = -1,
        });

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationError);
        var errors = problem.GetProperty("errors");
        errors.TryGetProperty("sku", out _).Should().BeTrue();
        errors.TryGetProperty("unitPrice", out _).Should().BeTrue();
        errors.TryGetProperty("minimumStock", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Produto_com_categoria_inexistente_devolve_422()
    {
        var response = await Admin.PostJsonAsync("/api/products", new
        {
            sku = "X-1",
            name = "Produto",
            categoryId = Guid.NewGuid(),
            unitPrice = 1m,
            minimumStock = 0,
        });

        await response.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.BusinessRuleViolation);
    }

    [Fact]
    public async Task Atualizar_produto_mantem_o_sku_e_o_saldo()
    {
        var product = await Admin.CreateStockedProductAsync(stock: 7, sku: "UPD-1");
        var otherCategory = await Admin.CreateCategoryAsync("Outra");

        var response = await Admin.PutJsonAsync($"/api/products/{product.Id}", new
        {
            name = "Nome novo",
            categoryId = otherCategory.Id,
            unitPrice = 3.25m,
            minimumStock = 10,
        });

        await response.ShouldHaveStatusAsync(HttpStatusCode.OK);
        var updated = await response.ReadAsAsync<ProductResponse>();
        updated.Sku.Should().Be("UPD-1");
        updated.Name.Should().Be("Nome novo");
        updated.CategoryName.Should().Be("Outra");
        updated.CurrentStock.Should().Be(7);
        updated.IsLowStock.Should().BeTrue();
    }

    [Fact]
    public async Task Filtros_ordenacao_e_paginacao_de_produtos()
    {
        var bebidas = await Admin.CreateCategoryAsync("Bebidas");
        var limpeza = await Admin.CreateCategoryAsync("Limpeza");
        var agua = await Admin.CreateProductAsync(bebidas.Id, "BEB-1", minimumStock: 5, name: "Água mineral");
        var suco = await Admin.CreateProductAsync(bebidas.Id, "BEB-2", minimumStock: 5, name: "Suco de uva");
        var sabao = await Admin.CreateProductAsync(limpeza.Id, "LIM-1", minimumStock: 1, name: "Sabão em pó");
        await Admin.EntryAsync(agua.Id, 20);
        await Admin.EntryAsync(suco.Id, 3);
        await Admin.EntryAsync(sabao.Id, 8);
        await Admin.PatchAsync($"/api/products/{sabao.Id}/deactivate", null);

        var search = await Viewer.GetAsAsync<PagedResult<ProductResponse>>("/api/products?search=SUCO");
        search.Items.Should().ContainSingle().Which.Id.Should().Be(suco.Id);

        var byCategory = await Viewer.GetAsAsync<PagedResult<ProductResponse>>($"/api/products?categoryId={bebidas.Id}");
        byCategory.TotalItems.Should().Be(2);

        var low = await Viewer.GetAsAsync<PagedResult<ProductResponse>>("/api/products?lowStock=true");
        low.Items.Select(p => p.Id).Should().Equal(suco.Id);

        var inactive = await Viewer.GetAsAsync<PagedResult<ProductResponse>>("/api/products?isActive=false");
        inactive.Items.Select(p => p.Id).Should().Equal(sabao.Id);

        var sorted = await Viewer.GetAsAsync<PagedResult<ProductResponse>>("/api/products?sort=-stock");
        sorted.Items.Select(p => p.CurrentStock).Should().Equal(20, 8, 3);

        var page2 = await Viewer.GetAsAsync<PagedResult<ProductResponse>>("/api/products?sort=sku&page=2&pageSize=2");
        page2.TotalItems.Should().Be(3);
        page2.TotalPages.Should().Be(2);
        page2.Items.Select(p => p.Sku).Should().Equal("LIM-1");
    }

    [Fact]
    public async Task Ordenacao_invalida_devolve_400()
    {
        var response = await Viewer.GetAsync("/api/products?sort=preco&pageSize=500");

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationError);
        problem.GetProperty("errors").TryGetProperty("sort", out _).Should().BeTrue();
        problem.GetProperty("errors").TryGetProperty("pageSize", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Produto_com_movimentacao_nao_e_excluido_e_sem_movimentacao_e()
    {
        var withHistory = await Admin.CreateStockedProductAsync(stock: 1);
        var category = await Admin.CreateCategoryAsync();
        var withoutHistory = await Admin.CreateProductAsync(category.Id, "DEL-1");

        var blocked = await Admin.DeleteAsync($"/api/products/{withHistory.Id}");
        await blocked.ShouldBeProblemAsync(HttpStatusCode.Conflict, ErrorCodes.Conflict);

        (await Admin.DeleteAsync($"/api/products/{withoutHistory.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Admin.GetAsync($"/api/products/{withoutHistory.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
