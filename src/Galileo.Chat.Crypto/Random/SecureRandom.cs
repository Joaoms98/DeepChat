using System.Security.Cryptography;

namespace Galileo.Chat.Crypto.Random;

/// <summary>
/// Thin facade over <see cref="RandomNumberGenerator"/> to keep crypto callers free of
/// System.Security.Cryptography imports and to allow easy interception in tests.
/// </summary>
public static class SecureRandom
{
    public static byte[] GetBytes(int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        var buffer = new byte[length];
        RandomNumberGenerator.Fill(buffer);
        return buffer;
    }

    public static void Fill(Span<byte> destination)
    {
        if (destination.IsEmpty)
            throw new ArgumentException("Destination span must not be empty.", nameof(destination));
        RandomNumberGenerator.Fill(destination);
    }

    /// <summary>
    /// Convenience helper for generating a fresh AES-GCM nonce (12 bytes).
    /// </summary>
    public static byte[] NewIv() => GetBytes(12);

    /// <summary>
    /// Convenience helper for generating a fresh per-room salt (16 bytes).
    /// </summary>
    public static byte[] NewSalt() => GetBytes(16);
}
