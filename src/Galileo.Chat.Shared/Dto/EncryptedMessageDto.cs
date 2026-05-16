using MessagePack;

namespace Galileo.Chat.Shared.Dto;

/// <summary>
/// Wire-format envelope for an end-to-end encrypted message. The server NEVER
/// inspects Iv/Ciphertext/Tag — it only persists and re-broadcasts. The room
/// key (and therefore decryption capability) lives on the clients.
/// </summary>
[MessagePackObject]
public sealed record EncryptedMessageDto
{
    /// <summary>Server-assigned id; clients use it for dedup / replay protection.</summary>
    [Key(0)] public Guid MessageId { get; init; }

    /// <summary>Room the message belongs to. Empty for direct messages.</summary>
    [Key(1)] public Guid RoomId { get; init; }

    [Key(2)] public Guid SenderId { get; init; }

    /// <summary>Plain-text display name (NOT secret). Saves clients a presence lookup.</summary>
    [Key(3)] public string SenderNickname { get; init; } = string.Empty;

    [Key(4)] public DateTime CreatedAt { get; init; }

    [Key(5)] public byte[] Iv { get; init; } = Array.Empty<byte>();
    [Key(6)] public byte[] Ciphertext { get; init; } = Array.Empty<byte>();
    [Key(7)] public byte[] Tag { get; init; } = Array.Empty<byte>();
}
