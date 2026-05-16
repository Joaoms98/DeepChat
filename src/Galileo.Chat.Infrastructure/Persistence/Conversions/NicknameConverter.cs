using Galileo.Chat.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Galileo.Chat.Infrastructure.Persistence.Conversions;

public sealed class NicknameConverter : ValueConverter<Nickname, string>
{
    public NicknameConverter()
        : base(
            convertToProviderExpression: n => n.Value,
            convertFromProviderExpression: s => Nickname.Create(s))
    {
    }
}
