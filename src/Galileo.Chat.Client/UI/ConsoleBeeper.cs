namespace Galileo.Chat.Client.UI;

/// <summary>
/// Cross-thread safe wrapper around Console.Beep. Beep is OS-blocking on Windows,
/// so we hand it off to a Task and never await — caller is not delayed by the chime.
/// </summary>
public sealed class ConsoleBeeper
{
    public bool Enabled { get; set; } = true;

    public void Notify()
    {
        if (!Enabled) return;
        if (!OperatingSystem.IsWindows()) return;

        _ = Task.Run(() =>
        {
            try { Console.Beep(880, 90); }
            catch { /* Beep can throw on headless / no-audio hosts; ignore. */ }
        });
    }
}
