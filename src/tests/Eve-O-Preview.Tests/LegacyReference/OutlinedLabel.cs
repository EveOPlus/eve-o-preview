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
using EveOPreview.Preview;
using EveOPreview.View.Rendering;
using System.Windows.Forms;

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
            // Keep the existing label's layout and input handling, but share every
            // glyph and marker with DPS, native composition and the settings preview.
            OverlaySceneRasterizer.Draw(e.Graphics, new OverlayScene
            {
                Title = Text, ShowTitle = showTitle,
                Font = new OverlayFont(Font.FontFamily.Name, Font.Size, (OverlayFontStyle)Font.Style,
                    unchecked((uint)ForeColor.ToArgb()), unchecked((uint)OutlineColor.ToArgb()), OutlineWidth, 0, 0),
                CycleSkipped = cycleSkipped,
                MarkerStyle = skipStyle switch { "Pause" => CycleMarkerStyle.Pause, "Cross" => CycleMarkerStyle.Cross, _ => CycleMarkerStyle.CircleSlash },
                MarkerColor = unchecked((uint)skipColor.ToArgb())
            }, new PreviewSize(ClientSize.Width, ClientSize.Height));
        }
    }
}
