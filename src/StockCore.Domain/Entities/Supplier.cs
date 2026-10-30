namespace StockCore.Domain.Entities;

public sealed class Supplier
{
    private Supplier()
    {
    }

    public Supplier(string name, string? document, string? email, string? phone, DateTime createdAt)
    {
        Id = Guid.NewGuid();
        IsActive = true;
        CreatedAt = createdAt;
        Update(name, document, email, phone);
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    /// <summary>CNPJ (fictício nos dados de exemplo). Opcional, mas único quando informado.</summary>
    public string? Document { get; private set; }

    public string? Email { get; private set; }

    public string? Phone { get; private set; }

    public bool IsActive { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public void Update(string name, string? document, string? email, string? phone)
    {
        Name = name.Trim();
        Document = Clean(document);
        Email = Clean(email)?.ToLowerInvariant();
        Phone = Clean(phone);
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
