using System.Net.Http;
using System.Net.Http.Json;
using Galileo.Chat.Client.Gui.Infrastructure;
using Galileo.Chat.Shared.Dto;

namespace Galileo.Chat.Client.Gui.ViewModels;

/// <summary>Login / register screen.</summary>
public sealed class LoginViewModel : ViewModelBase
{
    private readonly MainViewModel _main;
    private string _username = string.Empty;
    private string _password = string.Empty;
    private string _nickname = string.Empty;
    private string _status = string.Empty;
    private bool _isError;
    private bool _busy;

    public LoginViewModel(MainViewModel main)
    {
        _main = main;
        LoginCommand = new AsyncRelayCommand(LoginAsync, () => CanSubmit);
        RegisterCommand = new AsyncRelayCommand(RegisterAsync, () => CanSubmit);
    }

    public string Username { get => _username; set => SetField(ref _username, value); }
    public string Password { get => _password; set => SetField(ref _password, value); }
    public string Nickname { get => _nickname; set => SetField(ref _nickname, value); }

    public string Status { get => _status; private set => SetField(ref _status, value); }
    public bool IsError { get => _isError; private set => SetField(ref _isError, value); }
    public bool Busy
    {
        get => _busy;
        private set { if (SetField(ref _busy, value)) OnPropertyChanged(nameof(CanSubmit)); }
    }

    public bool CanSubmit => !Busy;

    public AsyncRelayCommand LoginCommand { get; }
    public AsyncRelayCommand RegisterCommand { get; }

    private async Task LoginAsync()
    {
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrEmpty(Password))
        {
            SetStatus("Informe usuário e senha.", error: true);
            return;
        }

        Busy = true;
        SetStatus("Autenticando…", error: false);
        try
        {
            var login = await _main.Auth.LoginAsync(Username, Password);
            if (login is null)
            {
                SetStatus("Usuário ou senha inválidos.", error: true);
                return;
            }
            _main.OnAuthenticated(login);
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

    private async Task RegisterAsync()
    {
        if (string.IsNullOrWhiteSpace(Username) || Password.Length < 8)
        {
            SetStatus("Usuário obrigatório e senha com no mínimo 8 caracteres.", error: true);
            return;
        }

        Busy = true;
        SetStatus("Registrando…", error: false);
        try
        {
            var nick = string.IsNullOrWhiteSpace(Nickname) ? Username : Nickname;
            var resp = await _main.Http.PostAsJsonAsync("/api/users",
                new RegisterRequest { Username = Username, Nickname = nick, Password = Password });
            if (resp.IsSuccessStatusCode)
                SetStatus("Registrado! Agora clique em Entrar.", error: false);
            else
                SetStatus($"Falha no registro: {await resp.Content.ReadAsStringAsync()}", error: true);
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

    private void SetStatus(string message, bool error)
    {
        Status = message;
        IsError = error;
    }
}
