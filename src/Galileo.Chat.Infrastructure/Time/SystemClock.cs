using Galileo.Chat.Domain.Abstractions;

namespace Galileo.Chat.Infrastructure.Time;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
