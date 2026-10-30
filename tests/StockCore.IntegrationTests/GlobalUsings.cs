global using System.Net;
global using System.Net.Http.Json;
global using System.Text.Json;
global using FluentAssertions;
global using StockCore.IntegrationTests.Infrastructure;
global using Xunit;

// Todos os testes compartilham o mesmo banco e o limpam antes de cada teste.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
