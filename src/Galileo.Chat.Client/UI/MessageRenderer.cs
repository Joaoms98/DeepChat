using Spectre.Console;

namespace Galileo.Chat.Client.UI;

/// <summary>
/// Console output for chat events. Thread-safe — guarded by a single lock so
/// background SignalR callbacks and the input thread never tear each other's
/// output mid-line.
/// </summary>
public sealed class MessageRenderer
{
    private readonly IAnsiConsole _console;
    private readonly object _gate = new();
    private readonly string _selfNickname;

    public MessageRenderer(IAnsiConsole console, string selfNickname)
    {
        _console = console;
        _selfNickname = selfNickname;
    }

    public void Incoming(string nickname, string text, DateTime timestamp)
    {
        lock (_gate)
        {
            var time = timestamp.ToLocalTime().ToString("HH:mm:ss");
            var style = nickname == _selfNickname ? "seagreen1" : "skyblue1";
            _console.MarkupLineInterpolated(
                $"[grey50]{time}[/]  [bold {style}]{nickname}[/]  [white]{text}[/]");
            RedrawPrompt();
        }
    }

    /// <summary>
    /// Renders the user's own outgoing message AFTER erasing the raw line that
    /// the readline left on screen ("› hello"). Without this, every send shows
    /// twice: the raw typed line, then the styled echo.
    /// </summary>
    public void Outgoing(string text, DateTime timestamp)
    {
        lock (_gate)
        {
            // Erase the previous line (the one that contains the prompt + raw input).
            // Falls back to a plain echo if the terminal does not support ANSI.
            if (_console.Profile.Capabilities.Ansi)
                _console.Write("\x1b[1A\r\x1b[2K");

            var time = timestamp.ToLocalTime().ToString("HH:mm:ss");
            _console.MarkupLineInterpolated(
                $"[grey50]{time}[/]  [bold seagreen1]{_selfNickname}[/]  [white]{text}[/]");
            RedrawPrompt();
        }
    }

    public void System(string message)
    {
        lock (_gate)
        {
            _console.MarkupLineInterpolated($"[grey70]· {message}[/]");
            RedrawPrompt();
        }
    }

    public void Warning(string message)
    {
        lock (_gate)
        {
            _console.MarkupLineInterpolated($"[yellow]! {message}[/]");
            RedrawPrompt();
        }
    }

    public void Error(string message)
    {
        lock (_gate)
        {
            _console.MarkupLineInterpolated($"[red]× {message}[/]");
            RedrawPrompt();
        }
    }

    public void Success(string message)
    {
        lock (_gate)
        {
            _console.MarkupLineInterpolated($"[springgreen2_1]✓ {message}[/]");
            RedrawPrompt();
        }
    }

    public void Prompt()
    {
        lock (_gate) RedrawPrompt();
    }

    /// <summary>Redraws the input cursor marker. Best-effort — terminals without ANSI support degrade gracefully.</summary>
    private void RedrawPrompt()
    {
        _console.Markup("[grey50]› [/]");
    }
}
