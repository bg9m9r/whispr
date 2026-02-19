using System.Security.Cryptography;

namespace Whispr.Server.Services;

/// <summary>
/// AES-256-GCM encryption for message content at rest.
/// Uses a 12-byte IV and 16-byte auth tag per message.
/// </summary>
public sealed class AesMessageEncryption : IMessageEncryption
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private readonly byte[] _key;

    public AesMessageEncryption(byte[] key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key.Length != 32)
            throw new ArgumentException("Key must be 32 bytes for AES-256.", nameof(key));
        _key = key;
    }

    public byte[] Encrypt(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        var plainBytes = System.Text.Encoding.UTF8.GetBytes(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertextWithTag = new byte[plainBytes.Length + TagSize];
        var tag = ciphertextWithTag.AsSpan(plainBytes.Length, TagSize);

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plainBytes, ciphertextWithTag.AsSpan(0, plainBytes.Length), tag);

        var result = new byte[NonceSize + ciphertextWithTag.Length];
        nonce.CopyTo(result, 0);
        ciphertextWithTag.CopyTo(result, NonceSize);
        return result;
    }

    public string Decrypt(byte[] ciphertext)
    {
        ArgumentNullException.ThrowIfNull(ciphertext);
        if (ciphertext.Length < NonceSize + TagSize)
            throw new ArgumentException("Ciphertext too short.", nameof(ciphertext));

        var nonce = ciphertext.AsSpan(0, NonceSize);
        var ciphertextWithTag = ciphertext.AsSpan(NonceSize);
        var cipherLen = ciphertextWithTag.Length - TagSize;
        var plaintext = new byte[cipherLen];
        var tag = ciphertextWithTag.Slice(cipherLen, TagSize);

        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(nonce, ciphertextWithTag.Slice(0, cipherLen), tag, plaintext);
        return System.Text.Encoding.UTF8.GetString(plaintext);
    }
}
