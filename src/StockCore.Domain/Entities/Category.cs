namespace StockCore.Domain.Entities;

public sealed class Category
{
    private Category()
    {
    }

    public Category(string name, string? description, DateTime createdAt)
    {
        Id = Guid.NewGuid();
        CreatedAt = createdAt;
        Update(name, description);
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public void Update(string name, string? description)
    {
        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }
}
