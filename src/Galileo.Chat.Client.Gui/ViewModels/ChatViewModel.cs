using System.Collections.ObjectModel;
using System.Net.Http;
using System.Net.Http.Json;
using System.Windows;
using Galileo.Chat.Client.App;
using Galileo.Chat.Client.Gui.Infrastructure;
using Galileo.Chat.Client.Gui.Models;
using Galileo.Chat.Client.Gui.Services;
using Galileo.Chat.Shared.Dto;

namespace Galileo.Chat.Client.Gui.ViewModels;

/// <summary>The chat screen: transcript, online roster, message input and slash commands.</summary>
public sealed class ChatViewModel : ViewModelBase, IAsyncDisposable
{
    private readonly MainViewModel _main;
    private readonly ChatSession _session;
    private readonly RoomDto _initialRoom;
    private readonly string _initialPassphrase;

    private string _input = string.Empty;
    private string _roomTitle;
    private string _connectionStatus = "Conectando…";
    private bool _connected;

    public ChatViewModel(MainViewModel main, SessionState state, RoomDto room, string passphrase)
    {
        _main = main;
        _initialRoom = room;
        _initialPassphrase = passphrase;
        _roomTitle = $"#{room.Name}";

        _session = new ChatSession(main.Options, state);
        _session.LineAdded += OnLineAdded;
        _session.PresenceChanged += OnPresenceChanged;

        SendCommand = new AsyncRelayCommand(SendAsync, () => Connected && !string.IsNullOrWhiteSpace(Input));
        RefreshOnlineCommand = new AsyncRelayCommand(RefreshOnlineAsync, () => Connected);
        LeaveCommand = new RelayCommand(() => _main.ShowRooms());

        _ = ConnectAsync();
    }

    public ObservableCollection<ChatMessageItem> Messages { get; } = new();
    public ObservableCollection<UserPresenceDto> Online { get; } = new();

    /// <summary>Set by the view to a function that asks the user for a passphrase (used by /join).</summary>
    public Func<string, string?>? PassphrasePrompt { get; set; }

    public string Nickname => _session.Nickname;

    public string Input { get => _input; set => SetField(ref _input, value); }
    public string RoomTitle { get => _roomTitle; private set => SetField(ref _roomTitle, value); }
    public string ConnectionStatus { get => _connectionStatus; private set => SetField(ref _connectionStatus, value); }

    public bool Connected
    {
        get => _connected;
        private set => SetField(ref _connected, value);
    }

    public AsyncRelayCommand SendCommand { get; }
    public AsyncRelayCommand RefreshOnlineCommand { get; }
    public RelayCommand LeaveCommand { get; }

    private async Task ConnectAsync()
    {
        try
        {
            await _session.ConnectAsync(_initialRoom, _initialPassphrase);
            Connected = true;
            ConnectionStatus = "Conectado";
            Add(ChatMessageItem.Status(ChatMessageKind.Success,
                $"Você entrou em #{_initialRoom.Name}. Digite /help para ver os comandos."));
        }
        catch (Exception ex)
        {
            ConnectionStatus = "Falha na conexão";
            Add(ChatMessageItem.Status(ChatMessageKind.Error, $"Não consegui conectar: {ex.Message}"));
        }
    }

    private async Task SendAsync()
    {
        var line = Input.Trim();
        Input = string.Empty;
        if (line.Length == 0) return;

        if (line.StartsWith('/'))
        {
            await HandleCommandAsync(line);
            return;
        }

        await _session.SendMessageAsync(line);
    }

    private async Task HandleCommandAsync(string line)
    {
        var space = line.IndexOf(' ');
        var name = (space < 0 ? line[1..] : line[1..space]).ToLowerInvariant();
        var args = space < 0 ? string.Empty : line[(space + 1)..].Trim();

        switch (name)
        {
            case "help":
                Add(ChatMessageItem.Status(ChatMessageKind.System,
                    "/online · /rooms · /join <sala> · /msg <nick|guid> <texto> · /clear · /help"));
                break;
            case "clear":
                Messages.Clear();
                break;
            case "online":
                await RefreshOnlineAsync();
                break;
            case "rooms":
                await ListRoomsAsync();
                break;
            case "msg":
                await SendDmAsync(args);
                break;
            case "join":
                await JoinAsync(args);
                break;
            default:
                Add(ChatMessageItem.Status(ChatMessageKind.Warning, $"Comando desconhecido: /{name}"));
                break;
        }
    }

    private async Task SendDmAsync(string args)
    {
        var space = args.IndexOf(' ');
        if (space < 0)
        {
            Add(ChatMessageItem.Status(ChatMessageKind.Warning, "Uso: /msg <nick|guid> <texto>"));
            return;
        }
        await _session.SendPrivateMessageAsync(args[..space], args[(space + 1)..].Trim());
    }

    private async Task ListRoomsAsync()
    {
        try
        {
            var rooms = await _main.Http.GetFromJsonAsync<List<RoomDto>>("/api/rooms") ?? new();
            Add(ChatMessageItem.Status(ChatMessageKind.System,
                rooms.Count == 0 ? "Nenhuma sala." : "Salas: " + string.Join(", ", rooms.Select(r => "#" + r.Name))));
        }
        catch (Exception ex)
        {
            Add(ChatMessageItem.Status(ChatMessageKind.Error, $"Erro ao listar salas: {ex.Message}"));
        }
    }

    private async Task JoinAsync(string roomName)
    {
        if (string.IsNullOrWhiteSpace(roomName))
        {
            Add(ChatMessageItem.Status(ChatMessageKind.Warning, "Uso: /join <sala>"));
            return;
        }

        RoomDto? room;
        try
        {
            room = await GetOrCreateRoomAsync(roomName);
        }
        catch (Exception ex)
        {
            Add(ChatMessageItem.Status(ChatMessageKind.Error, $"Não consegui acessar a sala: {ex.Message}"));
            return;
        }
        if (room is null) return;

        var passphrase = PassphrasePrompt?.Invoke($"Passphrase de #{room.Name}");
        if (string.IsNullOrEmpty(passphrase))
        {
            Add(ChatMessageItem.Status(ChatMessageKind.System, "Troca de sala cancelada."));
            return;
        }

        try
        {
            await _session.SwitchRoomAsync(room, passphrase);
            RoomTitle = $"#{room.Name}";
        }
        catch (Exception ex)
        {
            Add(ChatMessageItem.Status(ChatMessageKind.Error, $"Falha ao trocar de sala: {ex.Message}"));
        }
    }

    private async Task<RoomDto?> GetOrCreateRoomAsync(string name)
    {
        var resp = await _main.Http.GetAsync($"/api/rooms/{Uri.EscapeDataString(name)}");
        if (resp.IsSuccessStatusCode)
            return await resp.Content.ReadFromJsonAsync<RoomDto>();

        if (resp.StatusCode != System.Net.HttpStatusCode.NotFound)
        {
            resp.EnsureSuccessStatusCode();
            return null;
        }

        var confirm = MessageBox.Show($"Sala '{name}' não existe. Criar?", "DeepChat",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return null;

        var create = await _main.Http.PostAsJsonAsync("/api/rooms", new CreateRoomRequest { Name = name });
        create.EnsureSuccessStatusCode();
        return await create.Content.ReadFromJsonAsync<RoomDto>();
    }

    private async Task RefreshOnlineAsync()
    {
        try
        {
            var users = await _session.ListOnlineAsync();
            OnPresenceChanged(users);
        }
        catch (Exception ex)
        {
            Add(ChatMessageItem.Status(ChatMessageKind.Error, $"Erro ao listar online: {ex.Message}"));
        }
    }

    // ----- session events (fire on background threads → marshal to UI) -----

    private void OnLineAdded(ChatMessageItem item) => Dispatch(() => Add(item));

    private void OnPresenceChanged(IReadOnlyList<UserPresenceDto> users) => Dispatch(() =>
    {
        Online.Clear();
        foreach (var u in users.OrderBy(u => u.Nickname, StringComparer.OrdinalIgnoreCase))
            Online.Add(u);
    });

    private void Add(ChatMessageItem item) => Messages.Add(item);

    private static void Dispatch(Action action)
    {
        var app = Application.Current;
        if (app is null) { action(); return; }
        if (app.Dispatcher.CheckAccess()) action();
        else app.Dispatcher.Invoke(action);
    }

    public async ValueTask DisposeAsync()
    {
        _session.LineAdded -= OnLineAdded;
        _session.PresenceChanged -= OnPresenceChanged;
        await _session.DisposeAsync();
    }
}
