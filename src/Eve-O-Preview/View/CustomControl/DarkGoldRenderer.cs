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

using System.Drawing;
using System.Windows.Forms;

namespace EveOPreview.View.CustomControl;

public class DarkGoldRenderer : ToolStripProfessionalRenderer
{
    public DarkGoldRenderer() : base(new GoldColorTable()) { }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        var g = e.Graphics;
        var rect = new Rectangle(e.ImageRectangle.Location, e.ImageRectangle.Size);
        rect.Inflate(-1, -1);

        using (var pen = new Pen(Color.FromArgb(212, 175, 55), 2))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.DrawLine(pen, rect.Left + 3, rect.Top + 7, rect.Left + 6, rect.Top + 10);
            g.DrawLine(pen, rect.Left + 6, rect.Top + 10, rect.Left + 11, rect.Top + 4);
        }
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        int padding = (int)(e.Item.Width * 0.1);
        int y = e.Item.Height / 2;

        using (var pen = new Pen(Color.FromArgb(40, 212, 175, 55)))
        {
            e.Graphics.DrawLine(pen, padding, y, e.Item.Width - padding, y);
        }
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        using (var pen = new Pen(Color.FromArgb(45, 45, 48)))
            e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
    }
}

public class GoldColorTable : ProfessionalColorTable
{
    private Color DarkBackground = Color.FromArgb(20, 20, 22);
    private Color DarkSelection = Color.FromArgb(38, 38, 42);
    private Color AccentBorder = Color.FromArgb(60, 50, 20);

    // Backgrounds
    public override Color ToolStripDropDownBackground => DarkBackground;

    public override Color ImageMarginGradientBegin => DarkBackground;
    public override Color ImageMarginGradientMiddle => DarkBackground;
    public override Color ImageMarginGradientEnd => DarkBackground;

    // Hover
    public override Color MenuItemSelected => DarkSelection;
    public override Color MenuItemSelectedGradientBegin => DarkSelection;
    public override Color MenuItemSelectedGradientEnd => DarkSelection;
    public override Color MenuItemBorder => AccentBorder;

    // Checkmark background for if we want a tick box later.
    public override Color CheckBackground => Color.FromArgb(50, 45, 30);
    public override Color CheckSelectedBackground => Color.FromArgb(70, 60, 40);
    public override Color CheckPressedBackground => Color.FromArgb(80, 70, 50);
}