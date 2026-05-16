using Galileo.Chat.Domain.Entities;
using Galileo.Chat.Domain.Enums;
using Galileo.Chat.Domain.ValueObjects;

namespace Galileo.Chat.Domain.Tests.Entities;

/// <summary>
/// Rehydrate is the only path that writes to private setters without re-running invariants.
/// EF Core / migrations / serialization will all go through these. A bug here means the
/// persistence layer can quietly produce entities in states the constructors would reject —
/// these tests pin the contract.
/// </summary>
public sealed class RehydrateTests
{
    private static readonly DateTime Now = new(2026, 5, 15, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void User_Rehydrate_preserves_all_fields_including_LastLoginAt_and_IsActive()
    {
        var id = Guid.NewGuid();
        var lastLogin = Now.AddHours(-3);

        var user = User.Rehydrate(
            id: id,
            username: Username.Create("alice"),
            nickname: Nickname.Create("Alice"),
            passwordHash: "$argon2id$stored",
            createdAt: Now.AddDays(-30),
            lastLoginAt: lastLogin,
            isActive: false);

        user.Id.Should().Be(id);
        user.Username.Value.Should().Be("alice");
        user.Nickname.Value.Should().Be("Alice");
        user.PasswordHash.Should().Be("$argon2id$stored");
        user.CreatedAt.Should().Be(Now.AddDays(-30));
        user.LastLoginAt.Should().Be(lastLogin);
        user.IsActive.Should().BeFalse();
    }

    [Fact]
    public void User_Rehydrate_skips_password_hash_validation()
    {
        // Register would reject empty hash; Rehydrate must accept whatever the DB has,
        // so persistence corruption surfaces during query rather than swallowed.
        var act = () => User.Rehydrate(
            id: Guid.NewGuid(),
            username: Username.Create("alice"),
            nickname: Nickname.Create("Alice"),
            passwordHash: "",
            createdAt: Now,
            lastLoginAt: null,
            isActive: true);

        act.Should().NotThrow();
    }

    [Fact]
    public void Room_Rehydrate_preserves_id_name_salt_and_created_at()
    {
        var id = Guid.NewGuid();
        var salt = new byte[Room.SaltLength];
        Array.Fill(salt, (byte)0x77);

        var room = Room.Rehydrate(id, RoomName.Create("backend"), salt, Now);

        room.Id.Should().Be(id);
        room.Name.Value.Should().Be("backend");
        room.Salt.Should().Equal(salt);
        room.CreatedAt.Should().Be(Now);
    }

    [Fact]
    public void Session_Rehydrate_preserves_RevokedAt_and_does_not_re_issue()
    {
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var jwtId = Guid.NewGuid();
        var revoked = Now.AddMinutes(5);

        var session = Session.Rehydrate(
            id: id,
            userId: userId,
            jwtId: jwtId,
            issuedAt: Now,
            expiresAt: Now.AddHours(8),
            revokedAt: revoked,
            remoteIp: "192.168.1.42");

        session.Id.Should().Be(id);
        session.UserId.Should().Be(userId);
        session.JwtId.Should().Be(jwtId);
        session.IssuedAt.Should().Be(Now);
        session.ExpiresAt.Should().Be(Now.AddHours(8));
        session.RevokedAt.Should().Be(revoked);
        session.RemoteIp.Should().Be("192.168.1.42");
        session.IsActive(Now).Should().BeFalse();
    }

    [Fact]
    public void Message_Rehydrate_preserves_kind_and_optional_room_or_recipient()
    {
        var payload = EncryptedPayload.Create(
            new byte[EncryptedPayload.IvLength],
            new byte[] { 1, 2, 3 },
            new byte[EncryptedPayload.TagLength]);

        var broadcast = Message.Rehydrate(
            id: Guid.NewGuid(),
            senderId: Guid.NewGuid(),
            roomId: Guid.NewGuid(),
            recipientId: null,
            kind: MessageKind.Broadcast,
            payload: payload,
            createdAt: Now);

        broadcast.RoomId.Should().NotBeNull();
        broadcast.RecipientId.Should().BeNull();
        broadcast.Kind.Should().Be(MessageKind.Broadcast);
        broadcast.CreatedAt.Should().Be(Now);

        var direct = Message.Rehydrate(
            id: Guid.NewGuid(),
            senderId: Guid.NewGuid(),
            roomId: null,
            recipientId: Guid.NewGuid(),
            kind: MessageKind.Direct,
            payload: payload,
            createdAt: Now);

        direct.RoomId.Should().BeNull();
        direct.RecipientId.Should().NotBeNull();
        direct.Kind.Should().Be(MessageKind.Direct);
    }
}
