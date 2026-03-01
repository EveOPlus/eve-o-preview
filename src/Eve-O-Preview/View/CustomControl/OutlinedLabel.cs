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
        private Color outlineColor = Color.Black;
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
            using (GraphicsPath gp = new GraphicsPath())
            using (Pen outline = new Pen(OutlineColor, OutlineWidth) { LineJoin = LineJoin.Round, Alignment = PenAlignment.Outset })
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Near })
            using (Brush foreBrush = new SolidBrush(ForeColor))
            {
                gp.AddString(Text, Font.FontFamily, (int)Font.Style, Font.Size, ClientRectangle, sf);

                // Turn off any anti-alias because our background is going to be transparent and aliasing creates artifacts.
                e.Graphics.SmoothingMode = SmoothingMode.None;
                e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;

                if (this.outlineWidth > 0.1)
                {
                    e.Graphics.DrawPath(outline, gp);

                    if (this.outlineWidth > 1.9)
                    {
                        // If we drew an outline that's tick enough, then we can anti-alias against that for smoother results.
                        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                        e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                    }
                }

                e.Graphics.FillPath(foreBrush, gp);
            }
        }
    }
}