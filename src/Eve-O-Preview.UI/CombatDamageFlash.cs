namespace EveOPreview.UI;

/// <summary>One clock for name and thumbnail flashing, with a smooth fade or optional blink.</summary>
public static class CombatDamageFlash
{
    public readonly record struct Frame(bool Highlighted, DateTimeOffset? NextChangeAt)
    {
        public bool TitleHighlighted { get; init; }
        public uint? ThumbnailTint { get; init; }
        public double Intensity { get; init; }
    }

    public static Frame Evaluate(CharacterCombatSnapshot? character, CombatLogSettings settings, DateTimeOffset now)
    {
        var lastHit = settings.FlashPlayerOnly ? character?.LastPlayerDamageAt : character?.LastIncomingDamageAt;
        if (!settings.FlashIncomingDamage || lastHit is not { } last || last > now) return default;
        var until = last.AddSeconds(Math.Clamp(settings.FlashSeconds, 1, 10));
        if (now >= until) return default;
        var start = (settings.FlashPlayerOnly ? character?.PlayerDamageStartedAt : character?.IncomingDamageStartedAt) ?? last;
        if (start > last) start = last;
        long period = TimeSpan.FromMilliseconds(Math.Clamp(settings.FlashIntervalMilliseconds, 100, 2000)).Ticks;
        long phase = (now - start).Ticks % period;
        bool highlighted = phase < period / 2;
        var next = now.AddTicks((highlighted ? period / 2 : period) - phase);
        double intensity = highlighted ? 1 : 0;
        if (settings.FlashAnimation == DamageFlashAnimation.Fade)
        {
            // Smooth rise and fall on the same burst clock. Only active bursts
            // request animation ticks; idle thumbnails never run this timer.
            intensity = (1 - Math.Cos(2 * Math.PI * phase / period)) / 2;
            intensity *= Math.Min(1, (until - now).TotalMilliseconds / Math.Min(100, settings.FlashIntervalMilliseconds / 2d));
            highlighted = true;
            next = now.AddMilliseconds(1000d / 30);
        }
        return new(highlighted, next < until ? next : until)
        {
            Intensity = intensity,
            TitleHighlighted = highlighted && settings.FlashTarget is DamageFlashTarget.Title or DamageFlashTarget.Both,
            // A translucent wash leaves the running game image visible beneath it.
            ThumbnailTint = highlighted && settings.FlashOpacityPercent > 0 && settings.FlashTarget is DamageFlashTarget.Thumbnail or DamageFlashTarget.Both
                ? ((uint)Math.Round(255 * Math.Clamp(settings.FlashOpacityPercent, 0, 100) / 100d) << 24)
                    | (CombatOverlayFormatter.Color(settings.FlashColor) & 0x00FFFFFFu) : null
        };
    }
}
