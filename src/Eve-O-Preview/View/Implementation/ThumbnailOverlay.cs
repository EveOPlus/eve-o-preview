using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using EveOPreview.Configuration;
using EveOPreview.Services;

namespace EveOPreview.View
{
	public partial class ThumbnailOverlay : Form
	{
		public bool highlighted;

		#region Private fields
		private readonly Action<object, MouseEventArgs> _areaClickAction;
        private IThumbnailConfiguration _config;
        #endregion

        public ThumbnailOverlay(Form owner, IThumbnailConfiguration config, Action<object, MouseEventArgs> areaClickAction)
		{
			this.Owner = owner;
			this.highlighted = false;
			this._config = config;
			this._areaClickAction = areaClickAction;

			InitializeComponent();
		}

		private void OverlayArea_Click(object sender, MouseEventArgs e)
		{
			this._areaClickAction(this, e);
		}

		public void SetOverlayLabel(string label)
		{
			this.OverlayLabel.Text = label;
        }

		public Color ColorConvertor(string colorSetting)
		{
			if(colorSetting == null || colorSetting.Trim().ToLower() == "none" || colorSetting.Trim().ToLower() == "transparent") {
				return Color.Transparent;
			}

			if(colorSetting.StartsWith("#"))
			{
				if(colorSetting.Length == 9)
				{
                    return Color.FromArgb(
                        Int32.Parse(colorSetting.Substring(1), System.Globalization.NumberStyles.HexNumber)
                    );
                }
				if(colorSetting.Length == 4 ||  colorSetting.Length == 7) {
					return ColorTranslator.FromHtml(colorSetting);
                }
			}

			return Color.Black;
		}

		public void FormatLabel()
        {
            int paddedOffset = this.highlighted ? this._config.PaddingWidthActive : this._config.PaddingWidth;
            Color paddedColor = this.ColorConvertor(this.highlighted ? this._config.PaddingColorActive : this._config.PaddingColor);
            Color labelBackgroundColor = this.ColorConvertor(this.highlighted ? this._config.LabelBackgroundActive : this._config.LabelBackground);
            Color labelForegroundColor = this.ColorConvertor(this.highlighted ? this._config.LabelForegroundActive : this._config.LabelForeground);

			this.OverlayLabel.ForeColor = labelForegroundColor;
            this.OverlayLabel.BackColor = labelBackgroundColor;
            this.OverlayLabel.Font = new Font(
				this._config.LabelFontName,
				0F + this._config.LabelFontSize,
				FontStyle.Regular
			);

            int renderedHeight = TextRenderer.MeasureText(this.OverlayLabel.Text, this.OverlayLabel.Font).Height + paddedOffset;
            this.OverlayLabel.AutoSize = false;
            this.OverlayLabel.Height = renderedHeight;
				/*Math.Max(
				this._config.ThumbnailSize.Width,
				this.Owner.ClientSize.Width
			) - 100;*/

            switch (this._config.LabelHorizontalAlign.ToLower().Trim())
            {
                case "left":
                    this.OverlayLabel.TextAlign = ContentAlignment.TopLeft;
                    break;
                case "right":
					this.OverlayLabel.TextAlign = ContentAlignment.TopRight;
					break;
                case "center":
                    this.OverlayLabel.TextAlign = ContentAlignment.TopCenter;
                    break;
            }

            this.OverlayLabel.Width = this.Owner.ClientSize.Width - (paddedOffset * 2);
            this.OverlayLabel.Location =
				(this._config.LabelVerticalAlign.ToLower().Trim() == "bottom")
				? new Point(paddedOffset, this.Owner.ClientSize.Height - renderedHeight - paddedOffset)
				: new Point(paddedOffset, paddedOffset);

            this.paddingB.Visible = true;
            this.paddingB.AutoSize = false;
            this.paddingB.Height = paddedOffset;
            this.paddingB.Width = this.Owner.ClientSize.Width;
			this.paddingB.Location = new Point(paddedOffset, this.Owner.ClientSize.Height - paddedOffset);
            this.paddingB.BackColor = paddedColor;

            this.paddingT.Visible = true;
            this.paddingT.AutoSize = false;
            this.paddingT.Height = paddedOffset;
            this.paddingT.Width = this.Owner.ClientSize.Width;
            this.paddingT.Location = new Point(paddedOffset, 0);
            this.paddingT.BackColor = paddedColor;

            this.paddingR.Visible = true;
            this.paddingR.AutoSize = false;
			this.paddingR.Height = this.Owner.ClientSize.Height;
            this.paddingR.Width = paddedOffset;
            this.paddingR.Location = new Point(this.Owner.ClientSize.Width - paddedOffset, 0);
            this.paddingR.BackColor = paddedColor;

            this.paddingL.Visible = true;
            this.paddingL.AutoSize = false;
            this.paddingL.Height = this.Owner.ClientSize.Height;
            this.paddingL.Width = paddedOffset;
			this.paddingL.Location = new Point(0, 0);
            this.paddingL.BackColor = paddedColor;

            if (this._config.ShowThumbnailOverlays) { this.OverlayLabel.Visible = true; }
        }

        public void EnableOverlayLabel(bool enable)
		{
			this.OverlayLabel.Visible = enable;
		}

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
