namespace EveOPreview.Preview;

public static class OverlayColors
{
    public static uint Blend(uint original, uint highlight, double amount)
    {
        amount = double.IsFinite(amount) ? Math.Clamp(amount, 0, 1) : 0;
        uint Channel(int shift) => (uint)Math.Round(((original >> shift) & 255) * (1 - amount) + ((highlight >> shift) & 255) * amount);
        return Channel(24) << 24 | Channel(16) << 16 | Channel(8) << 8 | Channel(0);
    }
}
