using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia.Data.Converters;
using Whispr.Client.Models;

namespace Whispr.Client.Converters;

/// <summary>
/// Converts message content string to a list of text and link segments for display with clickable URLs.
/// </summary>
public sealed class ContentToSegmentsConverter : IValueConverter
{
    public static readonly ContentToSegmentsConverter Instance = new();

    // Match http/https URLs (simple pattern; avoids trailing punctuation where possible)
    private static readonly Regex UrlRegex = new(@"https?://[^\s<>""']+", RegexOptions.Compiled);

    private static bool IsGifEmbedUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        return url.Contains("tenor.com", StringComparison.OrdinalIgnoreCase)
            || url.Contains("giphy.com", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsYouTubeEmbedUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        return url.Contains("youtube.com/watch", StringComparison.OrdinalIgnoreCase)
            || url.Contains("youtu.be/", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Splits plain text into runs of emoji vs non-emoji so we can use different fonts:
    /// emoji runs use the emoji font, other runs (spaces, letters) use primary font only to avoid wide spaces.
    /// </summary>
    private static void AddTextRuns(string text, List<MessageContentSegment> list)
    {
        if (string.IsNullOrEmpty(text)) return;
        bool? runIsEmoji = null;
        int runStart = 0;
        for (int i = 0; i < text.Length;)
        {
            int cp;
            int len;
            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                cp = char.ConvertToUtf32(text[i], text[i + 1]);
                len = 2;
            }
            else
            {
                cp = text[i];
                len = 1;
            }
            bool isEmoji = IsEmojiCodePoint(cp);
            if (runIsEmoji.HasValue && isEmoji != runIsEmoji.Value)
            {
                var runText = text.Substring(runStart, i - runStart);
                list.Add(new MessageContentSegment(runText, false, runIsEmoji.Value, runIsEmoji.Value));
                runStart = i;
            }
            runIsEmoji = isEmoji;
            i += len;
        }
        var lastRun = text.Substring(runStart);
        list.Add(new MessageContentSegment(lastRun, false, runIsEmoji!.Value, runIsEmoji.Value));
    }

    private static bool IsEmojiCodePoint(int codePoint)
    {
        if (codePoint < 0) return false;
        return codePoint is >= 0x2600 and <= 0x26FF or >= 0x2700 and <= 0x27BF
            or >= 0x2B00 and <= 0x2BFF  // e.g. ⭐ U+2B50
            or >= 0x1F000 and <= 0x1F02F or >= 0x1F300 and <= 0x1F9FF
            or >= 0x1FA00 and <= 0x1FA6F;
    }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string s || string.IsNullOrEmpty(s))
            return Array.Empty<MessageContentSegment>();

        var list = new List<MessageContentSegment>();
        var lastEnd = 0;
        foreach (Match m in UrlRegex.Matches(s))
        {
            if (m.Index > lastEnd)
                AddTextRuns(s.Substring(lastEnd, m.Index - lastEnd), list);
            var url = m.Value;
            if (IsGifEmbedUrl(url))
                list.Add(new MessageContentSegment(url, false, true, false, true, false)); // GIF embed
            else if (IsYouTubeEmbedUrl(url))
                list.Add(new MessageContentSegment(url, false, true, false, false, true)); // YouTube embed
            else
                list.Add(new MessageContentSegment(url, true)); // links keep default UseEmojiFont
            lastEnd = m.Index + m.Length;
        }
        if (lastEnd < s.Length)
            AddTextRuns(s.Substring(lastEnd), list);

        return list.Count == 0 ? (object)Array.Empty<MessageContentSegment>() : list;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
