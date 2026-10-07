using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using EveOPreview.Services.Logs;
using EveOPreview.UI;
using Xunit;

namespace EveOPreview.Tests.Checks;

public sealed class LogTemplateTests
{
    private static readonly EveLogCatalog Catalog = new()
    {
        Systems = new(StringComparer.Ordinal) { ["Jita"] = 30000142 },
        Weapons = new(StringComparer.Ordinal)
        {
            ["Arkonor"] = new(WeaponPlatform.Unknown, CombatDamageType.Unknown, TypeId: 22),
            ["Sample Module"] = new(WeaponPlatform.Railgun, CombatDamageType.Kinetic, TypeId: 42)
        }
    };
    private static readonly HashSet<string> Players = new(StringComparer.Ordinal) { "Sample Pilot" };

    [Theory]
    [InlineData(LogLanguage.English)]
    [InlineData(LogLanguage.Chinese)]
    [InlineData(LogLanguage.Russian)]
    [InlineData(LogLanguage.German)]
    [InlineData(LogLanguage.French)]
    [InlineData(LogLanguage.Japanese)]
    [InlineData(LogLanguage.Korean)]
    [InlineData(LogLanguage.Spanish)]
    public void CustomOverviewFormattingAndDelimitersDoNotChangeEventFields(LogLanguage language)
    {
        using var fixture = Fixture();
        // Controlled variations supplement the recorded overview fixtures. These
        // are display labels, not evidence of a recoverable character identity.
        string[] labels =
        [
            "<fontsize=16><color=0xffcc0000><b><u>Custom - Label ★</u></b></color></fontsize>",
            "<font size=14><color=#ff00ffff><b>Custom &amp; Label ★",
            "<b><i>Custom</i> <CORP> &lt;Alias&gt; * ★</b>",
            "<a href=\"showinfo:1373//1\"><color=0xffffffff>Custom - Label ★</a>"
        ];
        foreach (var row in fixture.RootElement.GetProperty("entries").EnumerateArray()
            .Where(x => x.GetProperty("language").GetString() == language.ToString()))
        foreach (string label in labels)
        {
            string category = row.GetProperty("category").GetString();
            string text = row.GetProperty("text").GetString();
            var baseline = Parse(category, text, language);
            text = text.Replace("Sample Pilot [SAMPLE](Rifter)", label, StringComparison.Ordinal)
                .Replace("Sample Pilot", label, StringComparison.Ordinal)
                .Replace("Sample Object", label, StringComparison.Ordinal);
            var parsed = Parse(category, text, language);
            Assert.Equal(baseline.EventKind, parsed.EventKind);
            Assert.Equal(baseline.Amount, parsed.Amount); Assert.Equal(baseline.Direction, parsed.Direction);
            Assert.Equal(baseline.Effect, parsed.Effect); Assert.Equal(baseline.Weapon, parsed.Weapon);
            Assert.Equal(baseline.Quantity, parsed.Quantity); Assert.Equal(baseline.ResidueQuantity, parsed.ResidueQuantity);
            Assert.Equal(EveLogText.PlainText(text), parsed.Text);
            if (baseline.EventKind is LogEventKind.Damage or LogEventKind.Repair)
                Assert.Equal(EveLogText.PlainText(label), parsed.Counterparty);
        }
    }
    private static JsonDocument Fixture() => JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
        "Fixtures", "logs", "localization-templates.json")));
    private static ParsedLogEntry Parse(string category, string text, LogLanguage language,
        LogLanguage preference = LogLanguage.Automatic) => EveLogParser.Parse(
            $"[ 2026.09.19 12:00:00 ] ({category}) {text}", new("Sample Observer", Language: language), false, Catalog, Players, preference);

    [Theory]
    [InlineData(LogLanguage.English)]
    [InlineData(LogLanguage.Chinese)]
    [InlineData(LogLanguage.Russian)]
    [InlineData(LogLanguage.German)]
    [InlineData(LogLanguage.French)]
    [InlineData(LogLanguage.Japanese)]
    [InlineData(LogLanguage.Korean)]
    [InlineData(LogLanguage.Spanish)]
    public void EveryBundledTemplateParsesWithAutomaticAndManualSelection(LogLanguage language)
    {
        using var fixture = Fixture();
        int count = 0;
        foreach (var row in fixture.RootElement.GetProperty("entries").EnumerateArray()
            .Where(x => x.GetProperty("language").GetString() == language.ToString()))
        {
            int id = row.GetProperty("messageId").GetInt32();
            string text = row.GetProperty("text").GetString(), category = row.GetProperty("category").GetString();
            var kind = Enum.Parse<LogEventKind>(row.GetProperty("kind").GetString());
            foreach (var preference in new[] { LogLanguage.Automatic, language })
            {
                var entry = Parse(category, text, language, preference);
                Assert.NotNull(entry);
                Assert.True(entry.EventKind == kind, $"{language} {id}: expected {kind}, got {entry.EventKind}");
                Assert.Equal(language, entry.Language);
                // Some client IDs have identical visible messages. The recorded
                // ID identifies a matching template, not a recovered wire ID.
                Assert.NotNull(entry.MessageId);
                Assert.Equal(EveLogText.PlainText(text), entry.Text);
                if (kind is LogEventKind.Damage or LogEventKind.Repair)
                {
                    Assert.Equal(214, entry.Amount);
                    Assert.Equal(Enum.Parse<DamageDirection>(row.GetProperty("direction").GetString()), entry.Direction);
                    Assert.Equal(Enum.Parse<CombatEffect>(row.GetProperty("effect").GetString()), entry.Effect);
                    Assert.Equal("Sample Pilot", entry.Counterparty);
                    Assert.Equal("Sample Module", entry.Weapon);
                }
                else
                {
                    Assert.Null(entry.Direction); Assert.Equal(0, entry.Amount);
                    if (kind == LogEventKind.SystemChange) Assert.Equal(30000142, entry.SolarSystemId);
                    else Assert.Null(entry.SolarSystemId);
                    if (kind == LogEventKind.Mining)
                    {
                        Assert.Equal(214m, entry.Quantity); Assert.Equal("Arkonor", entry.ItemName); Assert.Equal(22, entry.ItemTypeId);
                        Assert.Equal(id == 530055 ? 7m : (decimal?)null, entry.ResidueQuantity);
                    }
                    if (kind == LogEventKind.MiningResidue) Assert.Equal(7m, entry.Quantity);
                    if (kind == LogEventKind.Bounty) { Assert.Equal(225000m, entry.Quantity); Assert.Equal("ISK", entry.Unit); }
                    if (kind == LogEventKind.Capacitor) { Assert.Equal(214m, entry.Quantity); Assert.Equal("GJ", entry.Unit); }
                }
            }
            var plain = Parse(category, EveLogText.PlainText(text), language);
            if (language == LogLanguage.Japanese && kind == LogEventKind.Damage)
                Assert.Equal(LogEventKind.Unknown, plain.EventKind); // Identical source/target wording without color evidence.
            else Assert.True(plain.EventKind == kind, $"Plain {language} {id}: expected {kind}, got {plain.EventKind}");
            // Presentation is shared across message types and languages. Exercise
            // the same omitted closing tags seen in client output on every template.
            string decorated = text;
            foreach (string name in new[] { "Sample Pilot", "Sample Module", "Sample Object", "Sample Station", "Arkonor", "Jita" })
                decorated = decorated.Replace(name, "<localized hint=\"Hidden &gt; translation\"><font size=12>" + name + "*</font>", StringComparison.Ordinal);
            var unclosed = Parse(category, decorated, language);
            Assert.True(unclosed.EventKind == kind, $"Unclosed markup {language} {id}: expected {kind}, got {unclosed.EventKind}");
            Assert.Equal(EveLogText.PlainText(text), unclosed.Text);
            Assert.Equal(LogEventKind.Unknown, Parse("unrecognized-category", text, language).EventKind);
            count++;
        }
        Assert.True(count >= 150, $"Incomplete template fixtures for {language}: {count}");
    }

    [Fact]
    public void SupportedLanguagesHeadersAndLocalMessagesAreSelfContained()
    {
        using var fixture = Fixture();
        Assert.Equal(9, Enum.GetValues<LogLanguage>().Length); // Automatic + the eight game languages.
        Assert.DoesNotContain("Italian", Enum.GetNames<LogLanguage>());
        foreach (var row in fixture.RootElement.GetProperty("headers").EnumerateArray())
        {
            var language = Enum.Parse<LogLanguage>(row.GetProperty("language").GetString());
            var header = EveLogParser.ReadHeader(new(), row.GetProperty("listener").GetString() + ": Sample Observer");
            header = EveLogParser.ReadHeader(header, row.GetProperty("session").GetString() + ": 2026.09.19 12:00:00");
            header = EveLogParser.ReadHeader(header, row.GetProperty("channel").GetString() + ": " + row.GetProperty("local").GetString());
            Assert.Equal(language, header.Language); Assert.NotNull(header.Session);
            Assert.True(EveLogParser.IsLocalFile(header.Channel + "_20260919_120000.txt"));
            string line = "[ 2026.09.19 12:00:00 ] " + row.GetProperty("sender").GetString() + " > " + row.GetProperty("change").GetString();
            var entry = EveLogParser.Parse(line, header, true, Catalog, Players);
            Assert.NotNull(entry); Assert.Equal(language, entry.Language); Assert.Equal(30000142, entry.SolarSystemId);
            Assert.Null(EveLogParser.Parse(line.Replace(row.GetProperty("sender").GetString(), "Sample Pilot"), header, true, Catalog, Players));
        }
    }

    [Fact]
    public void JapaneseDamageRequiresColorOnTheAmountAndPreservesEvidenceDuringMetadataRefresh()
    {
        const string body = "214 から Sample Pilot - Sample Module - 直撃";
        foreach (var direction in Enum.GetValues<DamageDirection>())
        {
            string color = direction == DamageDirection.Incoming ? "0xffcc0000" : "0xff00ffff";
            var entry = Parse("combat", "<font size=12><color=" + color + ">" + body, LogLanguage.Japanese);
            Assert.Equal(direction, entry.Direction);
            var saved = JsonSerializer.Deserialize<ParsedLogEntry>(JsonSerializer.Serialize(entry));
            var method = typeof(EveLogParser).GetMethod("Reparse", BindingFlags.Static | BindingFlags.NonPublic);
            var refreshed = (ParsedLogEntry)method.Invoke(null, new object[] { saved, Catalog, Players });
            Assert.Equal(direction, refreshed.Direction); Assert.Equal(entry.DamageSourceTypeId, refreshed.DamageSourceTypeId);
        }
        Assert.Null(Parse("combat", body, LogLanguage.Japanese).Direction);
        Assert.Null(Parse("combat", body.Replace("Sample Pilot", "<color=0xff00ffff>Sample Pilot"), LogLanguage.Japanese).Direction);
        Assert.Null(Parse("combat", "<color=0xff00ffff><color=0xffffffff>" + body, LogLanguage.Japanese).Direction);
        Assert.Equal(LogEventKind.Unknown, Parse("combat", "214 攻撃者：Sample Pilot", LogLanguage.Japanese).EventKind);
    }

    [Fact]
    public void ClientWordOrderAndCombinedMiningValuesAreNotInferredFromEnglish()
    {
        var incoming = Parse("combat", "214의 피해를 Sample Pilot에 의해 입음 - Sample Module", LogLanguage.Korean);
        var outgoing = Parse("combat", "214의 피해를 Sample Pilot에게 입힘 - Sample Module", LogLanguage.Korean);
        Assert.Equal(DamageDirection.Incoming, incoming.Direction); Assert.Equal(DamageDirection.Outgoing, outgoing.Direction);
        Assert.Equal("Sample Pilot", incoming.Counterparty); Assert.Equal("Sample Pilot", outgoing.Counterparty);
        var repair = Parse("combat", "214リモートアーマーリペアをSample Pilot - Sample Moduleから受けました", LogLanguage.Japanese);
        Assert.Equal(CombatEffect.ArmorRepair, repair.Effect); Assert.Equal(DamageDirection.Incoming, repair.Direction);
        var mining = Parse("mining", "你开采了214单位的Arkonor，产生了7单位的残渣。", LogLanguage.Chinese);
        Assert.Equal(214m, mining.Quantity); Assert.Equal(7m, mining.ResidueQuantity); Assert.Equal(22, mining.ItemTypeId);
        Assert.Equal(LogEventKind.Unknown, Parse("mining", mining.Text, LogLanguage.English, LogLanguage.English).EventKind);
    }

    [Fact]
    public void SharedFrenchAndSpanishWordingDoesNotInventAFileLanguage()
    {
        const string text = "214 de Sample Pilot - Sample Module";
        Assert.Equal(LogLanguage.Automatic, Parse("combat", text, LogLanguage.Automatic).Language);
        Assert.Equal(DamageDirection.Incoming, Parse("combat", text, LogLanguage.Automatic).Direction);
        Assert.Equal(LogLanguage.Spanish, Parse("combat", text, LogLanguage.Spanish).Language);
        Assert.Equal(LogLanguage.French, Parse("combat", text, LogLanguage.French).Language);
    }

    [Theory]
    [InlineData(LogLanguage.English, "214 from Sample NPC - Hits")]
    [InlineData(LogLanguage.Chinese, "214来自 Sample NPC - 命中")]
    [InlineData(LogLanguage.German, "214 von Sample NPC - Treffer")]
    [InlineData(LogLanguage.Russian, "214 из Sample NPC - Попал")]
    [InlineData(LogLanguage.French, "214 de Sample NPC - Touche")]
    [InlineData(LogLanguage.Spanish, "214 de Sample NPC - Impacta")]
    [InlineData(LogLanguage.Korean, "214의 피해를 Sample NPC에 의해 입음 - 명중")]
    [InlineData(LogLanguage.Japanese, "<color=0xffcc0000>214 から Sample NPC - 直撃")]
    public void NativeHitQualityCanResolveKnownNpcAttackEvidence(LogLanguage language, string text)
    {
        var catalog = new EveLogCatalog
        {
            NpcNames = new(StringComparer.Ordinal) { "Sample NPC" },
            NpcAttacks = new(StringComparer.Ordinal) { ["Sample NPC"] = new(WeaponPlatform.Laser, CombatDamageType.EM, DamageTypes.EM, 42) }
        };
        var entry = EveLogParser.Parse("[ 2026.01.01 00:00:00 ] (combat) " + text,
            new("Sample Observer", Language: language), false, catalog, Players);
        Assert.Equal(CombatantKind.Npc, entry.Kind); Assert.Equal(DamageEvidence.NpcAttack, entry.DamageEvidence);
        Assert.Equal(DamageTypes.EM, entry.DamageTypes); Assert.Null(entry.Weapon);
    }
}
