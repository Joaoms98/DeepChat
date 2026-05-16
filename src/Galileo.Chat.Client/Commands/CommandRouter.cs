using Galileo.Chat.Client.UI;

namespace Galileo.Chat.Client.Commands;

public sealed class CommandRouter
{
    private readonly Dictionary<string, ICommand> _commands;
    private readonly MessageRenderer _renderer;

    public CommandRouter(IEnumerable<ICommand> commands, MessageRenderer renderer)
    {
        _commands = commands.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);
        _renderer = renderer;
    }

    public bool IsCommand(string line) => line.StartsWith('/');

    public async Task DispatchAsync(string line, CancellationToken ct)
    {
        var trimmed = line[1..].TrimStart();
        var spaceIdx = trimmed.IndexOf(' ');
        var name = spaceIdx >= 0 ? trimmed[..spaceIdx] : trimmed;
        var args = spaceIdx >= 0 ? trimmed[(spaceIdx + 1)..] : string.Empty;

        if (string.Equals(name, "help", StringComparison.OrdinalIgnoreCase))
        {
            ShowHelp();
            return;
        }

        if (!_commands.TryGetValue(name, out var cmd))
        {
            _renderer.Warning($"Comando desconhecido: /{name}. Tente /help.");
            return;
        }

        try
        {
            await cmd.ExecuteAsync(args, ct);
        }
        catch (Exception ex)
        {
            _renderer.Error($"Comando /{name} falhou: {ex.Message}");
        }
    }

    private void ShowHelp()
    {
        _renderer.System("Comandos disponíveis:");
        foreach (var c in _commands.Values.OrderBy(c => c.Name))
            _renderer.System($"  /{c.Name,-8} {c.Description}");
        _renderer.System("  /help     Mostra esta ajuda");
    }
}
