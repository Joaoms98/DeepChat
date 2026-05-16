namespace Galileo.Chat.Client.Commands;

public interface ICommand
{
    string Name { get; }
    string Description { get; }
    Task ExecuteAsync(string arguments, CancellationToken ct);
}
