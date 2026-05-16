using Galileo.Chat.Domain.Common;
using Galileo.Chat.Domain.Exceptions;

namespace Galileo.Chat.Domain.ValueObjects;

public sealed record Nickname
{
    public const int MinLength = 1;
    public const int MaxLength = 40;

    public string Value { get; }

    private Nickname(string value) => Value = value;

    public static Nickname Create(string value)
    {
        Guard.NotNullOrWhiteSpace(value);
        var trimmed = value.Trim();
        Guard.LengthBetween(trimmed, MinLength, MaxLength);

        foreach (var c in trimmed)
        {
            if (char.IsControl(c))
            {
                throw new DomainValidationException(
                    nameof(Nickname),
                    "must not contain control characters");
            }
        }

        return new Nickname(trimmed);
    }

    public override string ToString() => Value;
    public static implicit operator string(Nickname n) => n.Value;
}
