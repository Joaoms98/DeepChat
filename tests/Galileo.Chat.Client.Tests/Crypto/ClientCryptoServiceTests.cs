using System.Text;
using Galileo.Chat.Client.Crypto;
using Galileo.Chat.Crypto.Aes;
using Galileo.Chat.Crypto.Exceptions;
using Galileo.Chat.Crypto.KeyStore;
using Galileo.Chat.Crypto.Random;
using Galileo.Chat.Shared.Dto;

namespace Galileo.Chat.Client.Tests.Crypto;

public sealed class ClientCryptoServiceTests
{
    private readonly InMemoryRoomKeyStore _keys = new();
    private readonly ClientCryptoService _sut;
    private static readonly DateTime Now = new(2026, 5, 15, 12, 0, 0, DateTimeKind.Utc);

    public ClientCryptoServiceTests() => _sut = new ClientCryptoService(_keys);

    private Guid SeedRoom()
    {
        var roomId = Guid.NewGuid();
        _keys.Save(roomId, SecureRandom.GetBytes(AesGcmCipher.KeySize));
        return roomId;
    }

    [Fact]
    public void Encrypt_then_Decrypt_recovers_plaintext()
    {
        var roomId = SeedRoom();
        var plaintext = Encoding.UTF8.GetBytes("olá privado");

        var dto = _sut.EncryptForRoom(roomId, plaintext, Guid.NewGuid(), "Alice", Now);
        var roundTripped = _sut.DecryptFromRoom(dto);

        roundTripped.Should().Equal(plaintext);
        dto.RoomId.Should().Be(roomId);
        dto.SenderNickname.Should().Be("Alice");
        dto.CreatedAt.Should().Be(Now);
        dto.Iv.Should().HaveCount(AesGcmCipher.IvSize);
        dto.Tag.Should().HaveCount(AesGcmCipher.TagSize);
    }

    [Fact]
    public void Encrypt_throws_for_locked_room()
    {
        var act = () => _sut.EncryptForRoom(Guid.NewGuid(),
            Encoding.UTF8.GetBytes("x"), Guid.NewGuid(), "Alice", Now);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Decrypt_throws_for_locked_room()
    {
        var dto = new EncryptedMessageDto
        {
            RoomId = Guid.NewGuid(),
            Iv = new byte[AesGcmCipher.IvSize],
            Ciphertext = new byte[] { 1 },
            Tag = new byte[AesGcmCipher.TagSize]
        };
        var act = () => _sut.DecryptFromRoom(dto);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Decrypt_with_wrong_room_id_AAD_fails_authentication()
    {
        // Encrypt for room A, then forge an envelope claiming room B.
        // Even if room B is unlocked with the SAME key bytes, AAD mismatch
        // makes the GCM tag fail to verify.
        var roomA = SeedRoom();
        var keyBytes = _keys.TryGet(roomA)!;
        var roomB = Guid.NewGuid();
        _keys.Save(roomB, keyBytes); // pretend B has the same key

        var dto = _sut.EncryptForRoom(roomA, Encoding.UTF8.GetBytes("secret"),
            Guid.NewGuid(), "Alice", Now);

        var forged = dto with { RoomId = roomB };
        var act = () => _sut.DecryptFromRoom(forged);
        act.Should().Throw<DecryptionFailedException>();
    }

    [Fact]
    public void Encrypted_DTOs_for_same_plaintext_have_distinct_IVs_and_ciphertexts()
    {
        var roomId = SeedRoom();
        var pt = Encoding.UTF8.GetBytes("repeat");

        var a = _sut.EncryptForRoom(roomId, pt, Guid.NewGuid(), "Alice", Now);
        var b = _sut.EncryptForRoom(roomId, pt, Guid.NewGuid(), "Alice", Now);

        a.Iv.Should().NotEqual(b.Iv);
        a.Ciphertext.Should().NotEqual(b.Ciphertext);
        a.Tag.Should().NotEqual(b.Tag);
    }
}
