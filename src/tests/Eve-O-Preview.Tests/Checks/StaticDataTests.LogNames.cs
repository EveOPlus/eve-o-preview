using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EveOPreview.Services.Logs;
using EveOPreview.Services.StaticData;
using EveOPreview.UI;
using Microsoft.Data.Sqlite;
using Serilog;
using Xunit;

namespace EveOPreview.Tests.Checks;

public sealed partial class StaticDataTests
{
    [Fact]
    public async Task FullSdeResolvesLocalizedSystemsAndTypesWithoutHintTranslationAndUpgradesOffline()
    {
        string root = Path.Combine(Path.GetTempPath(), "eve-log-names-" + Guid.NewGuid().ToString("N"));
        using var logger = new LoggerConfiguration().CreateLogger();
        try
        {
            using (var service = new StaticDataService(root, logger, new ExportHandler()))
            {
                Assert.True((await service.UpdateStaticDataAsync()).Success);
                Assert.Equal(30000142, service.FindSystem("吉他")); Assert.Equal(30000142, service.FindSystem("Джита"));
                Assert.Equal(30000142, service.FindSystem("jita")); Assert.Null(service.FindSystem("共享别名"));
                var catalog = new EveLogCatalog { StaticData = service, Systems = new() { ["Not in installed SDE"] = 1 } };
                Assert.Null(catalog.FindSystem("Not in installed SDE"));
                var entry = EveLogParser.Parse("[ 2026.01.01 12:00:00 ] (combat) 25 对 Sample Target - <localized hint=\"Wrong hint\">示例导弹*</localized> - Hits",
                    new("Sample Observer"), false, catalog, new HashSet<string>());
                Assert.Equal(LogLanguage.Chinese, entry.Language); Assert.Equal(20, entry.DamageSourceTypeId);
                Assert.Equal("示例导弹", entry.Weapon); Assert.Equal(DamageTypes.Kinetic | DamageTypes.Explosive, entry.DamageTypes);

                // Important Names in English is independent of message language,
                // including when the authoritative full SDE is installed.
                using var templates = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
                    "Fixtures", "logs", "localization-templates.json")));
                foreach (var row in templates.RootElement.GetProperty("entries").EnumerateArray()
                    .Where(x => x.GetProperty("messageId").GetInt32() == 285197))
                {
                    var language = Enum.Parse<LogLanguage>(row.GetProperty("language").GetString());
                    string text = row.GetProperty("text").GetString()
                        .Replace("Sample Pilot [SAMPLE](Rifter)", "Sample Warden")
                        .Replace("Sample Pilot", "Sample Warden")
                        .Replace("Sample Module", "<localized hint=\"示例导弹\">Sample Missile*");
                    foreach (var preference in new[] { LogLanguage.Automatic, language })
                    {
                        var englishName = EveLogParser.Parse("[ 2026.01.01 12:00:00 ] (combat) " + text,
                            new("Sample Observer", Language: language), false, catalog, new HashSet<string>(), preference);
                        Assert.Equal(language, englishName.Language); Assert.Equal(LogEventKind.Damage, englishName.EventKind);
                        Assert.Equal(CombatantKind.Npc, englishName.Kind); Assert.Equal("Sample Missile", englishName.Weapon);
                        Assert.Equal(20, englishName.DamageSourceTypeId);
                        var unknown = EveLogParser.Parse("[ 2026.01.01 12:00:00 ] (combat) " + text.Replace("Sample Missile*", "Unknown Item*"),
                            new("Sample Observer", Language: language), false, catalog, new HashSet<string>(), preference);
                        Assert.Null(unknown.Weapon); Assert.Null(unknown.DamageSourceTypeId);
                    }
                }
            }
            string path = Assert.Single(Directory.GetFiles(root, "sde-*.sqlite"));
            using (var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString()))
            {
                db.Open(); using var command = db.CreateCommand();
                command.CommandText = "DROP TABLE system_names"; command.ExecuteNonQuery();
            }
            // Simulate a previously installed complete SDE: no HTTP request is needed.
            using var offline = new StaticDataService(root, logger, new ExportHandler { Broken = true });
            Assert.Equal(42, offline.Build); Assert.Equal(30000142, offline.FindSystem("吉他"));
            Assert.NotNull(offline.ReadRecord("futureDataset", "test-key"));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
