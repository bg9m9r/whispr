namespace Whispr.Server.Services;

/// <summary>
/// Stub used when database is in use but message encryption key is not set (e.g. add-user CLI).
/// Throws if Encrypt/Decrypt are ever called; server startup requires a real key.
/// </summary>
public sealed class ThrowingMessageEncryptionStub : IMessageEncryption
{
    public byte[] Encrypt(string plaintext) =>
        throw new InvalidOperationException("Message encryption key is not configured. Set WHISPR_MESSAGE_ENCRYPTION_KEY.");

    public string Decrypt(byte[] ciphertext) =>
        throw new InvalidOperationException("Message encryption key is not configured. Set WHISPR_MESSAGE_ENCRYPTION_KEY.");
}
