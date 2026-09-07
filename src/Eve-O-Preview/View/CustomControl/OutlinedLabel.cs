//Eve-O Preview Plus is a program designed to deliver quality of life tooling. Primarily but not limited to enabling rapid window foreground and focus changes for the online game Eve Online.
//Copyright (C) 2026  Aura Asuna
//
//This program is free software: you can redistribute it and/or modify
//it under the terms of the GNU General Public License as published by
//the Free Software Foundation, either version 3 of the License, or
//(at your option) any later version.
//
//This program is distributed in the hope that it will be useful,
//but WITHOUT ANY WARRANTY; without even the implied warranty of
//MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//GNU General Public License for more details.
//
//You should have received a copy of the GNU General Public License
//along with this program.  If not, see <https://www.gnu.org/licenses/>.

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
        private bool cycleSkipped;
        private bool showTitle = true;
        private string skipStyle = "Circle with slash";
        private Color skipColor = Color.Red;
        private int SkipSize => Math.Clamp((int)Math.Ceiling(Font.Size), 12, 22);

        public void SetCycleSkipIndicator(bool skipped, string style, Color color)
        {
            if (cycleSkipped == skipped && skipStyle == style && skipColor == color) return;
            cycleSkipped = skipped; skipStyle = style; skipColor = color;
            Size = GetPreferredSize(Size.Empty);
            Invalidate();
        }

        public void SetTitleVisible(bool visible)
        {
            if (showTitle == visible) return;
            showTitle = visible;
            Size = GetPreferredSize(Size.Empty);
            Invalidate();
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            var size = showTitle ? base.GetPreferredSize(proposedSize) : Size.Empty;
            return cycleSkipped ? new Size(size.Width + SkipSize + 5, Math.Max(size.Height, SkipSize + 4)) : size;
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color OutlineColor
        {
            get { return outlineColor; }
            set
            {
                outlineColor = value;
                Invalidate(); // Redraw the control
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
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
            var titleBounds = ClientRectangle;
            if (cycleSkipped)
            {
                // Vector strokes stay legible on transparent overlays without relying on emoji fonts.
                using var marker = new GraphicsPath();
                var box = new RectangleF(2, 3, SkipSize - 3, SkipSize - 3);
                if (skipStyle == "Pause")
                {
                    marker.AddLine(box.Left + 2, box.Top, box.Left + 2, box.Bottom);
                    marker.StartFigure(); marker.AddLine(box.Right - 2, box.Top, box.Right - 2, box.Bottom);
                }
                else if (skipStyle == "Cross")
                {
                    marker.AddLine(box.Left, box.Top, box.Right, box.Bottom);
                    marker.StartFigure(); marker.AddLine(box.Right, box.Top, box.Left, box.Bottom);
                }
                else
                {
                    marker.AddEllipse(box);
                    marker.StartFigure(); marker.AddLine(box.Left + 2, box.Top + 2, box.Right - 2, box.Bottom - 2);
                }
                e.Graphics.SmoothingMode = SmoothingMode.None;
                using var contrast = new Pen(Color.Black, 4) { LineJoin = LineJoin.Round };
                using var color = new Pen(skipColor, 2) { LineJoin = LineJoin.Round };
                e.Graphics.DrawPath(contrast, marker);
                e.Graphics.DrawPath(color, marker);
                titleBounds.X += SkipSize + 5;
                titleBounds.Width = Math.Max(0, titleBounds.Width - SkipSize - 5);
            }
            if (!showTitle) return;
            using (GraphicsPath gp = new GraphicsPath())
            using (Pen outline = new Pen(OutlineColor, OutlineWidth) { LineJoin = LineJoin.Round, Alignment = PenAlignment.Outset })
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Near })
            using (Brush foreBrush = new SolidBrush(ForeColor))
            {
                gp.AddString(Text, Font.FontFamily, (int)Font.Style, Font.Size, titleBounds, sf);

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
