using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EveOPreview.Services.Logs;
using EveOPreview.UI;
using Xunit;

namespace EveOPreview.Tests.Checks;

public sealed class JapaneseLogFixtureTests
{
    private static readonly EveLogCatalog Catalog = EveLogCatalog.Load();

    [Theory]
    [InlineData(LogLanguage.Automatic)]
    [InlineData(LogLanguage.Japanese)]
    public void RecordedJapaneseIncomingAndOutgoingDamageKeepSeparateTotals(LogLanguage preference)
    {
        using var input = CompleteLogReader.OpenShared(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "logs", "japanese-damage.txt"));
        var batch = CompleteLogReader.Read(input, CompleteLogReader.Identity(input), null);
        Assert.False(batch.More); Assert.Equal(0, batch.OversizedLines);
        var header = new LogHeader();
        foreach (var line in batch.Lines) header = EveLogParser.ReadHeader(header, line.Text);
        Assert.Equal("Sample Observer", header.Listener); Assert.Equal(LogLanguage.Japanese, header.Language);
        var raw = batch.Lines.Where(x => x.Text.Contains("(combat)")).Select(x => x.Text).ToArray();
        var entries = raw.Select(line => EveLogParser.Parse(line, header, false, Catalog,
            new HashSet<string>(), preference)).ToArray();
        Assert.Equal(6, entries.Length);
        Assert.Equal(new[] { 40d, 17d, 126d, 492d, 393d, 268d }, entries.Select(x => x.Amount));
        Assert.All(entries, entry =>
        {
            Assert.Equal(LogEventKind.Damage, entry.EventKind); Assert.Equal(LogLanguage.Japanese, entry.Language);
            Assert.Equal(CombatantKind.Npc, entry.Kind);
            Assert.DoesNotContain("<", entry.Text); Assert.DoesNotContain("*", entry.Counterparty);
        });
        Assert.All(entries.Take(3), entry =>
        { Assert.Equal(DamageDirection.Incoming, entry.Direction); Assert.Null(entry.Weapon); });
        Assert.All(entries.Skip(3), entry =>
        {
            Assert.Equal(DamageDirection.Outgoing, entry.Direction);
            Assert.Equal("ノヴァライトミサイル", entry.Weapon);
            Assert.Equal(213, entry.DamageSourceTypeId); Assert.Equal(DamageTypes.Explosive, entry.DamageTypes);
            Assert.Equal(DamageEvidence.NamedItem, entry.DamageEvidence);
        });
        Assert.Equal(183, entries.Where(x => x.Direction == DamageDirection.Incoming).Sum(x => x.Amount));
        Assert.Equal(1153, entries.Where(x => x.Direction == DamageDirection.Outgoing).Sum(x => x.Amount));
        Assert.Equal(TimeSpan.FromSeconds(25), entries[^1].Timestamp - entries[0].Timestamp);

        for (int i = 0; i < raw.Length; i++)
        {
            // Controlled mutations of recorded input, not additional real examples.
            string leading = i < 3 ? "0xffcc0000" : "0xff00ffff";
            string opposite = i < 3 ? "0xff00ffff" : "0xffcc0000";
            string overviewColor = raw[i].Replace("<color=0xffffffff>", $"<color={opposite}>");
            var colored = EveLogParser.Parse(overviewColor, header, false, Catalog, new HashSet<string>(), preference);
            Assert.Equal(entries[i].Direction, colored.Direction); Assert.Equal(entries[i].Amount, colored.Amount);
            var ambiguous = EveLogParser.Parse(overviewColor.Replace($"(combat) <color={leading}>", "(combat) "),
                header, false, Catalog, new HashSet<string>(), preference);
            Assert.Equal(LogEventKind.Unknown, ambiguous.EventKind); Assert.Null(ambiguous.Direction);
        }
    }

    [Theory]
    [InlineData(LogLanguage.Automatic)]
    [InlineData(LogLanguage.Japanese)]
    public void RecordedJapaneseDamageDecloakAndMiningParse(LogLanguage preference)
    {
        using var input = CompleteLogReader.OpenShared(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "logs", "japanese-client.txt"));
        var batch = CompleteLogReader.Read(input, CompleteLogReader.Identity(input), null);
        Assert.False(batch.More); Assert.Equal(0, batch.OversizedLines);
        var header = new LogHeader();
        foreach (var line in batch.Lines) header = EveLogParser.ReadHeader(header, line.Text);
        Assert.Equal("Sample Observer", header.Listener); Assert.Equal(LogLanguage.Japanese, header.Language);
        var entries = batch.Lines.Select(line => EveLogParser.Parse(line.Text, header, false,
            Catalog, new HashSet<string>(), preference)).Where(x => x is not null).ToArray();
        Assert.Equal(new[] { LogEventKind.Damage, LogEventKind.Damage, LogEventKind.Damage,
            LogEventKind.CombatMiss, LogEventKind.Damage, LogEventKind.Decloak, LogEventKind.Decloak,
            LogEventKind.Mining, LogEventKind.Mining, LogEventKind.MiningStatus }, entries.Select(x => x.EventKind));
        var hits = entries.Where(x => x.EventKind == LogEventKind.Damage).ToArray();
        Assert.Equal(new[] { 34d, 132d, 33d, 403d }, hits.Select(x => x.Amount));
        Assert.All(hits, hit =>
        {
            Assert.Equal(DamageDirection.Incoming, hit.Direction);
            Assert.Equal(CombatantKind.Npc, hit.Kind);
            Assert.Null(hit.Weapon); // The trailing text is hit quality, not a weapon.
        });
        Assert.All(entries.Skip(5).Take(2), entry => Assert.Equal(581419, entry.MessageId));
        Assert.All(entries.Skip(7).Take(2), entry =>
        {
            Assert.Equal(1m, entry.Quantity); Assert.Equal("units", entry.Unit);
            Assert.Equal("ベズドナシン", entry.ItemName); Assert.Null(entry.ResidueQuantity);
            Assert.Equal("あなたは1ユニットのベズドナシンを採掘しました", entry.Text);
        });
        Assert.Equal(TimeSpan.FromSeconds(20), entries[8].Timestamp - entries[7].Timestamp);
        Assert.Null(entries[9].Quantity); // A full-hold notice is not another mining yield.
        Assert.All(entries, entry =>
        {
            Assert.Equal(LogLanguage.Japanese, entry.Language);
            Assert.DoesNotContain("<", entry.Text); Assert.DoesNotContain("*", entry.Text);
            if (entry.EventKind != LogEventKind.Damage)
            { Assert.Null(entry.Direction); Assert.Equal(0, entry.Amount); }
        });

        // Controlled changes to recorded input: an overview color cannot replace
        // missing leading-amount evidence for Japanese's ambiguous wording.
        string raw = batch.Lines.First(x => x.Text.Contains("<b>34</b>")).Text;
        string withoutColor = raw.Replace("<color=0xffcc0000>", "")
            .Replace("<color=0xffffffff>", "<color=0xff00ffff>");
        var ambiguous = EveLogParser.Parse(withoutColor, header, false, Catalog, new HashSet<string>(), preference);
        Assert.Equal(LogEventKind.Unknown, ambiguous.EventKind); Assert.Null(ambiguous.Direction);
    }
}
