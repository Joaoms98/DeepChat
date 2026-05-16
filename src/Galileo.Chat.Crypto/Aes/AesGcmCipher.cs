using System.Security.Cryptography;
using Galileo.Chat.Crypto.Exceptions;
using Galileo.Chat.Crypto.Random;

namespace Galileo.Chat.Crypto.Aes;

/// <summary>
/// AES-256-GCM cipher used for end-to-end encrypted message payloads.
///
/// Contract:
///  - Key MUST be exactly <see cref="KeySize"/> bytes (256 bits).
///  - Each call generates a fresh 96-bit IV (NEVER reuse an IV with the same key).
///  - Tag is the full 128-bit GCM authentication tag.
///  - Optional Associated Data is authenticated but not encrypted (e.g. roomId, senderId).
///
/// Decrypt throws <see cref="DecryptionFailedException"/> for any tag mismatch
/// (tampered ciphertext, wrong key, wrong AAD, truncated tag).
/// </summary>
public sealed class AesGcmCipher
{
    public const int KeySize = 32;  // 256 bits
    public const int IvSize = 12;   // 96 bits — recommended GCM nonce size
    public const int TagSize = 16;  // 128 bits — full authentication tag

    public EncryptedEnvelope Encrypt(
        ReadOnlySpan<byte> plaintext,
        ReadOnlySpan<byte> key,
        ReadOnlySpan<byte> associatedData = default)
    {
        EnsureKeySize(key);

        var iv = SecureRandom.GetBytes(IvSize);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(iv, plaintext, ciphertext, tag, associatedData);

        return new EncryptedEnvelope(iv, ciphertext, tag);
    }

    public byte[] Decrypt(
        EncryptedEnvelope envelope,
        ReadOnlySpan<byte> key,
        ReadOnlySpan<byte> associatedData = default)
    {
        EnsureKeySize(key);
        EnsureEnvelopeShape(envelope);

        var plaintext = new byte[envelope.Ciphertext.Length];
        try
        {
            using var aes = new AesGcm(key, TagSize);
            aes.Decrypt(envelope.Iv, envelope.Ciphertext, envelope.Tag, plaintext, associatedData);
        }
        catch (CryptographicException ex)
        {
            // Wipe partial plaintext to avoid leaking partially-decrypted bytes if the caller
            // somehow inspects the array after the exception bubbles.
            CryptographicOperations.ZeroMemory(plaintext);
            throw new DecryptionFailedException(
                "Authentication tag mismatch: ciphertext was tampered with, key is wrong, or AAD differs.",
                ex);
        }
        return plaintext;
    }

    private static void EnsureKeySize(ReadOnlySpan<byte> key)
    {
        if (key.Length != KeySize)
            throw new InvalidKeyLengthException(key.Length, KeySize);
    }

    private static void EnsureEnvelopeShape(in EncryptedEnvelope envelope)
    {
        if (envelope.Iv is null || envelope.Iv.Length != IvSize)
            throw new DecryptionFailedException($"IV must be {IvSize} bytes.");
        if (envelope.Tag is null || envelope.Tag.Length != TagSize)
            throw new DecryptionFailedException($"Tag must be {TagSize} bytes.");
        if (envelope.Ciphertext is null)
            throw new DecryptionFailedException("Ciphertext must not be null.");
    }
}
