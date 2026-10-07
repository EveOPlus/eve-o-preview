using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using EveOPreview.Services.Logs;
using EveOPreview.UI;
using Xunit;

namespace EveOPreview.Tests.Checks;

public sealed class MixedNameLogFixtureTests
{
    private static readonly EveLogCatalog Catalog = CreateCatalog();
    private static EveLogCatalog CreateCatalog()
    {
        var catalog = EveLogCatalog.Load();
        // Minimal public SDE ore record supplements the bundled combat catalog.
        // Tests remain independent of an installed full static-data database.
        catalog.Weapons["Bezdnacine"] = new(WeaponPlatform.Unknown, CombatDamageType.Unknown, TypeId: 52316);
        return catalog;
    }

    private static ParsedLogEntry[] Read(string file, LogLanguage expectedLanguage, LogLanguage preference)
    {
        using var input = CompleteLogReader.OpenShared(Path.Combine(AppContext.BaseDirectory, "Fixtures", "logs", file));
        var batch = CompleteLogReader.Read(input, CompleteLogReader.Identity(input), null);
        Assert.False(batch.More); Assert.Equal(0, batch.OversizedLines);
        var header = new LogHeader();
        foreach (var line in batch.Lines) header = EveLogParser.ReadHeader(header, line.Text);
        Assert.Equal("Sample Observer", header.Listener); Assert.Equal(expectedLanguage, header.Language);
        var entries = batch.Lines.Select(line => EveLogParser.Parse(line.Text, header, false,
            Catalog, new HashSet<string>(), preference)).Where(x => x is not null).ToArray();
        Assert.All(entries, entry => Assert.Equal(expectedLanguage, entry.Language));
        return entries;
    }

    [Theory]
    [InlineData(LogLanguage.Automatic)]
    [InlineData(LogLanguage.Russian)]
    public void RecordedRussianMessagesResolveEnglishNames(LogLanguage preference)
    {
        var entries = Read("russian-client.txt", LogLanguage.Russian, preference);
        Assert.Equal(new[] { LogEventKind.Connection, LogEventKind.SystemChange, LogEventKind.Damage,
            LogEventKind.Damage, LogEventKind.Decloak, LogEventKind.Mining, LogEventKind.Mining,
            LogEventKind.MiningStatus }, entries.Select(x => x.EventKind));
        Assert.Equal("Jita", entries[1].SolarSystem); Assert.Equal(30000142, entries[1].SolarSystemId);
        Assert.Equal(130, entries[2].Amount); Assert.Equal(DamageDirection.Incoming, entries[2].Direction);
        Assert.Equal(983, entries[3].Amount); Assert.Equal(DamageDirection.Outgoing, entries[3].Direction);
        Assert.All(entries.Skip(2).Take(2), entry => Assert.Equal(CombatantKind.Npc, entry.Kind));
        Assert.Equal("Nova Light Missile", entries[3].Weapon); Assert.Equal(213, entries[3].DamageSourceTypeId);
        Assert.Equal(DamageTypes.Explosive, entries[3].DamageTypes);
        Assert.Equal(581419, entries[4].MessageId);
        Assert.All(entries.Skip(5).Take(2), entry =>
        { Assert.Equal(1m, entry.Quantity); Assert.Equal("Bezdnacine", entry.ItemName); Assert.Equal(52316, entry.ItemTypeId); });
        Assert.Equal(TimeSpan.FromSeconds(10), entries[6].Timestamp - entries[5].Timestamp);
        Assert.Null(entries[7].Quantity); Assert.Null(entries[7].Direction);
        Assert.StartsWith("Civilian Miner*", entries[7].Text); // Unwrapped stars are literal text.
    }

    [Theory]
    [InlineData(LogLanguage.Automatic)]
    [InlineData(LogLanguage.Chinese)]
    public void RecordedChineseEnglishNameOptionUsesVisibleSystem(LogLanguage preference)
    {
        var entries = Read("chinese-english-names.txt", LogLanguage.Chinese, preference);
        Assert.Equal(new[] { LogEventKind.Connection, LogEventKind.SystemChange }, entries.Select(x => x.EventKind));
        Assert.Equal("Jita", entries[1].SolarSystem); Assert.Equal(30000142, entries[1].SolarSystemId);
        Assert.Contains("Sample Station", entries[1].Text); Assert.DoesNotContain("吉他", entries[1].Text);
        Assert.DoesNotContain("*", entries[1].Text);
    }

    [Theory]
    [InlineData(LogLanguage.English)]
    [InlineData(LogLanguage.Chinese)]
    [InlineData(LogLanguage.Russian)]
    [InlineData(LogLanguage.German)]
    [InlineData(LogLanguage.French)]
    [InlineData(LogLanguage.Japanese)]
    [InlineData(LogLanguage.Korean)]
    [InlineData(LogLanguage.Spanish)]
    public void EnglishNamesAndForeignHintsAreIndependentOfMessageLanguage(LogLanguage language)
    {
        // Controlled template variants model the recorded name-display option
        // across all languages; only the separate reader fixtures are wire evidence.
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "logs", "localization-templates.json")));
        var rows = fixture.RootElement.GetProperty("entries").EnumerateArray().Where(row =>
            row.GetProperty("language").GetString() == language.ToString()
            && row.GetProperty("messageId").GetInt32() is 285197 or 285198 or 1019582 or 238851).ToArray();
        Assert.Equal(4, rows.Length);
        foreach (var row in rows)
        foreach (int markup in new[] { 0, 1, 2 })
        foreach (var preference in new[] { LogLanguage.Automatic, language })
        {
            string Name(string value) => markup == 0 ? value
                : "<localized hint=\"错误提示 &gt; Amarr\">" + value + "*" + (markup == 1 ? "</localized>" : "");
            string text = row.GetProperty("text").GetString()
                .Replace("Sample Pilot [SAMPLE](Rifter)", Name("Overmind Destructor Delta"))
                .Replace("Sample Pilot", Name("Overmind Destructor Delta"))
                .Replace("Sample Module", Name("Nova Light Missile"))
                .Replace("Arkonor", Name("Bezdnacine")).Replace("Jita", Name("Jita"));
            string category = row.GetProperty("category").GetString();
            var entry = EveLogParser.Parse($"[ 2026.01.01 00:00:00 ] ({category}) {text}",
                new("Sample Observer", Language: language), false, Catalog, new HashSet<string>(), preference);
            Assert.Equal(language, entry.Language);
            Assert.Equal(Enum.Parse<LogEventKind>(row.GetProperty("kind").GetString()), entry.EventKind);
            Assert.DoesNotContain("错误提示", entry.Text);
            if (entry.EventKind == LogEventKind.Damage)
            {
                Assert.Equal(Enum.Parse<DamageDirection>(row.GetProperty("direction").GetString()), entry.Direction);
                Assert.Equal(CombatantKind.Npc, entry.Kind); Assert.Equal("Overmind Destructor Delta", entry.Counterparty);
                Assert.Equal("Nova Light Missile", entry.Weapon); Assert.Equal(213, entry.DamageSourceTypeId);
            }
            else if (entry.EventKind == LogEventKind.Mining)
            { Assert.Equal(52316, entry.ItemTypeId); Assert.Equal("Bezdnacine", entry.ItemName); }
            else { Assert.Equal("Jita", entry.SolarSystem); Assert.Equal(30000142, entry.SolarSystemId); }
        }
    }
}
