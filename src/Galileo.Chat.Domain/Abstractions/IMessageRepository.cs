using Galileo.Chat.Domain.Entities;

namespace Galileo.Chat.Domain.Abstractions;

public interface IMessageRepository
{
    Task AddAsync(Message message, CancellationToken ct = default);

    /// <summary>
    /// Returns the N most recent broadcast messages for the room, oldest first
    /// (suitable for direct render into the chat panel).
    /// </summary>
    Task<IReadOnlyList<Message>> GetRecentByRoomAsync(Guid roomId, int take, CancellationToken ct = default);

    /// <summary>
    /// Returns the N most recent direct messages exchanged between the two users
    /// (in either direction), oldest first.
    /// </summary>
    Task<IReadOnlyList<Message>> GetRecentDirectAsync(Guid userA, Guid userB, int take, CancellationToken ct = default);

    /// <summary>
    /// Deletes every message older than <paramref name="cutoff"/>. Returns the count
    /// purged so the BackgroundService can log / emit metrics.
    /// </summary>
    Task<int> PurgeOlderThanAsync(DateTime cutoff, CancellationToken ct = default);
}
