using Galileo.Chat.Shared.Dto;

namespace Galileo.Chat.Server.Presence;

/// <summary>
/// Process-local tracker of who is connected and which rooms they're in.
/// Resets when the server restarts (presence is ephemeral by design — clients
/// reconnect and re-announce). Thread-safe.
/// </summary>
public interface IPresenceTracker
{
    void MarkOnline(Guid userId, string nickname, string connectionId, DateTime utcNow);

    /// <summary>Removes a single connection. Returns the rooms the user was in (so the hub can broadcast UserLeft).</summary>
    IReadOnlyList<string> MarkOffline(Guid userId, string connectionId);

    void AddRoom(Guid userId, string connectionId, string roomId);
    void RemoveRoom(Guid userId, string connectionId, string roomId);

    IReadOnlyList<UserPresenceDto> ListInRoom(string roomId);

    int OnlineCount { get; }
}
