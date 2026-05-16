using MessagePack;

namespace Galileo.Chat.Shared.Dto;

[MessagePackObject]
public sealed record RoomDto
{
    [Key(0)] public Guid Id { get; init; }
    [Key(1)] public string Name { get; init; } = string.Empty;
    /// <summary>Base64-encoded per-room salt for Argon2id key derivation. Public — not a secret on its own.</summary>
    [Key(2)] public string SaltBase64 { get; init; } = string.Empty;
}

[MessagePackObject]
public sealed record RegisterRequest
{
    [Key(0)] public string Username { get; init; } = string.Empty;
    [Key(1)] public string Nickname { get; init; } = string.Empty;
    [Key(2)] public string Password { get; init; } = string.Empty;
}

[MessagePackObject]
public sealed record CreateRoomRequest
{
    [Key(0)] public string Name { get; init; } = string.Empty;
}
