namespace Galileo.Chat.Shared.Constants;

public static class ProtocolConstants
{
    public const string ProtocolVersion = "1.0";

    /// <summary>Path of the chat hub. Must match the JWT bearer query-string allow-list in Program.cs.</summary>
    public const string ChatHubPath = "/hubs/chat";

    /// <summary>Hard ceiling on encrypted ciphertext bytes per message.</summary>
    public const int MaxCiphertextBytes = 64 * 1024;

    /// <summary>AES-GCM nonce size — must match Galileo.Chat.Crypto.Aes.AesGcmCipher.IvSize.</summary>
    public const int IvLength = 12;

    /// <summary>AES-GCM authentication tag size.</summary>
    public const int TagLength = 16;
}
