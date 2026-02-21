using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Whispr.Client.Models;

/// <summary>
/// A segment of message content: text, link, emoji image, GIF embed, or YouTube embed.
/// </summary>
public sealed record MessageContentSegment(string Content, bool IsLink, bool UseEmojiFont = true, bool IsEmojiImage = false, bool IsGifEmbed = false, bool IsYouTubeEmbed = false)
{
    /// <summary>True when segment should be shown as plain text (not link, not emoji image, not GIF/YouTube embed).</summary>
    public bool IsTextSegment => !IsLink && !IsEmojiImage && !IsGifEmbed && !IsYouTubeEmbed;

    /// <summary>When <see cref="IsGifEmbed"/> is true, direct image URL for the GIF (Tenor/Giphy).</summary>
    public string? GifEmbedImageUrl => IsGifEmbed ? TryResolveGifEmbedUrl(Content) : null;

    /// <summary>When <see cref="IsYouTubeEmbed"/> is true, YouTube video id (e.g. from watch?v=ID or youtu.be/ID).</summary>
    public string? YouTubeVideoId => IsYouTubeEmbed ? TryExtractYouTubeVideoId(Content) : null;

    /// <summary>When <see cref="IsYouTubeEmbed"/> is true, thumbnail URL (no API key).</summary>
    public string? YouTubeThumbnailUrl => YouTubeVideoId is { } id ? $"https://img.youtube.com/vi/{id}/mqdefault.jpg" : null;

    /// <summary>Text/link segments: font for display. Ignored when <see cref="IsEmojiImage"/> is true.</summary>
    public string FontFamily => UseEmojiFont ? "Noto Color Emoji" : "sans-serif";

    /// <summary>When <see cref="IsEmojiImage"/> is true, Twemoji CDN URL (PNG) for the emoji in <see cref="Content"/>.</summary>
    public string? EmojiImageUrl => IsEmojiImage ? GetTwemojiImageUrl(Content) : null;

    /// <summary>Twemoji CDN: 72x72 PNG by Unicode codepoint(s). Avalonia Image loads PNG from URL; SVG from URL is not supported.</summary>
    private static string? GetTwemojiImageUrl(string emoji)
    {
        if (string.IsNullOrEmpty(emoji)) return null;
        var parts = new List<string>();
        for (int i = 0; i < emoji.Length;)
        {
            int cp;
            if (char.IsHighSurrogate(emoji[i]) && i + 1 < emoji.Length && char.IsLowSurrogate(emoji[i + 1]))
            {
                cp = char.ConvertToUtf32(emoji[i], emoji[i + 1]);
                i += 2;
            }
            else
            {
                cp = emoji[i];
                i++;
            }
            parts.Add(cp.ToString("x"));
        }
        if (parts.Count == 0) return null;
        const string baseUrl = "https://cdn.jsdelivr.net/gh/jdecked/twemoji@latest/assets/72x72/";
        return baseUrl + string.Join("-", parts) + ".png";
    }

    /// <summary>Resolves Tenor/Giphy page URL to a direct image URL when possible; otherwise returns the original.</summary>
    private static string? TryResolveGifEmbedUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        // Giphy: extract id from /gifs/...-id or /media/id/ and use i.giphy.com
        var giphyMatch = Regex.Match(url, @"giphy\.com/(?:gifs/[^/]+-|media/)([a-zA-Z0-9]+)", RegexOptions.IgnoreCase);
        if (giphyMatch.Success)
            return $"https://i.giphy.com/media/{giphyMatch.Groups[1].Value}/giphy.gif";
        // Tenor: view URLs don't give us media.tenor.com id without API; use original URL as fallback
        if (url.Contains("tenor.com", StringComparison.OrdinalIgnoreCase))
            return url;
        return null;
    }

    /// <summary>Extracts YouTube video id from watch?v=id or youtu.be/id.</summary>
    private static string? TryExtractYouTubeVideoId(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var m = Regex.Match(url, @"(?:youtube\.com/watch\?v=|youtu\.be/)([a-zA-Z0-9_-]{11})", RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value : null;
    }
}
