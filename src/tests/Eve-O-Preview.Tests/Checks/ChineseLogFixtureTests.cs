using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EveOPreview.Services.Logs;
using EveOPreview.UI;
using Xunit;

namespace EveOPreview.Tests.Checks;

public sealed class ChineseLogFixtureTests
{
    private static readonly EveLogCatalog Catalog = EveLogCatalog.Load();
    private static ParsedLogEntry[] Read(string name, bool chat = false, LogLanguage preference = LogLanguage.Automatic)
    {
        using var input = CompleteLogReader.OpenShared(Path.Combine(AppContext.BaseDirectory, "Fixtures", "logs", name));
        var batch = CompleteLogReader.Read(input, CompleteLogReader.Identity(input), null);
        Assert.False(batch.More); Assert.Equal(0, batch.OversizedLines);
        var header = new LogHeader();
        foreach (var line in batch.Lines) header = EveLogParser.ReadHeader(header, line.Text);
        Assert.Equal("Sample Observer", header.Listener);
        var entries = batch.Lines.Select(line => EveLogParser.Parse(line.Text, header, chat,
            Catalog, new HashSet<string>(), preference)).Where(x => x is not null).ToArray();
        Assert.All(entries, entry => Assert.Equal(LogLanguage.Chinese, entry.Language));
        return entries;
    }

    [Theory]
    [InlineData(LogLanguage.Automatic)]
    [InlineData(LogLanguage.Chinese)]
    public void RecordedActivitiesKeepMiningAndDecloakSeparateFromDamage(LogLanguage preference)
    {
        var entries = Read("chinese-activities.txt", preference: preference);
        Assert.Equal(new[] { LogEventKind.CombatMiss, LogEventKind.Damage, LogEventKind.Damage,
            LogEventKind.CombatMiss, LogEventKind.Mining, LogEventKind.Decloak }, entries.Select(x => x.EventKind));
        Assert.Equal(23, entries[1].Amount); Assert.Equal(DamageDirection.Incoming, entries[1].Direction);
        Assert.Equal(2, entries[2].Amount); Assert.Equal(DamageDirection.Outgoing, entries[2].Direction);
        Assert.Equal(1m, entries[4].Quantity); Assert.Equal("贝兹岩", entries[4].ItemName);
        Assert.Equal("units", entries[4].Unit); Assert.Null(entries[4].ResidueQuantity);
        Assert.Equal(581419, entries[5].MessageId);
        Assert.Equal("由于附近的空间站（加达里2）的影响，你的隐形状态已解除。", entries[5].Text);
        Assert.All(entries.Where(x => x.EventKind != LogEventKind.Damage), entry =>
        { Assert.Null(entry.Direction); Assert.Equal(0, entry.Amount); });
        Assert.All(entries, entry => { Assert.DoesNotContain("<", entry.Text); Assert.DoesNotContain("*", entry.Text); });
        Assert.Equal(TimeSpan.FromMinutes(11) + TimeSpan.FromSeconds(31), entries[^1].Timestamp - entries[0].Timestamp);
    }

    [Theory]
    [InlineData(LogLanguage.Automatic)]
    [InlineData(LogLanguage.Chinese)]
    public void RecordedOverviewRepairsAndLocalizedBountiesParse(LogLanguage preference)
    {
        var entries = Read("chinese-overview.txt", preference: preference);
        Assert.Equal(8, entries.Length);
        Assert.All(entries.Take(2), entry =>
        {
            Assert.Equal(LogEventKind.Bounty, entry.EventKind); Assert.Equal(567951, entry.MessageId);
            Assert.Equal("ISK", entry.Unit); Assert.Null(entry.Direction); Assert.Equal(0, entry.Amount);
        });
        Assert.Equal(281m, entries[0].Quantity); Assert.Equal(60_000_000m, entries[1].Quantity);
        Assert.Equal(LogEventKind.Cloak, entries[2].EventKind); Assert.Null(entries[2].Direction);
        Assert.Equal(new[] { 85d, 35d, 0d, 0d, 14d }, entries.Skip(3).Select(x => x.Amount));
        Assert.Equal(new[] { CombatEffect.ShieldRepair, CombatEffect.ArmorRepair, CombatEffect.ShieldRepair,
            CombatEffect.ArmorRepair, CombatEffect.HullRepair }, entries.Skip(3).Select(x => x.Effect));
        Assert.Equal(new[] { DamageDirection.Incoming, DamageDirection.Incoming, DamageDirection.Outgoing,
            DamageDirection.Outgoing, DamageDirection.Incoming }, entries.Skip(3).Select(x => x.Direction!.Value));
        Assert.All(entries.Skip(3), entry =>
        {
            Assert.Equal(LogEventKind.Repair, entry.EventKind);
            Assert.DoesNotContain("<", entry.Counterparty); Assert.DoesNotContain("*", entry.Counterparty);
            Assert.Contains("[SAMPLE]", entry.Text); Assert.Equal(DamageTypes.None, entry.DamageTypes);
        });
        Assert.Equal("矮脚鸡级 [SAMPLE] [SAMPLE]", entries[3].Counterparty);
        Assert.Null(entries[3].Weapon); // The custom overview omitted the module.
        Assert.Contains("Sample Pilot", entries[5].Counterparty);
    }

    [Theory]
    [InlineData(LogLanguage.Automatic)]
    [InlineData(LogLanguage.Chinese)]
    public void RecordedUtf16LocalUsesChineseMessageWithEnglishHeader(LogLanguage preference)
    {
        var entry = Assert.Single(Read("chinese-local.txt", chat: true, preference: preference));
        Assert.Equal(LogEventKind.SystemChange, entry.EventKind);
        Assert.Equal("吉他", entry.SolarSystem); Assert.Equal(30000142, entry.SolarSystemId);
        Assert.Equal(TimeSpan.FromSeconds(4), entry.Timestamp.TimeOfDay);
    }

    [Fact]
    public void UnwrappedLocalMarkerStillRequiresSystemSenderAndKnownSystem()
    {
        const string text = "[ 2026.01.01 00:00:04 ] EVE系统 > 频道更换为本地：吉他*";
        var header = new LogHeader("Sample Observer", "本地");
        Assert.Null(EveLogParser.Parse(text.Replace("EVE系统", "Sample Pilot"), header, true, Catalog, new HashSet<string>()));
        Assert.Null(EveLogParser.Parse(text.Replace("吉他", "Unknown System"), header, true, Catalog, new HashSet<string>()));
    }

    [Theory]
    [InlineData(LogLanguage.Automatic)]
    [InlineData(LogLanguage.Chinese)]
    public void RecordedEffectsWithCustomLabelsDoNotBecomeDamageOrRepairs(LogLanguage preference)
    {
        var entries = Read("chinese-effects.txt", preference: preference);
        Assert.Equal(new[] { LogEventKind.SystemChange, LogEventKind.WarpDisruption, LogEventKind.Capacitor,
            LogEventKind.Navigation, LogEventKind.ElectronicWarfare, LogEventKind.Drone }, entries.Select(x => x.EventKind));
        Assert.Equal("Jita", entries[0].SolarSystem); Assert.Equal(30000142, entries[0].SolarSystemId);
        Assert.Equal(165m, entries[2].Quantity); Assert.Equal("GJ", entries[2].Unit);
        Assert.Contains("Sample Attacker", entries[1].Text); Assert.Contains("Sample Defender", entries[1].Text);
        Assert.Contains("Sample Jammer", entries[4].Text);
        Assert.All(entries, entry =>
        {
            Assert.Null(entry.Direction); Assert.Equal(0, entry.Amount);
            Assert.DoesNotContain("<", entry.Text); Assert.DoesNotContain("*", entry.Text);
        });
    }

    [Fact]
    public void ChineseClientSampleResolvesUnclosedLocalizedNamesAndNonCombatEvents()
    {
        // Anonymized client output, with dates rebased and no personal identity,
        // original filename, source path or player chat retained.
        string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "logs", "chinese-client.txt");
        var catalog = EveLogCatalog.Load(); // Bundled SDE aliases; no installed client/export needed.
        using var input = CompleteLogReader.OpenShared(path);
        var batch = CompleteLogReader.Read(input, CompleteLogReader.Identity(input), null);
        Assert.False(batch.More); Assert.Equal(0, batch.OversizedLines);
        var header = new LogHeader();
        foreach (var line in batch.Lines) header = EveLogParser.ReadHeader(header, line.Text);
        Assert.Equal("Sample Observer", header.Listener); Assert.Equal(LogLanguage.Chinese, header.Language);
        var entries = batch.Lines.Select(x => EveLogParser.Parse(x.Text, header, false, catalog, new HashSet<string>()))
            .Where(x => x is not null).ToArray();
        Assert.Equal(new[] { LogEventKind.Connection, LogEventKind.Connection, LogEventKind.SystemChange,
            LogEventKind.Module, LogEventKind.Module, LogEventKind.Targeting, LogEventKind.Damage, LogEventKind.Damage },
            entries.Select(x => x.EventKind));
        Assert.Equal("吉他", entries[2].SolarSystem); Assert.Equal(30000142, entries[2].SolarSystemId);
        Assert.Equal("正在将高级重型攻击导弹载入重型攻击导弹发射器，预计于10秒后完成。", entries[3].Text);
        Assert.All(entries, entry =>
        {
            Assert.Equal(LogLanguage.Chinese, entry.Language);
            Assert.DoesNotContain("<", entry.Text); Assert.DoesNotContain("*", entry.Text);
        });
        Assert.All(entries.Skip(6), entry =>
        {
            Assert.Equal(DamageDirection.Outgoing, entry.Direction); Assert.Equal(0, entry.Amount);
            Assert.Equal("塔拉岩（2级）", entry.Counterparty); Assert.Equal("炼狱狂乱重型攻击导弹", entry.Weapon);
            Assert.Equal(DamageTypes.Thermal, entry.DamageTypes);
            Assert.Equal(24486, entry.DamageSourceTypeId);
        });
        Assert.Equal("Sample * Pilot", EveLogText.PlainText("Sample * Pilot"));
    }
}
