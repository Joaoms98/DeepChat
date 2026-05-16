using Galileo.Chat.Domain.Exceptions;
using Galileo.Chat.Domain.ValueObjects;

namespace Galileo.Chat.Domain.Tests.ValueObjects;

public class UsernameTests
{
    [Theory]
    [InlineData("alice", "alice")]
    [InlineData("ALICE", "alice")]
    [InlineData("  alice  ", "alice")]
    [InlineData("alice.silva", "alice.silva")]
    [InlineData("alice_silva", "alice_silva")]
    [InlineData("alice-silva", "alice-silva")]
    [InlineData("user123", "user123")]
    public void Create_normalizes_to_lowercase_and_trims(string input, string expected)
    {
        var u = Username.Create(input);
        u.Value.Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_rejects_null_or_whitespace(string? input)
    {
        var act = () => Username.Create(input!);
        act.Should().Throw<DomainValidationException>();
    }

    [Theory]
    [InlineData("ab")]                                  // too short
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]   // 33 chars: too long
    public void Create_rejects_out_of_range_length(string input)
    {
        var act = () => Username.Create(input);
        act.Should().Throw<DomainValidationException>();
    }

    [Theory]
    [InlineData("josé")]        // diacritic
    [InlineData("alice silva")]  // space
    [InlineData("alice@host")]   // @
    [InlineData("alice!")]
    [InlineData(".alice")]       // cannot start with separator
    [InlineData("-alice")]
    public void Create_rejects_invalid_characters(string input)
    {
        var act = () => Username.Create(input);
        act.Should().Throw<DomainValidationException>();
    }

    [Fact]
    public void Equality_is_value_based()
    {
        var a = Username.Create("Alice");
        var b = Username.Create("alice");
        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void ImplicitStringCast_returns_inner_value()
    {
        var u = Username.Create("alice");
        string s = u;
        s.Should().Be("alice");
    }
}
