namespace StockCore.Application.Categories;

public sealed record CategoryRequest(string Name, string? Description);

public sealed record CategoryResponse(Guid Id, string Name, string? Description, int ProductCount, DateTime CreatedAt);
