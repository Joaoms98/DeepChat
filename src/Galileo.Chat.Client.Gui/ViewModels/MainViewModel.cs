using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using Galileo.Chat.Client.App;
using Galileo.Chat.Client.Auth;
using Galileo.Chat.Client.Configuration;
using Galileo.Chat.Client.Gui.Infrastructure;
using Galileo.Chat.Shared.Dto;

namespace Galileo.Chat.Client.Gui.ViewModels;

/// <summary>
/// Root view-model: owns the shared HttpClient + auth, and swaps the active
/// screen (login → rooms → chat). Child view-models call back here to navigate.
/// </summary>
public sealed class MainViewModel : ViewModelBase, IDisposable
{
    private readonly HttpClient _http;
    private object? _current;

    public MainViewModel()
    {
        Options = LoadOptions();

        var handler = new HttpClientHandler();
        if (Options.AllowInsecureTls)
            handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;

        _http = new HttpClient(handler) { BaseAddress = new Uri(Options.ServerUrl) };
        Auth = new HttpAuthClient(_http);

        ShowLogin();
    }

    public ClientOptions Options { get; }
    public HttpAuthClient Auth { get; }
    public HttpClient Http => _http;

    /// <summary>Set once the user authenticates; cleared on logout.</summary>
    public LoginResponse? Login { get; private set; }

    public object? Current
    {
        get => _current;
        private set
        {
            var previous = _current;
            if (SetField(ref _current, value))
                DisposePrevious(previous);
        }
    }

    private static void DisposePrevious(object? vm)
    {
        switch (vm)
        {
            case IAsyncDisposable async:
                _ = async.DisposeAsync().AsTask();
                break;
            case IDisposable sync:
                sync.Dispose();
                break;
        }
    }

    public void ShowLogin()
    {
        Login = null;
        _http.DefaultRequestHeaders.Authorization = null;
        Current = new LoginViewModel(this);
    }

    public void OnAuthenticated(LoginResponse login)
    {
        Login = login;
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);
        ShowRooms();
    }

    public void ShowRooms() => Current = new RoomsViewModel(this);

    public void ShowChat(RoomDto room, string passphrase)
    {
        var state = new SessionState
        {
            UserId = Login!.UserId,
            Nickname = Login.Nickname,
            Token = Login.Token,
            TokenExpiresAt = Login.ExpiresAt,
            RoomId = room.Id,
            RoomName = room.Name
        };
        Current = new ChatViewModel(this, state, room, passphrase);
    }

    /// <summary>
    /// Reads ServerUrl / AllowInsecureTls from appsettings.json next to the exe.
    /// Plain System.Text.Json keeps the GUI free of the Configuration packages —
    /// the file is the same shape the console client and host scripts already use.
    /// A DEEPCHAT_ServerUrl env var, if set, wins (handy for testing).
    /// </summary>
    private static ClientOptions LoadOptions()
    {
        var options = new ClientOptions();
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        try
        {
            if (File.Exists(path))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                if (doc.RootElement.TryGetProperty("Client", out var client))
                {
                    if (client.TryGetProperty(nameof(ClientOptions.ServerUrl), out var url) &&
                        url.GetString() is { Length: > 0 } s)
                        options.ServerUrl = s;
                    if (client.TryGetProperty(nameof(ClientOptions.DefaultRoom), out var room) &&
                        room.GetString() is { Length: > 0 } r)
                        options.DefaultRoom = r;
                    if (client.TryGetProperty(nameof(ClientOptions.AllowInsecureTls), out var tls) &&
                        tls.ValueKind is JsonValueKind.True or JsonValueKind.False)
                        options.AllowInsecureTls = tls.GetBoolean();
                }
            }
        }
        catch { /* fall back to defaults on a malformed file */ }

        if (Environment.GetEnvironmentVariable("DEEPCHAT_ServerUrl") is { Length: > 0 } envUrl)
            options.ServerUrl = envUrl;

        return options;
    }

    public void Dispose() => _http.Dispose();
}
