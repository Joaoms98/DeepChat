using Galileo.Chat.Domain.Exceptions;
using Galileo.Chat.Domain.ValueObjects;

namespace Galileo.Chat.Domain.Tests.ValueObjects;

public class RoomNameTests
{
    [Theory]
    [InlineData("backend", "backend")]
    [InlineData("BACKEND", "backend")]
    [InlineData("  team-alpha  ", "team-alpha")]
    [InlineData("ops_room1", "ops_room1")]
    public void Create_normalizes_to_lowercase_and_trims(string input, string expected)
    {
        var r = RoomName.Create(input);
        r.Value.Should().Be(expected);
    }

    [Theory]
    [InlineData("a")]                                   // too short (min 2)
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]   // 33 chars
    public void Create_rejects_out_of_range_length(string input)
    {
        var act = () => RoomName.Create(input);
        act.Should().Throw<DomainValidationException>();
    }

    [Theory]
    [InlineData("-room")]    // cannot start with dash
    [InlineData("_room")]
    [InlineData("room.x")]   // dot not allowed in room
    [InlineData("room x")]
    [InlineData("sala#1")]
    public void Create_rejects_invalid_characters(string input)
    {
        var act = () => RoomName.Create(input);
        act.Should().Throw<DomainValidationException>();
    }
}
