using Galileo.Chat.Domain.Common;

namespace Galileo.Chat.Domain.ValueObjects;

/// <summary>
/// Carrier domain VO for an AES-GCM ciphertext. Domain stays oblivious to crypto details
/// (the actual encrypt/decrypt lives in Galileo.Chat.Crypto); we only enforce structural shape.
///
/// Immutability contract: backing buffers are private. Public getters return defensive
/// copies — a caller writing to <c>payload.Ciphertext[0]</c> mutates only its own copy
/// and never corrupts the stored ciphertext. Equality and hashing read the private
/// buffers directly to avoid the per-getter clone cost on hot paths.
/// </summary>
public sealed record EncryptedPayload
{
    public const int IvLength = 12;   // 96 bits — AES-GCM standard nonce
    public const int TagLength = 16;  // 128 bits — full authentication tag

    private readonly byte[] _iv;
    private readonly byte[] _ciphertext;
    private readonly byte[] _tag;

    public byte[] Iv => (byte[])_iv.Clone();
    public byte[] Ciphertext => (byte[])_ciphertext.Clone();
    public byte[] Tag => (byte[])_tag.Clone();

    /// <summary>
    /// Total byte size of the underlying buffers. Computed from private fields, no allocations.
    /// </summary>
    public int TotalBytes => _iv.Length + _ciphertext.Length + _tag.Length;

    /// <summary>
    /// Zero-copy access for trusted infrastructure (persistence/serialization layers)
    /// that must NOT mutate the bytes. Callers commit not to write.
    /// </summary>
    public ReadOnlySpan<byte> IvSpan => _iv;
    public ReadOnlySpan<byte> CiphertextSpan => _ciphertext;
    public ReadOnlySpan<byte> TagSpan => _tag;

    private EncryptedPayload(byte[] iv, byte[] ciphertext, byte[] tag)
    {
        _iv = iv;
        _ciphertext = ciphertext;
        _tag = tag;
    }

    public static EncryptedPayload Create(ReadOnlySpan<byte> iv, ReadOnlySpan<byte> ciphertext, ReadOnlySpan<byte> tag)
    {
        Guard.ExactLength(iv, IvLength);
        Guard.NotEmpty(ciphertext);
        Guard.ExactLength(tag, TagLength);

        return new EncryptedPayload(iv.ToArray(), ciphertext.ToArray(), tag.ToArray());
    }

    public bool Equals(EncryptedPayload? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return _iv.AsSpan().SequenceEqual(other._iv)
            && _ciphertext.AsSpan().SequenceEqual(other._ciphertext)
            && _tag.AsSpan().SequenceEqual(other._tag);
    }

    public override int GetHashCode()
    {
        var hc = new HashCode();
        hc.AddBytes(_iv);
        hc.AddBytes(_ciphertext);
        hc.AddBytes(_tag);
        return hc.ToHashCode();
    }
}
