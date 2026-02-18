using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Whispr.Core.Crypto;

/// <summary>
/// Encrypts and decrypts audio payloads using AES-256-GCM.
/// Nonces are generated using a counter to ensure uniqueness.
/// </summary>
public sealed class AudioEncryption(byte[] key) : IDisposable
{
    private readonly byte[] _key = CreateKeyCopy(key);
    private ulong _nonceCounter;
    private readonly object _lock = new();

    private static byte[] CreateKeyCopy(byte[] key)
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(key.Length, 32);
        var k = new byte[32];
        key.CopyTo(k, 0);
        return k;
    }

    /// <summary>
    /// Encrypts plaintext and returns ciphertext + auth tag.
    /// </summary>
    /// <param name="plaintext">Data to encrypt (e.g. Opus-encoded audio).</param>
    /// <returns>Ciphertext with 16-byte auth tag appended (suitable for AudioProtocol.BuildPacket).</returns>
    public byte[] Encrypt(ReadOnlySpan<byte> plaintext)
    {
        var nonce = GetNextNonce();
        var ciphertext = new byte[plaintext.Length + 16];

        using var aes = new AesGcm(_key, 16);
        aes.Encrypt(nonce, plaintext, ciphertext.AsSpan(0, plaintext.Length), ciphertext.AsSpan(plaintext.Length));

        return ciphertext;
    }

    /// <summary>
    /// Encrypts plaintext with a provided nonce (for cases where nonce is managed externally).
    /// </summary>
    public static byte[] EncryptWithNonce(ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> plaintext)
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(key.Length, 32);
        ArgumentOutOfRangeException.ThrowIfNotEqual(nonce.Length, 12);

        var ciphertext = new byte[plaintext.Length + 16];
        using var aes = new AesGcm(key.ToArray(), 16);
        aes.Encrypt(nonce, plaintext, ciphertext.AsSpan(0, plaintext.Length), ciphertext.AsSpan(plaintext.Length));
        return ciphertext;
    }

    /// <summary>
    /// Decrypts ciphertext (including auth tag) and returns plaintext.
    /// </summary>
    /// <param name="nonce">12-byte nonce used for encryption.</param>
    /// <param name="ciphertextWithTag">Ciphertext with 16-byte auth tag appended.</param>
    /// <returns>Decrypted plaintext.</returns>
    public byte[] Decrypt(ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> ciphertextWithTag)
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(nonce.Length, 12);
        ArgumentOutOfRangeException.ThrowIfLessThan(ciphertextWithTag.Length, 16);

        var ciphertextLen = ciphertextWithTag.Length - 16;
        var plaintext = new byte[ciphertextLen];

        using var aes = new AesGcm(_key, 16);
        aes.Decrypt(nonce, ciphertextWithTag.Slice(0, ciphertextLen), ciphertextWithTag.Slice(ciphertextLen), plaintext);

        return plaintext;
    }

    /// <summary>
    /// Decrypts using a static method (no instance state).
    /// </summary>
    public static byte[] DecryptWithKey(ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> ciphertextWithTag)
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(key.Length, 32);
        ArgumentOutOfRangeException.ThrowIfNotEqual(nonce.Length, 12);
        ArgumentOutOfRangeException.ThrowIfLessThan(ciphertextWithTag.Length, 16);

        var ciphertextLen = ciphertextWithTag.Length - 16;
        var plaintext = new byte[ciphertextLen];

        using var aes = new AesGcm(key.ToArray(), 16);
        aes.Decrypt(nonce, ciphertextWithTag.Slice(0, ciphertextLen), ciphertextWithTag.Slice(ciphertextLen), plaintext);

        return plaintext;
    }

    private byte[] GetNextNonce()
    {
        byte[] nonce;
        lock (_lock)
        {
            nonce = new byte[12];
            BinaryPrimitives.WriteUInt64LittleEndian(nonce.AsSpan(4, 8), _nonceCounter++);
        }
        return nonce;
    }

    public void Dispose() => Array.Clear(_key, 0, _key.Length);
}
