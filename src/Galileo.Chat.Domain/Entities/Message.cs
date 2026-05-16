using Galileo.Chat.Domain.Enums;
using Galileo.Chat.Domain.Exceptions;
using Galileo.Chat.Domain.ValueObjects;

namespace Galileo.Chat.Domain.Entities;

public sealed class Message
{
    public Guid Id { get; }
    public Guid SenderId { get; }
    public Guid? RoomId { get; }
    public Guid? RecipientId { get; }
    public MessageKind Kind { get; }
    public EncryptedPayload Payload { get; private set; }
    public DateTime CreatedAt { get; }

    private Message(Guid id, Guid senderId, Guid? roomId, Guid? recipientId,
        MessageKind kind, EncryptedPayload payload, DateTime createdAt)
    {
        Id = id;
        SenderId = senderId;
        RoomId = roomId;
        RecipientId = recipientId;
        Kind = kind;
        Payload = payload;
        CreatedAt = createdAt;
    }

    /// <summary>
    /// EF Core constructor binding cannot bind owned navigations (Payload) as ctor
    /// parameters. EF picks this overload, then materializes the owned EncryptedPayload
    /// and assigns it via the private setter. Never call from domain code — use the
    /// factory methods or <see cref="Rehydrate"/>.
    /// </summary>
    private Message(Guid id, Guid senderId, Guid? roomId, Guid? recipientId,
        MessageKind kind, DateTime createdAt)
    {
        Id = id;
        SenderId = senderId;
        RoomId = roomId;
        RecipientId = recipientId;
        Kind = kind;
        CreatedAt = createdAt;
        Payload = null!; // EF will assign before the entity is observable.
    }

    public static Message CreateBroadcast(Guid senderId, Guid roomId, EncryptedPayload payload, DateTime utcNow)
    {
        if (senderId == Guid.Empty)
            throw new DomainException("SenderId is required.");
        if (roomId == Guid.Empty)
            throw new DomainException("RoomId is required for broadcast.");

        return new Message(Guid.NewGuid(), senderId, roomId, recipientId: null,
            MessageKind.Broadcast, payload, utcNow);
    }

    public static Message CreateDirect(Guid senderId, Guid recipientId, EncryptedPayload payload, DateTime utcNow)
    {
        if (senderId == Guid.Empty)
            throw new DomainException("SenderId is required.");
        if (recipientId == Guid.Empty)
            throw new DomainException("RecipientId is required for direct message.");
        if (senderId == recipientId)
            throw new DomainException("SenderId and RecipientId must differ.");

        return new Message(Guid.NewGuid(), senderId, roomId: null, recipientId,
            MessageKind.Direct, payload, utcNow);
    }

    public static Message CreateSystem(Guid roomId, EncryptedPayload payload, DateTime utcNow)
    {
        if (roomId == Guid.Empty)
            throw new DomainException("RoomId is required for system message.");

        return new Message(Guid.NewGuid(), senderId: Guid.Empty, roomId,
            recipientId: null, MessageKind.System, payload, utcNow);
    }

    public bool IsExpired(DateTime utcNow, TimeSpan ttl) => utcNow - CreatedAt > ttl;

    public static Message Rehydrate(Guid id, Guid senderId, Guid? roomId, Guid? recipientId,
        MessageKind kind, EncryptedPayload payload, DateTime createdAt) =>
        new(id, senderId, roomId, recipientId, kind, payload, createdAt);
}
