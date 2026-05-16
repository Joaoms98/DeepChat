using Galileo.Chat.Domain.Common;
using Galileo.Chat.Domain.Exceptions;
using Galileo.Chat.Domain.ValueObjects;

namespace Galileo.Chat.Domain.Entities;

public sealed class Room
{
    /// <summary>Salt length used by Argon2id (room key derivation).</summary>
    public const int SaltLength = 16;

    public Guid Id { get; }
    public RoomName Name { get; }
    public byte[] Salt { get; private set; }
    public DateTime CreatedAt { get; }

    private Room(Guid id, RoomName name, byte[] salt, DateTime createdAt)
    {
        Id = id;
        Name = name;
        Salt = salt;
        CreatedAt = createdAt;
    }

    public static Room Create(RoomName name, ReadOnlySpan<byte> salt, DateTime utcNow)
    {
        Guard.ExactLength(salt, SaltLength);
        return new Room(Guid.NewGuid(), name, salt.ToArray(), utcNow);
    }

    /// <summary>
    /// Rotate the room salt. Existing ciphertexts cannot be decrypted with the new key —
    /// they will be purged within the retention window. Trigger only on explicit admin action.
    /// </summary>
    public void RotateSalt(ReadOnlySpan<byte> newSalt)
    {
        Guard.ExactLength(newSalt, SaltLength);
        if (newSalt.SequenceEqual(Salt))
            throw new DomainException("New salt must differ from the current salt.");
        Salt = newSalt.ToArray();
    }

    public static Room Rehydrate(Guid id, RoomName name, byte[] salt, DateTime createdAt) =>
        new(id, name, salt, createdAt);
}
