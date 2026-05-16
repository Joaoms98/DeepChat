using Galileo.Chat.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Galileo.Chat.Infrastructure.Persistence.Conversions;

public sealed class RoomNameConverter : ValueConverter<RoomName, string>
{
    public RoomNameConverter()
        : base(
            convertToProviderExpression: r => r.Value,
            convertFromProviderExpression: s => RoomName.Create(s))
    {
    }
}
