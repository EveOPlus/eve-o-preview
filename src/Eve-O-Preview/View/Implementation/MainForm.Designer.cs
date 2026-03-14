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
            System.Windows.Forms.TabPage AboutTabPage;
            System.Windows.Forms.Panel AboutPanel;
            System.Windows.Forms.Label DocumentationLinkLabel;
            System.Windows.Forms.Label DescriptionLabel;
            System.Windows.Forms.Label NameLabel;
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
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
            this.VersionLabel = new System.Windows.Forms.Label();
            this.DocumentationLink = new System.Windows.Forms.LinkLabel();
            this.NotifyIcon = new System.Windows.Forms.NotifyIcon(this.components);
            this.TrayMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.lblClientNote = new System.Windows.Forms.Label();
            this.txtClientNote = new System.Windows.Forms.TextBox();
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
            AboutTabPage = new System.Windows.Forms.TabPage();
            AboutPanel = new System.Windows.Forms.Panel();
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
            AboutTabPage.SuspendLayout();
            AboutPanel.SuspendLayout();
            this.TrayMenu.SuspendLayout();
            this.SuspendLayout();
            // 
            // RestoreWindowMenuItem
            // 
            RestoreWindowMenuItem.Name = "RestoreWindowMenuItem";
            RestoreWindowMenuItem.Size = new System.Drawing.Size(206, 30);
            RestoreWindowMenuItem.Text = "Restore";
            RestoreWindowMenuItem.Click += new System.EventHandler(this.RestoreMainForm_Handler);
            // 
            // ExitMenuItem
            // 
            ExitMenuItem.Name = "ExitMenuItem";
            ExitMenuItem.Size = new System.Drawing.Size(206, 30);
            ExitMenuItem.Text = "Exit";
            ExitMenuItem.Click += new System.EventHandler(this.ExitMenuItemClick_Handler);
            // 
            // TitleMenuItem
            // 
            TitleMenuItem.Enabled = false;
            TitleMenuItem.Name = "TitleMenuItem";
            TitleMenuItem.Size = new System.Drawing.Size(206, 30);
            TitleMenuItem.Text = "EVE-O Preview";
            // 
            // SeparatorMenuItem
            // 
            SeparatorMenuItem.Name = "SeparatorMenuItem";
            SeparatorMenuItem.Size = new System.Drawing.Size(203, 6);
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
            ContentTabControl.Controls.Add(AboutTabPage);
            ContentTabControl.Dock = System.Windows.Forms.DockStyle.Fill;
            ContentTabControl.DrawMode = System.Windows.Forms.TabDrawMode.OwnerDrawFixed;
            ContentTabControl.ItemSize = new System.Drawing.Size(35, 120);
            ContentTabControl.Location = new System.Drawing.Point(0, 0);
            ContentTabControl.Margin = new System.Windows.Forms.Padding(4);
            ContentTabControl.Multiline = true;
            ContentTabControl.Name = "ContentTabControl";
            ContentTabControl.SelectedIndex = 0;
            ContentTabControl.Size = new System.Drawing.Size(585, 361);
            ContentTabControl.SizeMode = System.Windows.Forms.TabSizeMode.Fixed;
            ContentTabControl.TabIndex = 7;
            ContentTabControl.DrawItem += new System.Windows.Forms.DrawItemEventHandler(this.ContentTabControl_DrawItem);
            // 
            // GeneralTabPage
            // 
            GeneralTabPage.BackColor = System.Drawing.SystemColors.Control;
            GeneralTabPage.Controls.Add(GeneralSettingsPanel);
            GeneralTabPage.Location = new System.Drawing.Point(124, 4);
            GeneralTabPage.Margin = new System.Windows.Forms.Padding(4);
            GeneralTabPage.Name = "GeneralTabPage";
            GeneralTabPage.Padding = new System.Windows.Forms.Padding(4);
            GeneralTabPage.Size = new System.Drawing.Size(457, 353);
            GeneralTabPage.TabIndex = 0;
            GeneralTabPage.Text = "常规";
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
            GeneralSettingsPanel.Location = new System.Drawing.Point(4, 4);
            GeneralSettingsPanel.Margin = new System.Windows.Forms.Padding(4);
            GeneralSettingsPanel.Name = "GeneralSettingsPanel";
            GeneralSettingsPanel.Size = new System.Drawing.Size(449, 345);
            GeneralSettingsPanel.TabIndex = 18;
            // 
            // MinimizeInactiveClientsCheckBox
            // 
            this.MinimizeInactiveClientsCheckBox.AutoSize = true;
            this.MinimizeInactiveClientsCheckBox.Location = new System.Drawing.Point(12, 109);
            this.MinimizeInactiveClientsCheckBox.Margin = new System.Windows.Forms.Padding(4);
            this.MinimizeInactiveClientsCheckBox.Name = "MinimizeInactiveClientsCheckBox";
            this.MinimizeInactiveClientsCheckBox.Size = new System.Drawing.Size(232, 22);
            this.MinimizeInactiveClientsCheckBox.TabIndex = 24;
            this.MinimizeInactiveClientsCheckBox.Text = "自动最小化未激活客户端";
            this.MinimizeInactiveClientsCheckBox.UseVisualStyleBackColor = true;
            this.MinimizeInactiveClientsCheckBox.CheckedChanged += new System.EventHandler(this.OptionChanged_Handler);
            // 
            // EnableClientLayoutTrackingCheckBox
            // 
            this.EnableClientLayoutTrackingCheckBox.AutoSize = true;
            this.EnableClientLayoutTrackingCheckBox.Location = new System.Drawing.Point(12, 43);
            this.EnableClientLayoutTrackingCheckBox.Margin = new System.Windows.Forms.Padding(4);
            this.EnableClientLayoutTrackingCheckBox.Name = "EnableClientLayoutTrackingCheckBox";
            this.EnableClientLayoutTrackingCheckBox.Size = new System.Drawing.Size(340, 22);
            this.EnableClientLayoutTrackingCheckBox.TabIndex = 19;
            this.EnableClientLayoutTrackingCheckBox.Text = "激活或启动客户端时是否恢复窗口位置";
            this.EnableClientLayoutTrackingCheckBox.UseVisualStyleBackColor = true;
            this.EnableClientLayoutTrackingCheckBox.CheckedChanged += new System.EventHandler(this.OptionChanged_Handler);
            // 
            // HideActiveClientThumbnailCheckBox
            // 
            this.HideActiveClientThumbnailCheckBox.AutoSize = true;
            this.HideActiveClientThumbnailCheckBox.Checked = true;
            this.HideActiveClientThumbnailCheckBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.HideActiveClientThumbnailCheckBox.Location = new System.Drawing.Point(12, 76);
            this.HideActiveClientThumbnailCheckBox.Margin = new System.Windows.Forms.Padding(4);
            this.HideActiveClientThumbnailCheckBox.Name = "HideActiveClientThumbnailCheckBox";
            this.HideActiveClientThumbnailCheckBox.Size = new System.Drawing.Size(304, 22);
            this.HideActiveClientThumbnailCheckBox.TabIndex = 20;
            this.HideActiveClientThumbnailCheckBox.Text = "是否隐藏当前激活客户端的缩略图";
            this.HideActiveClientThumbnailCheckBox.UseVisualStyleBackColor = true;
            this.HideActiveClientThumbnailCheckBox.CheckedChanged += new System.EventHandler(this.OptionChanged_Handler);
            // 
            // ShowThumbnailsAlwaysOnTopCheckBox
            // 
            this.ShowThumbnailsAlwaysOnTopCheckBox.AutoSize = true;
            this.ShowThumbnailsAlwaysOnTopCheckBox.Checked = true;
            this.ShowThumbnailsAlwaysOnTopCheckBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.ShowThumbnailsAlwaysOnTopCheckBox.Location = new System.Drawing.Point(12, 143);
            this.ShowThumbnailsAlwaysOnTopCheckBox.Margin = new System.Windows.Forms.Padding(4);
            this.ShowThumbnailsAlwaysOnTopCheckBox.Name = "ShowThumbnailsAlwaysOnTopCheckBox";
            this.ShowThumbnailsAlwaysOnTopCheckBox.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.ShowThumbnailsAlwaysOnTopCheckBox.Size = new System.Drawing.Size(196, 22);
            this.ShowThumbnailsAlwaysOnTopCheckBox.TabIndex = 21;
            this.ShowThumbnailsAlwaysOnTopCheckBox.Text = "缩略图是否始终置顶";
            this.ShowThumbnailsAlwaysOnTopCheckBox.UseVisualStyleBackColor = true;
            this.ShowThumbnailsAlwaysOnTopCheckBox.CheckedChanged += new System.EventHandler(this.OptionChanged_Handler);
            // 
            // HideThumbnailsOnLostFocusCheckBox
            // 
            this.HideThumbnailsOnLostFocusCheckBox.AutoSize = true;
            this.HideThumbnailsOnLostFocusCheckBox.Checked = true;
            this.HideThumbnailsOnLostFocusCheckBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.HideThumbnailsOnLostFocusCheckBox.Location = new System.Drawing.Point(12, 176);
            this.HideThumbnailsOnLostFocusCheckBox.Margin = new System.Windows.Forms.Padding(4);
            this.HideThumbnailsOnLostFocusCheckBox.Name = "HideThumbnailsOnLostFocusCheckBox";
            this.HideThumbnailsOnLostFocusCheckBox.Size = new System.Drawing.Size(304, 22);
            this.HideThumbnailsOnLostFocusCheckBox.TabIndex = 22;
            this.HideThumbnailsOnLostFocusCheckBox.Text = "非激活状态时是否隐藏所有缩略图";
            this.HideThumbnailsOnLostFocusCheckBox.UseVisualStyleBackColor = true;
            this.HideThumbnailsOnLostFocusCheckBox.CheckedChanged += new System.EventHandler(this.OptionChanged_Handler);
            // 
            // EnablePerClientThumbnailsLayoutsCheckBox
            // 
            this.EnablePerClientThumbnailsLayoutsCheckBox.AutoSize = true;
            this.EnablePerClientThumbnailsLayoutsCheckBox.Checked = true;
            this.EnablePerClientThumbnailsLayoutsCheckBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.EnablePerClientThumbnailsLayoutsCheckBox.Location = new System.Drawing.Point(12, 209);
            this.EnablePerClientThumbnailsLayoutsCheckBox.Margin = new System.Windows.Forms.Padding(4);
            this.EnablePerClientThumbnailsLayoutsCheckBox.Name = "EnablePerClientThumbnailsLayoutsCheckBox";
            this.EnablePerClientThumbnailsLayoutsCheckBox.Size = new System.Drawing.Size(340, 22);
            this.EnablePerClientThumbnailsLayoutsCheckBox.TabIndex = 23;
            this.EnablePerClientThumbnailsLayoutsCheckBox.Text = "是否为不同客户端使用不同缩略图布局";
            this.EnablePerClientThumbnailsLayoutsCheckBox.UseVisualStyleBackColor = true;
            this.EnablePerClientThumbnailsLayoutsCheckBox.CheckedChanged += new System.EventHandler(this.OptionChanged_Handler);
            // 
            // MinimizeToTrayCheckBox
            // 
            this.MinimizeToTrayCheckBox.AutoSize = true;
            this.MinimizeToTrayCheckBox.Location = new System.Drawing.Point(12, 10);
            this.MinimizeToTrayCheckBox.Margin = new System.Windows.Forms.Padding(4);
            this.MinimizeToTrayCheckBox.Name = "MinimizeToTrayCheckBox";
            this.MinimizeToTrayCheckBox.Size = new System.Drawing.Size(322, 22);
            this.MinimizeToTrayCheckBox.TabIndex = 18;
            this.MinimizeToTrayCheckBox.Text = "关闭主窗口时是否最小化到系统托盘";
            this.MinimizeToTrayCheckBox.UseVisualStyleBackColor = true;
            this.MinimizeToTrayCheckBox.CheckedChanged += new System.EventHandler(this.OptionChanged_Handler);
            // 
            // ThumbnailTabPage
            // 
            ThumbnailTabPage.BackColor = System.Drawing.SystemColors.Control;
            ThumbnailTabPage.Controls.Add(ThumbnailSettingsPanel);
            ThumbnailTabPage.Location = new System.Drawing.Point(124, 4);
            ThumbnailTabPage.Margin = new System.Windows.Forms.Padding(4);
            ThumbnailTabPage.Name = "ThumbnailTabPage";
            ThumbnailTabPage.Padding = new System.Windows.Forms.Padding(4);
            ThumbnailTabPage.Size = new System.Drawing.Size(457, 353);
            ThumbnailTabPage.TabIndex = 1;
            ThumbnailTabPage.Text = "缩略图";
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
            ThumbnailSettingsPanel.Location = new System.Drawing.Point(4, 4);
            ThumbnailSettingsPanel.Margin = new System.Windows.Forms.Padding(4);
            ThumbnailSettingsPanel.Name = "ThumbnailSettingsPanel";
            ThumbnailSettingsPanel.Size = new System.Drawing.Size(449, 345);
            ThumbnailSettingsPanel.TabIndex = 19;
            // 
            // HeigthLabel
            // 
            HeigthLabel.AutoSize = true;
            HeigthLabel.Location = new System.Drawing.Point(12, 79);
            HeigthLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            HeigthLabel.Name = "HeigthLabel";
            HeigthLabel.Size = new System.Drawing.Size(98, 18);
            HeigthLabel.TabIndex = 24;
            HeigthLabel.Text = "缩略图高度";
            // 
            // WidthLabel
            // 
            WidthLabel.AutoSize = true;
            WidthLabel.Location = new System.Drawing.Point(12, 46);
            WidthLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            WidthLabel.Name = "WidthLabel";
            WidthLabel.Size = new System.Drawing.Size(98, 18);
            WidthLabel.TabIndex = 23;
            WidthLabel.Text = "缩略图宽度";
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
            this.ThumbnailsWidthNumericEdit.Location = new System.Drawing.Point(158, 43);
            this.ThumbnailsWidthNumericEdit.Margin = new System.Windows.Forms.Padding(4);
            this.ThumbnailsWidthNumericEdit.Maximum = new decimal(new int[] {
            999999,
            0,
            0,
            0});
            this.ThumbnailsWidthNumericEdit.Name = "ThumbnailsWidthNumericEdit";
            this.ThumbnailsWidthNumericEdit.Size = new System.Drawing.Size(72, 28);
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
            this.ThumbnailsHeightNumericEdit.Location = new System.Drawing.Point(158, 76);
            this.ThumbnailsHeightNumericEdit.Margin = new System.Windows.Forms.Padding(4);
            this.ThumbnailsHeightNumericEdit.Maximum = new decimal(new int[] {
            99999999,
            0,
            0,
            0});
            this.ThumbnailsHeightNumericEdit.Name = "ThumbnailsHeightNumericEdit";
            this.ThumbnailsHeightNumericEdit.Size = new System.Drawing.Size(72, 28);
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
            this.ThumbnailOpacityTrackBar.Location = new System.Drawing.Point(92, 8);
            this.ThumbnailOpacityTrackBar.Margin = new System.Windows.Forms.Padding(4);
            this.ThumbnailOpacityTrackBar.Maximum = 100;
            this.ThumbnailOpacityTrackBar.Minimum = 20;
            this.ThumbnailOpacityTrackBar.Name = "ThumbnailOpacityTrackBar";
            this.ThumbnailOpacityTrackBar.Size = new System.Drawing.Size(286, 30);
            this.ThumbnailOpacityTrackBar.TabIndex = 20;
            this.ThumbnailOpacityTrackBar.TickFrequency = 10;
            this.ThumbnailOpacityTrackBar.Value = 20;
            this.ThumbnailOpacityTrackBar.ValueChanged += new System.EventHandler(this.OptionChanged_Handler);
            // 
            // OpacityLabel
            // 
            OpacityLabel.AutoSize = true;
            OpacityLabel.Location = new System.Drawing.Point(12, 12);
            OpacityLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            OpacityLabel.Name = "OpacityLabel";
            OpacityLabel.Size = new System.Drawing.Size(62, 18);
            OpacityLabel.TabIndex = 19;
            OpacityLabel.Text = "透明度";
            // 
            // ZoomTabPage
            // 
            this.ZoomTabPage.BackColor = System.Drawing.SystemColors.Control;
            this.ZoomTabPage.Controls.Add(ZoomSettingsPanel);
            this.ZoomTabPage.Location = new System.Drawing.Point(124, 4);
            this.ZoomTabPage.Margin = new System.Windows.Forms.Padding(4);
            this.ZoomTabPage.Name = "ZoomTabPage";
            this.ZoomTabPage.Size = new System.Drawing.Size(457, 353);
            this.ZoomTabPage.TabIndex = 2;
            this.ZoomTabPage.Text = "缩放";
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
            ZoomSettingsPanel.Margin = new System.Windows.Forms.Padding(4);
            ZoomSettingsPanel.Name = "ZoomSettingsPanel";
            ZoomSettingsPanel.Size = new System.Drawing.Size(457, 353);
            ZoomSettingsPanel.TabIndex = 36;
            // 
            // ZoomFactorLabel
            // 
            ZoomFactorLabel.AutoSize = true;
            ZoomFactorLabel.Location = new System.Drawing.Point(12, 46);
            ZoomFactorLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            ZoomFactorLabel.Name = "ZoomFactorLabel";
            ZoomFactorLabel.Size = new System.Drawing.Size(80, 18);
            ZoomFactorLabel.TabIndex = 39;
            ZoomFactorLabel.Text = "放大倍数";
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
            this.ZoomAnchorPanel.Location = new System.Drawing.Point(130, 75);
            this.ZoomAnchorPanel.Margin = new System.Windows.Forms.Padding(4);
            this.ZoomAnchorPanel.Name = "ZoomAnchorPanel";
            this.ZoomAnchorPanel.Size = new System.Drawing.Size(114, 100);
            this.ZoomAnchorPanel.TabIndex = 38;
            // 
            // ZoomAanchorNWRadioButton
            // 
            this.ZoomAanchorNWRadioButton.AutoSize = true;
            this.ZoomAanchorNWRadioButton.Location = new System.Drawing.Point(4, 4);
            this.ZoomAanchorNWRadioButton.Margin = new System.Windows.Forms.Padding(4);
            this.ZoomAanchorNWRadioButton.Name = "ZoomAanchorNWRadioButton";
            this.ZoomAanchorNWRadioButton.Size = new System.Drawing.Size(21, 20);
            this.ZoomAanchorNWRadioButton.TabIndex = 0;
            this.ZoomAanchorNWRadioButton.TabStop = true;
            this.ZoomAanchorNWRadioButton.UseVisualStyleBackColor = true;
            this.ZoomAanchorNWRadioButton.CheckedChanged += new System.EventHandler(this.OptionChanged_Handler);
            // 
            // ZoomAanchorNRadioButton
            // 
            this.ZoomAanchorNRadioButton.AutoSize = true;
            this.ZoomAanchorNRadioButton.Location = new System.Drawing.Point(46, 4);
            this.ZoomAanchorNRadioButton.Margin = new System.Windows.Forms.Padding(4);
            this.ZoomAanchorNRadioButton.Name = "ZoomAanchorNRadioButton";
            this.ZoomAanchorNRadioButton.Size = new System.Drawing.Size(21, 20);
            this.ZoomAanchorNRadioButton.TabIndex = 1;
            this.ZoomAanchorNRadioButton.TabStop = true;
            this.ZoomAanchorNRadioButton.UseVisualStyleBackColor = true;
            this.ZoomAanchorNRadioButton.CheckedChanged += new System.EventHandler(this.OptionChanged_Handler);
            // 
            // ZoomAanchorNERadioButton
            // 
            this.ZoomAanchorNERadioButton.AutoSize = true;
            this.ZoomAanchorNERadioButton.Location = new System.Drawing.Point(88, 4);
            this.ZoomAanchorNERadioButton.Margin = new System.Windows.Forms.Padding(4);
            this.ZoomAanchorNERadioButton.Name = "ZoomAanchorNERadioButton";
            this.ZoomAanchorNERadioButton.Size = new System.Drawing.Size(21, 20);
            this.ZoomAanchorNERadioButton.TabIndex = 2;
            this.ZoomAanchorNERadioButton.TabStop = true;
            this.ZoomAanchorNERadioButton.UseVisualStyleBackColor = true;
            this.ZoomAanchorNERadioButton.CheckedChanged += new System.EventHandler(this.OptionChanged_Handler);
            // 
            // ZoomAanchorWRadioButton
            // 
            this.ZoomAanchorWRadioButton.AutoSize = true;
            this.ZoomAanchorWRadioButton.Location = new System.Drawing.Point(4, 40);
            this.ZoomAanchorWRadioButton.Margin = new System.Windows.Forms.Padding(4);
            this.ZoomAanchorWRadioButton.Name = "ZoomAanchorWRadioButton";
            this.ZoomAanchorWRadioButton.Size = new System.Drawing.Size(21, 20);
            this.ZoomAanchorWRadioButton.TabIndex = 3;
            this.ZoomAanchorWRadioButton.TabStop = true;
            this.ZoomAanchorWRadioButton.UseVisualStyleBackColor = true;
            this.ZoomAanchorWRadioButton.CheckedChanged += new System.EventHandler(this.OptionChanged_Handler);
            // 
            // ZoomAanchorSERadioButton
            // 
            this.ZoomAanchorSERadioButton.AutoSize = true;
            this.ZoomAanchorSERadioButton.Location = new System.Drawing.Point(88, 76);
            this.ZoomAanchorSERadioButton.Margin = new System.Windows.Forms.Padding(4);
            this.ZoomAanchorSERadioButton.Name = "ZoomAanchorSERadioButton";
            this.ZoomAanchorSERadioButton.Size = new System.Drawing.Size(21, 20);
            this.ZoomAanchorSERadioButton.TabIndex = 8;
            this.ZoomAanchorSERadioButton.TabStop = true;
            this.ZoomAanchorSERadioButton.UseVisualStyleBackColor = true;
            this.ZoomAanchorSERadioButton.CheckedChanged += new System.EventHandler(this.OptionChanged_Handler);
            // 
            // ZoomAanchorCRadioButton
            // 
            this.ZoomAanchorCRadioButton.AutoSize = true;
            this.ZoomAanchorCRadioButton.Location = new System.Drawing.Point(46, 40);
            this.ZoomAanchorCRadioButton.Margin = new System.Windows.Forms.Padding(4);
            this.ZoomAanchorCRadioButton.Name = "ZoomAanchorCRadioButton";
            this.ZoomAanchorCRadioButton.Size = new System.Drawing.Size(21, 20);
            this.ZoomAanchorCRadioButton.TabIndex = 4;
            this.ZoomAanchorCRadioButton.TabStop = true;
            this.ZoomAanchorCRadioButton.UseVisualStyleBackColor = true;
            this.ZoomAanchorCRadioButton.CheckedChanged += new System.EventHandler(this.OptionChanged_Handler);
            // 
            // ZoomAanchorSRadioButton
            // 
            this.ZoomAanchorSRadioButton.AutoSize = true;
            this.ZoomAanchorSRadioButton.Location = new System.Drawing.Point(46, 76);
            this.ZoomAanchorSRadioButton.Margin = new System.Windows.Forms.Padding(4);
            this.ZoomAanchorSRadioButton.Name = "ZoomAanchorSRadioButton";
            this.ZoomAanchorSRadioButton.Size = new System.Drawing.Size(21, 20);
            this.ZoomAanchorSRadioButton.TabIndex = 7;
            this.ZoomAanchorSRadioButton.TabStop = true;
            this.ZoomAanchorSRadioButton.UseVisualStyleBackColor = true;
            this.ZoomAanchorSRadioButton.CheckedChanged += new System.EventHandler(this.OptionChanged_Handler);
            // 
            // ZoomAanchorERadioButton
            // 
            this.ZoomAanchorERadioButton.AutoSize = true;
            this.ZoomAanchorERadioButton.Location = new System.Drawing.Point(88, 40);
            this.ZoomAanchorERadioButton.Margin = new System.Windows.Forms.Padding(4);
            this.ZoomAanchorERadioButton.Name = "ZoomAanchorERadioButton";
            this.ZoomAanchorERadioButton.Size = new System.Drawing.Size(21, 20);
            this.ZoomAanchorERadioButton.TabIndex = 5;
            this.ZoomAanchorERadioButton.TabStop = true;
            this.ZoomAanchorERadioButton.UseVisualStyleBackColor = true;
            this.ZoomAanchorERadioButton.CheckedChanged += new System.EventHandler(this.OptionChanged_Handler);
            // 
            // ZoomAanchorSWRadioButton
            // 
            this.ZoomAanchorSWRadioButton.AutoSize = true;
            this.ZoomAanchorSWRadioButton.Location = new System.Drawing.Point(4, 76);
            this.ZoomAanchorSWRadioButton.Margin = new System.Windows.Forms.Padding(4);
            this.ZoomAanchorSWRadioButton.Name = "ZoomAanchorSWRadioButton";
            this.ZoomAanchorSWRadioButton.Size = new System.Drawing.Size(21, 20);
            this.ZoomAanchorSWRadioButton.TabIndex = 6;
            this.ZoomAanchorSWRadioButton.TabStop = true;
            this.ZoomAanchorSWRadioButton.UseVisualStyleBackColor = true;
            this.ZoomAanchorSWRadioButton.CheckedChanged += new System.EventHandler(this.OptionChanged_Handler);
            // 
            // ZoomAnchorLabel
            // 
            ZoomAnchorLabel.AutoSize = true;
            ZoomAnchorLabel.Location = new System.Drawing.Point(12, 79);
            ZoomAnchorLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            ZoomAnchorLabel.Name = "ZoomAnchorLabel";
            ZoomAnchorLabel.Size = new System.Drawing.Size(116, 18);
            ZoomAnchorLabel.TabIndex = 40;
            ZoomAnchorLabel.Text = "缩放起始位置";
            // 
            // EnableThumbnailZoomCheckBox
            // 
            this.EnableThumbnailZoomCheckBox.AutoSize = true;
            this.EnableThumbnailZoomCheckBox.Checked = true;
            this.EnableThumbnailZoomCheckBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.EnableThumbnailZoomCheckBox.Location = new System.Drawing.Point(12, 10);
            this.EnableThumbnailZoomCheckBox.Margin = new System.Windows.Forms.Padding(4);
            this.EnableThumbnailZoomCheckBox.Name = "EnableThumbnailZoomCheckBox";
            this.EnableThumbnailZoomCheckBox.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.EnableThumbnailZoomCheckBox.Size = new System.Drawing.Size(250, 22);
            this.EnableThumbnailZoomCheckBox.TabIndex = 36;
            this.EnableThumbnailZoomCheckBox.Text = "鼠标悬停时是否放大缩略图";
            this.EnableThumbnailZoomCheckBox.UseVisualStyleBackColor = true;
            this.EnableThumbnailZoomCheckBox.CheckedChanged += new System.EventHandler(this.OptionChanged_Handler);
            // 
            // ThumbnailZoomFactorNumericEdit
            // 
            this.ThumbnailZoomFactorNumericEdit.BackColor = System.Drawing.SystemColors.Window;
            this.ThumbnailZoomFactorNumericEdit.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.ThumbnailZoomFactorNumericEdit.Location = new System.Drawing.Point(130, 43);
            this.ThumbnailZoomFactorNumericEdit.Margin = new System.Windows.Forms.Padding(4);
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
            this.ThumbnailZoomFactorNumericEdit.Size = new System.Drawing.Size(57, 28);
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
            OverlayTabPage.Margin = new System.Windows.Forms.Padding(4);
            OverlayTabPage.Name = "OverlayTabPage";
            OverlayTabPage.Size = new System.Drawing.Size(457, 353);
            OverlayTabPage.TabIndex = 3;
            OverlayTabPage.Text = "叠加";
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
            OverlaySettingsPanel.Margin = new System.Windows.Forms.Padding(4);
            OverlaySettingsPanel.Name = "OverlaySettingsPanel";
            OverlaySettingsPanel.Size = new System.Drawing.Size(457, 353);
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
            this.groupBoxOverlayFont.Location = new System.Drawing.Point(12, 155);
            this.groupBoxOverlayFont.Margin = new System.Windows.Forms.Padding(4);
            this.groupBoxOverlayFont.Name = "groupBoxOverlayFont";
            this.groupBoxOverlayFont.Padding = new System.Windows.Forms.Padding(4);
            this.groupBoxOverlayFont.Size = new System.Drawing.Size(368, 183);
            this.groupBoxOverlayFont.TabIndex = 33;
            this.groupBoxOverlayFont.TabStop = false;
            this.groupBoxOverlayFont.Text = "标题颜色";
            // 
            // lblTitleOffsetTop
            // 
            this.lblTitleOffsetTop.AutoSize = true;
            this.lblTitleOffsetTop.Location = new System.Drawing.Point(138, 71);
            this.lblTitleOffsetTop.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblTitleOffsetTop.Name = "lblTitleOffsetTop";
            this.lblTitleOffsetTop.Size = new System.Drawing.Size(62, 18);
            this.lblTitleOffsetTop.TabIndex = 39;
            this.lblTitleOffsetTop.Text = "上边距";
            // 
            // txtTitleOffsetTop
            // 
            this.txtTitleOffsetTop.Location = new System.Drawing.Point(206, 66);
            this.txtTitleOffsetTop.Margin = new System.Windows.Forms.Padding(4);
            this.txtTitleOffsetTop.Name = "txtTitleOffsetTop";
            this.txtTitleOffsetTop.Size = new System.Drawing.Size(37, 28);
            this.txtTitleOffsetTop.TabIndex = 38;
            this.txtTitleOffsetTop.Text = "1";
            this.txtTitleOffsetTop.Enter += new System.EventHandler(this.txtTitleOffsetTop_Enter);
            this.txtTitleOffsetTop.Leave += new System.EventHandler(this.txtTitleOffsetTop_Leave);
            // 
            // lblTitleOffsetLeft
            // 
            this.lblTitleOffsetLeft.AutoSize = true;
            this.lblTitleOffsetLeft.Location = new System.Drawing.Point(16, 71);
            this.lblTitleOffsetLeft.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblTitleOffsetLeft.Name = "lblTitleOffsetLeft";
            this.lblTitleOffsetLeft.Size = new System.Drawing.Size(62, 18);
            this.lblTitleOffsetLeft.TabIndex = 37;
            this.lblTitleOffsetLeft.Text = "左边距";
            // 
            // txtTitleOffsetLeft
            // 
            this.txtTitleOffsetLeft.Location = new System.Drawing.Point(100, 66);
            this.txtTitleOffsetLeft.Margin = new System.Windows.Forms.Padding(4);
            this.txtTitleOffsetLeft.Name = "txtTitleOffsetLeft";
            this.txtTitleOffsetLeft.Size = new System.Drawing.Size(37, 28);
            this.txtTitleOffsetLeft.TabIndex = 36;
            this.txtTitleOffsetLeft.Text = "1";
            this.txtTitleOffsetLeft.Enter += new System.EventHandler(this.txtTitleOffsetLeft_Enter);
            this.txtTitleOffsetLeft.Leave += new System.EventHandler(this.txtTitleOffsetLeft_Leave);
            // 
            // lblTitleBorderWidth
            // 
            this.lblTitleBorderWidth.AutoSize = true;
            this.lblTitleBorderWidth.Location = new System.Drawing.Point(254, 71);
            this.lblTitleBorderWidth.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblTitleBorderWidth.Name = "lblTitleBorderWidth";
            this.lblTitleBorderWidth.Size = new System.Drawing.Size(44, 18);
            this.lblTitleBorderWidth.TabIndex = 35;
            this.lblTitleBorderWidth.Text = "描边";
            // 
            // txtFontOutlineWidth
            // 
            this.txtFontOutlineWidth.Location = new System.Drawing.Point(302, 66);
            this.txtFontOutlineWidth.Margin = new System.Windows.Forms.Padding(4);
            this.txtFontOutlineWidth.Name = "txtFontOutlineWidth";
            this.txtFontOutlineWidth.Size = new System.Drawing.Size(37, 28);
            this.txtFontOutlineWidth.TabIndex = 34;
            this.txtFontOutlineWidth.Text = "0";
            this.txtFontOutlineWidth.Enter += new System.EventHandler(this.txtFontOutlineWidth_Enter);
            this.txtFontOutlineWidth.Leave += new System.EventHandler(this.txtFontOutlineWidth_Leave);
            // 
            // btnFontOutlineColor
            // 
            this.btnFontOutlineColor.Font = new System.Drawing.Font("宋体", 8F);
            this.btnFontOutlineColor.Location = new System.Drawing.Point(244, 26);
            this.btnFontOutlineColor.Margin = new System.Windows.Forms.Padding(4);
            this.btnFontOutlineColor.Name = "btnFontOutlineColor";
            this.btnFontOutlineColor.Size = new System.Drawing.Size(114, 32);
            this.btnFontOutlineColor.TabIndex = 33;
            this.btnFontOutlineColor.Text = "描边颜色";
            this.btnFontOutlineColor.UseVisualStyleBackColor = true;
            this.btnFontOutlineColor.Click += new System.EventHandler(this.btnFontOutlineColor_Click);
            // 
            // btnSetOverlayFontColor
            // 
            this.btnSetOverlayFontColor.Font = new System.Drawing.Font("宋体", 8F);
            this.btnSetOverlayFontColor.Location = new System.Drawing.Point(126, 26);
            this.btnSetOverlayFontColor.Margin = new System.Windows.Forms.Padding(4);
            this.btnSetOverlayFontColor.Name = "btnSetOverlayFontColor";
            this.btnSetOverlayFontColor.Size = new System.Drawing.Size(88, 32);
            this.btnSetOverlayFontColor.TabIndex = 31;
            this.btnSetOverlayFontColor.Text = "字体颜色";
            this.btnSetOverlayFontColor.UseVisualStyleBackColor = true;
            this.btnSetOverlayFontColor.Click += new System.EventHandler(this.btnSetOverlayFontColor_Click);
            // 
            // lblDisplaySampleFont
            // 
            this.lblDisplaySampleFont.AutoSize = true;
            this.lblDisplaySampleFont.BackColor = System.Drawing.SystemColors.Control;
            this.lblDisplaySampleFont.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(128)))), ((int)(((byte)(0)))));
            this.lblDisplaySampleFont.Location = new System.Drawing.Point(9, 105);
            this.lblDisplaySampleFont.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblDisplaySampleFont.Name = "lblDisplaySampleFont";
            this.lblDisplaySampleFont.OutlineColor = System.Drawing.Color.White;
            this.lblDisplaySampleFont.OutlineWidth = 1F;
            this.lblDisplaySampleFont.Size = new System.Drawing.Size(71, 18);
            this.lblDisplaySampleFont.TabIndex = 32;
            this.lblDisplaySampleFont.Text = "Sample.";
            // 
            // btnSetOverlayFont
            // 
            this.btnSetOverlayFont.Font = new System.Drawing.Font("宋体", 8F);
            this.btnSetOverlayFont.Location = new System.Drawing.Point(9, 26);
            this.btnSetOverlayFont.Margin = new System.Windows.Forms.Padding(4);
            this.btnSetOverlayFont.Name = "btnSetOverlayFont";
            this.btnSetOverlayFont.Size = new System.Drawing.Size(84, 32);
            this.btnSetOverlayFont.TabIndex = 30;
            this.btnSetOverlayFont.Text = "设置字体";
            this.btnSetOverlayFont.UseVisualStyleBackColor = true;
            this.btnSetOverlayFont.Click += new System.EventHandler(this.btnSetOverlayFont_Click);
            // 
            // HighlightColorLabel
            // 
            this.HighlightColorLabel.AutoSize = true;
            this.HighlightColorLabel.Location = new System.Drawing.Point(8, 108);
            this.HighlightColorLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.HighlightColorLabel.Name = "HighlightColorLabel";
            this.HighlightColorLabel.Size = new System.Drawing.Size(116, 18);
            this.HighlightColorLabel.TabIndex = 29;
            this.HighlightColorLabel.Text = "高亮边框颜色";
            // 
            // ActiveClientHighlightColorButton
            // 
            this.ActiveClientHighlightColorButton.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.ActiveClientHighlightColorButton.Location = new System.Drawing.Point(130, 107);
            this.ActiveClientHighlightColorButton.Margin = new System.Windows.Forms.Padding(4);
            this.ActiveClientHighlightColorButton.Name = "ActiveClientHighlightColorButton";
            this.ActiveClientHighlightColorButton.Size = new System.Drawing.Size(138, 23);
            this.ActiveClientHighlightColorButton.TabIndex = 28;
            this.ActiveClientHighlightColorButton.Click += new System.EventHandler(this.ActiveClientHighlightColorButton_Click);
            // 
            // EnableActiveClientHighlightCheckBox
            // 
            this.EnableActiveClientHighlightCheckBox.AutoSize = true;
            this.EnableActiveClientHighlightCheckBox.Checked = true;
            this.EnableActiveClientHighlightCheckBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.EnableActiveClientHighlightCheckBox.Location = new System.Drawing.Point(12, 76);
            this.EnableActiveClientHighlightCheckBox.Margin = new System.Windows.Forms.Padding(4);
            this.EnableActiveClientHighlightCheckBox.Name = "EnableActiveClientHighlightCheckBox";
            this.EnableActiveClientHighlightCheckBox.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.EnableActiveClientHighlightCheckBox.Size = new System.Drawing.Size(196, 22);
            this.EnableActiveClientHighlightCheckBox.TabIndex = 27;
            this.EnableActiveClientHighlightCheckBox.Text = "是否高亮当前客户端";
            this.EnableActiveClientHighlightCheckBox.UseVisualStyleBackColor = true;
            this.EnableActiveClientHighlightCheckBox.CheckedChanged += new System.EventHandler(this.OptionChanged_Handler);
            // 
            // ShowThumbnailOverlaysCheckBox
            // 
            this.ShowThumbnailOverlaysCheckBox.AutoSize = true;
            this.ShowThumbnailOverlaysCheckBox.Checked = true;
            this.ShowThumbnailOverlaysCheckBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.ShowThumbnailOverlaysCheckBox.Location = new System.Drawing.Point(12, 10);
            this.ShowThumbnailOverlaysCheckBox.Margin = new System.Windows.Forms.Padding(4);
            this.ShowThumbnailOverlaysCheckBox.Name = "ShowThumbnailOverlaysCheckBox";
            this.ShowThumbnailOverlaysCheckBox.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.ShowThumbnailOverlaysCheckBox.Size = new System.Drawing.Size(196, 22);
            this.ShowThumbnailOverlaysCheckBox.TabIndex = 25;
            this.ShowThumbnailOverlaysCheckBox.Text = "是否显示客户端名称";
            this.ShowThumbnailOverlaysCheckBox.UseVisualStyleBackColor = true;
            this.ShowThumbnailOverlaysCheckBox.CheckedChanged += new System.EventHandler(this.OptionChanged_Handler);
            // 
            // ShowThumbnailFramesCheckBox
            // 
            this.ShowThumbnailFramesCheckBox.AutoSize = true;
            this.ShowThumbnailFramesCheckBox.Checked = true;
            this.ShowThumbnailFramesCheckBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.ShowThumbnailFramesCheckBox.Location = new System.Drawing.Point(12, 43);
            this.ShowThumbnailFramesCheckBox.Margin = new System.Windows.Forms.Padding(4);
            this.ShowThumbnailFramesCheckBox.Name = "ShowThumbnailFramesCheckBox";
            this.ShowThumbnailFramesCheckBox.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.ShowThumbnailFramesCheckBox.Size = new System.Drawing.Size(232, 22);
            this.ShowThumbnailFramesCheckBox.TabIndex = 26;
            this.ShowThumbnailFramesCheckBox.Text = "是否显示窗口标题和边框";
            this.ShowThumbnailFramesCheckBox.UseVisualStyleBackColor = true;
            this.ShowThumbnailFramesCheckBox.CheckedChanged += new System.EventHandler(this.OptionChanged_Handler);
            // 
            // ClientsTabPage
            // 
            ClientsTabPage.BackColor = System.Drawing.SystemColors.Control;
            ClientsTabPage.Controls.Add(ClientsPanel);
            ClientsTabPage.Location = new System.Drawing.Point(124, 4);
            ClientsTabPage.Margin = new System.Windows.Forms.Padding(4);
            ClientsTabPage.Name = "ClientsTabPage";
            ClientsTabPage.Size = new System.Drawing.Size(457, 353);
            ClientsTabPage.TabIndex = 4;
            ClientsTabPage.Text = "活动客户端";
            // 
            // ClientsPanel
            // 
            ClientsPanel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            ClientsPanel.Controls.Add(this.activeClientsSplitContainer);
            ClientsPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            ClientsPanel.Location = new System.Drawing.Point(0, 0);
            ClientsPanel.Margin = new System.Windows.Forms.Padding(4);
            ClientsPanel.Name = "ClientsPanel";
            ClientsPanel.Size = new System.Drawing.Size(457, 353);
            ClientsPanel.TabIndex = 32;
            // 
            // activeClientsSplitContainer
            // 
            this.activeClientsSplitContainer.Dock = System.Windows.Forms.DockStyle.Fill;
            this.activeClientsSplitContainer.Location = new System.Drawing.Point(0, 0);
            this.activeClientsSplitContainer.Margin = new System.Windows.Forms.Padding(4);
            this.activeClientsSplitContainer.Name = "activeClientsSplitContainer";
            this.activeClientsSplitContainer.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // activeClientsSplitContainer.Panel1
            // 
            this.activeClientsSplitContainer.Panel1.Controls.Add(ThumbnailsListLabel);
            // 
            // activeClientsSplitContainer.Panel2
            // 
            this.activeClientsSplitContainer.Panel2.Controls.Add(this.ThumbnailsList);
            this.activeClientsSplitContainer.Panel2.Controls.Add(this.lblClientNote);
            this.activeClientsSplitContainer.Panel2.Controls.Add(this.txtClientNote);
            this.activeClientsSplitContainer.Size = new System.Drawing.Size(455, 351);
            this.activeClientsSplitContainer.SplitterDistance = 34;
            this.activeClientsSplitContainer.SplitterWidth = 6;
            this.activeClientsSplitContainer.TabIndex = 35;
            // 
            // ThumbnailsListLabel
            // 
            ThumbnailsListLabel.AutoSize = true;
            ThumbnailsListLabel.Location = new System.Drawing.Point(9, 10);
            ThumbnailsListLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            ThumbnailsListLabel.Name = "ThumbnailsListLabel";
            ThumbnailsListLabel.Size = new System.Drawing.Size(386, 18);
            ThumbnailsListLabel.TabIndex = 33;
            ThumbnailsListLabel.Text = "当前客户端缩略图列表。勾选可隐藏缩略图";
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
            this.ThumbnailsList.Margin = new System.Windows.Forms.Padding(4);
            this.ThumbnailsList.Name = "ThumbnailsList";
            this.ThumbnailsList.Size = new System.Drawing.Size(455, 248);
            this.ThumbnailsList.TabIndex = 34;
            this.ThumbnailsList.ItemCheck += new System.Windows.Forms.ItemCheckEventHandler(this.ThumbnailsList_ItemCheck_Handler);
            this.ThumbnailsList.SelectedIndexChanged += new System.EventHandler(this.ThumbnailsList_SelectedIndexChanged);

            // 
            // lblClientNote
            // 
            this.lblClientNote.AutoSize = true;
            this.lblClientNote.Location = new System.Drawing.Point(9, 260);
            this.lblClientNote.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblClientNote.Name = "lblClientNote";
            this.lblClientNote.Size = new System.Drawing.Size(70, 18);
            this.lblClientNote.TabIndex = 35;
            this.lblClientNote.Text = "备注:";
            // 
            // txtClientNote
            // 
            this.txtClientNote.Location = new System.Drawing.Point(60, 255);
            this.txtClientNote.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.txtClientNote.Name = "txtClientNote";
            this.txtClientNote.Size = new System.Drawing.Size(380, 28);
            this.txtClientNote.TabIndex = 36;
            this.txtClientNote.Leave += new System.EventHandler(this.txtClientNote_Leave);
            // 
            // CycleGroupTabPage
            // 
            CycleGroupTabPage.Controls.Add(this.CycleGroupPanel);
            CycleGroupTabPage.Location = new System.Drawing.Point(124, 4);
            CycleGroupTabPage.Margin = new System.Windows.Forms.Padding(4);
            CycleGroupTabPage.Name = "CycleGroupTabPage";
            CycleGroupTabPage.Size = new System.Drawing.Size(457, 353);
            CycleGroupTabPage.TabIndex = 6;
            CycleGroupTabPage.Text = "循环组";
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
            this.CycleGroupPanel.Margin = new System.Windows.Forms.Padding(4);
            this.CycleGroupPanel.Name = "CycleGroupPanel";
            this.CycleGroupPanel.Size = new System.Drawing.Size(457, 353);
            this.CycleGroupPanel.TabIndex = 0;
            // 
            // removeGroupButton
            // 
            this.removeGroupButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.removeGroupButton.Location = new System.Drawing.Point(300, 30);
            this.removeGroupButton.Margin = new System.Windows.Forms.Padding(4);
            this.removeGroupButton.Name = "removeGroupButton";
            this.removeGroupButton.Size = new System.Drawing.Size(36, 32);
            this.removeGroupButton.TabIndex = 16;
            this.removeGroupButton.Text = "-";
            this.removeGroupButton.UseVisualStyleBackColor = true;
            this.removeGroupButton.Click += new System.EventHandler(this.removeGroupButton_Click);
            // 
            // cycleGroupMoveClientOrderUpButton
            // 
            this.cycleGroupMoveClientOrderUpButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.cycleGroupMoveClientOrderUpButton.Location = new System.Drawing.Point(248, 179);
            this.cycleGroupMoveClientOrderUpButton.Margin = new System.Windows.Forms.Padding(4);
            this.cycleGroupMoveClientOrderUpButton.Name = "cycleGroupMoveClientOrderUpButton";
            this.cycleGroupMoveClientOrderUpButton.Size = new System.Drawing.Size(50, 32);
            this.cycleGroupMoveClientOrderUpButton.TabIndex = 15;
            this.cycleGroupMoveClientOrderUpButton.Text = "Up";
            this.cycleGroupMoveClientOrderUpButton.UseVisualStyleBackColor = true;
            this.cycleGroupMoveClientOrderUpButton.Click += new System.EventHandler(this.cycleGroupMoveClientOrderUpButton_Click);
            // 
            // removeClientToCycleGroupButton
            // 
            this.removeClientToCycleGroupButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.removeClientToCycleGroupButton.Location = new System.Drawing.Point(300, 179);
            this.removeClientToCycleGroupButton.Margin = new System.Windows.Forms.Padding(4);
            this.removeClientToCycleGroupButton.Name = "removeClientToCycleGroupButton";
            this.removeClientToCycleGroupButton.Size = new System.Drawing.Size(33, 32);
            this.removeClientToCycleGroupButton.TabIndex = 14;
            this.removeClientToCycleGroupButton.Text = "-";
            this.removeClientToCycleGroupButton.UseVisualStyleBackColor = true;
            this.removeClientToCycleGroupButton.Click += new System.EventHandler(this.removeClientToCycleGroupButton_Click);
            // 
            // addNewGroupButton
            // 
            this.addNewGroupButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.addNewGroupButton.Location = new System.Drawing.Point(334, 30);
            this.addNewGroupButton.Margin = new System.Windows.Forms.Padding(4);
            this.addNewGroupButton.Name = "addNewGroupButton";
            this.addNewGroupButton.Size = new System.Drawing.Size(36, 32);
            this.addNewGroupButton.TabIndex = 13;
            this.addNewGroupButton.Text = "+";
            this.addNewGroupButton.UseVisualStyleBackColor = true;
            this.addNewGroupButton.Click += new System.EventHandler(this.addNewGroupButton_Click);
            // 
            // cycleGroupClientOrderLabel
            // 
            this.cycleGroupClientOrderLabel.AutoSize = true;
            this.cycleGroupClientOrderLabel.Location = new System.Drawing.Point(4, 186);
            this.cycleGroupClientOrderLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.cycleGroupClientOrderLabel.Name = "cycleGroupClientOrderLabel";
            this.cycleGroupClientOrderLabel.Size = new System.Drawing.Size(116, 18);
            this.cycleGroupClientOrderLabel.TabIndex = 12;
            this.cycleGroupClientOrderLabel.Text = "客户端及排序";
            // 
            // cycleGroupClientOrderList
            // 
            this.cycleGroupClientOrderList.FormattingEnabled = true;
            this.cycleGroupClientOrderList.ItemHeight = 18;
            this.cycleGroupClientOrderList.Location = new System.Drawing.Point(9, 212);
            this.cycleGroupClientOrderList.Margin = new System.Windows.Forms.Padding(4);
            this.cycleGroupClientOrderList.Name = "cycleGroupClientOrderList";
            this.cycleGroupClientOrderList.Size = new System.Drawing.Size(356, 130);
            this.cycleGroupClientOrderList.TabIndex = 11;
            // 
            // cycleGroupBackwardHotkey2Text
            // 
            this.cycleGroupBackwardHotkey2Text.BackColor = System.Drawing.SystemColors.Control;
            this.cycleGroupBackwardHotkey2Text.Location = new System.Drawing.Point(248, 143);
            this.cycleGroupBackwardHotkey2Text.Margin = new System.Windows.Forms.Padding(4);
            this.cycleGroupBackwardHotkey2Text.Name = "cycleGroupBackwardHotkey2Text";
            this.cycleGroupBackwardHotkey2Text.ReadOnly = true;
            this.cycleGroupBackwardHotkey2Text.Size = new System.Drawing.Size(118, 28);
            this.cycleGroupBackwardHotkey2Text.TabIndex = 10;
            this.cycleGroupBackwardHotkey2Text.DoubleClick += new System.EventHandler(this.cycleGroupBackwardHotkey2Text_DoubleClick);
            // 
            // cycleGroupBackwardHotkey1Text
            // 
            this.cycleGroupBackwardHotkey1Text.BackColor = System.Drawing.SystemColors.Control;
            this.cycleGroupBackwardHotkey1Text.Location = new System.Drawing.Point(123, 143);
            this.cycleGroupBackwardHotkey1Text.Margin = new System.Windows.Forms.Padding(4);
            this.cycleGroupBackwardHotkey1Text.Name = "cycleGroupBackwardHotkey1Text";
            this.cycleGroupBackwardHotkey1Text.ReadOnly = true;
            this.cycleGroupBackwardHotkey1Text.Size = new System.Drawing.Size(118, 28);
            this.cycleGroupBackwardHotkey1Text.TabIndex = 9;
            this.cycleGroupBackwardHotkey1Text.DoubleClick += new System.EventHandler(this.cycleGroupBackwardHotkey1Text_DoubleClick);
            // 
            // cycleGroupBackHotkeyLabel
            // 
            this.cycleGroupBackHotkeyLabel.AutoSize = true;
            this.cycleGroupBackHotkeyLabel.Location = new System.Drawing.Point(4, 147);
            this.cycleGroupBackHotkeyLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.cycleGroupBackHotkeyLabel.Name = "cycleGroupBackHotkeyLabel";
            this.cycleGroupBackHotkeyLabel.Size = new System.Drawing.Size(98, 18);
            this.cycleGroupBackHotkeyLabel.TabIndex = 8;
            this.cycleGroupBackHotkeyLabel.Text = "向前快捷键";
            // 
            // cycleGroupForwardHotkey2Text
            // 
            this.cycleGroupForwardHotkey2Text.BackColor = System.Drawing.SystemColors.Control;
            this.cycleGroupForwardHotkey2Text.Location = new System.Drawing.Point(248, 107);
            this.cycleGroupForwardHotkey2Text.Margin = new System.Windows.Forms.Padding(4);
            this.cycleGroupForwardHotkey2Text.Name = "cycleGroupForwardHotkey2Text";
            this.cycleGroupForwardHotkey2Text.ReadOnly = true;
            this.cycleGroupForwardHotkey2Text.Size = new System.Drawing.Size(118, 28);
            this.cycleGroupForwardHotkey2Text.TabIndex = 7;
            this.cycleGroupForwardHotkey2Text.DoubleClick += new System.EventHandler(this.cycleGroupForwardHotkey2Text_DoubleClick);
            // 
            // cycleGroupForwardHotkey1Text
            // 
            this.cycleGroupForwardHotkey1Text.BackColor = System.Drawing.SystemColors.Control;
            this.cycleGroupForwardHotkey1Text.Location = new System.Drawing.Point(123, 107);
            this.cycleGroupForwardHotkey1Text.Margin = new System.Windows.Forms.Padding(4);
            this.cycleGroupForwardHotkey1Text.Name = "cycleGroupForwardHotkey1Text";
            this.cycleGroupForwardHotkey1Text.ReadOnly = true;
            this.cycleGroupForwardHotkey1Text.Size = new System.Drawing.Size(118, 28);
            this.cycleGroupForwardHotkey1Text.TabIndex = 6;
            this.cycleGroupForwardHotkey1Text.DoubleClick += new System.EventHandler(this.cycleGroupForwardHotkey1Text_DoubleClick);
            // 
            // cycleGroupForwardHotkeyLabel
            // 
            this.cycleGroupForwardHotkeyLabel.AutoSize = true;
            this.cycleGroupForwardHotkeyLabel.Location = new System.Drawing.Point(4, 111);
            this.cycleGroupForwardHotkeyLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.cycleGroupForwardHotkeyLabel.Name = "cycleGroupForwardHotkeyLabel";
            this.cycleGroupForwardHotkeyLabel.Size = new System.Drawing.Size(98, 18);
            this.cycleGroupForwardHotkeyLabel.TabIndex = 5;
            this.cycleGroupForwardHotkeyLabel.Text = "向后快捷键";
            // 
            // cycleGroupDescriptionText
            // 
            this.cycleGroupDescriptionText.Location = new System.Drawing.Point(123, 71);
            this.cycleGroupDescriptionText.Margin = new System.Windows.Forms.Padding(4);
            this.cycleGroupDescriptionText.Name = "cycleGroupDescriptionText";
            this.cycleGroupDescriptionText.Size = new System.Drawing.Size(242, 28);
            this.cycleGroupDescriptionText.TabIndex = 4;
            this.cycleGroupDescriptionText.Leave += new System.EventHandler(this.cycleGroupDescriptionText_Leave);
            // 
            // cycleGroupDescriptionLabel
            // 
            this.cycleGroupDescriptionLabel.AutoSize = true;
            this.cycleGroupDescriptionLabel.Location = new System.Drawing.Point(4, 75);
            this.cycleGroupDescriptionLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.cycleGroupDescriptionLabel.Name = "cycleGroupDescriptionLabel";
            this.cycleGroupDescriptionLabel.Size = new System.Drawing.Size(62, 18);
            this.cycleGroupDescriptionLabel.TabIndex = 3;
            this.cycleGroupDescriptionLabel.Text = "备注：";
            // 
            // CycleGroupLabel
            // 
            this.CycleGroupLabel.AutoSize = true;
            this.CycleGroupLabel.Location = new System.Drawing.Point(4, 10);
            this.CycleGroupLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.CycleGroupLabel.Name = "CycleGroupLabel";
            this.CycleGroupLabel.Size = new System.Drawing.Size(98, 18);
            this.CycleGroupLabel.TabIndex = 2;
            this.CycleGroupLabel.Text = "选择循环组";
            // 
            // selectCycleGroupComboBox
            // 
            this.selectCycleGroupComboBox.FormattingEnabled = true;
            this.selectCycleGroupComboBox.Location = new System.Drawing.Point(9, 32);
            this.selectCycleGroupComboBox.Margin = new System.Windows.Forms.Padding(4);
            this.selectCycleGroupComboBox.Name = "selectCycleGroupComboBox";
            this.selectCycleGroupComboBox.Size = new System.Drawing.Size(286, 26);
            this.selectCycleGroupComboBox.TabIndex = 1;
            this.selectCycleGroupComboBox.SelectedValueChanged += new System.EventHandler(this.selectCycleGroupComboBox_SelectedValueChanged);
            // 
            // addClientToCycleGroupButton
            // 
            this.addClientToCycleGroupButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.addClientToCycleGroupButton.Location = new System.Drawing.Point(334, 179);
            this.addClientToCycleGroupButton.Margin = new System.Windows.Forms.Padding(4);
            this.addClientToCycleGroupButton.Name = "addClientToCycleGroupButton";
            this.addClientToCycleGroupButton.Size = new System.Drawing.Size(33, 32);
            this.addClientToCycleGroupButton.TabIndex = 0;
            this.addClientToCycleGroupButton.Text = "+";
            this.addClientToCycleGroupButton.UseVisualStyleBackColor = true;
            this.addClientToCycleGroupButton.Click += new System.EventHandler(this.addClientToCycleGroupButton_Click);
            // 
            // AboutTabPage
            // 
            AboutTabPage.BackColor = System.Drawing.SystemColors.Control;
            AboutTabPage.Controls.Add(AboutPanel);
            AboutTabPage.Location = new System.Drawing.Point(124, 4);
            AboutTabPage.Margin = new System.Windows.Forms.Padding(4);
            AboutTabPage.Name = "AboutTabPage";
            AboutTabPage.Size = new System.Drawing.Size(457, 353);
            AboutTabPage.TabIndex = 5;
            AboutTabPage.Text = "关于";
            // 
            // AboutPanel
            // 
            AboutPanel.BackColor = System.Drawing.Color.Transparent;
            AboutPanel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            AboutPanel.Controls.Add(DocumentationLinkLabel);
            AboutPanel.Controls.Add(DescriptionLabel);
            AboutPanel.Controls.Add(this.VersionLabel);
            AboutPanel.Controls.Add(NameLabel);
            AboutPanel.Controls.Add(this.DocumentationLink);
            AboutPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            AboutPanel.Location = new System.Drawing.Point(0, 0);
            AboutPanel.Margin = new System.Windows.Forms.Padding(4);
            AboutPanel.Name = "AboutPanel";
            AboutPanel.Size = new System.Drawing.Size(457, 353);
            AboutPanel.TabIndex = 2;
            // 
            // DocumentationLinkLabel
            // 
            DocumentationLinkLabel.AutoSize = true;
            DocumentationLinkLabel.Location = new System.Drawing.Point(0, 220);
            DocumentationLinkLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            DocumentationLinkLabel.Name = "DocumentationLinkLabel";
            DocumentationLinkLabel.Padding = new System.Windows.Forms.Padding(12, 4, 12, 4);
            DocumentationLinkLabel.Size = new System.Drawing.Size(383, 26);
            DocumentationLinkLabel.TabIndex = 6;
            DocumentationLinkLabel.Text = "For more information visit our discord:";
            // 
            // DescriptionLabel
            // 
            DescriptionLabel.BackColor = System.Drawing.Color.Transparent;
            DescriptionLabel.Location = new System.Drawing.Point(0, 40);
            DescriptionLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            DescriptionLabel.Name = "DescriptionLabel";
            DescriptionLabel.Padding = new System.Windows.Forms.Padding(12, 4, 12, 4);
            DescriptionLabel.Size = new System.Drawing.Size(392, 201);
            DescriptionLabel.TabIndex = 5;
            DescriptionLabel.Text = "该汉化版由AMIYA-老余汉化";
            // 
            // VersionLabel
            // 
            this.VersionLabel.AutoSize = true;
            this.VersionLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.VersionLabel.Location = new System.Drawing.Point(200, 12);
            this.VersionLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.VersionLabel.Name = "VersionLabel";
            this.VersionLabel.Size = new System.Drawing.Size(69, 29);
            this.VersionLabel.TabIndex = 4;
            this.VersionLabel.Text = "1.0.0";
            // 
            // NameLabel
            // 
            NameLabel.AutoSize = true;
            NameLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            NameLabel.Location = new System.Drawing.Point(6, 12);
            NameLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            NameLabel.Name = "NameLabel";
            NameLabel.Size = new System.Drawing.Size(193, 29);
            NameLabel.TabIndex = 3;
            NameLabel.Text = "EVE-O Preview";
            // 
            // DocumentationLink
            // 
            this.DocumentationLink.Location = new System.Drawing.Point(0, 245);
            this.DocumentationLink.Margin = new System.Windows.Forms.Padding(45, 4, 4, 4);
            this.DocumentationLink.Name = "DocumentationLink";
            this.DocumentationLink.Padding = new System.Windows.Forms.Padding(12, 4, 12, 4);
            this.DocumentationLink.Size = new System.Drawing.Size(393, 46);
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
            this.TrayMenu.Size = new System.Drawing.Size(207, 100);
            // 
            // MainForm
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.BackColor = System.Drawing.SystemColors.Control;
            this.ClientSize = new System.Drawing.Size(585, 361);
            this.Controls.Add(ContentTabControl);
            this.ForeColor = System.Drawing.SystemColors.ControlText;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(0);
            this.MaximizeBox = false;
            this.Name = "MainForm";
            this.Text = "EVE-O Preview（汉化版）";
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
        private System.Windows.Forms.Label lblClientNote;
        private System.Windows.Forms.TextBox txtClientNote;
    }
}
