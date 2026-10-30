using Microsoft.EntityFrameworkCore;
using StockCore.Application.Common;
using StockCore.Application.Users;
using StockCore.Domain.Entities;
using StockCore.Domain.Exceptions;
using StockCore.Infrastructure.Persistence;

namespace StockCore.Infrastructure.Services;

public sealed class UserService(AppDbContext db, IPasswordHasher hasher, TimeProvider clock) : IUserService
{
    public async Task<IReadOnlyList<UserResponse>> ListAsync(CancellationToken cancellationToken) =>
        await db.Users.AsNoTracking()
            .OrderBy(u => u.Name)
            .Select(u => new UserResponse(u.Id, u.Name, u.Email, u.Role, u.CreatedAt))
            .ToListAsync(cancellationToken);

    public async Task<UserResponse> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var email = User.NormalizeEmail(request.Email);
        if (await db.Users.AnyAsync(u => u.Email == email, cancellationToken))
        {
            throw new ConflictException("Já existe um usuário com este e-mail.");
        }

        var user = new User(request.Name, email, hasher.Hash(request.Password), request.Role, clock.GetUtcNow().UtcDateTime);
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        return UserResponse.From(user);
    }
}
