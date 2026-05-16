using Galileo.Chat.Domain.Entities;
using Galileo.Chat.Domain.Exceptions;
using Galileo.Chat.Domain.Tests.Fakes;
using Galileo.Chat.Domain.UseCases.Auth;
using Galileo.Chat.Domain.ValueObjects;

namespace Galileo.Chat.Domain.Tests.UseCases.Auth;

public sealed class LoginHandlerTests
{
    private readonly FakeUserRepository _users = new();
    private readonly FakeSessionRepository _sessions = new();
    private readonly FakePasswordHasher _hasher = new();
    private readonly FakeClock _clock = new();
    private readonly FakeTokenService _tokens;
    private readonly LoginHandler _handler;

    public LoginHandlerTests()
    {
        _tokens = new FakeTokenService(_clock);
        _handler = new LoginHandler(_users, _sessions, _hasher, _tokens, _clock);
    }

    private async Task<User> SeedUserAsync(string username = "alice", string password = "secret123", bool active = true)
    {
        var user = User.Register(
            Username.Create(username),
            Nickname.Create("Alice"),
            _hasher.Hash(password),
            _clock.UtcNow);
        if (!active) user.Deactivate();
        await _users.AddAsync(user);
        return user;
    }

    [Fact]
    public async Task Successful_login_returns_token_and_user_data()
    {
        var user = await SeedUserAsync();

        var result = await _handler.HandleAsync(new LoginCommand("alice", "secret123", "192.168.1.42"));

        result.UserId.Should().Be(user.Id);
        result.Nickname.Should().Be("Alice");
        result.Token.Should().StartWith("fake-jwt-for-");
        result.ExpiresAt.Should().Be(_clock.UtcNow + _tokens.Lifetime);
    }

    [Fact]
    public async Task Successful_login_persists_session_with_jti_matching_token()
    {
        var user = await SeedUserAsync();

        var result = await _handler.HandleAsync(new LoginCommand("alice", "secret123", "192.168.1.42"));

        _sessions.All.Values.Should().HaveCount(1);
        var session = _sessions.All.Values.Single();
        session.UserId.Should().Be(user.Id);
        session.RemoteIp.Should().Be("192.168.1.42");
        session.JwtId.Should().Be(_tokens.Issued.Single().JwtId);
        session.ExpiresAt.Should().Be(result.ExpiresAt);
    }

    [Fact]
    public async Task Successful_login_records_LastLoginAt_on_user()
    {
        var user = await SeedUserAsync();

        await _handler.HandleAsync(new LoginCommand("alice", "secret123", "192.168.1.42"));

        var stored = await _users.FindByIdAsync(user.Id);
        stored!.LastLoginAt.Should().Be(_clock.UtcNow);
    }

    [Fact]
    public async Task Wrong_password_throws_AuthenticationFailed()
    {
        await SeedUserAsync();

        var act = async () => await _handler.HandleAsync(new LoginCommand("alice", "WRONG", "1.1.1.1"));

        await act.Should().ThrowAsync<AuthenticationFailedException>();
        _sessions.All.Should().BeEmpty();
    }

    [Fact]
    public async Task Unknown_user_throws_AuthenticationFailed_with_same_message()
    {
        var act = async () => await _handler.HandleAsync(new LoginCommand("nobody", "any", "1.1.1.1"));

        // Same exception type and message as wrong-password case — denies user
        // enumeration via response body.
        var ex = (await act.Should().ThrowAsync<AuthenticationFailedException>()).Which;
        ex.Message.Should().Be("Invalid credentials.");
    }

    [Fact]
    public async Task Deactivated_user_cannot_login()
    {
        await SeedUserAsync(active: false);

        var act = async () => await _handler.HandleAsync(new LoginCommand("alice", "secret123", "1.1.1.1"));

        await act.Should().ThrowAsync<AuthenticationFailedException>();
    }

    [Fact]
    public async Task Malformed_username_throws_AuthenticationFailed_not_DomainValidation()
    {
        // Bad-shape input must be funneled into the same generic 401 path.
        var act = async () => await _handler.HandleAsync(new LoginCommand("  ", "x", "1.1.1.1"));

        await act.Should().ThrowAsync<AuthenticationFailedException>();
    }
}
