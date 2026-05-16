using MessagePack;

namespace Galileo.Chat.Shared.Dto;

[MessagePackObject]
public sealed record UserPresenceDto
{
    [Key(0)] public Guid UserId { get; init; }
    [Key(1)] public string Nickname { get; init; } = string.Empty;
    [Key(2)] public DateTime ConnectedAt { get; init; }
}
