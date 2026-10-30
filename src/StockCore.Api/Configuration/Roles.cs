using StockCore.Domain.Enums;

namespace StockCore.Api.Configuration;

/// <summary>Combinações de papéis usadas em [Authorize(Roles = ...)].</summary>
public static class Roles
{
    public const string Admin = nameof(UserRole.Admin);
    public const string AdminOrOperator = nameof(UserRole.Admin) + "," + nameof(UserRole.Operator);
}
