using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using EveOPreview.Configuration.Implementation;
using EveOPreview.UI;
using Serilog;
using Xunit;

namespace EveOPreview.Tests.Checks;

public sealed class WorkspaceLocalizationTests
{
    private static Dictionary<string, string> Catalog(string code)
    {
        using var stream = typeof(WorkspaceLocalization).Assembly.GetManifestResourceStream($"EveOPreview.UI.Localization.{code}.json");
        Assert.NotNull(stream);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream);
    }

    [Fact]
    public void EveryBundledCatalogCoversTheSourceAndPreservesFormatArguments()
    {
        var source = Catalog("en");
        Assert.True(source.Count > 400);
        foreach (var language in WorkspaceLocalization.Languages)
        {
            var translated = Catalog(language.Code);
            Assert.Equal(source.Keys.OrderBy(x => x), translated.Keys.OrderBy(x => x));
            foreach (var entry in source)
            {
                string value = translated[entry.Key];
                Assert.False(string.IsNullOrWhiteSpace(value), language.Code + ": " + entry.Key);
                Assert.DoesNotContain('\uFFFD', value);
                Assert.Equal(CompositeFormat.Parse(entry.Key).MinimumArgumentCount, CompositeFormat.Parse(value).MinimumArgumentCount);
                Assert.Equal(Regex.Matches(entry.Key, @"\{\d+[^}]*\}").Select(m => m.Value).OrderBy(x => x),
                    Regex.Matches(value, @"\{\d+[^}]*\}").Select(m => m.Value).OrderBy(x => x));
            }
            foreach (var definition in SettingCatalog.All)
            {
                Assert.Contains(definition.Label, source.Keys);
                Assert.Contains(definition.Description, source.Keys);
            }
        }
    }

    [Theory]
    [InlineData("de-AT", "de")]
    [InlineData("es-MX", "es")]
    [InlineData("pt-PT", "pt-BR")]
    [InlineData("zh-HK", "zh-Hant")]
    [InlineData("zh-SG", "zh-Hans")]
    [InlineData("not_a_culture", "en")]
    [InlineData("fi-FI", "en")]
    public void RegionResolutionUsesBundledParentOrEnglish(string preference, string expected) =>
        Assert.Equal(expected, WorkspaceLocalization.Resolve(preference));

    [Fact]
    public void DisplayLanguageDoesNotChangeParsingCultureOrUserData()
    {
        var culture = CultureInfo.CurrentCulture;
        var uiCulture = CultureInfo.CurrentUICulture;
        var german = new WorkspaceLocalization("de");
        Assert.Equal("de", german.Code);
        Assert.Equal("Not a catalog key", german.Get("Not a catalog key"));
        Assert.Contains("EVE - Appearance", german.Format($"Character settings saved for {"EVE - Appearance"}"));
        Assert.Same(culture, CultureInfo.CurrentCulture);
        Assert.Same(uiCulture, CultureInfo.CurrentUICulture);
        Assert.Equal(WorkspaceLocalization.Resolve(uiCulture.Name), WorkspaceLocalization.Resolve("auto"));
        Assert.True(new WorkspaceLocalization("ar").RightToLeft);
        Assert.False(german.RightToLeft);
        Assert.True(new WorkspaceLocalization("tr").Contains("İstemciler", "istemciler"));
    }

    [Fact]
    public void LanguageSurvivesRestartWithoutChangingThemeOrUnknownPreferences()
    {
        string path = Path.GetTempFileName();
        using var logger = new LoggerConfiguration().CreateLogger();
        try
        {
            File.WriteAllText(path, "{\"ConfigVersion\":1,\"Theme\":\"Light\",\"FutureSetting\":42}");
            var preferences = new ApplicationPreferences(path, logger);
            Assert.Equal("auto", preferences.UiLanguage);
            int changes = 0; preferences.Changed += () => changes++;
            preferences.SetLanguage("ja-JP");
            Assert.Equal(1, changes);
            var restored = new ApplicationPreferences(path, logger);
            Assert.Equal("ja", restored.UiLanguage);
            Assert.Equal("Light", restored.Theme);
            Assert.Equal(42, Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(path)).Value<int>("FutureSetting"));
            restored.SetLanguage("auto");
            Assert.Equal("auto", new ApplicationPreferences(path, logger).UiLanguage);
            Assert.False(File.Exists(path + ".tmp"));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void FailedLanguageSaveKeepsTheActiveLanguageAndDoesNotNotify()
    {
        string blocker = Path.GetTempFileName();
        using var logger = new LoggerConfiguration().CreateLogger();
        try
        {
            var preferences = new ApplicationPreferences(Path.Combine(blocker, "settings.json"), logger);
            int changes = 0; preferences.Changed += () => changes++;
            Assert.ThrowsAny<IOException>(() => preferences.SetLanguage("ar"));
            Assert.Equal("auto", preferences.UiLanguage);
            Assert.Equal(0, changes);
        }
        finally { File.Delete(blocker); }
    }
}
