using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StockCore.Application.Common;
using StockCore.Domain.Entities;
using StockCore.Domain.Enums;

namespace StockCore.Infrastructure.Persistence;

/// <summary>
/// Dados fictícios para explorar a API em desenvolvimento. Só roda com Database:Seed=true
/// e não faz nada se já existir algum usuário.
/// </summary>
public sealed class DataSeeder(AppDbContext db, IPasswordHasher hasher, TimeProvider clock, ILogger<DataSeeder> logger)
{
    /// <summary>Senha de demonstração, só para uso local.</summary>
    public const string DemoPassword = "demo12345";

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (await db.Users.AnyAsync(cancellationToken))
        {
            logger.LogInformation("Seed ignorado: o banco já tem usuários.");
            return;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var passwordHash = hasher.Hash(DemoPassword);

        var admin = new User("Admin Exemplo", "admin@exemplo.com", passwordHash, UserRole.Admin, now);
        var operador = new User("Operador Exemplo", "operador@exemplo.com", passwordHash, UserRole.Operator, now);
        var leitor = new User("Leitor Exemplo", "leitor@exemplo.com", passwordHash, UserRole.Viewer, now);

        var bebidas = new Category("Bebidas", "Bebidas em geral", now);
        var mercearia = new Category("Mercearia", "Alimentos não perecíveis", now);
        var limpeza = new Category("Limpeza", "Produtos de limpeza", now);

        // CNPJs fictícios (dígitos repetidos não formam um CNPJ válido).
        var distribuidora = new Supplier(
            "Distribuidora Exemplo Ltda", "11.111.111/0001-11", "contato@distribuidora.exemplo", "(11) 0000-0001", now);
        var atacado = new Supplier(
            "Atacado Modelo SA", "22.222.222/0001-22", "vendas@atacado.exemplo", "(11) 0000-0002", now);

        var products = new[]
        {
            new Product("BEB-001", "Água mineral 500 ml", null, bebidas.Id, distribuidora.Id, 2.50m, 24, now),
            new Product("BEB-002", "Suco de uva 1 L", null, bebidas.Id, distribuidora.Id, 9.90m, 10, now),
            new Product("BEB-003", "Refrigerante lata 350 ml", null, bebidas.Id, distribuidora.Id, 4.75m, 30, now),
            new Product("MER-001", "Arroz tipo 1 5 kg", null, mercearia.Id, atacado.Id, 27.90m, 8, now),
            new Product("MER-002", "Feijão carioca 1 kg", null, mercearia.Id, atacado.Id, 8.49m, 12, now),
            new Product("MER-003", "Macarrão espaguete 500 g", null, mercearia.Id, atacado.Id, 4.99m, 15, now),
            new Product("LIM-001", "Detergente 500 ml", null, limpeza.Id, atacado.Id, 2.99m, 20, now),
            new Product("LIM-002", "Sabão em pó 1 kg", "Embalagem de papelão", limpeza.Id, null, 14.90m, 6, now),
        };

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        db.AddRange(admin, operador, leitor, bebidas, mercearia, limpeza, distribuidora, atacado);
        db.Products.AddRange(products);
        await db.SaveChangesAsync(cancellationToken);

        // Entradas iniciais e algumas saídas, usando as mesmas regras da API.
        int[] initialStock = [120, 18, 60, 25, 40, 50, 15, 10];
        var movements = new List<StockMovement>();
        for (var i = 0; i < products.Length; i++)
        {
            movements.Add(products[i].RegisterEntry(initialStock[i], products[i].UnitPrice * 0.7m, "Estoque inicial", admin.Id, now));
        }

        movements.Add(products[1].RegisterExit(10, "Venda balcão", operador.Id, now));
        movements.Add(products[3].RegisterExit(19, "Venda balcão", operador.Id, now));
        movements.Add(products[6].RegisterExit(5, "Uso interno", operador.Id, now));
        var wrongExit = products[0].RegisterExit(30, "Saída lançada por engano", operador.Id, now);
        movements.Add(wrongExit);
        movements.Add(products[0].CancelMovement(wrongExit, admin.Id, "Lançamento duplicado", now));

        db.StockMovements.AddRange(movements);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Seed aplicado: 3 usuários, 3 categorias, 2 fornecedores, {Products} produtos e {Movements} movimentações.",
            products.Length,
            movements.Count);
    }
}
