using Galileo.Chat.Shared.Dto;

namespace Galileo.Chat.Shared.Hubs;

/// <summary>Methods clients invoke on ChatHub.</summary>
public interface IChatHubServer
{
    Task PostMessage(string roomId, EncryptedMessageDto envelope);

    /// <summary>
    /// DM to a single user. Envelope is routed only to that user — never to the room.
    /// MVP uses the sender's current room key; pair-wise ECDH is future work.
    /// </summary>
    Task PostPrivateMessage(string targetUserId, EncryptedMessageDto envelope);

    Task JoinRoom(string roomId);
    Task LeaveRoom(string roomId);
    Task<IReadOnlyList<UserPresenceDto>> ListOnline(string roomId);
}
