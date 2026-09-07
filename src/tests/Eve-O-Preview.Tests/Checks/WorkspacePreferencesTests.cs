using System;
using System.IO;
using System.Linq;
using EveOPreview.UI;
using EveOPreview.Configuration.Implementation;
using Newtonsoft.Json.Linq;
using Serilog;
using Xunit;

namespace EveOPreview.Tests.Checks;

public sealed class WorkspacePreferencesTests
{
    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    [InlineData("Legacy")]
    public void AppearanceSurvivesRestartWithoutGameplayProfileStorage(string theme)
    {
        string directory = Path.Combine(Path.GetTempPath(), "EveOPreviewAppearance-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "appearance.json");
        using var logger = new LoggerConfiguration().CreateLogger();
        try
        {
            var preferences = new ApplicationPreferences(path, logger);
            int changes = 0;
            preferences.Changed += () => changes++;
            preferences.SetTheme(theme);

            Assert.Equal(theme, preferences.Theme);
            Assert.Equal(theme, new ApplicationPreferences(path, logger).Theme);
            Assert.Equal(1, changes);
            var saved = JObject.Parse(File.ReadAllText(path));
            Assert.Equal(theme, (string)saved["Theme"]);
            Assert.Equal(1, (int)saved["ConfigVersion"]);
            Assert.Equal(2, saved.Properties().Count());
            Assert.False(File.Exists(path + ".tmp"));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            if (Directory.Exists(directory)) Directory.Delete(directory);
        }
    }

    [Theory]
    [InlineData("{ malformed")]
    [InlineData("null")]
    [InlineData("{\"Theme\":\"Removed theme\"}")]
    public void UnreadableOrUnknownAppearanceUsesDarkWithoutDestroyingTheFile(string contents)
    {
        string path = Path.GetTempFileName();
        using var logger = new LoggerConfiguration().CreateLogger();
        try
        {
            File.WriteAllText(path, contents);
            Assert.Equal("Dark", new ApplicationPreferences(path, logger).Theme);
            Assert.Equal(contents, File.ReadAllText(path));
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData(null, "Legacy")]
    [InlineData(0, "Legacy")]
    [InlineData(null, "Light")]
    public void OlderSettingsDefaultToDarkUntilTheUserSelectsLegacyAgain(int? version, string previousTheme)
    {
        string path = Path.GetTempFileName();
        using var logger = new LoggerConfiguration().CreateLogger();
        try
        {
            var saved = new JObject { ["Theme"] = previousTheme, ["FutureSetting"] = 42,
                ["ThumbnailMenuTheme"] = "caldari", ["ThumbnailMenuOrder"] = new JArray(ThumbnailMenuActions.DefaultOrder) };
            if (version.HasValue) saved["ConfigVersion"] = version.Value;
            string original = saved.ToString();
            File.WriteAllText(path, original);
            var preferences = new ApplicationPreferences(path, logger);
            Assert.Equal("Dark", preferences.Theme);
            Assert.Equal(original, File.ReadAllText(path)); // Reading old files does not require write access.
            Assert.Equal("caldari", preferences.ThumbnailMenuTheme);
            Assert.Equal(ThumbnailMenuActions.DefaultOrder, preferences.ThumbnailMenuOrder);
            preferences.SetThumbnailMenuTheme("amarr");
            var migrated = new ApplicationPreferences(path, logger);
            Assert.Equal("Dark", migrated.Theme);
            Assert.Equal("amarr", migrated.ThumbnailMenuTheme);
            migrated.SetTheme("Legacy");
            Assert.Equal("Legacy", new ApplicationPreferences(path, logger).Theme);
            var actual = JObject.Parse(File.ReadAllText(path));
            Assert.Equal(1, (int)actual["ConfigVersion"]);
            Assert.Equal(42, (int)actual["FutureSetting"]);
            Assert.False(File.Exists(path + ".tmp"));
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void CurrentAndFutureSettingsKeepAnExplicitThemeAndTheirVersion(int version)
    {
        string path = Path.GetTempFileName();
        using var logger = new LoggerConfiguration().CreateLogger();
        try
        {
            File.WriteAllText(path, new JObject { ["ConfigVersion"] = version, ["Theme"] = "Legacy" }.ToString());
            var preferences = new ApplicationPreferences(path, logger);
            Assert.Equal("Legacy", preferences.Theme);
            preferences.SetThumbnailMenuTheme("caldari");
            Assert.Equal("Legacy", new ApplicationPreferences(path, logger).Theme);
            Assert.Equal(version, (int)JObject.Parse(File.ReadAllText(path))["ConfigVersion"]);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void InvalidThemeCannotReplaceTheSavedThemeOrRaiseChanged()
    {
        string path = Path.GetTempFileName();
        using var logger = new LoggerConfiguration().CreateLogger();
        try
        {
            var preferences = new ApplicationPreferences(path, logger);
            preferences.SetTheme("Light");
            string saved = File.ReadAllText(path);
            int changes = 0;
            preferences.Changed += () => changes++;

            Assert.Throws<ArgumentException>(() => preferences.SetTheme("Unrecognized"));

            Assert.Equal("Light", preferences.Theme);
            Assert.Equal(saved, File.ReadAllText(path));
            Assert.Equal(0, changes);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void ThemeEditPreservesUnknownFutureGlobalSettings()
    {
        string path = Path.GetTempFileName();
        using var logger = new LoggerConfiguration().CreateLogger();
        try
        {
            var saved = new JObject
            {
                ["Theme"] = "Dark",
                ["FutureFeature"] = new JObject { ["Enabled"] = true, ["Columns"] = new JArray("Pilot", "DPS") },
                ["LastWorkspace"] = "Characters"
            };
            File.WriteAllText(path, saved.ToString());

            new ApplicationPreferences(path, logger).SetTheme("Light");

            var actual = JObject.Parse(File.ReadAllText(path));
            Assert.Equal("Light", (string)actual["Theme"]);
            Assert.True(JToken.DeepEquals(saved["FutureFeature"], actual["FutureFeature"]));
            Assert.Equal("Characters", (string)actual["LastWorkspace"]);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void MenuOrderSharesGlobalSettingsAndRepairsOlderOrHandEditedLists()
    {
        string path = Path.GetTempFileName();
        using var logger = new LoggerConfiguration().CreateLogger();
        try
        {
            File.WriteAllText(path, "{\"Theme\":\"Light\",\"FutureSetting\":42,\"ThumbnailMenuOrder\":[\"skip-cycling\",null,5,\"removed\",\"skip-cycling\",\"resize\"]}");
            var preferences = new ApplicationPreferences(path, logger);
            var repaired = new[] { "skip-cycling", "resize", "minimize", "minimize-all", "move" };
            Assert.Equal(repaired, preferences.ThumbnailMenuOrder);
            int changes = 0;
            preferences.Changed += () => changes++;
            preferences.SetThumbnailMenuOrder(repaired.Reverse());
            preferences.SetTheme("Legacy");
            preferences.SetThumbnailMenuTheme("caldari");
            var restarted = new ApplicationPreferences(path, logger);
            Assert.Equal(repaired.Reverse(), restarted.ThumbnailMenuOrder);
            Assert.Equal("Legacy", restarted.Theme);
            Assert.Equal("caldari", restarted.ThumbnailMenuTheme);
            Assert.Equal(42, (int)JObject.Parse(File.ReadAllText(path))["FutureSetting"]);
            Assert.Equal(3, changes);
            Assert.False(File.Exists(path + ".tmp"));
            restarted.SetThumbnailMenuOrder(ThumbnailMenuActions.DefaultOrder);
            Assert.Equal(ThumbnailMenuActions.DefaultOrder, new ApplicationPreferences(path, logger).ThumbnailMenuOrder);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void GlobalSettingsFollowTheResolvedPortableProfileDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "EveOPreviewPortable-" + Guid.NewGuid().ToString("N"));
        try
        {
            string path = ApplicationPreferences.ResolveFilePath(Path.Combine(directory, "Profiles"));
            Assert.Equal(Path.Combine(directory, "EVE-O Preview.settings.json"), path);
            Assert.Empty(Directory.GetFileSystemEntries(directory));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory); }
    }

    [Fact]
    public void UnusablePortableLocationFallsBackToLocalApplicationData()
    {
        string existingFile = Path.GetTempFileName();
        try
        {
            // An ordinary file cannot become the parent directory of the settings file.
            string actual = ApplicationPreferences.ResolveFilePath(Path.Combine(existingFile, "Profiles"));
            string expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Eve-O Preview", "EVE-O Preview.settings.json");
            Assert.Equal(expected, actual);
        }
        finally { File.Delete(existingFile); }
    }
}
