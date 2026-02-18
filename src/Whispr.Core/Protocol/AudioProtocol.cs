using System.Buffers.Binary;

namespace Whispr.Core.Protocol;

/// <summary>
/// Defines the structure of encrypted audio packets over UDP.
/// Format: [ClientId:4 bytes][Nonce:12 bytes][Ciphertext][AuthTag:16 bytes]
/// </summary>
public static class AudioProtocol
{
    /// <summary>
    /// Size of the client ID field (uint32).
    /// </summary>
    public const int ClientIdSize = 4;

    /// <summary>
    /// Size of the AES-GCM nonce.
    /// </summary>
    public const int NonceSize = 12;

    /// <summary>
    /// Size of the AES-GCM authentication tag.
    /// </summary>
    public const int AuthTagSize = 16;

    /// <summary>
    /// Size of the packet header (ClientId + Nonce). Auth tag is appended after ciphertext.
    /// </summary>
    public const int HeaderSize = ClientIdSize + NonceSize;

    /// <summary>
    /// Fixed overhead per packet (ClientId + Nonce + AuthTag).
    /// </summary>
    public const int OverheadSize = ClientIdSize + NonceSize + AuthTagSize;

    /// <summary>
    /// Maximum UDP packet size (MTU-friendly, typical 1500 - 28 for IP+UDP = 1472, use 1200 for safety).
    /// </summary>
    public const int MaxPacketSize = 1200;

    /// <summary>
    /// Maximum ciphertext size (MaxPacketSize - OverheadSize).
    /// </summary>
    public const int MaxCiphertextSize = MaxPacketSize - OverheadSize;

    /// <summary>
    /// Opus frame size: 20ms @ 48kHz = 960 samples.
    /// </summary>
    public const int OpusFrameSamples = 960;

    /// <summary>
    /// Sample rate for Opus (48kHz recommended).
    /// </summary>
    public const int SampleRate = 48000;

    /// <summary>
    /// Builds an audio packet with the given client ID, nonce, and ciphertext (which includes auth tag).
    /// </summary>
    /// <param name="clientId">The sender's client ID.</param>
    /// <param name="nonce">12-byte nonce (must never be reused).</param>
    /// <param name="ciphertextWithTag">Ciphertext + 16-byte auth tag from AES-GCM.</param>
    /// <returns>Complete packet ready to send.</returns>
    public static byte[] BuildPacket(uint clientId, ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> ciphertextWithTag)
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(nonce.Length, NonceSize);

        var packet = new byte[HeaderSize + ciphertextWithTag.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(0, ClientIdSize), clientId);
        nonce.CopyTo(packet.AsSpan(ClientIdSize, NonceSize));
        ciphertextWithTag.CopyTo(packet.AsSpan(HeaderSize));

        return packet;
    }

    /// <summary>
    /// Parses an incoming audio packet into its components.
    /// </summary>
    /// <param name="packet">Raw packet bytes.</param>
    /// <param name="clientId">Output: sender's client ID.</param>
    /// <param name="nonce">Output: 12-byte nonce.</param>
    /// <param name="ciphertextWithTag">Output: ciphertext + auth tag.</param>
    /// <returns>True if packet was valid and parsed successfully.</returns>
    public static bool TryParsePacket(
        ReadOnlySpan<byte> packet,
        out uint clientId,
        out ReadOnlySpan<byte> nonce,
        out ReadOnlySpan<byte> ciphertextWithTag)
    {
        clientId = 0;
        nonce = default;
        ciphertextWithTag = default;

        if (packet.Length < OverheadSize)
            return false;

        clientId = BinaryPrimitives.ReadUInt32LittleEndian(packet);
        nonce = packet.Slice(ClientIdSize, NonceSize);
        ciphertextWithTag = packet.Slice(HeaderSize);

        return true;
    }
}
