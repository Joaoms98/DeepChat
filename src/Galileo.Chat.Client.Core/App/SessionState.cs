namespace Galileo.Chat.Client.App;

/// <summary>Mutable runtime state for the connected client.</summary>
public sealed class SessionState
{
    public Guid UserId { get; set; }
    public string Nickname { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public DateTime TokenExpiresAt { get; set; }

    public Guid RoomId { get; set; }
    public string RoomName { get; set; } = string.Empty;
}
