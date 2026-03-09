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

using EveOPreview.Configuration.Implementation;
using EveOPreview.Mediator.Messages;
using EveOPreview.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace EveOPreview.View
{
	public partial class MainForm : Form, IMainFormView
	{
		#region Private fields
		private readonly ApplicationContext _context;
		private readonly Dictionary<ViewZoomAnchor, RadioButton> _zoomAnchorMap;
		private ViewZoomAnchor _cachedThumbnailZoomAnchor;
		private bool _suppressEvents;
		private Size _minimumSize;
		private Size _maximumSize;
		#endregion

		public MainForm(ApplicationContext context)
		{
			this._context = context;
			this._zoomAnchorMap = new Dictionary<ViewZoomAnchor, RadioButton>();
			this._cachedThumbnailZoomAnchor = ViewZoomAnchor.NW;
			this._suppressEvents = false;
			this._minimumSize = new Size(80, 60);
			this._maximumSize = new Size(80, 60);

			InitializeComponent();

			this.ThumbnailsList.DisplayMember = "Title";

            this.InitZoomAnchorMap();
		}

		public List<CycleGroup> CycleGroups { get; set; }

		public bool MinimizeToTray
		{
			get => this.MinimizeToTrayCheckBox.Checked;
			set => this.MinimizeToTrayCheckBox.Checked = value;
		}

		public double ThumbnailOpacity
		{
			get => Math.Min(this.ThumbnailOpacityTrackBar.Value / 100.00, 1.00);
			set
			{
				int barValue = (int)(100.0 * value);
				if (barValue > 100)
				{
					barValue = 100;
				}
				else if (barValue < 10)
				{
					barValue = 10;
				}

				this.ThumbnailOpacityTrackBar.Value = barValue;
			}
		}

		public bool EnableClientLayoutTracking
		{
			get => this.EnableClientLayoutTrackingCheckBox.Checked;
			set => this.EnableClientLayoutTrackingCheckBox.Checked = value;
		}

		public bool HideActiveClientThumbnail
		{
			get => this.HideActiveClientThumbnailCheckBox.Checked;
			set => this.HideActiveClientThumbnailCheckBox.Checked = value;
		}

		public bool MinimizeInactiveClients
		{
			get => this.MinimizeInactiveClientsCheckBox.Checked;
			set => this.MinimizeInactiveClientsCheckBox.Checked = value;
		}

		public bool ShowThumbnailsAlwaysOnTop
		{
			get => this.ShowThumbnailsAlwaysOnTopCheckBox.Checked;
			set => this.ShowThumbnailsAlwaysOnTopCheckBox.Checked = value;
		}

		public bool HideThumbnailsOnLostFocus
		{
			get => this.HideThumbnailsOnLostFocusCheckBox.Checked;
			set => this.HideThumbnailsOnLostFocusCheckBox.Checked = value;
		}

		public bool EnablePerClientThumbnailLayouts
		{
			get => this.EnablePerClientThumbnailsLayoutsCheckBox.Checked;
			set => this.EnablePerClientThumbnailsLayoutsCheckBox.Checked = value;
		}

		public Size ThumbnailSize
		{
			get => new Size((int)this.ThumbnailsWidthNumericEdit.Value, (int)this.ThumbnailsHeightNumericEdit.Value);
			set
			{
				this.ThumbnailsWidthNumericEdit.Value = value.Width;
				this.ThumbnailsHeightNumericEdit.Value = value.Height;
			}
		}

		public bool EnableThumbnailZoom
		{
			get => this.EnableThumbnailZoomCheckBox.Checked;
			set
			{
				this.EnableThumbnailZoomCheckBox.Checked = value;
				this.RefreshZoomSettings();
			}
		}

		public int ThumbnailZoomFactor
		{
			get => (int)this.ThumbnailZoomFactorNumericEdit.Value;
			set => this.ThumbnailZoomFactorNumericEdit.Value = value;
		}

		public ViewZoomAnchor ThumbnailZoomAnchor
		{
			get
			{
				if (this._zoomAnchorMap[this._cachedThumbnailZoomAnchor].Checked)
				{
					return this._cachedThumbnailZoomAnchor;
				}

				foreach (KeyValuePair<ViewZoomAnchor, RadioButton> valuePair in this._zoomAnchorMap)
				{
					if (!valuePair.Value.Checked)
					{
						continue;
					}

					this._cachedThumbnailZoomAnchor = valuePair.Key;
					return this._cachedThumbnailZoomAnchor;
				}

				// Default value
				return ViewZoomAnchor.NW;
			}
			set
			{
				this._cachedThumbnailZoomAnchor = value;
				this._zoomAnchorMap[this._cachedThumbnailZoomAnchor].Checked = true;
			}
		}

		public bool ShowThumbnailOverlays
		{
			get => this.ShowThumbnailOverlaysCheckBox.Checked;
			set => this.ShowThumbnailOverlaysCheckBox.Checked = value;
		}

		public bool ShowThumbnailFrames
		{
			get => this.ShowThumbnailFramesCheckBox.Checked;
			set => this.ShowThumbnailFramesCheckBox.Checked = value;
		}

		public bool EnableActiveClientHighlight
		{
			get => this.EnableActiveClientHighlightCheckBox.Checked;
			set => this.EnableActiveClientHighlightCheckBox.Checked = value;
		}

		public Color ActiveClientHighlightColor
		{
			get => this._activeClientHighlightColor;
			set
			{
				this._activeClientHighlightColor = value;
				this.ActiveClientHighlightColorButton.BackColor = value;
			}
		}
		private Color _activeClientHighlightColor;

        public FontSettings TitleFontSettings
        {
            get
            {
                var result = new FontSettings();
                result.Name = lblDisplaySampleFont.Font.FontFamily.Name;
                result.Size = lblDisplaySampleFont.Font.Size;
				result.Style = lblDisplaySampleFont.Font.Style;
                result.ForeColor = lblDisplaySampleFont.ForeColor;
                result.OutlineColor = lblDisplaySampleFont.OutlineColor;
                result.OutlineWidth = lblDisplaySampleFont.OutlineWidth;
                result.PositionOffsetFromLeft = int.Parse(txtTitleOffsetLeft.Text);
                result.PositionOffsetFromTop = int.Parse(txtTitleOffsetTop.Text);

                return result;
            }
            set
            {
                if (value?.Name == null || value?.Size < 0)
                {
					return;
                }

                lblDisplaySampleFont.OutlineColor = value.OutlineColor;
                lblDisplaySampleFont.OutlineWidth = value.OutlineWidth;
                lblDisplaySampleFont.Font = new Font(value.Name, value.Size, value.Style);
                lblDisplaySampleFont.ForeColor = value.ForeColor;
                txtFontOutlineWidth.Text = value.OutlineWidth.ToString(CultureInfo.InvariantCulture);
                txtTitleOffsetLeft.Text = value.PositionOffsetFromLeft.ToString();
                txtTitleOffsetTop.Text = value.PositionOffsetFromTop.ToString();
            }
        }

        private FpsLimiterSettings _fpsLimiterSettings;

        public FpsLimiterSettings FpsLimiterSettings
        {
            get
            {
                return _fpsLimiterSettings;
            }
            set
            {
                _fpsLimiterSettings = value;
                numericFpsForegroundLimit.Value = value.FpsFocused;
                numericFpsBackgroundLimit.Value = value.FpsBackground;
                numericFpsPredictedLimit.Value = value.FpsPredictingFocus;
                chbIsFpsThrottlingEnabled.Checked = value.IsEnabled;
            }
        }

        private AudioMuteSettings _audioMuteSettings;

        public AudioMuteSettings AudioMuteSettings
        {
            get
            {
                return _audioMuteSettings;
            }
            set
            {
                _audioMuteSettings = value;
                chbIsGateTunnelMuted.Checked = value.MuteJumpGateTunnel;
                chbIsLocationBannerMuted.Checked = value.MuteLocationBanner;
            }
        }

        private bool _isPremium;

        public bool IsPremium
        {
            get
            {
                return _isPremium;
            }
            set
            {
                _isPremium = value;
                
                if (!value)
                {
                    numericFpsForegroundLimit.Enabled = false;
                    numericFpsBackgroundLimit.Enabled = false;
                    numericFpsPredictedLimit.Enabled = false;
                    chbIsFpsThrottlingEnabled.Enabled = false;
                    chbIsFpsThrottlingEnabled.Checked = false;
                    lblFpsFeatureExpired.Visible = true;
                    groupBoxFpsLimits.Visible = false;
                    groupBoxAudioMuting.Visible = false;
                }
            }
        }

        public new void Show()
		{
			// Registers the current instance as the application's Main Form
			this._context.MainForm = this;

			this._suppressEvents = true;
			this.FormActivated?.Invoke();
			this._suppressEvents = false;

			Application.Run(this._context);
		}

		public void SetThumbnailSizeLimitations(Size minimumSize, Size maximumSize)
		{
			this._minimumSize = minimumSize;
			this._maximumSize = maximumSize;
		}

		public void Minimize()
		{
			this.WindowState = FormWindowState.Minimized;
		}

		public void SetVersionInfo(string version)
		{
			this.VersionLabel.Text = version;
		}

		public void SetDocumentationUrl(string url)
		{
			this.DocumentationLink.Text = url;
		}

		public void AddThumbnails(IList<IThumbnailDescription> thumbnails)
		{
			this.ThumbnailsList.BeginUpdate();

			foreach (IThumbnailDescription view in thumbnails)
			{
				this.ThumbnailsList.SetItemChecked(this.ThumbnailsList.Items.Add(view), view.IsDisabled);
			}

			this.ThumbnailsList.EndUpdate();
		}

		public void RemoveThumbnails(IList<IThumbnailDescription> thumbnails)
		{
			this.ThumbnailsList.BeginUpdate();

			foreach (IThumbnailDescription view in thumbnails)
			{
				this.ThumbnailsList.Items.Remove(view);
			}

			this.ThumbnailsList.EndUpdate();
		}

		public void RefreshZoomSettings()
		{
			bool enableControls = this.EnableThumbnailZoom;
			this.ThumbnailZoomFactorNumericEdit.Enabled = enableControls;
			this.ZoomAnchorPanel.Enabled = enableControls;
		}

		public Action ApplicationExitRequested { get; set; }

		public Action FormActivated { get; set; }

		public Action FormMinimized { get; set; }

		public Action<ViewCloseRequest> FormCloseRequested { get; set; }

		public Action ApplicationSettingsChanged { get; set; }

		public Action ThumbnailsSizeChanged { get; set; }

		public Action<string> ThumbnailStateChanged { get; set; }

		public Action DocumentationLinkActivated { get; set; }

		public Func<string> GetClientNameFromInput { get; set; }
        
		public Func<string, CaptureNewHotkeyResponse> CaptureNewHotkey { get; set; }

        public Action FpsLimiterChanged { get; set; }
        public Action FpsLimiterEnabledChanged { get; set; }
        public Action AudioSettingsChanged { get; set; }

        #region UI events
        private void ContentTabControl_DrawItem(object sender, DrawItemEventArgs e)
		{
			TabControl control = (TabControl)sender;
			TabPage page = control.TabPages[e.Index];
			Rectangle bounds = control.GetTabRect(e.Index);

			Graphics graphics = e.Graphics;

			Brush textBrush = new SolidBrush(SystemColors.ActiveCaptionText);
			Brush backgroundBrush = (e.State == DrawItemState.Selected)
										? new SolidBrush(SystemColors.Control)
										: new SolidBrush(SystemColors.ControlDark);
			graphics.FillRectangle(backgroundBrush, e.Bounds);

			// Use our own font
			Font font = new Font("Arial", this.Font.Size * 1.5f, FontStyle.Bold, GraphicsUnit.Pixel);

			// Draw string and center the text
			StringFormat stringFlags = new StringFormat();
			stringFlags.Alignment = StringAlignment.Center;
			stringFlags.LineAlignment = StringAlignment.Center;

			graphics.DrawString(page.Text, font, textBrush, bounds, stringFlags);
		}

		private void OptionChanged_Handler(object sender, EventArgs e)
		{
			if (this._suppressEvents)
			{
				return;
			}

			this.ApplicationSettingsChanged?.Invoke();
		}

		private void ThumbnailSizeChanged_Handler(object sender, EventArgs e)
		{
			if (this._suppressEvents)
			{
				return;
			}

			// Perform some View work that is not properly done in the Control
			this._suppressEvents = true;
			Size thumbnailSize = this.ThumbnailSize;
			thumbnailSize.Width = Math.Min(Math.Max(thumbnailSize.Width, this._minimumSize.Width), this._maximumSize.Width);
			thumbnailSize.Height = Math.Min(Math.Max(thumbnailSize.Height, this._minimumSize.Height), this._maximumSize.Height);
			this.ThumbnailSize = thumbnailSize;
			this._suppressEvents = false;

			this.ThumbnailsSizeChanged?.Invoke();
		}

		private void ActiveClientHighlightColorButton_Click(object sender, EventArgs e)
		{
			using (ColorDialog dialog = new ColorDialog())
			{
				dialog.Color = this.ActiveClientHighlightColor;

				if (dialog.ShowDialog() != DialogResult.OK)
				{
					return;
				}

				this.ActiveClientHighlightColor = dialog.Color;
			}

			this.OptionChanged_Handler(sender, e);
		}

		private void ThumbnailsList_ItemCheck_Handler(object sender, ItemCheckEventArgs e)
		{
			if (!(this.ThumbnailsList.Items[e.Index] is IThumbnailDescription selectedItem))
			{
				return;
			}

			selectedItem.IsDisabled = (e.NewValue == CheckState.Checked);

			this.ThumbnailStateChanged?.Invoke(selectedItem.Title);
		}

		private void DocumentationLinkClicked_Handler(object sender, LinkLabelLinkClickedEventArgs e)
		{
			this.DocumentationLinkActivated?.Invoke();
		}

		private void MainFormResize_Handler(object sender, EventArgs e)
        {
            if (this.WindowState != FormWindowState.Minimized)
            {
                return;
            }

            RefreshCycleGroups();

            this.FormMinimized?.Invoke();
        }

        private void RefreshCycleGroups()
        {
            selectCycleGroupComboBox.DataSource = null;
            selectCycleGroupComboBox.DataSource = CycleGroups;
            selectCycleGroupComboBox.DisplayMember = "Description";
            selectCycleGroupComboBox.Update();
			RefreshSelectedCycleGroup();
        }

		private void RefreshSelectedCycleGroup()
		{
			var selectedGroup = selectCycleGroupComboBox.SelectedItem as CycleGroup;

			if (selectedGroup == null)
			{
				return;
			}

            cycleGroupDescriptionText.Text = selectedGroup.Description;
			cycleGroupForwardHotkey1Text.Text = selectedGroup.ForwardHotkeys.FirstOrDefault();
			cycleGroupForwardHotkey2Text.Text = selectedGroup.ForwardHotkeys.Skip(1).FirstOrDefault();

            cycleGroupBackwardHotkey1Text.Text = selectedGroup.BackwardHotkeys.FirstOrDefault();
            cycleGroupBackwardHotkey2Text.Text = selectedGroup.BackwardHotkeys.Skip(1).FirstOrDefault();

            cycleGroupClientOrderList.DataSource = null;
            cycleGroupClientOrderList.DataSource = new BindingSource(selectedGroup.ClientsOrder, null);
            selectCycleGroupComboBox.DisplayMember = "Value";
            cycleGroupClientOrderList.Update();
        }

        private void MainFormClosing_Handler(object sender, FormClosingEventArgs e)
		{
			ViewCloseRequest request = new ViewCloseRequest();

			this.FormCloseRequested?.Invoke(request);

			e.Cancel = !request.Allow;
		}

		private void RestoreMainForm_Handler(object sender, EventArgs e)
		{
			// This is form's GUI lifecycle event that is invariant to the Form data
			base.Show();
			this.WindowState = FormWindowState.Normal;
			this.BringToFront();
		}

		private void ExitMenuItemClick_Handler(object sender, EventArgs e)
		{
			this.ApplicationExitRequested?.Invoke();
		}
		#endregion

		private void InitZoomAnchorMap()
		{
			this._zoomAnchorMap[ViewZoomAnchor.NW] = this.ZoomAanchorNWRadioButton;
			this._zoomAnchorMap[ViewZoomAnchor.N] = this.ZoomAanchorNRadioButton;
			this._zoomAnchorMap[ViewZoomAnchor.NE] = this.ZoomAanchorNERadioButton;
			this._zoomAnchorMap[ViewZoomAnchor.W] = this.ZoomAanchorWRadioButton;
			this._zoomAnchorMap[ViewZoomAnchor.C] = this.ZoomAanchorCRadioButton;
			this._zoomAnchorMap[ViewZoomAnchor.E] = this.ZoomAanchorERadioButton;
			this._zoomAnchorMap[ViewZoomAnchor.SW] = this.ZoomAanchorSWRadioButton;
			this._zoomAnchorMap[ViewZoomAnchor.S] = this.ZoomAanchorSRadioButton;
			this._zoomAnchorMap[ViewZoomAnchor.SE] = this.ZoomAanchorSERadioButton;
		}

        private void addClientToCycleGroupButton_Click(object sender, EventArgs e)
        {
			var toonToAdd = this.GetClientNameFromInput();

            var selectedGroup = selectCycleGroupComboBox.SelectedItem as CycleGroup;

            if (selectedGroup == null)
            {
                return;
            }

			if (selectedGroup.ClientsOrder.ContainsValue(toonToAdd))
			{
				MessageBox.Show($"{toonToAdd} is already part of this group.");
				return;
			}

			var nextOrderNumber = selectedGroup.ClientsOrder.Any() ? selectedGroup.ClientsOrder.Max(x => x.Key) + 1 : 1;
			selectedGroup.ClientsOrder.Add(nextOrderNumber, toonToAdd);

			RefreshSelectedCycleGroup();
            this.ApplicationSettingsChanged?.Invoke();
        }

        private void removeClientToCycleGroupButton_Click(object sender, EventArgs e)
        {
            var selectedGroup = selectCycleGroupComboBox.SelectedItem as CycleGroup;

            if (selectedGroup == null)
            {
                return;
            }

            if (cycleGroupClientOrderList.SelectedIndex < 0)
            {
                return;
            }

            var KeyToRemove = ((KeyValuePair<int, string>)cycleGroupClientOrderList.SelectedItem).Key;
            selectedGroup.ClientsOrder.Remove(KeyToRemove);

            RefreshSelectedCycleGroup();
            this.ApplicationSettingsChanged?.Invoke();
        }

        private void selectCycleGroupComboBox_SelectedValueChanged(object sender, EventArgs e)
        {
            RefreshSelectedCycleGroup();
        }

        private void cycleGroupMoveClientOrderUpButton_Click(object sender, EventArgs e)
        {
            var selectedGroup = selectCycleGroupComboBox.SelectedItem as CycleGroup;

            if (selectedGroup == null)
            {
                return;
            }

			if (cycleGroupClientOrderList.SelectedIndex < 0)
			{
				return;
			}

			var KeyToMoveUpOne = ((KeyValuePair<int, string>)cycleGroupClientOrderList.SelectedItem).Key;

			int previousKey = 0;
			foreach (var item in selectedGroup.ClientsOrder)
			{
                if (item.Key == KeyToMoveUpOne)
                {
                    break;
                }

                previousKey = item.Key;
            }

			if (previousKey == 0)
			{
				return;
			}

			var previousValue = selectedGroup.ClientsOrder[previousKey];
			var valueToMoveUp = selectedGroup.ClientsOrder[KeyToMoveUpOne];

			selectedGroup.ClientsOrder[previousKey] = valueToMoveUp;
            selectedGroup.ClientsOrder[KeyToMoveUpOne] = previousValue;

            RefreshSelectedCycleGroup();
            this.ApplicationSettingsChanged?.Invoke();
        }

        private void cycleGroupDescriptionText_Leave(object sender, EventArgs e)
        {
            var selectedGroup = selectCycleGroupComboBox.SelectedItem as CycleGroup;

            if (selectedGroup == null)
            {
                return;
            }

			int groupsWithSameText = CycleGroups.Count(x => x.Description == cycleGroupDescriptionText.Text);
			if (groupsWithSameText > 0)
			{
				// It's either this groups name already or already taken, either way we won't change anything.
				return;
			}

            selectedGroup.Description = cycleGroupDescriptionText.Text;

            this.ApplicationSettingsChanged?.Invoke();
			RefreshCycleGroups();
        }

        private void addNewGroupButton_Click(object sender, EventArgs e)
        {
			var newName = "New Cycle Group";
			var countGroupsWithSameName = CycleGroups.Count(x => x.Description.StartsWith(newName));

			if (countGroupsWithSameName > 0)
			{
				newName += $"({countGroupsWithSameName + 1})";
            }

			CycleGroups.Add(new CycleGroup { Description = newName });

            this.ApplicationSettingsChanged?.Invoke();
            RefreshCycleGroups();
        }

        private void removeGroupButton_Click(object sender, EventArgs e)
        {
            var selectedGroup = selectCycleGroupComboBox.SelectedItem as CycleGroup;

            if (selectedGroup == null)
            {
                return;
            }

			CycleGroups.Remove(selectedGroup);

            this.ApplicationSettingsChanged?.Invoke();
            selectCycleGroupComboBox.SelectedItem = selectCycleGroupComboBox.Items[0];
            RefreshCycleGroups();
        }

        private void cycleGroupForwardHotkey1Text_DoubleClick(object sender, EventArgs e)
        {
            var selectedGroup = selectCycleGroupComboBox.SelectedItem as CycleGroup;

            if (selectedGroup == null)
            {
                return;
            }

            if (WaitForHotkeyCapture(cycleGroupForwardHotkey1Text, out var captureHotkeyResponse))
            {
                return;
            }

            cycleGroupForwardHotkey1Text.Text = captureHotkeyResponse.KeyString;
			if (!selectedGroup.ForwardHotkeys.Any())
			{
				selectedGroup.ForwardHotkeys.Add(captureHotkeyResponse.KeyString);
            }
            else
            {
				selectedGroup.ForwardHotkeys[0] = captureHotkeyResponse.KeyString;
            }

            this.ApplicationSettingsChanged?.Invoke();
        }
        
        private void cycleGroupForwardHotkey2Text_DoubleClick(object sender, EventArgs e)
        {
            var selectedGroup = selectCycleGroupComboBox.SelectedItem as CycleGroup;

            if (selectedGroup == null)
            {
                return;
            }

            if (WaitForHotkeyCapture(cycleGroupForwardHotkey2Text, out var captureHotkeyResponse))
            {
                return;
            }

            cycleGroupForwardHotkey2Text.Text = captureHotkeyResponse.KeyString;
            if (selectedGroup.ForwardHotkeys.Count < 2)
            {
                selectedGroup.ForwardHotkeys.Add(captureHotkeyResponse.KeyString);
            }
            else
            {
                selectedGroup.ForwardHotkeys[1] = captureHotkeyResponse.KeyString;
            }

            this.ApplicationSettingsChanged?.Invoke();
        }

        private bool WaitForHotkeyCapture(TextBox inputBox, out CaptureNewHotkeyResponse captureHotkeyResponse)
        {
            var previousValue = inputBox.Text;
            inputBox.Text = "Listening...";
            this.Enabled = false;
            captureHotkeyResponse = this.CaptureNewHotkey(previousValue);
            this.Enabled = true;

            if (!captureHotkeyResponse.IsValid)
            {
                inputBox.Text = previousValue;
                MessageBox.Show(captureHotkeyResponse.ErrorMessage);
                return true;
            }

            return false;
        }

        private void cycleGroupBackwardHotkey1Text_DoubleClick(object sender, EventArgs e)
        {
            var selectedGroup = selectCycleGroupComboBox.SelectedItem as CycleGroup;

            if (selectedGroup == null)
            {
                return;
            }

            if (WaitForHotkeyCapture(cycleGroupBackwardHotkey1Text, out var captureHotkeyResponse))
            {
                return;
            }

            cycleGroupBackwardHotkey1Text.Text = captureHotkeyResponse.KeyString;
            if (!selectedGroup.BackwardHotkeys.Any())
            {
                selectedGroup.BackwardHotkeys.Add(captureHotkeyResponse.KeyString);
            }
            else
            {
                selectedGroup.BackwardHotkeys[0] = captureHotkeyResponse.KeyString;
            }

            this.ApplicationSettingsChanged?.Invoke();
        }

        private void cycleGroupBackwardHotkey2Text_DoubleClick(object sender, EventArgs e)
        {
            var selectedGroup = selectCycleGroupComboBox.SelectedItem as CycleGroup;

            if (selectedGroup == null)
            {
                return;
            }

            if (WaitForHotkeyCapture(cycleGroupBackwardHotkey2Text, out var captureHotkeyResponse))
            {
                return;
            }

            cycleGroupBackwardHotkey2Text.Text = captureHotkeyResponse.KeyString;
            if (selectedGroup.BackwardHotkeys.Count < 2)
            {
                selectedGroup.BackwardHotkeys.Add(captureHotkeyResponse.KeyString);
            }
            else
            {
                selectedGroup.BackwardHotkeys[1] = captureHotkeyResponse.KeyString;
            }

            this.ApplicationSettingsChanged?.Invoke();
        }

        private void btnSetOverlayFont_Click(object sender, EventArgs e)
        {
            FontDialog fontDialog = new FontDialog();
            fontDialog.Font = lblDisplaySampleFont.Font;

            if (fontDialog.ShowDialog() == DialogResult.OK)
            {
                lblDisplaySampleFont.Font = fontDialog.Font;

                this.ApplicationSettingsChanged?.Invoke();
            }
        }

        private void btnSetOverlayFontColor_Click(object sender, EventArgs e)
        {
            ColorDialog colorDialog = new ColorDialog();
            colorDialog.Color = lblDisplaySampleFont.ForeColor;

            if (colorDialog.ShowDialog() == DialogResult.OK)
            {
                lblDisplaySampleFont.ForeColor = colorDialog.Color;
                this.ApplicationSettingsChanged?.Invoke();
            }
        }

        private void btnFontOutlineColor_Click(object sender, EventArgs e)
        {
            ColorDialog colorDialog = new ColorDialog();
            colorDialog.Color = lblDisplaySampleFont.OutlineColor;

            if (colorDialog.ShowDialog() == DialogResult.OK)
            {
                lblDisplaySampleFont.OutlineColor = colorDialog.Color;
                this.ApplicationSettingsChanged?.Invoke();
            }
        }

        private void UpdateFontOutlineWidth()
        {
            var newValue = new string(txtFontOutlineWidth.Text.TakeWhile(char.IsNumber).ToArray());
            txtFontOutlineWidth.Text = newValue;

            var newFloatValue = float.Parse(newValue);

            lblDisplaySampleFont.OutlineWidth = newFloatValue;
            this.ApplicationSettingsChanged?.Invoke();
        }

        private void UpdateTitleOffset()
        {
			var cleanOffsetLeft = new string(txtTitleOffsetLeft.Text.TakeWhile(char.IsNumber).ToArray());
			var cleanOffsetTop = new string(txtTitleOffsetTop.Text.TakeWhile(char.IsNumber).ToArray());

            txtTitleOffsetLeft.Text = cleanOffsetLeft;
            txtTitleOffsetTop.Text = cleanOffsetTop;

            var offsetLeft = int.Parse(cleanOffsetLeft);
            var offsetTop = int.Parse(cleanOffsetTop);

            this.TitleFontSettings.PositionOffsetFromLeft = offsetLeft;
            this.TitleFontSettings.PositionOffsetFromTop = offsetTop;
            this.ApplicationSettingsChanged?.Invoke();
        }

        private void txtTitleOffsetLeft_Leave(object sender, EventArgs e)
        {
            UpdateTitleOffset();
        }

        private void txtTitleOffsetLeft_Enter(object sender, EventArgs e)
        {
            UpdateTitleOffset();
        }

        private void txtTitleOffsetTop_Leave(object sender, EventArgs e)
        {
            UpdateTitleOffset();
        }

        private void txtTitleOffsetTop_Enter(object sender, EventArgs e)
        {
            UpdateTitleOffset();
        }

        private void txtFontOutlineWidth_Leave(object sender, EventArgs e)
        {
            UpdateFontOutlineWidth();
        }

        private void txtFontOutlineWidth_Enter(object sender, EventArgs e)
        {
            UpdateFontOutlineWidth();
        }

        private void chbIsFpsThrottlingEnabled_CheckedChanged(object sender, EventArgs e)
        {
            FpsLimiterSettings.IsEnabled = chbIsFpsThrottlingEnabled.Checked;
            this.ApplicationSettingsChanged?.Invoke();
            this.FpsLimiterEnabledChanged?.Invoke();
        }

        private void numericFpsForegroundLimit_Leave(object sender, EventArgs e)
        {
            FpsLimiterSettings.FpsFocused = (int)numericFpsForegroundLimit.Value;
            this.ApplicationSettingsChanged?.Invoke();
            this.FpsLimiterChanged?.Invoke();
        }

        private void numericFpsBackgroundLimit_Leave(object sender, EventArgs e)
        {
            FpsLimiterSettings.FpsBackground = (int)numericFpsBackgroundLimit.Value;
            this.ApplicationSettingsChanged?.Invoke();
            this.FpsLimiterChanged?.Invoke();
        }

        private void numericFpsPredictedLimit_Leave(object sender, EventArgs e)
        {
            FpsLimiterSettings.FpsPredictingFocus = (int)numericFpsPredictedLimit.Value;
            this.ApplicationSettingsChanged?.Invoke();
            this.FpsLimiterChanged?.Invoke();
        }

        private void chbIsGateTunnelMuted_CheckedChanged(object sender, EventArgs e)
        {
            AnyAudioSettings_CheckedChanged();
        }

        private void chbIsLocationBannerMuted_CheckedChanged(object sender, EventArgs e)
        {
            AnyAudioSettings_CheckedChanged();
        }

        private void AnyAudioSettings_CheckedChanged()
        {
            this.AudioMuteSettings.MuteJumpGateTunnel = chbIsGateTunnelMuted.Checked;
            this.AudioMuteSettings.MuteLocationBanner = chbIsLocationBannerMuted.Checked;
            
            this.ApplicationSettingsChanged?.Invoke();
            this.AudioSettingsChanged?.Invoke();
        }
    }
}
