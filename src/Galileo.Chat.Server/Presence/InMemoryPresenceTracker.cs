using System.Collections.Concurrent;
using Galileo.Chat.Shared.Dto;

namespace Galileo.Chat.Server.Presence;

public sealed class InMemoryPresenceTracker : IPresenceTracker
{
    private readonly record struct ConnectionKey(Guid UserId, string ConnectionId);

    private sealed class Connection
    {
        public required string Nickname { get; init; }
        public required DateTime ConnectedAt { get; init; }
        public ConcurrentDictionary<string, byte> Rooms { get; } = new();
    }

    private readonly ConcurrentDictionary<ConnectionKey, Connection> _connections = new();

    public int OnlineCount => _connections.Select(kv => kv.Key.UserId).Distinct().Count();

    public void MarkOnline(Guid userId, string nickname, string connectionId, DateTime utcNow)
    {
        _connections[new ConnectionKey(userId, connectionId)] = new Connection
        {
            Nickname = nickname,
            ConnectedAt = utcNow
        };
    }

    public IReadOnlyList<string> MarkOffline(Guid userId, string connectionId)
    {
        if (!_connections.TryRemove(new ConnectionKey(userId, connectionId), out var conn))
            return Array.Empty<string>();
        return conn.Rooms.Keys.ToList();
    }

    public void AddRoom(Guid userId, string connectionId, string roomId)
    {
        if (_connections.TryGetValue(new ConnectionKey(userId, connectionId), out var conn))
            conn.Rooms[roomId] = 0;
    }

    public void RemoveRoom(Guid userId, string connectionId, string roomId)
    {
        if (_connections.TryGetValue(new ConnectionKey(userId, connectionId), out var conn))
            conn.Rooms.TryRemove(roomId, out _);
    }

    public IReadOnlyList<UserPresenceDto> ListInRoom(string roomId)
    {
        // Deduplicate per user — a single user with two devices counts once.
        var byUser = new Dictionary<Guid, UserPresenceDto>();
        foreach (var kv in _connections)
        {
            if (!kv.Value.Rooms.ContainsKey(roomId)) continue;
            if (byUser.ContainsKey(kv.Key.UserId)) continue;

            byUser[kv.Key.UserId] = new UserPresenceDto
            {
                UserId = kv.Key.UserId,
                Nickname = kv.Value.Nickname,
                ConnectedAt = kv.Value.ConnectedAt
            };
        }
        return byUser.Values.OrderBy(u => u.Nickname).ToList();
    }
}
