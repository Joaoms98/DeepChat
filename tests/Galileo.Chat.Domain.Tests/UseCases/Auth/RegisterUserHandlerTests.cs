using Galileo.Chat.Domain.Exceptions;
using Galileo.Chat.Domain.Tests.Fakes;
using Galileo.Chat.Domain.UseCases.Auth;
using Galileo.Chat.Domain.ValueObjects;

namespace Galileo.Chat.Domain.Tests.UseCases.Auth;

public sealed class RegisterUserHandlerTests
{
    private readonly FakeUserRepository _users = new();
    private readonly FakePasswordHasher _hasher = new();
    private readonly FakeClock _clock = new();
    private readonly RegisterUserHandler _handler;

    public RegisterUserHandlerTests()
    {
        _handler = new RegisterUserHandler(_users, _hasher, _clock);
    }

    [Fact]
    public async Task Register_creates_user_with_hashed_password()
    {
        var result = await _handler.HandleAsync(new RegisterUserCommand("alice", "Alice", "secret123"));

        result.UserId.Should().NotBe(Guid.Empty);
        result.Username.Should().Be("alice");

        var stored = await _users.FindByIdAsync(result.UserId);
        stored!.PasswordHash.Should().Be("fake:secret123");
        stored.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Register_rejects_short_password()
    {
        var act = async () => await _handler.HandleAsync(new RegisterUserCommand("alice", "Alice", "short"));

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*at least 8 characters*");
    }

    [Fact]
    public async Task Register_rejects_duplicate_username()
    {
        await _handler.HandleAsync(new RegisterUserCommand("alice", "Alice A", "secret123"));

        var act = async () => await _handler.HandleAsync(new RegisterUserCommand("alice", "Alice B", "secret456"));

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*already taken*");
    }

    [Fact]
    public async Task Register_rejects_invalid_username_format()
    {
        var act = async () => await _handler.HandleAsync(new RegisterUserCommand("josé!", "Alice", "secret123"));

        await act.Should().ThrowAsync<DomainValidationException>();
    }
}
