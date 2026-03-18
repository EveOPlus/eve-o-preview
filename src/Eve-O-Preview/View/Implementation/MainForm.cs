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
using EveOPreview.Configuration.Model;
using EveOPreview.Mediator.Messages;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.ComponentModel;

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

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public List<CycleGroup> CycleGroups
        {
            get;
            set
            {
                this._suppressEvents = true;
                field = value;
                RefreshCycleGroups();
                this._suppressEvents = false;
            }
        } = [];

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool MinimizeToTray
        {
            get => this.MinimizeToTrayCheckBox.Checked;
            set
            {
                this._suppressEvents = true;

                this.MinimizeToTrayCheckBox.Checked = value;
                this._suppressEvents = false;

            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public double ThumbnailOpacity
        {
            get => Math.Min(this.ThumbnailOpacityTrackBar.Value / 100.00, 1.00);
            set
            {
                this._suppressEvents = true;
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
                this._suppressEvents = false;
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool EnableClientLayoutTracking
        {
            get => this.EnableClientLayoutTrackingCheckBox.Checked;
            set
            {
                this._suppressEvents = true;
                this.EnableClientLayoutTrackingCheckBox.Checked = value;
                this._suppressEvents = false;
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool HideActiveClientThumbnail
        {
            get => this.HideActiveClientThumbnailCheckBox.Checked;
            set
            {
                this._suppressEvents = true;
                this.HideActiveClientThumbnailCheckBox.Checked = value;
                this._suppressEvents = false;
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool MinimizeInactiveClients
        {
            get => this.MinimizeInactiveClientsCheckBox.Checked;
            set
            {
                this._suppressEvents = true;
                this.MinimizeInactiveClientsCheckBox.Checked = value;
                this._suppressEvents = false;
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool ShowThumbnailsAlwaysOnTop
        {
            get => this.ShowThumbnailsAlwaysOnTopCheckBox.Checked;
            set
            {
                this._suppressEvents = true;
                this.ShowThumbnailsAlwaysOnTopCheckBox.Checked = value;
                this._suppressEvents = false;
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool HideThumbnailsOnLostFocus
        {
            get => this.HideThumbnailsOnLostFocusCheckBox.Checked;
            set
            {
                this._suppressEvents = true;
                this.HideThumbnailsOnLostFocusCheckBox.Checked = value;
                this._suppressEvents = false;
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool EnablePerClientThumbnailLayouts
        {
            get => this.EnablePerClientThumbnailsLayoutsCheckBox.Checked;
            set
            {
                this._suppressEvents = true;
                this.EnablePerClientThumbnailsLayoutsCheckBox.Checked = value;
                this._suppressEvents = false;
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Size ThumbnailSize
        {
            get => new Size((int)this.ThumbnailsWidthNumericEdit.Value, (int)this.ThumbnailsHeightNumericEdit.Value);
            set
            {
                this._suppressEvents = true;
                this.ThumbnailsWidthNumericEdit.Value = value.Width;
                this.ThumbnailsHeightNumericEdit.Value = value.Height;
                this._suppressEvents = false;
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool EnableThumbnailZoom
        {
            get => this.EnableThumbnailZoomCheckBox.Checked;
            set
            {
                this._suppressEvents = true;
                this.EnableThumbnailZoomCheckBox.Checked = value;
                this.RefreshZoomSettings();
                this._suppressEvents = false;
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int ThumbnailZoomFactor
        {
            get => (int)this.ThumbnailZoomFactorNumericEdit.Value;
            set
            {
                this._suppressEvents = true;
                this.ThumbnailZoomFactorNumericEdit.Value = value;
                this._suppressEvents = false;
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
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
                this._suppressEvents = true;
                this._cachedThumbnailZoomAnchor = value;
                this._zoomAnchorMap[this._cachedThumbnailZoomAnchor].Checked = true;
                this._suppressEvents = false;
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool ShowThumbnailOverlays
        {
            get => this.ShowThumbnailOverlaysCheckBox.Checked;
            set
            {
                this._suppressEvents = true;
                this.ShowThumbnailOverlaysCheckBox.Checked = value;
                this._suppressEvents = false;
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool ShowThumbnailFrames
        {
            get => this.ShowThumbnailFramesCheckBox.Checked;
            set
            {
                this._suppressEvents = true;
                this.ShowThumbnailFramesCheckBox.Checked = value;
                this._suppressEvents = false;
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool EnableActiveClientHighlight
        {
            get => this.EnableActiveClientHighlightCheckBox.Checked;
            set
            {
                this._suppressEvents = true;
                this.EnableActiveClientHighlightCheckBox.Checked = value;
                this._suppressEvents = false;
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color ActiveClientHighlightColor
        {
            get => this._activeClientHighlightColor;
            set
            {
                this._suppressEvents = true;
                this._activeClientHighlightColor = value;
                this.ActiveClientHighlightColorButton.BackColor = value;
                this._suppressEvents = false;
            }
        }
        private Color _activeClientHighlightColor;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
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
                this._suppressEvents = true;
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
                this._suppressEvents = false;
            }
        }

        private FpsLimiterSettings _fpsLimiterSettings;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public FpsLimiterSettings FpsLimiterSettings
        {
            get
            {
                return _fpsLimiterSettings;
            }
            set
            {
                this._suppressEvents = true;
                _fpsLimiterSettings = value;
                numericFpsForegroundLimit.Value = value.FpsFocused;
                numericFpsBackgroundLimit.Value = value.FpsBackground;
                numericFpsPredictedLimit.Value = value.FpsPredictingFocus;
                chbIsFpsThrottlingEnabled.Checked = value.IsEnabled;
                this._suppressEvents = false;
            }
        }

        private AudioMuteSettings _audioMuteSettings;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public AudioMuteSettings AudioMuteSettings
        {
            get
            {
                return _audioMuteSettings;
            }
            set
            {
                this._suppressEvents = true;
                _audioMuteSettings = value;
                chbIsGateTunnelMuted.Checked = value.MuteJumpGateTunnel;
                chbIsLocationBannerMuted.Checked = value.MuteLocationBanner;
                this._suppressEvents = false;
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string ToggleHideAllActiveHotkey
        {
            get => this.txtToggleHideAllActiveHotkey.Text;
            set
            {
                this._suppressEvents = true;
                this.txtToggleHideAllActiveHotkey.Text = value;
                this._suppressEvents = false;
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string MinimizeAllClientsHotkey
        {
            get => this.txtMinimizeAllClientsHotkey.Text;
            set
            {
                this._suppressEvents = true;
                this.txtMinimizeAllClientsHotkey.Text = value;
                this._suppressEvents = false;
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string LoadedProfileName
        {
            get => this.txtLoadedProfileName.Text;
            set
            {
                this._suppressEvents = true;
                this.txtLoadedProfileName.Text = value;
                this._suppressEvents = false;
            }
        }


        private bool _isPremium;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool IsPremium
        {
            get
            {
                return _isPremium;
            }
            set
            {
                this._suppressEvents = true;
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
                this._suppressEvents = false;
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

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Action ApplicationExitRequested { get; set; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Action FormActivated { get; set; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Action FormMinimized { get; set; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Action<ViewCloseRequest> FormCloseRequested { get; set; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Action ApplicationSettingsChanged { get; set; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Action ThumbnailsSizeChanged { get; set; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Action<string> ThumbnailStateChanged { get; set; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Action DocumentationLinkActivated { get; set; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Func<string> GetClientNameFromInput { get; set; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Func<string, CaptureNewHotkeyResponse> CaptureNewHotkey { get; set; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Action FpsLimiterChanged { get; set; }
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Action FpsLimiterEnabledChanged { get; set; }
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Action AudioSettingsChanged { get; set; }
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Action ToggleHideAllActiveClients { get; set; }
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Action MinimizeAllClients { get; set; }
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Action CloneCurrentProfile { get; set; }
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Action DeleteCurrentProfile { get; set; }
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Action<string> RenameCurrentProfile { get; set; }
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Action<ProfileLocation> SwitchToProfile { get; set; }


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

        private void txtTitleOffsetTop_Leave(object sender, EventArgs e)
        {
            UpdateTitleOffset();
        }

        private void txtFontOutlineWidth_Leave(object sender, EventArgs e)
        {
            UpdateFontOutlineWidth();
        }

        private void chbIsFpsThrottlingEnabled_CheckedChanged(object sender, EventArgs e)
        {
            if (this._suppressEvents)
            {
                return;
            }

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
            if (this._suppressEvents)
            {
                return;
            }

            AnyAudioSettings_CheckedChanged();
        }

        private void chbIsLocationBannerMuted_CheckedChanged(object sender, EventArgs e)
        {
            if (this._suppressEvents)
            {
                return;
            }

            AnyAudioSettings_CheckedChanged();
        }

        private void AnyAudioSettings_CheckedChanged()
        {
            this.AudioMuteSettings.MuteJumpGateTunnel = chbIsGateTunnelMuted.Checked;
            this.AudioMuteSettings.MuteLocationBanner = chbIsLocationBannerMuted.Checked;

            this.ApplicationSettingsChanged?.Invoke();
            this.AudioSettingsChanged?.Invoke();
        }

        public void UpdateThumbnailToggleHideAllStatus(bool notificationIsHidden)
        {
            this.btnToggleHideAll.Text = notificationIsHidden ? "Show All" : "Hide All";
            this.btnToggleHideAll.BackColor = notificationIsHidden ? Color.RosyBrown : SystemColors.Control;
            this.ClientsTabPage.Text = notificationIsHidden ? "ALL HIDDEN" : "All Clients";
        }

        public void UpdateProfileList(List<ProfileLocation> notificationNewProfileLocations)
        {
            var selectedProfile = txtLoadedProfileName.Text;

            notificationNewProfileLocations =
                notificationNewProfileLocations.OrderByDescending(x => x.FriendlyName == "Default") // Put default on the top, then the rest.
                    .ThenBy(x => x.FriendlyName).ToList();

            listBoxProfiles.DataSource = null;
            listBoxProfiles.DataSource = notificationNewProfileLocations;
            listBoxProfiles.DisplayMember = nameof(ProfileLocation.FriendlyName);
            listBoxProfiles.Update();

            var itemToSelect = notificationNewProfileLocations.FirstOrDefault(x => x.FriendlyName == selectedProfile);
            if (itemToSelect != null)
            {
                listBoxProfiles.SelectedItem = itemToSelect;
            }
        }

        private void btnToggleHideAll_Click(object sender, EventArgs e)
        {
            this.ToggleHideAllActiveClients();
        }

        private void txtToggleHideAllActiveHotkey_DoubleClick(object sender, EventArgs e)
        {
            if (WaitForHotkeyCapture(txtToggleHideAllActiveHotkey, out var captureHotkeyResponse))
            {
                return;
            }

            txtToggleHideAllActiveHotkey.Text = captureHotkeyResponse.KeyString;

            this.ApplicationSettingsChanged?.Invoke();
        }

        private void txtMinimizeAllClientsHotkey_DoubleClick(object sender, EventArgs e)
        {
            if (WaitForHotkeyCapture(txtMinimizeAllClientsHotkey, out var captureHotkeyResponse))
            {
                return;
            }

            txtMinimizeAllClientsHotkey.Text = captureHotkeyResponse.KeyString;

            this.ApplicationSettingsChanged?.Invoke();
        }

        private void btnMinimizeAllClients_Click(object sender, EventArgs e)
        {
            this.MinimizeAllClients();
        }

        private void listBoxProfiles_Click(object sender, EventArgs e)
        {
            var selectedProfile = listBoxProfiles.SelectedItem as ProfileLocation;
            if (selectedProfile == null)
            {
                return;
            }

            this.SwitchToProfile(selectedProfile);
        }

        private void btnCloneProfile_Click(object sender, EventArgs e)
        {
            this.CloneCurrentProfile();
        }

        private void btnDeleteProfile_Click(object sender, EventArgs e)
        {
            if (this.txtLoadedProfileName.Text == "Default")
            {
                MessageBox.Show("Cannot delete the Default profile");
                return;
            }

            DialogResult result = MessageBox.Show(
                $"Are you sure you want to permanently delete the profile '{this.txtLoadedProfileName.Text}'?\n\nThis action cannot be undone. Please ensure you have a backup if needed.",
                "Confirm Deletion",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                this.DeleteCurrentProfile();
            }
        }

        private void txtLoadedProfileName_Leave(object sender, EventArgs e)
        {
            UI_RenameCurrentProfile();
        }

        private void txtLoadedProfileName_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                UI_RenameCurrentProfile();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private void UI_RenameCurrentProfile()
        {
            var newName = txtLoadedProfileName.Text.Trim();

            if (!ValidateProfileName(newName, out var message))
            {
                MessageBox.Show(message);
                return;
            }

            this.RenameCurrentProfile(newName);
        }

        private bool ValidateProfileName(string name, out string errorMessage)
        {
            errorMessage = string.Empty;

            if (name.Length > 50)
            {
                errorMessage = "Profile name cannot exceed 50 characters.";
                return false;
            }

            char[] invalidChars = Path.GetInvalidFileNameChars();
            if (name.IndexOfAny(invalidChars) >= 0)
            {
                errorMessage = "Name contains invalid characters (\\ / : * ? \" < > |)";
                return false;
            }

            if (name.EndsWith(" ") || name.EndsWith("."))
            {
                errorMessage = "Name cannot end with a space or a period.";
                return false;
            }

            return true;
        }

        private void ContentTabControl_DpiChangedAfterParent(object sender, EventArgs e)
        {
            float newDpi = this.ContentTabControl.DeviceDpi;

            float scaleFactor = newDpi / 96f;

            int originalHeight = 120;
            this.ContentTabControl.ItemSize = new Size(this.ContentTabControl.ItemSize.Width, (int)(originalHeight * scaleFactor));
        }
    }
}
