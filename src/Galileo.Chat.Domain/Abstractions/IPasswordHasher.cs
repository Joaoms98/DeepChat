namespace Galileo.Chat.Domain.Abstractions;

public interface IPasswordHasher
{
    /// <summary>Returns a self-contained hash string (PHC format) suitable for storing in the User table.</summary>
    string Hash(string password);

    /// <summary>Constant-time verification — never short-circuit on length or first-byte mismatches.</summary>
    bool Verify(string password, string storedHash);
}
