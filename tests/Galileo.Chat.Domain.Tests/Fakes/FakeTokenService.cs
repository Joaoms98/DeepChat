using Galileo.Chat.Domain.Abstractions;
using Galileo.Chat.Domain.Entities;

namespace Galileo.Chat.Domain.Tests.Fakes;

public sealed class FakeTokenService : ITokenService
{
    private readonly FakeClock _clock;
    public TimeSpan Lifetime { get; set; } = TimeSpan.FromHours(8);

    public List<TokenIssued> Issued { get; } = new();

    public FakeTokenService(FakeClock clock) => _clock = clock;

    public TokenIssued Issue(User user, Guid sessionId)
    {
        var token = new TokenIssued(
            Token: $"fake-jwt-for-{user.Id:N}-sid-{sessionId:N}",
            JwtId: Guid.NewGuid(),
            ExpiresAt: _clock.UtcNow + Lifetime);
        Issued.Add(token);
        return token;
    }
}
