namespace Galileo.Chat.Domain.Exceptions;

/// <summary>
/// Raised on any login failure: unknown user, wrong password, deactivated account.
/// The message NEVER discloses which case it is — that's an information-disclosure
/// vector that lets attackers enumerate valid usernames.
/// </summary>
public sealed class AuthenticationFailedException : DomainException
{
    public AuthenticationFailedException() : base("Invalid credentials.") { }
    public AuthenticationFailedException(string message) : base(message) { }
}
