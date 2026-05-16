using System.Text;
using Galileo.Chat.Crypto.Exceptions;
using Konscious.Security.Cryptography;

namespace Galileo.Chat.Crypto.Kdf;

/// <summary>
/// Derives an AES-256 room key from a shared passphrase + per-room salt using Argon2id.
///
/// The salt is public (stored on the server) but the passphrase is shared offline among
/// trusted room members and never traverses the wire. Without the passphrase, the salt
/// alone is useless.
/// </summary>
public sealed class Argon2KeyDerivation
{
    public const int DefaultKeyLength = 32; // AES-256

    public Argon2Parameters Parameters { get; }

    public Argon2KeyDerivation(Argon2Parameters? parameters = null)
    {
        Parameters = parameters ?? Argon2Parameters.Interactive;
        Parameters.Validate();
    }

    public byte[] DeriveKey(string passphrase, ReadOnlySpan<byte> salt, int keyLengthBytes = DefaultKeyLength)
    {
        if (string.IsNullOrEmpty(passphrase))
            throw new CryptoException("Passphrase must not be empty.");
        if (salt.Length < 8)
            throw new CryptoException("Salt must be at least 8 bytes (RFC 9106 §3.1).");
        if (keyLengthBytes is < 16 or > 64)
            throw new CryptoException("Key length must be between 16 and 64 bytes.");

        var passphraseBytes = Encoding.UTF8.GetBytes(passphrase);
        try
        {
            using var argon2 = new Argon2id(passphraseBytes)
            {
                Salt = salt.ToArray(),
                DegreeOfParallelism = Parameters.Parallelism,
                MemorySize = Parameters.MemoryKb,
                Iterations = Parameters.Iterations
            };
            return argon2.GetBytes(keyLengthBytes);
        }
        finally
        {
            // Best-effort wipe of the UTF-8 password copy. We can't wipe the original .NET string
            // (interned, GC-managed) — that's a known limitation; the canonical mitigation is to
            // pass passphrases via SecureString/console input that we keep ephemeral.
            Array.Clear(passphraseBytes, 0, passphraseBytes.Length);
        }
    }
}
