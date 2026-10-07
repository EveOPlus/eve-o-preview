using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EveOPreview.Configuration.Implementation;
using EveOPreview.Services.Logs;
using EveOPreview.UI;
using Serilog;
using Xunit;

namespace EveOPreview.Tests.Checks;

public sealed class LogLanguageTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static EveLogCatalog Catalog() => new()
    {
        Systems = new(StringComparer.OrdinalIgnoreCase) { ["Jita"] = 30000142, ["吉他"] = 30000142, ["Amarr"] = 30002187 },
        Weapons = new(StringComparer.OrdinalIgnoreCase) { ["Scourge Rocket"] = new(WeaponPlatform.Rocket, CombatDamageType.Kinetic, TypeId: 266),
            ["Arkonor"] = new(WeaponPlatform.Unknown, CombatDamageType.Unknown, TypeId: 22),
            ["艾克诺岩"] = new(WeaponPlatform.Unknown, CombatDamageType.Unknown, TypeId: 22) }
    };
    private static ParsedLogEntry Parse(string category, string text, LogHeader header = null, LogLanguage preference = LogLanguage.Automatic) =>
        EveLogParser.Parse($"[ 2026.01.01 12:00:00 ] ({category}) {text}", header ?? new("Sample Observer"), false,
            Catalog(), new HashSet<string>(), preference);

    [Theory]
    [InlineData("Listener", LogLanguage.English, "12 from Sample Target - Scourge Rocket - Hits")]
    [InlineData("收听者", LogLanguage.Chinese, "12来自 Sample Target - Scourge Rocket - Hits")]
    [InlineData("Слушатель", LogLanguage.Russian, "12 из Sample Target - Scourge Rocket - Hits")]
    [InlineData("Empfänger", LogLanguage.German, "12 von Sample Target - Scourge Rocket - Hits")]
    [InlineData("Auditeur", LogLanguage.French, "12 de Sample Target - Scourge Rocket - Hits")]
    [InlineData("傍聴者", LogLanguage.Japanese, "<color=0xffcc0000>12 から Sample Target - Scourge Rocket - 直撃")]
    public void DetectsEachFileWithoutUsingTheWorkspaceOrOperatingSystemLanguage(string key, LogLanguage language, string body)
    {
        var header = EveLogParser.ReadHeader(new(), key + "： Sample Observer");
        Assert.Equal(language, header.Language);
        Assert.Equal(header, JsonSerializer.Deserialize<LogHeader>(JsonSerializer.Serialize(header)));
        var parsed = Parse("combat", body, header);
        Assert.Equal(language, parsed.Language); Assert.Equal(LogEventKind.Damage, parsed.EventKind);
        Assert.Equal(DamageDirection.Incoming, parsed.Direction); Assert.Equal(12, parsed.Amount);
        Assert.Equal(CombatDamageType.Kinetic, parsed.DamageType);
        // Older checkpoints and mixed-language headers still have body-based detection.
        // French and Spanish share the damage fragment "de". Without the header,
        // the direction is known but the language cannot be distinguished.
        Assert.Equal(language == LogLanguage.French ? LogLanguage.Automatic : language,
            Parse("combat", body, new("Sample Observer", Language: LogLanguage.English)).Language);
    }

    [Fact]
    public void ManualOverrideIsStrictAndDoesNotWeakenListenerAttribution()
    {
        const string body = "125 对 Sample Target - Scourge Rocket - Hits";
        Assert.Equal(LogEventKind.Unknown, Parse("combat", body, preference: LogLanguage.English).EventKind);
        Assert.Equal(DamageDirection.Outgoing, Parse("combat", body, preference: LogLanguage.Chinese).Direction);
        Assert.Null(Parse("combat", body, new(), LogLanguage.Chinese));
        var header = EveLogParser.ReadHeader(new("Sample Observer"), "收听者: Different Observer");
        Assert.True(header.Ambiguous); Assert.Null(Parse("combat", body, header, LogLanguage.Chinese));
        Assert.Equal(LogLanguage.Automatic, JsonSerializer.Deserialize<LogHeader>("{\"Listener\":\"Sample Observer\"}").Language);
    }

    [Theory]
    [InlineData("500远程护盾回充增量由Sample Target", LogLanguage.Chinese, CombatEffect.ShieldRepair, DamageDirection.Incoming)]
    [InlineData("500远程装甲维修量至Sample Target", LogLanguage.Chinese, CombatEffect.ArmorRepair, DamageDirection.Outgoing)]
    [InlineData("500远程结构维修量由Sample Target", LogLanguage.Chinese, CombatEffect.HullRepair, DamageDirection.Incoming)]
    [InlineData("500 Panzerungs-Fernreparatur von Sample Target", LogLanguage.German, CombatEffect.ArmorRepair, DamageDirection.Incoming)]
    [InlineData("500 Rumpf-Fernreparatur zu Sample Target", LogLanguage.German, CombatEffect.HullRepair, DamageDirection.Outgoing)]
    [InlineData("500 единиц запаса прочности щитов получено накачкой от Sample Target", LogLanguage.Russian, CombatEffect.ShieldRepair, DamageDirection.Incoming)]
    [InlineData("500 единиц запаса прочности брони отремонтировано Sample Target", LogLanguage.Russian, CombatEffect.ArmorRepair, DamageDirection.Outgoing)]
    [InlineData("500 points de structure transférés à distance à Sample Target", LogLanguage.French, CombatEffect.HullRepair, DamageDirection.Outgoing)]
    public void RepairWordingHasExplicitEffectAndDirection(string body, LogLanguage language, CombatEffect effect, DamageDirection direction)
    {
        var parsed = Parse("combat", body);
        Assert.Equal(LogEventKind.Repair, parsed.EventKind); Assert.Equal(language, parsed.Language);
        Assert.Equal(effect, parsed.Effect); Assert.Equal(direction, parsed.Direction); Assert.Equal(500, parsed.Amount);
    }

    [Fact]
    public void ExtractsVisibleOverviewTextWithoutDependingOnColorsOrHintLanguage()
    {
        const string raw = "<color=#123456><b>1,234.5</b> <font size=10>to</font> <b>Sample &amp; Co[TEST](Ship)</b> - <localized hint=\"Untrusted > alternate\"><font size=12>Scourge Rocket*</localized> - Hits";
        var parsed = Parse("combat", raw);
        Assert.Equal("Sample & Co", parsed.Counterparty); Assert.Equal(1234.5, parsed.Amount);
        Assert.Equal("Scourge Rocket", parsed.Weapon); Assert.Equal(CombatDamageType.Kinetic, parsed.DamageType);
        Assert.DoesNotContain("<font", parsed.Text); Assert.DoesNotContain("Untrusted", parsed.Text);
        Assert.Equal("<CORP> Pilot* & <b>literal</b>", EveLogText.PlainText("<CORP> Pilot* &amp; &lt;b&gt;literal&lt;/b&gt;"));
        Assert.Equal("艾克诺岩", EveLogText.PlainText("<localized hint='Arkonor'>艾克诺岩*"));
        Assert.Equal("Arkonor", EveLogText.PlainText("<localized hint='艾克诺岩'>Arkonor*</localized>"));
    }

    [Theory]
    [InlineData("Local", "EVE System", "Channel changed to Local : Jita", LogLanguage.English)]
    [InlineData("本地", "EVE系统", "频道更换为本地：<localized hint=\"Jita\">吉他*</localized>", LogLanguage.Chinese)]
    [InlineData("Lokal", "EVE-System", "Chatkanal geändert zu Lokal : Jita", LogLanguage.German)]
    [InlineData("Локальный", "Система EVE", "Канал изменен на Локальный : Jita", LogLanguage.Russian)]
    public void LocalChangesRequireExactSystemSenderChannelAndSdeName(string channel, string sender, string message, LogLanguage language)
    {
        ParsedLogEntry Read(string author, string room, string text) => EveLogParser.Parse(
            $"[ 2026.01.01 12:00:00 ] {author} > {text}", new("Sample Observer", room), true, Catalog(), new HashSet<string>());
        var location = Read(sender, channel, message);
        Assert.Equal(30000142, location.SolarSystemId); Assert.Equal(language, location.Language);
        Assert.Equal(LogEventKind.SystemChange, location.EventKind);
        Assert.Null(Read("Sample Player", channel, sender + " > " + message));
        Assert.Null(Read(sender, "Corporation", message));
        Assert.Null(Read(sender, channel, message.Replace("Jita", "Unlisted").Replace("吉他", "Unlisted")));
    }

    [Theory]
    [InlineData("None", "Jumping from Amarr to Jita", LogEventKind.SystemChange, LogLanguage.English)]
    [InlineData("None", "Undocking from Sample Station to Jita solar system.", LogEventKind.SystemChange, LogLanguage.English)]
    [InlineData("None", "从 Amarr 跳到 吉他", LogEventKind.SystemChange, LogLanguage.Chinese)]
    [InlineData("notify", "Your cloak deactivates due to proximity to a nearby Stargate (Gallente System).", LogEventKind.Decloak, LogLanguage.English)]
    [InlineData("notify", "由于附近物体的影响，你的隐形状态已解除。", LogEventKind.Decloak, LogLanguage.Chinese)]
    [InlineData("notify", "Маскировка выключается: вы подлетели к объекту.", LogEventKind.Decloak, LogLanguage.Russian)]
    [InlineData("notify", "Ihr Tarnmodul wird aufgrund des Abstands zu einem nahe gelegenen Objekt deaktiviert.", LogEventKind.Decloak, LogLanguage.German)]
    public void NonCombatMessagesBecomeTypedEvents(string category, string text, LogEventKind kind, LogLanguage language)
    {
        var entry = Parse(category, text);
        Assert.Equal(kind, entry.EventKind); Assert.Equal(language, entry.Language);
        Assert.Null(entry.Direction); Assert.Equal(0, entry.Amount);
        Assert.Equal(LogEventKind.Unknown, Parse("question", text).EventKind);
    }

    [Theory]
    [InlineData("You mined <font size=12><color=#ff8dc169>214<color=0x77ffffff><font size=10> units of <color=0xffffffff><font size=12>Arkonor", 214, LogLanguage.English)]
    [InlineData("<color=#fff0ff45>Critical mining success!<color=0x77ffffff><font size=10> You mined an additional <color=#fff0ff45><font size=12>32<color=0x77ffffff><font size=10> units of <color=0xffffffff><font size=12>Arkonor", 32, LogLanguage.English)]
    [InlineData("你挖掘到214单位的<localized hint=\"Arkonor\">艾克诺岩*</localized>", 214, LogLanguage.Chinese)]
    [InlineData("出现采矿暴击！你额外挖掘到32单位的艾克诺岩", 32, LogLanguage.Chinese)]
    [InlineData("Sie haben 214 Einheiten Arkonor abgebaut", 214, LogLanguage.German)]
    [InlineData("Вы добыли 214 ед. ресурса Arkonor", 214, LogLanguage.Russian)]
    public void MiningHasSeparateQuantityAndResolvesVisibleSdeAlias(string body, int quantity, LogLanguage language)
    {
        var parsed = Parse("mining", body);
        Assert.Equal(LogEventKind.Mining, parsed.EventKind); Assert.Equal(language, parsed.Language);
        Assert.Equal((decimal)quantity, parsed.Quantity); Assert.Equal("units", parsed.Unit); Assert.Equal(22, parsed.ItemTypeId);
        Assert.Null(parsed.Direction); Assert.Equal(0, parsed.Amount);
    }

    [Fact]
    public void BountyAccrualIsNotDamageAndAmbiguousNumbersStayUnrecognized()
    {
        var residue = Parse("mining", "Additional 1 units depleted from asteroid as residue");
        Assert.Equal(LogEventKind.MiningResidue, residue.EventKind); Assert.Equal(1m, residue.Quantity);
        Assert.Null(residue.ItemName); Assert.Null(residue.Direction);
        var bounty = Parse("bounty", "<font size=12><b><color=0xff00aa00>225,000 ISK</b><color=0x77ffffff> added to next bounty payout (payment adjusted)");
        Assert.Equal(LogEventKind.Bounty, bounty.EventKind); Assert.Equal(225000m, bounty.Quantity); Assert.Equal("ISK", bounty.Unit);
        Assert.Null(bounty.Direction); Assert.Equal(0, bounty.Amount);
        foreach (string invalid in new[] { "12,34 ISK", "-123 ISK", "10 ISK and 20 ISK" })
            Assert.Equal(LogEventKind.Unknown, Parse("bounty", invalid).EventKind);
        Assert.Equal(LogEventKind.Unknown, Parse("combat", "1,5 from Sample Target").EventKind);
        Assert.Equal(LogEventKind.Unknown, Parse("combat", "100 toad").EventKind);
        Assert.Equal(LogEventKind.Unknown, Parse("notify", "Ihr Tarnmodul wird aufgrund des Abstands überprüft.").EventKind);
        Assert.Equal(LogEventKind.Unknown, Parse("combat", "10 to " + new string('x', 5000)).EventKind);
    }

    [Fact]
    public void TypedEventsSurviveStorageWithoutChangingDamageOrRepairTotals()
    {
        using var store = new CombatLogStore(":memory:", Now.AddSeconds(-1));
        var header = new LogHeader("Sample Observer", Language: LogLanguage.English);
        var cursor = new LogCursor("sample", 0, 1000, "utf-8", "");
        var entries = new[] { Parse("combat", "100 from Sample Target"), Parse("bounty", "225,000 ISK added to next bounty payout"),
            Parse("mining", "You mined 214 units of Arkonor"), Parse("None", "Jumping from Amarr to Jita") }
            .Select((entry, index) => new PositionedLogEntry(index, entry)).ToArray();
        store.Commit(new("sample.txt", cursor, header), entries); store.Commit(new("sample.txt", cursor, header), entries);
        var snapshot = store.Snapshot(Now, 10, "", "", new Dictionary<string, long?>());
        var character = Assert.Single(snapshot.Characters);
        Assert.Equal(100, character.Categories.Sum(x => x.Total.Incoming));
        Assert.Equal(0, character.Activity.Repairs.Sum(x => x.Total.Incoming + x.Total.Outgoing));
        Assert.Contains(snapshot.RecentEntries, x => x.EventKind == LogEventKind.Mining && x.Quantity == 214m && x.ItemTypeId == 22);
        Assert.Equal(LogLanguage.English, store.ReadFile("sample").Header.Language);
    }

    [Fact]
    public void LogLanguagePreferenceIsGlobalAndRoundTripsIndependentlyOfUiLanguage()
    {
        using var logger = new LoggerConfiguration().CreateLogger();
        string directory = Path.Combine(Path.GetTempPath(), "eve-log-language-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(directory);
            string file = Path.Combine(directory, "settings.json");
            var settings = new ApplicationPreferences(file, logger);
            Assert.Equal(LogLanguage.Automatic, settings.CombatLogs.Language);
            settings.SetCombatLogs(new() { Language = LogLanguage.Chinese });
            var reloaded = new ApplicationPreferences(file, logger);
            Assert.Equal(LogLanguage.Chinese, reloaded.CombatLogs.Language); Assert.Equal(settings.UiLanguage, reloaded.UiLanguage);
            Assert.Throws<ArgumentException>(() => settings.SetCombatLogs(new() { Language = (LogLanguage)999 }));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task ReaderAppliesOverrideToNewEntriesAndPublishesTypedEventsWithoutReplayingHistory()
    {
        using var logger = new LoggerConfiguration().CreateLogger();
        string root = Path.Combine(Path.GetTempPath(), "eve-language-reader-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "Gamelogs")); Directory.CreateDirectory(Path.Combine(root, "Chatlogs"));
        var preferences = new ApplicationPreferences(Path.Combine(root, "settings.json"), logger);
        preferences.SetCombatLogs(new() { Enabled = true, Directory = root, Language = LogLanguage.English });
        var received = new ConcurrentQueue<ParsedLogEntry>();
        var service = new CombatLogService(preferences, null, logger, Path.Combine(root, "history.sqlite"), Catalog(), () => Now);
        service.LogEvent += received.Enqueue;
        async Task Wait(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (!condition() && DateTime.UtcNow < deadline) await Task.Delay(25, TestContext.Current.CancellationToken);
            Assert.True(condition(), "Reader did not publish the expected state.");
        }
        try
        {
            string path = Path.Combine(root, "Gamelogs", "sample.txt");
            await File.WriteAllTextAsync(path, "Listener: Sample Observer\n[ 2026.01.01 12:00:00 ] (combat) 100来自 Sample Target\n", TestContext.Current.CancellationToken);
            await Wait(() => service.ReadLogs().RecentEntries.Count == 1);
            Assert.Equal(LogEventKind.Unknown, service.ReadLogs().RecentEntries[0].EventKind); Assert.Empty(received);
            Assert.True((await service.SaveLogSettingsAsync(preferences.CombatLogs with { Language = LogLanguage.Chinese })).Success);
            await service.RescanLogsAsync();
            await File.AppendAllTextAsync(path, "[ 2026.01.01 12:00:00 ] (combat) 50来自 Sample Target\n[ 2026.01.01 12:00:00 ] (mining) 你挖掘到214单位的艾克诺岩\n", TestContext.Current.CancellationToken);
            string local = Path.Combine(root, "Chatlogs", "本地_sample.txt");
            await File.WriteAllTextAsync(local, "Channel Name: 本地\n收听者: Sample Observer\n[ 2026.01.01 12:00:00 ] EVE系统 > 频道更换为本地：吉他\n", System.Text.Encoding.Unicode, TestContext.Current.CancellationToken);
            await Wait(() => service.ReadLogs().RecentEntries.Count == 4 && received.Count == 3);
            Assert.Contains(service.ReadLogs().RecentEntries, x => x.EventKind == LogEventKind.Unknown);
            Assert.Contains(received, x => x.EventKind == LogEventKind.Mining && x.ItemTypeId == 22);
            var character = Assert.Single(service.ReadLogs().Characters);
            Assert.Equal(50, character.Categories.Sum(x => x.Total.Incoming)); Assert.Equal(30000142, character.SolarSystemId);
            await service.RescanLogsAsync();
            Assert.Equal(3, received.Count); // Existing positions survive a language edit/reconciliation.
        }
        finally
        {
            service.Dispose(); await service.Completion.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Directory.Delete(root, true);
        }
    }
}
