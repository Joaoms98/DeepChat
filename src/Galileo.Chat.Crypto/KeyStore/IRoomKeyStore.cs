namespace Galileo.Chat.Crypto.KeyStore;

/// <summary>
/// Persists per-room AES keys on the client. Implementations must keep keys at-rest
/// in a form that is non-trivially recoverable by another OS user / another machine.
///
/// Concrete impls:
///   - <see cref="InMemoryRoomKeyStore"/>: process-local, never persisted (default).
///   - DpapiRoomKeyStore (Windows-only, added in a later phase): wraps with DPAPI / CurrentUser.
/// </summary>
public interface IRoomKeyStore
{
    /// <summary>Stores or replaces the key for the given room.</summary>
    /// <remarks>The provided <paramref name="key"/> buffer is copied — caller may wipe it after.</remarks>
    void Save(Guid roomId, ReadOnlySpan<byte> key);

    /// <summary>Returns the key bytes for the room, or null if not stored.</summary>
    byte[]? TryGet(Guid roomId);

    /// <summary>Removes the key for the given room. No-op if absent.</summary>
    void Remove(Guid roomId);

    /// <summary>True if a key for the given room is stored.</summary>
    bool Contains(Guid roomId);

    /// <summary>Number of keys currently stored.</summary>
    int Count { get; }

    /// <summary>Clears every key in the store. Used on logout / panic.</summary>
    void Clear();
}
