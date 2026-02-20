using System.Collections.Generic;
using System.Windows.Forms;

namespace EveOPreview.View
{
    partial class ClientNameInputBox : Form, IClientNameInputBoxView
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        public bool IsWaitingOnUserInput { get; set; }

        public string SelectedClientName { get; set; }

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

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.listOfAllClients = new System.Windows.Forms.ListBox();
            this.selectedClientNameTextBox = new System.Windows.Forms.TextBox();
            this.acceptSelectionButton = new System.Windows.Forms.Button();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.panel1 = new System.Windows.Forms.Panel();
            this.panel2 = new System.Windows.Forms.Panel();
            this.tableLayoutPanel1.SuspendLayout();
            this.panel1.SuspendLayout();
            this.panel2.SuspendLayout();
            this.SuspendLayout();
            // 
            // listOfAllClients
            // 
            this.listOfAllClients.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listOfAllClients.FormattingEnabled = true;
            this.listOfAllClients.Location = new System.Drawing.Point(0, 0);
            this.listOfAllClients.Name = "listOfAllClients";
            this.listOfAllClients.Size = new System.Drawing.Size(217, 228);
            this.listOfAllClients.TabIndex = 0;
            this.listOfAllClients.SelectedValueChanged += new System.EventHandler(this.listOfAllClients_SelectedValueChanged);
            this.listOfAllClients.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.listOfAllClients_MouseDoubleClick);
            // 
            // selectedClientNameTextBox
            // 
            this.selectedClientNameTextBox.Location = new System.Drawing.Point(9, 3);
            this.selectedClientNameTextBox.Name = "selectedClientNameTextBox";
            this.selectedClientNameTextBox.Size = new System.Drawing.Size(198, 20);
            this.selectedClientNameTextBox.TabIndex = 1;
            // 
            // acceptSelectionButton
            // 
            this.acceptSelectionButton.Location = new System.Drawing.Point(9, 30);
            this.acceptSelectionButton.Name = "acceptSelectionButton";
            this.acceptSelectionButton.Size = new System.Drawing.Size(198, 23);
            this.acceptSelectionButton.TabIndex = 2;
            this.acceptSelectionButton.Text = "Select";
            this.acceptSelectionButton.UseVisualStyleBackColor = true;
            this.acceptSelectionButton.Click += new System.EventHandler(this.acceptSelectionButton_Click);
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 1;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Controls.Add(this.panel1, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.panel2, 0, 1);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 2;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 63F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(223, 297);
            this.tableLayoutPanel1.TabIndex = 3;
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.listOfAllClients);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel1.Location = new System.Drawing.Point(3, 3);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(217, 228);
            this.panel1.TabIndex = 0;
            // 
            // panel2
            // 
            this.panel2.Controls.Add(this.selectedClientNameTextBox);
            this.panel2.Controls.Add(this.acceptSelectionButton);
            this.panel2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel2.Location = new System.Drawing.Point(3, 237);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(217, 57);
            this.panel2.TabIndex = 1;
            // 
            // ClientNameInputBox
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(223, 297);
            this.Controls.Add(this.tableLayoutPanel1);
            this.Name = "ClientNameInputBox";
            this.Text = "Client Name";
            this.TopMost = true;
            this.tableLayoutPanel1.ResumeLayout(false);
            this.panel1.ResumeLayout(false);
            this.panel2.ResumeLayout(false);
            this.panel2.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ListBox listOfAllClients;
        private System.Windows.Forms.TextBox selectedClientNameTextBox;
        private System.Windows.Forms.Button acceptSelectionButton;
        private TableLayoutPanel tableLayoutPanel1;
        private Panel panel1;
        private Panel panel2;
    }
}