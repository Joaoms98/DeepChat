namespace Galileo.Chat.Crypto.Exceptions;

public class CryptoException : Exception
{
    public CryptoException(string message) : base(message) { }
    public CryptoException(string message, Exception inner) : base(message, inner) { }
}

public sealed class DecryptionFailedException : CryptoException
{
    public DecryptionFailedException(string message)
        : base(message) { }

    public DecryptionFailedException(string message, Exception inner)
        : base(message, inner) { }
}

public sealed class InvalidKeyLengthException : CryptoException
{
    public InvalidKeyLengthException(int actual, int expected)
        : base($"Invalid key length: expected {expected} bytes, got {actual}.") { }
}
