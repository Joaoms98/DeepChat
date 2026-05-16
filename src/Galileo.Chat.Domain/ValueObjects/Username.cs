using System.Text.RegularExpressions;
using Galileo.Chat.Domain.Common;
using Galileo.Chat.Domain.Exceptions;

namespace Galileo.Chat.Domain.ValueObjects;

public sealed partial record Username
{
    public const int MinLength = 3;
    public const int MaxLength = 32;

    public string Value { get; }

    private Username(string value) => Value = value;

    public static Username Create(string value)
    {
        Guard.NotNullOrWhiteSpace(value);
        var normalized = value.Trim().ToLowerInvariant();
        Guard.LengthBetween(normalized, MinLength, MaxLength);

        if (!UsernamePattern().IsMatch(normalized))
        {
            throw new DomainValidationException(
                nameof(Username),
                "must contain only lowercase letters, digits, dots, dashes or underscores");
        }

        return new Username(normalized);
    }

    public override string ToString() => Value;
    public static implicit operator string(Username u) => u.Value;

    [GeneratedRegex(@"^[a-z0-9][a-z0-9._-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex UsernamePattern();
}
