using Galileo.Chat.Shared.Dto;

namespace Galileo.Chat.Shared.Hubs;

/// <summary>Server-to-client callbacks invoked by ChatHub on connected clients.</summary>
public interface IChatHubClient
{
    Task ReceiveMessage(EncryptedMessageDto message);
    Task ReceivePrivateMessage(EncryptedMessageDto message);
    Task UserJoined(string roomId, UserPresenceDto user);
    Task UserLeft(string roomId, Guid userId);
    Task SystemNotice(string code, string message);
    Task ForceDisconnect(string reason);
}
