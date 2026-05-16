using Galileo.Chat.Shared.Dto;
using Spectre.Console;

namespace Galileo.Chat.Client.UI;

public static class PresenceRenderer
{
    public static void RenderTable(IAnsiConsole console, IReadOnlyList<UserPresenceDto> users, string roomName)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderStyle(Theme.Brand)
            .Title($"[bold steelblue1]Online em #{Markup.Escape(roomName)}[/] [grey50]({users.Count})[/]")
            .AddColumn(new TableColumn("[grey70]Nick[/]"))
            .AddColumn(new TableColumn("[grey70]Conectado[/]").RightAligned());

        foreach (var u in users)
        {
            var since = u.ConnectedAt.ToLocalTime().ToString("HH:mm:ss");
            table.AddRow(
                $"[bold skyblue1]{Markup.Escape(u.Nickname)}[/]",
                $"[grey50]{since}[/]");
        }

        console.Write(table);
    }
}
