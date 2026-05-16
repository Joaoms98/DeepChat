using Galileo.Chat.Domain.Entities;

namespace Galileo.Chat.Domain.Abstractions;

public sealed record TokenIssued(string Token, Guid JwtId, DateTime ExpiresAt);

public interface ITokenService
{
    /// <summary>Issues a JWT for the given user. Implementation populates the <c>jti</c>, <c>sub</c>, <c>nick</c>, <c>sid</c>, <c>iat</c>, <c>exp</c> claims.</summary>
    TokenIssued Issue(User user, Guid sessionId);
}
