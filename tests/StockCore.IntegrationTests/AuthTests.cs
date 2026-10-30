using StockCore.Application.Auth;
using StockCore.Application.Users;
using StockCore.Domain.Enums;
using StockCore.Domain.Exceptions;

namespace StockCore.IntegrationTests;

public class AuthTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Login_valido_devolve_token_que_acessa_rotas_protegidas()
    {
        var user = await Factory.CreateUserAsync(UserRole.Operator, "maria@teste.com");

        var response = await Anonymous.PostJsonAsync(
            "/api/auth/login", new { email = "  MARIA@teste.com ", password = ApiFactory.DefaultPassword });

        await response.ShouldHaveStatusAsync(HttpStatusCode.OK);
        var login = await response.ReadAsAsync<LoginResponse>();
        login.TokenType.Should().Be("Bearer");
        login.AccessToken.Should().NotBeNullOrWhiteSpace();
        login.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
        login.User.Role.Should().Be(UserRole.Operator);

        using var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", login.AccessToken);
        var me = await client.GetAsAsync<UserResponse>("/api/auth/me");
        me.Id.Should().Be(user.Id);
        me.Email.Should().Be("maria@teste.com");
    }

    [Fact]
    public async Task Senha_errada_e_email_inexistente_devolvem_401_com_a_mesma_mensagem()
    {
        await Factory.CreateUserAsync(UserRole.Viewer, "joao@teste.com");

        var wrongPassword = await Anonymous.PostJsonAsync("/api/auth/login", new { email = "joao@teste.com", password = "errada123" });
        var unknownEmail = await Anonymous.PostJsonAsync("/api/auth/login", new { email = "ninguem@teste.com", password = "errada123" });

        var first = await wrongPassword.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, ErrorCodes.Unauthorized);
        var second = await unknownEmail.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, ErrorCodes.Unauthorized);
        first.GetProperty("detail").GetString().Should().Be(second.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Login_sem_campos_devolve_400_com_erros_por_campo()
    {
        var response = await Anonymous.PostJsonAsync("/api/auth/login", new { });

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationError);
        var errors = problem.GetProperty("errors");
        errors.TryGetProperty("email", out _).Should().BeTrue();
        errors.TryGetProperty("password", out _).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("token-invalido")]
    public async Task Rota_protegida_sem_token_valido_devolve_401(string? token)
    {
        using var client = Factory.CreateClient();
        if (token is not null)
        {
            client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        }

        var response = await client.GetAsync("/api/products");

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, ErrorCodes.Unauthorized);
    }

    [Fact]
    public async Task Viewer_le_mas_nao_escreve()
    {
        (await Viewer.GetAsync("/api/categories")).StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await Viewer.PostJsonAsync("/api/categories", new { name = "Bebidas" });

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden);
    }

    [Fact]
    public async Task Admin_cria_usuario_e_email_repetido_devolve_409()
    {
        var body = new { name = "Paula", email = "paula@teste.com", password = "senha-forte-1", role = "Operator" };

        var created = await Admin.PostJsonAsync("/api/users", body);
        await created.ShouldHaveStatusAsync(HttpStatusCode.Created);
        (await created.ReadAsAsync<UserResponse>()).Role.Should().Be(UserRole.Operator);

        var duplicated = await Admin.PostJsonAsync("/api/users", body with { email = "PAULA@teste.com" });
        await duplicated.ShouldBeProblemAsync(HttpStatusCode.Conflict, ErrorCodes.Conflict);

        var login = await Anonymous.PostJsonAsync("/api/auth/login", new { email = "paula@teste.com", password = "senha-forte-1" });
        await login.ShouldHaveStatusAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Operator_nao_gerencia_usuarios()
    {
        var response = await Operator.PostJsonAsync(
            "/api/users", new { name = "X", email = "x@teste.com", password = "12345678", role = "Admin" });

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden);
    }

    [Fact]
    public async Task Papel_inexistente_devolve_400()
    {
        var response = await Admin.PostJsonAsync(
            "/api/users", new { name = "Teste", email = "t@teste.com", password = "12345678", role = "Chefe" });

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationError);
    }
}
