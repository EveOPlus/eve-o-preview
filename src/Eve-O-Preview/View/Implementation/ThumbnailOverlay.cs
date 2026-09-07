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

using EveOPreview.Services;
using System;
using System.Drawing;
using System.Windows.Forms;
using EveOPreview.Configuration.Implementation;

namespace EveOPreview.View
{
    public partial class ThumbnailOverlay : Form
    {
        #region Private fields
        private readonly Action<object, MouseEventArgs> _areaClickAction;
        private bool _titleEnabled = true;
        private bool _cycleSkipped;
        #endregion

        public ThumbnailOverlay(Form owner, Action<object, MouseEventArgs> areaClickAction)
        {
            this.Owner = owner;
            this._areaClickAction = areaClickAction;

            InitializeComponent();
            OverlayLabel.MouseUp += OverlayArea_Click;
        }

        private void OverlayArea_Click(object sender, MouseEventArgs e)
        {
            // Label events use label-local coordinates; the preview opens its menu
            // in owner-client coordinates so the first action stays under the click.
            var source = sender as Control ?? this;
            Point point = Owner.PointToClient(source.PointToScreen(e.Location));
            this._areaClickAction(this, new MouseEventArgs(e.Button, e.Clicks, point.X, point.Y, e.Delta));
        }

        public void SetOverlayLabel(string label)
        {
            this.OverlayLabel.Text = label;
        }
        
        public void SetOverlayFont(FontSettings fontSettings)
        {
            this.OverlayLabel.Font = new Font(fontSettings.Name, fontSettings.Size, fontSettings.Style);
            this.OverlayLabel.ForeColor = fontSettings.ForeColor;
            this.OverlayLabel.OutlineColor = fontSettings.OutlineColor;
            this.OverlayLabel.OutlineWidth = fontSettings.OutlineWidth;
            this.OverlayLabel.Top = fontSettings.PositionOffsetFromTop;
            this.OverlayLabel.Left = fontSettings.PositionOffsetFromLeft;
        }

        public void EnableOverlayLabel(bool enable)
        {
            _titleEnabled = enable;
            this.OverlayLabel.SetTitleVisible(enable);
            this.OverlayLabel.Visible = enable || _cycleSkipped;
        }

        public void SetCycleSkipIndicator(bool skipped, string style, Color color)
        {
            _cycleSkipped = skipped;
            OverlayLabel.SetCycleSkipIndicator(skipped, style, color);
            OverlayLabel.Visible = _titleEnabled || skipped;
        }

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                var Params = base.CreateParams;
                Params.ExStyle |= (int)InteropConstants.WS_EX_TOOLWINDOW;
                return Params;
            }
        }
    }
}
