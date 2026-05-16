using Galileo.Chat.Client.UI;

namespace Galileo.Chat.Client.Commands;

public sealed class BeepCommand : ICommand
{
    public string Name => "beep";
    public string Description => "Liga/desliga o beep ao receber mensagem";

    private readonly ConsoleBeeper _beeper;
    private readonly MessageRenderer _renderer;

    public BeepCommand(ConsoleBeeper beeper, MessageRenderer renderer)
    {
        _beeper = beeper;
        _renderer = renderer;
    }

    public Task ExecuteAsync(string arguments, CancellationToken ct)
    {
        _beeper.Enabled = !_beeper.Enabled;
        _renderer.System($"Beep {(_beeper.Enabled ? "ligado" : "desligado")}.");
        return Task.CompletedTask;
    }
}
