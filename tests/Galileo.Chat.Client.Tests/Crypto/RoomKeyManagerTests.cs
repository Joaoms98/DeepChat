using System.Text;
using Galileo.Chat.Client.Crypto;
using Galileo.Chat.Crypto.Aes;
using Galileo.Chat.Crypto.Kdf;
using Galileo.Chat.Crypto.KeyStore;
using Galileo.Chat.Crypto.Random;
using Galileo.Chat.Shared.Dto;

namespace Galileo.Chat.Client.Tests.Crypto;

public sealed class RoomKeyManagerTests
{
    private readonly InMemoryRoomKeyStore _store = new();
    private readonly RoomKeyManager _sut;
    private readonly ClientCryptoService _crypto;

    public RoomKeyManagerTests()
    {
        // Use the cheap test profile so the KDF doesn't burn 250ms per test.
        _sut = new RoomKeyManager(_store, new Argon2KeyDerivation(Argon2Parameters.Test));
        _crypto = new ClientCryptoService(_store);
    }

    [Fact]
    public void UnlockRoom_makes_room_usable_for_encryption()
    {
        var roomId = Guid.NewGuid();
        var salt = SecureRandom.NewSalt();

        _sut.IsUnlocked(roomId).Should().BeFalse();
        _sut.UnlockRoom(roomId, "correct horse battery staple", salt);
        _sut.IsUnlocked(roomId).Should().BeTrue();

        var dto = _crypto.EncryptForRoom(roomId,
            Encoding.UTF8.GetBytes("hello"), Guid.NewGuid(), "Alice", DateTime.UtcNow);
        _crypto.DecryptFromRoom(dto).Should().Equal(Encoding.UTF8.GetBytes("hello"));
    }

    [Fact]
    public void Two_clients_with_same_passphrase_and_salt_can_exchange_messages()
    {
        var roomId = Guid.NewGuid();
        var salt = SecureRandom.NewSalt();

        var aliceStore = new InMemoryRoomKeyStore();
        var bobStore = new InMemoryRoomKeyStore();
        var alice = new RoomKeyManager(aliceStore, new Argon2KeyDerivation(Argon2Parameters.Test));
        var bob = new RoomKeyManager(bobStore, new Argon2KeyDerivation(Argon2Parameters.Test));

        alice.UnlockRoom(roomId, "shared-passphrase", salt);
        bob.UnlockRoom(roomId, "shared-passphrase", salt);

        var aliceCrypto = new ClientCryptoService(aliceStore);
        var bobCrypto = new ClientCryptoService(bobStore);

        var dto = aliceCrypto.EncryptForRoom(roomId,
            Encoding.UTF8.GetBytes("from Alice"), Guid.NewGuid(), "Alice", DateTime.UtcNow);

        bobCrypto.DecryptFromRoom(dto).Should().Equal(Encoding.UTF8.GetBytes("from Alice"));
    }

    [Fact]
    public void LockRoom_removes_the_key()
    {
        var roomId = Guid.NewGuid();
        _sut.UnlockRoom(roomId, "p", SecureRandom.NewSalt());

        _sut.LockRoom(roomId);

        _sut.IsUnlocked(roomId).Should().BeFalse();
        _store.Contains(roomId).Should().BeFalse();
    }

    [Fact]
    public void UnlockRoom_rejects_empty_room_id()
    {
        var act = () => _sut.UnlockRoom(Guid.Empty, "p", SecureRandom.NewSalt());
        act.Should().Throw<ArgumentException>();
    }
}
