using Galileo.Chat.Domain.Entities;

namespace Galileo.Chat.Domain.Abstractions;

public interface ISessionRepository
{
    Task<Session?> FindByIdAsync(Guid id, CancellationToken ct = default);
    Task<Session?> FindByJwtIdAsync(Guid jwtId, CancellationToken ct = default);
    Task<IReadOnlyList<Session>> ListActiveByUserAsync(Guid userId, DateTime utcNow, CancellationToken ct = default);
    Task AddAsync(Session session, CancellationToken ct = default);
    Task UpdateAsync(Session session, CancellationToken ct = default);
    Task<int> PurgeExpiredAsync(DateTime utcNow, CancellationToken ct = default);
}
