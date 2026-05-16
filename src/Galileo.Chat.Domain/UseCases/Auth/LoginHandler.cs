using Galileo.Chat.Domain.Abstractions;
using Galileo.Chat.Domain.Entities;
using Galileo.Chat.Domain.Exceptions;
using Galileo.Chat.Domain.ValueObjects;

namespace Galileo.Chat.Domain.UseCases.Auth;

public sealed class LoginHandler
{
    private readonly IUserRepository _users;
    private readonly ISessionRepository _sessions;
    private readonly IPasswordHasher _hasher;
    private readonly ITokenService _tokens;
    private readonly IClock _clock;

    public LoginHandler(
        IUserRepository users,
        ISessionRepository sessions,
        IPasswordHasher hasher,
        ITokenService tokens,
        IClock clock)
    {
        _users = users;
        _sessions = sessions;
        _hasher = hasher;
        _tokens = tokens;
        _clock = clock;
    }

    public async Task<LoginResult> HandleAsync(LoginCommand cmd, CancellationToken ct = default)
    {
        Username username;
        try
        {
            username = Username.Create(cmd.Username);
        }
        catch (DomainException)
        {
            // Bad-format username gets the same generic failure as wrong password —
            // we never tell the caller whether the username was even shaped correctly.
            throw new AuthenticationFailedException();
        }

        var user = await _users.FindByUsernameAsync(username, ct);

        // Always run the hash verify even when the user is unknown, against a
        // dummy hash. This keeps the response time roughly constant whether or
        // not the username exists — denies the timing oracle that lets attackers
        // enumerate accounts.
        var hashToCheck = user?.PasswordHash ?? DummyHash;
        var passwordOk = _hasher.Verify(cmd.Password, hashToCheck);

        if (user is null || !user.IsActive || !passwordOk)
            throw new AuthenticationFailedException();

        var sessionId = Guid.NewGuid();
        var token = _tokens.Issue(user, sessionId);

        var session = Session.Issue(
            userId: user.Id,
            jwtId: token.JwtId,
            issuedAt: _clock.UtcNow,
            lifetime: token.ExpiresAt - _clock.UtcNow,
            remoteIp: cmd.RemoteIp);

        // Replace the auto-generated session.Id with the one baked into the JWT
        // — the token's "sid" claim must match the row in the Sessions table.
        // Cleanest path: build Session via Rehydrate so we control its Id.
        session = Session.Rehydrate(
            id: sessionId,
            userId: session.UserId,
            jwtId: session.JwtId,
            issuedAt: session.IssuedAt,
            expiresAt: session.ExpiresAt,
            revokedAt: null,
            remoteIp: session.RemoteIp);

        await _sessions.AddAsync(session, ct);

        user.RecordLogin(_clock.UtcNow);
        await _users.UpdateAsync(user, ct);

        return new LoginResult(token.Token, token.ExpiresAt, user.Id, user.Nickname.Value);
    }

    /// <summary>
    /// A pre-computed Argon2id hash with the production parameters, so the timing
    /// of "user unknown" matches the timing of "password wrong". The plaintext
    /// it hashes is unguessable garbage and never matches a real password input.
    /// </summary>
    private const string DummyHash =
        "$argon2id$v=19$m=131072,t=4,p=2$" +
        "Y29uc3RhbnRfdGltZV9zYWx0XzE2YnQ$" +
        "ZGVjb3lfaGFzaF9ub3RfYV9yZWFsX29uZV9hYWFhYWFhYWFhYWFhYWE";
}
