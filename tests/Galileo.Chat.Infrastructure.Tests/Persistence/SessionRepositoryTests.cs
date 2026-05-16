using Galileo.Chat.Domain.Entities;
using Galileo.Chat.Infrastructure.Persistence.Repositories;
using Galileo.Chat.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace Galileo.Chat.Infrastructure.Tests.Persistence;

public sealed class SessionRepositoryTests
{
    private static Session NewSession(Guid userId, DateTime issuedAt, TimeSpan? lifetime = null) =>
        Session.Issue(userId, Guid.NewGuid(), issuedAt, lifetime ?? TimeSpan.FromHours(8), "192.168.1.42");

    [Fact]
    public async Task AddAsync_then_FindByJwtId_returns_session()
    {
        await using var db = await TestDb.CreateAsync();
        var users = new UserRepository(db.Context);
        var user = TestData.NewUser();
        await users.AddAsync(user);

        var sessions = new SessionRepository(db.Context);
        var session = NewSession(user.Id, TestData.FixedNow);
        await sessions.AddAsync(session);

        await using var fresh = db.NewContext();
        var found = await new SessionRepository(fresh).FindByJwtIdAsync(session.JwtId);

        found.Should().NotBeNull();
        found!.UserId.Should().Be(user.Id);
        found.RemoteIp.Should().Be("192.168.1.42");
        found.RevokedAt.Should().BeNull();
    }

    [Fact]
    public async Task ListActiveByUser_filters_revoked_and_expired_sessions()
    {
        await using var db = await TestDb.CreateAsync();
        var users = new UserRepository(db.Context);
        var user = TestData.NewUser();
        await users.AddAsync(user);

        var sessions = new SessionRepository(db.Context);

        var activeRecent = NewSession(user.Id, TestData.FixedNow);
        var activeOlder = NewSession(user.Id, TestData.FixedNow.AddMinutes(-30));
        var expired = NewSession(user.Id, TestData.FixedNow.AddHours(-9), TimeSpan.FromHours(8));
        var revoked = NewSession(user.Id, TestData.FixedNow);
        revoked.Revoke(TestData.FixedNow.AddMinutes(1));

        await sessions.AddAsync(activeRecent);
        await sessions.AddAsync(activeOlder);
        await sessions.AddAsync(expired);
        await sessions.AddAsync(revoked);

        var active = await sessions.ListActiveByUserAsync(user.Id, TestData.FixedNow.AddMinutes(2));

        active.Select(s => s.Id).Should().BeEquivalentTo(new[] { activeRecent.Id, activeOlder.Id });
        active[0].Id.Should().Be(activeRecent.Id); // ordered IssuedAt desc
    }

    [Fact]
    public async Task UpdateAsync_persists_revocation()
    {
        await using var db = await TestDb.CreateAsync();
        var users = new UserRepository(db.Context);
        var user = TestData.NewUser();
        await users.AddAsync(user);

        var sessions = new SessionRepository(db.Context);
        var session = NewSession(user.Id, TestData.FixedNow);
        await sessions.AddAsync(session);

        session.Revoke(TestData.FixedNow.AddMinutes(5));
        await sessions.UpdateAsync(session);

        await using var fresh = db.NewContext();
        var found = await new SessionRepository(fresh).FindByIdAsync(session.Id);
        found!.RevokedAt.Should().Be(TestData.FixedNow.AddMinutes(5));
    }

    [Fact]
    public async Task PurgeExpired_deletes_only_expired_rows()
    {
        await using var db = await TestDb.CreateAsync();
        var users = new UserRepository(db.Context);
        var user = TestData.NewUser();
        await users.AddAsync(user);

        var sessions = new SessionRepository(db.Context);
        await sessions.AddAsync(NewSession(user.Id, TestData.FixedNow.AddHours(-9), TimeSpan.FromHours(8)));   // expired
        await sessions.AddAsync(NewSession(user.Id, TestData.FixedNow.AddHours(-10), TimeSpan.FromHours(8)));  // expired
        await sessions.AddAsync(NewSession(user.Id, TestData.FixedNow));                                      // still active

        var purged = await sessions.PurgeExpiredAsync(TestData.FixedNow);

        purged.Should().Be(2);
        var remaining = await sessions.ListActiveByUserAsync(user.Id, TestData.FixedNow);
        remaining.Should().HaveCount(1);
    }

    [Fact]
    public async Task Adding_session_for_nonexistent_user_violates_FK_constraint()
    {
        // Proves the FK is actually present in the SQLite schema and enforced
        // (PRAGMA foreign_keys=ON via connection string).
        await using var db = await TestDb.CreateAsync();
        var sessions = new SessionRepository(db.Context);

        var orphan = NewSession(userId: Guid.NewGuid(), TestData.FixedNow);
        var act = async () => await sessions.AddAsync(orphan);

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Removing_user_via_EF_cascades_to_sessions()
    {
        // EF Core's own cascade (configured in SessionConfiguration with
        // OnDelete(Cascade)) is what runs in production code paths.
        await using var db = await TestDb.CreateAsync();
        var users = new UserRepository(db.Context);
        var user = TestData.NewUser();
        await users.AddAsync(user);

        var sessions = new SessionRepository(db.Context);
        await sessions.AddAsync(NewSession(user.Id, TestData.FixedNow));
        await sessions.AddAsync(NewSession(user.Id, TestData.FixedNow));

        db.Context.Users.Remove(user);
        await db.Context.SaveChangesAsync();

        await using var fresh = db.NewContext();
        var remaining = await new SessionRepository(fresh)
            .ListActiveByUserAsync(user.Id, TestData.FixedNow);
        remaining.Should().BeEmpty();
    }
}
