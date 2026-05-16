using Galileo.Chat.Crypto.Exceptions;
using Galileo.Chat.Crypto.Kdf;
using Galileo.Chat.Crypto.Random;

namespace Galileo.Chat.Crypto.Tests.Kdf;

public sealed class Argon2KeyDerivationTests
{
    // Use the cheap Test profile so the suite stays under ~1s.
    private readonly Argon2KeyDerivation _kdf = new(Argon2Parameters.Test);

    private static readonly byte[] Salt = SecureRandom.NewSalt();

    [Fact]
    public void DeriveKey_is_deterministic_for_same_inputs()
    {
        var k1 = _kdf.DeriveKey("correct horse battery staple", Salt);
        var k2 = _kdf.DeriveKey("correct horse battery staple", Salt);

        k1.Should().Equal(k2);
    }

    [Fact]
    public void DeriveKey_returns_default_32_bytes()
    {
        var key = _kdf.DeriveKey("p", Salt);
        key.Should().HaveCount(Argon2KeyDerivation.DefaultKeyLength);
    }

    [Theory]
    [InlineData(16)]
    [InlineData(24)]
    [InlineData(32)]
    [InlineData(48)]
    [InlineData(64)]
    public void DeriveKey_honors_explicit_key_length(int len)
    {
        var key = _kdf.DeriveKey("p", Salt, len);
        key.Should().HaveCount(len);
    }

    [Fact]
    public void DeriveKey_with_different_passphrase_produces_different_key()
    {
        var k1 = _kdf.DeriveKey("alpha", Salt);
        var k2 = _kdf.DeriveKey("beta", Salt);
        k1.Should().NotEqual(k2);
    }

    [Fact]
    public void DeriveKey_with_different_salt_produces_different_key()
    {
        var saltA = SecureRandom.NewSalt();
        var saltB = SecureRandom.NewSalt();
        saltA.Should().NotEqual(saltB);

        var k1 = _kdf.DeriveKey("same passphrase", saltA);
        var k2 = _kdf.DeriveKey("same passphrase", saltB);
        k1.Should().NotEqual(k2);
    }

    [Fact]
    public void DeriveKey_with_different_parameters_produces_different_key()
    {
        var test = new Argon2KeyDerivation(Argon2Parameters.Test);
        var stronger = new Argon2KeyDerivation(new Argon2Parameters(Iterations: 2, MemoryKb: 8 * 1024, Parallelism: 1));

        var k1 = test.DeriveKey("p", Salt);
        var k2 = stronger.DeriveKey("p", Salt);

        k1.Should().NotEqual(k2);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void DeriveKey_rejects_empty_or_null_passphrase(string? passphrase)
    {
        var act = () => _kdf.DeriveKey(passphrase!, Salt);
        act.Should().Throw<CryptoException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    public void DeriveKey_rejects_short_salt(int saltLen)
    {
        var act = () => _kdf.DeriveKey("p", new byte[saltLen]);
        act.Should().Throw<CryptoException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(15)]
    [InlineData(65)]
    [InlineData(128)]
    public void DeriveKey_rejects_invalid_key_length(int len)
    {
        var act = () => _kdf.DeriveKey("p", Salt, len);
        act.Should().Throw<CryptoException>();
    }

    [Fact]
    public void Constructor_validates_parameters()
    {
        Action a1 = () => _ = new Argon2KeyDerivation(new Argon2Parameters(0, 8 * 1024, 1));
        Action a2 = () => _ = new Argon2KeyDerivation(new Argon2Parameters(1, 100, 1));   // <1 MiB
        Action a3 = () => _ = new Argon2KeyDerivation(new Argon2Parameters(1, 8 * 1024, 0));

        a1.Should().Throw<ArgumentOutOfRangeException>();
        a2.Should().Throw<ArgumentOutOfRangeException>();
        a3.Should().Throw<ArgumentOutOfRangeException>();
    }
}
