namespace EveOPreview.Configuration.Implementation
{
    public class FpsLimiterSettings
    {
        public bool IsEnabled { get; set; } = false;
        public int FpsFocused { get; set; } = 144;
        public int FpsPredictingFocus { get; set; } = 45;
        public int FpsBackground { get; set; } = 20;
    }
}