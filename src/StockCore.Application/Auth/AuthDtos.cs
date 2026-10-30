using StockCore.Application.Users;

namespace StockCore.Application.Auth;

public sealed record LoginRequest(string Email, string Password);

public sealed record LoginResponse(string AccessToken, string TokenType, DateTime ExpiresAt, UserResponse User);

public sealed record AccessToken(string Token, DateTime ExpiresAt);
