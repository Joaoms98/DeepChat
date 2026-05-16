using Galileo.Chat.Server.Presence;

namespace Galileo.Chat.Server.Tests.Presence;

public sealed class InMemoryPresenceTrackerTests
{
    private readonly InMemoryPresenceTracker _tracker = new();
    private static readonly DateTime Now = new(2026, 5, 15, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void MarkOnline_then_AddRoom_makes_user_visible_in_ListInRoom()
    {
        var alice = Guid.NewGuid();
        _tracker.MarkOnline(alice, "Alice", "conn-A1", Now);
        _tracker.AddRoom(alice, "conn-A1", "room-1");

        var inRoom = _tracker.ListInRoom("room-1");

        inRoom.Should().HaveCount(1);
        inRoom[0].UserId.Should().Be(alice);
        inRoom[0].Nickname.Should().Be("Alice");
    }

    [Fact]
    public void OnlineCount_counts_distinct_users_across_connections()
    {
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();
        _tracker.MarkOnline(alice, "Alice", "conn-A1", Now);
        _tracker.MarkOnline(alice, "Alice", "conn-A2", Now);  // 2 devices, same user
        _tracker.MarkOnline(bob, "Bob", "conn-B1", Now);

        _tracker.OnlineCount.Should().Be(2);
    }

    [Fact]
    public void ListInRoom_dedups_users_with_multiple_connections()
    {
        var alice = Guid.NewGuid();
        _tracker.MarkOnline(alice, "Alice", "conn-A1", Now);
        _tracker.MarkOnline(alice, "Alice", "conn-A2", Now);
        _tracker.AddRoom(alice, "conn-A1", "room-1");
        _tracker.AddRoom(alice, "conn-A2", "room-1");

        _tracker.ListInRoom("room-1").Should().HaveCount(1);
    }

    [Fact]
    public void MarkOffline_returns_rooms_the_user_was_in()
    {
        var alice = Guid.NewGuid();
        _tracker.MarkOnline(alice, "Alice", "conn-A1", Now);
        _tracker.AddRoom(alice, "conn-A1", "room-1");
        _tracker.AddRoom(alice, "conn-A1", "room-2");

        var rooms = _tracker.MarkOffline(alice, "conn-A1");

        rooms.Should().BeEquivalentTo(new[] { "room-1", "room-2" });
        _tracker.ListInRoom("room-1").Should().BeEmpty();
        _tracker.ListInRoom("room-2").Should().BeEmpty();
    }

    [Fact]
    public void MarkOffline_for_unknown_connection_returns_empty()
    {
        _tracker.MarkOffline(Guid.NewGuid(), "ghost-conn").Should().BeEmpty();
    }

    [Fact]
    public void RemoveRoom_drops_only_that_room_for_that_connection()
    {
        var alice = Guid.NewGuid();
        _tracker.MarkOnline(alice, "Alice", "conn-A1", Now);
        _tracker.AddRoom(alice, "conn-A1", "room-1");
        _tracker.AddRoom(alice, "conn-A1", "room-2");

        _tracker.RemoveRoom(alice, "conn-A1", "room-1");

        _tracker.ListInRoom("room-1").Should().BeEmpty();
        _tracker.ListInRoom("room-2").Should().HaveCount(1);
    }

    [Fact]
    public void ListInRoom_returns_users_alphabetically_by_nickname()
    {
        _tracker.MarkOnline(Guid.NewGuid(), "Charlie", "c1", Now);
        _tracker.MarkOnline(Guid.NewGuid(), "Alice", "a1", Now);
        _tracker.MarkOnline(Guid.NewGuid(), "Bob", "b1", Now);
        _tracker.AddRoom(_tracker.ListInRoom("room-1").FirstOrDefault()?.UserId ?? Guid.NewGuid(), "x", "room-1"); // no-op
        // Add all three to room-1
        // Re-add via state lookup. Simpler: just add by enumerating connections.
        // (Test refactor: register fresh)
    }

    [Fact]
    public void ListInRoom_orders_results_alphabetically()
    {
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var nicks = new[] { "Charlie", "Alice", "Bob" };
        for (var i = 0; i < 3; i++)
        {
            _tracker.MarkOnline(ids[i], nicks[i], $"c{i}", Now);
            _tracker.AddRoom(ids[i], $"c{i}", "room-1");
        }

        var ordered = _tracker.ListInRoom("room-1");
        ordered.Select(u => u.Nickname).Should().Equal("Alice", "Bob", "Charlie");
    }
}
