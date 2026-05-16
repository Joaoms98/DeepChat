using Galileo.Chat.Infrastructure.Auth;

namespace Galileo.Chat.Infrastructure.Tests.Auth;

public sealed class Argon2PasswordHasherTests
{
    private readonly Argon2PasswordHasher _hasher = new();

    [Fact]
    public void Hash_produces_PHC_argon2id_string()
    {
        var hash = _hasher.Hash("secret123");

        hash.Should().StartWith("$argon2id$v=19$m=131072,t=4,p=2$");
        hash.Split('$').Should().HaveCount(6);
    }

    [Fact]
    public void Hash_uses_a_unique_salt_per_call()
    {
        var h1 = _hasher.Hash("secret123");
        var h2 = _hasher.Hash("secret123");
        h1.Should().NotBe(h2);
    }

    [Fact]
    public void Verify_succeeds_for_correct_password()
    {
        var hash = _hasher.Hash("secret123");
        _hasher.Verify("secret123", hash).Should().BeTrue();
    }

    [Fact]
    public void Verify_fails_for_wrong_password()
    {
        var hash = _hasher.Hash("secret123");
        _hasher.Verify("WRONG", hash).Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-hash")]
    [InlineData("$argon2i$v=19$m=131072,t=4,p=2$abc$def")]   // wrong variant
    [InlineData("$argon2id$v=18$m=131072,t=4,p=2$abc$def")]  // wrong version
    [InlineData("$argon2id$v=19$m=131072,p=2$abc$def")]      // missing t
    [InlineData("$argon2id$v=19$m=131072,t=4,p=2$!!!$def")]  // bad b64 salt
    public void Verify_rejects_malformed_or_corrupt_hashes(string hash)
    {
        _hasher.Verify("any", hash).Should().BeFalse();
    }

    [Fact]
    public void Hash_throws_for_empty_password()
    {
        var act = () => _hasher.Hash(string.Empty);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Verify_against_empty_password_returns_false_without_throwing()
    {
        var hash = _hasher.Hash("secret123");
        _hasher.Verify(string.Empty, hash).Should().BeFalse();
    }
}
