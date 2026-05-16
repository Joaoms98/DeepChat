using MessagePack;

namespace Galileo.Chat.Shared.Dto;

[MessagePackObject]
public sealed record LoginResponse
{
    [Key(0)] public string Token { get; init; } = string.Empty;
    [Key(1)] public DateTime ExpiresAt { get; init; }
    [Key(2)] public Guid UserId { get; init; }
    [Key(3)] public string Nickname { get; init; } = string.Empty;
}
