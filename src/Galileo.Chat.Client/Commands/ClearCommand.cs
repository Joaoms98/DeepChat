using Galileo.Chat.Client.UI;
using Spectre.Console;

namespace Galileo.Chat.Client.Commands;

public sealed class ClearCommand : ICommand
{
    public string Name => "clear";
    public string Description => "Limpa a tela";

    private readonly MessageRenderer _renderer;
    private readonly IAnsiConsole _console;

    public ClearCommand(MessageRenderer renderer, IAnsiConsole console)
    {
        _renderer = renderer;
        _console = console;
    }

    public Task ExecuteAsync(string arguments, CancellationToken ct)
    {
        _console.Clear();
        _renderer.System("Tela limpa.");
        return Task.CompletedTask;
    }
}
