using FluentValidation.TestHelper;
using StockCore.Application.Movements;
using StockCore.Application.Products;
using StockCore.Application.Reports;
using StockCore.Application.Suppliers;
using StockCore.Application.Users;
using StockCore.Domain.Enums;

namespace StockCore.UnitTests.Application;

public class ValidatorTests
{
    [Fact]
    public void Produto_valido_passa()
    {
        var request = new CreateProductRequest("BEB-001", "Água 500 ml", null, Guid.NewGuid(), null, 2.50m, 10);

        new CreateProductRequestValidator().TestValidate(request).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Produto_com_preco_negativo_sku_invalido_e_minimo_negativo_falha()
    {
        var request = new CreateProductRequest("sku com espaço", "A", null, Guid.Empty, null, -1m, -5);

        var result = new CreateProductRequestValidator().TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Sku);
        result.ShouldHaveValidationErrorFor(x => x.Name);
        result.ShouldHaveValidationErrorFor(x => x.CategoryId);
        result.ShouldHaveValidationErrorFor(x => x.UnitPrice);
        result.ShouldHaveValidationErrorFor(x => x.MinimumStock);
    }

    [Fact]
    public void Preco_com_tres_casas_decimais_falha()
    {
        var request = new CreateProductRequest("SKU-1", "Produto", null, Guid.NewGuid(), null, 1.999m, 0);

        new CreateProductRequestValidator().TestValidate(request).ShouldHaveValidationErrorFor(x => x.UnitPrice);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    [InlineData(1_000_001)]
    public void Quantidade_de_saida_fora_do_intervalo_falha(int quantity)
    {
        var request = new ExitRequest(Guid.NewGuid(), quantity, null);

        new ExitRequestValidator().TestValidate(request).ShouldHaveValidationErrorFor(x => x.Quantity);
    }

    [Fact]
    public void Entrada_com_custo_negativo_falha()
    {
        var request = new EntryRequest(Guid.NewGuid(), 5, -0.01m, null);

        new EntryRequestValidator().TestValidate(request).ShouldHaveValidationErrorFor(x => x.UnitCost);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ab")]
    public void Cancelamento_exige_motivo(string reason)
    {
        new CancelMovementRequestValidator()
            .TestValidate(new CancelMovementRequest(reason))
            .ShouldHaveValidationErrorFor(x => x.Reason);
    }

    [Theory]
    [InlineData("11.111.111/0001-11", true)]
    [InlineData("11111111000111", true)]
    [InlineData("123", false)]
    [InlineData(null, true)]
    public void Documento_do_fornecedor_segue_o_formato_de_cnpj(string? document, bool valid)
    {
        var result = new SupplierRequestValidator().TestValidate(new SupplierRequest("Fornecedor", document, null, null));

        if (valid)
        {
            result.ShouldNotHaveValidationErrorFor(x => x.Document);
        }
        else
        {
            result.ShouldHaveValidationErrorFor(x => x.Document);
        }
    }

    [Fact]
    public void Usuario_com_senha_curta_e_email_invalido_falha()
    {
        var result = new CreateUserRequestValidator().TestValidate(
            new CreateUserRequest("Ana", "nao-e-email", "123", UserRole.Viewer));

        result.ShouldHaveValidationErrorFor(x => x.Email);
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Consulta_de_produtos_valida_ordenacao_e_tamanho_da_pagina()
    {
        var result = new ProductQueryValidator().TestValidate(new ProductQuery { Sort = "preco", PageSize = 500 });

        result.ShouldHaveValidationErrorFor(x => x.Sort);
        result.ShouldHaveValidationErrorFor(x => x.PageSize);
    }

    [Fact]
    public void Periodo_com_data_final_antes_da_inicial_falha()
    {
        var query = new MovementQuery { From = DateTimeOffset.UtcNow, To = DateTimeOffset.UtcNow.AddDays(-1) };

        new MovementQueryValidator().TestValidate(query).ShouldHaveValidationErrorFor(x => x.To);
    }

    [Fact]
    public void Relatorio_com_periodo_maior_que_um_ano_falha()
    {
        var query = new MovementReportQuery { From = DateTimeOffset.UtcNow.AddDays(-400), To = DateTimeOffset.UtcNow };

        new MovementReportQueryValidator().TestValidate(query).ShouldHaveValidationErrorFor("to");
    }
}
