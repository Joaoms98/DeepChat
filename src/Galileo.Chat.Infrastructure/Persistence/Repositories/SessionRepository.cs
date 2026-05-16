using Galileo.Chat.Domain.Abstractions;
using Galileo.Chat.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Galileo.Chat.Infrastructure.Persistence.Repositories;

internal sealed class SessionRepository : ISessionRepository
{
    private readonly ChatDbContext _db;

    public SessionRepository(ChatDbContext db) => _db = db;

    public Task<Session?> FindByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Sessions.FirstOrDefaultAsync(s => s.Id == id, ct);

    public Task<Session?> FindByJwtIdAsync(Guid jwtId, CancellationToken ct = default) =>
        _db.Sessions.FirstOrDefaultAsync(s => s.JwtId == jwtId, ct);

    public async Task<IReadOnlyList<Session>> ListActiveByUserAsync(Guid userId, DateTime utcNow, CancellationToken ct = default) =>
        await _db.Sessions
            .AsNoTracking()
            .Where(s => s.UserId == userId
                        && s.RevokedAt == null
                        && s.ExpiresAt > utcNow)
            .OrderByDescending(s => s.IssuedAt)
            .ToListAsync(ct);

    public async Task AddAsync(Session session, CancellationToken ct = default)
    {
        await _db.Sessions.AddAsync(session, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Session session, CancellationToken ct = default)
    {
        _db.Sessions.Update(session);
        await _db.SaveChangesAsync(ct);
    }

    public Task<int> PurgeExpiredAsync(DateTime utcNow, CancellationToken ct = default) =>
        _db.Sessions
            .Where(s => s.ExpiresAt < utcNow)
            .ExecuteDeleteAsync(ct);
}
