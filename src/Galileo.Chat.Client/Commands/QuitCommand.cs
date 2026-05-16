namespace Galileo.Chat.Client.Commands;

public sealed class QuitCommand : ICommand
{
    public string Name => "quit";
    public string Description => "Desconecta e sai";

    private readonly CancellationTokenSource _cts;

    public QuitCommand(CancellationTokenSource cts) => _cts = cts;

    public Task ExecuteAsync(string arguments, CancellationToken ct)
    {
        _cts.Cancel();
        return Task.CompletedTask;
    }
}
