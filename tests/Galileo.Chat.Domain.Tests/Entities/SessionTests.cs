using Galileo.Chat.Domain.Entities;
using Galileo.Chat.Domain.Exceptions;

namespace Galileo.Chat.Domain.Tests.Entities;

public class SessionTests
{
    private static Session Issue(TimeSpan? lifetime = null, DateTime? issuedAt = null) =>
        Session.Issue(
            userId: Guid.NewGuid(),
            jwtId: Guid.NewGuid(),
            issuedAt: issuedAt ?? DateTime.UtcNow,
            lifetime: lifetime ?? TimeSpan.FromHours(8),
            remoteIp: "192.168.1.42");

    [Fact]
    public void Issue_creates_active_session_with_expected_expiration()
    {
        var ts = DateTime.UtcNow;
        var s = Issue(TimeSpan.FromHours(2), ts);

        s.IsActive(ts).Should().BeTrue();
        s.ExpiresAt.Should().Be(ts.AddHours(2));
        s.RevokedAt.Should().BeNull();
    }

    [Fact]
    public void Issue_rejects_empty_user_id()
    {
        var act = () => Session.Issue(Guid.Empty, Guid.NewGuid(), DateTime.UtcNow,
            TimeSpan.FromHours(1), "127.0.0.1");
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Issue_rejects_empty_jwt_id()
    {
        var act = () => Session.Issue(Guid.NewGuid(), Guid.Empty, DateTime.UtcNow,
            TimeSpan.FromHours(1), "127.0.0.1");
        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Issue_rejects_non_positive_lifetime(int hours)
    {
        var act = () => Session.Issue(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow,
            TimeSpan.FromHours(hours), "127.0.0.1");
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Revoke_marks_inactive_and_is_idempotent()
    {
        var s = Issue();
        var ts = DateTime.UtcNow;
        s.Revoke(ts);
        s.RevokedAt.Should().Be(ts);
        s.IsActive(ts).Should().BeFalse();

        // second call must not overwrite the timestamp
        s.Revoke(ts.AddSeconds(10));
        s.RevokedAt.Should().Be(ts);
    }

    [Fact]
    public void IsActive_returns_false_after_expiration()
    {
        var ts = DateTime.UtcNow;
        var s = Issue(TimeSpan.FromMinutes(5), ts);
        s.IsActive(ts.AddMinutes(6)).Should().BeFalse();
    }
}
