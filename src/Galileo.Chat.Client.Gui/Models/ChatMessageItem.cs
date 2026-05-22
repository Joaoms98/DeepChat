namespace Galileo.Chat.Client.Gui.Models;

/// <summary>Visual category of a chat line — drives colour in the message list.</summary>
public enum ChatMessageKind
{
    Self,
    Other,
    DirectMessage,
    System,
    Warning,
    Error,
    Success
}

/// <summary>Immutable display row for the chat transcript.</summary>
public sealed class ChatMessageItem
{
    public required ChatMessageKind Kind { get; init; }
    public required string Sender { get; init; }
    public required string Text { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    public string TimeLabel => Timestamp.ToLocalTime().ToString("HH:mm:ss");

    /// <summary>True for system/status lines that render without a sender column.</summary>
    public bool IsStatus =>
        Kind is ChatMessageKind.System or ChatMessageKind.Warning
            or ChatMessageKind.Error or ChatMessageKind.Success;

    public static ChatMessageItem Status(ChatMessageKind kind, string text) => new()
    {
        Kind = kind,
        Sender = string.Empty,
        Text = text
    };
}
