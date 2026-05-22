using System.Text;
using Galileo.Chat.Client.App;
using Galileo.Chat.Client.Configuration;
using Galileo.Chat.Client.Crypto;
using Galileo.Chat.Client.Gui.Models;
using Galileo.Chat.Crypto.Exceptions;
using Galileo.Chat.Crypto.Kdf;
using Galileo.Chat.Crypto.KeyStore;
using Galileo.Chat.Crypto.Random;
using Galileo.Chat.Shared.Dto;
using Microsoft.AspNetCore.SignalR.Client;

namespace Galileo.Chat.Client.Gui.Services;

/// <summary>
/// Owns the live chat: room-key derivation, SignalR connection, encrypt/decrypt,
/// presence and DM routing. Mirrors the chat phase of the console client's
/// Program.cs, but surfaces everything through events so a view-model can render
/// it. Events fire on background threads — callers marshal to the UI thread.
/// </summary>
public sealed class ChatSession : IAsyncDisposable
{
    private readonly ClientOptions _options;
    private readonly SessionState _state;
    private readonly InMemoryRoomKeyStore _keyStore = new();
    private readonly ClientCryptoService _crypto;
    private readonly NicknameResolver _resolver = new();

    private ConnectionManager? _conn;

    public ChatSession(ClientOptions options, SessionState state)
    {
        _options = options;
        _state = state;
        _crypto = new ClientCryptoService(_keyStore);
    }

    /// <summary>A line ready to be appended to the transcript.</summary>
    public event Action<ChatMessageItem>? LineAdded;

    /// <summary>Raised after presence changes; carries the current online roster.</summary>
    public event Action<IReadOnlyList<UserPresenceDto>>? PresenceChanged;

    public string RoomName => _state.RoomName;
    public string Nickname => _state.Nickname;

    /// <summary>Derives the room key, opens the connection and joins the room.</summary>
    public async Task ConnectAsync(RoomDto room, string passphrase, CancellationToken ct = default)
    {
        DeriveAndStoreKey(room, passphrase);
        _state.RoomId = room.Id;
        _state.RoomName = room.Name;

        var conn = ConnectionManager.Build(
            _options.ServerUrl,
            () => Task.FromResult<string?>(_state.Token),
            _options.AllowInsecureTls);
        _conn = conn;

        RegisterHandlers(conn);

        await conn.StartAsync(ct);
        await conn.JoinRoomAsync(room.Id, ct);

        await RefreshPresenceAsync(room.Id, ct);
    }

    /// <summary>Switches to another room in-session, deriving the new key first.</summary>
    public async Task SwitchRoomAsync(RoomDto room, string passphrase, CancellationToken ct = default)
    {
        if (_conn is null) throw new InvalidOperationException("Not connected.");

        if (string.Equals(room.Name, _state.RoomName, StringComparison.OrdinalIgnoreCase))
        {
            Emit(ChatMessageItem.Status(ChatMessageKind.System, $"Você já está em #{room.Name}."));
            return;
        }

        DeriveAndStoreKey(room, passphrase, out var fingerprint);

        var oldRoomId = _state.RoomId;
        try
        {
            await _conn.LeaveRoomAsync(oldRoomId, ct);
            await _conn.JoinRoomAsync(room.Id, ct);
        }
        catch
        {
            _keyStore.Remove(room.Id);
            try { await _conn.JoinRoomAsync(oldRoomId, ct); } catch { /* best-effort recovery */ }
            throw;
        }

        _keyStore.Remove(oldRoomId);
        _state.RoomId = room.Id;
        _state.RoomName = room.Name;

        await RefreshPresenceAsync(room.Id, ct);
        Emit(ChatMessageItem.Status(ChatMessageKind.Success,
            $"Você está em #{room.Name}. Fingerprint da chave: {fingerprint}"));
    }

    public async Task SendMessageAsync(string text, CancellationToken ct = default)
    {
        if (_conn is null || string.IsNullOrWhiteSpace(text)) return;

        var envelope = _crypto.EncryptForRoom(
            _state.RoomId, Encoding.UTF8.GetBytes(text), _state.UserId, _state.Nickname, DateTime.UtcNow);
        await _conn.PostMessageAsync(_state.RoomId, envelope, ct);

        Emit(new ChatMessageItem
        {
            Kind = ChatMessageKind.Self,
            Sender = _state.Nickname,
            Text = text
        });
    }

    /// <summary>Sends a DM. Resolves a nickname or GUID; reports problems as warning lines.</summary>
    public async Task SendPrivateMessageAsync(string targetToken, string text, CancellationToken ct = default)
    {
        if (_conn is null || string.IsNullOrWhiteSpace(text)) return;

        var targetId = _resolver.Resolve(targetToken, out var ambiguous);
        if (targetId is null)
        {
            var reason = ambiguous
                ? $"Apelido '{targetToken}' é ambíguo — use o GUID."
                : $"Destinatário '{targetToken}' desconhecido. Atualize a lista de online.";
            Emit(ChatMessageItem.Status(ChatMessageKind.Warning, reason));
            return;
        }
        if (targetId == _state.UserId)
        {
            Emit(ChatMessageItem.Status(ChatMessageKind.Warning, "Você não pode mandar DM pra você mesmo."));
            return;
        }

        var envelope = _crypto.EncryptForRoom(
            _state.RoomId, Encoding.UTF8.GetBytes(text), _state.UserId, _state.Nickname, DateTime.UtcNow);
        await _conn.PostPrivateMessageAsync(targetId.Value, envelope, ct);

        Emit(new ChatMessageItem
        {
            Kind = ChatMessageKind.DirectMessage,
            Sender = $"{_state.Nickname} → {targetToken}",
            Text = text
        });
    }

    public async Task<IReadOnlyList<UserPresenceDto>> ListOnlineAsync(CancellationToken ct = default)
    {
        if (_conn is null) return Array.Empty<UserPresenceDto>();
        var users = await _conn.ListOnlineAsync(_state.RoomId, ct);
        _resolver.Observe(users);
        return users;
    }

    // ----- internals -----

    private void DeriveAndStoreKey(RoomDto room, string passphrase) =>
        DeriveAndStoreKey(room, passphrase, out _);

    private void DeriveAndStoreKey(RoomDto room, string passphrase, out string fingerprint)
    {
        var salt = Convert.FromBase64String(room.SaltBase64);
        var key = new Argon2KeyDerivation(Argon2Parameters.Interactive).DeriveKey(passphrase, salt);
        try
        {
            fingerprint = KeyFingerprint.Of(key);
            _keyStore.Save(room.Id, key);
        }
        finally
        {
            Array.Clear(key);
        }
    }

    private void RegisterHandlers(ConnectionManager conn)
    {
        conn.Connection.On<EncryptedMessageDto>("ReceiveMessage", dto => HandleIncoming(dto, isDm: false));
        conn.Connection.On<EncryptedMessageDto>("ReceivePrivateMessage", dto => HandleIncoming(dto, isDm: true));

        conn.Connection.On<string, UserPresenceDto>("UserJoined", (roomId, user) =>
        {
            _resolver.Observe(user.UserId, user.Nickname);
            Emit(ChatMessageItem.Status(ChatMessageKind.System, $"{user.Nickname} entrou na sala."));
            _ = RefreshPresenceAsync(_state.RoomId);
        });

        conn.Connection.On<string, Guid>("UserLeft", (roomId, userId) =>
        {
            Emit(ChatMessageItem.Status(ChatMessageKind.System, $"Usuário {userId:N}… saiu da sala."));
            _ = RefreshPresenceAsync(_state.RoomId);
        });

        conn.Connection.Reconnecting += _ =>
        {
            Emit(ChatMessageItem.Status(ChatMessageKind.Warning, "Reconectando…"));
            return Task.CompletedTask;
        };
        conn.Connection.Reconnected += async _ =>
        {
            Emit(ChatMessageItem.Status(ChatMessageKind.Success, "Reconectado."));
            await conn.JoinRoomAsync(_state.RoomId);
        };
    }

    private void HandleIncoming(EncryptedMessageDto dto, bool isDm)
    {
        try
        {
            var text = Encoding.UTF8.GetString(_crypto.DecryptFromRoom(dto));
            Emit(new ChatMessageItem
            {
                Kind = isDm ? ChatMessageKind.DirectMessage : ChatMessageKind.Other,
                Sender = isDm ? $"{dto.SenderNickname} (DM)" : dto.SenderNickname,
                Text = text,
                Timestamp = dto.CreatedAt
            });
        }
        catch (DecryptionFailedException)
        {
            Emit(ChatMessageItem.Status(ChatMessageKind.Error,
                $"Mensagem de {dto.SenderNickname} não pôde ser lida — passphrase difere da de quem enviou."));
        }
        catch (Exception ex)
        {
            Emit(ChatMessageItem.Status(ChatMessageKind.Error, $"Falha ao processar mensagem: {ex.Message}"));
        }
    }

    private async Task RefreshPresenceAsync(Guid roomId, CancellationToken ct = default)
    {
        if (_conn is null) return;
        try
        {
            var users = await _conn.ListOnlineAsync(roomId, ct);
            _resolver.Observe(users);
            PresenceChanged?.Invoke(users);
        }
        catch { /* presence is best-effort */ }
    }

    private void Emit(ChatMessageItem item) => LineAdded?.Invoke(item);

    public async ValueTask DisposeAsync()
    {
        if (_conn is not null) await _conn.DisposeAsync();
        _keyStore.Dispose();
    }
}
