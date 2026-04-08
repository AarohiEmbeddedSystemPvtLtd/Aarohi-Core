using Aarohi.ExtendedUI;
using System.Drawing;
using System.Windows.Forms;

namespace Aarohi.Classes
{
    partial class ExtendedDataView
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            FillterPanel = new ExtendedPanel();
            LocationSpecifier = new ExtendedPanel();
            labelLocationSpecifier = new Label();
            tabControl = new TabControl();
            LocationSpecifier.SuspendLayout();
            SuspendLayout();
            // 
            // FillterPanel
            // 
            FillterPanel.BackColor = Color.White;
            FillterPanel.BlurTint = Color.FromArgb(40, 255, 255, 255);
            FillterPanel.BorderColor = Color.Transparent;
            FillterPanel.BorderWidth = 1;
            FillterPanel.DisplayMode = DisplayMode.Grid;
            FillterPanel.Dock = DockStyle.Top;
            FillterPanel.Enabled = false;
            FillterPanel.GridAutoColumnWidth = false;
            FillterPanel.Location = new Point(0, 0);
            FillterPanel.Name = "FillterPanel";
            FillterPanel.Padding = new Padding(6);
            FillterPanel.Size = new Size(1125, 100);
            FillterPanel.TabIndex = 0;
            FillterPanel.Visible = false;
            // 
            // LocationSpecifier
            // 
            LocationSpecifier.BackColor = Color.White;
            LocationSpecifier.BlurTint = Color.FromArgb(40, 255, 255, 255);
            LocationSpecifier.BorderColor = Color.Transparent;
            LocationSpecifier.BorderWidth = 1;
            LocationSpecifier.Controls.Add(labelLocationSpecifier);
            LocationSpecifier.Dock = DockStyle.Top;
            LocationSpecifier.Enabled = false;
            LocationSpecifier.GridAutoColumnWidth = false;
            LocationSpecifier.Location = new Point(0, 100);
            LocationSpecifier.Name = "LocationSpecifier";
            LocationSpecifier.Padding = new Padding(6);
            LocationSpecifier.Size = new Size(1125, 43);
            LocationSpecifier.TabIndex = 2;
            // 
            // labelLocationSpecifier
            // 
            labelLocationSpecifier.Dock = DockStyle.Fill;
            labelLocationSpecifier.Font = new Font("Gadugi", 14.25F, FontStyle.Bold, GraphicsUnit.Point, 0);
            labelLocationSpecifier.Location = new Point(6, 6);
            labelLocationSpecifier.Name = "labelLocationSpecifier";
            labelLocationSpecifier.Size = new Size(1113, 31);
            labelLocationSpecifier.TabIndex = 0;
            labelLocationSpecifier.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // tabControl
            // 
            tabControl.Dock = DockStyle.Fill;
            tabControl.Font = new Font("Gadugi", 14.25F, FontStyle.Bold, GraphicsUnit.Point, 0);
            tabControl.Location = new Point(0, 143);
            tabControl.Multiline = true;
            tabControl.Name = "tabControl";
            tabControl.SelectedIndex = 0;
            tabControl.Size = new Size(1125, 556);
            tabControl.TabIndex = 5;
            // 
            // ExtendedDataView
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            Controls.Add(tabControl);
            Controls.Add(LocationSpecifier);
            Controls.Add(FillterPanel);
            Name = "ExtendedDataView";
            Size = new Size(1125, 699);
            Load += ExtendedDataView_Load;
            LocationSpecifier.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion
        private ExtendedPanel FillterPanel;
        private ExtendedPanel LocationSpecifier;
        private Label labelLocationSpecifier;
        private TabControl tabControl;
    }
}
