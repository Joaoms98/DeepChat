using Galileo.Chat.Domain.Entities;
using Galileo.Chat.Domain.Enums;
using Galileo.Chat.Domain.Exceptions;
using Galileo.Chat.Domain.ValueObjects;

namespace Galileo.Chat.Domain.Tests.Entities;

public sealed class MessageTests
{
    private static readonly DateTime Now = new(2026, 5, 15, 12, 0, 0, DateTimeKind.Utc);

    private static EncryptedPayload Payload() => EncryptedPayload.Create(
        new byte[EncryptedPayload.IvLength],
        new byte[] { 1, 2, 3 },
        new byte[EncryptedPayload.TagLength]);

    [Fact]
    public void CreateBroadcast_assigns_room_and_no_recipient()
    {
        var sender = Guid.NewGuid();
        var room = Guid.NewGuid();
        var msg = Message.CreateBroadcast(sender, room, Payload(), Now);

        msg.Id.Should().NotBe(Guid.Empty);
        msg.SenderId.Should().Be(sender);
        msg.RoomId.Should().Be(room);
        msg.RecipientId.Should().BeNull();
        msg.Kind.Should().Be(MessageKind.Broadcast);
        msg.CreatedAt.Should().Be(Now);
    }

    [Fact]
    public void CreateBroadcast_rejects_empty_sender_or_room()
    {
        Action a1 = () => Message.CreateBroadcast(Guid.Empty, Guid.NewGuid(), Payload(), Now);
        Action a2 = () => Message.CreateBroadcast(Guid.NewGuid(), Guid.Empty, Payload(), Now);
        a1.Should().Throw<DomainException>();
        a2.Should().Throw<DomainException>();
    }

    [Fact]
    public void CreateDirect_requires_distinct_sender_and_recipient()
    {
        var same = Guid.NewGuid();
        Action act = () => Message.CreateDirect(same, same, Payload(), Now);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void CreateDirect_assigns_recipient_and_no_room()
    {
        var sender = Guid.NewGuid();
        var recipient = Guid.NewGuid();
        var msg = Message.CreateDirect(sender, recipient, Payload(), Now);

        msg.Kind.Should().Be(MessageKind.Direct);
        msg.RecipientId.Should().Be(recipient);
        msg.RoomId.Should().BeNull();
    }

    [Fact]
    public void CreateSystem_uses_empty_sender()
    {
        var msg = Message.CreateSystem(Guid.NewGuid(), Payload(), Now);
        msg.SenderId.Should().Be(Guid.Empty);
        msg.Kind.Should().Be(MessageKind.System);
    }

    [Fact]
    public void IsExpired_returns_true_after_ttl()
    {
        var msg = Message.CreateBroadcast(Guid.NewGuid(), Guid.NewGuid(), Payload(), Now);
        msg.IsExpired(Now.AddHours(25), TimeSpan.FromHours(24)).Should().BeTrue();
        msg.IsExpired(Now.AddHours(23), TimeSpan.FromHours(24)).Should().BeFalse();
    }
}
