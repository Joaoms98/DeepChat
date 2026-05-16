namespace Galileo.Chat.Crypto.Aes;

/// <summary>
/// Wire-format envelope produced by <see cref="AesGcmCipher"/>.
/// All fields travel together over the network and are persisted as-is by the server.
/// </summary>
public readonly record struct EncryptedEnvelope(byte[] Iv, byte[] Ciphertext, byte[] Tag)
{
    public int TotalBytes => Iv.Length + Ciphertext.Length + Tag.Length;
}
