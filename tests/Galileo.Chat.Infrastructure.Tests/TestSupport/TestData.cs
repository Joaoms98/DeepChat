using Galileo.Chat.Domain.Entities;
using Galileo.Chat.Domain.ValueObjects;

namespace Galileo.Chat.Infrastructure.Tests.TestSupport;

internal static class TestData
{
    public static readonly DateTime FixedNow = new(2026, 5, 15, 12, 0, 0, DateTimeKind.Utc);

    public static User NewUser(string username = "alice", string nick = "Alice", DateTime? at = null) =>
        User.Register(
            Username.Create(username),
            Nickname.Create(nick),
            passwordHash: "$argon2id$test",
            utcNow: at ?? FixedNow);

    public static Room NewRoom(string name = "backend", DateTime? at = null)
    {
        var salt = new byte[Room.SaltLength];
        Array.Fill(salt, (byte)0xAB);
        return Room.Create(RoomName.Create(name), salt, at ?? FixedNow);
    }

    public static EncryptedPayload NewPayload(byte cipherFill = 0x42)
    {
        var iv = new byte[EncryptedPayload.IvLength];
        Array.Fill(iv, (byte)0x11);
        var cipher = new byte[16];
        Array.Fill(cipher, cipherFill);
        var tag = new byte[EncryptedPayload.TagLength];
        Array.Fill(tag, (byte)0x99);
        return EncryptedPayload.Create(iv, cipher, tag);
    }
}
