using Galileo.Chat.Crypto.KeyStore;
using Galileo.Chat.Crypto.Random;

namespace Galileo.Chat.Crypto.Tests.KeyStore;

public sealed class InMemoryRoomKeyStoreTests
{
    [Fact]
    public void Save_then_TryGet_returns_a_copy_of_the_stored_key()
    {
        using var store = new InMemoryRoomKeyStore();
        var room = Guid.NewGuid();
        var key = SecureRandom.GetBytes(32);

        store.Save(room, key);
        var retrieved = store.TryGet(room);

        retrieved.Should().NotBeNull();
        retrieved!.Should().Equal(key);
        retrieved.Should().NotBeSameAs(key);
    }

    [Fact]
    public void TryGet_returns_a_fresh_copy_each_call_so_callers_can_mutate_safely()
    {
        using var store = new InMemoryRoomKeyStore();
        var room = Guid.NewGuid();
        store.Save(room, SecureRandom.GetBytes(32));

        var first = store.TryGet(room)!;
        first[0] = 0xFF;

        var second = store.TryGet(room)!;
        second[0].Should().NotBe(0xFF);
    }

    [Fact]
    public void TryGet_returns_null_when_room_is_unknown()
    {
        using var store = new InMemoryRoomKeyStore();
        store.TryGet(Guid.NewGuid()).Should().BeNull();
    }

    [Fact]
    public void Save_overwrites_existing_key_for_same_room()
    {
        using var store = new InMemoryRoomKeyStore();
        var room = Guid.NewGuid();
        var k1 = SecureRandom.GetBytes(32);
        var k2 = SecureRandom.GetBytes(32);

        store.Save(room, k1);
        store.Save(room, k2);

        store.TryGet(room).Should().Equal(k2);
    }

    [Fact]
    public void Remove_deletes_the_stored_key()
    {
        using var store = new InMemoryRoomKeyStore();
        var room = Guid.NewGuid();
        store.Save(room, SecureRandom.GetBytes(32));

        store.Remove(room);

        store.TryGet(room).Should().BeNull();
        store.Contains(room).Should().BeFalse();
    }

    [Fact]
    public void Remove_is_silent_for_unknown_room()
    {
        using var store = new InMemoryRoomKeyStore();
        Action act = () => store.Remove(Guid.NewGuid());
        act.Should().NotThrow();
    }

    [Fact]
    public void Contains_reflects_current_state()
    {
        using var store = new InMemoryRoomKeyStore();
        var room = Guid.NewGuid();

        store.Contains(room).Should().BeFalse();
        store.Save(room, SecureRandom.GetBytes(32));
        store.Contains(room).Should().BeTrue();
        store.Remove(room);
        store.Contains(room).Should().BeFalse();
    }

    [Fact]
    public void Clear_drops_every_stored_key()
    {
        using var store = new InMemoryRoomKeyStore();
        var ids = new List<Guid>();
        for (var i = 0; i < 10; i++)
        {
            var id = Guid.NewGuid();
            ids.Add(id);
            store.Save(id, SecureRandom.GetBytes(32));
        }

        store.Clear();

        foreach (var id in ids)
        {
            store.Contains(id).Should().BeFalse();
            store.TryGet(id).Should().BeNull();
        }
    }

    [Fact]
    public void Operations_after_Dispose_throw_ObjectDisposedException()
    {
        var store = new InMemoryRoomKeyStore();
        store.Save(Guid.NewGuid(), SecureRandom.GetBytes(32));
        store.Dispose();

        Action save = () => store.Save(Guid.NewGuid(), SecureRandom.GetBytes(32));
        Action get = () => store.TryGet(Guid.NewGuid());
        Action remove = () => store.Remove(Guid.NewGuid());
        Action contains = () => store.Contains(Guid.NewGuid());
        Action clear = () => store.Clear();

        save.Should().Throw<ObjectDisposedException>();
        get.Should().Throw<ObjectDisposedException>();
        remove.Should().Throw<ObjectDisposedException>();
        contains.Should().Throw<ObjectDisposedException>();
        clear.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Dispose_is_idempotent()
    {
        var store = new InMemoryRoomKeyStore();
        store.Dispose();
        Action act = () => store.Dispose();
        act.Should().NotThrow();
    }
}
