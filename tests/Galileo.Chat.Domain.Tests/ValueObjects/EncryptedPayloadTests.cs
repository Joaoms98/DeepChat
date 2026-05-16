using Galileo.Chat.Domain.Exceptions;
using Galileo.Chat.Domain.ValueObjects;

namespace Galileo.Chat.Domain.Tests.ValueObjects;

public class EncryptedPayloadTests
{
    private static readonly byte[] ValidIv = new byte[EncryptedPayload.IvLength];
    private static readonly byte[] ValidTag = new byte[EncryptedPayload.TagLength];
    private static readonly byte[] ValidCipher = new byte[] { 1, 2, 3, 4 };

    [Fact]
    public void Create_succeeds_with_well_shaped_inputs()
    {
        var p = EncryptedPayload.Create(ValidIv, ValidCipher, ValidTag);

        p.Iv.Should().HaveCount(EncryptedPayload.IvLength);
        p.Tag.Should().HaveCount(EncryptedPayload.TagLength);
        p.Ciphertext.Should().Equal(ValidCipher);
        p.TotalBytes.Should().Be(EncryptedPayload.IvLength + ValidCipher.Length + EncryptedPayload.TagLength);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    [InlineData(11)]
    [InlineData(13)]
    [InlineData(16)]
    public void Create_rejects_iv_of_wrong_length(int ivLen)
    {
        var iv = new byte[ivLen];
        var act = () => EncryptedPayload.Create(iv, ValidCipher, ValidTag);
        act.Should().Throw<DomainValidationException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(15)]
    [InlineData(17)]
    [InlineData(32)]
    public void Create_rejects_tag_of_wrong_length(int tagLen)
    {
        var tag = new byte[tagLen];
        var act = () => EncryptedPayload.Create(ValidIv, ValidCipher, tag);
        act.Should().Throw<DomainValidationException>();
    }

    [Fact]
    public void Create_rejects_empty_ciphertext()
    {
        var act = () => EncryptedPayload.Create(ValidIv, Array.Empty<byte>(), ValidTag);
        act.Should().Throw<DomainValidationException>();
    }

    [Fact]
    public void Equals_is_byte_wise_value_based()
    {
        var a = EncryptedPayload.Create(ValidIv, ValidCipher, ValidTag);
        var b = EncryptedPayload.Create(ValidIv, ValidCipher, ValidTag);
        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void Equals_returns_false_for_any_byte_difference()
    {
        var a = EncryptedPayload.Create(ValidIv, new byte[] { 1, 2, 3 }, ValidTag);
        var b = EncryptedPayload.Create(ValidIv, new byte[] { 1, 2, 4 }, ValidTag);
        a.Should().NotBe(b);
    }

    [Fact]
    public void Mutating_input_buffers_after_Create_does_not_affect_the_payload()
    {
        var iv = new byte[EncryptedPayload.IvLength];
        var cipher = new byte[] { 0x10, 0x20, 0x30 };
        var tag = new byte[EncryptedPayload.TagLength];

        var p = EncryptedPayload.Create(iv, cipher, tag);

        iv[0] = 0xFF;
        cipher[0] = 0xFF;
        tag[0] = 0xFF;

        p.IvSpan[0].Should().Be(0);
        p.CiphertextSpan[0].Should().Be(0x10);
        p.TagSpan[0].Should().Be(0);
    }

    [Fact]
    public void Mutating_returned_arrays_does_not_affect_the_payload()
    {
        var p = EncryptedPayload.Create(ValidIv, ValidCipher, ValidTag);

        var ivOut = p.Iv;
        var cipherOut = p.Ciphertext;
        var tagOut = p.Tag;

        ivOut[0] = 0xFF;
        cipherOut[0] = 0xFF;
        tagOut[0] = 0xFF;

        p.IvSpan[0].Should().Be(0);
        p.CiphertextSpan[0].Should().Be(1);
        p.TagSpan[0].Should().Be(0);
    }

    [Fact]
    public void Each_getter_call_returns_a_fresh_clone()
    {
        var p = EncryptedPayload.Create(ValidIv, ValidCipher, ValidTag);

        p.Iv.Should().NotBeSameAs(p.Iv);
        p.Ciphertext.Should().NotBeSameAs(p.Ciphertext);
        p.Tag.Should().NotBeSameAs(p.Tag);
    }
}
