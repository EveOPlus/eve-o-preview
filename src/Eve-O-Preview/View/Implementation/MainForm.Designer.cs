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
            components = new System.ComponentModel.Container();
            ToolStripMenuItem RestoreWindowMenuItem;
            ToolStripMenuItem ExitMenuItem;
            ToolStripMenuItem TitleMenuItem;
            ToolStripSeparator SeparatorMenuItem;
            TabPage GeneralTabPage;
            Panel GeneralSettingsPanel;
            TabPage ThumbnailTabPage;
            Panel ThumbnailSettingsPanel;
            Label HeigthLabel;
            Label WidthLabel;
            Label OpacityLabel;
            Panel ZoomSettingsPanel;
            Label ZoomFactorLabel;
            Label ZoomAnchorLabel;
            TabPage OverlayTabPage;
            Panel OverlaySettingsPanel;
            Panel ClientsPanel;
            Label ThumbnailsListLabel;
            TabPage CycleGroupTabPage;
            Label label1;
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            Label lblFpsPredictiveLimit;
            Label lblFpsBackgroundLimit;
            Label lblFpsForegroundLimit;
            TabPage AboutTabPage;
            Panel AboutPanel;
            Label lblLiabilityDisclaimer;
            Label CreditMaintLabel;
            Label DocumentationLinkLabel;
            Label DescriptionLabel;
            Label NameLabel;
            MinimizeInactiveClientsCheckBox = new CheckBox();
            EnableClientLayoutTrackingCheckBox = new CheckBox();
            HideActiveClientThumbnailCheckBox = new CheckBox();
            ShowThumbnailsAlwaysOnTopCheckBox = new CheckBox();
            HideThumbnailsOnLostFocusCheckBox = new CheckBox();
            EnablePerClientThumbnailsLayoutsCheckBox = new CheckBox();
            MinimizeToTrayCheckBox = new CheckBox();
            ThumbnailsWidthNumericEdit = new NumericUpDown();
            ThumbnailsHeightNumericEdit = new NumericUpDown();
            ThumbnailOpacityTrackBar = new TrackBar();
            ZoomAnchorPanel = new Panel();
            ZoomAanchorNWRadioButton = new RadioButton();
            ZoomAanchorNRadioButton = new RadioButton();
            ZoomAanchorNERadioButton = new RadioButton();
            ZoomAanchorWRadioButton = new RadioButton();
            ZoomAanchorSERadioButton = new RadioButton();
            ZoomAanchorCRadioButton = new RadioButton();
            ZoomAanchorSRadioButton = new RadioButton();
            ZoomAanchorERadioButton = new RadioButton();
            ZoomAanchorSWRadioButton = new RadioButton();
            EnableThumbnailZoomCheckBox = new CheckBox();
            ThumbnailZoomFactorNumericEdit = new NumericUpDown();
            groupBoxOverlayFont = new GroupBox();
            lblTitleOffsetTop = new Label();
            txtTitleOffsetTop = new TextBox();
            lblTitleOffsetLeft = new Label();
            txtTitleOffsetLeft = new TextBox();
            lblTitleBorderWidth = new Label();
            txtFontOutlineWidth = new TextBox();
            btnFontOutlineColor = new Button();
            btnSetOverlayFontColor = new Button();
            lblDisplaySampleFont = new OutlinedLabel();
            btnSetOverlayFont = new Button();
            HighlightColorLabel = new Label();
            ActiveClientHighlightColorButton = new Panel();
            EnableActiveClientHighlightCheckBox = new CheckBox();
            ShowThumbnailOverlaysCheckBox = new CheckBox();
            ShowThumbnailFramesCheckBox = new CheckBox();
            activeClientsSplitContainer = new SplitContainer();
            groupBoxToggleHideAllThumbnails = new GroupBox();
            btnMinimizeAllClients = new Button();
            lblMinimizeAllClientsHotkey = new Label();
            txtMinimizeAllClientsHotkey = new TextBox();
            btnToggleHideAll = new Button();
            lblToggleHideAllActiveHotkey = new Label();
            txtToggleHideAllActiveHotkey = new TextBox();
            ThumbnailsList = new CheckedListBox();
            CycleGroupPanel = new Panel();
            splitContainerMainCycleGroup = new SplitContainer();
            CycleGroupLabel = new Label();
            removeGroupButton = new Button();
            addClientToCycleGroupButton = new Button();
            cycleGroupMoveClientOrderUpButton = new Button();
            selectCycleGroupComboBox = new ComboBox();
            removeClientToCycleGroupButton = new Button();
            cycleGroupDescriptionLabel = new Label();
            addNewGroupButton = new Button();
            cycleGroupDescriptionText = new TextBox();
            cycleGroupClientOrderLabel = new Label();
            cycleGroupForwardHotkeyLabel = new Label();
            cycleGroupForwardHotkey1Text = new TextBox();
            cycleGroupBackwardHotkey2Text = new TextBox();
            cycleGroupForwardHotkey2Text = new TextBox();
            cycleGroupBackwardHotkey1Text = new TextBox();
            cycleGroupBackHotkeyLabel = new Label();
            cycleGroupClientOrderList = new ListBox();
            VersionLabel = new Label();
            DocumentationLink = new LinkLabel();
            ClientsTabPage = new TabPage();
            ContentTabControl = new TabControl();
            ZoomTabPage = new TabPage();
            FpsLimiterTabPage = new TabPage();
            fpsMainLayoutPanel = new TableLayoutPanel();
            fpsTopPanel = new Panel();
            fpsBottomPanel = new Panel();
            groupBoxAudioMuting = new GroupBox();
            chbIsLocationBannerMuted = new CheckBox();
            chbIsGateTunnelMuted = new CheckBox();
            groupBoxFpsLimits = new GroupBox();
            btnDummyFpsSave = new Button();
            numericFpsPredictedLimit = new NumericUpDown();
            numericFpsBackgroundLimit = new NumericUpDown();
            numericFpsForegroundLimit = new NumericUpDown();
            lblFpsFeatureExpired = new Label();
            chbIsFpsThrottlingEnabled = new CheckBox();
            tabPageProfiles = new TabPage();
            splitContainerMainProfiles = new SplitContainer();
            lblProfilesExperimentalWarning = new Label();
            lblLoadedProfileName = new Label();
            btnDeleteProfile = new Button();
            btnCloneProfile = new Button();
            txtLoadedProfileName = new TextBox();
            listBoxProfiles = new ListBox();
            NotifyIcon = new NotifyIcon(components);
            TrayMenu = new ContextMenuStrip(components);
            instantToolTip = new ToolTip(components);
            RestoreWindowMenuItem = new ToolStripMenuItem();
            ExitMenuItem = new ToolStripMenuItem();
            TitleMenuItem = new ToolStripMenuItem();
            SeparatorMenuItem = new ToolStripSeparator();
            GeneralTabPage = new TabPage();
            GeneralSettingsPanel = new Panel();
            ThumbnailTabPage = new TabPage();
            ThumbnailSettingsPanel = new Panel();
            HeigthLabel = new Label();
            WidthLabel = new Label();
            OpacityLabel = new Label();
            ZoomSettingsPanel = new Panel();
            ZoomFactorLabel = new Label();
            ZoomAnchorLabel = new Label();
            OverlayTabPage = new TabPage();
            OverlaySettingsPanel = new Panel();
            ClientsPanel = new Panel();
            ThumbnailsListLabel = new Label();
            CycleGroupTabPage = new TabPage();
            label1 = new Label();
            lblFpsPredictiveLimit = new Label();
            lblFpsBackgroundLimit = new Label();
            lblFpsForegroundLimit = new Label();
            AboutTabPage = new TabPage();
            AboutPanel = new Panel();
            lblLiabilityDisclaimer = new Label();
            CreditMaintLabel = new Label();
            DocumentationLinkLabel = new Label();
            DescriptionLabel = new Label();
            NameLabel = new Label();
            GeneralTabPage.SuspendLayout();
            GeneralSettingsPanel.SuspendLayout();
            ThumbnailTabPage.SuspendLayout();
            ThumbnailSettingsPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)ThumbnailsWidthNumericEdit).BeginInit();
            ((System.ComponentModel.ISupportInitialize)ThumbnailsHeightNumericEdit).BeginInit();
            ((System.ComponentModel.ISupportInitialize)ThumbnailOpacityTrackBar).BeginInit();
            ZoomSettingsPanel.SuspendLayout();
            ZoomAnchorPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)ThumbnailZoomFactorNumericEdit).BeginInit();
            OverlayTabPage.SuspendLayout();
            OverlaySettingsPanel.SuspendLayout();
            groupBoxOverlayFont.SuspendLayout();
            ClientsPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)activeClientsSplitContainer).BeginInit();
            activeClientsSplitContainer.Panel1.SuspendLayout();
            activeClientsSplitContainer.Panel2.SuspendLayout();
            activeClientsSplitContainer.SuspendLayout();
            groupBoxToggleHideAllThumbnails.SuspendLayout();
            CycleGroupTabPage.SuspendLayout();
            CycleGroupPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainerMainCycleGroup).BeginInit();
            splitContainerMainCycleGroup.Panel1.SuspendLayout();
            splitContainerMainCycleGroup.Panel2.SuspendLayout();
            splitContainerMainCycleGroup.SuspendLayout();
            AboutTabPage.SuspendLayout();
            AboutPanel.SuspendLayout();
            ClientsTabPage.SuspendLayout();
            ContentTabControl.SuspendLayout();
            ZoomTabPage.SuspendLayout();
            FpsLimiterTabPage.SuspendLayout();
            fpsMainLayoutPanel.SuspendLayout();
            fpsTopPanel.SuspendLayout();
            fpsBottomPanel.SuspendLayout();
            groupBoxAudioMuting.SuspendLayout();
            groupBoxFpsLimits.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)numericFpsPredictedLimit).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numericFpsBackgroundLimit).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numericFpsForegroundLimit).BeginInit();
            tabPageProfiles.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainerMainProfiles).BeginInit();
            splitContainerMainProfiles.Panel1.SuspendLayout();
            splitContainerMainProfiles.Panel2.SuspendLayout();
            splitContainerMainProfiles.SuspendLayout();
            TrayMenu.SuspendLayout();
            SuspendLayout();
            // 
            // RestoreWindowMenuItem
            // 
            RestoreWindowMenuItem.Name = "RestoreWindowMenuItem";
            RestoreWindowMenuItem.Size = new Size(151, 22);
            RestoreWindowMenuItem.Text = "Restore";
            RestoreWindowMenuItem.Click += RestoreMainForm_Handler;
            // 
            // ExitMenuItem
            // 
            ExitMenuItem.Name = "ExitMenuItem";
            ExitMenuItem.Size = new Size(151, 22);
            ExitMenuItem.Text = "Exit";
            ExitMenuItem.Click += ExitMenuItemClick_Handler;
            // 
            // TitleMenuItem
            // 
            TitleMenuItem.Enabled = false;
            TitleMenuItem.Name = "TitleMenuItem";
            TitleMenuItem.Size = new Size(151, 22);
            TitleMenuItem.Text = "EVE-O Preview";
            // 
            // SeparatorMenuItem
            // 
            SeparatorMenuItem.Name = "SeparatorMenuItem";
            SeparatorMenuItem.Size = new Size(148, 6);
            // 
            // GeneralTabPage
            // 
            GeneralTabPage.BackColor = SystemColors.Control;
            GeneralTabPage.Controls.Add(GeneralSettingsPanel);
            GeneralTabPage.Location = new Point(124, 4);
            GeneralTabPage.Margin = new Padding(4, 3, 4, 3);
            GeneralTabPage.Name = "GeneralTabPage";
            GeneralTabPage.Padding = new Padding(4, 3, 4, 3);
            GeneralTabPage.Size = new Size(332, 409);
            GeneralTabPage.TabIndex = 0;
            GeneralTabPage.Text = "General";
            // 
            // GeneralSettingsPanel
            // 
            GeneralSettingsPanel.BorderStyle = BorderStyle.FixedSingle;
            GeneralSettingsPanel.Controls.Add(MinimizeInactiveClientsCheckBox);
            GeneralSettingsPanel.Controls.Add(EnableClientLayoutTrackingCheckBox);
            GeneralSettingsPanel.Controls.Add(HideActiveClientThumbnailCheckBox);
            GeneralSettingsPanel.Controls.Add(ShowThumbnailsAlwaysOnTopCheckBox);
            GeneralSettingsPanel.Controls.Add(HideThumbnailsOnLostFocusCheckBox);
            GeneralSettingsPanel.Controls.Add(EnablePerClientThumbnailsLayoutsCheckBox);
            GeneralSettingsPanel.Controls.Add(MinimizeToTrayCheckBox);
            GeneralSettingsPanel.Dock = DockStyle.Fill;
            GeneralSettingsPanel.Location = new Point(4, 3);
            GeneralSettingsPanel.Margin = new Padding(4, 3, 4, 3);
            GeneralSettingsPanel.Name = "GeneralSettingsPanel";
            GeneralSettingsPanel.Size = new Size(324, 403);
            GeneralSettingsPanel.TabIndex = 18;
            // 
            // MinimizeInactiveClientsCheckBox
            // 
            MinimizeInactiveClientsCheckBox.AutoSize = true;
            MinimizeInactiveClientsCheckBox.Location = new Point(9, 91);
            MinimizeInactiveClientsCheckBox.Margin = new Padding(4, 3, 4, 3);
            MinimizeInactiveClientsCheckBox.Name = "MinimizeInactiveClientsCheckBox";
            MinimizeInactiveClientsCheckBox.Size = new Size(178, 19);
            MinimizeInactiveClientsCheckBox.TabIndex = 24;
            MinimizeInactiveClientsCheckBox.Text = "Minimize inactive EVE clients";
            MinimizeInactiveClientsCheckBox.UseVisualStyleBackColor = true;
            MinimizeInactiveClientsCheckBox.CheckedChanged += OptionChanged_Handler;
            // 
            // EnableClientLayoutTrackingCheckBox
            // 
            EnableClientLayoutTrackingCheckBox.AutoSize = true;
            EnableClientLayoutTrackingCheckBox.Location = new Point(9, 36);
            EnableClientLayoutTrackingCheckBox.Margin = new Padding(4, 3, 4, 3);
            EnableClientLayoutTrackingCheckBox.Name = "EnableClientLayoutTrackingCheckBox";
            EnableClientLayoutTrackingCheckBox.Size = new Size(137, 19);
            EnableClientLayoutTrackingCheckBox.TabIndex = 19;
            EnableClientLayoutTrackingCheckBox.Text = "Track client locations";
            EnableClientLayoutTrackingCheckBox.UseVisualStyleBackColor = true;
            EnableClientLayoutTrackingCheckBox.CheckedChanged += OptionChanged_Handler;
            // 
            // HideActiveClientThumbnailCheckBox
            // 
            HideActiveClientThumbnailCheckBox.AutoSize = true;
            HideActiveClientThumbnailCheckBox.Checked = true;
            HideActiveClientThumbnailCheckBox.CheckState = CheckState.Checked;
            HideActiveClientThumbnailCheckBox.Location = new Point(9, 63);
            HideActiveClientThumbnailCheckBox.Margin = new Padding(4, 3, 4, 3);
            HideActiveClientThumbnailCheckBox.Name = "HideActiveClientThumbnailCheckBox";
            HideActiveClientThumbnailCheckBox.Size = new Size(197, 19);
            HideActiveClientThumbnailCheckBox.TabIndex = 20;
            HideActiveClientThumbnailCheckBox.Text = "Hide preview of active EVE client";
            HideActiveClientThumbnailCheckBox.UseVisualStyleBackColor = true;
            HideActiveClientThumbnailCheckBox.CheckedChanged += OptionChanged_Handler;
            // 
            // ShowThumbnailsAlwaysOnTopCheckBox
            // 
            ShowThumbnailsAlwaysOnTopCheckBox.AutoSize = true;
            ShowThumbnailsAlwaysOnTopCheckBox.Checked = true;
            ShowThumbnailsAlwaysOnTopCheckBox.CheckState = CheckState.Checked;
            ShowThumbnailsAlwaysOnTopCheckBox.Location = new Point(9, 119);
            ShowThumbnailsAlwaysOnTopCheckBox.Margin = new Padding(4, 3, 4, 3);
            ShowThumbnailsAlwaysOnTopCheckBox.Name = "ShowThumbnailsAlwaysOnTopCheckBox";
            ShowThumbnailsAlwaysOnTopCheckBox.RightToLeft = RightToLeft.No;
            ShowThumbnailsAlwaysOnTopCheckBox.Size = new Size(148, 19);
            ShowThumbnailsAlwaysOnTopCheckBox.TabIndex = 21;
            ShowThumbnailsAlwaysOnTopCheckBox.Text = "Previews always on top";
            ShowThumbnailsAlwaysOnTopCheckBox.UseVisualStyleBackColor = true;
            ShowThumbnailsAlwaysOnTopCheckBox.CheckedChanged += OptionChanged_Handler;
            // 
            // HideThumbnailsOnLostFocusCheckBox
            // 
            HideThumbnailsOnLostFocusCheckBox.AutoSize = true;
            HideThumbnailsOnLostFocusCheckBox.Checked = true;
            HideThumbnailsOnLostFocusCheckBox.CheckState = CheckState.Checked;
            HideThumbnailsOnLostFocusCheckBox.Location = new Point(9, 147);
            HideThumbnailsOnLostFocusCheckBox.Margin = new Padding(4, 3, 4, 3);
            HideThumbnailsOnLostFocusCheckBox.Name = "HideThumbnailsOnLostFocusCheckBox";
            HideThumbnailsOnLostFocusCheckBox.Size = new Size(252, 19);
            HideThumbnailsOnLostFocusCheckBox.TabIndex = 22;
            HideThumbnailsOnLostFocusCheckBox.Text = "Hide previews when EVE client is not active";
            HideThumbnailsOnLostFocusCheckBox.UseVisualStyleBackColor = true;
            HideThumbnailsOnLostFocusCheckBox.CheckedChanged += OptionChanged_Handler;
            // 
            // EnablePerClientThumbnailsLayoutsCheckBox
            // 
            EnablePerClientThumbnailsLayoutsCheckBox.AutoSize = true;
            EnablePerClientThumbnailsLayoutsCheckBox.Checked = true;
            EnablePerClientThumbnailsLayoutsCheckBox.CheckState = CheckState.Checked;
            EnablePerClientThumbnailsLayoutsCheckBox.Location = new Point(9, 174);
            EnablePerClientThumbnailsLayoutsCheckBox.Margin = new Padding(4, 3, 4, 3);
            EnablePerClientThumbnailsLayoutsCheckBox.Name = "EnablePerClientThumbnailsLayoutsCheckBox";
            EnablePerClientThumbnailsLayoutsCheckBox.Size = new Size(200, 19);
            EnablePerClientThumbnailsLayoutsCheckBox.TabIndex = 23;
            EnablePerClientThumbnailsLayoutsCheckBox.Text = "Unique layout for each EVE client";
            EnablePerClientThumbnailsLayoutsCheckBox.UseVisualStyleBackColor = true;
            EnablePerClientThumbnailsLayoutsCheckBox.CheckedChanged += OptionChanged_Handler;
            // 
            // MinimizeToTrayCheckBox
            // 
            MinimizeToTrayCheckBox.AutoSize = true;
            MinimizeToTrayCheckBox.Location = new Point(9, 8);
            MinimizeToTrayCheckBox.Margin = new Padding(4, 3, 4, 3);
            MinimizeToTrayCheckBox.Name = "MinimizeToTrayCheckBox";
            MinimizeToTrayCheckBox.Size = new Size(155, 19);
            MinimizeToTrayCheckBox.TabIndex = 18;
            MinimizeToTrayCheckBox.Text = "Minimize to System Tray";
            MinimizeToTrayCheckBox.UseVisualStyleBackColor = true;
            MinimizeToTrayCheckBox.CheckedChanged += OptionChanged_Handler;
            // 
            // ThumbnailTabPage
            // 
            ThumbnailTabPage.BackColor = SystemColors.Control;
            ThumbnailTabPage.Controls.Add(ThumbnailSettingsPanel);
            ThumbnailTabPage.Location = new Point(124, 4);
            ThumbnailTabPage.Margin = new Padding(4, 3, 4, 3);
            ThumbnailTabPage.Name = "ThumbnailTabPage";
            ThumbnailTabPage.Padding = new Padding(4, 3, 4, 3);
            ThumbnailTabPage.Size = new Size(332, 409);
            ThumbnailTabPage.TabIndex = 1;
            ThumbnailTabPage.Text = "Thumbnail";
            // 
            // ThumbnailSettingsPanel
            // 
            ThumbnailSettingsPanel.BorderStyle = BorderStyle.FixedSingle;
            ThumbnailSettingsPanel.Controls.Add(HeigthLabel);
            ThumbnailSettingsPanel.Controls.Add(WidthLabel);
            ThumbnailSettingsPanel.Controls.Add(ThumbnailsWidthNumericEdit);
            ThumbnailSettingsPanel.Controls.Add(ThumbnailsHeightNumericEdit);
            ThumbnailSettingsPanel.Controls.Add(ThumbnailOpacityTrackBar);
            ThumbnailSettingsPanel.Controls.Add(OpacityLabel);
            ThumbnailSettingsPanel.Dock = DockStyle.Fill;
            ThumbnailSettingsPanel.Location = new Point(4, 3);
            ThumbnailSettingsPanel.Margin = new Padding(4, 3, 4, 3);
            ThumbnailSettingsPanel.Name = "ThumbnailSettingsPanel";
            ThumbnailSettingsPanel.Size = new Size(324, 403);
            ThumbnailSettingsPanel.TabIndex = 19;
            // 
            // HeigthLabel
            // 
            HeigthLabel.AutoSize = true;
            HeigthLabel.Location = new Point(9, 66);
            HeigthLabel.Margin = new Padding(4, 0, 4, 0);
            HeigthLabel.Name = "HeigthLabel";
            HeigthLabel.Size = new Size(104, 15);
            HeigthLabel.TabIndex = 24;
            HeigthLabel.Text = "Thumbnail Heigth";
            // 
            // WidthLabel
            // 
            WidthLabel.AutoSize = true;
            WidthLabel.Location = new Point(9, 38);
            WidthLabel.Margin = new Padding(4, 0, 4, 0);
            WidthLabel.Name = "WidthLabel";
            WidthLabel.Size = new Size(100, 15);
            WidthLabel.TabIndex = 23;
            WidthLabel.Text = "Thumbnail Width";
            // 
            // ThumbnailsWidthNumericEdit
            // 
            ThumbnailsWidthNumericEdit.BackColor = SystemColors.Window;
            ThumbnailsWidthNumericEdit.BorderStyle = BorderStyle.FixedSingle;
            ThumbnailsWidthNumericEdit.CausesValidation = false;
            ThumbnailsWidthNumericEdit.Increment = new decimal(new int[] { 10, 0, 0, 0 });
            ThumbnailsWidthNumericEdit.Location = new Point(122, 36);
            ThumbnailsWidthNumericEdit.Margin = new Padding(4, 3, 4, 3);
            ThumbnailsWidthNumericEdit.Maximum = new decimal(new int[] { 999999, 0, 0, 0 });
            ThumbnailsWidthNumericEdit.Name = "ThumbnailsWidthNumericEdit";
            ThumbnailsWidthNumericEdit.Size = new Size(56, 23);
            ThumbnailsWidthNumericEdit.TabIndex = 21;
            ThumbnailsWidthNumericEdit.Value = new decimal(new int[] { 100, 0, 0, 0 });
            ThumbnailsWidthNumericEdit.ValueChanged += ThumbnailSizeChanged_Handler;
            // 
            // ThumbnailsHeightNumericEdit
            // 
            ThumbnailsHeightNumericEdit.BackColor = SystemColors.Window;
            ThumbnailsHeightNumericEdit.BorderStyle = BorderStyle.FixedSingle;
            ThumbnailsHeightNumericEdit.CausesValidation = false;
            ThumbnailsHeightNumericEdit.Increment = new decimal(new int[] { 10, 0, 0, 0 });
            ThumbnailsHeightNumericEdit.Location = new Point(122, 63);
            ThumbnailsHeightNumericEdit.Margin = new Padding(4, 3, 4, 3);
            ThumbnailsHeightNumericEdit.Maximum = new decimal(new int[] { 99999999, 0, 0, 0 });
            ThumbnailsHeightNumericEdit.Name = "ThumbnailsHeightNumericEdit";
            ThumbnailsHeightNumericEdit.Size = new Size(56, 23);
            ThumbnailsHeightNumericEdit.TabIndex = 22;
            ThumbnailsHeightNumericEdit.Value = new decimal(new int[] { 70, 0, 0, 0 });
            ThumbnailsHeightNumericEdit.ValueChanged += ThumbnailSizeChanged_Handler;
            // 
            // ThumbnailOpacityTrackBar
            // 
            ThumbnailOpacityTrackBar.AutoSize = false;
            ThumbnailOpacityTrackBar.LargeChange = 10;
            ThumbnailOpacityTrackBar.Location = new Point(71, 7);
            ThumbnailOpacityTrackBar.Margin = new Padding(4, 3, 4, 3);
            ThumbnailOpacityTrackBar.Maximum = 100;
            ThumbnailOpacityTrackBar.Minimum = 20;
            ThumbnailOpacityTrackBar.Name = "ThumbnailOpacityTrackBar";
            ThumbnailOpacityTrackBar.Size = new Size(223, 25);
            ThumbnailOpacityTrackBar.TabIndex = 20;
            ThumbnailOpacityTrackBar.TickFrequency = 10;
            ThumbnailOpacityTrackBar.Value = 20;
            ThumbnailOpacityTrackBar.ValueChanged += OptionChanged_Handler;
            // 
            // OpacityLabel
            // 
            OpacityLabel.AutoSize = true;
            OpacityLabel.Location = new Point(9, 10);
            OpacityLabel.Margin = new Padding(4, 0, 4, 0);
            OpacityLabel.Name = "OpacityLabel";
            OpacityLabel.Size = new Size(48, 15);
            OpacityLabel.TabIndex = 19;
            OpacityLabel.Text = "Opacity";
            // 
            // ZoomSettingsPanel
            // 
            ZoomSettingsPanel.BorderStyle = BorderStyle.FixedSingle;
            ZoomSettingsPanel.Controls.Add(ZoomFactorLabel);
            ZoomSettingsPanel.Controls.Add(ZoomAnchorPanel);
            ZoomSettingsPanel.Controls.Add(ZoomAnchorLabel);
            ZoomSettingsPanel.Controls.Add(EnableThumbnailZoomCheckBox);
            ZoomSettingsPanel.Controls.Add(ThumbnailZoomFactorNumericEdit);
            ZoomSettingsPanel.Dock = DockStyle.Fill;
            ZoomSettingsPanel.Location = new Point(0, 0);
            ZoomSettingsPanel.Margin = new Padding(4, 3, 4, 3);
            ZoomSettingsPanel.Name = "ZoomSettingsPanel";
            ZoomSettingsPanel.Size = new Size(332, 409);
            ZoomSettingsPanel.TabIndex = 36;
            // 
            // ZoomFactorLabel
            // 
            ZoomFactorLabel.AutoSize = true;
            ZoomFactorLabel.Location = new Point(9, 38);
            ZoomFactorLabel.Margin = new Padding(4, 0, 4, 0);
            ZoomFactorLabel.Name = "ZoomFactorLabel";
            ZoomFactorLabel.Size = new Size(75, 15);
            ZoomFactorLabel.TabIndex = 39;
            ZoomFactorLabel.Text = "Zoom Factor";
            // 
            // ZoomAnchorPanel
            // 
            ZoomAnchorPanel.BorderStyle = BorderStyle.FixedSingle;
            ZoomAnchorPanel.Controls.Add(ZoomAanchorNWRadioButton);
            ZoomAnchorPanel.Controls.Add(ZoomAanchorNRadioButton);
            ZoomAnchorPanel.Controls.Add(ZoomAanchorNERadioButton);
            ZoomAnchorPanel.Controls.Add(ZoomAanchorWRadioButton);
            ZoomAnchorPanel.Controls.Add(ZoomAanchorSERadioButton);
            ZoomAnchorPanel.Controls.Add(ZoomAanchorCRadioButton);
            ZoomAnchorPanel.Controls.Add(ZoomAanchorSRadioButton);
            ZoomAnchorPanel.Controls.Add(ZoomAanchorERadioButton);
            ZoomAnchorPanel.Controls.Add(ZoomAanchorSWRadioButton);
            ZoomAnchorPanel.Location = new Point(94, 62);
            ZoomAnchorPanel.Margin = new Padding(4, 3, 4, 3);
            ZoomAnchorPanel.Name = "ZoomAnchorPanel";
            ZoomAnchorPanel.Size = new Size(90, 84);
            ZoomAnchorPanel.TabIndex = 38;
            // 
            // ZoomAanchorNWRadioButton
            // 
            ZoomAanchorNWRadioButton.AutoSize = true;
            ZoomAanchorNWRadioButton.Location = new Point(4, 3);
            ZoomAanchorNWRadioButton.Margin = new Padding(4, 3, 4, 3);
            ZoomAanchorNWRadioButton.Name = "ZoomAanchorNWRadioButton";
            ZoomAanchorNWRadioButton.Size = new Size(14, 13);
            ZoomAanchorNWRadioButton.TabIndex = 0;
            ZoomAanchorNWRadioButton.TabStop = true;
            ZoomAanchorNWRadioButton.UseVisualStyleBackColor = true;
            ZoomAanchorNWRadioButton.CheckedChanged += OptionChanged_Handler;
            // 
            // ZoomAanchorNRadioButton
            // 
            ZoomAanchorNRadioButton.AutoSize = true;
            ZoomAanchorNRadioButton.Location = new Point(36, 3);
            ZoomAanchorNRadioButton.Margin = new Padding(4, 3, 4, 3);
            ZoomAanchorNRadioButton.Name = "ZoomAanchorNRadioButton";
            ZoomAanchorNRadioButton.Size = new Size(14, 13);
            ZoomAanchorNRadioButton.TabIndex = 1;
            ZoomAanchorNRadioButton.TabStop = true;
            ZoomAanchorNRadioButton.UseVisualStyleBackColor = true;
            ZoomAanchorNRadioButton.CheckedChanged += OptionChanged_Handler;
            // 
            // ZoomAanchorNERadioButton
            // 
            ZoomAanchorNERadioButton.AutoSize = true;
            ZoomAanchorNERadioButton.Location = new Point(69, 3);
            ZoomAanchorNERadioButton.Margin = new Padding(4, 3, 4, 3);
            ZoomAanchorNERadioButton.Name = "ZoomAanchorNERadioButton";
            ZoomAanchorNERadioButton.Size = new Size(14, 13);
            ZoomAanchorNERadioButton.TabIndex = 2;
            ZoomAanchorNERadioButton.TabStop = true;
            ZoomAanchorNERadioButton.UseVisualStyleBackColor = true;
            ZoomAanchorNERadioButton.CheckedChanged += OptionChanged_Handler;
            // 
            // ZoomAanchorWRadioButton
            // 
            ZoomAanchorWRadioButton.AutoSize = true;
            ZoomAanchorWRadioButton.Location = new Point(4, 33);
            ZoomAanchorWRadioButton.Margin = new Padding(4, 3, 4, 3);
            ZoomAanchorWRadioButton.Name = "ZoomAanchorWRadioButton";
            ZoomAanchorWRadioButton.Size = new Size(14, 13);
            ZoomAanchorWRadioButton.TabIndex = 3;
            ZoomAanchorWRadioButton.TabStop = true;
            ZoomAanchorWRadioButton.UseVisualStyleBackColor = true;
            ZoomAanchorWRadioButton.CheckedChanged += OptionChanged_Handler;
            // 
            // ZoomAanchorSERadioButton
            // 
            ZoomAanchorSERadioButton.AutoSize = true;
            ZoomAanchorSERadioButton.Location = new Point(69, 63);
            ZoomAanchorSERadioButton.Margin = new Padding(4, 3, 4, 3);
            ZoomAanchorSERadioButton.Name = "ZoomAanchorSERadioButton";
            ZoomAanchorSERadioButton.Size = new Size(14, 13);
            ZoomAanchorSERadioButton.TabIndex = 8;
            ZoomAanchorSERadioButton.TabStop = true;
            ZoomAanchorSERadioButton.UseVisualStyleBackColor = true;
            ZoomAanchorSERadioButton.CheckedChanged += OptionChanged_Handler;
            // 
            // ZoomAanchorCRadioButton
            // 
            ZoomAanchorCRadioButton.AutoSize = true;
            ZoomAanchorCRadioButton.Location = new Point(36, 33);
            ZoomAanchorCRadioButton.Margin = new Padding(4, 3, 4, 3);
            ZoomAanchorCRadioButton.Name = "ZoomAanchorCRadioButton";
            ZoomAanchorCRadioButton.Size = new Size(14, 13);
            ZoomAanchorCRadioButton.TabIndex = 4;
            ZoomAanchorCRadioButton.TabStop = true;
            ZoomAanchorCRadioButton.UseVisualStyleBackColor = true;
            ZoomAanchorCRadioButton.CheckedChanged += OptionChanged_Handler;
            // 
            // ZoomAanchorSRadioButton
            // 
            ZoomAanchorSRadioButton.AutoSize = true;
            ZoomAanchorSRadioButton.Location = new Point(36, 63);
            ZoomAanchorSRadioButton.Margin = new Padding(4, 3, 4, 3);
            ZoomAanchorSRadioButton.Name = "ZoomAanchorSRadioButton";
            ZoomAanchorSRadioButton.Size = new Size(14, 13);
            ZoomAanchorSRadioButton.TabIndex = 7;
            ZoomAanchorSRadioButton.TabStop = true;
            ZoomAanchorSRadioButton.UseVisualStyleBackColor = true;
            ZoomAanchorSRadioButton.CheckedChanged += OptionChanged_Handler;
            // 
            // ZoomAanchorERadioButton
            // 
            ZoomAanchorERadioButton.AutoSize = true;
            ZoomAanchorERadioButton.Location = new Point(69, 33);
            ZoomAanchorERadioButton.Margin = new Padding(4, 3, 4, 3);
            ZoomAanchorERadioButton.Name = "ZoomAanchorERadioButton";
            ZoomAanchorERadioButton.Size = new Size(14, 13);
            ZoomAanchorERadioButton.TabIndex = 5;
            ZoomAanchorERadioButton.TabStop = true;
            ZoomAanchorERadioButton.UseVisualStyleBackColor = true;
            ZoomAanchorERadioButton.CheckedChanged += OptionChanged_Handler;
            // 
            // ZoomAanchorSWRadioButton
            // 
            ZoomAanchorSWRadioButton.AutoSize = true;
            ZoomAanchorSWRadioButton.Location = new Point(4, 63);
            ZoomAanchorSWRadioButton.Margin = new Padding(4, 3, 4, 3);
            ZoomAanchorSWRadioButton.Name = "ZoomAanchorSWRadioButton";
            ZoomAanchorSWRadioButton.Size = new Size(14, 13);
            ZoomAanchorSWRadioButton.TabIndex = 6;
            ZoomAanchorSWRadioButton.TabStop = true;
            ZoomAanchorSWRadioButton.UseVisualStyleBackColor = true;
            ZoomAanchorSWRadioButton.CheckedChanged += OptionChanged_Handler;
            // 
            // ZoomAnchorLabel
            // 
            ZoomAnchorLabel.AutoSize = true;
            ZoomAnchorLabel.Location = new Point(9, 66);
            ZoomAnchorLabel.Margin = new Padding(4, 0, 4, 0);
            ZoomAnchorLabel.Name = "ZoomAnchorLabel";
            ZoomAnchorLabel.Size = new Size(46, 15);
            ZoomAnchorLabel.TabIndex = 40;
            ZoomAnchorLabel.Text = "Anchor";
            // 
            // EnableThumbnailZoomCheckBox
            // 
            EnableThumbnailZoomCheckBox.AutoSize = true;
            EnableThumbnailZoomCheckBox.Checked = true;
            EnableThumbnailZoomCheckBox.CheckState = CheckState.Checked;
            EnableThumbnailZoomCheckBox.Location = new Point(9, 8);
            EnableThumbnailZoomCheckBox.Margin = new Padding(4, 3, 4, 3);
            EnableThumbnailZoomCheckBox.Name = "EnableThumbnailZoomCheckBox";
            EnableThumbnailZoomCheckBox.RightToLeft = RightToLeft.No;
            EnableThumbnailZoomCheckBox.Size = new Size(108, 19);
            EnableThumbnailZoomCheckBox.TabIndex = 36;
            EnableThumbnailZoomCheckBox.Text = "Zoom on hover";
            EnableThumbnailZoomCheckBox.UseVisualStyleBackColor = true;
            EnableThumbnailZoomCheckBox.CheckedChanged += OptionChanged_Handler;
            // 
            // ThumbnailZoomFactorNumericEdit
            // 
            ThumbnailZoomFactorNumericEdit.BackColor = SystemColors.Window;
            ThumbnailZoomFactorNumericEdit.BorderStyle = BorderStyle.FixedSingle;
            ThumbnailZoomFactorNumericEdit.Location = new Point(94, 36);
            ThumbnailZoomFactorNumericEdit.Margin = new Padding(4, 3, 4, 3);
            ThumbnailZoomFactorNumericEdit.Maximum = new decimal(new int[] { 10, 0, 0, 0 });
            ThumbnailZoomFactorNumericEdit.Minimum = new decimal(new int[] { 2, 0, 0, 0 });
            ThumbnailZoomFactorNumericEdit.Name = "ThumbnailZoomFactorNumericEdit";
            ThumbnailZoomFactorNumericEdit.Size = new Size(44, 23);
            ThumbnailZoomFactorNumericEdit.TabIndex = 37;
            ThumbnailZoomFactorNumericEdit.Value = new decimal(new int[] { 2, 0, 0, 0 });
            ThumbnailZoomFactorNumericEdit.ValueChanged += OptionChanged_Handler;
            // 
            // OverlayTabPage
            // 
            OverlayTabPage.BackColor = SystemColors.Control;
            OverlayTabPage.Controls.Add(OverlaySettingsPanel);
            OverlayTabPage.Location = new Point(124, 4);
            OverlayTabPage.Margin = new Padding(4, 3, 4, 3);
            OverlayTabPage.Name = "OverlayTabPage";
            OverlayTabPage.Size = new Size(332, 409);
            OverlayTabPage.TabIndex = 3;
            OverlayTabPage.Text = "Overlay";
            // 
            // OverlaySettingsPanel
            // 
            OverlaySettingsPanel.BorderStyle = BorderStyle.FixedSingle;
            OverlaySettingsPanel.Controls.Add(groupBoxOverlayFont);
            OverlaySettingsPanel.Controls.Add(HighlightColorLabel);
            OverlaySettingsPanel.Controls.Add(ActiveClientHighlightColorButton);
            OverlaySettingsPanel.Controls.Add(EnableActiveClientHighlightCheckBox);
            OverlaySettingsPanel.Controls.Add(ShowThumbnailOverlaysCheckBox);
            OverlaySettingsPanel.Controls.Add(ShowThumbnailFramesCheckBox);
            OverlaySettingsPanel.Dock = DockStyle.Fill;
            OverlaySettingsPanel.Location = new Point(0, 0);
            OverlaySettingsPanel.Margin = new Padding(4, 3, 4, 3);
            OverlaySettingsPanel.Name = "OverlaySettingsPanel";
            OverlaySettingsPanel.Size = new Size(332, 409);
            OverlaySettingsPanel.TabIndex = 25;
            // 
            // groupBoxOverlayFont
            // 
            groupBoxOverlayFont.Controls.Add(lblTitleOffsetTop);
            groupBoxOverlayFont.Controls.Add(txtTitleOffsetTop);
            groupBoxOverlayFont.Controls.Add(lblTitleOffsetLeft);
            groupBoxOverlayFont.Controls.Add(txtTitleOffsetLeft);
            groupBoxOverlayFont.Controls.Add(lblTitleBorderWidth);
            groupBoxOverlayFont.Controls.Add(txtFontOutlineWidth);
            groupBoxOverlayFont.Controls.Add(btnFontOutlineColor);
            groupBoxOverlayFont.Controls.Add(btnSetOverlayFontColor);
            groupBoxOverlayFont.Controls.Add(lblDisplaySampleFont);
            groupBoxOverlayFont.Controls.Add(btnSetOverlayFont);
            groupBoxOverlayFont.Location = new Point(9, 129);
            groupBoxOverlayFont.Margin = new Padding(4, 3, 4, 3);
            groupBoxOverlayFont.Name = "groupBoxOverlayFont";
            groupBoxOverlayFont.Padding = new Padding(4, 3, 4, 3);
            groupBoxOverlayFont.Size = new Size(286, 152);
            groupBoxOverlayFont.TabIndex = 33;
            groupBoxOverlayFont.TabStop = false;
            groupBoxOverlayFont.Text = "Title Font";
            // 
            // lblTitleOffsetTop
            // 
            lblTitleOffsetTop.AutoSize = true;
            lblTitleOffsetTop.Location = new Point(122, 59);
            lblTitleOffsetTop.Margin = new Padding(4, 0, 4, 0);
            lblTitleOffsetTop.Name = "lblTitleOffsetTop";
            lblTitleOffsetTop.Size = new Size(27, 15);
            lblTitleOffsetTop.TabIndex = 39;
            lblTitleOffsetTop.Text = "Top";
            // 
            // txtTitleOffsetTop
            // 
            txtTitleOffsetTop.Location = new Point(160, 55);
            txtTitleOffsetTop.Margin = new Padding(4, 3, 4, 3);
            txtTitleOffsetTop.Name = "txtTitleOffsetTop";
            txtTitleOffsetTop.Size = new Size(30, 23);
            txtTitleOffsetTop.TabIndex = 38;
            txtTitleOffsetTop.Text = "1";
            txtTitleOffsetTop.Leave += txtTitleOffsetTop_Leave;
            // 
            // lblTitleOffsetLeft
            // 
            lblTitleOffsetLeft.AutoSize = true;
            lblTitleOffsetLeft.Location = new Point(13, 59);
            lblTitleOffsetLeft.Margin = new Padding(4, 0, 4, 0);
            lblTitleOffsetLeft.Name = "lblTitleOffsetLeft";
            lblTitleOffsetLeft.Size = new Size(62, 15);
            lblTitleOffsetLeft.TabIndex = 37;
            lblTitleOffsetLeft.Text = "Offset Left";
            // 
            // txtTitleOffsetLeft
            // 
            txtTitleOffsetLeft.Location = new Point(85, 55);
            txtTitleOffsetLeft.Margin = new Padding(4, 3, 4, 3);
            txtTitleOffsetLeft.Name = "txtTitleOffsetLeft";
            txtTitleOffsetLeft.Size = new Size(30, 23);
            txtTitleOffsetLeft.TabIndex = 36;
            txtTitleOffsetLeft.Text = "1";
            txtTitleOffsetLeft.Leave += txtTitleOffsetLeft_Leave;
            // 
            // lblTitleBorderWidth
            // 
            lblTitleBorderWidth.AutoSize = true;
            lblTitleBorderWidth.Location = new Point(197, 59);
            lblTitleBorderWidth.Margin = new Padding(4, 0, 4, 0);
            lblTitleBorderWidth.Name = "lblTitleBorderWidth";
            lblTitleBorderWidth.Size = new Size(46, 15);
            lblTitleBorderWidth.TabIndex = 35;
            lblTitleBorderWidth.Text = "Outline";
            // 
            // txtFontOutlineWidth
            // 
            txtFontOutlineWidth.Location = new Point(251, 55);
            txtFontOutlineWidth.Margin = new Padding(4, 3, 4, 3);
            txtFontOutlineWidth.Name = "txtFontOutlineWidth";
            txtFontOutlineWidth.Size = new Size(30, 23);
            txtFontOutlineWidth.TabIndex = 34;
            txtFontOutlineWidth.Text = "0";
            txtFontOutlineWidth.Leave += txtFontOutlineWidth_Leave;
            // 
            // btnFontOutlineColor
            // 
            btnFontOutlineColor.Location = new Point(190, 22);
            btnFontOutlineColor.Margin = new Padding(4, 3, 4, 3);
            btnFontOutlineColor.Name = "btnFontOutlineColor";
            btnFontOutlineColor.Size = new Size(89, 27);
            btnFontOutlineColor.TabIndex = 33;
            btnFontOutlineColor.Text = "Outline Color";
            btnFontOutlineColor.UseVisualStyleBackColor = true;
            btnFontOutlineColor.Click += btnFontOutlineColor_Click;
            // 
            // btnSetOverlayFontColor
            // 
            btnSetOverlayFontColor.Location = new Point(98, 22);
            btnSetOverlayFontColor.Margin = new Padding(4, 3, 4, 3);
            btnSetOverlayFontColor.Name = "btnSetOverlayFontColor";
            btnSetOverlayFontColor.Size = new Size(69, 27);
            btnSetOverlayFontColor.TabIndex = 31;
            btnSetOverlayFontColor.Text = "Forecolor";
            btnSetOverlayFontColor.UseVisualStyleBackColor = true;
            btnSetOverlayFontColor.Click += btnSetOverlayFontColor_Click;
            // 
            // lblDisplaySampleFont
            // 
            lblDisplaySampleFont.AutoSize = true;
            lblDisplaySampleFont.BackColor = SystemColors.Control;
            lblDisplaySampleFont.ForeColor = Color.FromArgb(255, 128, 0);
            lblDisplaySampleFont.Location = new Point(7, 88);
            lblDisplaySampleFont.Margin = new Padding(4, 0, 4, 0);
            lblDisplaySampleFont.Name = "lblDisplaySampleFont";
            lblDisplaySampleFont.Size = new Size(49, 15);
            lblDisplaySampleFont.TabIndex = 32;
            lblDisplaySampleFont.Text = "Sample.";
            // 
            // btnSetOverlayFont
            // 
            btnSetOverlayFont.Location = new Point(7, 22);
            btnSetOverlayFont.Margin = new Padding(4, 3, 4, 3);
            btnSetOverlayFont.Name = "btnSetOverlayFont";
            btnSetOverlayFont.Size = new Size(65, 27);
            btnSetOverlayFont.TabIndex = 30;
            btnSetOverlayFont.Text = "Set Font";
            btnSetOverlayFont.UseVisualStyleBackColor = true;
            btnSetOverlayFont.Click += btnSetOverlayFont_Click;
            // 
            // HighlightColorLabel
            // 
            HighlightColorLabel.AutoSize = true;
            HighlightColorLabel.Location = new Point(6, 90);
            HighlightColorLabel.Margin = new Padding(4, 0, 4, 0);
            HighlightColorLabel.Name = "HighlightColorLabel";
            HighlightColorLabel.Size = new Size(36, 15);
            HighlightColorLabel.TabIndex = 29;
            HighlightColorLabel.Text = "Color";
            // 
            // ActiveClientHighlightColorButton
            // 
            ActiveClientHighlightColorButton.BorderStyle = BorderStyle.FixedSingle;
            ActiveClientHighlightColorButton.Location = new Point(49, 89);
            ActiveClientHighlightColorButton.Margin = new Padding(4, 3, 4, 3);
            ActiveClientHighlightColorButton.Name = "ActiveClientHighlightColorButton";
            ActiveClientHighlightColorButton.Size = new Size(108, 19);
            ActiveClientHighlightColorButton.TabIndex = 28;
            ActiveClientHighlightColorButton.Click += ActiveClientHighlightColorButton_Click;
            // 
            // EnableActiveClientHighlightCheckBox
            // 
            EnableActiveClientHighlightCheckBox.AutoSize = true;
            EnableActiveClientHighlightCheckBox.Checked = true;
            EnableActiveClientHighlightCheckBox.CheckState = CheckState.Checked;
            EnableActiveClientHighlightCheckBox.Location = new Point(9, 63);
            EnableActiveClientHighlightCheckBox.Margin = new Padding(4, 3, 4, 3);
            EnableActiveClientHighlightCheckBox.Name = "EnableActiveClientHighlightCheckBox";
            EnableActiveClientHighlightCheckBox.RightToLeft = RightToLeft.No;
            EnableActiveClientHighlightCheckBox.Size = new Size(142, 19);
            EnableActiveClientHighlightCheckBox.TabIndex = 27;
            EnableActiveClientHighlightCheckBox.Text = "Highlight active client";
            EnableActiveClientHighlightCheckBox.UseVisualStyleBackColor = true;
            EnableActiveClientHighlightCheckBox.CheckedChanged += OptionChanged_Handler;
            // 
            // ShowThumbnailOverlaysCheckBox
            // 
            ShowThumbnailOverlaysCheckBox.AutoSize = true;
            ShowThumbnailOverlaysCheckBox.Checked = true;
            ShowThumbnailOverlaysCheckBox.CheckState = CheckState.Checked;
            ShowThumbnailOverlaysCheckBox.Location = new Point(9, 8);
            ShowThumbnailOverlaysCheckBox.Margin = new Padding(4, 3, 4, 3);
            ShowThumbnailOverlaysCheckBox.Name = "ShowThumbnailOverlaysCheckBox";
            ShowThumbnailOverlaysCheckBox.RightToLeft = RightToLeft.No;
            ShowThumbnailOverlaysCheckBox.Size = new Size(96, 19);
            ShowThumbnailOverlaysCheckBox.TabIndex = 25;
            ShowThumbnailOverlaysCheckBox.Text = "Show overlay";
            ShowThumbnailOverlaysCheckBox.UseVisualStyleBackColor = true;
            ShowThumbnailOverlaysCheckBox.CheckedChanged += OptionChanged_Handler;
            // 
            // ShowThumbnailFramesCheckBox
            // 
            ShowThumbnailFramesCheckBox.AutoSize = true;
            ShowThumbnailFramesCheckBox.Checked = true;
            ShowThumbnailFramesCheckBox.CheckState = CheckState.Checked;
            ShowThumbnailFramesCheckBox.Location = new Point(9, 36);
            ShowThumbnailFramesCheckBox.Margin = new Padding(4, 3, 4, 3);
            ShowThumbnailFramesCheckBox.Name = "ShowThumbnailFramesCheckBox";
            ShowThumbnailFramesCheckBox.RightToLeft = RightToLeft.No;
            ShowThumbnailFramesCheckBox.Size = new Size(94, 19);
            ShowThumbnailFramesCheckBox.TabIndex = 26;
            ShowThumbnailFramesCheckBox.Text = "Show frames";
            ShowThumbnailFramesCheckBox.UseVisualStyleBackColor = true;
            ShowThumbnailFramesCheckBox.CheckedChanged += OptionChanged_Handler;
            // 
            // ClientsPanel
            // 
            ClientsPanel.BorderStyle = BorderStyle.FixedSingle;
            ClientsPanel.Controls.Add(activeClientsSplitContainer);
            ClientsPanel.Dock = DockStyle.Fill;
            ClientsPanel.Location = new Point(0, 0);
            ClientsPanel.Margin = new Padding(4, 3, 4, 3);
            ClientsPanel.Name = "ClientsPanel";
            ClientsPanel.Size = new Size(332, 409);
            ClientsPanel.TabIndex = 32;
            // 
            // activeClientsSplitContainer
            // 
            activeClientsSplitContainer.Dock = DockStyle.Fill;
            activeClientsSplitContainer.FixedPanel = FixedPanel.Panel1;
            activeClientsSplitContainer.Location = new Point(0, 0);
            activeClientsSplitContainer.Margin = new Padding(4, 3, 4, 3);
            activeClientsSplitContainer.Name = "activeClientsSplitContainer";
            activeClientsSplitContainer.Orientation = Orientation.Horizontal;
            // 
            // activeClientsSplitContainer.Panel1
            // 
            activeClientsSplitContainer.Panel1.Controls.Add(groupBoxToggleHideAllThumbnails);
            activeClientsSplitContainer.Panel1.Controls.Add(ThumbnailsListLabel);
            // 
            // activeClientsSplitContainer.Panel2
            // 
            activeClientsSplitContainer.Panel2.Controls.Add(ThumbnailsList);
            activeClientsSplitContainer.Size = new Size(330, 407);
            activeClientsSplitContainer.SplitterDistance = 134;
            activeClientsSplitContainer.SplitterWidth = 5;
            activeClientsSplitContainer.TabIndex = 35;
            // 
            // groupBoxToggleHideAllThumbnails
            // 
            groupBoxToggleHideAllThumbnails.Controls.Add(btnMinimizeAllClients);
            groupBoxToggleHideAllThumbnails.Controls.Add(lblMinimizeAllClientsHotkey);
            groupBoxToggleHideAllThumbnails.Controls.Add(txtMinimizeAllClientsHotkey);
            groupBoxToggleHideAllThumbnails.Controls.Add(btnToggleHideAll);
            groupBoxToggleHideAllThumbnails.Controls.Add(lblToggleHideAllActiveHotkey);
            groupBoxToggleHideAllThumbnails.Controls.Add(txtToggleHideAllActiveHotkey);
            groupBoxToggleHideAllThumbnails.Location = new Point(4, 3);
            groupBoxToggleHideAllThumbnails.Margin = new Padding(4, 3, 4, 3);
            groupBoxToggleHideAllThumbnails.Name = "groupBoxToggleHideAllThumbnails";
            groupBoxToggleHideAllThumbnails.Padding = new Padding(4, 3, 4, 3);
            groupBoxToggleHideAllThumbnails.Size = new Size(292, 103);
            groupBoxToggleHideAllThumbnails.TabIndex = 37;
            groupBoxToggleHideAllThumbnails.TabStop = false;
            groupBoxToggleHideAllThumbnails.Text = "All Clients";
            // 
            // btnMinimizeAllClients
            // 
            btnMinimizeAllClients.Location = new Point(7, 59);
            btnMinimizeAllClients.Margin = new Padding(4, 3, 4, 3);
            btnMinimizeAllClients.Name = "btnMinimizeAllClients";
            btnMinimizeAllClients.Size = new Size(112, 30);
            btnMinimizeAllClients.TabIndex = 39;
            btnMinimizeAllClients.Text = "Minimize";
            btnMinimizeAllClients.UseVisualStyleBackColor = true;
            btnMinimizeAllClients.Click += btnMinimizeAllClients_Click;
            // 
            // lblMinimizeAllClientsHotkey
            // 
            lblMinimizeAllClientsHotkey.AutoSize = true;
            lblMinimizeAllClientsHotkey.Location = new Point(124, 67);
            lblMinimizeAllClientsHotkey.Margin = new Padding(4, 0, 4, 0);
            lblMinimizeAllClientsHotkey.Name = "lblMinimizeAllClientsHotkey";
            lblMinimizeAllClientsHotkey.Size = new Size(45, 15);
            lblMinimizeAllClientsHotkey.TabIndex = 37;
            lblMinimizeAllClientsHotkey.Text = "Hotkey";
            // 
            // txtMinimizeAllClientsHotkey
            // 
            txtMinimizeAllClientsHotkey.BackColor = SystemColors.Control;
            txtMinimizeAllClientsHotkey.Location = new Point(170, 62);
            txtMinimizeAllClientsHotkey.Margin = new Padding(4, 3, 4, 3);
            txtMinimizeAllClientsHotkey.Name = "txtMinimizeAllClientsHotkey";
            txtMinimizeAllClientsHotkey.ReadOnly = true;
            txtMinimizeAllClientsHotkey.Size = new Size(109, 23);
            txtMinimizeAllClientsHotkey.TabIndex = 38;
            instantToolTip.SetToolTip(txtMinimizeAllClientsHotkey, "Double click to set");
            txtMinimizeAllClientsHotkey.DoubleClick += txtMinimizeAllClientsHotkey_DoubleClick;
            // 
            // btnToggleHideAll
            // 
            btnToggleHideAll.Location = new Point(7, 22);
            btnToggleHideAll.Margin = new Padding(4, 3, 4, 3);
            btnToggleHideAll.Name = "btnToggleHideAll";
            btnToggleHideAll.Size = new Size(112, 30);
            btnToggleHideAll.TabIndex = 36;
            btnToggleHideAll.Text = "Hide Thumbnails";
            btnToggleHideAll.UseVisualStyleBackColor = true;
            btnToggleHideAll.Click += btnToggleHideAll_Click;
            // 
            // lblToggleHideAllActiveHotkey
            // 
            lblToggleHideAllActiveHotkey.AutoSize = true;
            lblToggleHideAllActiveHotkey.Location = new Point(124, 30);
            lblToggleHideAllActiveHotkey.Margin = new Padding(4, 0, 4, 0);
            lblToggleHideAllActiveHotkey.Name = "lblToggleHideAllActiveHotkey";
            lblToggleHideAllActiveHotkey.Size = new Size(45, 15);
            lblToggleHideAllActiveHotkey.TabIndex = 34;
            lblToggleHideAllActiveHotkey.Text = "Hotkey";
            // 
            // txtToggleHideAllActiveHotkey
            // 
            txtToggleHideAllActiveHotkey.BackColor = SystemColors.Control;
            txtToggleHideAllActiveHotkey.Location = new Point(170, 25);
            txtToggleHideAllActiveHotkey.Margin = new Padding(4, 3, 4, 3);
            txtToggleHideAllActiveHotkey.Name = "txtToggleHideAllActiveHotkey";
            txtToggleHideAllActiveHotkey.ReadOnly = true;
            txtToggleHideAllActiveHotkey.Size = new Size(109, 23);
            txtToggleHideAllActiveHotkey.TabIndex = 35;
            instantToolTip.SetToolTip(txtToggleHideAllActiveHotkey, "Double click to set");
            txtToggleHideAllActiveHotkey.DoubleClick += txtToggleHideAllActiveHotkey_DoubleClick;
            // 
            // ThumbnailsListLabel
            // 
            ThumbnailsListLabel.AutoSize = true;
            ThumbnailsListLabel.Location = new Point(2, 112);
            ThumbnailsListLabel.Margin = new Padding(4, 0, 4, 0);
            ThumbnailsListLabel.Name = "ThumbnailsListLabel";
            ThumbnailsListLabel.Size = new Size(182, 15);
            ThumbnailsListLabel.TabIndex = 33;
            ThumbnailsListLabel.Text = "Thumbnails (check to force hide)";
            // 
            // ThumbnailsList
            // 
            ThumbnailsList.BackColor = SystemColors.Window;
            ThumbnailsList.BorderStyle = BorderStyle.FixedSingle;
            ThumbnailsList.CheckOnClick = true;
            ThumbnailsList.Dock = DockStyle.Fill;
            ThumbnailsList.FormattingEnabled = true;
            ThumbnailsList.IntegralHeight = false;
            ThumbnailsList.Location = new Point(0, 0);
            ThumbnailsList.Margin = new Padding(4, 3, 4, 3);
            ThumbnailsList.Name = "ThumbnailsList";
            ThumbnailsList.Size = new Size(330, 268);
            ThumbnailsList.TabIndex = 34;
            ThumbnailsList.ItemCheck += ThumbnailsList_ItemCheck_Handler;
            // 
            // CycleGroupTabPage
            // 
            CycleGroupTabPage.Controls.Add(CycleGroupPanel);
            CycleGroupTabPage.Location = new Point(124, 4);
            CycleGroupTabPage.Margin = new Padding(4, 3, 4, 3);
            CycleGroupTabPage.Name = "CycleGroupTabPage";
            CycleGroupTabPage.Size = new Size(332, 409);
            CycleGroupTabPage.TabIndex = 6;
            CycleGroupTabPage.Text = "Cycle Groups";
            // 
            // CycleGroupPanel
            // 
            CycleGroupPanel.Controls.Add(splitContainerMainCycleGroup);
            CycleGroupPanel.Dock = DockStyle.Fill;
            CycleGroupPanel.Location = new Point(0, 0);
            CycleGroupPanel.Margin = new Padding(4, 3, 4, 3);
            CycleGroupPanel.Name = "CycleGroupPanel";
            CycleGroupPanel.Size = new Size(332, 409);
            CycleGroupPanel.TabIndex = 0;
            // 
            // splitContainerMainCycleGroup
            // 
            splitContainerMainCycleGroup.Dock = DockStyle.Fill;
            splitContainerMainCycleGroup.FixedPanel = FixedPanel.Panel1;
            splitContainerMainCycleGroup.Location = new Point(0, 0);
            splitContainerMainCycleGroup.Margin = new Padding(4, 3, 4, 3);
            splitContainerMainCycleGroup.Name = "splitContainerMainCycleGroup";
            splitContainerMainCycleGroup.Orientation = Orientation.Horizontal;
            // 
            // splitContainerMainCycleGroup.Panel1
            // 
            splitContainerMainCycleGroup.Panel1.Controls.Add(CycleGroupLabel);
            splitContainerMainCycleGroup.Panel1.Controls.Add(removeGroupButton);
            splitContainerMainCycleGroup.Panel1.Controls.Add(addClientToCycleGroupButton);
            splitContainerMainCycleGroup.Panel1.Controls.Add(cycleGroupMoveClientOrderUpButton);
            splitContainerMainCycleGroup.Panel1.Controls.Add(selectCycleGroupComboBox);
            splitContainerMainCycleGroup.Panel1.Controls.Add(removeClientToCycleGroupButton);
            splitContainerMainCycleGroup.Panel1.Controls.Add(cycleGroupDescriptionLabel);
            splitContainerMainCycleGroup.Panel1.Controls.Add(addNewGroupButton);
            splitContainerMainCycleGroup.Panel1.Controls.Add(cycleGroupDescriptionText);
            splitContainerMainCycleGroup.Panel1.Controls.Add(cycleGroupClientOrderLabel);
            splitContainerMainCycleGroup.Panel1.Controls.Add(cycleGroupForwardHotkeyLabel);
            splitContainerMainCycleGroup.Panel1.Controls.Add(cycleGroupForwardHotkey1Text);
            splitContainerMainCycleGroup.Panel1.Controls.Add(cycleGroupBackwardHotkey2Text);
            splitContainerMainCycleGroup.Panel1.Controls.Add(cycleGroupForwardHotkey2Text);
            splitContainerMainCycleGroup.Panel1.Controls.Add(cycleGroupBackwardHotkey1Text);
            splitContainerMainCycleGroup.Panel1.Controls.Add(cycleGroupBackHotkeyLabel);
            // 
            // splitContainerMainCycleGroup.Panel2
            // 
            splitContainerMainCycleGroup.Panel2.Controls.Add(cycleGroupClientOrderList);
            splitContainerMainCycleGroup.Size = new Size(332, 409);
            splitContainerMainCycleGroup.SplitterDistance = 185;
            splitContainerMainCycleGroup.SplitterWidth = 5;
            splitContainerMainCycleGroup.TabIndex = 17;
            // 
            // CycleGroupLabel
            // 
            CycleGroupLabel.AutoSize = true;
            CycleGroupLabel.Location = new Point(15, 12);
            CycleGroupLabel.Margin = new Padding(4, 0, 4, 0);
            CycleGroupLabel.Name = "CycleGroupLabel";
            CycleGroupLabel.Size = new Size(109, 15);
            CycleGroupLabel.TabIndex = 2;
            CycleGroupLabel.Text = "Select Cycle Group:";
            // 
            // removeGroupButton
            // 
            removeGroupButton.Font = new Font("Microsoft Sans Serif", 8.25F, FontStyle.Bold, GraphicsUnit.Point, 0);
            removeGroupButton.Location = new Point(245, 29);
            removeGroupButton.Margin = new Padding(4, 3, 4, 3);
            removeGroupButton.Name = "removeGroupButton";
            removeGroupButton.Size = new Size(28, 27);
            removeGroupButton.TabIndex = 16;
            removeGroupButton.Text = "-";
            removeGroupButton.UseVisualStyleBackColor = true;
            removeGroupButton.Click += removeGroupButton_Click;
            // 
            // addClientToCycleGroupButton
            // 
            addClientToCycleGroupButton.Font = new Font("Microsoft Sans Serif", 8.25F, FontStyle.Bold, GraphicsUnit.Point, 0);
            addClientToCycleGroupButton.Location = new Point(272, 152);
            addClientToCycleGroupButton.Margin = new Padding(4, 3, 4, 3);
            addClientToCycleGroupButton.Name = "addClientToCycleGroupButton";
            addClientToCycleGroupButton.Size = new Size(26, 27);
            addClientToCycleGroupButton.TabIndex = 0;
            addClientToCycleGroupButton.Text = "+";
            addClientToCycleGroupButton.UseVisualStyleBackColor = true;
            addClientToCycleGroupButton.Click += addClientToCycleGroupButton_Click;
            // 
            // cycleGroupMoveClientOrderUpButton
            // 
            cycleGroupMoveClientOrderUpButton.Font = new Font("Microsoft Sans Serif", 8.25F, FontStyle.Bold, GraphicsUnit.Point, 0);
            cycleGroupMoveClientOrderUpButton.Location = new Point(204, 152);
            cycleGroupMoveClientOrderUpButton.Margin = new Padding(4, 3, 4, 3);
            cycleGroupMoveClientOrderUpButton.Name = "cycleGroupMoveClientOrderUpButton";
            cycleGroupMoveClientOrderUpButton.Size = new Size(38, 27);
            cycleGroupMoveClientOrderUpButton.TabIndex = 15;
            cycleGroupMoveClientOrderUpButton.Text = "Up";
            cycleGroupMoveClientOrderUpButton.UseVisualStyleBackColor = true;
            cycleGroupMoveClientOrderUpButton.Click += cycleGroupMoveClientOrderUpButton_Click;
            // 
            // selectCycleGroupComboBox
            // 
            selectCycleGroupComboBox.FormattingEnabled = true;
            selectCycleGroupComboBox.Location = new Point(19, 30);
            selectCycleGroupComboBox.Margin = new Padding(4, 3, 4, 3);
            selectCycleGroupComboBox.Name = "selectCycleGroupComboBox";
            selectCycleGroupComboBox.Size = new Size(223, 23);
            selectCycleGroupComboBox.TabIndex = 1;
            selectCycleGroupComboBox.SelectedValueChanged += selectCycleGroupComboBox_SelectedValueChanged;
            // 
            // removeClientToCycleGroupButton
            // 
            removeClientToCycleGroupButton.Font = new Font("Microsoft Sans Serif", 8.25F, FontStyle.Bold, GraphicsUnit.Point, 0);
            removeClientToCycleGroupButton.Location = new Point(245, 152);
            removeClientToCycleGroupButton.Margin = new Padding(4, 3, 4, 3);
            removeClientToCycleGroupButton.Name = "removeClientToCycleGroupButton";
            removeClientToCycleGroupButton.Size = new Size(26, 27);
            removeClientToCycleGroupButton.TabIndex = 14;
            removeClientToCycleGroupButton.Text = "-";
            removeClientToCycleGroupButton.UseVisualStyleBackColor = true;
            removeClientToCycleGroupButton.Click += removeClientToCycleGroupButton_Click;
            // 
            // cycleGroupDescriptionLabel
            // 
            cycleGroupDescriptionLabel.AutoSize = true;
            cycleGroupDescriptionLabel.Location = new Point(15, 66);
            cycleGroupDescriptionLabel.Margin = new Padding(4, 0, 4, 0);
            cycleGroupDescriptionLabel.Name = "cycleGroupDescriptionLabel";
            cycleGroupDescriptionLabel.Size = new Size(70, 15);
            cycleGroupDescriptionLabel.TabIndex = 3;
            cycleGroupDescriptionLabel.Text = "Description:";
            // 
            // addNewGroupButton
            // 
            addNewGroupButton.Font = new Font("Microsoft Sans Serif", 8.25F, FontStyle.Bold, GraphicsUnit.Point, 0);
            addNewGroupButton.Location = new Point(272, 29);
            addNewGroupButton.Margin = new Padding(4, 3, 4, 3);
            addNewGroupButton.Name = "addNewGroupButton";
            addNewGroupButton.Size = new Size(28, 27);
            addNewGroupButton.TabIndex = 13;
            addNewGroupButton.Text = "+";
            addNewGroupButton.UseVisualStyleBackColor = true;
            addNewGroupButton.Click += addNewGroupButton_Click;
            // 
            // cycleGroupDescriptionText
            // 
            cycleGroupDescriptionText.Location = new Point(107, 62);
            cycleGroupDescriptionText.Margin = new Padding(4, 3, 4, 3);
            cycleGroupDescriptionText.Name = "cycleGroupDescriptionText";
            cycleGroupDescriptionText.Size = new Size(190, 23);
            cycleGroupDescriptionText.TabIndex = 4;
            cycleGroupDescriptionText.Leave += cycleGroupDescriptionText_Leave;
            // 
            // cycleGroupClientOrderLabel
            // 
            cycleGroupClientOrderLabel.AutoSize = true;
            cycleGroupClientOrderLabel.Location = new Point(15, 158);
            cycleGroupClientOrderLabel.Margin = new Padding(4, 0, 4, 0);
            cycleGroupClientOrderLabel.Name = "cycleGroupClientOrderLabel";
            cycleGroupClientOrderLabel.Size = new Size(102, 15);
            cycleGroupClientOrderLabel.TabIndex = 12;
            cycleGroupClientOrderLabel.Text = "Clients and Order:";
            // 
            // cycleGroupForwardHotkeyLabel
            // 
            cycleGroupForwardHotkeyLabel.AutoSize = true;
            cycleGroupForwardHotkeyLabel.Location = new Point(15, 96);
            cycleGroupForwardHotkeyLabel.Margin = new Padding(4, 0, 4, 0);
            cycleGroupForwardHotkeyLabel.Name = "cycleGroupForwardHotkeyLabel";
            cycleGroupForwardHotkeyLabel.Size = new Size(75, 15);
            cycleGroupForwardHotkeyLabel.TabIndex = 5;
            cycleGroupForwardHotkeyLabel.Text = "Forward Key:";
            // 
            // cycleGroupForwardHotkey1Text
            // 
            cycleGroupForwardHotkey1Text.BackColor = SystemColors.Control;
            cycleGroupForwardHotkey1Text.Location = new Point(107, 92);
            cycleGroupForwardHotkey1Text.Margin = new Padding(4, 3, 4, 3);
            cycleGroupForwardHotkey1Text.Name = "cycleGroupForwardHotkey1Text";
            cycleGroupForwardHotkey1Text.ReadOnly = true;
            cycleGroupForwardHotkey1Text.Size = new Size(93, 23);
            cycleGroupForwardHotkey1Text.TabIndex = 6;
            instantToolTip.SetToolTip(cycleGroupForwardHotkey1Text, "Double click to set");
            cycleGroupForwardHotkey1Text.DoubleClick += cycleGroupForwardHotkey1Text_DoubleClick;
            // 
            // cycleGroupBackwardHotkey2Text
            // 
            cycleGroupBackwardHotkey2Text.BackColor = SystemColors.Control;
            cycleGroupBackwardHotkey2Text.Location = new Point(204, 122);
            cycleGroupBackwardHotkey2Text.Margin = new Padding(4, 3, 4, 3);
            cycleGroupBackwardHotkey2Text.Name = "cycleGroupBackwardHotkey2Text";
            cycleGroupBackwardHotkey2Text.ReadOnly = true;
            cycleGroupBackwardHotkey2Text.Size = new Size(93, 23);
            cycleGroupBackwardHotkey2Text.TabIndex = 10;
            instantToolTip.SetToolTip(cycleGroupBackwardHotkey2Text, "Double click to set");
            cycleGroupBackwardHotkey2Text.DoubleClick += cycleGroupBackwardHotkey2Text_DoubleClick;
            // 
            // cycleGroupForwardHotkey2Text
            // 
            cycleGroupForwardHotkey2Text.BackColor = SystemColors.Control;
            cycleGroupForwardHotkey2Text.Location = new Point(204, 92);
            cycleGroupForwardHotkey2Text.Margin = new Padding(4, 3, 4, 3);
            cycleGroupForwardHotkey2Text.Name = "cycleGroupForwardHotkey2Text";
            cycleGroupForwardHotkey2Text.ReadOnly = true;
            cycleGroupForwardHotkey2Text.Size = new Size(93, 23);
            cycleGroupForwardHotkey2Text.TabIndex = 7;
            instantToolTip.SetToolTip(cycleGroupForwardHotkey2Text, "Double click to set");
            cycleGroupForwardHotkey2Text.DoubleClick += cycleGroupForwardHotkey2Text_DoubleClick;
            // 
            // cycleGroupBackwardHotkey1Text
            // 
            cycleGroupBackwardHotkey1Text.BackColor = SystemColors.Control;
            cycleGroupBackwardHotkey1Text.Location = new Point(107, 122);
            cycleGroupBackwardHotkey1Text.Margin = new Padding(4, 3, 4, 3);
            cycleGroupBackwardHotkey1Text.Name = "cycleGroupBackwardHotkey1Text";
            cycleGroupBackwardHotkey1Text.ReadOnly = true;
            cycleGroupBackwardHotkey1Text.Size = new Size(93, 23);
            cycleGroupBackwardHotkey1Text.TabIndex = 9;
            instantToolTip.SetToolTip(cycleGroupBackwardHotkey1Text, "Double click to set");
            cycleGroupBackwardHotkey1Text.DoubleClick += cycleGroupBackwardHotkey1Text_DoubleClick;
            // 
            // cycleGroupBackHotkeyLabel
            // 
            cycleGroupBackHotkeyLabel.AutoSize = true;
            cycleGroupBackHotkeyLabel.Location = new Point(15, 126);
            cycleGroupBackHotkeyLabel.Margin = new Padding(4, 0, 4, 0);
            cycleGroupBackHotkeyLabel.Name = "cycleGroupBackHotkeyLabel";
            cycleGroupBackHotkeyLabel.Size = new Size(83, 15);
            cycleGroupBackHotkeyLabel.TabIndex = 8;
            cycleGroupBackHotkeyLabel.Text = "Backward Key:";
            // 
            // cycleGroupClientOrderList
            // 
            cycleGroupClientOrderList.Dock = DockStyle.Fill;
            cycleGroupClientOrderList.FormattingEnabled = true;
            cycleGroupClientOrderList.Location = new Point(0, 0);
            cycleGroupClientOrderList.Margin = new Padding(4, 3, 4, 3);
            cycleGroupClientOrderList.Name = "cycleGroupClientOrderList";
            cycleGroupClientOrderList.Size = new Size(332, 219);
            cycleGroupClientOrderList.TabIndex = 11;
            // 
            // label1
            // 
            label1.BackColor = Color.Transparent;
            label1.Location = new Point(-2, -2);
            label1.Margin = new Padding(4, 0, 4, 0);
            label1.Name = "label1";
            label1.Padding = new Padding(9, 3, 9, 3);
            label1.Size = new Size(304, 167);
            label1.TabIndex = 6;
            label1.Text = resources.GetString("label1.Text");
            // 
            // lblFpsPredictiveLimit
            // 
            lblFpsPredictiveLimit.AutoSize = true;
            lblFpsPredictiveLimit.Location = new Point(13, 93);
            lblFpsPredictiveLimit.Margin = new Padding(4, 0, 4, 0);
            lblFpsPredictiveLimit.Name = "lblFpsPredictiveLimit";
            lblFpsPredictiveLimit.Size = new Size(91, 15);
            lblFpsPredictiveLimit.TabIndex = 29;
            lblFpsPredictiveLimit.Text = "Predicted Client";
            // 
            // lblFpsBackgroundLimit
            // 
            lblFpsBackgroundLimit.AutoSize = true;
            lblFpsBackgroundLimit.Location = new Point(13, 58);
            lblFpsBackgroundLimit.Margin = new Padding(4, 0, 4, 0);
            lblFpsBackgroundLimit.Name = "lblFpsBackgroundLimit";
            lblFpsBackgroundLimit.Size = new Size(87, 15);
            lblFpsBackgroundLimit.TabIndex = 27;
            lblFpsBackgroundLimit.Text = "Inactive Clients";
            // 
            // lblFpsForegroundLimit
            // 
            lblFpsForegroundLimit.AutoSize = true;
            lblFpsForegroundLimit.Location = new Point(13, 24);
            lblFpsForegroundLimit.Margin = new Padding(4, 0, 4, 0);
            lblFpsForegroundLimit.Name = "lblFpsForegroundLimit";
            lblFpsForegroundLimit.Size = new Size(74, 15);
            lblFpsForegroundLimit.TabIndex = 25;
            lblFpsForegroundLimit.Text = "Active Client";
            // 
            // AboutTabPage
            // 
            AboutTabPage.BackColor = SystemColors.Control;
            AboutTabPage.Controls.Add(AboutPanel);
            AboutTabPage.Location = new Point(124, 4);
            AboutTabPage.Margin = new Padding(4, 3, 4, 3);
            AboutTabPage.Name = "AboutTabPage";
            AboutTabPage.Size = new Size(332, 409);
            AboutTabPage.TabIndex = 5;
            AboutTabPage.Text = "About";
            // 
            // AboutPanel
            // 
            AboutPanel.BackColor = Color.Transparent;
            AboutPanel.BorderStyle = BorderStyle.FixedSingle;
            AboutPanel.Controls.Add(lblLiabilityDisclaimer);
            AboutPanel.Controls.Add(CreditMaintLabel);
            AboutPanel.Controls.Add(DocumentationLinkLabel);
            AboutPanel.Controls.Add(DescriptionLabel);
            AboutPanel.Controls.Add(VersionLabel);
            AboutPanel.Controls.Add(NameLabel);
            AboutPanel.Controls.Add(DocumentationLink);
            AboutPanel.Dock = DockStyle.Fill;
            AboutPanel.Location = new Point(0, 0);
            AboutPanel.Margin = new Padding(4, 3, 4, 3);
            AboutPanel.Name = "AboutPanel";
            AboutPanel.Size = new Size(332, 409);
            AboutPanel.TabIndex = 2;
            // 
            // lblLiabilityDisclaimer
            // 
            lblLiabilityDisclaimer.BackColor = Color.Transparent;
            lblLiabilityDisclaimer.Location = new Point(6, 33);
            lblLiabilityDisclaimer.Margin = new Padding(4, 0, 4, 0);
            lblLiabilityDisclaimer.Name = "lblLiabilityDisclaimer";
            lblLiabilityDisclaimer.Padding = new Padding(9, 3, 9, 3);
            lblLiabilityDisclaimer.Size = new Size(304, 156);
            lblLiabilityDisclaimer.TabIndex = 9;
            lblLiabilityDisclaimer.Text = resources.GetString("lblLiabilityDisclaimer.Text");
            // 
            // CreditMaintLabel
            // 
            CreditMaintLabel.AutoSize = true;
            CreditMaintLabel.Location = new Point(6, 315);
            CreditMaintLabel.Margin = new Padding(4, 0, 4, 0);
            CreditMaintLabel.Name = "CreditMaintLabel";
            CreditMaintLabel.Padding = new Padding(9, 3, 9, 3);
            CreditMaintLabel.Size = new Size(292, 21);
            CreditMaintLabel.TabIndex = 7;
            CreditMaintLabel.Text = "Credit to previous maintainer: Phrynohyas Tig-Rah";
            // 
            // DocumentationLinkLabel
            // 
            DocumentationLinkLabel.AutoSize = true;
            DocumentationLinkLabel.Location = new Point(6, 337);
            DocumentationLinkLabel.Margin = new Padding(4, 0, 4, 0);
            DocumentationLinkLabel.Name = "DocumentationLinkLabel";
            DocumentationLinkLabel.Padding = new Padding(9, 3, 9, 3);
            DocumentationLinkLabel.Size = new Size(229, 21);
            DocumentationLinkLabel.TabIndex = 6;
            DocumentationLinkLabel.Text = "For more information visit our discord:";
            // 
            // DescriptionLabel
            // 
            DescriptionLabel.BackColor = Color.Transparent;
            DescriptionLabel.Location = new Point(6, 189);
            DescriptionLabel.Margin = new Padding(4, 0, 4, 0);
            DescriptionLabel.Name = "DescriptionLabel";
            DescriptionLabel.Padding = new Padding(9, 3, 9, 3);
            DescriptionLabel.Size = new Size(304, 138);
            DescriptionLabel.TabIndex = 5;
            DescriptionLabel.Text = resources.GetString("DescriptionLabel.Text");
            // 
            // VersionLabel
            // 
            VersionLabel.AutoSize = true;
            VersionLabel.Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Bold, GraphicsUnit.Point, 204);
            VersionLabel.Location = new Point(155, 10);
            VersionLabel.Margin = new Padding(4, 0, 4, 0);
            VersionLabel.Name = "VersionLabel";
            VersionLabel.Size = new Size(49, 20);
            VersionLabel.TabIndex = 4;
            VersionLabel.Text = "1.0.0";
            // 
            // NameLabel
            // 
            NameLabel.AutoSize = true;
            NameLabel.Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Bold, GraphicsUnit.Point, 204);
            NameLabel.Location = new Point(5, 10);
            NameLabel.Margin = new Padding(4, 0, 4, 0);
            NameLabel.Name = "NameLabel";
            NameLabel.Size = new Size(130, 20);
            NameLabel.TabIndex = 3;
            NameLabel.Text = "EVE-O Preview";
            // 
            // DocumentationLink
            // 
            DocumentationLink.Location = new Point(6, 359);
            DocumentationLink.Margin = new Padding(35, 3, 4, 3);
            DocumentationLink.Name = "DocumentationLink";
            DocumentationLink.Padding = new Padding(9, 3, 9, 3);
            DocumentationLink.Size = new Size(306, 38);
            DocumentationLink.TabIndex = 2;
            DocumentationLink.TabStop = true;
            DocumentationLink.Text = "to be set from prresenter to be set from prresenter to be set from prresenter to be set from prresenter";
            DocumentationLink.LinkClicked += DocumentationLinkClicked_Handler;
            // 
            // ClientsTabPage
            // 
            ClientsTabPage.BackColor = SystemColors.Control;
            ClientsTabPage.Controls.Add(ClientsPanel);
            ClientsTabPage.Location = new Point(124, 4);
            ClientsTabPage.Margin = new Padding(4, 3, 4, 3);
            ClientsTabPage.Name = "ClientsTabPage";
            ClientsTabPage.Size = new Size(332, 409);
            ClientsTabPage.TabIndex = 4;
            ClientsTabPage.Text = "Active Clients";
            // 
            // ContentTabControl
            // 
            ContentTabControl.Alignment = TabAlignment.Left;
            ContentTabControl.Controls.Add(GeneralTabPage);
            ContentTabControl.Controls.Add(ThumbnailTabPage);
            ContentTabControl.Controls.Add(ZoomTabPage);
            ContentTabControl.Controls.Add(OverlayTabPage);
            ContentTabControl.Controls.Add(ClientsTabPage);
            ContentTabControl.Controls.Add(CycleGroupTabPage);
            ContentTabControl.Controls.Add(FpsLimiterTabPage);
            ContentTabControl.Controls.Add(tabPageProfiles);
            ContentTabControl.Controls.Add(AboutTabPage);
            ContentTabControl.Dock = DockStyle.Fill;
            ContentTabControl.DrawMode = TabDrawMode.OwnerDrawFixed;
            ContentTabControl.ItemSize = new Size(35, 120);
            ContentTabControl.Location = new Point(0, 0);
            ContentTabControl.Margin = new Padding(4, 3, 4, 3);
            ContentTabControl.Multiline = true;
            ContentTabControl.Name = "ContentTabControl";
            ContentTabControl.SelectedIndex = 0;
            ContentTabControl.Size = new Size(460, 417);
            ContentTabControl.SizeMode = TabSizeMode.Fixed;
            ContentTabControl.TabIndex = 7;
            ContentTabControl.DrawItem += ContentTabControl_DrawItem;
            ContentTabControl.DpiChangedAfterParent += ContentTabControl_DpiChangedAfterParent;
            // 
            // ZoomTabPage
            // 
            ZoomTabPage.BackColor = SystemColors.Control;
            ZoomTabPage.Controls.Add(ZoomSettingsPanel);
            ZoomTabPage.Location = new Point(124, 4);
            ZoomTabPage.Margin = new Padding(4, 3, 4, 3);
            ZoomTabPage.Name = "ZoomTabPage";
            ZoomTabPage.Size = new Size(332, 409);
            ZoomTabPage.TabIndex = 2;
            ZoomTabPage.Text = "Zoom";
            // 
            // FpsLimiterTabPage
            // 
            FpsLimiterTabPage.Controls.Add(fpsMainLayoutPanel);
            FpsLimiterTabPage.Location = new Point(124, 4);
            FpsLimiterTabPage.Margin = new Padding(4, 3, 4, 3);
            FpsLimiterTabPage.Name = "FpsLimiterTabPage";
            FpsLimiterTabPage.Size = new Size(332, 409);
            FpsLimiterTabPage.TabIndex = 7;
            FpsLimiterTabPage.Text = "FPS / Audio";
            FpsLimiterTabPage.UseVisualStyleBackColor = true;
            // 
            // fpsMainLayoutPanel
            // 
            fpsMainLayoutPanel.ColumnCount = 1;
            fpsMainLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 315F));
            fpsMainLayoutPanel.Controls.Add(fpsTopPanel, 0, 0);
            fpsMainLayoutPanel.Controls.Add(fpsBottomPanel, 0, 1);
            fpsMainLayoutPanel.Dock = DockStyle.Fill;
            fpsMainLayoutPanel.Location = new Point(0, 0);
            fpsMainLayoutPanel.Margin = new Padding(4, 3, 4, 3);
            fpsMainLayoutPanel.Name = "fpsMainLayoutPanel";
            fpsMainLayoutPanel.RowCount = 2;
            fpsMainLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 126F));
            fpsMainLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            fpsMainLayoutPanel.Size = new Size(332, 409);
            fpsMainLayoutPanel.TabIndex = 0;
            // 
            // fpsTopPanel
            // 
            fpsTopPanel.Controls.Add(label1);
            fpsTopPanel.Dock = DockStyle.Fill;
            fpsTopPanel.Location = new Point(4, 3);
            fpsTopPanel.Margin = new Padding(4, 3, 4, 3);
            fpsTopPanel.Name = "fpsTopPanel";
            fpsTopPanel.Size = new Size(324, 120);
            fpsTopPanel.TabIndex = 0;
            // 
            // fpsBottomPanel
            // 
            fpsBottomPanel.Controls.Add(groupBoxAudioMuting);
            fpsBottomPanel.Controls.Add(groupBoxFpsLimits);
            fpsBottomPanel.Controls.Add(chbIsFpsThrottlingEnabled);
            fpsBottomPanel.Dock = DockStyle.Fill;
            fpsBottomPanel.Location = new Point(4, 129);
            fpsBottomPanel.Margin = new Padding(4, 3, 4, 3);
            fpsBottomPanel.Name = "fpsBottomPanel";
            fpsBottomPanel.Size = new Size(324, 277);
            fpsBottomPanel.TabIndex = 1;
            // 
            // groupBoxAudioMuting
            // 
            groupBoxAudioMuting.Controls.Add(chbIsLocationBannerMuted);
            groupBoxAudioMuting.Controls.Add(chbIsGateTunnelMuted);
            groupBoxAudioMuting.Location = new Point(12, 182);
            groupBoxAudioMuting.Margin = new Padding(4, 3, 4, 3);
            groupBoxAudioMuting.Name = "groupBoxAudioMuting";
            groupBoxAudioMuting.Padding = new Padding(4, 3, 4, 3);
            groupBoxAudioMuting.Size = new Size(281, 76);
            groupBoxAudioMuting.TabIndex = 23;
            groupBoxAudioMuting.TabStop = false;
            groupBoxAudioMuting.Text = "Audio";
            // 
            // chbIsLocationBannerMuted
            // 
            chbIsLocationBannerMuted.AutoSize = true;
            chbIsLocationBannerMuted.Location = new Point(16, 51);
            chbIsLocationBannerMuted.Margin = new Padding(4, 3, 4, 3);
            chbIsLocationBannerMuted.Name = "chbIsLocationBannerMuted";
            chbIsLocationBannerMuted.Size = new Size(168, 19);
            chbIsLocationBannerMuted.TabIndex = 23;
            chbIsLocationBannerMuted.Text = "Mute Asteroid Belt Warp In";
            chbIsLocationBannerMuted.UseVisualStyleBackColor = true;
            chbIsLocationBannerMuted.CheckedChanged += chbIsLocationBannerMuted_CheckedChanged;
            // 
            // chbIsGateTunnelMuted
            // 
            chbIsGateTunnelMuted.AutoSize = true;
            chbIsGateTunnelMuted.Location = new Point(16, 24);
            chbIsGateTunnelMuted.Margin = new Padding(4, 3, 4, 3);
            chbIsGateTunnelMuted.Name = "chbIsGateTunnelMuted";
            chbIsGateTunnelMuted.Size = new Size(153, 19);
            chbIsGateTunnelMuted.TabIndex = 22;
            chbIsGateTunnelMuted.Text = "Mute Jump Gate Tunnel";
            chbIsGateTunnelMuted.UseVisualStyleBackColor = true;
            chbIsGateTunnelMuted.CheckedChanged += chbIsGateTunnelMuted_CheckedChanged;
            // 
            // groupBoxFpsLimits
            // 
            groupBoxFpsLimits.Controls.Add(btnDummyFpsSave);
            groupBoxFpsLimits.Controls.Add(lblFpsPredictiveLimit);
            groupBoxFpsLimits.Controls.Add(numericFpsPredictedLimit);
            groupBoxFpsLimits.Controls.Add(lblFpsBackgroundLimit);
            groupBoxFpsLimits.Controls.Add(numericFpsBackgroundLimit);
            groupBoxFpsLimits.Controls.Add(lblFpsForegroundLimit);
            groupBoxFpsLimits.Controls.Add(numericFpsForegroundLimit);
            groupBoxFpsLimits.Controls.Add(lblFpsFeatureExpired);
            groupBoxFpsLimits.Location = new Point(12, 43);
            groupBoxFpsLimits.Margin = new Padding(4, 3, 4, 3);
            groupBoxFpsLimits.Name = "groupBoxFpsLimits";
            groupBoxFpsLimits.Padding = new Padding(4, 3, 4, 3);
            groupBoxFpsLimits.Size = new Size(281, 132);
            groupBoxFpsLimits.TabIndex = 22;
            groupBoxFpsLimits.TabStop = false;
            groupBoxFpsLimits.Text = "FPS Limits";
            // 
            // btnDummyFpsSave
            // 
            btnDummyFpsSave.Location = new Point(239, 88);
            btnDummyFpsSave.Margin = new Padding(4, 3, 4, 3);
            btnDummyFpsSave.Name = "btnDummyFpsSave";
            btnDummyFpsSave.Size = new Size(35, 27);
            btnDummyFpsSave.TabIndex = 30;
            btnDummyFpsSave.Text = "Go";
            btnDummyFpsSave.UseVisualStyleBackColor = true;
            // 
            // numericFpsPredictedLimit
            // 
            numericFpsPredictedLimit.BackColor = SystemColors.Window;
            numericFpsPredictedLimit.BorderStyle = BorderStyle.FixedSingle;
            numericFpsPredictedLimit.CausesValidation = false;
            numericFpsPredictedLimit.Location = new Point(126, 91);
            numericFpsPredictedLimit.Margin = new Padding(4, 3, 4, 3);
            numericFpsPredictedLimit.Maximum = new decimal(new int[] { 500, 0, 0, 0 });
            numericFpsPredictedLimit.Name = "numericFpsPredictedLimit";
            numericFpsPredictedLimit.Size = new Size(56, 23);
            numericFpsPredictedLimit.TabIndex = 28;
            numericFpsPredictedLimit.Value = new decimal(new int[] { 30, 0, 0, 0 });
            numericFpsPredictedLimit.Leave += numericFpsPredictedLimit_Leave;
            // 
            // numericFpsBackgroundLimit
            // 
            numericFpsBackgroundLimit.BackColor = SystemColors.Window;
            numericFpsBackgroundLimit.BorderStyle = BorderStyle.FixedSingle;
            numericFpsBackgroundLimit.CausesValidation = false;
            numericFpsBackgroundLimit.Location = new Point(126, 55);
            numericFpsBackgroundLimit.Margin = new Padding(4, 3, 4, 3);
            numericFpsBackgroundLimit.Maximum = new decimal(new int[] { 500, 0, 0, 0 });
            numericFpsBackgroundLimit.Name = "numericFpsBackgroundLimit";
            numericFpsBackgroundLimit.Size = new Size(56, 23);
            numericFpsBackgroundLimit.TabIndex = 26;
            numericFpsBackgroundLimit.Value = new decimal(new int[] { 60, 0, 0, 0 });
            numericFpsBackgroundLimit.Leave += numericFpsBackgroundLimit_Leave;
            // 
            // numericFpsForegroundLimit
            // 
            numericFpsForegroundLimit.BackColor = SystemColors.Window;
            numericFpsForegroundLimit.BorderStyle = BorderStyle.FixedSingle;
            numericFpsForegroundLimit.CausesValidation = false;
            numericFpsForegroundLimit.Location = new Point(126, 22);
            numericFpsForegroundLimit.Margin = new Padding(4, 3, 4, 3);
            numericFpsForegroundLimit.Maximum = new decimal(new int[] { 500, 0, 0, 0 });
            numericFpsForegroundLimit.Name = "numericFpsForegroundLimit";
            numericFpsForegroundLimit.Size = new Size(56, 23);
            numericFpsForegroundLimit.TabIndex = 24;
            numericFpsForegroundLimit.Value = new decimal(new int[] { 144, 0, 0, 0 });
            numericFpsForegroundLimit.Leave += numericFpsForegroundLimit_Leave;
            // 
            // lblFpsFeatureExpired
            // 
            lblFpsFeatureExpired.Location = new Point(13, 24);
            lblFpsFeatureExpired.Margin = new Padding(4, 0, 4, 0);
            lblFpsFeatureExpired.Name = "lblFpsFeatureExpired";
            lblFpsFeatureExpired.Size = new Size(261, 78);
            lblFpsFeatureExpired.TabIndex = 24;
            lblFpsFeatureExpired.Text = "This experimental feature has expired.\r\nIf this is the first time running, please try closing and re-starting, or alternatively please update to the latest version.\r\n";
            lblFpsFeatureExpired.Visible = false;
            // 
            // chbIsFpsThrottlingEnabled
            // 
            chbIsFpsThrottlingEnabled.AutoSize = true;
            chbIsFpsThrottlingEnabled.Location = new Point(12, 10);
            chbIsFpsThrottlingEnabled.Margin = new Padding(4, 3, 4, 3);
            chbIsFpsThrottlingEnabled.Name = "chbIsFpsThrottlingEnabled";
            chbIsFpsThrottlingEnabled.Size = new Size(159, 19);
            chbIsFpsThrottlingEnabled.TabIndex = 21;
            chbIsFpsThrottlingEnabled.Text = "Enable DirectX FPS Limits";
            chbIsFpsThrottlingEnabled.UseVisualStyleBackColor = true;
            chbIsFpsThrottlingEnabled.CheckedChanged += chbIsFpsThrottlingEnabled_CheckedChanged;
            // 
            // tabPageProfiles
            // 
            tabPageProfiles.Controls.Add(splitContainerMainProfiles);
            tabPageProfiles.Location = new Point(124, 4);
            tabPageProfiles.Margin = new Padding(4, 3, 4, 3);
            tabPageProfiles.Name = "tabPageProfiles";
            tabPageProfiles.Size = new Size(332, 409);
            tabPageProfiles.TabIndex = 8;
            tabPageProfiles.Text = "Profiles";
            tabPageProfiles.UseVisualStyleBackColor = true;
            // 
            // splitContainerMainProfiles
            // 
            splitContainerMainProfiles.Dock = DockStyle.Fill;
            splitContainerMainProfiles.FixedPanel = FixedPanel.Panel1;
            splitContainerMainProfiles.Location = new Point(0, 0);
            splitContainerMainProfiles.Margin = new Padding(4, 3, 4, 3);
            splitContainerMainProfiles.Name = "splitContainerMainProfiles";
            splitContainerMainProfiles.Orientation = Orientation.Horizontal;
            // 
            // splitContainerMainProfiles.Panel1
            // 
            splitContainerMainProfiles.Panel1.Controls.Add(lblProfilesExperimentalWarning);
            splitContainerMainProfiles.Panel1.Controls.Add(lblLoadedProfileName);
            splitContainerMainProfiles.Panel1.Controls.Add(btnDeleteProfile);
            splitContainerMainProfiles.Panel1.Controls.Add(btnCloneProfile);
            splitContainerMainProfiles.Panel1.Controls.Add(txtLoadedProfileName);
            // 
            // splitContainerMainProfiles.Panel2
            // 
            splitContainerMainProfiles.Panel2.Controls.Add(listBoxProfiles);
            splitContainerMainProfiles.Size = new Size(332, 409);
            splitContainerMainProfiles.SplitterDistance = 103;
            splitContainerMainProfiles.SplitterWidth = 5;
            splitContainerMainProfiles.TabIndex = 0;
            // 
            // lblProfilesExperimentalWarning
            // 
            lblProfilesExperimentalWarning.Location = new Point(6, 3);
            lblProfilesExperimentalWarning.Margin = new Padding(4, 0, 4, 0);
            lblProfilesExperimentalWarning.Name = "lblProfilesExperimentalWarning";
            lblProfilesExperimentalWarning.Size = new Size(229, 30);
            lblProfilesExperimentalWarning.TabIndex = 5;
            lblProfilesExperimentalWarning.Text = "This is a new experimental feature. Make sure you have backups!";
            // 
            // lblLoadedProfileName
            // 
            lblLoadedProfileName.AutoSize = true;
            lblLoadedProfileName.Location = new Point(10, 81);
            lblLoadedProfileName.Margin = new Padding(4, 0, 4, 0);
            lblLoadedProfileName.Name = "lblLoadedProfileName";
            lblLoadedProfileName.Size = new Size(84, 15);
            lblLoadedProfileName.TabIndex = 4;
            lblLoadedProfileName.Text = "Current Profile";
            // 
            // btnDeleteProfile
            // 
            btnDeleteProfile.Location = new Point(156, 39);
            btnDeleteProfile.Margin = new Padding(4, 3, 4, 3);
            btnDeleteProfile.Name = "btnDeleteProfile";
            btnDeleteProfile.Size = new Size(145, 33);
            btnDeleteProfile.TabIndex = 3;
            btnDeleteProfile.Text = "Delete Current Profile";
            btnDeleteProfile.UseVisualStyleBackColor = true;
            btnDeleteProfile.Click += btnDeleteProfile_Click;
            // 
            // btnCloneProfile
            // 
            btnCloneProfile.Location = new Point(4, 39);
            btnCloneProfile.Margin = new Padding(4, 3, 4, 3);
            btnCloneProfile.Name = "btnCloneProfile";
            btnCloneProfile.Size = new Size(145, 33);
            btnCloneProfile.TabIndex = 2;
            btnCloneProfile.Text = "Clone Current Profile";
            btnCloneProfile.UseVisualStyleBackColor = true;
            btnCloneProfile.Click += btnCloneProfile_Click;
            // 
            // txtLoadedProfileName
            // 
            txtLoadedProfileName.Location = new Point(103, 77);
            txtLoadedProfileName.Margin = new Padding(4, 3, 4, 3);
            txtLoadedProfileName.Name = "txtLoadedProfileName";
            txtLoadedProfileName.Size = new Size(198, 23);
            txtLoadedProfileName.TabIndex = 0;
            txtLoadedProfileName.Text = "Default";
            txtLoadedProfileName.KeyDown += txtLoadedProfileName_KeyDown;
            txtLoadedProfileName.Leave += txtLoadedProfileName_Leave;
            // 
            // listBoxProfiles
            // 
            listBoxProfiles.Dock = DockStyle.Fill;
            listBoxProfiles.FormattingEnabled = true;
            listBoxProfiles.Location = new Point(0, 0);
            listBoxProfiles.Margin = new Padding(4, 3, 4, 3);
            listBoxProfiles.Name = "listBoxProfiles";
            listBoxProfiles.Size = new Size(332, 301);
            listBoxProfiles.TabIndex = 0;
            listBoxProfiles.Click += listBoxProfiles_Click;
            // 
            // NotifyIcon
            // 
            NotifyIcon.ContextMenuStrip = TrayMenu;
            NotifyIcon.Icon = (Icon)resources.GetObject("NotifyIcon.Icon");
            NotifyIcon.Text = "EVE-O Preview";
            NotifyIcon.Visible = true;
            NotifyIcon.MouseDoubleClick += RestoreMainForm_Handler;
            // 
            // TrayMenu
            // 
            TrayMenu.ImageScalingSize = new Size(24, 24);
            TrayMenu.Items.AddRange(new ToolStripItem[] { TitleMenuItem, RestoreWindowMenuItem, SeparatorMenuItem, ExitMenuItem });
            TrayMenu.Name = "contextMenuStrip1";
            TrayMenu.Size = new Size(152, 76);
            // 
            // instantToolTip
            // 
            instantToolTip.AutoPopDelay = 5000;
            instantToolTip.InitialDelay = 0;
            instantToolTip.ReshowDelay = 100;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = SystemColors.Control;
            ClientSize = new Size(460, 417);
            Controls.Add(ContentTabControl);
            ForeColor = SystemColors.ControlText;
            Icon = (Icon)resources.GetObject("$this.Icon");
            Margin = new Padding(0);
            MaximizeBox = false;
            MinimizeBox = false;
            MinimumSize = new Size(476, 444);
            Name = "MainForm";
            Text = "EVE-O Preview";
            TopMost = true;
            FormClosing += MainFormClosing_Handler;
            Load += MainFormResize_Handler;
            Resize += MainFormResize_Handler;
            GeneralTabPage.ResumeLayout(false);
            GeneralSettingsPanel.ResumeLayout(false);
            GeneralSettingsPanel.PerformLayout();
            ThumbnailTabPage.ResumeLayout(false);
            ThumbnailSettingsPanel.ResumeLayout(false);
            ThumbnailSettingsPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)ThumbnailsWidthNumericEdit).EndInit();
            ((System.ComponentModel.ISupportInitialize)ThumbnailsHeightNumericEdit).EndInit();
            ((System.ComponentModel.ISupportInitialize)ThumbnailOpacityTrackBar).EndInit();
            ZoomSettingsPanel.ResumeLayout(false);
            ZoomSettingsPanel.PerformLayout();
            ZoomAnchorPanel.ResumeLayout(false);
            ZoomAnchorPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)ThumbnailZoomFactorNumericEdit).EndInit();
            OverlayTabPage.ResumeLayout(false);
            OverlaySettingsPanel.ResumeLayout(false);
            OverlaySettingsPanel.PerformLayout();
            groupBoxOverlayFont.ResumeLayout(false);
            groupBoxOverlayFont.PerformLayout();
            ClientsPanel.ResumeLayout(false);
            activeClientsSplitContainer.Panel1.ResumeLayout(false);
            activeClientsSplitContainer.Panel1.PerformLayout();
            activeClientsSplitContainer.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)activeClientsSplitContainer).EndInit();
            activeClientsSplitContainer.ResumeLayout(false);
            groupBoxToggleHideAllThumbnails.ResumeLayout(false);
            groupBoxToggleHideAllThumbnails.PerformLayout();
            CycleGroupTabPage.ResumeLayout(false);
            CycleGroupPanel.ResumeLayout(false);
            splitContainerMainCycleGroup.Panel1.ResumeLayout(false);
            splitContainerMainCycleGroup.Panel1.PerformLayout();
            splitContainerMainCycleGroup.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainerMainCycleGroup).EndInit();
            splitContainerMainCycleGroup.ResumeLayout(false);
            AboutTabPage.ResumeLayout(false);
            AboutPanel.ResumeLayout(false);
            AboutPanel.PerformLayout();
            ClientsTabPage.ResumeLayout(false);
            ContentTabControl.ResumeLayout(false);
            ZoomTabPage.ResumeLayout(false);
            FpsLimiterTabPage.ResumeLayout(false);
            fpsMainLayoutPanel.ResumeLayout(false);
            fpsTopPanel.ResumeLayout(false);
            fpsBottomPanel.ResumeLayout(false);
            fpsBottomPanel.PerformLayout();
            groupBoxAudioMuting.ResumeLayout(false);
            groupBoxAudioMuting.PerformLayout();
            groupBoxFpsLimits.ResumeLayout(false);
            groupBoxFpsLimits.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)numericFpsPredictedLimit).EndInit();
            ((System.ComponentModel.ISupportInitialize)numericFpsBackgroundLimit).EndInit();
            ((System.ComponentModel.ISupportInitialize)numericFpsForegroundLimit).EndInit();
            tabPageProfiles.ResumeLayout(false);
            splitContainerMainProfiles.Panel1.ResumeLayout(false);
            splitContainerMainProfiles.Panel1.PerformLayout();
            splitContainerMainProfiles.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainerMainProfiles).EndInit();
            splitContainerMainProfiles.ResumeLayout(false);
            TrayMenu.ResumeLayout(false);
            ResumeLayout(false);

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
        private ToolTip instantToolTip;
        private Button btnDummyFpsSave;
        private Label lblFpsFeatureExpired;
        private GroupBox groupBoxAudioMuting;
        private CheckBox chbIsLocationBannerMuted;
        private CheckBox chbIsGateTunnelMuted;
        private TextBox txtToggleHideAllActiveHotkey;
        private Label lblToggleHideAllActiveHotkey;
        private Button btnToggleHideAll;
        private TabControl ContentTabControl;
        private TabPage ClientsTabPage;
        private GroupBox groupBoxToggleHideAllThumbnails;
        private Button btnMinimizeAllClients;
        private Label lblMinimizeAllClientsHotkey;
        private TextBox txtMinimizeAllClientsHotkey;
        private TabPage tabPageProfiles;
        private SplitContainer splitContainerMainProfiles;
        private SplitContainer splitContainerMainCycleGroup;
        private Button btnDeleteProfile;
        private Button btnCloneProfile;
        private TextBox txtLoadedProfileName;
        private ListBox listBoxProfiles;
        private Label lblLoadedProfileName;
        private Label lblProfilesExperimentalWarning;
    }
}
