using System.Security.Cryptography;

namespace Galileo.Chat.Crypto.Random;

/// <summary>
/// 32-bit truncated SHA-256 of a symmetric key. Two clients compare fingerprints
/// to catch passphrase mismatches before sending any real ciphertext. Safe to
/// display: preimage-resistant and not narrow enough to brute-force in practice.
/// </summary>
public static class KeyFingerprint
{
    public static string Of(ReadOnlySpan<byte> key)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(key, hash);
        return $"{hash[0]:x2}{hash[1]:x2}-{hash[2]:x2}{hash[3]:x2}";
    }
}
