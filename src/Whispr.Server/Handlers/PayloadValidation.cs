namespace Whispr.Server.Handlers;

/// <summary>
/// Shared payload validation helpers.
/// </summary>
public static class PayloadValidation
{
    public const int MaxChannelNameLength = 256;
    public const int MaxMessageContentLength = 4096;

    public static bool IsValidChannelName(string? name, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(name))
        {
            error = "Channel name is required";
            return false;
        }
        var trimmed = name.Trim();
        if (trimmed.Length > MaxChannelNameLength)
        {
            error = $"Channel name must be at most {MaxChannelNameLength} characters";
            return false;
        }
        return true;
    }

    public static bool IsValidChannelId(Guid channelId, out string? error)
    {
        error = null;
        if (channelId == Guid.Empty)
        {
            error = "Channel ID is required";
            return false;
        }
        return true;
    }

    public static bool IsValidClientId(uint clientId, out string? error)
    {
        error = null;
        if (clientId == 0)
        {
            error = "Client ID cannot be zero";
            return false;
        }
        return true;
    }

    public static bool IsValidMessageContent(string? content, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(content))
        {
            error = "Message content cannot be empty";
            return false;
        }
        if (content.Length > MaxMessageContentLength)
        {
            error = $"Message content must be at most {MaxMessageContentLength} characters";
            return false;
        }
        return true;
    }
}
