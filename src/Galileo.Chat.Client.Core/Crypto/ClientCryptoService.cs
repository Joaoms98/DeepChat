using System.Security.Cryptography;
using Galileo.Chat.Crypto.Aes;
using Galileo.Chat.Crypto.Exceptions;
using Galileo.Chat.Crypto.KeyStore;
using Galileo.Chat.Shared.Dto;

namespace Galileo.Chat.Client.Crypto;

/// <summary>
/// Bridges Crypto's AesGcmCipher and Shared's wire DTO. The room key is fetched
/// from the IRoomKeyStore at use-time and zeroed after each operation. The room
/// id goes in as Associated Data, so a ciphertext crafted for room A can never
/// be replayed into room B (the tag won't validate).
/// </summary>
public sealed class ClientCryptoService
{
    private readonly AesGcmCipher _cipher;
    private readonly IRoomKeyStore _keys;

    public ClientCryptoService(IRoomKeyStore keys, AesGcmCipher? cipher = null)
    {
        _keys = keys;
        _cipher = cipher ?? new AesGcmCipher();
    }

    /// <summary>
    /// Encrypts <paramref name="plaintext"/> with the room's key and returns a wire-ready DTO
    /// (without MessageId — that is server-assigned).
    /// </summary>
    public EncryptedMessageDto EncryptForRoom(
        Guid roomId,
        ReadOnlySpan<byte> plaintext,
        Guid senderId,
        string senderNickname,
        DateTime utcNow)
    {
        var key = _keys.TryGet(roomId)
            ?? throw new InvalidOperationException(
                $"Room {roomId} is locked: derive its key via RoomKeyManager.UnlockRoom first.");

        try
        {
            var aad = BuildAad(roomId);
            var envelope = _cipher.Encrypt(plaintext, key, aad);
            return new EncryptedMessageDto
            {
                RoomId = roomId,
                SenderId = senderId,
                SenderNickname = senderNickname,
                CreatedAt = utcNow,
                Iv = envelope.Iv,
                Ciphertext = envelope.Ciphertext,
                Tag = envelope.Tag
            };
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    /// <summary>
    /// Decrypts a wire DTO. Throws <see cref="DecryptionFailedException"/> if the room key
    /// was rotated, the ciphertext was tampered with, or the AAD doesn't match.
    /// </summary>
    public byte[] DecryptFromRoom(EncryptedMessageDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var key = _keys.TryGet(dto.RoomId)
            ?? throw new InvalidOperationException(
                $"Room {dto.RoomId} is locked: derive its key via RoomKeyManager.UnlockRoom first.");

        try
        {
            var envelope = new EncryptedEnvelope(dto.Iv, dto.Ciphertext, dto.Tag);
            var aad = BuildAad(dto.RoomId);
            return _cipher.Decrypt(envelope, key, aad);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    /// <summary>
    /// Associated Data binds the ciphertext to the room id, so the same key
    /// (if accidentally reused across rooms) still produces a tag that fails
    /// to verify when delivered to the wrong room.
    /// </summary>
    private static byte[] BuildAad(Guid roomId) => roomId.ToByteArray();
}
