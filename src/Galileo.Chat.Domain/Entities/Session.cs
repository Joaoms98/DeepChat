using Galileo.Chat.Domain.Common;
using Galileo.Chat.Domain.Exceptions;

namespace Galileo.Chat.Domain.Entities;

public sealed class Session
{
    public Guid Id { get; }
    public Guid UserId { get; }
    public Guid JwtId { get; }
    public DateTime IssuedAt { get; }
    public DateTime ExpiresAt { get; }
    public DateTime? RevokedAt { get; private set; }
    public string RemoteIp { get; }

    private Session(Guid id, Guid userId, Guid jwtId, DateTime issuedAt, DateTime expiresAt, string remoteIp)
    {
        Id = id;
        UserId = userId;
        JwtId = jwtId;
        IssuedAt = issuedAt;
        ExpiresAt = expiresAt;
        RemoteIp = remoteIp;
    }

    public static Session Issue(Guid userId, Guid jwtId, DateTime issuedAt, TimeSpan lifetime, string remoteIp)
    {
        if (userId == Guid.Empty)
            throw new DomainException("UserId is required.");
        if (jwtId == Guid.Empty)
            throw new DomainException("JwtId is required.");
        if (lifetime <= TimeSpan.Zero)
            throw new DomainException("Session lifetime must be positive.");
        Guard.NotNullOrWhiteSpace(remoteIp);

        return new Session(Guid.NewGuid(), userId, jwtId, issuedAt, issuedAt + lifetime, remoteIp);
    }

    public void Revoke(DateTime utcNow)
    {
        if (RevokedAt is not null)
            return;
        RevokedAt = utcNow;
    }

    public bool IsActive(DateTime utcNow) =>
        RevokedAt is null && utcNow < ExpiresAt;

    public static Session Rehydrate(Guid id, Guid userId, Guid jwtId, DateTime issuedAt,
        DateTime expiresAt, DateTime? revokedAt, string remoteIp) =>
        new(id, userId, jwtId, issuedAt, expiresAt, remoteIp) { RevokedAt = revokedAt };
}
