namespace Galileo.Chat.Crypto.Kdf;

/// <summary>
/// Argon2id tuning profile.
///
/// MemoryKb is in **kibibytes** (Konscious convention): 131072 == 128 MiB.
///
/// Recommendation cheat-sheet (RFC 9106 §4):
///  - Memory-constrained:  Iterations >= 3, Memory  64 MiB, Parallelism 1
///  - Recommended default: Iterations >= 1, Memory 2 GiB, Parallelism 4   (we don't go that high)
///  - Our Interactive profile (suitable for ~250ms login on a typical desktop):
///      Iterations 4, Memory 128 MiB, Parallelism 2
///  - Our Test profile keeps tests fast (DO NOT use in production):
///      Iterations 1, Memory 8 MiB, Parallelism 1
/// </summary>
public sealed record Argon2Parameters(int Iterations, int MemoryKb, int Parallelism)
{
    public static Argon2Parameters Interactive { get; } = new(
        Iterations: 4,
        MemoryKb: 128 * 1024,
        Parallelism: 2);

    public static Argon2Parameters Sensitive { get; } = new(
        Iterations: 6,
        MemoryKb: 256 * 1024,
        Parallelism: 4);

    public static Argon2Parameters Test { get; } = new(
        Iterations: 1,
        MemoryKb: 8 * 1024,
        Parallelism: 1);

    public void Validate()
    {
        if (Iterations < 1)
            throw new ArgumentOutOfRangeException(nameof(Iterations), "Must be >= 1.");
        if (MemoryKb < 1024)
            throw new ArgumentOutOfRangeException(nameof(MemoryKb), "Must be >= 1024 KiB (1 MiB).");
        if (Parallelism < 1)
            throw new ArgumentOutOfRangeException(nameof(Parallelism), "Must be >= 1.");
    }
}
