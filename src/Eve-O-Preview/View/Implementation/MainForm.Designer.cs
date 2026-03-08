using System.Drawing;
using System.Windows.Forms;
using EveOPreview.View.CustomControl;

namespace EveOPreview.View
{
	partial class MainForm
	{
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose(bool disposing)
		{
			if (disposing && (components != null))
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Windows Form Designer generated code

		/// <summary>s
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
            this.components = new System.ComponentModel.Container();
            System.Windows.Forms.ToolStripMenuItem RestoreWindowMenuItem;
            System.Windows.Forms.ToolStripMenuItem ExitMenuItem;
            System.Windows.Forms.ToolStripMenuItem TitleMenuItem;
            System.Windows.Forms.ToolStripSeparator SeparatorMenuItem;
            System.Windows.Forms.TabControl ContentTabControl;
            System.Windows.Forms.TabPage GeneralTabPage;
            System.Windows.Forms.Panel GeneralSettingsPanel;
            System.Windows.Forms.TabPage ThumbnailTabPage;
            System.Windows.Forms.Panel ThumbnailSettingsPanel;
            System.Windows.Forms.Label HeigthLabel;
            System.Windows.Forms.Label WidthLabel;
            System.Windows.Forms.Label OpacityLabel;
            System.Windows.Forms.Panel ZoomSettingsPanel;
            System.Windows.Forms.Label ZoomFactorLabel;
            System.Windows.Forms.Label ZoomAnchorLabel;
            System.Windows.Forms.TabPage OverlayTabPage;
            System.Windows.Forms.Panel OverlaySettingsPanel;
            System.Windows.Forms.TabPage ClientsTabPage;
            System.Windows.Forms.Panel ClientsPanel;
            System.Windows.Forms.Label ThumbnailsListLabel;
            System.Windows.Forms.TabPage CycleGroupTabPage;
            System.Windows.Forms.Label label1;
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            System.Windows.Forms.Label lblFpsPredictiveLimit;
            System.Windows.Forms.Label lblFpsBackgroundLimit;
            System.Windows.Forms.Label lblFpsForegroundLimit;
            System.Windows.Forms.TabPage AboutTabPage;
            System.Windows.Forms.Panel AboutPanel;
            System.Windows.Forms.Label lblLiabilityDisclaimer;
            System.Windows.Forms.Label CreditMaintLabel;
            System.Windows.Forms.Label DocumentationLinkLabel;
            System.Windows.Forms.Label DescriptionLabel;
            System.Windows.Forms.Label NameLabel;
            this.MinimizeInactiveClientsCheckBox = new System.Windows.Forms.CheckBox();
            this.EnableClientLayoutTrackingCheckBox = new System.Windows.Forms.CheckBox();
            this.HideActiveClientThumbnailCheckBox = new System.Windows.Forms.CheckBox();
            this.ShowThumbnailsAlwaysOnTopCheckBox = new System.Windows.Forms.CheckBox();
            this.HideThumbnailsOnLostFocusCheckBox = new System.Windows.Forms.CheckBox();
            this.EnablePerClientThumbnailsLayoutsCheckBox = new System.Windows.Forms.CheckBox();
            this.MinimizeToTrayCheckBox = new System.Windows.Forms.CheckBox();
            this.ThumbnailsWidthNumericEdit = new System.Windows.Forms.NumericUpDown();
            this.ThumbnailsHeightNumericEdit = new System.Windows.Forms.NumericUpDown();
            this.ThumbnailOpacityTrackBar = new System.Windows.Forms.TrackBar();
            this.ZoomTabPage = new System.Windows.Forms.TabPage();
            this.ZoomAnchorPanel = new System.Windows.Forms.Panel();
            this.ZoomAanchorNWRadioButton = new System.Windows.Forms.RadioButton();
            this.ZoomAanchorNRadioButton = new System.Windows.Forms.RadioButton();
            this.ZoomAanchorNERadioButton = new System.Windows.Forms.RadioButton();
            this.ZoomAanchorWRadioButton = new System.Windows.Forms.RadioButton();
            this.ZoomAanchorSERadioButton = new System.Windows.Forms.RadioButton();
            this.ZoomAanchorCRadioButton = new System.Windows.Forms.RadioButton();
            this.ZoomAanchorSRadioButton = new System.Windows.Forms.RadioButton();
            this.ZoomAanchorERadioButton = new System.Windows.Forms.RadioButton();
            this.ZoomAanchorSWRadioButton = new System.Windows.Forms.RadioButton();
            this.EnableThumbnailZoomCheckBox = new System.Windows.Forms.CheckBox();
            this.ThumbnailZoomFactorNumericEdit = new System.Windows.Forms.NumericUpDown();
            this.groupBoxOverlayFont = new System.Windows.Forms.GroupBox();
            this.lblTitleOffsetTop = new System.Windows.Forms.Label();
            this.txtTitleOffsetTop = new System.Windows.Forms.TextBox();
            this.lblTitleOffsetLeft = new System.Windows.Forms.Label();
            this.txtTitleOffsetLeft = new System.Windows.Forms.TextBox();
            this.lblTitleBorderWidth = new System.Windows.Forms.Label();
            this.txtFontOutlineWidth = new System.Windows.Forms.TextBox();
            this.btnFontOutlineColor = new System.Windows.Forms.Button();
            this.btnSetOverlayFontColor = new System.Windows.Forms.Button();
            this.lblDisplaySampleFont = new EveOPreview.View.CustomControl.OutlinedLabel();
            this.btnSetOverlayFont = new System.Windows.Forms.Button();
            this.HighlightColorLabel = new System.Windows.Forms.Label();
            this.ActiveClientHighlightColorButton = new System.Windows.Forms.Panel();
            this.EnableActiveClientHighlightCheckBox = new System.Windows.Forms.CheckBox();
            this.ShowThumbnailOverlaysCheckBox = new System.Windows.Forms.CheckBox();
            this.ShowThumbnailFramesCheckBox = new System.Windows.Forms.CheckBox();
            this.activeClientsSplitContainer = new System.Windows.Forms.SplitContainer();
            this.ThumbnailsList = new System.Windows.Forms.CheckedListBox();
            this.CycleGroupPanel = new System.Windows.Forms.Panel();
            this.removeGroupButton = new System.Windows.Forms.Button();
            this.cycleGroupMoveClientOrderUpButton = new System.Windows.Forms.Button();
            this.removeClientToCycleGroupButton = new System.Windows.Forms.Button();
            this.addNewGroupButton = new System.Windows.Forms.Button();
            this.cycleGroupClientOrderLabel = new System.Windows.Forms.Label();
            this.cycleGroupClientOrderList = new System.Windows.Forms.ListBox();
            this.cycleGroupBackwardHotkey2Text = new System.Windows.Forms.TextBox();
            this.cycleGroupBackwardHotkey1Text = new System.Windows.Forms.TextBox();
            this.cycleGroupBackHotkeyLabel = new System.Windows.Forms.Label();
            this.cycleGroupForwardHotkey2Text = new System.Windows.Forms.TextBox();
            this.cycleGroupForwardHotkey1Text = new System.Windows.Forms.TextBox();
            this.cycleGroupForwardHotkeyLabel = new System.Windows.Forms.Label();
            this.cycleGroupDescriptionText = new System.Windows.Forms.TextBox();
            this.cycleGroupDescriptionLabel = new System.Windows.Forms.Label();
            this.CycleGroupLabel = new System.Windows.Forms.Label();
            this.selectCycleGroupComboBox = new System.Windows.Forms.ComboBox();
            this.addClientToCycleGroupButton = new System.Windows.Forms.Button();
            this.FpsLimiterTabPage = new System.Windows.Forms.TabPage();
            this.fpsMainLayoutPanel = new System.Windows.Forms.TableLayoutPanel();
            this.fpsTopPanel = new System.Windows.Forms.Panel();
            this.fpsBottomPanel = new System.Windows.Forms.Panel();
            this.groupBoxAudioMuting = new System.Windows.Forms.GroupBox();
            this.chbIsLocationBannerMuted = new System.Windows.Forms.CheckBox();
            this.chbIsGateTunnelMuted = new System.Windows.Forms.CheckBox();
            this.groupBoxFpsLimits = new System.Windows.Forms.GroupBox();
            this.btnDummyFpsSave = new System.Windows.Forms.Button();
            this.numericFpsPredictedLimit = new System.Windows.Forms.NumericUpDown();
            this.numericFpsBackgroundLimit = new System.Windows.Forms.NumericUpDown();
            this.numericFpsForegroundLimit = new System.Windows.Forms.NumericUpDown();
            this.lblFpsFeatureExpired = new System.Windows.Forms.Label();
            this.chbIsFpsThrottlingEnabled = new System.Windows.Forms.CheckBox();
            this.VersionLabel = new System.Windows.Forms.Label();
            this.DocumentationLink = new System.Windows.Forms.LinkLabel();
            this.NotifyIcon = new System.Windows.Forms.NotifyIcon(this.components);
            this.TrayMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.toolTip = new System.Windows.Forms.ToolTip(this.components);
            this.txtToggleHideAllActiveHotkey = new System.Windows.Forms.TextBox();
            this.lblToggleHideAllActive = new System.Windows.Forms.Label();
            RestoreWindowMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            ExitMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            TitleMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            SeparatorMenuItem = new System.Windows.Forms.ToolStripSeparator();
            ContentTabControl = new System.Windows.Forms.TabControl();
            GeneralTabPage = new System.Windows.Forms.TabPage();
            GeneralSettingsPanel = new System.Windows.Forms.Panel();
            ThumbnailTabPage = new System.Windows.Forms.TabPage();
            ThumbnailSettingsPanel = new System.Windows.Forms.Panel();
            HeigthLabel = new System.Windows.Forms.Label();
            WidthLabel = new System.Windows.Forms.Label();
            OpacityLabel = new System.Windows.Forms.Label();
            ZoomSettingsPanel = new System.Windows.Forms.Panel();
            ZoomFactorLabel = new System.Windows.Forms.Label();
            ZoomAnchorLabel = new System.Windows.Forms.Label();
            OverlayTabPage = new System.Windows.Forms.TabPage();
            OverlaySettingsPanel = new System.Windows.Forms.Panel();
            ClientsTabPage = new System.Windows.Forms.TabPage();
            ClientsPanel = new System.Windows.Forms.Panel();
            ThumbnailsListLabel = new System.Windows.Forms.Label();
            CycleGroupTabPage = new System.Windows.Forms.TabPage();
            label1 = new System.Windows.Forms.Label();
            lblFpsPredictiveLimit = new System.Windows.Forms.Label();
            lblFpsBackgroundLimit = new System.Windows.Forms.Label();
            lblFpsForegroundLimit = new System.Windows.Forms.Label();
            AboutTabPage = new System.Windows.Forms.TabPage();
            AboutPanel = new System.Windows.Forms.Panel();
            lblLiabilityDisclaimer = new System.Windows.Forms.Label();
            CreditMaintLabel = new System.Windows.Forms.Label();
            DocumentationLinkLabel = new System.Windows.Forms.Label();
            DescriptionLabel = new System.Windows.Forms.Label();
            NameLabel = new System.Windows.Forms.Label();
            ContentTabControl.SuspendLayout();
            GeneralTabPage.SuspendLayout();
            GeneralSettingsPanel.SuspendLayout();
            ThumbnailTabPage.SuspendLayout();
            ThumbnailSettingsPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.ThumbnailsWidthNumericEdit)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.ThumbnailsHeightNumericEdit)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.ThumbnailOpacityTrackBar)).BeginInit();
            this.ZoomTabPage.SuspendLayout();
            ZoomSettingsPanel.SuspendLayout();
            this.ZoomAnchorPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.ThumbnailZoomFactorNumericEdit)).BeginInit();
            OverlayTabPage.SuspendLayout();
            OverlaySettingsPanel.SuspendLayout();
            this.groupBoxOverlayFont.SuspendLayout();
            ClientsTabPage.SuspendLayout();
            ClientsPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.activeClientsSplitContainer)).BeginInit();
            this.activeClientsSplitContainer.Panel1.SuspendLayout();
            this.activeClientsSplitContainer.Panel2.SuspendLayout();
            this.activeClientsSplitContainer.SuspendLayout();
            CycleGroupTabPage.SuspendLayout();
            this.CycleGroupPanel.SuspendLayout();
            this.FpsLimiterTabPage.SuspendLayout();
            this.fpsMainLayoutPanel.SuspendLayout();
            this.fpsTopPanel.SuspendLayout();
            this.fpsBottomPanel.SuspendLayout();
            this.groupBoxAudioMuting.SuspendLayout();
            this.groupBoxFpsLimits.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericFpsPredictedLimit)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericFpsBackgroundLimit)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericFpsForegroundLimit)).BeginInit();
            AboutTabPage.SuspendLayout();
            AboutPanel.SuspendLayout();
            this.TrayMenu.SuspendLayout();
            this.SuspendLayout();
            // 
            // RestoreWindowMenuItem
            // 
            RestoreWindowMenuItem.Name = "RestoreWindowMenuItem";
            RestoreWindowMenuItem.Size = new System.Drawing.Size(151, 22);
            RestoreWindowMenuItem.Text = "Restore";
            RestoreWindowMenuItem.Click += new System.EventHandler(this.RestoreMainForm_Handler);
            // 
            // ExitMenuItem
            // 
            ExitMenuItem.Name = "ExitMenuItem";
            ExitMenuItem.Size = new System.Drawing.Size(151, 22);
            ExitMenuItem.Text = "Exit";
            ExitMenuItem.Click += new System.EventHandler(this.ExitMenuItemClick_Handler);
            // 
            // TitleMenuItem
            // 
            TitleMenuItem.Enabled = false;
            TitleMenuItem.Name = "TitleMenuItem";
            TitleMenuItem.Size = new System.Drawing.Size(151, 22);
            TitleMenuItem.Text = "EVE-O Preview";
            // 
            // SeparatorMenuItem
            // 
            SeparatorMenuItem.Name = "SeparatorMenuItem";
            SeparatorMenuItem.Size = new System.Drawing.Size(148, 6);
            // 
            // ContentTabControl
            // 
            ContentTabControl.Alignment = System.Windows.Forms.TabAlignment.Left;
            ContentTabControl.Controls.Add(GeneralTabPage);
            ContentTabControl.Controls.Add(ThumbnailTabPage);
            ContentTabControl.Controls.Add(this.ZoomTabPage);
            ContentTabControl.Controls.Add(OverlayTabPage);
            ContentTabControl.Controls.Add(ClientsTabPage);
            ContentTabControl.Controls.Add(CycleGroupTabPage);
            ContentTabControl.Controls.Add(this.FpsLimiterTabPage);
            ContentTabControl.Controls.Add(AboutTabPage);
            ContentTabControl.Dock = System.Windows.Forms.DockStyle.Fill;
            ContentTabControl.DrawMode = System.Windows.Forms.TabDrawMode.OwnerDrawFixed;
            ContentTabControl.ItemSize = new System.Drawing.Size(35, 120);
            ContentTabControl.Location = new System.Drawing.Point(0, 0);
            ContentTabControl.Multiline = true;
            ContentTabControl.Name = "ContentTabControl";
            ContentTabControl.SelectedIndex = 0;
            ContentTabControl.Size = new System.Drawing.Size(390, 361);
            ContentTabControl.SizeMode = System.Windows.Forms.TabSizeMode.Fixed;
            ContentTabControl.TabIndex = 7;
            ContentTabControl.DrawItem += new System.Windows.Forms.DrawItemEventHandler(this.ContentTabControl_DrawItem);
            // 
            // GeneralTabPage
            // 
            GeneralTabPage.BackColor = System.Drawing.SystemColors.Control;
            GeneralTabPage.Controls.Add(GeneralSettingsPanel);
            GeneralTabPage.Location = new System.Drawing.Point(124, 4);
            GeneralTabPage.Name = "GeneralTabPage";
            GeneralTabPage.Padding = new System.Windows.Forms.Padding(3);
            GeneralTabPage.Size = new System.Drawing.Size(262, 353);
            GeneralTabPage.TabIndex = 0;
            GeneralTabPage.Text = "General";
            // 
            // GeneralSettingsPanel
            // 
            GeneralSettingsPanel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            GeneralSettingsPanel.Controls.Add(this.MinimizeInactiveClientsCheckBox);
            GeneralSettingsPanel.Controls.Add(this.EnableClientLayoutTrackingCheckBox);
            GeneralSettingsPanel.Controls.Add(this.HideActiveClientThumbnailCheckBox);
            GeneralSettingsPanel.Controls.Add(this.ShowThumbnailsAlwaysOnTopCheckBox);
            GeneralSettingsPanel.Controls.Add(this.HideThumbnailsOnLostFocusCheckBox);
            GeneralSettingsPanel.Controls.Add(this.EnablePerClientThumbnailsLayoutsCheckBox);
            GeneralSettingsPanel.Controls.Add(this.MinimizeToTrayCheckBox);
            GeneralSettingsPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            GeneralSettingsPanel.Location = new System.Drawing.Point(3, 3);
            GeneralSettingsPanel.Name = "GeneralSettingsPanel";
            GeneralSettingsPanel.Size = new System.Drawing.Size(256, 347);
            GeneralSettingsPanel.TabIndex = 18;
            // 
            // MinimizeInactiveClientsCheckBox
            // 
            this.MinimizeInactiveClientsCheckBox.AutoSize = true;
            this.MinimizeInactiveClientsCheckBox.Location = new System.Drawing.Point(8, 79);
            this.MinimizeInactiveClientsCheckBox.Name = "MinimizeInactiveClientsCheckBox";
            this.MinimizeInactiveClientsCheckBox.Size = new System.Drawing.Size(163, 17);
            this.MinimizeInactiveClientsCheckBox.TabIndex = 24;
            this.MinimizeInactiveClientsCheckBox.Text = "Minimize inactive EVE clients";
            this.MinimizeInactiveClientsCheckBox.UseVisualStyleBackColor = true;
            this.MinimizeInactiveClientsCheckBox.CheckedChanged += new System.EventHandler(this.OptionChanged_Handler);
            // 
            // EnableClientLayoutTrackingCheckBox
            // 
            this.EnableClientLayoutTrackingCheckBox.AutoSize = true;
            this.EnableClientLayoutTrackingCheckBox.Location = new System.Drawing.Point(8, 31);
            this.EnableClientLayoutTrackingCheckBox.Name = "EnableClientLayoutTrackingCheckBox";
            this.EnableClientLayoutTrackingCheckBox.Size = new System.Drawing.Size(127, 17);
            this.EnableClientLayoutTrackingCheckBox.TabIndex = 19;
            this.EnableClientLayoutTrackingCheckBox.Text = "Track client locations";
            this.EnableClientLayoutTrackingCheckBox.UseVisualStyleBackColor = true;
            this.EnableClientLayoutTrackingCheckBox.CheckedChanged += new System.EventHandler(this.OptionChanged_Handler);
            // 
            // HideActiveClientThumbnailCheckBox
            // 
            this.HideActiveClientThumbnailCheckBox.AutoSize = true;
            this.HideActiveClientThumbnailCheckBox.Checked = true;
            this.HideActiveClientThumbnailCheckBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.HideActiveClientThumbnailCheckBox.Location = new System.Drawing.Point(8, 55);
            this.HideActiveClientThumbnailCheckBox.Name = "HideActiveClientThumbnailCheckBox";
            this.HideActiveClientThumbnailCheckBox.Size = new System.Drawing.Size(184, 17);
            this.HideActiveClientThumbnailCheckBox.TabIndex = 20;
            this.HideActiveClientThumbnailCheckBox.Text = "Hide preview of active EVE client";
            this.HideActiveClientThumbnailCheckBox.UseVisualStyleBackColor = true;
            this.HideActiveClientThumbnailCheckBox.CheckedChanged += new System.EventHandler(this.OptionChanged_Handler);
            // 
            // ShowThumbnailsAlwaysOnTopCheckBox
            // 
            this.ShowThumbnailsAlwaysOnTopCheckBox.AutoSize = true;
            this.ShowThumbnailsAlwaysOnTopCheckBox.Checked = true;
            this.ShowThumbnailsAlwaysOnTopCheckBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.ShowThumbnailsAlwaysOnTopCheckBox.Location = new System.Drawing.Point(8, 103);
            this.ShowThumbnailsAlwaysOnTopCheckBox.Name = "ShowThumbnailsAlwaysOnTopCheckBox";
            this.ShowThumbnailsAlwaysOnTopCheckBox.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.ShowThumbnailsAlwaysOnTopCheckBox.Size = new System.Drawing.Size(137, 17);
            this.ShowThumbnailsAlwaysOnTopCheckBox.TabIndex = 21;
            this.ShowThumbnailsAlwaysOnTopCheckBox.Text = "Previews always on top";
            this.ShowThumbnailsAlwaysOnTopCheckBox.UseVisualStyleBackColor = true;
            this.ShowThumbnailsAlwaysOnTopCheckBox.CheckedChanged += new System.EventHandler(this.OptionChanged_Handler);
            // 
            // HideThumbnailsOnLostFocusCheckBox
            // 
            this.HideThumbnailsOnLostFocusCheckBox.AutoSize = true;
            this.HideThumbnailsOnLostFocusCheckBox.Checked = true;
            this.HideThumbnailsOnLostFocusCheckBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.HideThumbnailsOnLostFocusCheckBox.Location = new System.Drawing.Point(8, 127);
            this.HideThumbnailsOnLostFocusCheckBox.Name = "HideThumbnailsOnLostFocusCheckBox";
            this.HideThumbnailsOnLostFocusCheckBox.Size = new System.Drawing.Size(234, 17);
            this.HideThumbnailsOnLostFocusCheckBox.TabIndex = 22;
            this.HideThumbnailsOnLostFocusCheckBox.Text = "Hide previews when EVE client is not active";
            this.HideThumbnailsOnLostFocusCheckBox.UseVisualStyleBackColor = true;
            this.HideThumbnailsOnLostFocusCheckBox.CheckedChanged += new System.EventHandler(this.OptionChanged_Handler);
            // 
            // EnablePerClientThumbnailsLayoutsCheckBox
            // 
            this.EnablePerClientThumbnailsLayoutsCheckBox.AutoSize = true;
            this.EnablePerClientThumbnailsLayoutsCheckBox.Checked = true;
            this.EnablePerClientThumbnailsLayoutsCheckBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.EnablePerClientThumbnailsLayoutsCheckBox.Location = new System.Drawing.Point(8, 151);
            this.EnablePerClientThumbnailsLayoutsCheckBox.Name = "EnablePerClientThumbnailsLayoutsCheckBox";
            this.EnablePerClientThumbnailsLayoutsCheckBox.Size = new System.Drawing.Size(185, 17);
            this.EnablePerClientThumbnailsLayoutsCheckBox.TabIndex = 23;
            this.EnablePerClientThumbnailsLayoutsCheckBox.Text = "Unique layout for each EVE client";
            this.EnablePerClientThumbnailsLayoutsCheckBox.UseVisualStyleBackColor = true;
            this.EnablePerClientThumbnailsLayoutsCheckBox.CheckedChanged += new System.EventHandler(this.OptionChanged_Handler);
            // 
            // MinimizeToTrayCheckBox
            // 
            this.MinimizeToTrayCheckBox.AutoSize = true;
            this.MinimizeToTrayCheckBox.Location = new System.Drawing.Point(8, 7);
            this.MinimizeToTrayCheckBox.Name = "MinimizeToTrayCheckBox";
            this.MinimizeToTrayCheckBox.Size = new System.Drawing.Size(139, 17);
            this.MinimizeToTrayCheckBox.TabIndex = 18;
            this.MinimizeToTrayCheckBox.Text = "Minimize to System Tray";
            this.MinimizeToTrayCheckBox.UseVisualStyleBackColor = true;
            this.MinimizeToTrayCheckBox.CheckedChanged += new System.EventHandler(this.OptionChanged_Handler);
            // 
            // ThumbnailTabPage
            // 
            ThumbnailTabPage.BackColor = System.Drawing.SystemColors.Control;
            ThumbnailTabPage.Controls.Add(ThumbnailSettingsPanel);
            ThumbnailTabPage.Location = new System.Drawing.Point(124, 4);
            ThumbnailTabPage.Name = "ThumbnailTabPage";
            ThumbnailTabPage.Padding = new System.Windows.Forms.Padding(3);
            ThumbnailTabPage.Size = new System.Drawing.Size(262, 353);
            ThumbnailTabPage.TabIndex = 1;
            ThumbnailTabPage.Text = "Thumbnail";
            // 
            // ThumbnailSettingsPanel
            // 
            ThumbnailSettingsPanel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            ThumbnailSettingsPanel.Controls.Add(HeigthLabel);
            ThumbnailSettingsPanel.Controls.Add(WidthLabel);
            ThumbnailSettingsPanel.Controls.Add(this.ThumbnailsWidthNumericEdit);
            ThumbnailSettingsPanel.Controls.Add(this.ThumbnailsHeightNumericEdit);
            ThumbnailSettingsPanel.Controls.Add(this.ThumbnailOpacityTrackBar);
            ThumbnailSettingsPanel.Controls.Add(OpacityLabel);
            ThumbnailSettingsPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            ThumbnailSettingsPanel.Location = new System.Drawing.Point(3, 3);
            ThumbnailSettingsPanel.Name = "ThumbnailSettingsPanel";
            ThumbnailSettingsPanel.Size = new System.Drawing.Size(256, 347);
            ThumbnailSettingsPanel.TabIndex = 19;
            // 
            // HeigthLabel
            // 
            HeigthLabel.AutoSize = true;
            HeigthLabel.Location = new System.Drawing.Point(8, 57);
            HeigthLabel.Name = "HeigthLabel";
            HeigthLabel.Size = new System.Drawing.Size(90, 13);
            HeigthLabel.TabIndex = 24;
            HeigthLabel.Text = "Thumbnail Heigth";
            // 
            // WidthLabel
            // 
            WidthLabel.AutoSize = true;
            WidthLabel.Location = new System.Drawing.Point(8, 33);
            WidthLabel.Name = "WidthLabel";
            WidthLabel.Size = new System.Drawing.Size(87, 13);
            WidthLabel.TabIndex = 23;
            WidthLabel.Text = "Thumbnail Width";
            // 
            // ThumbnailsWidthNumericEdit
            // 
            this.ThumbnailsWidthNumericEdit.BackColor = System.Drawing.SystemColors.Window;
            this.ThumbnailsWidthNumericEdit.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.ThumbnailsWidthNumericEdit.CausesValidation = false;
            this.ThumbnailsWidthNumericEdit.Increment = new decimal(new int[] {
            10,
            0,
            0,
            0});
            this.ThumbnailsWidthNumericEdit.Location = new System.Drawing.Point(105, 31);
            this.ThumbnailsWidthNumericEdit.Maximum = new decimal(new int[] {
            999999,
            0,
            0,
            0});
            this.ThumbnailsWidthNumericEdit.Name = "ThumbnailsWidthNumericEdit";
            this.ThumbnailsWidthNumericEdit.Size = new System.Drawing.Size(48, 20);
            this.ThumbnailsWidthNumericEdit.TabIndex = 21;
            this.ThumbnailsWidthNumericEdit.Value = new decimal(new int[] {
            100,
            0,
            0,
            0});
            this.ThumbnailsWidthNumericEdit.ValueChanged += new System.EventHandler(this.ThumbnailSizeChanged_Handler);
            // 
            // ThumbnailsHeightNumericEdit
            // 
            this.ThumbnailsHeightNumericEdit.BackColor = System.Drawing.SystemColors.Window;
            this.ThumbnailsHeightNumericEdit.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.ThumbnailsHeightNumericEdit.CausesValidation = false;
            this.ThumbnailsHeightNumericEdit.Increment = new decimal(new int[] {
            10,
            0,
            0,
            0});
            this.ThumbnailsHeightNumericEdit.Location = new System.Drawing.Point(105, 55);
            this.ThumbnailsHeightNumericEdit.Maximum = new decimal(new int[] {
            99999999,
            0,
            0,
            0});
            this.ThumbnailsHeightNumericEdit.Name = "ThumbnailsHeightNumericEdit";
            this.ThumbnailsHeightNumericEdit.Size = new System.Drawing.Size(48, 20);
            this.ThumbnailsHeightNumericEdit.TabIndex = 22;
            this.ThumbnailsHeightNumericEdit.Value = new decimal(new int[] {
            70,
            0,
            0,
            0});
            this.ThumbnailsHeightNumericEdit.ValueChanged += new System.EventHandler(this.ThumbnailSizeChanged_Handler);
            // 
            // ThumbnailOpacityTrackBar
            // 
            this.ThumbnailOpacityTrackBar.AutoSize = false;
            this.ThumbnailOpacityTrackBar.LargeChange = 10;
            this.ThumbnailOpacityTrackBar.Location = new System.Drawing.Point(61, 6);
            this.ThumbnailOpacityTrackBar.Maximum = 100;
            this.ThumbnailOpacityTrackBar.Minimum = 20;
            this.ThumbnailOpacityTrackBar.Name = "ThumbnailOpacityTrackBar";
            this.ThumbnailOpacityTrackBar.Size = new System.Drawing.Size(191, 22);
            this.ThumbnailOpacityTrackBar.TabIndex = 20;
            this.ThumbnailOpacityTrackBar.TickFrequency = 10;
            this.ThumbnailOpacityTrackBar.Value = 20;
            this.ThumbnailOpacityTrackBar.ValueChanged += new System.EventHandler(this.OptionChanged_Handler);
            // 
            // OpacityLabel
            // 
            OpacityLabel.AutoSize = true;
            OpacityLabel.Location = new System.Drawing.Point(8, 9);
            OpacityLabel.Name = "OpacityLabel";
            OpacityLabel.Size = new System.Drawing.Size(43, 13);
            OpacityLabel.TabIndex = 19;
            OpacityLabel.Text = "Opacity";
            // 
            // ZoomTabPage
            // 
            this.ZoomTabPage.BackColor = System.Drawing.SystemColors.Control;
            this.ZoomTabPage.Controls.Add(ZoomSettingsPanel);
            this.ZoomTabPage.Location = new System.Drawing.Point(124, 4);
            this.ZoomTabPage.Name = "ZoomTabPage";
            this.ZoomTabPage.Size = new System.Drawing.Size(262, 353);
            this.ZoomTabPage.TabIndex = 2;
            this.ZoomTabPage.Text = "Zoom";
            // 
            // ZoomSettingsPanel
            // 
            ZoomSettingsPanel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            ZoomSettingsPanel.Controls.Add(ZoomFactorLabel);
            ZoomSettingsPanel.Controls.Add(this.ZoomAnchorPanel);
            ZoomSettingsPanel.Controls.Add(ZoomAnchorLabel);
            ZoomSettingsPanel.Controls.Add(this.EnableThumbnailZoomCheckBox);
            ZoomSettingsPanel.Controls.Add(this.ThumbnailZoomFactorNumericEdit);
            ZoomSettingsPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            ZoomSettingsPanel.Location = new System.Drawing.Point(0, 0);
            ZoomSettingsPanel.Name = "ZoomSettingsPanel";
            ZoomSettingsPanel.Size = new System.Drawing.Size(262, 353);
            ZoomSettingsPanel.TabIndex = 36;
            // 
            // ZoomFactorLabel
            // 
            ZoomFactorLabel.AutoSize = true;
            ZoomFactorLabel.Location = new System.Drawing.Point(8, 33);
            ZoomFactorLabel.Name = "ZoomFactorLabel";
            ZoomFactorLabel.Size = new System.Drawing.Size(67, 13);
            ZoomFactorLabel.TabIndex = 39;
            ZoomFactorLabel.Text = "Zoom Factor";
            // 
            // ZoomAnchorPanel
            // 
            this.ZoomAnchorPanel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.ZoomAnchorPanel.Controls.Add(this.ZoomAanchorNWRadioButton);
            this.ZoomAnchorPanel.Controls.Add(this.ZoomAanchorNRadioButton);
            this.ZoomAnchorPanel.Controls.Add(this.ZoomAanchorNERadioButton);
            this.ZoomAnchorPanel.Controls.Add(this.ZoomAanchorWRadioButton);
            this.ZoomAnchorPanel.Controls.Add(this.ZoomAanchorSERadioButton);
            this.ZoomAnchorPanel.Controls.Add(this.ZoomAanchorCRadioButton);
            this.ZoomAnchorPanel.Controls.Add(this.ZoomAanchorSRadioButton);
            this.ZoomAnchorPanel.Controls.Add(this.ZoomAanchorERadioButton);
            this.ZoomAnchorPanel.Controls.Add(this.ZoomAanchorSWRadioButton);
            this.ZoomAnchorPanel.Location = new System.Drawing.Point(81, 54);
            this.ZoomAnchorPanel.Name = "ZoomAnchorPanel";
            this.ZoomAnchorPanel.Size = new System.Drawing.Size(77, 73);
            this.ZoomAnchorPanel.TabIndex = 38;
            // 
            // ZoomAanchorNWRadioButton
            // 
            this.ZoomAanchorNWRadioButton.AutoSize = true;
            this.ZoomAanchorNWRadioButton.Location = new System.Drawing.Point(3, 3);
            this.ZoomAanchorNWRadioButton.Name = "ZoomAanchorNWRadioButton";
            this.ZoomAanchorNWRadioButton.Size = new System.Drawing.Size(14, 13);
            this.ZoomAanchorNWRadioButton.TabIndex = 0;
            this.ZoomAanchorNWRadioButton.TabStop = true;
            this.ZoomAanchorNWRadioButton.UseVisualStyleBackColor = true;
            this.ZoomAanchorNWRadioButton.CheckedChanged += new System.EventHandler(this.OptionChanged_Handler);
            // 
            // ZoomAanchorNRadioButton
            // 
            this.ZoomAanchorNRadioButton.AutoSize = true;
            this.ZoomAanchorNRadioButton.Location = new System.Drawing.Point(31, 3);
            this.ZoomAanchorNRadioButton.Name = "ZoomAanchorNRadioButton";
            this.ZoomAanchorNRadioButton.Size = new System.Drawing.Size(14, 13);
            this.ZoomAanchorNRadioButton.TabIndex = 1;
            this.ZoomAanchorNRadioButton.TabStop = true;
            this.ZoomAanchorNRadioButton.UseVisualStyleBackColor = true;
            this.ZoomAanchorNRadioButton.CheckedChanged += new System.EventHandler(this.OptionChanged_Handler);
            // 
            // ZoomAanchorNERadioButton
            // 
            this.ZoomAanchorNERadioButton.AutoSize = true;
            this.ZoomAanchorNERadioButton.Location = new System.Drawing.Point(59, 3);
            this.ZoomAanchorNERadioButton.Name = "ZoomAanchorNERadioButton";
            this.ZoomAanchorNERadioButton.Size = new System.Drawing.Size(14, 13);
            this.ZoomAanchorNERadioButton.TabIndex = 2;
            this.ZoomAanchorNERadioButton.TabStop = true;
            this.ZoomAanchorNERadioButton.UseVisualStyleBackColor = true;
            this.ZoomAanchorNERadioButton.CheckedChanged += new System.EventHandler(this.OptionChanged_Handler);
            // 
            // ZoomAanchorWRadioButton
            // 
            this.ZoomAanchorWRadioButton.AutoSize = true;
            this.ZoomAanchorWRadioButton.Location = new System.Drawing.Point(3, 29);
            this.ZoomAanchorWRadioButton.Name = "ZoomAanchorWRadioButton";
            this.ZoomAanchorWRadioButton.Size = new System.Drawing.Size(14, 13);
            this.ZoomAanchorWRadioButton.TabIndex = 3;
            this.ZoomAanchorWRadioButton.TabStop = true;
            this.ZoomAanchorWRadioButton.UseVisualStyleBackColor = true;
            this.ZoomAanchorWRadioButton.CheckedChanged += new System.EventHandler(this.OptionChanged_Handler);
            // 
            // ZoomAanchorSERadioButton
            // 
            this.ZoomAanchorSERadioButton.AutoSize = true;
            this.ZoomAanchorSERadioButton.Location = new System.Drawing.Point(59, 55);
            this.ZoomAanchorSERadioButton.Name = "ZoomAanchorSERadioButton";
            this.ZoomAanchorSERadioButton.Size = new System.Drawing.Size(14, 13);
            this.ZoomAanchorSERadioButton.TabIndex = 8;
            this.ZoomAanchorSERadioButton.TabStop = true;
            this.ZoomAanchorSERadioButton.UseVisualStyleBackColor = true;
            this.ZoomAanchorSERadioButton.CheckedChanged += new System.EventHandler(this.OptionChanged_Handler);
            // 
            // ZoomAanchorCRadioButton
            // 
            this.ZoomAanchorCRadioButton.AutoSize = true;
            this.ZoomAanchorCRadioButton.Location = new System.Drawing.Point(31, 29);
            this.ZoomAanchorCRadioButton.Name = "ZoomAanchorCRadioButton";
            this.ZoomAanchorCRadioButton.Size = new System.Drawing.Size(14, 13);
            this.ZoomAanchorCRadioButton.TabIndex = 4;
            this.ZoomAanchorCRadioButton.TabStop = true;
            this.ZoomAanchorCRadioButton.UseVisualStyleBackColor = true;
            this.ZoomAanchorCRadioButton.CheckedChanged += new System.EventHandler(this.OptionChanged_Handler);
            // 
            // ZoomAanchorSRadioButton
            // 
            this.ZoomAanchorSRadioButton.AutoSize = true;
            this.ZoomAanchorSRadioButton.Location = new System.Drawing.Point(31, 55);
            this.ZoomAanchorSRadioButton.Name = "ZoomAanchorSRadioButton";
            this.ZoomAanchorSRadioButton.Size = new System.Drawing.Size(14, 13);
            this.ZoomAanchorSRadioButton.TabIndex = 7;
            this.ZoomAanchorSRadioButton.TabStop = true;
            this.ZoomAanchorSRadioButton.UseVisualStyleBackColor = true;
            this.ZoomAanchorSRadioButton.CheckedChanged += new System.EventHandler(this.OptionChanged_Handler);
            // 
            // ZoomAanchorERadioButton
            // 
            this.ZoomAanchorERadioButton.AutoSize = true;
            this.ZoomAanchorERadioButton.Location = new System.Drawing.Point(59, 29);
            this.ZoomAanchorERadioButton.Name = "ZoomAanchorERadioButton";
            this.ZoomAanchorERadioButton.Size = new System.Drawing.Size(14, 13);
            this.ZoomAanchorERadioButton.TabIndex = 5;
            this.ZoomAanchorERadioButton.TabStop = true;
            this.ZoomAanchorERadioButton.UseVisualStyleBackColor = true;
            this.ZoomAanchorERadioButton.CheckedChanged += new System.EventHandler(this.OptionChanged_Handler);
            // 
            // ZoomAanchorSWRadioButton
            // 
            this.ZoomAanchorSWRadioButton.AutoSize = true;
            this.ZoomAanchorSWRadioButton.Location = new System.Drawing.Point(3, 55);
            this.ZoomAanchorSWRadioButton.Name = "ZoomAanchorSWRadioButton";
            this.ZoomAanchorSWRadioButton.Size = new System.Drawing.Size(14, 13);
            this.ZoomAanchorSWRadioButton.TabIndex = 6;
            this.ZoomAanchorSWRadioButton.TabStop = true;
            this.ZoomAanchorSWRadioButton.UseVisualStyleBackColor = true;
            this.ZoomAanchorSWRadioButton.CheckedChanged += new System.EventHandler(this.OptionChanged_Handler);
            // 
            // ZoomAnchorLabel
            // 
            ZoomAnchorLabel.AutoSize = true;
            ZoomAnchorLabel.Location = new System.Drawing.Point(8, 57);
            ZoomAnchorLabel.Name = "ZoomAnchorLabel";
            ZoomAnchorLabel.Size = new System.Drawing.Size(41, 13);
            ZoomAnchorLabel.TabIndex = 40;
            ZoomAnchorLabel.Text = "Anchor";
            // 
            // EnableThumbnailZoomCheckBox
            // 
            this.EnableThumbnailZoomCheckBox.AutoSize = true;
            this.EnableThumbnailZoomCheckBox.Checked = true;
            this.EnableThumbnailZoomCheckBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.EnableThumbnailZoomCheckBox.Location = new System.Drawing.Point(8, 7);
            this.EnableThumbnailZoomCheckBox.Name = "EnableThumbnailZoomCheckBox";
            this.EnableThumbnailZoomCheckBox.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.EnableThumbnailZoomCheckBox.Size = new System.Drawing.Size(98, 17);
            this.EnableThumbnailZoomCheckBox.TabIndex = 36;
            this.EnableThumbnailZoomCheckBox.Text = "Zoom on hover";
            this.EnableThumbnailZoomCheckBox.UseVisualStyleBackColor = true;
            this.EnableThumbnailZoomCheckBox.CheckedChanged += new System.EventHandler(this.OptionChanged_Handler);
            // 
            // ThumbnailZoomFactorNumericEdit
            // 
            this.ThumbnailZoomFactorNumericEdit.BackColor = System.Drawing.SystemColors.Window;
            this.ThumbnailZoomFactorNumericEdit.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.ThumbnailZoomFactorNumericEdit.Location = new System.Drawing.Point(81, 31);
            this.ThumbnailZoomFactorNumericEdit.Maximum = new decimal(new int[] {
            10,
            0,
            0,
            0});
            this.ThumbnailZoomFactorNumericEdit.Minimum = new decimal(new int[] {
            2,
            0,
            0,
            0});
            this.ThumbnailZoomFactorNumericEdit.Name = "ThumbnailZoomFactorNumericEdit";
            this.ThumbnailZoomFactorNumericEdit.Size = new System.Drawing.Size(38, 20);
            this.ThumbnailZoomFactorNumericEdit.TabIndex = 37;
            this.ThumbnailZoomFactorNumericEdit.Value = new decimal(new int[] {
            2,
            0,
            0,
            0});
            this.ThumbnailZoomFactorNumericEdit.ValueChanged += new System.EventHandler(this.OptionChanged_Handler);
            // 
            // OverlayTabPage
            // 
            OverlayTabPage.BackColor = System.Drawing.SystemColors.Control;
            OverlayTabPage.Controls.Add(OverlaySettingsPanel);
            OverlayTabPage.Location = new System.Drawing.Point(124, 4);
            OverlayTabPage.Name = "OverlayTabPage";
            OverlayTabPage.Size = new System.Drawing.Size(262, 353);
            OverlayTabPage.TabIndex = 3;
            OverlayTabPage.Text = "Overlay";
            // 
            // OverlaySettingsPanel
            // 
            OverlaySettingsPanel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            OverlaySettingsPanel.Controls.Add(this.groupBoxOverlayFont);
            OverlaySettingsPanel.Controls.Add(this.HighlightColorLabel);
            OverlaySettingsPanel.Controls.Add(this.ActiveClientHighlightColorButton);
            OverlaySettingsPanel.Controls.Add(this.EnableActiveClientHighlightCheckBox);
            OverlaySettingsPanel.Controls.Add(this.ShowThumbnailOverlaysCheckBox);
            OverlaySettingsPanel.Controls.Add(this.ShowThumbnailFramesCheckBox);
            OverlaySettingsPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            OverlaySettingsPanel.Location = new System.Drawing.Point(0, 0);
            OverlaySettingsPanel.Name = "OverlaySettingsPanel";
            OverlaySettingsPanel.Size = new System.Drawing.Size(262, 353);
            OverlaySettingsPanel.TabIndex = 25;
            // 
            // groupBoxOverlayFont
            // 
            this.groupBoxOverlayFont.Controls.Add(this.lblTitleOffsetTop);
            this.groupBoxOverlayFont.Controls.Add(this.txtTitleOffsetTop);
            this.groupBoxOverlayFont.Controls.Add(this.lblTitleOffsetLeft);
            this.groupBoxOverlayFont.Controls.Add(this.txtTitleOffsetLeft);
            this.groupBoxOverlayFont.Controls.Add(this.lblTitleBorderWidth);
            this.groupBoxOverlayFont.Controls.Add(this.txtFontOutlineWidth);
            this.groupBoxOverlayFont.Controls.Add(this.btnFontOutlineColor);
            this.groupBoxOverlayFont.Controls.Add(this.btnSetOverlayFontColor);
            this.groupBoxOverlayFont.Controls.Add(this.lblDisplaySampleFont);
            this.groupBoxOverlayFont.Controls.Add(this.btnSetOverlayFont);
            this.groupBoxOverlayFont.Location = new System.Drawing.Point(8, 112);
            this.groupBoxOverlayFont.Name = "groupBoxOverlayFont";
            this.groupBoxOverlayFont.Size = new System.Drawing.Size(245, 132);
            this.groupBoxOverlayFont.TabIndex = 33;
            this.groupBoxOverlayFont.TabStop = false;
            this.groupBoxOverlayFont.Text = "Title Font";
            // 
            // lblTitleOffsetTop
            // 
            this.lblTitleOffsetTop.AutoSize = true;
            this.lblTitleOffsetTop.Location = new System.Drawing.Point(105, 51);
            this.lblTitleOffsetTop.Name = "lblTitleOffsetTop";
            this.lblTitleOffsetTop.Size = new System.Drawing.Size(26, 13);
            this.lblTitleOffsetTop.TabIndex = 39;
            this.lblTitleOffsetTop.Text = "Top";
            // 
            // txtTitleOffsetTop
            // 
            this.txtTitleOffsetTop.Location = new System.Drawing.Point(137, 48);
            this.txtTitleOffsetTop.Name = "txtTitleOffsetTop";
            this.txtTitleOffsetTop.Size = new System.Drawing.Size(26, 20);
            this.txtTitleOffsetTop.TabIndex = 38;
            this.txtTitleOffsetTop.Text = "1";
            this.txtTitleOffsetTop.Enter += new System.EventHandler(this.txtTitleOffsetTop_Enter);
            this.txtTitleOffsetTop.Leave += new System.EventHandler(this.txtTitleOffsetTop_Leave);
            // 
            // lblTitleOffsetLeft
            // 
            this.lblTitleOffsetLeft.AutoSize = true;
            this.lblTitleOffsetLeft.Location = new System.Drawing.Point(11, 51);
            this.lblTitleOffsetLeft.Name = "lblTitleOffsetLeft";
            this.lblTitleOffsetLeft.Size = new System.Drawing.Size(56, 13);
            this.lblTitleOffsetLeft.TabIndex = 37;
            this.lblTitleOffsetLeft.Text = "Offset Left";
            // 
            // txtTitleOffsetLeft
            // 
            this.txtTitleOffsetLeft.Location = new System.Drawing.Point(73, 48);
            this.txtTitleOffsetLeft.Name = "txtTitleOffsetLeft";
            this.txtTitleOffsetLeft.Size = new System.Drawing.Size(26, 20);
            this.txtTitleOffsetLeft.TabIndex = 36;
            this.txtTitleOffsetLeft.Text = "1";
            this.txtTitleOffsetLeft.Enter += new System.EventHandler(this.txtTitleOffsetLeft_Enter);
            this.txtTitleOffsetLeft.Leave += new System.EventHandler(this.txtTitleOffsetLeft_Leave);
            // 
            // lblTitleBorderWidth
            // 
            this.lblTitleBorderWidth.AutoSize = true;
            this.lblTitleBorderWidth.Location = new System.Drawing.Point(169, 51);
            this.lblTitleBorderWidth.Name = "lblTitleBorderWidth";
            this.lblTitleBorderWidth.Size = new System.Drawing.Size(40, 13);
            this.lblTitleBorderWidth.TabIndex = 35;
            this.lblTitleBorderWidth.Text = "Outline";
            // 
            // txtFontOutlineWidth
            // 
            this.txtFontOutlineWidth.Location = new System.Drawing.Point(215, 48);
            this.txtFontOutlineWidth.Name = "txtFontOutlineWidth";
            this.txtFontOutlineWidth.Size = new System.Drawing.Size(26, 20);
            this.txtFontOutlineWidth.TabIndex = 34;
            this.txtFontOutlineWidth.Text = "0";
            this.txtFontOutlineWidth.Enter += new System.EventHandler(this.txtFontOutlineWidth_Enter);
            this.txtFontOutlineWidth.Leave += new System.EventHandler(this.txtFontOutlineWidth_Leave);
            // 
            // btnFontOutlineColor
            // 
            this.btnFontOutlineColor.Location = new System.Drawing.Point(163, 19);
            this.btnFontOutlineColor.Name = "btnFontOutlineColor";
            this.btnFontOutlineColor.Size = new System.Drawing.Size(76, 23);
            this.btnFontOutlineColor.TabIndex = 33;
            this.btnFontOutlineColor.Text = "Outline Color";
            this.btnFontOutlineColor.UseVisualStyleBackColor = true;
            this.btnFontOutlineColor.Click += new System.EventHandler(this.btnFontOutlineColor_Click);
            // 
            // btnSetOverlayFontColor
            // 
            this.btnSetOverlayFontColor.Location = new System.Drawing.Point(84, 19);
            this.btnSetOverlayFontColor.Name = "btnSetOverlayFontColor";
            this.btnSetOverlayFontColor.Size = new System.Drawing.Size(59, 23);
            this.btnSetOverlayFontColor.TabIndex = 31;
            this.btnSetOverlayFontColor.Text = "Forecolor";
            this.btnSetOverlayFontColor.UseVisualStyleBackColor = true;
            this.btnSetOverlayFontColor.Click += new System.EventHandler(this.btnSetOverlayFontColor_Click);
            // 
            // lblDisplaySampleFont
            // 
            this.lblDisplaySampleFont.AutoSize = true;
            this.lblDisplaySampleFont.BackColor = System.Drawing.SystemColors.Control;
            this.lblDisplaySampleFont.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(128)))), ((int)(((byte)(0)))));
            this.lblDisplaySampleFont.Location = new System.Drawing.Point(6, 76);
            this.lblDisplaySampleFont.Name = "lblDisplaySampleFont";
            this.lblDisplaySampleFont.OutlineColor = System.Drawing.Color.White;
            this.lblDisplaySampleFont.OutlineWidth = 1F;
            this.lblDisplaySampleFont.Size = new System.Drawing.Size(45, 13);
            this.lblDisplaySampleFont.TabIndex = 32;
            this.lblDisplaySampleFont.Text = "Sample.";
            // 
            // btnSetOverlayFont
            // 
            this.btnSetOverlayFont.Location = new System.Drawing.Point(6, 19);
            this.btnSetOverlayFont.Name = "btnSetOverlayFont";
            this.btnSetOverlayFont.Size = new System.Drawing.Size(56, 23);
            this.btnSetOverlayFont.TabIndex = 30;
            this.btnSetOverlayFont.Text = "Set Font";
            this.btnSetOverlayFont.UseVisualStyleBackColor = true;
            this.btnSetOverlayFont.Click += new System.EventHandler(this.btnSetOverlayFont_Click);
            // 
            // HighlightColorLabel
            // 
            this.HighlightColorLabel.AutoSize = true;
            this.HighlightColorLabel.Location = new System.Drawing.Point(5, 78);
            this.HighlightColorLabel.Name = "HighlightColorLabel";
            this.HighlightColorLabel.Size = new System.Drawing.Size(31, 13);
            this.HighlightColorLabel.TabIndex = 29;
            this.HighlightColorLabel.Text = "Color";
            // 
            // ActiveClientHighlightColorButton
            // 
            this.ActiveClientHighlightColorButton.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.ActiveClientHighlightColorButton.Location = new System.Drawing.Point(42, 77);
            this.ActiveClientHighlightColorButton.Name = "ActiveClientHighlightColorButton";
            this.ActiveClientHighlightColorButton.Size = new System.Drawing.Size(93, 17);
            this.ActiveClientHighlightColorButton.TabIndex = 28;
            this.ActiveClientHighlightColorButton.Click += new System.EventHandler(this.ActiveClientHighlightColorButton_Click);
            // 
            // EnableActiveClientHighlightCheckBox
            // 
            this.EnableActiveClientHighlightCheckBox.AutoSize = true;
            this.EnableActiveClientHighlightCheckBox.Checked = true;
            this.EnableActiveClientHighlightCheckBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.EnableActiveClientHighlightCheckBox.Location = new System.Drawing.Point(8, 55);
            this.EnableActiveClientHighlightCheckBox.Name = "EnableActiveClientHighlightCheckBox";
            this.EnableActiveClientHighlightCheckBox.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.EnableActiveClientHighlightCheckBox.Size = new System.Drawing.Size(127, 17);
            this.EnableActiveClientHighlightCheckBox.TabIndex = 27;
            this.EnableActiveClientHighlightCheckBox.Text = "Highlight active client";
            this.EnableActiveClientHighlightCheckBox.UseVisualStyleBackColor = true;
            this.EnableActiveClientHighlightCheckBox.CheckedChanged += new System.EventHandler(this.OptionChanged_Handler);
            // 
            // ShowThumbnailOverlaysCheckBox
            // 
            this.ShowThumbnailOverlaysCheckBox.AutoSize = true;
            this.ShowThumbnailOverlaysCheckBox.Checked = true;
            this.ShowThumbnailOverlaysCheckBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.ShowThumbnailOverlaysCheckBox.Location = new System.Drawing.Point(8, 7);
            this.ShowThumbnailOverlaysCheckBox.Name = "ShowThumbnailOverlaysCheckBox";
            this.ShowThumbnailOverlaysCheckBox.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.ShowThumbnailOverlaysCheckBox.Size = new System.Drawing.Size(90, 17);
            this.ShowThumbnailOverlaysCheckBox.TabIndex = 25;
            this.ShowThumbnailOverlaysCheckBox.Text = "Show overlay";
            this.ShowThumbnailOverlaysCheckBox.UseVisualStyleBackColor = true;
            this.ShowThumbnailOverlaysCheckBox.CheckedChanged += new System.EventHandler(this.OptionChanged_Handler);
            // 
            // ShowThumbnailFramesCheckBox
            // 
            this.ShowThumbnailFramesCheckBox.AutoSize = true;
            this.ShowThumbnailFramesCheckBox.Checked = true;
            this.ShowThumbnailFramesCheckBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.ShowThumbnailFramesCheckBox.Location = new System.Drawing.Point(8, 31);
            this.ShowThumbnailFramesCheckBox.Name = "ShowThumbnailFramesCheckBox";
            this.ShowThumbnailFramesCheckBox.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.ShowThumbnailFramesCheckBox.Size = new System.Drawing.Size(87, 17);
            this.ShowThumbnailFramesCheckBox.TabIndex = 26;
            this.ShowThumbnailFramesCheckBox.Text = "Show frames";
            this.ShowThumbnailFramesCheckBox.UseVisualStyleBackColor = true;
            this.ShowThumbnailFramesCheckBox.CheckedChanged += new System.EventHandler(this.OptionChanged_Handler);
            // 
            // ClientsTabPage
            // 
            ClientsTabPage.BackColor = System.Drawing.SystemColors.Control;
            ClientsTabPage.Controls.Add(ClientsPanel);
            ClientsTabPage.Location = new System.Drawing.Point(124, 4);
            ClientsTabPage.Name = "ClientsTabPage";
            ClientsTabPage.Size = new System.Drawing.Size(262, 353);
            ClientsTabPage.TabIndex = 4;
            ClientsTabPage.Text = "Active Clients";
            // 
            // ClientsPanel
            // 
            ClientsPanel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            ClientsPanel.Controls.Add(this.activeClientsSplitContainer);
            ClientsPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            ClientsPanel.Location = new System.Drawing.Point(0, 0);
            ClientsPanel.Name = "ClientsPanel";
            ClientsPanel.Size = new System.Drawing.Size(262, 353);
            ClientsPanel.TabIndex = 32;
            // 
            // activeClientsSplitContainer
            // 
            this.activeClientsSplitContainer.Dock = System.Windows.Forms.DockStyle.Fill;
            this.activeClientsSplitContainer.Location = new System.Drawing.Point(0, 0);
            this.activeClientsSplitContainer.Name = "activeClientsSplitContainer";
            this.activeClientsSplitContainer.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // activeClientsSplitContainer.Panel1
            // 
            this.activeClientsSplitContainer.Panel1.Controls.Add(this.txtToggleHideAllActiveHotkey);
            this.activeClientsSplitContainer.Panel1.Controls.Add(this.lblToggleHideAllActive);
            this.activeClientsSplitContainer.Panel1.Controls.Add(ThumbnailsListLabel);
            // 
            // activeClientsSplitContainer.Panel2
            // 
            this.activeClientsSplitContainer.Panel2.Controls.Add(this.ThumbnailsList);
            this.activeClientsSplitContainer.Size = new System.Drawing.Size(260, 351);
            this.activeClientsSplitContainer.TabIndex = 35;
            // 
            // ThumbnailsListLabel
            // 
            ThumbnailsListLabel.AutoSize = true;
            ThumbnailsListLabel.Location = new System.Drawing.Point(6, 7);
            ThumbnailsListLabel.Name = "ThumbnailsListLabel";
            ThumbnailsListLabel.Size = new System.Drawing.Size(162, 13);
            ThumbnailsListLabel.TabIndex = 33;
            ThumbnailsListLabel.Text = "Thumbnails (check to force hide)";
            // 
            // ThumbnailsList
            // 
            this.ThumbnailsList.BackColor = System.Drawing.SystemColors.Window;
            this.ThumbnailsList.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.ThumbnailsList.CheckOnClick = true;
            this.ThumbnailsList.Dock = System.Windows.Forms.DockStyle.Top;
            this.ThumbnailsList.FormattingEnabled = true;
            this.ThumbnailsList.IntegralHeight = false;
            this.ThumbnailsList.Location = new System.Drawing.Point(0, 0);
            this.ThumbnailsList.Name = "ThumbnailsList";
            this.ThumbnailsList.Size = new System.Drawing.Size(260, 306);
            this.ThumbnailsList.TabIndex = 34;
            this.ThumbnailsList.ItemCheck += new System.Windows.Forms.ItemCheckEventHandler(this.ThumbnailsList_ItemCheck_Handler);
            // 
            // CycleGroupTabPage
            // 
            CycleGroupTabPage.Controls.Add(this.CycleGroupPanel);
            CycleGroupTabPage.Location = new System.Drawing.Point(124, 4);
            CycleGroupTabPage.Name = "CycleGroupTabPage";
            CycleGroupTabPage.Size = new System.Drawing.Size(262, 353);
            CycleGroupTabPage.TabIndex = 6;
            CycleGroupTabPage.Text = "Cycle Groups";
            // 
            // CycleGroupPanel
            // 
            this.CycleGroupPanel.Controls.Add(this.removeGroupButton);
            this.CycleGroupPanel.Controls.Add(this.cycleGroupMoveClientOrderUpButton);
            this.CycleGroupPanel.Controls.Add(this.removeClientToCycleGroupButton);
            this.CycleGroupPanel.Controls.Add(this.addNewGroupButton);
            this.CycleGroupPanel.Controls.Add(this.cycleGroupClientOrderLabel);
            this.CycleGroupPanel.Controls.Add(this.cycleGroupClientOrderList);
            this.CycleGroupPanel.Controls.Add(this.cycleGroupBackwardHotkey2Text);
            this.CycleGroupPanel.Controls.Add(this.cycleGroupBackwardHotkey1Text);
            this.CycleGroupPanel.Controls.Add(this.cycleGroupBackHotkeyLabel);
            this.CycleGroupPanel.Controls.Add(this.cycleGroupForwardHotkey2Text);
            this.CycleGroupPanel.Controls.Add(this.cycleGroupForwardHotkey1Text);
            this.CycleGroupPanel.Controls.Add(this.cycleGroupForwardHotkeyLabel);
            this.CycleGroupPanel.Controls.Add(this.cycleGroupDescriptionText);
            this.CycleGroupPanel.Controls.Add(this.cycleGroupDescriptionLabel);
            this.CycleGroupPanel.Controls.Add(this.CycleGroupLabel);
            this.CycleGroupPanel.Controls.Add(this.selectCycleGroupComboBox);
            this.CycleGroupPanel.Controls.Add(this.addClientToCycleGroupButton);
            this.CycleGroupPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.CycleGroupPanel.Location = new System.Drawing.Point(0, 0);
            this.CycleGroupPanel.Name = "CycleGroupPanel";
            this.CycleGroupPanel.Size = new System.Drawing.Size(262, 353);
            this.CycleGroupPanel.TabIndex = 0;
            // 
            // removeGroupButton
            // 
            this.removeGroupButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.removeGroupButton.Location = new System.Drawing.Point(200, 22);
            this.removeGroupButton.Name = "removeGroupButton";
            this.removeGroupButton.Size = new System.Drawing.Size(24, 23);
            this.removeGroupButton.TabIndex = 16;
            this.removeGroupButton.Text = "-";
            this.removeGroupButton.UseVisualStyleBackColor = true;
            this.removeGroupButton.Click += new System.EventHandler(this.removeGroupButton_Click);
            // 
            // cycleGroupMoveClientOrderUpButton
            // 
            this.cycleGroupMoveClientOrderUpButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.cycleGroupMoveClientOrderUpButton.Location = new System.Drawing.Point(165, 129);
            this.cycleGroupMoveClientOrderUpButton.Name = "cycleGroupMoveClientOrderUpButton";
            this.cycleGroupMoveClientOrderUpButton.Size = new System.Drawing.Size(33, 23);
            this.cycleGroupMoveClientOrderUpButton.TabIndex = 15;
            this.cycleGroupMoveClientOrderUpButton.Text = "Up";
            this.cycleGroupMoveClientOrderUpButton.UseVisualStyleBackColor = true;
            this.cycleGroupMoveClientOrderUpButton.Click += new System.EventHandler(this.cycleGroupMoveClientOrderUpButton_Click);
            // 
            // removeClientToCycleGroupButton
            // 
            this.removeClientToCycleGroupButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.removeClientToCycleGroupButton.Location = new System.Drawing.Point(200, 129);
            this.removeClientToCycleGroupButton.Name = "removeClientToCycleGroupButton";
            this.removeClientToCycleGroupButton.Size = new System.Drawing.Size(22, 23);
            this.removeClientToCycleGroupButton.TabIndex = 14;
            this.removeClientToCycleGroupButton.Text = "-";
            this.removeClientToCycleGroupButton.UseVisualStyleBackColor = true;
            this.removeClientToCycleGroupButton.Click += new System.EventHandler(this.removeClientToCycleGroupButton_Click);
            // 
            // addNewGroupButton
            // 
            this.addNewGroupButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.addNewGroupButton.Location = new System.Drawing.Point(223, 22);
            this.addNewGroupButton.Name = "addNewGroupButton";
            this.addNewGroupButton.Size = new System.Drawing.Size(24, 23);
            this.addNewGroupButton.TabIndex = 13;
            this.addNewGroupButton.Text = "+";
            this.addNewGroupButton.UseVisualStyleBackColor = true;
            this.addNewGroupButton.Click += new System.EventHandler(this.addNewGroupButton_Click);
            // 
            // cycleGroupClientOrderLabel
            // 
            this.cycleGroupClientOrderLabel.AutoSize = true;
            this.cycleGroupClientOrderLabel.Location = new System.Drawing.Point(3, 134);
            this.cycleGroupClientOrderLabel.Name = "cycleGroupClientOrderLabel";
            this.cycleGroupClientOrderLabel.Size = new System.Drawing.Size(91, 13);
            this.cycleGroupClientOrderLabel.TabIndex = 12;
            this.cycleGroupClientOrderLabel.Text = "Clients and Order:";
            // 
            // cycleGroupClientOrderList
            // 
            this.cycleGroupClientOrderList.FormattingEnabled = true;
            this.cycleGroupClientOrderList.Location = new System.Drawing.Point(6, 153);
            this.cycleGroupClientOrderList.Name = "cycleGroupClientOrderList";
            this.cycleGroupClientOrderList.Size = new System.Drawing.Size(239, 186);
            this.cycleGroupClientOrderList.TabIndex = 11;
            // 
            // cycleGroupBackwardHotkey2Text
            // 
            this.cycleGroupBackwardHotkey2Text.BackColor = System.Drawing.SystemColors.Control;
            this.cycleGroupBackwardHotkey2Text.Location = new System.Drawing.Point(165, 103);
            this.cycleGroupBackwardHotkey2Text.Name = "cycleGroupBackwardHotkey2Text";
            this.cycleGroupBackwardHotkey2Text.ReadOnly = true;
            this.cycleGroupBackwardHotkey2Text.Size = new System.Drawing.Size(80, 20);
            this.cycleGroupBackwardHotkey2Text.TabIndex = 10;
            this.cycleGroupBackwardHotkey2Text.DoubleClick += new System.EventHandler(this.cycleGroupBackwardHotkey2Text_DoubleClick);
            // 
            // cycleGroupBackwardHotkey1Text
            // 
            this.cycleGroupBackwardHotkey1Text.BackColor = System.Drawing.SystemColors.Control;
            this.cycleGroupBackwardHotkey1Text.Location = new System.Drawing.Point(82, 103);
            this.cycleGroupBackwardHotkey1Text.Name = "cycleGroupBackwardHotkey1Text";
            this.cycleGroupBackwardHotkey1Text.ReadOnly = true;
            this.cycleGroupBackwardHotkey1Text.Size = new System.Drawing.Size(80, 20);
            this.cycleGroupBackwardHotkey1Text.TabIndex = 9;
            this.cycleGroupBackwardHotkey1Text.DoubleClick += new System.EventHandler(this.cycleGroupBackwardHotkey1Text_DoubleClick);
            // 
            // cycleGroupBackHotkeyLabel
            // 
            this.cycleGroupBackHotkeyLabel.AutoSize = true;
            this.cycleGroupBackHotkeyLabel.Location = new System.Drawing.Point(3, 106);
            this.cycleGroupBackHotkeyLabel.Name = "cycleGroupBackHotkeyLabel";
            this.cycleGroupBackHotkeyLabel.Size = new System.Drawing.Size(79, 13);
            this.cycleGroupBackHotkeyLabel.TabIndex = 8;
            this.cycleGroupBackHotkeyLabel.Text = "Backward Key:";
            // 
            // cycleGroupForwardHotkey2Text
            // 
            this.cycleGroupForwardHotkey2Text.BackColor = System.Drawing.SystemColors.Control;
            this.cycleGroupForwardHotkey2Text.Location = new System.Drawing.Point(165, 77);
            this.cycleGroupForwardHotkey2Text.Name = "cycleGroupForwardHotkey2Text";
            this.cycleGroupForwardHotkey2Text.ReadOnly = true;
            this.cycleGroupForwardHotkey2Text.Size = new System.Drawing.Size(80, 20);
            this.cycleGroupForwardHotkey2Text.TabIndex = 7;
            this.cycleGroupForwardHotkey2Text.DoubleClick += new System.EventHandler(this.cycleGroupForwardHotkey2Text_DoubleClick);
            // 
            // cycleGroupForwardHotkey1Text
            // 
            this.cycleGroupForwardHotkey1Text.BackColor = System.Drawing.SystemColors.Control;
            this.cycleGroupForwardHotkey1Text.Location = new System.Drawing.Point(82, 77);
            this.cycleGroupForwardHotkey1Text.Name = "cycleGroupForwardHotkey1Text";
            this.cycleGroupForwardHotkey1Text.ReadOnly = true;
            this.cycleGroupForwardHotkey1Text.Size = new System.Drawing.Size(80, 20);
            this.cycleGroupForwardHotkey1Text.TabIndex = 6;
            this.cycleGroupForwardHotkey1Text.DoubleClick += new System.EventHandler(this.cycleGroupForwardHotkey1Text_DoubleClick);
            // 
            // cycleGroupForwardHotkeyLabel
            // 
            this.cycleGroupForwardHotkeyLabel.AutoSize = true;
            this.cycleGroupForwardHotkeyLabel.Location = new System.Drawing.Point(3, 80);
            this.cycleGroupForwardHotkeyLabel.Name = "cycleGroupForwardHotkeyLabel";
            this.cycleGroupForwardHotkeyLabel.Size = new System.Drawing.Size(69, 13);
            this.cycleGroupForwardHotkeyLabel.TabIndex = 5;
            this.cycleGroupForwardHotkeyLabel.Text = "Forward Key:";
            // 
            // cycleGroupDescriptionText
            // 
            this.cycleGroupDescriptionText.Location = new System.Drawing.Point(82, 51);
            this.cycleGroupDescriptionText.Name = "cycleGroupDescriptionText";
            this.cycleGroupDescriptionText.Size = new System.Drawing.Size(163, 20);
            this.cycleGroupDescriptionText.TabIndex = 4;
            this.cycleGroupDescriptionText.Leave += new System.EventHandler(this.cycleGroupDescriptionText_Leave);
            // 
            // cycleGroupDescriptionLabel
            // 
            this.cycleGroupDescriptionLabel.AutoSize = true;
            this.cycleGroupDescriptionLabel.Location = new System.Drawing.Point(3, 54);
            this.cycleGroupDescriptionLabel.Name = "cycleGroupDescriptionLabel";
            this.cycleGroupDescriptionLabel.Size = new System.Drawing.Size(63, 13);
            this.cycleGroupDescriptionLabel.TabIndex = 3;
            this.cycleGroupDescriptionLabel.Text = "Description:";
            // 
            // CycleGroupLabel
            // 
            this.CycleGroupLabel.AutoSize = true;
            this.CycleGroupLabel.Location = new System.Drawing.Point(3, 7);
            this.CycleGroupLabel.Name = "CycleGroupLabel";
            this.CycleGroupLabel.Size = new System.Drawing.Size(101, 13);
            this.CycleGroupLabel.TabIndex = 2;
            this.CycleGroupLabel.Text = "Select Cycle Group:";
            // 
            // selectCycleGroupComboBox
            // 
            this.selectCycleGroupComboBox.FormattingEnabled = true;
            this.selectCycleGroupComboBox.Location = new System.Drawing.Point(6, 23);
            this.selectCycleGroupComboBox.Name = "selectCycleGroupComboBox";
            this.selectCycleGroupComboBox.Size = new System.Drawing.Size(192, 21);
            this.selectCycleGroupComboBox.TabIndex = 1;
            this.selectCycleGroupComboBox.SelectedValueChanged += new System.EventHandler(this.selectCycleGroupComboBox_SelectedValueChanged);
            // 
            // addClientToCycleGroupButton
            // 
            this.addClientToCycleGroupButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.addClientToCycleGroupButton.Location = new System.Drawing.Point(223, 129);
            this.addClientToCycleGroupButton.Name = "addClientToCycleGroupButton";
            this.addClientToCycleGroupButton.Size = new System.Drawing.Size(22, 23);
            this.addClientToCycleGroupButton.TabIndex = 0;
            this.addClientToCycleGroupButton.Text = "+";
            this.addClientToCycleGroupButton.UseVisualStyleBackColor = true;
            this.addClientToCycleGroupButton.Click += new System.EventHandler(this.addClientToCycleGroupButton_Click);
            // 
            // FpsLimiterTabPage
            // 
            this.FpsLimiterTabPage.Controls.Add(this.fpsMainLayoutPanel);
            this.FpsLimiterTabPage.Location = new System.Drawing.Point(124, 4);
            this.FpsLimiterTabPage.Name = "FpsLimiterTabPage";
            this.FpsLimiterTabPage.Size = new System.Drawing.Size(262, 353);
            this.FpsLimiterTabPage.TabIndex = 7;
            this.FpsLimiterTabPage.Text = "Premium";
            this.FpsLimiterTabPage.UseVisualStyleBackColor = true;
            // 
            // fpsMainLayoutPanel
            // 
            this.fpsMainLayoutPanel.ColumnCount = 1;
            this.fpsMainLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 262F));
            this.fpsMainLayoutPanel.Controls.Add(this.fpsTopPanel, 0, 0);
            this.fpsMainLayoutPanel.Controls.Add(this.fpsBottomPanel, 0, 1);
            this.fpsMainLayoutPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.fpsMainLayoutPanel.Location = new System.Drawing.Point(0, 0);
            this.fpsMainLayoutPanel.Name = "fpsMainLayoutPanel";
            this.fpsMainLayoutPanel.RowCount = 2;
            this.fpsMainLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 109F));
            this.fpsMainLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.fpsMainLayoutPanel.Size = new System.Drawing.Size(262, 353);
            this.fpsMainLayoutPanel.TabIndex = 0;
            // 
            // fpsTopPanel
            // 
            this.fpsTopPanel.Controls.Add(label1);
            this.fpsTopPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.fpsTopPanel.Location = new System.Drawing.Point(3, 3);
            this.fpsTopPanel.Name = "fpsTopPanel";
            this.fpsTopPanel.Size = new System.Drawing.Size(256, 103);
            this.fpsTopPanel.TabIndex = 0;
            // 
            // label1
            // 
            label1.BackColor = System.Drawing.Color.Transparent;
            label1.Location = new System.Drawing.Point(-2, -2);
            label1.Name = "label1";
            label1.Padding = new System.Windows.Forms.Padding(8, 3, 8, 3);
            label1.Size = new System.Drawing.Size(261, 145);
            label1.TabIndex = 6;
            label1.Text = resources.GetString("label1.Text");
            // 
            // fpsBottomPanel
            // 
            this.fpsBottomPanel.Controls.Add(this.groupBoxAudioMuting);
            this.fpsBottomPanel.Controls.Add(this.groupBoxFpsLimits);
            this.fpsBottomPanel.Controls.Add(this.chbIsFpsThrottlingEnabled);
            this.fpsBottomPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.fpsBottomPanel.Location = new System.Drawing.Point(3, 112);
            this.fpsBottomPanel.Name = "fpsBottomPanel";
            this.fpsBottomPanel.Size = new System.Drawing.Size(256, 238);
            this.fpsBottomPanel.TabIndex = 1;
            // 
            // groupBoxAudioMuting
            // 
            this.groupBoxAudioMuting.Controls.Add(this.chbIsLocationBannerMuted);
            this.groupBoxAudioMuting.Controls.Add(this.chbIsGateTunnelMuted);
            this.groupBoxAudioMuting.Location = new System.Drawing.Point(10, 158);
            this.groupBoxAudioMuting.Name = "groupBoxAudioMuting";
            this.groupBoxAudioMuting.Size = new System.Drawing.Size(241, 66);
            this.groupBoxAudioMuting.TabIndex = 23;
            this.groupBoxAudioMuting.TabStop = false;
            this.groupBoxAudioMuting.Text = "Audio";
            // 
            // chbIsLocationBannerMuted
            // 
            this.chbIsLocationBannerMuted.AutoSize = true;
            this.chbIsLocationBannerMuted.Location = new System.Drawing.Point(14, 44);
            this.chbIsLocationBannerMuted.Name = "chbIsLocationBannerMuted";
            this.chbIsLocationBannerMuted.Size = new System.Drawing.Size(172, 17);
            this.chbIsLocationBannerMuted.TabIndex = 23;
            this.chbIsLocationBannerMuted.Text = "Mute Location Banner Warp In";
            this.chbIsLocationBannerMuted.UseVisualStyleBackColor = true;
            this.chbIsLocationBannerMuted.CheckedChanged += new System.EventHandler(this.chbIsLocationBannerMuted_CheckedChanged);
            // 
            // chbIsGateTunnelMuted
            // 
            this.chbIsGateTunnelMuted.AutoSize = true;
            this.chbIsGateTunnelMuted.Location = new System.Drawing.Point(14, 21);
            this.chbIsGateTunnelMuted.Name = "chbIsGateTunnelMuted";
            this.chbIsGateTunnelMuted.Size = new System.Drawing.Size(140, 17);
            this.chbIsGateTunnelMuted.TabIndex = 22;
            this.chbIsGateTunnelMuted.Text = "Mute Jump Gate Tunnel";
            this.chbIsGateTunnelMuted.UseVisualStyleBackColor = true;
            this.chbIsGateTunnelMuted.CheckedChanged += new System.EventHandler(this.chbIsGateTunnelMuted_CheckedChanged);
            // 
            // groupBoxFpsLimits
            // 
            this.groupBoxFpsLimits.Controls.Add(this.btnDummyFpsSave);
            this.groupBoxFpsLimits.Controls.Add(lblFpsPredictiveLimit);
            this.groupBoxFpsLimits.Controls.Add(this.numericFpsPredictedLimit);
            this.groupBoxFpsLimits.Controls.Add(lblFpsBackgroundLimit);
            this.groupBoxFpsLimits.Controls.Add(this.numericFpsBackgroundLimit);
            this.groupBoxFpsLimits.Controls.Add(lblFpsForegroundLimit);
            this.groupBoxFpsLimits.Controls.Add(this.numericFpsForegroundLimit);
            this.groupBoxFpsLimits.Controls.Add(this.lblFpsFeatureExpired);
            this.groupBoxFpsLimits.Location = new System.Drawing.Point(10, 37);
            this.groupBoxFpsLimits.Name = "groupBoxFpsLimits";
            this.groupBoxFpsLimits.Size = new System.Drawing.Size(241, 114);
            this.groupBoxFpsLimits.TabIndex = 22;
            this.groupBoxFpsLimits.TabStop = false;
            this.groupBoxFpsLimits.Text = "FPS Limits";
            // 
            // btnDummyFpsSave
            // 
            this.btnDummyFpsSave.Location = new System.Drawing.Point(205, 76);
            this.btnDummyFpsSave.Name = "btnDummyFpsSave";
            this.btnDummyFpsSave.Size = new System.Drawing.Size(30, 23);
            this.btnDummyFpsSave.TabIndex = 30;
            this.btnDummyFpsSave.Text = "Go";
            this.btnDummyFpsSave.UseVisualStyleBackColor = true;
            // 
            // lblFpsPredictiveLimit
            // 
            lblFpsPredictiveLimit.AutoSize = true;
            lblFpsPredictiveLimit.Location = new System.Drawing.Point(11, 81);
            lblFpsPredictiveLimit.Name = "lblFpsPredictiveLimit";
            lblFpsPredictiveLimit.Size = new System.Drawing.Size(81, 13);
            lblFpsPredictiveLimit.TabIndex = 29;
            lblFpsPredictiveLimit.Text = "Predicted Client";
            // 
            // numericFpsPredictedLimit
            // 
            this.numericFpsPredictedLimit.BackColor = System.Drawing.SystemColors.Window;
            this.numericFpsPredictedLimit.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.numericFpsPredictedLimit.CausesValidation = false;
            this.numericFpsPredictedLimit.Location = new System.Drawing.Point(108, 79);
            this.numericFpsPredictedLimit.Maximum = new decimal(new int[] {
            500,
            0,
            0,
            0});
            this.numericFpsPredictedLimit.Name = "numericFpsPredictedLimit";
            this.numericFpsPredictedLimit.Size = new System.Drawing.Size(48, 20);
            this.numericFpsPredictedLimit.TabIndex = 28;
            this.numericFpsPredictedLimit.Value = new decimal(new int[] {
            30,
            0,
            0,
            0});
            this.numericFpsPredictedLimit.Leave += new System.EventHandler(this.numericFpsPredictedLimit_Leave);
            // 
            // lblFpsBackgroundLimit
            // 
            lblFpsBackgroundLimit.AutoSize = true;
            lblFpsBackgroundLimit.Location = new System.Drawing.Point(11, 50);
            lblFpsBackgroundLimit.Name = "lblFpsBackgroundLimit";
            lblFpsBackgroundLimit.Size = new System.Drawing.Size(79, 13);
            lblFpsBackgroundLimit.TabIndex = 27;
            lblFpsBackgroundLimit.Text = "Inactive Clients";
            // 
            // numericFpsBackgroundLimit
            // 
            this.numericFpsBackgroundLimit.BackColor = System.Drawing.SystemColors.Window;
            this.numericFpsBackgroundLimit.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.numericFpsBackgroundLimit.CausesValidation = false;
            this.numericFpsBackgroundLimit.Location = new System.Drawing.Point(108, 48);
            this.numericFpsBackgroundLimit.Maximum = new decimal(new int[] {
            500,
            0,
            0,
            0});
            this.numericFpsBackgroundLimit.Name = "numericFpsBackgroundLimit";
            this.numericFpsBackgroundLimit.Size = new System.Drawing.Size(48, 20);
            this.numericFpsBackgroundLimit.TabIndex = 26;
            this.numericFpsBackgroundLimit.Value = new decimal(new int[] {
            60,
            0,
            0,
            0});
            this.numericFpsBackgroundLimit.Leave += new System.EventHandler(this.numericFpsBackgroundLimit_Leave);
            // 
            // lblFpsForegroundLimit
            // 
            lblFpsForegroundLimit.AutoSize = true;
            lblFpsForegroundLimit.Location = new System.Drawing.Point(11, 21);
            lblFpsForegroundLimit.Name = "lblFpsForegroundLimit";
            lblFpsForegroundLimit.Size = new System.Drawing.Size(66, 13);
            lblFpsForegroundLimit.TabIndex = 25;
            lblFpsForegroundLimit.Text = "Active Client";
            // 
            // numericFpsForegroundLimit
            // 
            this.numericFpsForegroundLimit.BackColor = System.Drawing.SystemColors.Window;
            this.numericFpsForegroundLimit.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.numericFpsForegroundLimit.CausesValidation = false;
            this.numericFpsForegroundLimit.Location = new System.Drawing.Point(108, 19);
            this.numericFpsForegroundLimit.Maximum = new decimal(new int[] {
            500,
            0,
            0,
            0});
            this.numericFpsForegroundLimit.Name = "numericFpsForegroundLimit";
            this.numericFpsForegroundLimit.Size = new System.Drawing.Size(48, 20);
            this.numericFpsForegroundLimit.TabIndex = 24;
            this.numericFpsForegroundLimit.Value = new decimal(new int[] {
            144,
            0,
            0,
            0});
            this.numericFpsForegroundLimit.Leave += new System.EventHandler(this.numericFpsForegroundLimit_Leave);
            // 
            // lblFpsFeatureExpired
            // 
            this.lblFpsFeatureExpired.Location = new System.Drawing.Point(11, 21);
            this.lblFpsFeatureExpired.Name = "lblFpsFeatureExpired";
            this.lblFpsFeatureExpired.Size = new System.Drawing.Size(224, 68);
            this.lblFpsFeatureExpired.TabIndex = 24;
            this.lblFpsFeatureExpired.Text = "This experimental feature has expired.\r\nIf this is the first time running, please" +
    " try closing and re-starting, or alternatively please update to the latest versi" +
    "on.\r\n";
            this.lblFpsFeatureExpired.Visible = false;
            // 
            // chbIsFpsThrottlingEnabled
            // 
            this.chbIsFpsThrottlingEnabled.AutoSize = true;
            this.chbIsFpsThrottlingEnabled.Location = new System.Drawing.Point(10, 9);
            this.chbIsFpsThrottlingEnabled.Name = "chbIsFpsThrottlingEnabled";
            this.chbIsFpsThrottlingEnabled.Size = new System.Drawing.Size(149, 17);
            this.chbIsFpsThrottlingEnabled.TabIndex = 21;
            this.chbIsFpsThrottlingEnabled.Text = "Enable DirectX FPS Limits";
            this.chbIsFpsThrottlingEnabled.UseVisualStyleBackColor = true;
            this.chbIsFpsThrottlingEnabled.CheckedChanged += new System.EventHandler(this.chbIsFpsThrottlingEnabled_CheckedChanged);
            // 
            // AboutTabPage
            // 
            AboutTabPage.BackColor = System.Drawing.SystemColors.Control;
            AboutTabPage.Controls.Add(AboutPanel);
            AboutTabPage.Location = new System.Drawing.Point(124, 4);
            AboutTabPage.Name = "AboutTabPage";
            AboutTabPage.Size = new System.Drawing.Size(262, 353);
            AboutTabPage.TabIndex = 5;
            AboutTabPage.Text = "About";
            // 
            // AboutPanel
            // 
            AboutPanel.BackColor = System.Drawing.Color.Transparent;
            AboutPanel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            AboutPanel.Controls.Add(lblLiabilityDisclaimer);
            AboutPanel.Controls.Add(CreditMaintLabel);
            AboutPanel.Controls.Add(DocumentationLinkLabel);
            AboutPanel.Controls.Add(DescriptionLabel);
            AboutPanel.Controls.Add(this.VersionLabel);
            AboutPanel.Controls.Add(NameLabel);
            AboutPanel.Controls.Add(this.DocumentationLink);
            AboutPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            AboutPanel.Location = new System.Drawing.Point(0, 0);
            AboutPanel.Name = "AboutPanel";
            AboutPanel.Size = new System.Drawing.Size(262, 353);
            AboutPanel.TabIndex = 2;
            // 
            // lblLiabilityDisclaimer
            // 
            lblLiabilityDisclaimer.BackColor = System.Drawing.Color.Transparent;
            lblLiabilityDisclaimer.Location = new System.Drawing.Point(5, 29);
            lblLiabilityDisclaimer.Name = "lblLiabilityDisclaimer";
            lblLiabilityDisclaimer.Padding = new System.Windows.Forms.Padding(8, 3, 8, 3);
            lblLiabilityDisclaimer.Size = new System.Drawing.Size(261, 117);
            lblLiabilityDisclaimer.TabIndex = 9;
            lblLiabilityDisclaimer.Text = resources.GetString("lblLiabilityDisclaimer.Text");
            // 
            // CreditMaintLabel
            // 
            CreditMaintLabel.AutoSize = true;
            CreditMaintLabel.Location = new System.Drawing.Point(5, 273);
            CreditMaintLabel.Name = "CreditMaintLabel";
            CreditMaintLabel.Padding = new System.Windows.Forms.Padding(8, 3, 8, 3);
            CreditMaintLabel.Size = new System.Drawing.Size(258, 19);
            CreditMaintLabel.TabIndex = 7;
            CreditMaintLabel.Text = "Credit to previous maintainer: Phrynohyas Tig-Rah";
            // 
            // DocumentationLinkLabel
            // 
            DocumentationLinkLabel.AutoSize = true;
            DocumentationLinkLabel.Location = new System.Drawing.Point(5, 292);
            DocumentationLinkLabel.Name = "DocumentationLinkLabel";
            DocumentationLinkLabel.Padding = new System.Windows.Forms.Padding(8, 3, 8, 3);
            DocumentationLinkLabel.Size = new System.Drawing.Size(197, 19);
            DocumentationLinkLabel.TabIndex = 6;
            DocumentationLinkLabel.Text = "For more information visit our discord:";
            // 
            // DescriptionLabel
            // 
            DescriptionLabel.BackColor = System.Drawing.Color.Transparent;
            DescriptionLabel.Location = new System.Drawing.Point(5, 157);
            DescriptionLabel.Name = "DescriptionLabel";
            DescriptionLabel.Padding = new System.Windows.Forms.Padding(8, 3, 8, 3);
            DescriptionLabel.Size = new System.Drawing.Size(261, 127);
            DescriptionLabel.TabIndex = 5;
            DescriptionLabel.Text = resources.GetString("DescriptionLabel.Text");
            // 
            // VersionLabel
            // 
            this.VersionLabel.AutoSize = true;
            this.VersionLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.VersionLabel.Location = new System.Drawing.Point(133, 9);
            this.VersionLabel.Name = "VersionLabel";
            this.VersionLabel.Size = new System.Drawing.Size(49, 20);
            this.VersionLabel.TabIndex = 4;
            this.VersionLabel.Text = "1.0.0";
            // 
            // NameLabel
            // 
            NameLabel.AutoSize = true;
            NameLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            NameLabel.Location = new System.Drawing.Point(4, 9);
            NameLabel.Name = "NameLabel";
            NameLabel.Size = new System.Drawing.Size(130, 20);
            NameLabel.TabIndex = 3;
            NameLabel.Text = "EVE-O Preview";
            // 
            // DocumentationLink
            // 
            this.DocumentationLink.Location = new System.Drawing.Point(5, 311);
            this.DocumentationLink.Margin = new System.Windows.Forms.Padding(30, 3, 3, 3);
            this.DocumentationLink.Name = "DocumentationLink";
            this.DocumentationLink.Padding = new System.Windows.Forms.Padding(8, 3, 8, 3);
            this.DocumentationLink.Size = new System.Drawing.Size(262, 33);
            this.DocumentationLink.TabIndex = 2;
            this.DocumentationLink.TabStop = true;
            this.DocumentationLink.Text = "to be set from prresenter to be set from prresenter to be set from prresenter to " +
    "be set from prresenter";
            this.DocumentationLink.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.DocumentationLinkClicked_Handler);
            // 
            // NotifyIcon
            // 
            this.NotifyIcon.ContextMenuStrip = this.TrayMenu;
            this.NotifyIcon.Icon = ((System.Drawing.Icon)(resources.GetObject("NotifyIcon.Icon")));
            this.NotifyIcon.Text = "EVE-O Preview";
            this.NotifyIcon.Visible = true;
            this.NotifyIcon.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.RestoreMainForm_Handler);
            // 
            // TrayMenu
            // 
            this.TrayMenu.ImageScalingSize = new System.Drawing.Size(24, 24);
            this.TrayMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            TitleMenuItem,
            RestoreWindowMenuItem,
            SeparatorMenuItem,
            ExitMenuItem});
            this.TrayMenu.Name = "contextMenuStrip1";
            this.TrayMenu.Size = new System.Drawing.Size(152, 76);
            // 
            // txtToggleHideAllActiveHotkey
            // 
            this.txtToggleHideAllActiveHotkey.BackColor = System.Drawing.SystemColors.Control;
            this.txtToggleHideAllActiveHotkey.Location = new System.Drawing.Point(134, 25);
            this.txtToggleHideAllActiveHotkey.Name = "txtToggleHideAllActiveHotkey";
            this.txtToggleHideAllActiveHotkey.ReadOnly = true;
            this.txtToggleHideAllActiveHotkey.Size = new System.Drawing.Size(119, 20);
            this.txtToggleHideAllActiveHotkey.TabIndex = 35;
            // 
            // lblToggleHideAllActive
            // 
            this.lblToggleHideAllActive.AutoSize = true;
            this.lblToggleHideAllActive.Location = new System.Drawing.Point(6, 28);
            this.lblToggleHideAllActive.Name = "lblToggleHideAllActive";
            this.lblToggleHideAllActive.Size = new System.Drawing.Size(122, 13);
            this.lblToggleHideAllActive.TabIndex = 34;
            this.lblToggleHideAllActive.Text = "Toggle Hide All (Hotkey)";
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.Control;
            this.ClientSize = new System.Drawing.Size(390, 361);
            this.Controls.Add(ContentTabControl);
            this.ForeColor = System.Drawing.SystemColors.ControlText;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(0);
            this.MaximizeBox = false;
            this.Name = "MainForm";
            this.Text = "EVE-O Preview";
            this.TopMost = true;
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainFormClosing_Handler);
            this.Load += new System.EventHandler(this.MainFormResize_Handler);
            this.Resize += new System.EventHandler(this.MainFormResize_Handler);
            ContentTabControl.ResumeLayout(false);
            GeneralTabPage.ResumeLayout(false);
            GeneralSettingsPanel.ResumeLayout(false);
            GeneralSettingsPanel.PerformLayout();
            ThumbnailTabPage.ResumeLayout(false);
            ThumbnailSettingsPanel.ResumeLayout(false);
            ThumbnailSettingsPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.ThumbnailsWidthNumericEdit)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.ThumbnailsHeightNumericEdit)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.ThumbnailOpacityTrackBar)).EndInit();
            this.ZoomTabPage.ResumeLayout(false);
            ZoomSettingsPanel.ResumeLayout(false);
            ZoomSettingsPanel.PerformLayout();
            this.ZoomAnchorPanel.ResumeLayout(false);
            this.ZoomAnchorPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.ThumbnailZoomFactorNumericEdit)).EndInit();
            OverlayTabPage.ResumeLayout(false);
            OverlaySettingsPanel.ResumeLayout(false);
            OverlaySettingsPanel.PerformLayout();
            this.groupBoxOverlayFont.ResumeLayout(false);
            this.groupBoxOverlayFont.PerformLayout();
            ClientsTabPage.ResumeLayout(false);
            ClientsPanel.ResumeLayout(false);
            this.activeClientsSplitContainer.Panel1.ResumeLayout(false);
            this.activeClientsSplitContainer.Panel1.PerformLayout();
            this.activeClientsSplitContainer.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.activeClientsSplitContainer)).EndInit();
            this.activeClientsSplitContainer.ResumeLayout(false);
            CycleGroupTabPage.ResumeLayout(false);
            this.CycleGroupPanel.ResumeLayout(false);
            this.CycleGroupPanel.PerformLayout();
            this.FpsLimiterTabPage.ResumeLayout(false);
            this.fpsMainLayoutPanel.ResumeLayout(false);
            this.fpsTopPanel.ResumeLayout(false);
            this.fpsBottomPanel.ResumeLayout(false);
            this.fpsBottomPanel.PerformLayout();
            this.groupBoxAudioMuting.ResumeLayout(false);
            this.groupBoxAudioMuting.PerformLayout();
            this.groupBoxFpsLimits.ResumeLayout(false);
            this.groupBoxFpsLimits.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericFpsPredictedLimit)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericFpsBackgroundLimit)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericFpsForegroundLimit)).EndInit();
            AboutTabPage.ResumeLayout(false);
            AboutPanel.ResumeLayout(false);
            AboutPanel.PerformLayout();
            this.TrayMenu.ResumeLayout(false);
            this.ResumeLayout(false);

		}

		#endregion
		private NotifyIcon NotifyIcon;
		private ContextMenuStrip TrayMenu;
		private TabPage ZoomTabPage;
		private CheckBox EnableClientLayoutTrackingCheckBox;
		private CheckBox HideActiveClientThumbnailCheckBox;
		private CheckBox ShowThumbnailsAlwaysOnTopCheckBox;
		private CheckBox HideThumbnailsOnLostFocusCheckBox;
		private CheckBox EnablePerClientThumbnailsLayoutsCheckBox;
		private CheckBox MinimizeToTrayCheckBox;
		private NumericUpDown ThumbnailsWidthNumericEdit;
		private NumericUpDown ThumbnailsHeightNumericEdit;
		private TrackBar ThumbnailOpacityTrackBar;
		private Panel ZoomAnchorPanel;
		private RadioButton ZoomAanchorNWRadioButton;
		private RadioButton ZoomAanchorNRadioButton;
		private RadioButton ZoomAanchorNERadioButton;
		private RadioButton ZoomAanchorWRadioButton;
		private RadioButton ZoomAanchorSERadioButton;
		private RadioButton ZoomAanchorCRadioButton;
		private RadioButton ZoomAanchorSRadioButton;
		private RadioButton ZoomAanchorERadioButton;
		private RadioButton ZoomAanchorSWRadioButton;
		private CheckBox EnableThumbnailZoomCheckBox;
		private NumericUpDown ThumbnailZoomFactorNumericEdit;
		private Label HighlightColorLabel;
		private Panel ActiveClientHighlightColorButton;
		private CheckBox EnableActiveClientHighlightCheckBox;
		private CheckBox ShowThumbnailOverlaysCheckBox;
		private CheckBox ShowThumbnailFramesCheckBox;
		private CheckedListBox ThumbnailsList;
		private LinkLabel DocumentationLink;
		private Label VersionLabel;
		private CheckBox MinimizeInactiveClientsCheckBox;
        private Panel CycleGroupPanel;
        private Button addClientToCycleGroupButton;
        private Button cycleGroupMoveClientOrderUpButton;
        private Button removeClientToCycleGroupButton;
        private Button addNewGroupButton;
        private Label cycleGroupClientOrderLabel;
        private ListBox cycleGroupClientOrderList;
        private TextBox cycleGroupBackwardHotkey2Text;
        private TextBox cycleGroupBackwardHotkey1Text;
        private Label cycleGroupBackHotkeyLabel;
        private TextBox cycleGroupForwardHotkey2Text;
        private TextBox cycleGroupForwardHotkey1Text;
        private Label cycleGroupForwardHotkeyLabel;
        private TextBox cycleGroupDescriptionText;
        private Label cycleGroupDescriptionLabel;
        private Label CycleGroupLabel;
        private ComboBox selectCycleGroupComboBox;
        private SplitContainer activeClientsSplitContainer;
        private Button removeGroupButton;
        private Button btnSetOverlayFont;
        private OutlinedLabel lblDisplaySampleFont;
        private GroupBox groupBoxOverlayFont;
        private Button btnSetOverlayFontColor;
        private TextBox txtFontOutlineWidth;
        private Button btnFontOutlineColor;
        private Label lblTitleOffsetTop;
        private TextBox txtTitleOffsetTop;
        private Label lblTitleOffsetLeft;
        private TextBox txtTitleOffsetLeft;
        private Label lblTitleBorderWidth;
        private TabPage FpsLimiterTabPage;
        private TableLayoutPanel fpsMainLayoutPanel;
        private Panel fpsBottomPanel;
        private Panel fpsTopPanel;
        private GroupBox groupBoxFpsLimits;
        private NumericUpDown numericFpsForegroundLimit;
        private CheckBox chbIsFpsThrottlingEnabled;
        private NumericUpDown numericFpsPredictedLimit;
        private NumericUpDown numericFpsBackgroundLimit;
        private ToolTip toolTip;
        private Button btnDummyFpsSave;
        private Label lblFpsFeatureExpired;
        private GroupBox groupBoxAudioMuting;
        private CheckBox chbIsLocationBannerMuted;
        private CheckBox chbIsGateTunnelMuted;
        private TextBox txtToggleHideAllActiveHotkey;
        private Label lblToggleHideAllActive;
    }
}
