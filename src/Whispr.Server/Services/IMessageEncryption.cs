namespace Whispr.Server.Services;

/// <summary>
/// Encrypts and decrypts message content at rest.
/// </summary>
public interface IMessageEncryption
{
    byte[] Encrypt(string plaintext);
    string Decrypt(byte[] ciphertext);
}
