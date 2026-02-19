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

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string s || string.IsNullOrEmpty(s))
            return Array.Empty<MessageContentSegment>();

        var list = new List<MessageContentSegment>();
        var lastEnd = 0;
        foreach (Match m in UrlRegex.Matches(s))
        {
            if (m.Index > lastEnd)
            {
                var text = s.Substring(lastEnd, m.Index - lastEnd);
                list.Add(new MessageContentSegment(text, false));
            }
            list.Add(new MessageContentSegment(m.Value, true));
            lastEnd = m.Index + m.Length;
        }
        if (lastEnd < s.Length)
            list.Add(new MessageContentSegment(s.Substring(lastEnd), false));

        return list.Count == 0 ? [new MessageContentSegment(s, false)] : list;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
