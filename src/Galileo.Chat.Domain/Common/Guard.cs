using System.Runtime.CompilerServices;
using Galileo.Chat.Domain.Exceptions;

namespace Galileo.Chat.Domain.Common;

internal static class Guard
{
    public static void NotNull<T>(T? value, [CallerArgumentExpression(nameof(value))] string? name = null)
        where T : class
    {
        if (value is null)
            throw new DomainValidationException(name ?? "value", "must not be null");
    }

    public static void NotNullOrWhiteSpace(string? value, [CallerArgumentExpression(nameof(value))] string? name = null)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainValidationException(name ?? "value", "must not be empty");
    }

    public static void LengthBetween(string value, int min, int max, [CallerArgumentExpression(nameof(value))] string? name = null)
    {
        if (value.Length < min || value.Length > max)
            throw new DomainValidationException(name ?? "value", $"length must be between {min} and {max}");
    }

    public static void Positive(int value, [CallerArgumentExpression(nameof(value))] string? name = null)
    {
        if (value <= 0)
            throw new DomainValidationException(name ?? "value", "must be positive");
    }

    public static void ExactLength(ReadOnlySpan<byte> value, int expected, [CallerArgumentExpression(nameof(value))] string? name = null)
    {
        if (value.Length != expected)
            throw new DomainValidationException(name ?? "value", $"length must be exactly {expected} bytes (got {value.Length})");
    }

    public static void NotEmpty(ReadOnlySpan<byte> value, [CallerArgumentExpression(nameof(value))] string? name = null)
    {
        if (value.IsEmpty)
            throw new DomainValidationException(name ?? "value", "must not be empty");
    }
}
