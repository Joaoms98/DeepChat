namespace Galileo.Chat.Domain.UseCases.Auth;

public sealed record LoginResult(
    string Token,
    DateTime ExpiresAt,
    Guid UserId,
    string Nickname);
