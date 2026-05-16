using System.Text;
using Galileo.Chat.Crypto.Aes;
using Galileo.Chat.Crypto.Exceptions;
using Galileo.Chat.Crypto.Random;

namespace Galileo.Chat.Crypto.Tests.Aes;

public sealed class AesGcmCipherTests
{
    private readonly AesGcmCipher _cipher = new();

    private static byte[] NewKey() => SecureRandom.GetBytes(AesGcmCipher.KeySize);

    private static byte[] Bytes(string s) => Encoding.UTF8.GetBytes(s);

    [Fact]
    public void Encrypt_then_Decrypt_recovers_the_original_plaintext()
    {
        var key = NewKey();
        var plaintext = Bytes("olá, mundo — privado");

        var envelope = _cipher.Encrypt(plaintext, key);
        var roundTripped = _cipher.Decrypt(envelope, key);

        roundTripped.Should().Equal(plaintext);
    }

    [Fact]
    public void Encrypt_produces_envelope_with_correct_iv_and_tag_sizes()
    {
        var envelope = _cipher.Encrypt(Bytes("x"), NewKey());

        envelope.Iv.Should().HaveCount(AesGcmCipher.IvSize);
        envelope.Tag.Should().HaveCount(AesGcmCipher.TagSize);
        envelope.Ciphertext.Should().HaveCount(1);
    }

    [Fact]
    public void Encrypt_supports_empty_plaintext()
    {
        var key = NewKey();
        var envelope = _cipher.Encrypt(ReadOnlySpan<byte>.Empty, key);
        envelope.Ciphertext.Should().BeEmpty();
        envelope.Tag.Should().HaveCount(AesGcmCipher.TagSize);

        var pt = _cipher.Decrypt(envelope, key);
        pt.Should().BeEmpty();
    }

    [Fact]
    public void Decrypt_with_wrong_key_throws_DecryptionFailedException()
    {
        var envelope = _cipher.Encrypt(Bytes("secret"), NewKey());
        var wrongKey = NewKey();

        var act = () => _cipher.Decrypt(envelope, wrongKey);

        act.Should().Throw<DecryptionFailedException>();
    }

    [Fact]
    public void Decrypt_detects_ciphertext_tampering()
    {
        var key = NewKey();
        var envelope = _cipher.Encrypt(Bytes("hello"), key);

        envelope.Ciphertext[0] ^= 0x01;

        var act = () => _cipher.Decrypt(envelope, key);
        act.Should().Throw<DecryptionFailedException>();
    }

    [Fact]
    public void Decrypt_detects_tag_tampering()
    {
        var key = NewKey();
        var envelope = _cipher.Encrypt(Bytes("hello"), key);

        envelope.Tag[0] ^= 0x01;

        var act = () => _cipher.Decrypt(envelope, key);
        act.Should().Throw<DecryptionFailedException>();
    }

    [Fact]
    public void Decrypt_detects_iv_tampering()
    {
        var key = NewKey();
        var envelope = _cipher.Encrypt(Bytes("hello"), key);

        envelope.Iv[0] ^= 0x01;

        var act = () => _cipher.Decrypt(envelope, key);
        act.Should().Throw<DecryptionFailedException>();
    }

    [Fact]
    public void Encrypt_with_AAD_round_trips_when_AAD_matches()
    {
        var key = NewKey();
        var aad = Bytes("room=backend;sender=alice");
        var plaintext = Bytes("payload");

        var envelope = _cipher.Encrypt(plaintext, key, aad);
        var pt = _cipher.Decrypt(envelope, key, aad);

        pt.Should().Equal(plaintext);
    }

    [Fact]
    public void Decrypt_fails_when_AAD_differs()
    {
        var key = NewKey();
        var envelope = _cipher.Encrypt(Bytes("payload"), key, Bytes("room=backend"));

        var act = () => _cipher.Decrypt(envelope, key, Bytes("room=ops"));
        act.Should().Throw<DecryptionFailedException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(15)]
    [InlineData(16)]   // AES-128 — too short for our 256-bit contract
    [InlineData(24)]   // AES-192
    [InlineData(31)]
    [InlineData(33)]
    [InlineData(64)]
    public void Encrypt_throws_for_wrong_key_length(int keyLen)
    {
        var key = new byte[keyLen];
        var act = () => _cipher.Encrypt(Bytes("x"), key);
        act.Should().Throw<InvalidKeyLengthException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(16)]
    [InlineData(24)]
    [InlineData(33)]
    public void Decrypt_throws_for_wrong_key_length(int keyLen)
    {
        var envelope = _cipher.Encrypt(Bytes("x"), NewKey());
        var act = () => _cipher.Decrypt(envelope, new byte[keyLen]);
        act.Should().Throw<InvalidKeyLengthException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    [InlineData(11)]
    [InlineData(13)]
    [InlineData(16)]
    public void Decrypt_rejects_envelope_with_invalid_iv_size(int ivLen)
    {
        var malformed = new EncryptedEnvelope(
            Iv: new byte[ivLen],
            Ciphertext: new byte[] { 1 },
            Tag: new byte[AesGcmCipher.TagSize]);
        var act = () => _cipher.Decrypt(malformed, NewKey());
        act.Should().Throw<DecryptionFailedException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(15)]
    [InlineData(17)]
    [InlineData(32)]
    public void Decrypt_rejects_envelope_with_invalid_tag_size(int tagLen)
    {
        var malformed = new EncryptedEnvelope(
            Iv: new byte[AesGcmCipher.IvSize],
            Ciphertext: new byte[] { 1 },
            Tag: new byte[tagLen]);
        var act = () => _cipher.Decrypt(malformed, NewKey());
        act.Should().Throw<DecryptionFailedException>();
    }

    [Fact]
    public void Encrypt_uses_a_unique_iv_per_call_under_repeated_invocations()
    {
        // GCM nonce reuse with the same key is catastrophic (allows key recovery).
        // A correct random IV scheme should produce no collisions in 1k samples for
        // a 96-bit space (birthday bound is ~2^48).
        var key = NewKey();
        var seen = new HashSet<string>(capacity: 1000);
        for (var i = 0; i < 1000; i++)
        {
            var env = _cipher.Encrypt(Bytes("x"), key);
            seen.Add(Convert.ToHexString(env.Iv)).Should().BeTrue("IVs must never repeat for the same key");
        }
    }

    [Fact]
    public void Encrypt_produces_different_ciphertexts_for_the_same_plaintext_and_key()
    {
        var key = NewKey();
        var pt = Bytes("repeat");

        var a = _cipher.Encrypt(pt, key);
        var b = _cipher.Encrypt(pt, key);

        a.Iv.Should().NotEqual(b.Iv);
        a.Ciphertext.Should().NotEqual(b.Ciphertext);
        a.Tag.Should().NotEqual(b.Tag);
    }

    [Fact]
    public void Decrypted_buffer_is_fresh_per_call_and_safe_to_mutate()
    {
        var key = NewKey();
        var envelope = _cipher.Encrypt(Bytes("hello"), key);

        var pt1 = _cipher.Decrypt(envelope, key);
        var pt2 = _cipher.Decrypt(envelope, key);

        pt1.Should().NotBeSameAs(pt2);
        pt1.Should().Equal(pt2);

        pt1[0] = 0xFF;
        pt2[0].Should().NotBe(0xFF);
    }
}
