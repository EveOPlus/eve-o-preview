namespace EveOPreview.Configuration.Implementation
{
    using System;
    using System.Drawing;

    public class FontSettings
    {
        public string Name { get; set; }
        public FontStyle Style { get; set; }
        public float Size { get; set; }
        public Color ForeColor { get; set; }
        public Color OutlineColor { get; set; }
        public float OutlineWidth { get; set; }
        public int PositionOffsetFromLeft { get; set; }
        public int PositionOffsetFromTop { get; set; }
    }
}