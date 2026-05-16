using System.Net.Http.Json;
using Galileo.Chat.Client.App;
using Galileo.Chat.Client.Configuration;
using Galileo.Chat.Client.UI;
using Galileo.Chat.Crypto.Kdf;
using Galileo.Chat.Crypto.KeyStore;
using Galileo.Chat.Crypto.Random;
using Galileo.Chat.Shared.Dto;
using Spectre.Console;

namespace Galileo.Chat.Client.Commands;

/// <summary>
/// /join &lt;room&gt; — switches rooms in-session. Derives the new key BEFORE
/// leaving the old one so a failed passphrase/hub call rolls back cleanly.
/// </summary>
public sealed class JoinCommand : ICommand
{
    public string Name => "join";
    public string Description => "Entra em outra sala: /join <nome>";

    private readonly HttpClient _http;
    private readonly ConnectionManager _conn;
    private readonly InMemoryRoomKeyStore _keyStore;
    private readonly SessionState _state;
    private readonly NicknameResolver _resolver;
    private readonly IAnsiConsole _console;
    private readonly MessageRenderer _renderer;

    public JoinCommand(
        HttpClient http,
        ConnectionManager conn,
        InMemoryRoomKeyStore keyStore,
        SessionState state,
        NicknameResolver resolver,
        IAnsiConsole console,
        MessageRenderer renderer)
    {
        _http = http;
        _conn = conn;
        _keyStore = keyStore;
        _state = state;
        _resolver = resolver;
        _console = console;
        _renderer = renderer;
    }

    public async Task ExecuteAsync(string arguments, CancellationToken ct)
    {
        var name = arguments?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            _renderer.Warning("Uso: /join <nome>");
            return;
        }

        if (string.Equals(name, _state.RoomName, StringComparison.OrdinalIgnoreCase))
        {
            _renderer.System($"Você já está em #{_state.RoomName}.");
            return;
        }

        // ----- Resolve or create the target room -----
        RoomDto? room;
        try
        {
            room = await GetOrCreateRoomAsync(name, ct);
        }
        catch (Exception ex)
        {
            _renderer.Error($"Não consegui acessar a sala: {ex.Message}");
            return;
        }
        if (room is null)
        {
            _renderer.Warning("Operação cancelada.");
            return;
        }

        // ----- Derive the new room key BEFORE leaving the old one -----
        // If anything below fails, we want to bail out without disrupting the
        // user's current session.
        var passphrase = PassphrasePrompt.Ask(_console, $"Passphrase de #{room.Name}");

        byte[] newKey;
        string fingerprint;
        try
        {
            var salt = Convert.FromBase64String(room.SaltBase64);
            newKey = _console.Status().Start($"Derivando chave de #{room.Name}…",
                _ => new Argon2KeyDerivation(Argon2Parameters.Interactive).DeriveKey(passphrase, salt));
            fingerprint = KeyFingerprint.Of(newKey);
        }
        catch (Exception ex)
        {
            _renderer.Error($"Falha na derivação da chave: {ex.Message}");
            return;
        }

        // ----- Switch SignalR group membership -----
        var oldRoomId = _state.RoomId;
        try
        {
            await _conn.LeaveRoomAsync(oldRoomId, ct);
            await _conn.JoinRoomAsync(room.Id, ct);
        }
        catch (Exception ex)
        {
            Array.Clear(newKey);
            _renderer.Error($"Falha ao trocar de sala no hub: {ex.Message}");
            // Best-effort rejoin to recover the previous state.
            try { await _conn.JoinRoomAsync(oldRoomId, ct); } catch { /* swallow */ }
            return;
        }

        // ----- Commit: state + key store + presence resolver -----
        _keyStore.Save(room.Id, newKey);
        Array.Clear(newKey);
        _keyStore.Remove(oldRoomId);

        _state.RoomId = room.Id;
        _state.RoomName = room.Name;

        // Refresh the resolver with whoever is in the new room.
        try
        {
            var users = await _conn.ListOnlineAsync(room.Id, ct);
            _resolver.Observe(users);
        }
        catch { /* presence list is best-effort */ }

        _renderer.Success($"Você está em #{room.Name}. Key fingerprint: {fingerprint}");
    }

    private async Task<RoomDto?> GetOrCreateRoomAsync(string name, CancellationToken ct)
    {
        var resp = await _http.GetAsync($"/api/rooms/{Uri.EscapeDataString(name)}", ct);
        if (resp.IsSuccessStatusCode)
            return await resp.Content.ReadFromJsonAsync<RoomDto>(cancellationToken: ct);

        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            if (!_console.Confirm($"Sala '{name}' não existe. Criar?", defaultValue: true))
                return null;

            var create = await _http.PostAsJsonAsync("/api/rooms",
                new CreateRoomRequest { Name = name }, ct);
            create.EnsureSuccessStatusCode();
            return await create.Content.ReadFromJsonAsync<RoomDto>(cancellationToken: ct);
        }

        resp.EnsureSuccessStatusCode();
        return null; // unreachable
    }
}
