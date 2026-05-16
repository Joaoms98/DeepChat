using Galileo.Chat.Domain.Entities;
using Galileo.Chat.Infrastructure.Persistence.Repositories;
using Galileo.Chat.Infrastructure.Tests.TestSupport;

namespace Galileo.Chat.Infrastructure.Tests.Persistence;

/// <summary>
/// End-to-end behavioral coverage of the purge contract that the Retention
/// BackgroundService relies on. The service itself is just a timer + this call;
/// the truth lives in the repositories.
/// </summary>
public sealed class PurgeBehaviorTests
{
    private static readonly DateTime Now = TestData.FixedNow;

    [Fact]
    public async Task Purge_at_24h_TTL_removes_messages_older_than_24h()
    {
        await using var db = await TestDb.CreateAsync();
        var repo = new MessageRepository(db.Context);
        var room = Guid.NewGuid();
        var sender = Guid.NewGuid();

        await repo.AddAsync(Message.CreateBroadcast(sender, room, TestData.NewPayload(), Now.AddHours(-25)));
        await repo.AddAsync(Message.CreateBroadcast(sender, room, TestData.NewPayload(), Now.AddHours(-23)));
        await repo.AddAsync(Message.CreateBroadcast(sender, room, TestData.NewPayload(), Now));

        var cutoff = Now - TimeSpan.FromHours(24);
        var purged = await repo.PurgeOlderThanAsync(cutoff);

        purged.Should().Be(1);
        var remaining = await repo.GetRecentByRoomAsync(room, take: 10);
        remaining.Should().HaveCount(2);
    }

    [Fact]
    public async Task Purge_with_no_expired_messages_returns_zero_and_keeps_data()
    {
        await using var db = await TestDb.CreateAsync();
        var repo = new MessageRepository(db.Context);
        var room = Guid.NewGuid();
        var sender = Guid.NewGuid();

        await repo.AddAsync(Message.CreateBroadcast(sender, room, TestData.NewPayload(), Now.AddHours(-1)));

        var purged = await repo.PurgeOlderThanAsync(Now.AddHours(-24));
        purged.Should().Be(0);

        (await repo.GetRecentByRoomAsync(room, take: 10)).Should().HaveCount(1);
    }

    [Fact]
    public async Task Session_purge_removes_only_expired_sessions()
    {
        await using var db = await TestDb.CreateAsync();
        var users = new UserRepository(db.Context);
        var user = TestData.NewUser();
        await users.AddAsync(user);

        var sessions = new SessionRepository(db.Context);
        await sessions.AddAsync(Session.Issue(user.Id, Guid.NewGuid(), Now.AddHours(-9), TimeSpan.FromHours(8), "1.1.1.1"));  // expired
        await sessions.AddAsync(Session.Issue(user.Id, Guid.NewGuid(), Now, TimeSpan.FromHours(8), "1.1.1.1"));               // active

        var purged = await sessions.PurgeExpiredAsync(Now);
        purged.Should().Be(1);

        (await sessions.ListActiveByUserAsync(user.Id, Now)).Should().HaveCount(1);
    }
}
