using StockCore.Domain.Enums;

namespace StockCore.Domain.Entities;

public sealed class User
{
    private User()
    {
    }

    public User(string name, string email, string passwordHash, UserRole role, DateTime createdAt)
    {
        Id = Guid.NewGuid();
        Name = name.Trim();
        Email = NormalizeEmail(email);
        PasswordHash = passwordHash;
        Role = role;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    public string PasswordHash { get; private set; } = string.Empty;

    public UserRole Role { get; private set; }

    public DateTime CreatedAt { get; private set; }

    /// <summary>E-mails são comparados sem espaços e em minúsculas.</summary>
    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
