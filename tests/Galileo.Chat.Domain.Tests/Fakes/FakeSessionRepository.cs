using Galileo.Chat.Domain.Abstractions;
using Galileo.Chat.Domain.Entities;

namespace Galileo.Chat.Domain.Tests.Fakes;

public sealed class FakeSessionRepository : ISessionRepository
{
    private readonly Dictionary<Guid, Session> _byId = new();

    public IReadOnlyDictionary<Guid, Session> All => _byId;

    public Task<Session?> FindByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_byId.GetValueOrDefault(id));

    public Task<Session?> FindByJwtIdAsync(Guid jwtId, CancellationToken ct = default) =>
        Task.FromResult<Session?>(_byId.Values.FirstOrDefault(s => s.JwtId == jwtId));

    public Task<IReadOnlyList<Session>> ListActiveByUserAsync(Guid userId, DateTime utcNow, CancellationToken ct = default)
    {
        IReadOnlyList<Session> list = _byId.Values
            .Where(s => s.UserId == userId && s.IsActive(utcNow))
            .OrderByDescending(s => s.IssuedAt)
            .ToList();
        return Task.FromResult(list);
    }

    public Task AddAsync(Session session, CancellationToken ct = default)
    {
        _byId[session.Id] = session;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Session session, CancellationToken ct = default)
    {
        _byId[session.Id] = session;
        return Task.CompletedTask;
    }

    public Task<int> PurgeExpiredAsync(DateTime utcNow, CancellationToken ct = default)
    {
        var expired = _byId.Values.Where(s => s.ExpiresAt < utcNow).ToList();
        foreach (var s in expired) _byId.Remove(s.Id);
        return Task.FromResult(expired.Count);
    }
}
