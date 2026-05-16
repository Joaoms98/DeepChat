using Galileo.Chat.Domain.Abstractions;

namespace Galileo.Chat.Domain.Tests.Fakes;

public sealed class FakeClock : IClock
{
    public DateTime UtcNow { get; set; } = new(2026, 5, 15, 12, 0, 0, DateTimeKind.Utc);
}
