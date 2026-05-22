using System.Security.Cryptography;
using Galileo.Chat.Crypto.Kdf;
using Galileo.Chat.Crypto.KeyStore;

namespace Galileo.Chat.Client.Crypto;

/// <summary>
/// Owns the lifecycle of room keys on the client. The passphrase is shared
/// offline among trusted members of the room and never leaves the client —
/// only the per-room salt comes from the server.
/// </summary>
public sealed class RoomKeyManager
{
    private readonly Argon2KeyDerivation _kdf;
    private readonly IRoomKeyStore _store;

    public RoomKeyManager(IRoomKeyStore store, Argon2KeyDerivation? kdf = null)
    {
        _store = store;
        _kdf = kdf ?? new Argon2KeyDerivation();
    }

    /// <summary>
    /// Derives the room key from passphrase+salt and stores it for subsequent
    /// encrypt/decrypt calls. The intermediate key bytes are zeroed after handoff.
    /// </summary>
    public void UnlockRoom(Guid roomId, string passphrase, ReadOnlySpan<byte> salt)
    {
        if (roomId == Guid.Empty)
            throw new ArgumentException("RoomId required.", nameof(roomId));

        var key = _kdf.DeriveKey(passphrase, salt);
        try
        {
            _store.Save(roomId, key);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    public void LockRoom(Guid roomId) => _store.Remove(roomId);

    public bool IsUnlocked(Guid roomId) => _store.Contains(roomId);
}
