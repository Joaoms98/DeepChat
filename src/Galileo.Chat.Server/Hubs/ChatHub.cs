using System.Globalization;
using System.Security.Claims;
using Galileo.Chat.Domain.Abstractions;
using Galileo.Chat.Domain.Entities;
using Galileo.Chat.Domain.ValueObjects;
using Galileo.Chat.Infrastructure.Auth;
using Galileo.Chat.Server.Presence;
using Galileo.Chat.Shared.Constants;
using Galileo.Chat.Shared.Dto;
using Galileo.Chat.Shared.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Galileo.Chat.Server.Hubs;

[Authorize]
public sealed class ChatHub : Hub<IChatHubClient>, IChatHubServer
{
    private readonly IMessageRepository _messages;
    private readonly IRoomRepository _rooms;
    private readonly IPresenceTracker _presence;
    private readonly IClock _clock;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(
        IMessageRepository messages,
        IRoomRepository rooms,
        IPresenceTracker presence,
        IClock clock,
        ILogger<ChatHub> logger)
    {
        _messages = messages;
        _rooms = rooms;
        _presence = presence;
        _clock = clock;
        _logger = logger;
    }

    public override Task OnConnectedAsync()
    {
        var userId = GetUserId();
        var nick = GetNickname();
        _presence.MarkOnline(userId, nick, Context.ConnectionId, _clock.UtcNow);
        _logger.LogInformation("User {UserId} ({Nick}) connected as {ConnectionId}",
            userId, nick, Context.ConnectionId);
        return base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        var rooms = _presence.MarkOffline(userId, Context.ConnectionId);
        foreach (var roomId in rooms)
            await Clients.OthersInGroup(roomId).UserLeft(roomId, userId);

        _logger.LogInformation("User {UserId} disconnected ({ConnectionId}); reason: {Exception}",
            userId, Context.ConnectionId, exception?.GetType().Name ?? "clean");
        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinRoom(string roomId)
    {
        var roomGuid = ValidateRoomId(roomId);
        var room = await _rooms.FindByIdAsync(roomGuid)
                   ?? throw new HubException("Unknown room.");

        await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
        _presence.AddRoom(GetUserId(), Context.ConnectionId, roomId);

        var presence = new UserPresenceDto
        {
            UserId = GetUserId(),
            Nickname = GetNickname(),
            ConnectedAt = _clock.UtcNow
        };
        await Clients.OthersInGroup(roomId).UserJoined(roomId, presence);

        _ = room; // reserved: room metadata could go on a SystemNotice in a later phase.
    }

    public async Task LeaveRoom(string roomId)
    {
        ValidateRoomId(roomId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomId);
        _presence.RemoveRoom(GetUserId(), Context.ConnectionId, roomId);
        await Clients.OthersInGroup(roomId).UserLeft(roomId, GetUserId());
    }

    public Task<IReadOnlyList<UserPresenceDto>> ListOnline(string roomId)
    {
        ValidateRoomId(roomId);
        return Task.FromResult(_presence.ListInRoom(roomId));
    }

    public async Task PostPrivateMessage(string targetUserId, EncryptedMessageDto envelope)
    {
        ValidateEnvelope(envelope);
        if (!Guid.TryParse(targetUserId, CultureInfo.InvariantCulture, out var targetGuid) || targetGuid == Guid.Empty)
            throw new HubException("Invalid targetUserId.");

        var senderId = GetUserId();
        if (targetGuid == senderId)
            throw new HubException("Cannot send a private message to yourself.");

        var payload = EncryptedPayload.Create(envelope.Iv, envelope.Ciphertext, envelope.Tag);
        var message = Message.CreateDirect(senderId, targetGuid, payload, _clock.UtcNow);
        await _messages.AddAsync(message);

        _logger.LogInformation("DM {MsgId} from={Sender} to={Target} bytes={Len}",
            message.Id, senderId, targetGuid, envelope.Ciphertext.Length);

        var outgoing = envelope with
        {
            MessageId = message.Id,
            SenderId = senderId,
            SenderNickname = GetNickname(),
            CreatedAt = message.CreatedAt
        };

        // SignalR's User() group routes to all connections of that user (multi-device).
        await Clients.User(targetUserId).ReceivePrivateMessage(outgoing);
    }

    public async Task PostMessage(string roomId, EncryptedMessageDto envelope)
    {
        var roomGuid = ValidateRoomId(roomId);
        ValidateEnvelope(envelope);

        var senderId = GetUserId();
        var payload = EncryptedPayload.Create(envelope.Iv, envelope.Ciphertext, envelope.Tag);
        var message = Message.CreateBroadcast(senderId, roomGuid, payload, _clock.UtcNow);
        await _messages.AddAsync(message);

        // We do NOT log payload bytes — only metadata. See [[feedback-security-priority]].
        _logger.LogInformation("Msg {MsgId} room={RoomId} sender={UserId} bytes={Len}",
            message.Id, roomGuid, senderId, envelope.Ciphertext.Length);

        var outgoing = envelope with
        {
            MessageId = message.Id,
            RoomId = roomGuid,
            SenderId = senderId,
            SenderNickname = GetNickname(),
            CreatedAt = message.CreatedAt
        };

        await Clients.OthersInGroup(roomId).ReceiveMessage(outgoing);
    }

    private Guid GetUserId()
    {
        var sub = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? Context.User?.FindFirstValue("sub")
                  ?? throw new HubException("Missing 'sub' claim.");
        return Guid.Parse(sub, CultureInfo.InvariantCulture);
    }

    private string GetNickname() =>
        Context.User?.FindFirstValue(JwtTokenService.ClaimNick) ?? "unknown";

    private static Guid ValidateRoomId(string roomId)
    {
        if (!Guid.TryParse(roomId, CultureInfo.InvariantCulture, out var g) || g == Guid.Empty)
            throw new HubException("Invalid roomId.");
        return g;
    }

    private static void ValidateEnvelope(EncryptedMessageDto e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (e.Iv is null || e.Iv.Length != ProtocolConstants.IvLength)
            throw new HubException($"Iv must be {ProtocolConstants.IvLength} bytes.");
        if (e.Tag is null || e.Tag.Length != ProtocolConstants.TagLength)
            throw new HubException($"Tag must be {ProtocolConstants.TagLength} bytes.");
        if (e.Ciphertext is null || e.Ciphertext.Length is 0
            || e.Ciphertext.Length > ProtocolConstants.MaxCiphertextBytes)
            throw new HubException($"Ciphertext must be 1..{ProtocolConstants.MaxCiphertextBytes} bytes.");
    }
}
