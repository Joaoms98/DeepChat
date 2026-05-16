using Galileo.Chat.Domain.Entities;
using Galileo.Chat.Domain.Exceptions;
using Galileo.Chat.Domain.ValueObjects;

namespace Galileo.Chat.Domain.Tests.Entities;

public class UserTests
{
    private static User NewUser() =>
        User.Register(
            Username.Create("alice"),
            Nickname.Create("Alice"),
            passwordHash: "$argon2id$v=19$m=131072,t=4,p=2$abc$def",
            utcNow: DateTime.UtcNow);

    [Fact]
    public void Register_initializes_invariants()
    {
        var u = NewUser();
        u.Id.Should().NotBe(Guid.Empty);
        u.IsActive.Should().BeTrue();
        u.LastLoginAt.Should().BeNull();
    }

    [Fact]
    public void Register_rejects_empty_password_hash()
    {
        var act = () => User.Register(
            Username.Create("alice"),
            Nickname.Create("Alice"),
            passwordHash: "",
            utcNow: DateTime.UtcNow);
        act.Should().Throw<DomainValidationException>();
    }

    [Fact]
    public void RecordLogin_updates_LastLoginAt()
    {
        var u = NewUser();
        var ts = DateTime.UtcNow.AddHours(1);
        u.RecordLogin(ts);
        u.LastLoginAt.Should().Be(ts);
    }

    [Fact]
    public void RecordLogin_throws_for_deactivated_user()
    {
        var u = NewUser();
        u.Deactivate();
        var act = () => u.RecordLogin(DateTime.UtcNow);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void ChangeNickname_replaces_value()
    {
        var u = NewUser();
        u.ChangeNickname(Nickname.Create("Alice da Silva"));
        u.Nickname.Value.Should().Be("Alice da Silva");
    }

    [Fact]
    public void ChangeNickname_throws_for_deactivated_user()
    {
        var u = NewUser();
        u.Deactivate();
        var act = () => u.ChangeNickname(Nickname.Create("X"));
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void ChangePassword_replaces_hash_and_rejects_empty()
    {
        var u = NewUser();
        u.ChangePassword("$argon2id$v=19$m=131072,t=4,p=2$xyz$abc");
        u.PasswordHash.Should().StartWith("$argon2id$");

        var act = () => u.ChangePassword("");
        act.Should().Throw<DomainValidationException>();
    }
}
