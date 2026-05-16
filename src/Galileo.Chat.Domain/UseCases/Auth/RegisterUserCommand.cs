namespace Galileo.Chat.Domain.UseCases.Auth;

public sealed record RegisterUserCommand(string Username, string Nickname, string Password);

public sealed record RegisterUserResult(Guid UserId, string Username);
