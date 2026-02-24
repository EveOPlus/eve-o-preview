using System.Drawing;
using System.Windows.Forms;

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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            System.Windows.Forms.Label ThumbnailsListLabel;
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
            System.Windows.Forms.TabPage AboutTabPage;
            System.Windows.Forms.Panel AboutPanel;
            System.Windows.Forms.Label CreditMaintLabel;
            System.Windows.Forms.Label DocumentationLinkLabel;
            System.Windows.Forms.Label DescriptionLabel;
            System.Windows.Forms.Label NameLabel;
            System.Windows.Forms.TabPage CycleGroupTabPage;
            this.activeClientsSplitContainer = new System.Windows.Forms.SplitContainer();
            this.ThumbnailsList = new System.Windows.Forms.CheckedListBox();
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
            this.HighlightColorLabel = new System.Windows.Forms.Label();
            this.ActiveClientHighlightColorButton = new System.Windows.Forms.Panel();
            this.EnableActiveClientHighlightCheckBox = new System.Windows.Forms.CheckBox();
            this.ShowThumbnailOverlaysCheckBox = new System.Windows.Forms.CheckBox();
            this.ShowThumbnailFramesCheckBox = new System.Windows.Forms.CheckBox();
            this.VersionLabel = new System.Windows.Forms.Label();
            this.DocumentationLink = new System.Windows.Forms.LinkLabel();
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
            this.NotifyIcon = new System.Windows.Forms.NotifyIcon(this.components);
            this.TrayMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
            RestoreWindowMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            ThumbnailsListLabel = new System.Windows.Forms.Label();
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
            AboutTabPage = new System.Windows.Forms.TabPage();
            AboutPanel = new System.Windows.Forms.Panel();
            CreditMaintLabel = new System.Windows.Forms.Label();
            DocumentationLinkLabel = new System.Windows.Forms.Label();
            DescriptionLabel = new System.Windows.Forms.Label();
            NameLabel = new System.Windows.Forms.Label();
            CycleGroupTabPage = new System.Windows.Forms.TabPage();
            ((System.ComponentModel.ISupportInitialize)(this.activeClientsSplitContainer)).BeginInit();
            this.activeClientsSplitContainer.Panel1.SuspendLayout();
            this.activeClientsSplitContainer.Panel2.SuspendLayout();
            this.activeClientsSplitContainer.SuspendLayout();
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
            ClientsTabPage.SuspendLayout();
            ClientsPanel.SuspendLayout();
            AboutTabPage.SuspendLayout();
            AboutPanel.SuspendLayout();
            CycleGroupTabPage.SuspendLayout();
            this.CycleGroupPanel.SuspendLayout();
            this.TrayMenu.SuspendLayout();
            this.SuspendLayout();
            // 
            // RestoreWindowMenuItem
            // 
            resources.ApplyResources(RestoreWindowMenuItem, "RestoreWindowMenuItem");
            RestoreWindowMenuItem.Name = "RestoreWindowMenuItem";
            RestoreWindowMenuItem.Click += new System.EventHandler(this.RestoreMainForm_Handler);
            // 
            // activeClientsSplitContainer
            // 
            resources.ApplyResources(this.activeClientsSplitContainer, "activeClientsSplitContainer");
            this.activeClientsSplitContainer.Name = "activeClientsSplitContainer";
            // 
            // activeClientsSplitContainer.Panel1
            // 
            resources.ApplyResources(this.activeClientsSplitContainer.Panel1, "activeClientsSplitContainer.Panel1");
            this.activeClientsSplitContainer.Panel1.Controls.Add(ThumbnailsListLabel);
            // 
            // activeClientsSplitContainer.Panel2
            // 
            resources.ApplyResources(this.activeClientsSplitContainer.Panel2, "activeClientsSplitContainer.Panel2");
            this.activeClientsSplitContainer.Panel2.Controls.Add(this.ThumbnailsList);
            // 
            // ThumbnailsListLabel
            // 
            resources.ApplyResources(ThumbnailsListLabel, "ThumbnailsListLabel");
            ThumbnailsListLabel.Name = "ThumbnailsListLabel";
            // 
            // ThumbnailsList
            // 
            resources.ApplyResources(this.ThumbnailsList, "ThumbnailsList");
            this.ThumbnailsList.BackColor = System.Drawing.SystemColors.Window;
            this.ThumbnailsList.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.ThumbnailsList.CheckOnClick = true;
            this.ThumbnailsList.FormattingEnabled = true;
            this.ThumbnailsList.Name = "ThumbnailsList";
            this.ThumbnailsList.ItemCheck += new System.Windows.Forms.ItemCheckEventHandler(this.ThumbnailsList_ItemCheck_Handler);
            // 
            // ExitMenuItem
            // 
            resources.ApplyResources(ExitMenuItem, "ExitMenuItem");
            ExitMenuItem.Name = "ExitMenuItem";
            ExitMenuItem.Click += new System.EventHandler(this.ExitMenuItemClick_Handler);
            // 
            // TitleMenuItem
            // 
            resources.ApplyResources(TitleMenuItem, "TitleMenuItem");
            TitleMenuItem.Name = "TitleMenuItem";
            // 
            // SeparatorMenuItem
            // 
            resources.ApplyResources(SeparatorMenuItem, "SeparatorMenuItem");
            SeparatorMenuItem.Name = "SeparatorMenuItem";
            // 
            // ContentTabControl
            // 
            resources.ApplyResources(ContentTabControl, "ContentTabControl");
            ContentTabControl.Controls.Add(GeneralTabPage);
            ContentTabControl.Controls.Add(ThumbnailTabPage);
            ContentTabControl.Controls.Add(this.ZoomTabPage);
            ContentTabControl.Controls.Add(OverlayTabPage);
            ContentTabControl.Controls.Add(ClientsTabPage);
            ContentTabControl.Controls.Add(AboutTabPage);
            ContentTabControl.Controls.Add(CycleGroupTabPage);
            ContentTabControl.DrawMode = System.Windows.Forms.TabDrawMode.OwnerDrawFixed;
            ContentTabControl.Multiline = true;
            ContentTabControl.Name = "ContentTabControl";
            ContentTabControl.SelectedIndex = 0;
            ContentTabControl.SizeMode = System.Windows.Forms.TabSizeMode.Fixed;
            ContentTabControl.DrawItem += new System.Windows.Forms.DrawItemEventHandler(this.ContentTabControl_DrawItem);
            // 
            // GeneralTabPage
            // 
            resources.ApplyResources(GeneralTabPage, "GeneralTabPage");
            GeneralTabPage.BackColor = System.Drawing.SystemColors.Control;
            GeneralTabPage.Controls.Add(GeneralSettingsPanel);
            GeneralTabPage.Name = "GeneralTabPage";
            // 
            // GeneralSettingsPanel
            // 
            resources.ApplyResources(GeneralSettingsPanel, "GeneralSettingsPanel");
            GeneralSettingsPanel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            GeneralSettingsPanel.Controls.Add(this.MinimizeInactiveClientsCheckBox);
            GeneralSettingsPanel.Controls.Add(this.EnableClientLayoutTrackingCheckBox);
            GeneralSettingsPanel.Controls.Add(this.HideActiveClientThumbnailCheckBox);
            GeneralSettingsPanel.Controls.Add(this.ShowThumbnailsAlwaysOnTopCheckBox);
            GeneralSettingsPanel.Controls.Add(this.HideThumbnailsOnLostFocusCheckBox);
            GeneralSettingsPanel.Controls.Add(this.EnablePerClientThumbnailsLayoutsCheckBox);
            GeneralSettingsPanel.Controls.Add(this.MinimizeToTrayCheckBox);
            GeneralSettingsPanel.Name = "GeneralSettingsPanel";
            // 
            // MinimizeInactiveClientsCheckBox
            // 
            resources.ApplyResources(this.MinimizeInactiveClientsCheckBox, "MinimizeInactiveClientsCheckBox");
            this.MinimizeInactiveClientsCheckBox.Name = "MinimizeInactiveClientsCheckBox";
            this.MinimizeInactiveClientsCheckBox.UseVisualStyleBackColor = true;
            this.MinimizeInactiveClientsCheckBox.CheckedChanged += new System.EventHandler(this.OptionChanged_Handler);
            // 
            // EnableClientLayoutTrackingCheckBox
            // 
            resources.ApplyResources(this.EnableClientLayoutTrackingCheckBox, "EnableClientLayoutTrackingCheckBox");
            this.EnableClientLayoutTrackingCheckBox.Name = "EnableClientLayoutTrackingCheckBox";
            this.EnableClientLayoutTrackingCheckBox.UseVisualStyleBackColor = true;
            this.EnableClientLayoutTrackingCheckBox.CheckedChanged += new System.EventHandler(this.OptionChanged_Handler);
            // 
            // HideActiveClientThumbnailCheckBox
            // 
            resources.ApplyResources(this.HideActiveClientThumbnailCheckBox, "HideActiveClientThumbnailCheckBox");
            this.HideActiveClientThumbnailCheckBox.Checked = true;
            this.HideActiveClientThumbnailCheckBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.HideActiveClientThumbnailCheckBox.Name = "HideActiveClientThumbnailCheckBox";
            this.HideActiveClientThumbnailCheckBox.UseVisualStyleBackColor = true;
            this.HideActiveClientThumbnailCheckBox.CheckedChanged += new System.EventHandler(this.OptionChanged_Handler);
            // 
            // ShowThumbnailsAlwaysOnTopCheckBox
            // 
            resources.ApplyResources(this.ShowThumbnailsAlwaysOnTopCheckBox, "ShowThumbnailsAlwaysOnTopCheckBox");
            this.ShowThumbnailsAlwaysOnTopCheckBox.Checked = true;
            this.ShowThumbnailsAlwaysOnTopCheckBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.ShowThumbnailsAlwaysOnTopCheckBox.Name = "ShowThumbnailsAlwaysOnTopCheckBox";
            this.ShowThumbnailsAlwaysOnTopCheckBox.UseVisualStyleBackColor = true;
            this.ShowThumbnailsAlwaysOnTopCheckBox.CheckedChanged += new System.EventHandler(this.OptionChanged_Handler);
            // 
            // HideThumbnailsOnLostFocusCheckBox
            // 
            resources.ApplyResources(this.HideThumbnailsOnLostFocusCheckBox, "HideThumbnailsOnLostFocusCheckBox");
            this.HideThumbnailsOnLostFocusCheckBox.Checked = true;
            this.HideThumbnailsOnLostFocusCheckBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.HideThumbnailsOnLostFocusCheckBox.Name = "HideThumbnailsOnLostFocusCheckBox";
            this.HideThumbnailsOnLostFocusCheckBox.UseVisualStyleBackColor = true;
            this.HideThumbnailsOnLostFocusCheckBox.CheckedChanged += new System.EventHandler(this.OptionChanged_Handler);
            // 
            // EnablePerClientThumbnailsLayoutsCheckBox
            // 
            resources.ApplyResources(this.EnablePerClientThumbnailsLayoutsCheckBox, "EnablePerClientThumbnailsLayoutsCheckBox");
            this.EnablePerClientThumbnailsLayoutsCheckBox.Checked = true;
            this.EnablePerClientThumbnailsLayoutsCheckBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.EnablePerClientThumbnailsLayoutsCheckBox.Name = "EnablePerClientThumbnailsLayoutsCheckBox";
            this.EnablePerClientThumbnailsLayoutsCheckBox.UseVisualStyleBackColor = true;
            this.EnablePerClientThumbnailsLayoutsCheckBox.CheckedChanged += new System.EventHandler(this.OptionChanged_Handler);
            // 
            // MinimizeToTrayCheckBox
            // 
            resources.ApplyResources(this.MinimizeToTrayCheckBox, "MinimizeToTrayCheckBox");
            this.MinimizeToTrayCheckBox.Name = "MinimizeToTrayCheckBox";
            this.MinimizeToTrayCheckBox.UseVisualStyleBackColor = true;
            this.MinimizeToTrayCheckBox.CheckedChanged += new System.EventHandler(this.OptionChanged_Handler);
            // 
            // ThumbnailTabPage
            // 
            resources.ApplyResources(ThumbnailTabPage, "ThumbnailTabPage");
            ThumbnailTabPage.BackColor = System.Drawing.SystemColors.Control;
            ThumbnailTabPage.Controls.Add(ThumbnailSettingsPanel);
            ThumbnailTabPage.Name = "ThumbnailTabPage";
            // 
            // ThumbnailSettingsPanel
            // 
            resources.ApplyResources(ThumbnailSettingsPanel, "ThumbnailSettingsPanel");
            ThumbnailSettingsPanel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            ThumbnailSettingsPanel.Controls.Add(HeigthLabel);
            ThumbnailSettingsPanel.Controls.Add(WidthLabel);
            ThumbnailSettingsPanel.Controls.Add(this.ThumbnailsWidthNumericEdit);
            ThumbnailSettingsPanel.Controls.Add(this.ThumbnailsHeightNumericEdit);
            ThumbnailSettingsPanel.Controls.Add(this.ThumbnailOpacityTrackBar);
            ThumbnailSettingsPanel.Controls.Add(OpacityLabel);
            ThumbnailSettingsPanel.Name = "ThumbnailSettingsPanel";
            // 
            // HeigthLabel
            // 
            resources.ApplyResources(HeigthLabel, "HeigthLabel");
            HeigthLabel.Name = "HeigthLabel";
            // 
            // WidthLabel
            // 
            resources.ApplyResources(WidthLabel, "WidthLabel");
            WidthLabel.Name = "WidthLabel";
            // 
            // ThumbnailsWidthNumericEdit
            // 
            resources.ApplyResources(this.ThumbnailsWidthNumericEdit, "ThumbnailsWidthNumericEdit");
            this.ThumbnailsWidthNumericEdit.BackColor = System.Drawing.SystemColors.Window;
            this.ThumbnailsWidthNumericEdit.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.ThumbnailsWidthNumericEdit.CausesValidation = false;
            this.ThumbnailsWidthNumericEdit.Increment = new decimal(new int[] {
            10,
            0,
            0,
            0});
            this.ThumbnailsWidthNumericEdit.Maximum = new decimal(new int[] {
            999999,
            0,
            0,
            0});
            this.ThumbnailsWidthNumericEdit.Name = "ThumbnailsWidthNumericEdit";
            this.ThumbnailsWidthNumericEdit.Value = new decimal(new int[] {
            100,
            0,
            0,
            0});
            this.ThumbnailsWidthNumericEdit.ValueChanged += new System.EventHandler(this.ThumbnailSizeChanged_Handler);
            // 
            // ThumbnailsHeightNumericEdit
            // 
            resources.ApplyResources(this.ThumbnailsHeightNumericEdit, "ThumbnailsHeightNumericEdit");
            this.ThumbnailsHeightNumericEdit.BackColor = System.Drawing.SystemColors.Window;
            this.ThumbnailsHeightNumericEdit.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.ThumbnailsHeightNumericEdit.CausesValidation = false;
            this.ThumbnailsHeightNumericEdit.Increment = new decimal(new int[] {
            10,
            0,
            0,
            0});
            this.ThumbnailsHeightNumericEdit.Maximum = new decimal(new int[] {
            99999999,
            0,
            0,
            0});
            this.ThumbnailsHeightNumericEdit.Name = "ThumbnailsHeightNumericEdit";
            this.ThumbnailsHeightNumericEdit.Value = new decimal(new int[] {
            70,
            0,
            0,
            0});
            this.ThumbnailsHeightNumericEdit.ValueChanged += new System.EventHandler(this.ThumbnailSizeChanged_Handler);
            // 
            // ThumbnailOpacityTrackBar
            // 
            resources.ApplyResources(this.ThumbnailOpacityTrackBar, "ThumbnailOpacityTrackBar");
            this.ThumbnailOpacityTrackBar.LargeChange = 10;
            this.ThumbnailOpacityTrackBar.Maximum = 100;
            this.ThumbnailOpacityTrackBar.Minimum = 20;
            this.ThumbnailOpacityTrackBar.Name = "ThumbnailOpacityTrackBar";
            this.ThumbnailOpacityTrackBar.TickFrequency = 10;
            this.ThumbnailOpacityTrackBar.Value = 20;
            this.ThumbnailOpacityTrackBar.ValueChanged += new System.EventHandler(this.OptionChanged_Handler);
            // 
            // OpacityLabel
            // 
            resources.ApplyResources(OpacityLabel, "OpacityLabel");
            OpacityLabel.Name = "OpacityLabel";
            // 
            // ZoomTabPage
            // 
            resources.ApplyResources(this.ZoomTabPage, "ZoomTabPage");
            this.ZoomTabPage.BackColor = System.Drawing.SystemColors.Control;
            this.ZoomTabPage.Controls.Add(ZoomSettingsPanel);
            this.ZoomTabPage.Name = "ZoomTabPage";
            // 
            // ZoomSettingsPanel
            // 
            resources.ApplyResources(ZoomSettingsPanel, "ZoomSettingsPanel");
            ZoomSettingsPanel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            ZoomSettingsPanel.Controls.Add(ZoomFactorLabel);
            ZoomSettingsPanel.Controls.Add(this.ZoomAnchorPanel);
            ZoomSettingsPanel.Controls.Add(ZoomAnchorLabel);
            ZoomSettingsPanel.Controls.Add(this.EnableThumbnailZoomCheckBox);
            ZoomSettingsPanel.Controls.Add(this.ThumbnailZoomFactorNumericEdit);
            ZoomSettingsPanel.Name = "ZoomSettingsPanel";
            // 
            // ZoomFactorLabel
            // 
            resources.ApplyResources(ZoomFactorLabel, "ZoomFactorLabel");
            ZoomFactorLabel.Name = "ZoomFactorLabel";
            // 
            // ZoomAnchorPanel
            // 
            resources.ApplyResources(this.ZoomAnchorPanel, "ZoomAnchorPanel");
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
            this.ZoomAnchorPanel.Name = "ZoomAnchorPanel";
            // 
            // ZoomAanchorNWRadioButton
            // 
            resources.ApplyResources(this.ZoomAanchorNWRadioButton, "ZoomAanchorNWRadioButton");
            this.ZoomAanchorNWRadioButton.Name = "ZoomAanchorNWRadioButton";
            this.ZoomAanchorNWRadioButton.TabStop = true;
            this.ZoomAanchorNWRadioButton.UseVisualStyleBackColor = true;
            this.ZoomAanchorNWRadioButton.CheckedChanged += new System.EventHandler(this.OptionChanged_Handler);
            // 
            // ZoomAanchorNRadioButton
            // 
            resources.ApplyResources(this.ZoomAanchorNRadioButton, "ZoomAanchorNRadioButton");
            this.ZoomAanchorNRadioButton.Name = "ZoomAanchorNRadioButton";
            this.ZoomAanchorNRadioButton.TabStop = true;
            this.ZoomAanchorNRadioButton.UseVisualStyleBackColor = true;
            this.ZoomAanchorNRadioButton.CheckedChanged += new System.EventHandler(this.OptionChanged_Handler);
            // 
            // ZoomAanchorNERadioButton
            // 
            resources.ApplyResources(this.ZoomAanchorNERadioButton, "ZoomAanchorNERadioButton");
            this.ZoomAanchorNERadioButton.Name = "ZoomAanchorNERadioButton";
            this.ZoomAanchorNERadioButton.TabStop = true;
            this.ZoomAanchorNERadioButton.UseVisualStyleBackColor = true;
            this.ZoomAanchorNERadioButton.CheckedChanged += new System.EventHandler(this.OptionChanged_Handler);
            // 
            // ZoomAanchorWRadioButton
            // 
            resources.ApplyResources(this.ZoomAanchorWRadioButton, "ZoomAanchorWRadioButton");
            this.ZoomAanchorWRadioButton.Name = "ZoomAanchorWRadioButton";
            this.ZoomAanchorWRadioButton.TabStop = true;
            this.ZoomAanchorWRadioButton.UseVisualStyleBackColor = true;
            this.ZoomAanchorWRadioButton.CheckedChanged += new System.EventHandler(this.OptionChanged_Handler);
            // 
            // ZoomAanchorSERadioButton
            // 
            resources.ApplyResources(this.ZoomAanchorSERadioButton, "ZoomAanchorSERadioButton");
            this.ZoomAanchorSERadioButton.Name = "ZoomAanchorSERadioButton";
            this.ZoomAanchorSERadioButton.TabStop = true;
            this.ZoomAanchorSERadioButton.UseVisualStyleBackColor = true;
            this.ZoomAanchorSERadioButton.CheckedChanged += new System.EventHandler(this.OptionChanged_Handler);
            // 
            // ZoomAanchorCRadioButton
            // 
            resources.ApplyResources(this.ZoomAanchorCRadioButton, "ZoomAanchorCRadioButton");
            this.ZoomAanchorCRadioButton.Name = "ZoomAanchorCRadioButton";
            this.ZoomAanchorCRadioButton.TabStop = true;
            this.ZoomAanchorCRadioButton.UseVisualStyleBackColor = true;
            this.ZoomAanchorCRadioButton.CheckedChanged += new System.EventHandler(this.OptionChanged_Handler);
            // 
            // ZoomAanchorSRadioButton
            // 
            resources.ApplyResources(this.ZoomAanchorSRadioButton, "ZoomAanchorSRadioButton");
            this.ZoomAanchorSRadioButton.Name = "ZoomAanchorSRadioButton";
            this.ZoomAanchorSRadioButton.TabStop = true;
            this.ZoomAanchorSRadioButton.UseVisualStyleBackColor = true;
            this.ZoomAanchorSRadioButton.CheckedChanged += new System.EventHandler(this.OptionChanged_Handler);
            // 
            // ZoomAanchorERadioButton
            // 
            resources.ApplyResources(this.ZoomAanchorERadioButton, "ZoomAanchorERadioButton");
            this.ZoomAanchorERadioButton.Name = "ZoomAanchorERadioButton";
            this.ZoomAanchorERadioButton.TabStop = true;
            this.ZoomAanchorERadioButton.UseVisualStyleBackColor = true;
            this.ZoomAanchorERadioButton.CheckedChanged += new System.EventHandler(this.OptionChanged_Handler);
            // 
            // ZoomAanchorSWRadioButton
            // 
            resources.ApplyResources(this.ZoomAanchorSWRadioButton, "ZoomAanchorSWRadioButton");
            this.ZoomAanchorSWRadioButton.Name = "ZoomAanchorSWRadioButton";
            this.ZoomAanchorSWRadioButton.TabStop = true;
            this.ZoomAanchorSWRadioButton.UseVisualStyleBackColor = true;
            this.ZoomAanchorSWRadioButton.CheckedChanged += new System.EventHandler(this.OptionChanged_Handler);
            // 
            // ZoomAnchorLabel
            // 
            resources.ApplyResources(ZoomAnchorLabel, "ZoomAnchorLabel");
            ZoomAnchorLabel.Name = "ZoomAnchorLabel";
            // 
            // EnableThumbnailZoomCheckBox
            // 
            resources.ApplyResources(this.EnableThumbnailZoomCheckBox, "EnableThumbnailZoomCheckBox");
            this.EnableThumbnailZoomCheckBox.Checked = true;
            this.EnableThumbnailZoomCheckBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.EnableThumbnailZoomCheckBox.Name = "EnableThumbnailZoomCheckBox";
            this.EnableThumbnailZoomCheckBox.UseVisualStyleBackColor = true;
            this.EnableThumbnailZoomCheckBox.CheckedChanged += new System.EventHandler(this.OptionChanged_Handler);
            // 
            // ThumbnailZoomFactorNumericEdit
            // 
            resources.ApplyResources(this.ThumbnailZoomFactorNumericEdit, "ThumbnailZoomFactorNumericEdit");
            this.ThumbnailZoomFactorNumericEdit.BackColor = System.Drawing.SystemColors.Window;
            this.ThumbnailZoomFactorNumericEdit.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
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
            this.ThumbnailZoomFactorNumericEdit.Value = new decimal(new int[] {
            2,
            0,
            0,
            0});
            this.ThumbnailZoomFactorNumericEdit.ValueChanged += new System.EventHandler(this.OptionChanged_Handler);
            // 
            // OverlayTabPage
            // 
            resources.ApplyResources(OverlayTabPage, "OverlayTabPage");
            OverlayTabPage.BackColor = System.Drawing.SystemColors.Control;
            OverlayTabPage.Controls.Add(OverlaySettingsPanel);
            OverlayTabPage.Name = "OverlayTabPage";
            // 
            // OverlaySettingsPanel
            // 
            resources.ApplyResources(OverlaySettingsPanel, "OverlaySettingsPanel");
            OverlaySettingsPanel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            OverlaySettingsPanel.Controls.Add(this.HighlightColorLabel);
            OverlaySettingsPanel.Controls.Add(this.ActiveClientHighlightColorButton);
            OverlaySettingsPanel.Controls.Add(this.EnableActiveClientHighlightCheckBox);
            OverlaySettingsPanel.Controls.Add(this.ShowThumbnailOverlaysCheckBox);
            OverlaySettingsPanel.Controls.Add(this.ShowThumbnailFramesCheckBox);
            OverlaySettingsPanel.Name = "OverlaySettingsPanel";
            // 
            // HighlightColorLabel
            // 
            resources.ApplyResources(this.HighlightColorLabel, "HighlightColorLabel");
            this.HighlightColorLabel.Name = "HighlightColorLabel";
            // 
            // ActiveClientHighlightColorButton
            // 
            resources.ApplyResources(this.ActiveClientHighlightColorButton, "ActiveClientHighlightColorButton");
            this.ActiveClientHighlightColorButton.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.ActiveClientHighlightColorButton.Name = "ActiveClientHighlightColorButton";
            this.ActiveClientHighlightColorButton.Click += new System.EventHandler(this.ActiveClientHighlightColorButton_Click);
            // 
            // EnableActiveClientHighlightCheckBox
            // 
            resources.ApplyResources(this.EnableActiveClientHighlightCheckBox, "EnableActiveClientHighlightCheckBox");
            this.EnableActiveClientHighlightCheckBox.Checked = true;
            this.EnableActiveClientHighlightCheckBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.EnableActiveClientHighlightCheckBox.Name = "EnableActiveClientHighlightCheckBox";
            this.EnableActiveClientHighlightCheckBox.UseVisualStyleBackColor = true;
            this.EnableActiveClientHighlightCheckBox.CheckedChanged += new System.EventHandler(this.OptionChanged_Handler);
            // 
            // ShowThumbnailOverlaysCheckBox
            // 
            resources.ApplyResources(this.ShowThumbnailOverlaysCheckBox, "ShowThumbnailOverlaysCheckBox");
            this.ShowThumbnailOverlaysCheckBox.Checked = true;
            this.ShowThumbnailOverlaysCheckBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.ShowThumbnailOverlaysCheckBox.Name = "ShowThumbnailOverlaysCheckBox";
            this.ShowThumbnailOverlaysCheckBox.UseVisualStyleBackColor = true;
            this.ShowThumbnailOverlaysCheckBox.CheckedChanged += new System.EventHandler(this.OptionChanged_Handler);
            // 
            // ShowThumbnailFramesCheckBox
            // 
            resources.ApplyResources(this.ShowThumbnailFramesCheckBox, "ShowThumbnailFramesCheckBox");
            this.ShowThumbnailFramesCheckBox.Checked = true;
            this.ShowThumbnailFramesCheckBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.ShowThumbnailFramesCheckBox.Name = "ShowThumbnailFramesCheckBox";
            this.ShowThumbnailFramesCheckBox.UseVisualStyleBackColor = true;
            this.ShowThumbnailFramesCheckBox.CheckedChanged += new System.EventHandler(this.OptionChanged_Handler);
            // 
            // ClientsTabPage
            // 
            resources.ApplyResources(ClientsTabPage, "ClientsTabPage");
            ClientsTabPage.BackColor = System.Drawing.SystemColors.Control;
            ClientsTabPage.Controls.Add(ClientsPanel);
            ClientsTabPage.Name = "ClientsTabPage";
            // 
            // ClientsPanel
            // 
            resources.ApplyResources(ClientsPanel, "ClientsPanel");
            ClientsPanel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            ClientsPanel.Controls.Add(this.activeClientsSplitContainer);
            ClientsPanel.Name = "ClientsPanel";
            // 
            // AboutTabPage
            // 
            resources.ApplyResources(AboutTabPage, "AboutTabPage");
            AboutTabPage.BackColor = System.Drawing.SystemColors.Control;
            AboutTabPage.Controls.Add(AboutPanel);
            AboutTabPage.Name = "AboutTabPage";
            // 
            // AboutPanel
            // 
            resources.ApplyResources(AboutPanel, "AboutPanel");
            AboutPanel.BackColor = System.Drawing.Color.Transparent;
            AboutPanel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            AboutPanel.Controls.Add(CreditMaintLabel);
            AboutPanel.Controls.Add(DocumentationLinkLabel);
            AboutPanel.Controls.Add(DescriptionLabel);
            AboutPanel.Controls.Add(this.VersionLabel);
            AboutPanel.Controls.Add(NameLabel);
            AboutPanel.Controls.Add(this.DocumentationLink);
            AboutPanel.Name = "AboutPanel";
            // 
            // CreditMaintLabel
            // 
            resources.ApplyResources(CreditMaintLabel, "CreditMaintLabel");
            CreditMaintLabel.Name = "CreditMaintLabel";
            // 
            // DocumentationLinkLabel
            // 
            resources.ApplyResources(DocumentationLinkLabel, "DocumentationLinkLabel");
            DocumentationLinkLabel.Name = "DocumentationLinkLabel";
            // 
            // DescriptionLabel
            // 
            resources.ApplyResources(DescriptionLabel, "DescriptionLabel");
            DescriptionLabel.BackColor = System.Drawing.Color.Transparent;
            DescriptionLabel.Name = "DescriptionLabel";
            // 
            // VersionLabel
            // 
            resources.ApplyResources(this.VersionLabel, "VersionLabel");
            this.VersionLabel.Name = "VersionLabel";
            // 
            // NameLabel
            // 
            resources.ApplyResources(NameLabel, "NameLabel");
            NameLabel.Name = "NameLabel";
            // 
            // DocumentationLink
            // 
            resources.ApplyResources(this.DocumentationLink, "DocumentationLink");
            this.DocumentationLink.Name = "DocumentationLink";
            this.DocumentationLink.TabStop = true;
            this.DocumentationLink.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.DocumentationLinkClicked_Handler);
            // 
            // CycleGroupTabPage
            // 
            resources.ApplyResources(CycleGroupTabPage, "CycleGroupTabPage");
            CycleGroupTabPage.Controls.Add(this.CycleGroupPanel);
            CycleGroupTabPage.Name = "CycleGroupTabPage";
            // 
            // CycleGroupPanel
            // 
            resources.ApplyResources(this.CycleGroupPanel, "CycleGroupPanel");
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
            this.CycleGroupPanel.Name = "CycleGroupPanel";
            // 
            // removeGroupButton
            // 
            resources.ApplyResources(this.removeGroupButton, "removeGroupButton");
            this.removeGroupButton.Name = "removeGroupButton";
            this.removeGroupButton.UseVisualStyleBackColor = true;
            this.removeGroupButton.Click += new System.EventHandler(this.removeGroupButton_Click);
            // 
            // cycleGroupMoveClientOrderUpButton
            // 
            resources.ApplyResources(this.cycleGroupMoveClientOrderUpButton, "cycleGroupMoveClientOrderUpButton");
            this.cycleGroupMoveClientOrderUpButton.Name = "cycleGroupMoveClientOrderUpButton";
            this.cycleGroupMoveClientOrderUpButton.UseVisualStyleBackColor = true;
            this.cycleGroupMoveClientOrderUpButton.Click += new System.EventHandler(this.cycleGroupMoveClientOrderUpButton_Click);
            // 
            // removeClientToCycleGroupButton
            // 
            resources.ApplyResources(this.removeClientToCycleGroupButton, "removeClientToCycleGroupButton");
            this.removeClientToCycleGroupButton.Name = "removeClientToCycleGroupButton";
            this.removeClientToCycleGroupButton.UseVisualStyleBackColor = true;
            this.removeClientToCycleGroupButton.Click += new System.EventHandler(this.removeClientToCycleGroupButton_Click);
            // 
            // addNewGroupButton
            // 
            resources.ApplyResources(this.addNewGroupButton, "addNewGroupButton");
            this.addNewGroupButton.Name = "addNewGroupButton";
            this.addNewGroupButton.UseVisualStyleBackColor = true;
            this.addNewGroupButton.Click += new System.EventHandler(this.addNewGroupButton_Click);
            // 
            // cycleGroupClientOrderLabel
            // 
            resources.ApplyResources(this.cycleGroupClientOrderLabel, "cycleGroupClientOrderLabel");
            this.cycleGroupClientOrderLabel.Name = "cycleGroupClientOrderLabel";
            // 
            // cycleGroupClientOrderList
            // 
            resources.ApplyResources(this.cycleGroupClientOrderList, "cycleGroupClientOrderList");
            this.cycleGroupClientOrderList.FormattingEnabled = true;
            this.cycleGroupClientOrderList.Name = "cycleGroupClientOrderList";
            // 
            // cycleGroupBackwardHotkey2Text
            // 
            resources.ApplyResources(this.cycleGroupBackwardHotkey2Text, "cycleGroupBackwardHotkey2Text");
            this.cycleGroupBackwardHotkey2Text.BackColor = System.Drawing.SystemColors.Control;
            this.cycleGroupBackwardHotkey2Text.Name = "cycleGroupBackwardHotkey2Text";
            this.cycleGroupBackwardHotkey2Text.ReadOnly = true;
            this.cycleGroupBackwardHotkey2Text.DoubleClick += new System.EventHandler(this.cycleGroupBackwardHotkey2Text_DoubleClick);
            // 
            // cycleGroupBackwardHotkey1Text
            // 
            resources.ApplyResources(this.cycleGroupBackwardHotkey1Text, "cycleGroupBackwardHotkey1Text");
            this.cycleGroupBackwardHotkey1Text.BackColor = System.Drawing.SystemColors.Control;
            this.cycleGroupBackwardHotkey1Text.Name = "cycleGroupBackwardHotkey1Text";
            this.cycleGroupBackwardHotkey1Text.ReadOnly = true;
            this.cycleGroupBackwardHotkey1Text.DoubleClick += new System.EventHandler(this.cycleGroupBackwardHotkey1Text_DoubleClick);
            // 
            // cycleGroupBackHotkeyLabel
            // 
            resources.ApplyResources(this.cycleGroupBackHotkeyLabel, "cycleGroupBackHotkeyLabel");
            this.cycleGroupBackHotkeyLabel.Name = "cycleGroupBackHotkeyLabel";
            // 
            // cycleGroupForwardHotkey2Text
            // 
            resources.ApplyResources(this.cycleGroupForwardHotkey2Text, "cycleGroupForwardHotkey2Text");
            this.cycleGroupForwardHotkey2Text.BackColor = System.Drawing.SystemColors.Control;
            this.cycleGroupForwardHotkey2Text.Name = "cycleGroupForwardHotkey2Text";
            this.cycleGroupForwardHotkey2Text.ReadOnly = true;
            this.cycleGroupForwardHotkey2Text.DoubleClick += new System.EventHandler(this.cycleGroupForwardHotkey2Text_DoubleClick);
            // 
            // cycleGroupForwardHotkey1Text
            // 
            resources.ApplyResources(this.cycleGroupForwardHotkey1Text, "cycleGroupForwardHotkey1Text");
            this.cycleGroupForwardHotkey1Text.BackColor = System.Drawing.SystemColors.Control;
            this.cycleGroupForwardHotkey1Text.Name = "cycleGroupForwardHotkey1Text";
            this.cycleGroupForwardHotkey1Text.ReadOnly = true;
            this.cycleGroupForwardHotkey1Text.DoubleClick += new System.EventHandler(this.cycleGroupForwardHotkey1Text_DoubleClick);
            // 
            // cycleGroupForwardHotkeyLabel
            // 
            resources.ApplyResources(this.cycleGroupForwardHotkeyLabel, "cycleGroupForwardHotkeyLabel");
            this.cycleGroupForwardHotkeyLabel.Name = "cycleGroupForwardHotkeyLabel";
            // 
            // cycleGroupDescriptionText
            // 
            resources.ApplyResources(this.cycleGroupDescriptionText, "cycleGroupDescriptionText");
            this.cycleGroupDescriptionText.Name = "cycleGroupDescriptionText";
            this.cycleGroupDescriptionText.Leave += new System.EventHandler(this.cycleGroupDescriptionText_Leave);
            // 
            // cycleGroupDescriptionLabel
            // 
            resources.ApplyResources(this.cycleGroupDescriptionLabel, "cycleGroupDescriptionLabel");
            this.cycleGroupDescriptionLabel.Name = "cycleGroupDescriptionLabel";
            // 
            // CycleGroupLabel
            // 
            resources.ApplyResources(this.CycleGroupLabel, "CycleGroupLabel");
            this.CycleGroupLabel.Name = "CycleGroupLabel";
            // 
            // selectCycleGroupComboBox
            // 
            resources.ApplyResources(this.selectCycleGroupComboBox, "selectCycleGroupComboBox");
            this.selectCycleGroupComboBox.FormattingEnabled = true;
            this.selectCycleGroupComboBox.Name = "selectCycleGroupComboBox";
            this.selectCycleGroupComboBox.SelectedValueChanged += new System.EventHandler(this.selectCycleGroupComboBox_SelectedValueChanged);
            // 
            // addClientToCycleGroupButton
            // 
            resources.ApplyResources(this.addClientToCycleGroupButton, "addClientToCycleGroupButton");
            this.addClientToCycleGroupButton.Name = "addClientToCycleGroupButton";
            this.addClientToCycleGroupButton.UseVisualStyleBackColor = true;
            this.addClientToCycleGroupButton.Click += new System.EventHandler(this.addClientToCycleGroupButton_Click);
            // 
            // NotifyIcon
            // 
            resources.ApplyResources(this.NotifyIcon, "NotifyIcon");
            this.NotifyIcon.ContextMenuStrip = this.TrayMenu;
            this.NotifyIcon.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.RestoreMainForm_Handler);
            // 
            // TrayMenu
            // 
            resources.ApplyResources(this.TrayMenu, "TrayMenu");
            this.TrayMenu.ImageScalingSize = new System.Drawing.Size(24, 24);
            this.TrayMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            TitleMenuItem,
            RestoreWindowMenuItem,
            SeparatorMenuItem,
            ExitMenuItem});
            this.TrayMenu.Name = "contextMenuStrip1";
            // 
            // MainForm
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.Control;
            this.Controls.Add(ContentTabControl);
            this.ForeColor = System.Drawing.SystemColors.ControlText;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.Name = "MainForm";
            this.TopMost = true;
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainFormClosing_Handler);
            this.Load += new System.EventHandler(this.MainFormResize_Handler);
            this.Resize += new System.EventHandler(this.MainFormResize_Handler);
            this.activeClientsSplitContainer.Panel1.ResumeLayout(false);
            this.activeClientsSplitContainer.Panel1.PerformLayout();
            this.activeClientsSplitContainer.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.activeClientsSplitContainer)).EndInit();
            this.activeClientsSplitContainer.ResumeLayout(false);
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
            ClientsTabPage.ResumeLayout(false);
            ClientsPanel.ResumeLayout(false);
            AboutTabPage.ResumeLayout(false);
            AboutPanel.ResumeLayout(false);
            AboutPanel.PerformLayout();
            CycleGroupTabPage.ResumeLayout(false);
            this.CycleGroupPanel.ResumeLayout(false);
            this.CycleGroupPanel.PerformLayout();
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
    }
}
