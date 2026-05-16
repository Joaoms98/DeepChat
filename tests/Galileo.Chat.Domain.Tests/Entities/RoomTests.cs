using Galileo.Chat.Domain.Entities;
using Galileo.Chat.Domain.Exceptions;
using Galileo.Chat.Domain.ValueObjects;

namespace Galileo.Chat.Domain.Tests.Entities;

public class RoomTests
{
    private static byte[] Salt(byte fill = 0xAB)
    {
        var s = new byte[Room.SaltLength];
        Array.Fill(s, fill);
        return s;
    }

    [Fact]
    public void Create_initializes_room_with_cloned_salt()
    {
        var salt = Salt();
        var r = Room.Create(RoomName.Create("backend"), salt, DateTime.UtcNow);

        r.Id.Should().NotBe(Guid.Empty);
        r.Name.Value.Should().Be("backend");
        r.Salt.Should().Equal(salt);
        r.Salt.Should().NotBeSameAs(salt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    [InlineData(15)]
    [InlineData(17)]
    [InlineData(32)]
    public void Create_rejects_invalid_salt_length(int len)
    {
        var act = () => Room.Create(RoomName.Create("backend"), new byte[len], DateTime.UtcNow);
        act.Should().Throw<DomainValidationException>();
    }

    [Fact]
    public void RotateSalt_replaces_with_new_salt()
    {
        var r = Room.Create(RoomName.Create("backend"), Salt(0x01), DateTime.UtcNow);
        var newSalt = Salt(0x02);

        r.RotateSalt(newSalt);

        r.Salt.Should().Equal(newSalt);
    }

    [Fact]
    public void RotateSalt_rejects_identical_salt()
    {
        var salt = Salt();
        var r = Room.Create(RoomName.Create("backend"), salt, DateTime.UtcNow);
        var act = () => r.RotateSalt(salt);
        act.Should().Throw<DomainException>();
    }
}
