using StockCore.Domain.Entities;
using StockCore.Domain.Enums;

namespace StockCore.Application.Users;

public sealed record CreateUserRequest(string Name, string Email, string Password, UserRole Role);

public sealed record UserResponse(Guid Id, string Name, string Email, UserRole Role, DateTime CreatedAt)
{
    public static UserResponse From(User user) => new(user.Id, user.Name, user.Email, user.Role, user.CreatedAt);
}
