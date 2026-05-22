using System.Collections.ObjectModel;
using System.Net.Http;
using System.Net.Http.Json;
using Galileo.Chat.Client.Gui.Infrastructure;
using Galileo.Chat.Shared.Dto;

namespace Galileo.Chat.Client.Gui.ViewModels;

/// <summary>Room picker: list, create, then unlock with a passphrase.</summary>
public sealed class RoomsViewModel : ViewModelBase
{
    private readonly MainViewModel _main;
    private RoomDto? _selectedRoom;
    private string _newRoomName = string.Empty;
    private string _passphrase = string.Empty;
    private string _status = string.Empty;
    private bool _isError;
    private bool _busy;

    public RoomsViewModel(MainViewModel main)
    {
        _main = main;
        RefreshCommand = new AsyncRelayCommand(LoadRoomsAsync, () => !Busy);
        CreateRoomCommand = new AsyncRelayCommand(CreateRoomAsync, () => !Busy);
        EnterRoomCommand = new RelayCommand(EnterRoom, () => SelectedRoom is not null && Passphrase.Length > 0);
        LogoutCommand = new RelayCommand(() => _main.ShowLogin());
        _ = LoadRoomsAsync();
    }

    public ObservableCollection<RoomDto> Rooms { get; } = new();

    public string Greeting => $"Logado como {_main.Login?.Nickname}";

    public RoomDto? SelectedRoom
    {
        get => _selectedRoom;
        set => SetField(ref _selectedRoom, value);
    }

    public string NewRoomName { get => _newRoomName; set => SetField(ref _newRoomName, value); }
    public string Passphrase { get => _passphrase; set => SetField(ref _passphrase, value); }

    public string Status { get => _status; private set => SetField(ref _status, value); }
    public bool IsError { get => _isError; private set => SetField(ref _isError, value); }
    public bool Busy { get => _busy; private set => SetField(ref _busy, value); }

    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand CreateRoomCommand { get; }
    public RelayCommand EnterRoomCommand { get; }
    public RelayCommand LogoutCommand { get; }

    private async Task LoadRoomsAsync()
    {
        Busy = true;
        try
        {
            var rooms = await _main.Http.GetFromJsonAsync<List<RoomDto>>("/api/rooms") ?? new();
            Rooms.Clear();
            foreach (var r in rooms) Rooms.Add(r);
            SetStatus(rooms.Count == 0 ? "Nenhuma sala criada ainda." : $"{rooms.Count} sala(s).", error: false);
        }
        catch (Exception ex)
        {
            SetStatus($"Erro ao listar salas: {ex.Message}", error: true);
        }
        finally
        {
            Busy = false;
        }
    }

    private async Task CreateRoomAsync()
    {
        if (string.IsNullOrWhiteSpace(NewRoomName))
        {
            SetStatus("Informe o nome da sala.", error: true);
            return;
        }

        Busy = true;
        try
        {
            var resp = await _main.Http.PostAsJsonAsync("/api/rooms", new CreateRoomRequest { Name = NewRoomName });
            if (resp.IsSuccessStatusCode)
            {
                var room = await resp.Content.ReadFromJsonAsync<RoomDto>();
                NewRoomName = string.Empty;
                await LoadRoomsAsync();
                if (room is not null)
                    SelectedRoom = Rooms.FirstOrDefault(r => r.Id == room.Id);
                SetStatus("Sala criada.", error: false);
            }
            else
            {
                SetStatus($"Falha ao criar sala: {await resp.Content.ReadAsStringAsync()}", error: true);
            }
        }
        catch (Exception ex)
        {
            SetStatus($"Erro: {ex.Message}", error: true);
        }
        finally
        {
            Busy = false;
        }
    }

    private void EnterRoom()
    {
        if (SelectedRoom is null || Passphrase.Length == 0) return;
        _main.ShowChat(SelectedRoom, Passphrase);
    }

    private void SetStatus(string message, bool error)
    {
        Status = message;
        IsError = error;
    }
}
