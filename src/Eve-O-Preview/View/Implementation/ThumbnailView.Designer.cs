using EveOPreview.View.CustomControl;

namespace EveOPreview.View
{
    partial class ThumbnailView
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.thumbnailContextMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.menuMinimize = new System.Windows.Forms.ToolStripMenuItem();
            this.menuReposition = new System.Windows.Forms.ToolStripMenuItem();
            this.resizeThumbnailToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.minimizeAllToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.holdRightClickToMoveTimer = new System.Windows.Forms.Timer(this.components);
            this.thumbnailContextMenu.SuspendLayout();
            this.SuspendLayout();
            // 
            // thumbnailContextMenu
            // 
            this.thumbnailContextMenu.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(20)))), ((int)(((byte)(20)))), ((int)(((byte)(22)))));
            this.thumbnailContextMenu.Font = new System.Drawing.Font("Segoe UI Semibold", 9.5F);
            this.thumbnailContextMenu.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(212)))), ((int)(((byte)(175)))), ((int)(((byte)(55)))));
            this.thumbnailContextMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.menuMinimize,
            this.minimizeAllToolStripMenuItem,
            this.toolStripSeparator1,
            this.menuReposition,
            this.resizeThumbnailToolStripMenuItem});
            this.thumbnailContextMenu.Name = "thumbnailContextMenu";
            this.thumbnailContextMenu.ShowImageMargin = false;
            this.thumbnailContextMenu.Size = new System.Drawing.Size(125, 98);
            // 
            // menuMinimize
            // 
            this.menuMinimize.Name = "menuMinimize";
            this.menuMinimize.Size = new System.Drawing.Size(124, 22);
            this.menuMinimize.Text = "Minimize";
            this.menuMinimize.Click += new System.EventHandler(this.menuMinimize_Click);
            // 
            // menuReposition
            // 
            this.menuReposition.Name = "menuReposition";
            this.menuReposition.Size = new System.Drawing.Size(124, 22);
            this.menuReposition.Text = "Move";
            this.menuReposition.Click += new System.EventHandler(this.menuReposition_Click);
            // 
            // resizeThumbnailToolStripMenuItem
            // 
            this.resizeThumbnailToolStripMenuItem.Name = "resizeThumbnailToolStripMenuItem";
            this.resizeThumbnailToolStripMenuItem.Size = new System.Drawing.Size(124, 22);
            this.resizeThumbnailToolStripMenuItem.Text = "Resize";
            this.resizeThumbnailToolStripMenuItem.ToolTipText = "Hold Shift to maintain aspect ratio while resizing";
            this.resizeThumbnailToolStripMenuItem.Click += new System.EventHandler(this.resizeThumbnailToolStripMenuItem_Click);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(121, 6);
            // 
            // minimizeAllToolStripMenuItem
            // 
            this.minimizeAllToolStripMenuItem.Name = "minimizeAllToolStripMenuItem";
            this.minimizeAllToolStripMenuItem.Size = new System.Drawing.Size(124, 22);
            this.minimizeAllToolStripMenuItem.Text = "Minimize All";
            this.minimizeAllToolStripMenuItem.Click += new System.EventHandler(this.minimizeAllToolStripMenuItem_Click);
            // 
            // holdRightClickToMoveTimer
            // 
            this.holdRightClickToMoveTimer.Interval = 350;
            this.holdRightClickToMoveTimer.Tick += new System.EventHandler(this.holdRightClickToMoveTimer_Tick);
            // 
            // ThumbnailView
            // 
            this.AccessibleRole = System.Windows.Forms.AccessibleRole.None;
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.BackColor = System.Drawing.Color.Black;
            this.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.ClientSize = new System.Drawing.Size(153, 89);
            this.ControlBox = false;
            this.DoubleBuffered = true;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.SizableToolWindow;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(64, 64);
            this.Name = "ThumbnailView";
            this.Opacity = 0.1D;
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.Text = "Preview";
            this.TopMost = true;
            this.MouseDown += new System.Windows.Forms.MouseEventHandler(this.MouseDown_Handler);
            this.MouseEnter += new System.EventHandler(this.MouseEnter_Handler);
            this.MouseLeave += new System.EventHandler(this.MouseLeave_Handler);
            this.MouseMove += new System.Windows.Forms.MouseEventHandler(this.MouseMove_Handler);
            this.MouseUp += new System.Windows.Forms.MouseEventHandler(this.MouseUp_Handler);
            this.Move += new System.EventHandler(this.Move_Handler);
            this.Resize += new System.EventHandler(this.Resize_Handler);
            this.thumbnailContextMenu.ResumeLayout(false);
            this.ResumeLayout(false);

        }


        #endregion

        private System.Windows.Forms.ContextMenuStrip thumbnailContextMenu;
        private System.ComponentModel.IContainer components;
        private System.Windows.Forms.ToolStripMenuItem menuMinimize;
        private System.Windows.Forms.ToolStripMenuItem menuReposition;
        private System.Windows.Forms.ToolStripMenuItem resizeThumbnailToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripMenuItem minimizeAllToolStripMenuItem;
        private System.Windows.Forms.ToolTip toolTip1;
        private System.Windows.Forms.Timer holdRightClickToMoveTimer;
    }
}