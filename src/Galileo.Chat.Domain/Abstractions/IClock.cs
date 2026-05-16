namespace Galileo.Chat.Domain.Abstractions;

/// <summary>
/// Indirection over <see cref="DateTime.UtcNow"/> so handlers and entities can be
/// tested with deterministic timestamps. Production registration: SystemClock.
/// </summary>
public interface IClock
{
    DateTime UtcNow { get; }
}
