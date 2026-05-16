using System.Net.Http.Json;
using Galileo.Chat.Client.UI;
using Galileo.Chat.Shared.Dto;
using Spectre.Console;

namespace Galileo.Chat.Client.Commands;

/// <summary>/rooms — lists every room the server knows about.</summary>
public sealed class RoomsCommand : ICommand
{
    public string Name => "rooms";
    public string Description => "Lista as salas existentes no servidor";

    private readonly HttpClient _http;
    private readonly IAnsiConsole _console;
    private readonly MessageRenderer _renderer;

    public RoomsCommand(HttpClient http, IAnsiConsole console, MessageRenderer renderer)
    {
        _http = http;
        _console = console;
        _renderer = renderer;
    }

    public async Task ExecuteAsync(string arguments, CancellationToken ct)
    {
        var rooms = await _http.GetFromJsonAsync<List<RoomDto>>("/api/rooms", ct);
        if (rooms is null || rooms.Count == 0)
        {
            _renderer.System("Nenhuma sala cadastrada.");
            return;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderStyle(Theme.Brand)
            .Title("[bold steelblue1]Salas[/]")
            .AddColumn("[grey70]Nome[/]")
            .AddColumn("[grey70]Id[/]");

        foreach (var r in rooms)
            table.AddRow($"[skyblue1]#{Markup.Escape(r.Name)}[/]", $"[grey50]{r.Id:D}[/]");

        _console.Write(table);
    }
}
