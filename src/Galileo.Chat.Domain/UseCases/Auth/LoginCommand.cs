namespace Galileo.Chat.Domain.UseCases.Auth;

public sealed record LoginCommand(string Username, string Password, string RemoteIp);
