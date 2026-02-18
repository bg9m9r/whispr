using System.Buffers.Binary;
using System.Text;
using System.Text.Json;

namespace Whispr.Core.Protocol;

/// <summary>
/// Handles serialization and deserialization of control channel messages.
/// Format: [4-byte length (little-endian)][UTF-8 JSON payload]
/// </summary>
public static class ControlProtocol
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Maximum message size (64 KB).
    /// </summary>
    public const int MaxMessageSize = 64 * 1024;

    /// <summary>
    /// Serializes a control message to a byte array (length-prefixed).
    /// </summary>
    public static byte[] Serialize(ControlMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var json = JsonSerializer.Serialize(message, JsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);

        ArgumentOutOfRangeException.ThrowIfGreaterThan(bytes.Length, MaxMessageSize, $"Message exceeds max size of {MaxMessageSize} bytes.");

        var result = new byte[4 + bytes.Length];
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(0, 4), bytes.Length);
        bytes.CopyTo(result, 4);
        return result;
    }

    /// <summary>
    /// Serializes a message with a typed payload.
    /// </summary>
    public static byte[] Serialize<T>(string type, T payload)
    {
        var message = new ControlMessage
        {
            Type = type,
            Payload = JsonSerializer.SerializeToElement(payload, JsonOptions)
        };
        return Serialize(message);
    }

    /// <summary>
    /// Deserializes a control message from a byte array (without length prefix).
    /// Assumes the caller has already read the length and stripped it.
    /// </summary>
    public static ControlMessage Deserialize(ReadOnlySpan<byte> payload)
    {
        var json = Encoding.UTF8.GetString(payload);
        return JsonSerializer.Deserialize<ControlMessage>(json, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize control message.");
    }

    /// <summary>
    /// Deserializes the payload of a control message to a strongly-typed object.
    /// </summary>
    public static T? DeserializePayload<T>(ControlMessage message)
    {
        return message.Payload is null ? default : message.Payload.Value.Deserialize<T>(JsonOptions);
    }

    /// <summary>
    /// Tries to read a length-prefixed message from the stream.
    /// Returns null if not enough data is available.
    /// </summary>
    public static async Task<ControlMessage?> TryReadAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var lengthBuffer = new byte[4];
        var bytesRead = await stream.ReadAsync(lengthBuffer.AsMemory(0, 4), cancellationToken);
        if (bytesRead < 4)
            return null;

        var length = BinaryPrimitives.ReadInt32LittleEndian(lengthBuffer);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(length, MaxMessageSize, $"Invalid message length: {length}");

        var payloadBuffer = new byte[length];
        var totalRead = 0;
        while (totalRead < length)
        {
            var read = await stream.ReadAsync(payloadBuffer.AsMemory(totalRead, length - totalRead), cancellationToken);
            if (read == 0)
                return null;
            totalRead += read;
        }

        return Deserialize(payloadBuffer);
    }
}
