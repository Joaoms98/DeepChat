using System.Security.Cryptography;
using System.Text;
using Galileo.Chat.Domain.Abstractions;
using Konscious.Security.Cryptography;

namespace Galileo.Chat.Infrastructure.Auth;

/// <summary>
/// Argon2id password hasher producing PHC-format strings:
/// <c>$argon2id$v=19$m=131072,t=4,p=2$&lt;b64salt&gt;$&lt;b64hash&gt;</c>
///
/// Parameters match the "Interactive" Argon2 profile (≈250ms on a typical desktop).
/// Verify is constant-time and resistant to timing attacks.
/// </summary>
public sealed class Argon2PasswordHasher : IPasswordHasher
{
    private const int SaltLength = 16;
    private const int HashLength = 32;
    private const int Memory = 128 * 1024;     // 128 MiB
    private const int Iterations = 4;
    private const int Parallelism = 2;

    public string Hash(string password)
    {
        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("Password must not be empty.", nameof(password));

        var salt = RandomNumberGenerator.GetBytes(SaltLength);
        var hash = ComputeHash(password, salt);

        return string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"$argon2id$v=19$m={Memory},t={Iterations},p={Parallelism}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}");
    }

    public bool Verify(string password, string storedHash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(storedHash))
            return false;

        if (!TryParse(storedHash, out var memory, out var iterations, out var parallelism, out var salt, out var expected))
            return false;

        var passwordBytes = Encoding.UTF8.GetBytes(password);
        try
        {
            using var argon2 = new Argon2id(passwordBytes)
            {
                Salt = salt,
                MemorySize = memory,
                Iterations = iterations,
                DegreeOfParallelism = parallelism
            };
            var actual = argon2.GetBytes(expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
        }
    }

    private static byte[] ComputeHash(string password, byte[] salt)
    {
        var passwordBytes = Encoding.UTF8.GetBytes(password);
        try
        {
            using var argon2 = new Argon2id(passwordBytes)
            {
                Salt = salt,
                MemorySize = Memory,
                Iterations = Iterations,
                DegreeOfParallelism = Parallelism
            };
            return argon2.GetBytes(HashLength);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
        }
    }

    private static bool TryParse(
        string phc,
        out int memory, out int iterations, out int parallelism,
        out byte[] salt, out byte[] hash)
    {
        memory = iterations = parallelism = 0;
        salt = hash = Array.Empty<byte>();

        // Format: $argon2id$v=19$m=131072,t=4,p=2$<b64salt>$<b64hash>
        var parts = phc.Split('$');
        if (parts.Length != 6) return false;
        if (parts[1] != "argon2id") return false;
        if (parts[2] != "v=19") return false;

        var paramParts = parts[3].Split(',');
        if (paramParts.Length != 3) return false;

        if (!TryParseKv(paramParts[0], "m", out memory)) return false;
        if (!TryParseKv(paramParts[1], "t", out iterations)) return false;
        if (!TryParseKv(paramParts[2], "p", out parallelism)) return false;

        try
        {
            salt = Convert.FromBase64String(parts[4]);
            hash = Convert.FromBase64String(parts[5]);
        }
        catch (FormatException)
        {
            return false;
        }

        return salt.Length > 0 && hash.Length > 0;
    }

    private static bool TryParseKv(string kv, string expectedKey, out int value)
    {
        value = 0;
        var eq = kv.IndexOf('=');
        if (eq <= 0) return false;
        if (kv[..eq] != expectedKey) return false;
        return int.TryParse(kv.AsSpan(eq + 1), System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture, out value);
    }
}
