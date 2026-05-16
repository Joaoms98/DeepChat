using System.Net.Http.Json;
using Galileo.Chat.Client.App;
using Galileo.Chat.Client.Auth;
using Galileo.Chat.Client.Commands;
using Galileo.Chat.Client.Configuration;
using Galileo.Chat.Client.Crypto;
using Galileo.Chat.Client.UI;
using Galileo.Chat.Crypto.Kdf;
using Galileo.Chat.Crypto.KeyStore;
using Galileo.Chat.Crypto.Random;
using Galileo.Chat.Shared.Dto;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Spectre.Console;

// ----- Boot (one-shot) -----
var console = AnsiConsole.Console;
CatSplash.Play(console);

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables(prefix: "DEEPCHAT_")
    .Build();

var clientOptions = new ClientOptions();
configuration.GetSection(ClientOptions.SectionName).Bind(clientOptions);

// ServerUrl é fixo: vem do appsettings.json (ou do default em ClientOptions).
// Removemos o prompt de confirmação pra cortar uma etapa do fluxo.
console.MarkupLineInterpolated($"[grey50]Servidor: {clientOptions.ServerUrl}[/]");

var httpHandler = new HttpClientHandler();
if (clientOptions.AllowInsecureTls)
    httpHandler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;

using var http = new HttpClient(httpHandler) { BaseAddress = new Uri(clientOptions.ServerUrl) };
var auth = new HttpAuthClient(http);

// =========================================================================
// MAIN LOOP — wraps pre-auth + room-selection.
// Logout falls back here; only "Sair" or entering chat exits this.
// =========================================================================
while (true)
{
    // ---- Pre-auth menu loop ----
    LoginResponse? login = null;
    while (login is null)
    {
        console.WriteLine();
        var choice = console.Prompt(new SelectionPrompt<string>()
            .Title("[bold steelblue1]Menu inicial[/]")
            .AddChoices("Entrar", "Registrar", "Sair"));

        try
        {
            switch (choice)
            {
                case "Entrar":
                {
                    var u = console.Ask<string>("[steelblue1]Usuário[/]:");
                    var p = console.Ask<string>("[steelblue1]Senha[/]:");
                    login = await auth.LoginAsync(u, p);
                    if (login is null)
                        console.MarkupLine("[yellow]! Usuário ou senha inválidos.[/]");
                    break;
                }
                case "Registrar":
                {
                    var u = console.Ask<string>("[steelblue1]Usuário[/]:");
                    var p = console.Ask<string>("[steelblue1]Senha[/] [grey50](mínimo 8 caracteres)[/]:");
                    var n = console.Prompt(
                        new TextPrompt<string>("[steelblue1]Apelido[/]:").DefaultValue(u));
                    var resp = await http.PostAsJsonAsync("/api/users",
                        new RegisterRequest { Username = u, Nickname = n, Password = p });
                    if (resp.IsSuccessStatusCode)
                    {
                        console.MarkupLine("[springgreen2_1]✓ Registrado. Volte ao menu e escolha 'Entrar'.[/]");
                    }
                    else
                    {
                        var body = await resp.Content.ReadAsStringAsync();
                        console.MarkupLineInterpolated($"[red]× Falha no registro: {body}[/]");
                    }
                    break;
                }
                case "Sair":
                    return 0;
            }
        }
        catch (Exception ex)
        {
            console.MarkupLineInterpolated($"[red]× Erro: {ex.Message}[/]");
        }
    }

    // Authenticated: token goes into every subsequent HTTP call.
    console.MarkupLineInterpolated($"[springgreen2_1]✓ Autenticado como {login.Nickname} ({login.UserId:D})[/]");
    console.MarkupLineInterpolated($"[grey50]Token expira às {login.ExpiresAt.ToLocalTime():HH:mm:ss}.[/]");

    http.DefaultRequestHeaders.Authorization =
        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", login.Token);

    // ---- Post-auth menu loop: pick a room ----
    RoomDto? selectedRoom = null;
    bool wantsLogout = false;
    while (selectedRoom is null && !wantsLogout)
    {
        // Refresh the room list every iteration so a sala just created elsewhere
        // appears without restarting the client.
        List<RoomDto> rooms = new();
        try
        {
            rooms = await http.GetFromJsonAsync<List<RoomDto>>("/api/rooms") ?? new();
        }
        catch (Exception ex)
        {
            console.MarkupLineInterpolated($"[red]× Erro ao listar salas: {ex.Message}[/]");
        }

        console.WriteLine();
        if (rooms.Count == 0)
        {
            console.MarkupLine("[grey70]· Nenhuma sala criada ainda no servidor.[/]");
        }
        else
        {
            var table = new Table()
                .Border(TableBorder.Rounded)
                .BorderStyle(Theme.Brand)
                .Title($"[bold steelblue1]Salas existentes ({rooms.Count})[/]")
                .AddColumn("[grey70]Nome[/]");
            foreach (var r in rooms)
                table.AddRow($"[skyblue1]#{Markup.Escape(r.Name)}[/]");
            console.Write(table);
        }

        var choices = new List<string>();
        if (rooms.Count > 0) choices.Add("Entrar em sala existente");
        choices.Add("Criar sala nova");
        choices.Add("Logout");
        choices.Add("Sair");

        var choice = console.Prompt(new SelectionPrompt<string>()
            .Title($"[bold steelblue1]Logado como {login.Nickname}[/] — o que deseja fazer?")
            .AddChoices(choices));

        try
        {
            switch (choice)
            {
                case "Entrar em sala existente":
                {
                    selectedRoom = console.Prompt(new SelectionPrompt<RoomDto>()
                        .Title("Escolha a sala:")
                        .UseConverter(r => $"#{r.Name}")
                        .PageSize(10)
                        .MoreChoicesText("[grey50](use ↑↓ pra ver mais)[/]")
                        .AddChoices(rooms));
                    break;
                }
                case "Criar sala nova":
                {
                    var name = console.Ask<string>("[steelblue1]Nome da nova sala[/]:");
                    var resp = await http.PostAsJsonAsync("/api/rooms",
                        new CreateRoomRequest { Name = name });
                    if (resp.IsSuccessStatusCode)
                    {
                        selectedRoom = await resp.Content.ReadFromJsonAsync<RoomDto>();
                        console.MarkupLineInterpolated($"[springgreen2_1]✓ Sala criada: #{selectedRoom!.Name}[/]");
                    }
                    else
                    {
                        var body = await resp.Content.ReadAsStringAsync();
                        console.MarkupLineInterpolated($"[red]× Falha ao criar sala: {body}[/]");
                    }
                    break;
                }
                case "Logout":
                {
                    http.DefaultRequestHeaders.Authorization = null;
                    wantsLogout = true;
                    console.MarkupLine("[grey70]· Logout efetuado.[/]");
                    break;
                }
                case "Sair":
                    return 0;
            }
        }
        catch (Exception ex)
        {
            console.MarkupLineInterpolated($"[red]× Erro: {ex.Message}[/]");
        }
    }

    if (wantsLogout)
        continue; // back to the pre-auth menu

    // ---- Chat session: passphrase → key derivation → connect → loop ----
    var room = selectedRoom!;
    console.MarkupLineInterpolated($"[grey70]· Entrando na sala: #{room.Name}[/]");

    string passphrase;
    byte[] key;
    string fingerprint;
    try
    {
        passphrase = PassphrasePrompt.Ask(console, $"Passphrase de #{room.Name}");
        var salt = Convert.FromBase64String(room.SaltBase64);
        key = console.Status().Start("Derivando chave da sala (Argon2id)…",
            _ => new Argon2KeyDerivation(Argon2Parameters.Interactive).DeriveKey(passphrase, salt));
        fingerprint = KeyFingerprint.Of(key);
    }
    catch (Exception ex)
    {
        console.MarkupLineInterpolated($"[red]× Falha na derivação da chave: {ex.Message}[/]");
        continue; // back to the room menu
    }

    console.MarkupLineInterpolated(
        $"[grey50]Fingerprint da chave:[/] [bold steelblue1]{fingerprint}[/] [grey50](precisa bater com a dos outros membros)[/]");

    using var keyStore = new InMemoryRoomKeyStore();
    keyStore.Save(room.Id, key);
    Array.Clear(key);

    var crypto = new ClientCryptoService(keyStore);
    var resolver = new NicknameResolver();

    var state = new SessionState
    {
        UserId = login.UserId,
        Nickname = login.Nickname,
        Token = login.Token,
        TokenExpiresAt = login.ExpiresAt,
        RoomId = room.Id,
        RoomName = room.Name
    };

    await using var conn = ConnectionManager.Build(
        clientOptions.ServerUrl,
        () => Task.FromResult<string?>(state.Token),
        clientOptions.AllowInsecureTls);

    var renderer = new MessageRenderer(console, login.Nickname);
    var beeper = new ConsoleBeeper();

    conn.Connection.On<EncryptedMessageDto>("ReceiveMessage", dto =>
    {
        try
        {
            var bytes = crypto.DecryptFromRoom(dto);
            var text = System.Text.Encoding.UTF8.GetString(bytes);
            renderer.Incoming(dto.SenderNickname, text, dto.CreatedAt);
            beeper.Notify();
        }
        catch (Galileo.Chat.Crypto.Exceptions.DecryptionFailedException)
        {
            renderer.Error($"Mensagem de {dto.SenderNickname} não pôde ser lida — passphrase desta sala provavelmente difere da de quem enviou.");
        }
        catch (Exception ex)
        {
            renderer.Error($"Falha inesperada ao processar mensagem: {ex.Message}");
        }
    });

    conn.Connection.On<EncryptedMessageDto>("ReceivePrivateMessage", dto =>
    {
        try
        {
            var bytes = crypto.DecryptFromRoom(dto);
            var text = System.Text.Encoding.UTF8.GetString(bytes);
            renderer.Incoming($"{dto.SenderNickname} (DM)", text, dto.CreatedAt);
            beeper.Notify();
        }
        catch (Galileo.Chat.Crypto.Exceptions.DecryptionFailedException)
        {
            renderer.Error($"DM de {dto.SenderNickname} não pôde ser lida — o remetente está em outra sala / com chave diferente.");
        }
        catch (Exception ex)
        {
            renderer.Error($"Falha inesperada ao processar DM: {ex.Message}");
        }
    });

    conn.Connection.On<string, UserPresenceDto>("UserJoined", (_, user) =>
    {
        resolver.Observe(user.UserId, user.Nickname);
        renderer.System($"{user.Nickname} entrou na sala.");
    });

    conn.Connection.On<string, Guid>("UserLeft",
        (_, userId) => renderer.System($"Usuário {userId:N}… saiu da sala."));

    conn.Connection.Reconnecting += _ =>
    {
        renderer.Warning("Reconectando…");
        return Task.CompletedTask;
    };
    conn.Connection.Reconnected += _ =>
    {
        renderer.Success("Reconectado.");
        return conn.JoinRoomAsync(state.RoomId);
    };

    try
    {
        console.Status().Start("Conectando ao servidor…",
            _ => conn.StartAsync().GetAwaiter().GetResult());
        await conn.JoinRoomAsync(room.Id);
    }
    catch (Exception ex)
    {
        console.MarkupLineInterpolated($"[red]× Não consegui conectar ao chat: {ex.Message}[/]");
        continue; // back to the room menu
    }

    // Seed nickname resolver with whoever is already in the room.
    try
    {
        var initialPresence = await conn.ListOnlineAsync(room.Id);
        resolver.Observe(initialPresence);
    }
    catch { /* best-effort */ }

    // Clean slate when the chat starts — menu noise / login traces disappear.
    console.Clear();
    renderer.Success($"Você entrou em #{room.Name}. Digite /help pra ver os comandos disponíveis.");
    console.WriteLine();

    // ----- Command router & input loop -----
    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

    var commands = new ICommand[]
    {
        new OnlineCommand(conn, state, renderer, resolver),
        new ClearCommand(renderer, console),
        new BeepCommand(beeper, renderer),
        new MsgCommand(conn, crypto, state, renderer, resolver),
        new JoinCommand(http, conn, keyStore, state, resolver, console, renderer),
        new RoomsCommand(http, console, renderer),
        new QuitCommand(cts)
    };
    var router = new CommandRouter(commands, renderer);

    renderer.Prompt();
    while (!cts.IsCancellationRequested)
    {
        string? line;
        try
        {
            line = await Task.Run(Console.ReadLine, cts.Token);
        }
        catch (OperationCanceledException) { break; }

        if (line is null) break;
        if (string.IsNullOrWhiteSpace(line)) { renderer.Prompt(); continue; }

        if (router.IsCommand(line))
        {
            await router.DispatchAsync(line, cts.Token);
            renderer.Prompt();
            continue;
        }

        try
        {
            var envelope = crypto.EncryptForRoom(
                roomId: state.RoomId,
                plaintext: System.Text.Encoding.UTF8.GetBytes(line),
                senderId: state.UserId,
                senderNickname: state.Nickname,
                utcNow: DateTime.UtcNow);
            await conn.PostMessageAsync(state.RoomId, envelope, cts.Token);
            renderer.Outgoing(line, DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            renderer.Error($"Falha ao enviar: {ex.Message}");
        }
    }

    renderer.System("Desconectando…");
    return 0;
}
