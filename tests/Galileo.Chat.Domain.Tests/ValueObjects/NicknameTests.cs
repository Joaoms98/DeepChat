using Galileo.Chat.Domain.Exceptions;
using Galileo.Chat.Domain.ValueObjects;

namespace Galileo.Chat.Domain.Tests.ValueObjects;

public class NicknameTests
{
    [Theory]
    [InlineData("Alice", "Alice")]
    [InlineData("  Alice  ", "Alice")]
    [InlineData("user 1", "user 1")]
    [InlineData("X", "X")]
    public void Create_trims_and_preserves_original_case_and_unicode(string input, string expected)
    {
        var n = Nickname.Create(input);
        n.Value.Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_rejects_null_or_whitespace(string? input)
    {
        var act = () => Nickname.Create(input!);
        act.Should().Throw<DomainValidationException>();
    }

    [Fact]
    public void Create_rejects_out_of_range_length()
    {
        var tooLong = new string('a', Nickname.MaxLength + 1);
        var act = () => Nickname.Create(tooLong);
        act.Should().Throw<DomainValidationException>();
    }

    [Theory]
    [InlineData("alice")]   // BEL
    [InlineData("alice\nname")]   // LF
    [InlineData("alice\tname")]   // TAB
    public void Create_rejects_control_characters(string input)
    {
        var act = () => Nickname.Create(input);
        act.Should().Throw<DomainValidationException>();
    }
}
