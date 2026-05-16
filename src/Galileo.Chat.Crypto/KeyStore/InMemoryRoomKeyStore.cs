using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace Galileo.Chat.Crypto.KeyStore;

/// <summary>
/// Process-local key store. Keys live only in RAM and disappear on process exit.
/// Adequate for development, tests, and the "no persistence" client mode.
/// Production Windows clients should prefer DpapiRoomKeyStore (added in a later phase).
/// </summary>
public sealed class InMemoryRoomKeyStore : IRoomKeyStore, IDisposable
{
    private readonly ConcurrentDictionary<Guid, byte[]> _keys = new();
    private bool _disposed;

    public void Save(Guid roomId, ReadOnlySpan<byte> key)
    {
        ThrowIfDisposed();
        var copy = key.ToArray();
        _keys.AddOrUpdate(roomId, copy, (_, old) =>
        {
            CryptographicOperations.ZeroMemory(old);
            return copy;
        });
    }

    public byte[]? TryGet(Guid roomId)
    {
        ThrowIfDisposed();
        return _keys.TryGetValue(roomId, out var key)
            ? (byte[])key.Clone()  // hand out a copy — caller may wipe
            : null;
    }

    public void Remove(Guid roomId)
    {
        ThrowIfDisposed();
        if (_keys.TryRemove(roomId, out var key))
            CryptographicOperations.ZeroMemory(key);
    }

    public bool Contains(Guid roomId)
    {
        ThrowIfDisposed();
        return _keys.ContainsKey(roomId);
    }

    public int Count
    {
        get
        {
            ThrowIfDisposed();
            return _keys.Count;
        }
    }

    public void Clear()
    {
        ThrowIfDisposed();
        foreach (var kv in _keys)
            CryptographicOperations.ZeroMemory(kv.Value);
        _keys.Clear();
    }

    public void Dispose()
    {
        if (_disposed) return;
        Clear();
        _disposed = true;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
