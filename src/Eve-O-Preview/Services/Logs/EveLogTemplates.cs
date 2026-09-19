#nullable enable
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using EveOPreview.UI;

namespace EveOPreview.Services.Logs;

/// <summary>
/// Converts reviewed client templates into anchored plain-text grammars. Template
/// data is generated offline; log text can never supply a regex or a template.
/// </summary>
internal static class EveLogTemplates
{
    private static readonly Regex Argument = new(@"\{([^{}]+)\}", RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(250));
    private static readonly Regex Branch = new("^\\[(?:character|numeric)\\]\\w+(?:\\.gender)?\\s*->\\s*(?:\"[^\"]*\"\\s*,\\s*)+\"[^\"]*\"$",
        RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250));
    private static readonly Regex Choice = new("\"([^\"]*)\"", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250));
    private static readonly HashSet<string> Formatting = new(StringComparer.Ordinal)
    {
        "faintColor", "fontMarkUpStart", "fontMarkUpEnd", "normalFont", "smallFont",
        "normalColor", "amountMinedColor", "amountWastedColor", "critColor", "bountyPayoutColor"
    };

    internal static LogMessagePattern Rule(int id, string category, LogEventKind kind, string template,
        DamageDirection? direction = null, CombatEffect effect = CombatEffect.Damage,
        bool fragment = false, bool requiresDamageColor = false, string currency = "ISK")
    {
        string expression = Compile(template, kind, out string requiredLiteral, currency: currency);
        if (fragment)
            expression = "(?<amount>" + EveLogLanguages.Number + @")\s*" + expression
                + @"(?:\s+-\s+(?<suffix>.*))?";
        return new(category, kind, new LogTextPattern("^" + expression + "$", requiredLiteral), direction,
            effect, id, requiresDamageColor);
    }

    internal static LogTextPattern Local(string template, string localName)
    {
        // The channel name is composed by the client: localized Local + colon +
        // system. Keep suffix punctuation from the template (notably Spanish).
        string text = template.Replace("{channelName}", "{localChannel}", StringComparison.Ordinal);
        string expression = Compile(text, LogEventKind.SystemChange, out string requiredLiteral,
            Literal(localName) + @"\s*[:：]\s*(?<system>.+?)");
        return new("^" + expression + "$", requiredLiteral);
    }

    private static string Compile(string template, LogEventKind kind, out string requiredLiteral,
        string? localChannel = null, string currency = "ISK")
    {
        // extraNameText is the ship/ticker decoration attached directly to the
        // peer. Capture it together, leaving the existing SDE/label resolver in charge.
        template = Argument.Replace(template, m => Formatting.Contains(m.Groups[1].Value)
            || m.Groups[1].Value == "extraNameText" ? "" : m.Value);
        template = EveLogText.PlainText(template);
        const string weaponTail = " - {[item]type.name}";
        bool optionalWeapon = kind == LogEventKind.Repair && template.EndsWith(weaponTail, StringComparison.Ordinal);
        if (optionalWeapon) template = template[..^weaponTail.Length];
        var result = new StringBuilder();
        string longestLiteral = "";
        void AppendLiteral(string text)
        {
            result.Append(Literal(text));
            // Whitespace and colons have flexible regex spellings. Only select
            // an unconditional, exact token; omit branch alternatives and the
            // optional repair-weapon suffix. This is a rejection filter only.
            int start = 0;
            for (int i = 0; i <= text.Length; i++)
                if (i == text.Length || char.IsWhiteSpace(text[i]) || text[i] is ':' or '：')
                {
                    if (i - start > longestLiteral.Length) longestLiteral = text[start..i];
                    start = i + 1;
                }
        }
        var captured = new HashSet<string>(StringComparer.Ordinal);
        int offset = 0;
        foreach (Match argument in Argument.Matches(template))
        {
            AppendLiteral(template[offset..argument.Index]);
            string token = argument.Groups[1].Value;
            if (token.Contains("->", StringComparison.Ordinal))
            {
                if (!Branch.IsMatch(token)) throw new InvalidOperationException("Review conditional localization arguments before importing them.");
                result.Append("(?:");
                bool first = true;
                foreach (Match choice in Choice.Matches(token))
                {
                    if (!first) result.Append('|');
                    result.Append(Literal(choice.Groups[1].Value));
                    first = false;
                }
                result.Append(')');
                offset = argument.Index + argument.Length;
                continue;
            }
            int close = token.IndexOf(']');
            if (token.StartsWith('[') && close >= 0) token = token[(close + 1)..].TrimStart();
            token = token.Split(['.', ','])[0].Trim();
            string? group = token switch
            {
                "damage" => "amount",
                "amount" => kind is LogEventKind.Damage or LogEventKind.Repair ? "amount" : "quantity",
                "energyAmount" or "bounty" => "quantity",
                "amountWasted" => kind == LogEventKind.MiningResidue ? "quantity" : "residue",
                "target" or "source" or "owner" or "specialObject" => "peer",
                "system" or "toSystem" => "system",
                "oreTypeID" => "item",
                "weapon" or "type" => "suffix",
                _ => null
            };
            // Status events retain their full plain text and message ID. Their
            // arbitrary arguments are not quantities, repairs or proven locations.
            if (kind is LogEventKind.Navigation or LogEventKind.Cloak or LogEventKind.MiningStatus
                or LogEventKind.Salvage or LogEventKind.Cargo or LogEventKind.Drone or LogEventKind.Targeting
                or LogEventKind.Module or LogEventKind.Fleet or LogEventKind.SkillTraining or LogEventKind.Connection) group = null;
            if (token == "localChannel") result.Append(localChannel ?? throw new InvalidOperationException("Missing Local name."));
            else if (group is null) result.Append(@".+?");
            else
            {
                if (!captured.Add(group)) throw new InvalidOperationException("Repeated capture requires template review: " + group);
                string value = group is "amount" or "quantity" or "residue" ? EveLogLanguages.Number : ".+?";
                result.Append("(?<").Append(group).Append('>').Append(value).Append(')');
                // The .isk argument includes the client's localized currency label.
                if (token == "bounty") result.Append(@"\s*").Append(Literal(currency));
            }
            offset = argument.Index + argument.Length;
        }
        AppendLiteral(template[offset..]);
        // Weapon display can be absent. Only make a terminal weapon suffix
        // optional; Japanese direction-bearing endings must remain mandatory.
        if (optionalWeapon) result.Append(@"(?:\s+-\s+(?<suffix>.+?))?");
        requiredLiteral = longestLiteral;
        return result.ToString();
    }

    private static string Literal(string text)
    {
        var result = new StringBuilder();
        bool space = false;
        foreach (char c in text)
        {
            if (char.IsWhiteSpace(c)) { if (!space) result.Append(@"\s+"); space = true; }
            else { result.Append(c is ':' or '：' ? "[:：]" : Regex.Escape(c.ToString())); space = false; }
        }
        return result.ToString();
    }
}
