using Aarohi.ExtendedUI;
using System.Drawing;
using System.Windows.Forms;

namespace Aarohi.Classes
{
    partial class CommonDataViewer
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

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            PanelHeading = new ExtendedPanel();
            LabelHeading = new Label();
            extendedPanel1 = new ExtendedPanel();
            SearchTextBox = new TextBox();
            label1 = new Label();
            PanelCellHolder = new ExtendedPanel();
            PanelHeading.SuspendLayout();
            extendedPanel1.SuspendLayout();
            SuspendLayout();
            // 
            // PanelHeading
            // 
            PanelHeading.BackColor = Color.White;
            PanelHeading.BlurTint = Color.FromArgb(40, 255, 255, 255);
            PanelHeading.BorderColor = Color.Transparent;
            PanelHeading.BorderWidth = 1;
            PanelHeading.Controls.Add(LabelHeading);
            PanelHeading.CornerRadius = 0;
            PanelHeading.CornerRadiusBottomLeft = 0;
            PanelHeading.CornerRadiusBottomRight = 0;
            PanelHeading.CornerRadiusTopLeft = 0;
            PanelHeading.CornerRadiusTopRight = 0;
            PanelHeading.Dock = DockStyle.Top;
            PanelHeading.GradientColors.Add(Color.DeepSkyBlue);
            PanelHeading.GradientColors.Add(Color.MediumBlue);
            PanelHeading.GradientOpacity = 0.5F;
            PanelHeading.Location = new Point(0, 0);
            PanelHeading.Name = "PanelHeading";
            PanelHeading.Padding = new Padding(6);
            PanelHeading.Size = new Size(1016, 83);
            PanelHeading.TabIndex = 0;
            // 
            // LabelHeading
            // 
            LabelHeading.BackColor = Color.Transparent;
            LabelHeading.Dock = DockStyle.Fill;
            LabelHeading.Font = new Font("Gadugi", 18F, FontStyle.Bold, GraphicsUnit.Point, 0);
            LabelHeading.ForeColor = Color.MediumBlue;
            LabelHeading.Location = new Point(6, 6);
            LabelHeading.Name = "LabelHeading";
            LabelHeading.Size = new Size(1004, 71);
            LabelHeading.TabIndex = 0;
            LabelHeading.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // extendedPanel1
            // 
            extendedPanel1.BackColor = Color.White;
            extendedPanel1.BlurTint = Color.FromArgb(40, 255, 255, 255);
            extendedPanel1.BorderColor = Color.Transparent;
            extendedPanel1.BorderWidth = 1;
            extendedPanel1.Controls.Add(SearchTextBox);
            extendedPanel1.Controls.Add(label1);
            extendedPanel1.CornerRadius = 0;
            extendedPanel1.CornerRadiusBottomLeft = 0;
            extendedPanel1.CornerRadiusBottomRight = 0;
            extendedPanel1.CornerRadiusTopLeft = 0;
            extendedPanel1.CornerRadiusTopRight = 0;
            extendedPanel1.Dock = DockStyle.Top;
            extendedPanel1.FlexWrap = FlexWrap.Wrap;
            extendedPanel1.Location = new Point(0, 83);
            extendedPanel1.Name = "extendedPanel1";
            extendedPanel1.Padding = new Padding(6);
            extendedPanel1.Size = new Size(1016, 44);
            extendedPanel1.TabIndex = 2;
            // 
            // SearchTextBox
            // 
            SearchTextBox.Dock = DockStyle.Left;
            SearchTextBox.Font = new Font("Gadugi", 14.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
            SearchTextBox.Location = new Point(90, 6);
            SearchTextBox.Name = "SearchTextBox";
            SearchTextBox.Size = new Size(251, 33);
            SearchTextBox.TabIndex = 1;
            // 
            // label1
            // 
            label1.Dock = DockStyle.Left;
            label1.Font = new Font("Gadugi", 14.25F, FontStyle.Bold, GraphicsUnit.Point, 0);
            label1.Location = new Point(6, 6);
            label1.Name = "label1";
            label1.Size = new Size(84, 32);
            label1.TabIndex = 0;
            label1.Text = "Search:";
            label1.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // PanelCellHolder
            // 
            PanelCellHolder.AutoScroll = true;
            PanelCellHolder.BackColor = Color.White;
            PanelCellHolder.BlurTint = Color.FromArgb(40, 255, 255, 255);
            PanelCellHolder.BorderColor = Color.Transparent;
            PanelCellHolder.BorderWidth = 1;
            PanelCellHolder.CornerRadius = 0;
            PanelCellHolder.CornerRadiusBottomLeft = 0;
            PanelCellHolder.CornerRadiusBottomRight = 0;
            PanelCellHolder.CornerRadiusTopLeft = 0;
            PanelCellHolder.CornerRadiusTopRight = 0;
            PanelCellHolder.DisplayMode = DisplayMode.Flex;
            PanelCellHolder.Dock = DockStyle.Fill;
            PanelCellHolder.EnableAutoScrollY = true;
            PanelCellHolder.FlexWrap = FlexWrap.Wrap;
            PanelCellHolder.Location = new Point(0, 127);
            PanelCellHolder.Name = "PanelCellHolder";
            PanelCellHolder.Padding = new Padding(6);
            PanelCellHolder.Size = new Size(1016, 451);
            PanelCellHolder.TabIndex = 3;
            // 
            // CommonDataViewer
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1016, 578);
            Controls.Add(PanelCellHolder);
            Controls.Add(extendedPanel1);
            Controls.Add(PanelHeading);
            Name = "CommonDataViewer";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "CommonDataViewer";
            PanelHeading.ResumeLayout(false);
            extendedPanel1.ResumeLayout(false);
            extendedPanel1.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private ExtendedPanel PanelHeading;
        private Label LabelHeading;
        private ExtendedPanel extendedPanel1;
        private ExtendedPanel PanelCellHolder;
        private TextBox SearchTextBox;
        private Label label1;
    }
}