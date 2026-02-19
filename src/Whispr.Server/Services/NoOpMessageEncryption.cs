using System.Text;

namespace Whispr.Server.Services;

/// <summary>
/// No-op "encryption" for local development only. Stores content as base64 UTF-8 (same format as real encryption)
/// so the repository format is unchanged. Do not use in production.
/// </summary>
public sealed class NoOpMessageEncryption : IMessageEncryption
{
    public byte[] Encrypt(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        return Encoding.UTF8.GetBytes(plaintext);
    }

    public string Decrypt(byte[] ciphertext)
    {
        ArgumentNullException.ThrowIfNull(ciphertext);
        return Encoding.UTF8.GetString(ciphertext);
    }
}
