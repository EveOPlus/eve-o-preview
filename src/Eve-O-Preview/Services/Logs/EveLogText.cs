#nullable enable
using System;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using EveOPreview.UI;

namespace EveOPreview.Services.Logs;

/// <summary>Text extraction only, never HTML rendering. Keep visible names for SDE alias lookup.</summary>
public static class EveLogText
{
    // EVE markup need not be balanced and attributes may contain '>'. Restrict the
    // recognized tags so an overview label such as <CORP> is not silently deleted.
    private static readonly Regex Tags = new(
        "</?(?:color|font|fontsize|b|i|u|a|url|localized|br|p|span|div|strong|em)(?=[\\s=/ >])(?:[^>\"']|\"[^\"]*\"|'[^']*')*>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(50));

    private static readonly Regex Color = new("^<color\\s*=\\s*['\"]?(?<value>(?:0x|#)[0-9a-f]{8})['\"]?\\s*>$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(250));

    internal static DamageDirection? DamageDirection(string value)
    {
        int offset = 0;
        DamageDirection? direction = null;
        foreach (Match tag in Tags.Matches(value))
        {
            // Stop before any visible character. Overview tags come AFTER the
            // amount and therefore cannot supply direction evidence.
            if (!value.AsSpan(offset, tag.Index - offset).Trim().IsEmpty) break;
            if (tag.Value.StartsWith("<color", StringComparison.OrdinalIgnoreCase)
                || tag.Value.StartsWith("</color", StringComparison.OrdinalIgnoreCase))
                direction = Color.Match(tag.Value).Groups["value"].Value.ToLowerInvariant() switch
                {
                    "0xffcc0000" or "#ffcc0000" => UI.DamageDirection.Incoming,
                    "0xff00ffff" or "#ff00ffff" => UI.DamageDirection.Outgoing,
                    _ => null
                };
            offset = tag.Index + tag.Length;
        }
        var text = value.AsSpan(offset).TrimStart();
        return text.Length > 0 && char.IsAsciiDigit(text[0]) ? direction : null;
    }

    public static string PlainText(string value)
    {
        if (!value.Contains('<') && !value.Contains('&')) return value.Trim().TrimStart('\uFEFF');
        var text = new StringBuilder(value.Length);
        int offset = 0;
        bool localized = false;
        void Append(int end)
        {
            var chunk = value.AsSpan(offset, end - offset);
            // The client sometimes omits </localized>. Its star then ends the
            // name in the middle of a chunk, before the rest of the sentence.
            // Never remove stars from unwrapped player/custom overview labels.
            if (localized && chunk.IndexOf('*') is int star && star >= 0)
            {
                text.Append(chunk[..star]);
                chunk = chunk[(star + 1)..];
                localized = false;
            }
            text.Append(chunk);
        }
        foreach (Match tag in Tags.Matches(value))
        {
            Append(tag.Index);
            if (tag.Value.StartsWith("<localized", StringComparison.OrdinalIgnoreCase)) localized = true;
            if (tag.Value.StartsWith("</localized", StringComparison.OrdinalIgnoreCase)) localized = false;
            if (tag.Value.StartsWith("<br", StringComparison.OrdinalIgnoreCase)
                || tag.Value.StartsWith("</p>", StringComparison.OrdinalIgnoreCase)) text.Append(' ');
            offset = tag.Index + tag.Length;
        }
        Append(value.Length);
        // Decode once, after extraction. Encoded literal <...> remains visible text.
        return WebUtility.HtmlDecode(text.ToString()).Trim().TrimStart('\uFEFF');
    }
}
