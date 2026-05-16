using System.Text.RegularExpressions;
using Galileo.Chat.Domain.Common;
using Galileo.Chat.Domain.Exceptions;

namespace Galileo.Chat.Domain.ValueObjects;

public sealed partial record RoomName
{
    public const int MinLength = 2;
    public const int MaxLength = 32;

    public string Value { get; }

    private RoomName(string value) => Value = value;

    public static RoomName Create(string value)
    {
        Guard.NotNullOrWhiteSpace(value);
        var normalized = value.Trim().ToLowerInvariant();
        Guard.LengthBetween(normalized, MinLength, MaxLength);

        if (!RoomNamePattern().IsMatch(normalized))
        {
            throw new DomainValidationException(
                nameof(RoomName),
                "must contain only lowercase letters, digits, dashes or underscores");
        }

        return new RoomName(normalized);
    }

    public override string ToString() => Value;
    public static implicit operator string(RoomName n) => n.Value;

    [GeneratedRegex(@"^[a-z0-9][a-z0-9_-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex RoomNamePattern();
}
