using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Design;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using static System.Net.Mime.MediaTypeNames;

namespace EveOPreview.View.CustomControl
{
    public class OutlinedLabel : Label
    {
        private Color outlineColor = Color.White;
        private float outlineWidth = 1f;

        public Color OutlineColor
        {
            get { return outlineColor; }
            set
            {
                outlineColor = value;
                Invalidate(); // Redraw the control
            }
        }

        public float OutlineWidth
        {
            get { return outlineWidth; }
            set
            {
                outlineWidth = value;
                Invalidate(); // Redraw the control
            }
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            this.Invalidate();
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            this.Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {            
            if (this.outlineWidth < 0.1)
            {
                base.OnPaint(e);
                return;
            }
            
            using (GraphicsPath gp = new GraphicsPath())
            using (Pen outline = new Pen(OutlineColor, OutlineWidth) { LineJoin = LineJoin.Round, Alignment = PenAlignment.Outset })
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Near })
            using (Brush foreBrush = new SolidBrush(ForeColor))
            {
                // 2. Fix Size: AddString expects EmSize. Font.Size alone is often too small for the path.
                float emSize = e.Graphics.DpiY * Font.SizeInPoints / 72;

                gp.AddString(Text, Font.FontFamily, (int)Font.Style, Font.Size, ClientRectangle, sf);

                // 3. DRAW OUTLINE (Sharp for no "bits")
                e.Graphics.SmoothingMode = SmoothingMode.None;
                e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixel;

                e.Graphics.DrawPath(outline, gp);

                // 4. FILL TEXT (Smooth for quality)
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                e.Graphics.FillPath(foreBrush, gp);
            }
        }
    }
}