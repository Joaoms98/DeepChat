using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Galileo.Chat.Domain.Abstractions;
using Galileo.Chat.Domain.Entities;
using Galileo.Chat.Infrastructure.Options;
using Microsoft.IdentityModel.Tokens;

namespace Galileo.Chat.Infrastructure.Auth;

public sealed class JwtTokenService : ITokenService
{
    public const string ClaimNick = "nick";
    public const string ClaimSid = "sid";

    private readonly JwtOptions _opts;
    private readonly IClock _clock;
    private readonly SigningCredentials _signing;

    public JwtTokenService(Microsoft.Extensions.Options.IOptions<JwtOptions> opts, IClock clock)
    {
        _opts = opts.Value;
        _clock = clock;

        var keyBytes = ResolveKey(_opts.SecretKey);
        if (keyBytes.Length < 32)
            throw new InvalidOperationException(
                "JWT secret must decode to at least 32 bytes (256 bits) for HS256.");

        _signing = new SigningCredentials(
            new SymmetricSecurityKey(keyBytes),
            SecurityAlgorithms.HmacSha256);
    }

    public TokenIssued Issue(User user, Guid sessionId)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (sessionId == Guid.Empty)
            throw new ArgumentException("SessionId must not be empty.", nameof(sessionId));

        var jwtId = Guid.NewGuid();
        var issuedAt = _clock.UtcNow;
        var expiresAt = issuedAt.AddHours(_opts.AccessTokenLifetimeHours);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString("D")),
            new(JwtRegisteredClaimNames.Jti, jwtId.ToString("D")),
            new(ClaimNick, user.Nickname.Value),
            new(ClaimSid, sessionId.ToString("D"))
        };

        var token = new JwtSecurityToken(
            issuer: _opts.Issuer,
            audience: _opts.Audience,
            claims: claims,
            notBefore: issuedAt,
            expires: expiresAt,
            signingCredentials: _signing);

        var encoded = new JwtSecurityTokenHandler().WriteToken(token);
        return new TokenIssued(encoded, jwtId, expiresAt);
    }

    /// <summary>
    /// Accepts either a base64-encoded key or a raw passphrase. We always run
    /// it through SHA-256 to normalize to exactly 32 bytes (256 bits) — that
    /// way you can store either a base64 secret or just type a strong passphrase
    /// and it still produces a valid HS256 key.
    /// </summary>
    public static byte[] ResolveKey(string secret)
    {
        if (string.IsNullOrEmpty(secret))
            throw new InvalidOperationException("JWT secret is not configured. Set Jwt:SecretKey or env DEEPCHAT_JWT_SECRET.");

        // SHA-256 over the UTF-8 bytes always yields 32 bytes. Even if the user
        // provided a base64 secret, hashing it is fine — we just use the digest.
        return System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(secret));
    }
}
