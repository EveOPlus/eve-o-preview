using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;

namespace EveOPreview.UI;

public sealed partial class CombatLogView
{
    private readonly List<(string Character, TextBlock Name)> _damageNames = new();
    private DispatcherTimer? _nameFlashTimer;
    private Expander? _damageFlashSettings;

    private Control BuildDamageFlashSettings()
    {
        var settings = _logs.ReadLogSettings();
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(Toggle("Flash on incoming damage", "logs-flash-enabled", settings.FlashIncomingDamage,
            x => Save(_logs.ReadLogSettings() with { FlashIncomingDamage = x })));
        panel.Children.Add(Choice("Flash target", "logs-flash-target", Enum.GetValues<DamageFlashTarget>(), settings.FlashTarget,
            x => Save(_logs.ReadLogSettings() with { FlashTarget = x })));
        panel.Children.Add(Choice("Animation", "logs-flash-animation", Enum.GetValues<DamageFlashAnimation>(), settings.FlashAnimation,
            x => Save(_logs.ReadLogSettings() with { FlashAnimation = x })));
        panel.Children.Add(Choice("Damage sources", "logs-flash-sources", new[] { "Player only", "NPC + Player" },
            settings.FlashPlayerOnly ? "Player only" : "NPC + Player", x => Save(_logs.ReadLogSettings() with { FlashPlayerOnly = x == "Player only" })));
        panel.Children.Add(ColorField("Flash colour", "logs-flash-color", settings.FlashColor, x => Save(_logs.ReadLogSettings() with { FlashColor = x }), global: true));
        panel.Children.Add(Number("Thumbnail flash opacity (%)", "logs-flash-opacity", settings.FlashOpacityPercent, 0, 100,
            x => Save(_logs.ReadLogSettings() with { FlashOpacityPercent = x })));
        panel.Children.Add(Number("Duration (seconds)", "logs-flash-seconds", settings.FlashSeconds, 1, 10, x => Save(_logs.ReadLogSettings() with { FlashSeconds = x })));
        panel.Children.Add(Number("Flash interval (ms)", "logs-flash-interval", settings.FlashIntervalMilliseconds, 100, 2000,
            x => Save(_logs.ReadLogSettings() with { FlashIntervalMilliseconds = x })));
        return _damageFlashSettings = new Expander { Header = L(settings.FlashIncomingDamage ? "Incoming damage indicator" : "Incoming damage indicator (off)"),
            Name = "logs-flash-settings", Content = panel, HorizontalAlignment = HorizontalAlignment.Stretch };
    }

    private void RefreshNameFlashes()
    {
        if (_disposed) return;
        var settings = _logs.ReadLogSettings(); var now = DateTimeOffset.UtcNow;
        if (_damageFlashSettings is not null)
            _damageFlashSettings.Header = L(settings.FlashIncomingDamage ? "Incoming damage indicator" : "Incoming damage indicator (off)");
        var characters = _logs.ReadLogs().Characters.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
        DateTimeOffset? next = null;
        foreach (var (character, name) in _damageNames)
        {
            characters.TryGetValue(character, out var snapshot);
            var frame = CombatDamageFlash.Evaluate(snapshot, settings, now);
            name.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromUInt32(
                EveOPreview.Preview.OverlayColors.Blend(CombatOverlayFormatter.Color(_theme.Text),
                    CombatOverlayFormatter.Color(settings.FlashColor), frame.TitleHighlighted ? frame.Intensity : 0)));
            if (frame.NextChangeAt is { } change && (next is null || change < next)) next = change;
        }
        _nameFlashTimer?.Stop();
        if (next is null) return;
        if (_nameFlashTimer is null)
        {
            _nameFlashTimer = new DispatcherTimer();
            _nameFlashTimer.Tick += (_, _) => RefreshNameFlashes();
        }
        _nameFlashTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(1, (next.Value - DateTimeOffset.UtcNow).TotalMilliseconds));
        _nameFlashTimer.Start();
    }
}
