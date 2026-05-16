using Galileo.Chat.Crypto.Random;

namespace Galileo.Chat.Crypto.Tests.Random;

public sealed class SecureRandomTests
{
    [Fact]
    public void GetBytes_returns_array_of_requested_length()
    {
        SecureRandom.GetBytes(1).Length.Should().Be(1);
        SecureRandom.GetBytes(12).Length.Should().Be(12);
        SecureRandom.GetBytes(64).Length.Should().Be(64);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void GetBytes_rejects_non_positive_length(int len)
    {
        Action act = () => SecureRandom.GetBytes(len);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void GetBytes_returns_unique_outputs_across_many_calls()
    {
        // Sanity check on RNG entropy. 10k 12-byte samples must be unique
        // (collision probability is well below 2^-50).
        const int Samples = 10_000;
        var seen = new HashSet<string>(Samples);
        for (var i = 0; i < Samples; i++)
        {
            var iv = SecureRandom.GetBytes(12);
            seen.Add(Convert.ToHexString(iv)).Should().BeTrue("IV reuse breaks AES-GCM security");
        }
        seen.Count.Should().Be(Samples);
    }

    [Fact]
    public void Fill_throws_for_empty_span()
    {
        Action act = () => SecureRandom.Fill(Span<byte>.Empty);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void NewIv_and_NewSalt_have_canonical_lengths()
    {
        SecureRandom.NewIv().Length.Should().Be(12);
        SecureRandom.NewSalt().Length.Should().Be(16);
    }
}
