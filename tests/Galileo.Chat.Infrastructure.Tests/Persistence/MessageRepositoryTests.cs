using Galileo.Chat.Domain.Entities;
using Galileo.Chat.Domain.Enums;
using Galileo.Chat.Infrastructure.Persistence.Repositories;
using Galileo.Chat.Infrastructure.Tests.TestSupport;

namespace Galileo.Chat.Infrastructure.Tests.Persistence;

public sealed class MessageRepositoryTests
{
    [Fact]
    public async Task AddAsync_round_trips_EncryptedPayload_byte_for_byte()
    {
        await using var db = await TestDb.CreateAsync();
        var repo = new MessageRepository(db.Context);

        var payload = TestData.NewPayload(cipherFill: 0xAB);
        var msg = Message.CreateBroadcast(Guid.NewGuid(), Guid.NewGuid(), payload, TestData.FixedNow);
        await repo.AddAsync(msg);

        await using var fresh = db.NewContext();
        var stored = await new MessageRepository(fresh)
            .GetRecentByRoomAsync(msg.RoomId!.Value, take: 10);

        stored.Should().HaveCount(1);
        var got = stored.Single();
        got.Payload.IvSpan.SequenceEqual(payload.IvSpan).Should().BeTrue();
        got.Payload.CiphertextSpan.SequenceEqual(payload.CiphertextSpan).Should().BeTrue();
        got.Payload.TagSpan.SequenceEqual(payload.TagSpan).Should().BeTrue();
        got.Kind.Should().Be(MessageKind.Broadcast);
        got.CreatedAt.Should().Be(TestData.FixedNow);
    }

    [Fact]
    public async Task GetRecentByRoom_returns_oldest_first_capped_at_take()
    {
        await using var db = await TestDb.CreateAsync();
        var repo = new MessageRepository(db.Context);
        var room = Guid.NewGuid();
        var sender = Guid.NewGuid();

        for (var i = 0; i < 10; i++)
        {
            var msg = Message.CreateBroadcast(sender, room, TestData.NewPayload((byte)i),
                TestData.FixedNow.AddMinutes(i));
            await repo.AddAsync(msg);
        }

        var recent = await repo.GetRecentByRoomAsync(room, take: 3);

        recent.Should().HaveCount(3);
        recent[0].CreatedAt.Should().Be(TestData.FixedNow.AddMinutes(7));
        recent[1].CreatedAt.Should().Be(TestData.FixedNow.AddMinutes(8));
        recent[2].CreatedAt.Should().Be(TestData.FixedNow.AddMinutes(9));
    }

    [Fact]
    public async Task GetRecentByRoom_excludes_messages_from_other_rooms()
    {
        await using var db = await TestDb.CreateAsync();
        var repo = new MessageRepository(db.Context);
        var sender = Guid.NewGuid();
        var roomA = Guid.NewGuid();
        var roomB = Guid.NewGuid();

        await repo.AddAsync(Message.CreateBroadcast(sender, roomA, TestData.NewPayload(), TestData.FixedNow));
        await repo.AddAsync(Message.CreateBroadcast(sender, roomB, TestData.NewPayload(), TestData.FixedNow));

        var inA = await repo.GetRecentByRoomAsync(roomA, take: 10);
        inA.Should().HaveCount(1);
        inA.Single().RoomId.Should().Be(roomA);
    }

    [Fact]
    public async Task GetRecentByRoom_excludes_direct_messages()
    {
        await using var db = await TestDb.CreateAsync();
        var repo = new MessageRepository(db.Context);
        var roomId = Guid.NewGuid();
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();

        await repo.AddAsync(Message.CreateBroadcast(alice, roomId, TestData.NewPayload(), TestData.FixedNow));
        await repo.AddAsync(Message.CreateDirect(alice, bob, TestData.NewPayload(), TestData.FixedNow));

        var inRoom = await repo.GetRecentByRoomAsync(roomId, take: 10);
        inRoom.Should().HaveCount(1);
        inRoom.Single().Kind.Should().Be(MessageKind.Broadcast);
    }

    [Fact]
    public async Task GetRecentDirect_returns_messages_in_either_direction()
    {
        await using var db = await TestDb.CreateAsync();
        var repo = new MessageRepository(db.Context);
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();
        var charlie = Guid.NewGuid();

        await repo.AddAsync(Message.CreateDirect(alice, bob, TestData.NewPayload(0x01), TestData.FixedNow));
        await repo.AddAsync(Message.CreateDirect(bob, alice, TestData.NewPayload(0x02), TestData.FixedNow.AddMinutes(1)));
        await repo.AddAsync(Message.CreateDirect(alice, charlie, TestData.NewPayload(0x03), TestData.FixedNow.AddMinutes(2))); // unrelated

        var thread = await repo.GetRecentDirectAsync(alice, bob, take: 10);

        thread.Should().HaveCount(2);
        thread[0].Payload.Ciphertext[0].Should().Be(0x01);
        thread[1].Payload.Ciphertext[0].Should().Be(0x02);
    }

    [Fact]
    public async Task PurgeOlderThan_deletes_only_expired_rows_and_returns_count()
    {
        await using var db = await TestDb.CreateAsync();
        var repo = new MessageRepository(db.Context);
        var room = Guid.NewGuid();
        var sender = Guid.NewGuid();
        var now = TestData.FixedNow;

        await repo.AddAsync(Message.CreateBroadcast(sender, room, TestData.NewPayload(), now.AddHours(-25)));
        await repo.AddAsync(Message.CreateBroadcast(sender, room, TestData.NewPayload(), now.AddHours(-30)));
        await repo.AddAsync(Message.CreateBroadcast(sender, room, TestData.NewPayload(), now.AddHours(-23)));
        await repo.AddAsync(Message.CreateBroadcast(sender, room, TestData.NewPayload(), now));

        var cutoff = now.AddHours(-24);
        var purged = await repo.PurgeOlderThanAsync(cutoff);

        purged.Should().Be(2);
        var remaining = await repo.GetRecentByRoomAsync(room, take: 10);
        remaining.Should().HaveCount(2);
        remaining.Should().AllSatisfy(m => m.CreatedAt.Should().BeAfter(cutoff));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task GetRecentByRoom_throws_for_non_positive_take(int take)
    {
        await using var db = await TestDb.CreateAsync();
        var repo = new MessageRepository(db.Context);

        var act = async () => await repo.GetRecentByRoomAsync(Guid.NewGuid(), take);
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }
}
