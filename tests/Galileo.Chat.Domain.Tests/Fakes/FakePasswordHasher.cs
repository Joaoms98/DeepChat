using Galileo.Chat.Domain.Abstractions;

namespace Galileo.Chat.Domain.Tests.Fakes;

/// <summary>Trivial reversible "hasher" — DO NOT use outside tests.</summary>
public sealed class FakePasswordHasher : IPasswordHasher
{
    public string Hash(string password) => "fake:" + password;

    public bool Verify(string password, string storedHash) =>
        storedHash == "fake:" + password;
}
