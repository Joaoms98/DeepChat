using Galileo.Chat.Domain.Abstractions;
using Galileo.Chat.Domain.Entities;
using Galileo.Chat.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Galileo.Chat.Infrastructure.Persistence.Repositories;

internal sealed class MessageRepository : IMessageRepository
{
    private readonly ChatDbContext _db;

    public MessageRepository(ChatDbContext db) => _db = db;

    public async Task AddAsync(Message message, CancellationToken ct = default)
    {
        await _db.Messages.AddAsync(message, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Message>> GetRecentByRoomAsync(Guid roomId, int take, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(take);

        // Take the newest N rows then reverse so the caller sees oldest-first
        // (chat panel renders top-down).
        var newest = await _db.Messages
            .AsNoTracking()
            .Where(m => m.RoomId == roomId && m.Kind == MessageKind.Broadcast)
            .OrderByDescending(m => m.CreatedAt)
            .Take(take)
            .ToListAsync(ct);

        newest.Reverse();
        return newest;
    }

    public async Task<IReadOnlyList<Message>> GetRecentDirectAsync(Guid userA, Guid userB, int take, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(take);

        var newest = await _db.Messages
            .AsNoTracking()
            .Where(m => m.Kind == MessageKind.Direct
                && ((m.SenderId == userA && m.RecipientId == userB)
                 || (m.SenderId == userB && m.RecipientId == userA)))
            .OrderByDescending(m => m.CreatedAt)
            .Take(take)
            .ToListAsync(ct);

        newest.Reverse();
        return newest;
    }

    public Task<int> PurgeOlderThanAsync(DateTime cutoff, CancellationToken ct = default) =>
        _db.Messages
            .Where(m => m.CreatedAt < cutoff)
            .ExecuteDeleteAsync(ct);
}
