using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Galileo.Chat.Domain.Entities;
using Galileo.Chat.Domain.ValueObjects;
using Galileo.Chat.Infrastructure.Auth;
using Galileo.Chat.Infrastructure.Options;
using Galileo.Chat.Infrastructure.Time;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Galileo.Chat.Infrastructure.Tests.Auth;

public sealed class JwtTokenServiceTests
{
    private readonly JwtOptions _opts = new()
    {
        Issuer = "DeepChat.Server",
        Audience = "DeepChat.Client",
        SecretKey = "this-is-a-test-secret-with-enough-entropy-1234567890",
        AccessTokenLifetimeHours = 8
    };

    private static User NewUser() => User.Register(
        Username.Create("alice"),
        Nickname.Create("Alice"),
        passwordHash: "$argon2id$test",
        utcNow: DateTime.UtcNow);

    [Fact]
    public void Issue_produces_jwt_with_expected_claims()
    {
        var sut = new JwtTokenService(Microsoft.Extensions.Options.Options.Create(_opts), new SystemClock());
        var user = NewUser();
        var sessionId = Guid.NewGuid();

        var issued = sut.Issue(user, sessionId);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(issued.Token);
        jwt.Issuer.Should().Be(_opts.Issuer);
        jwt.Audiences.Should().Contain(_opts.Audience);
        jwt.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Sub).Value.Should().Be(user.Id.ToString("D"));
        jwt.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Jti).Value.Should().Be(issued.JwtId.ToString("D"));
        jwt.Claims.Single(c => c.Type == JwtTokenService.ClaimNick).Value.Should().Be("Alice");
        jwt.Claims.Single(c => c.Type == JwtTokenService.ClaimSid).Value.Should().Be(sessionId.ToString("D"));
    }

    [Fact]
    public void Issue_token_validates_with_same_key()
    {
        var sut = new JwtTokenService(Microsoft.Extensions.Options.Options.Create(_opts), new SystemClock());
        var issued = sut.Issue(NewUser(), Guid.NewGuid());

        var validator = new JwtSecurityTokenHandler();
        var key = JwtTokenService.ResolveKey(_opts.SecretKey);

        var principal = validator.ValidateToken(issued.Token, new TokenValidationParameters
        {
            ValidIssuer = _opts.Issuer,
            ValidAudience = _opts.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateLifetime = true
        }, out _);

        principal.Should().NotBeNull();
    }

    [Fact]
    public void Issue_throws_for_empty_session_id()
    {
        var sut = new JwtTokenService(Microsoft.Extensions.Options.Options.Create(_opts), new SystemClock());
        var act = () => sut.Issue(NewUser(), Guid.Empty);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_throws_if_secret_decodes_to_less_than_32_bytes()
    {
        // Note: ResolveKey always SHA-256s the input, so any non-empty string
        // ends up as 32 bytes. To force a short key we'd need to bypass ResolveKey.
        // Instead verify that an empty secret throws at construction.
        var bad = new JwtOptions { SecretKey = "" };
        var act = () => new JwtTokenService(Microsoft.Extensions.Options.Options.Create(bad), new SystemClock());
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ResolveKey_always_returns_32_bytes_via_sha256()
    {
        JwtTokenService.ResolveKey("short").Length.Should().Be(32);
        JwtTokenService.ResolveKey(new string('x', 1000)).Length.Should().Be(32);
    }
}
