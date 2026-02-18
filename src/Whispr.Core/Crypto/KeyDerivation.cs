using System.Security.Cryptography;

namespace Whispr.Core.Crypto;

/// <summary>
/// Derives encryption keys from shared secrets using HKDF.
/// </summary>
public static class KeyDerivation
{
    private static readonly byte[] DefaultSalt = "Whispr.Audio.KeySalt"u8.ToArray();
    private static readonly byte[] Info = "Whispr.Audio.v1"u8.ToArray();

    /// <summary>
    /// Derives a 256-bit (32-byte) AES key from the given key material.
    /// </summary>
    /// <param name="keyMaterial">Shared secret (e.g. from server key exchange).</param>
    /// <param name="salt">Optional salt for key derivation. If null, uses default.</param>
    /// <returns>32-byte key suitable for AES-256-GCM.</returns>
    public static byte[] DeriveAudioKey(ReadOnlySpan<byte> keyMaterial, byte[]? salt = null)
    {
        return HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            keyMaterial.ToArray(),
            32,
            salt ?? DefaultSalt,
            Info);
    }
}
