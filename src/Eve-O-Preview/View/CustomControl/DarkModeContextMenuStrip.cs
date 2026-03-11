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

using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace EveOPreview.View.CustomControl;

public class DarkModeContextMenuStrip : ContextMenuStrip
{
    private static readonly Color BackgroundColor = Color.FromArgb(20, 20, 22);
    private static readonly Color GoldText = Color.FromArgb(212, 175, 55);
    private static readonly Color DarkSelection = Color.FromArgb(38, 38, 42);
    private static readonly Color AccentBorder = Color.FromArgb(60, 50, 20);


    public DarkModeContextMenuStrip(IContainer container) : base(container)
    {
        ShowImageMargin = false;
        ShowCheckMargin = false;
        BackColor = BackgroundColor;
        ForeColor = GoldText;
        Font = new Font("Segoe UI Semibold", 9.5F);
        Renderer = new DarkGoldRenderer();
    }

    private class DarkGoldRenderer : ToolStripProfessionalRenderer
    {
        public DarkGoldRenderer() : base(new GoldColorTable()) { }

        // Custom subtle separator
        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            using (var pen = new Pen(Color.FromArgb(40, GoldText))) 
            {
                e.Graphics.DrawLine(pen, 10, e.Item.Height / 2, e.Item.Width - 10, e.Item.Height / 2);
            }
        }

        // Clean border for the whole menu
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            using (var pen = new Pen(Color.FromArgb(45, 45, 48)))
            {
                e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
            }
        }
    }

    private class GoldColorTable : ProfessionalColorTable
    {
        public override Color MenuItemSelected => DarkSelection;
        public override Color MenuItemSelectedGradientBegin => DarkSelection;
        public override Color MenuItemSelectedGradientEnd => DarkSelection;
        public override Color MenuItemBorder => AccentBorder;
        public override Color ToolStripDropDownBackground => BackgroundColor;

        public override Color ImageMarginGradientBegin => BackgroundColor;
        public override Color ImageMarginGradientMiddle => BackgroundColor;
        public override Color ImageMarginGradientEnd => BackgroundColor;
    }
}