#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using EveOPreview.UI;

namespace EveOPreview.Services.Logs;

internal sealed record LogMessagePattern(string Category, LogEventKind Kind, Regex Text,
    DamageDirection? Direction = null, CombatEffect Effect = CombatEffect.Damage,
    int? MessageId = null, bool RequiresDamageColor = false);

internal sealed record LogLanguageDefinition(LogLanguage Language, string Listener, string Session,
    string[] ChannelKeys, string LocalName, string SystemSender, Regex LocalMessage,
    LogMessagePattern[] Messages, string[] HitQualities)
{
    internal IReadOnlyDictionary<string, LogMessagePattern[]> ByCategory { get; } = Messages
        .GroupBy(x => x.Category, StringComparer.Ordinal).ToDictionary(x => x.Key, x => x.ToArray(), StringComparer.Ordinal);
}

/// <summary>Language selection and bounded grammar matching; templates live in EveLogLanguageCatalog.</summary>
internal static class EveLogLanguages
{
    // Log numeric rendering is independent of the operating system culture. The
    // templates alone do not establish decimal-comma rendering: do not guess it.
    internal const string Number = @"(?:[0-9]{1,3}(?:,[0-9]{3})+|[0-9]+)(?:\.[0-9]+)?";
    internal static Regex Pattern(string text) => new(text,
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, TimeSpan.FromMilliseconds(250));
    internal static readonly LogLanguageDefinition[] All = EveLogLanguageCatalog.Create();
    private static readonly Dictionary<LogLanguage, LogLanguageDefinition[]> Manual = All.ToDictionary(x => x.Language, x => new[] { x });
    private static readonly Dictionary<LogLanguage, LogLanguageDefinition[]> Automatic = All.ToDictionary(x => x.Language,
        preferred => All.OrderBy(x => x.Language == preferred.Language ? 0 : 1).ToArray());
    private static readonly HashSet<string> SharedWording = All.SelectMany(language => language.Messages
        .Select(rule => (language.Language, Signature: Signature(rule))))
        .GroupBy(x => x.Signature).Where(x => x.Select(y => y.Language).Distinct().Count() > 1)
        .Select(x => x.Key).ToHashSet(StringComparer.Ordinal);

    private static string Signature(LogMessagePattern rule) => rule.Category + "|" + rule.Kind + "|" + rule.Direction + "|" + rule.Effect + "|" + rule.Text;

    internal static IEnumerable<LogLanguageDefinition> Candidates(LogLanguage preference, LogLanguage detected)
    {
        if (preference != LogLanguage.Automatic) return Manual.GetValueOrDefault(preference) ?? [];
        // Headers are hints because files can mix localized and English messages.
        return Automatic.GetValueOrDefault(detected) ?? All;
    }

    internal static LogLanguage Resolve(LogLanguage preference, LogLanguage detected, LogLanguage candidate, LogMessagePattern rule) =>
        preference != LogLanguage.Automatic || candidate == detected || !SharedWording.Contains(Signature(rule))
            ? candidate : LogLanguage.Automatic;

    internal static bool IsLocalChannel(string? name) => All.Any(x => x.LocalName.Equals(name, StringComparison.Ordinal));
    internal static bool IsLocalFile(string name) => All.Any(x => name.StartsWith(x.LocalName + "_", StringComparison.OrdinalIgnoreCase));
}
