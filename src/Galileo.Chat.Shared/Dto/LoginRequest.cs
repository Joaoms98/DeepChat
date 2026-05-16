using MessagePack;

namespace Galileo.Chat.Shared.Dto;

[MessagePackObject]
public sealed record LoginRequest
{
    [Key(0)] public string Username { get; init; } = string.Empty;
    [Key(1)] public string Password { get; init; } = string.Empty;
}
