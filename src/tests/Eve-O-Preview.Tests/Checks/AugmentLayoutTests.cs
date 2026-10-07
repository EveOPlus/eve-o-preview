using System;
using System.IO;
using System.Linq;
using EveOPreview.Configuration.Implementation;
using EveOPreview.Preview;
using EveOPreview.UI;
using Serilog;
using Xunit;

namespace EveOPreview.Tests.Checks;

public class AugmentLayoutTests
{
    [Fact]
    public void FadeSharesIntensityForBothTargetsAndRestoresWithoutNewEvents()
    {
        var start = DateTimeOffset.Parse("2026-09-14T00:00:00Z");
        var character = new CharacterCombatSnapshot("Pilot", null, null, null, null, null, [], 1)
            { LastIncomingDamageAt = start, IncomingDamageStartedAt = start };
        var settings = new CombatLogSettings { FlashAnimation = DamageFlashAnimation.Fade, FlashTarget = DamageFlashTarget.Both };
        var values = new[] { 0, 125, 250, 375, 500 }.Select(ms => CombatDamageFlash.Evaluate(character, settings, start.AddMilliseconds(ms))).ToArray();
        Assert.Equal(new[] { 0d, .5, 1, .5, 0 }, values.Select(x => Math.Round(x.Intensity, 3)));
        Assert.All(values, frame => { Assert.True(frame.TitleHighlighted); Assert.Equal(0x33FF7666u, frame.ThumbnailTint); Assert.NotNull(frame.NextChangeAt); });
        var extended = CombatDamageFlash.Evaluate(character with { LastIncomingDamageAt = start.AddMilliseconds(100) }, settings, start.AddMilliseconds(125));
        Assert.Equal(values[1].Intensity, extended.Intensity); // Rapid hits extend expiry without restarting animation.
        Assert.Null(CombatDamageFlash.Evaluate(character, settings, start.AddSeconds(2)).NextChangeAt);
        Assert.Null(CombatDamageFlash.Evaluate(character, settings with { FlashPlayerOnly = true }, start.AddMilliseconds(125)).ThumbnailTint);
        Assert.Equal(0xFF808080u, OverlayColors.Blend(0xFF000000, 0xFFFFFFFF, .5));
    }

    [Fact]
    public void AllPositionsStackTitleSystemAndMeterWithoutMovingHiddenRows()
    {
        foreach (var position in Enum.GetValues<OverlayPosition>())
        foreach (var placement in Enum.GetValues<SubtitlePlacement>())
        {
            var scene = new OverlayScene { Title = "Pilot", Subtitle = "Jita", Font = new(Size: 14), TitlePosition = position,
                SubtitlePlacement = placement, StatsStyle = new(14) { Position = position }, Stats = [new("In", "12"), new("Out", "34")] };
            var arranged = OverlayLayout.Arrange(scene, new(384, 216), 45, 35, 140);
            var title = arranged.TitleLayout(45, 35);
            float bottom = Math.Max(title.TitleY + 14 * 1.35f + 3, title.SubtitleY + 14 * 1.35f + 3);
            Assert.True(arranged.StatsStyle.OffsetY >= bottom, position + "/" + placement);
            Assert.InRange(arranged.Font.OffsetX, 0, 384 - 45);
            Assert.InRange(arranged.Font.OffsetY, 0, 216 - 36);
            var hidden = OverlayLayout.Arrange(scene with { Stats = [new("", "") { Visible = false }, scene.Stats[1]] }, new(384, 216), 45, 35, 140);
            Assert.Equal(arranged.Font, hidden.Font);
            Assert.Equal(arranged.StatsStyle, hidden.StatsStyle);
        }
        foreach (var order in Enum.GetValues<CombatRowOrder>())
        {
            var rows = CombatOverlayFormatter.Meter(null, new() { RowOrder = order, Repairs = true });
            Assert.Equal(4, rows.Count); Assert.All(rows, x => Assert.False(x.Visible));
        }
    }

    [Fact]
    public void OldPreferencesKeepOffsetsAndBlinkWhileNewSettingsRoundTrip()
    {
        string path = Path.GetTempFileName(); using var logger = new LoggerConfiguration().CreateLogger();
        try
        {
            File.WriteAllText(path, """{"ConfigVersion":1,"Theme":"Light","Future":42,"CombatLogs":{"Enabled":true,"FlashAnimation":0,"DefaultOverlay":{"Top":true,"FontSize":17,"OffsetX":22,"OffsetY":24}}}""");
            var old = new ApplicationPreferences(path, logger);
            Assert.True(old.CombatLogs.Enabled); Assert.Equal(DamageFlashAnimation.Blink, old.CombatLogs.FlashAnimation);
            Assert.Equal(OverlayPosition.TopLeft, old.CombatLogs.DefaultOverlay.GetStatsStyle().EffectivePosition);
            Assert.Equal(22, old.CombatLogs.DefaultOverlay.OffsetX); Assert.Equal(24, old.CombatLogs.DefaultOverlay.OffsetY);
            old.SetCombatLogs(old.CombatLogs with { FlashAnimation = DamageFlashAnimation.Fade, FlashTarget = DamageFlashTarget.Both, FlashOpacityPercent = 10,
                DefaultOverlay = old.CombatLogs.DefaultOverlay with { Position = OverlayPosition.BottomRight, TitlePosition = OverlayPosition.BottomRight, RowOrder = CombatRowOrder.RepairsOutgoingIncoming } });
            var current = new ApplicationPreferences(path, logger);
            Assert.Equal(DamageFlashAnimation.Fade, current.CombatLogs.FlashAnimation);
            Assert.Equal(10, current.CombatLogs.FlashOpacityPercent);
            Assert.Equal(old.CombatLogs.DefaultOverlay, current.CombatLogs.DefaultOverlay);
            Assert.Equal("Light", current.Theme); Assert.Contains("\"Future\": 42", File.ReadAllText(path));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void FadeOpacityIsIndependentOfPhaseAndTitleAndValidatesEndpoints()
    {
        var now = DateTimeOffset.UtcNow;
        var character = new CharacterCombatSnapshot("Pilot", null, null, null, null, null, [], 0)
            { LastIncomingDamageAt = now, IncomingDamageStartedAt = now };
        var defaults = System.Text.Json.JsonSerializer.Deserialize<CombatLogSettings>("{}")!;
        Assert.Equal(DamageFlashAnimation.Fade, defaults.FlashAnimation); Assert.Equal(20, defaults.FlashOpacityPercent);
        foreach (int percent in new[] { 0, 10, 20, 100 })
        {
            var settings = defaults with { FlashTarget = DamageFlashTarget.Both, FlashOpacityPercent = percent };
            var frame = CombatDamageFlash.Evaluate(character, settings, now.AddMilliseconds(250));
            Assert.Equal(1, frame.Intensity); Assert.True(frame.TitleHighlighted);
            if (percent == 0) Assert.Null(frame.ThumbnailTint);
            else Assert.Equal((uint)Math.Round(255 * percent / 100d), frame.ThumbnailTint!.Value >> 24);
            Assert.Equal(settings.FlashOpacityPercent, ApplicationPreferences.NormalizeCombatLogs(settings).FlashOpacityPercent);
        }
        Assert.Throws<ArgumentException>(() => ApplicationPreferences.NormalizeCombatLogs(defaults with { FlashOpacityPercent = -1 }));
        Assert.Throws<ArgumentException>(() => ApplicationPreferences.NormalizeCombatLogs(defaults with { FlashOpacityPercent = 101 }));
    }
}
