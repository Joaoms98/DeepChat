namespace Galileo.Chat.Domain.Exceptions;

public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
    public DomainException(string message, Exception inner) : base(message, inner) { }
}

public sealed class DomainValidationException : DomainException
{
    public string Field { get; }

    public DomainValidationException(string field, string message)
        : base($"{field}: {message}")
    {
        Field = field;
    }
}
