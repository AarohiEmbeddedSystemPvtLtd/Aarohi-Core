using System.Drawing;
using System.Windows.Forms;
using System.Xml.Linq;
using Aarohi.ExtendedUI;

namespace Aarohi.Classes
{

    partial class DynamicAdderForm
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
            PanelHeader = new ExtendedPanel();
            LabelHeading = new Label();
            PanelFooter = new ExtendedPanel();
            extendedPanel1 = new ExtendedPanel();
            buttonCancel = new ExtendedButton();
            ButtonSave = new ExtendedButton();
            PanelSelection = new ExtendedPanel();
            LabelSelection = new Label();
            comboBoxSelection = new ComboBox();
            extendedPanel2 = new ExtendedPanel();
            PanelHolder = new ExtendedPanel();
            PanelHeader.SuspendLayout();
            PanelFooter.SuspendLayout();
            extendedPanel1.SuspendLayout();
            PanelSelection.SuspendLayout();
            SuspendLayout();
            // 
            // PanelHeader
            // 
            PanelHeader.AlignItems = AlignItems.Center;
            PanelHeader.BackColor = Color.White;
            PanelHeader.BlurTint = Color.FromArgb(40, 255, 255, 255);
            PanelHeader.BorderColor = Color.Transparent;
            PanelHeader.BorderWidth = 1;
            PanelHeader.Controls.Add(LabelHeading);
            PanelHeader.CornerRadius = 0;
            PanelHeader.CornerRadiusBottomLeft = 0;
            PanelHeader.CornerRadiusBottomRight = 0;
            PanelHeader.CornerRadiusTopLeft = 20;
            PanelHeader.CornerRadiusTopRight = 20;
            PanelHeader.DisplayMode = DisplayMode.Flex;
            PanelHeader.Dock = DockStyle.Top;
            PanelHeader.Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point, 0);
            PanelHeader.ForeColor = Color.MediumBlue;
            PanelHeader.GradientColors.Add(Color.DeepSkyBlue);
            PanelHeader.GradientColors.Add(Color.MediumBlue);
            PanelHeader.GradientOpacity = 0.5F;
            PanelHeader.Location = new Point(20, 20);
            PanelHeader.Name = "PanelHeader";
            PanelHeader.Padding = new Padding(20);
            PanelHeader.Size = new Size(1044, 100);
            PanelHeader.TabIndex = 0;
            // 
            // LabelHeading
            // 
            LabelHeading.BackColor = Color.Transparent;
            LabelHeading.Dock = DockStyle.Fill;
            LabelHeading.Font = new Font("Gadugi", 18F, FontStyle.Bold, GraphicsUnit.Point, 0);
            LabelHeading.Location = new Point(23, 20);
            LabelHeading.Name = "LabelHeading";
            LabelHeading.Size = new Size(1004, 60);
            LabelHeading.TabIndex = 0;
            LabelHeading.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // PanelFooter
            // 
            PanelFooter.BackColor = Color.White;
            PanelFooter.BlurTint = Color.FromArgb(40, 255, 255, 255);
            PanelFooter.BorderColor = Color.Transparent;
            PanelFooter.BorderWidth = 1;
            PanelFooter.Controls.Add(extendedPanel1);
            PanelFooter.CornerRadius = 0;
            PanelFooter.CornerRadiusBottomLeft = 20;
            PanelFooter.CornerRadiusBottomRight = 20;
            PanelFooter.CornerRadiusTopLeft = 0;
            PanelFooter.CornerRadiusTopRight = 0;
            PanelFooter.Dock = DockStyle.Bottom;
            PanelFooter.GradientColors.Add(Color.DeepSkyBlue);
            PanelFooter.GradientColors.Add(Color.MediumBlue);
            PanelFooter.GradientOpacity = 0.5F;
            PanelFooter.Location = new Point(20, 755);
            PanelFooter.Name = "PanelFooter";
            PanelFooter.Padding = new Padding(10);
            PanelFooter.Size = new Size(1044, 91);
            PanelFooter.TabIndex = 1;
            // 
            // extendedPanel1
            // 
            extendedPanel1.BackColor = Color.White;
            extendedPanel1.BlurTint = Color.FromArgb(40, 255, 255, 255);
            extendedPanel1.BorderColor = Color.Transparent;
            extendedPanel1.BorderWidth = 1;
            extendedPanel1.Controls.Add(buttonCancel);
            extendedPanel1.Controls.Add(ButtonSave);
            extendedPanel1.CornerRadius = 17;
            extendedPanel1.CornerRadiusBottomLeft = 17;
            extendedPanel1.CornerRadiusBottomRight = 17;
            extendedPanel1.CornerRadiusTopLeft = 17;
            extendedPanel1.CornerRadiusTopRight = 17;
            extendedPanel1.DisplayMode = DisplayMode.Grid;
            extendedPanel1.Dock = DockStyle.Right;
            extendedPanel1.GridAutoColumnWidth = false;
            extendedPanel1.GridAutoRowHeight = false;
            extendedPanel1.GridColumnCount = 2;
            extendedPanel1.GridColumnGap = 10;
            extendedPanel1.Location = new Point(638, 10);
            extendedPanel1.Name = "extendedPanel1";
            extendedPanel1.Padding = new Padding(10);
            extendedPanel1.Size = new Size(396, 71);
            extendedPanel1.TabIndex = 4;
            // 
            // buttonCancel
            // 
            buttonCancel.AutoLog = true;
            buttonCancel.BackColor = Color.HotPink;
            buttonCancel.BackColor2 = Color.OrangeRed;
            buttonCancel.BorderColor = Color.Transparent;
            buttonCancel.BorderThickness = 2;
            buttonCancel.CornerRadius = 12;
            buttonCancel.CornerRadiusBottomLeft = 0;
            buttonCancel.CornerRadiusBottomRight = 0;
            buttonCancel.CornerRadiusTopLeft = 0;
            buttonCancel.CornerRadiusTopRight = 0;
            buttonCancel.FlatAppearance.BorderSize = 0;
            buttonCancel.FlatStyle = FlatStyle.Flat;
            buttonCancel.FocusBorderColor = Color.DeepSkyBlue;
            buttonCancel.Font = new Font("Gadugi", 12F, FontStyle.Bold);
            buttonCancel.ForeColor = Color.White;
            buttonCancel.GradientMode = System.Drawing.Drawing2D.LinearGradientMode.Horizontal;
            buttonCancel.HoverBackColor = Color.OrangeRed;
            buttonCancel.HoverBackColor2 = Color.Fuchsia;
            buttonCancel.HoverBorderColor = Color.Transparent;
            buttonCancel.HoverFontScale = 1.02F;
            buttonCancel.HoverForeColor = Color.White;
            buttonCancel.IconImage = null;
            buttonCancel.IconPermanent = true;
            buttonCancel.IconSize = 18;
            buttonCancel.IconTextSpacing = 8;
            buttonCancel.Location = new Point(10, 10);
            buttonCancel.Margin = new Padding(0);
            buttonCancel.Name = "buttonCancel";
            buttonCancel.Padding = new Padding(14, 8, 14, 8);
            buttonCancel.Preset = ExtendedButton.ButtonVisualPreset.Normal;
            buttonCancel.Selected = false;
            buttonCancel.ShadowOpacity = 40;
            buttonCancel.ShadowSize = 6;
            buttonCancel.ShowShadow = false;
            buttonCancel.SidebarMode = ExtendedButton.SidebarSize.Large;
            buttonCancel.Size = new Size(183, 51);
            buttonCancel.TabIndex = 5;
            buttonCancel.Text = "Cancel";
            buttonCancel.UsedInSidebar = false;
            buttonCancel.UseVisualStyleBackColor = false;
            buttonCancel.Click += buttonCancel_Click;
            // 
            // ButtonSave
            // 
            ButtonSave.AutoLog = true;
            ButtonSave.BackColor = Color.DeepSkyBlue;
            ButtonSave.BackColor2 = Color.MediumBlue;
            ButtonSave.BorderColor = Color.Transparent;
            ButtonSave.BorderThickness = 2;
            ButtonSave.CornerRadius = 12;
            ButtonSave.CornerRadiusBottomLeft = 0;
            ButtonSave.CornerRadiusBottomRight = 0;
            ButtonSave.CornerRadiusTopLeft = 0;
            ButtonSave.CornerRadiusTopRight = 0;
            ButtonSave.FlatAppearance.BorderSize = 0;
            ButtonSave.FlatStyle = FlatStyle.Flat;
            ButtonSave.FocusBorderColor = Color.DeepSkyBlue;
            ButtonSave.Font = new Font("Gadugi", 12F, FontStyle.Bold);
            ButtonSave.ForeColor = Color.White;
            ButtonSave.GradientMode = System.Drawing.Drawing2D.LinearGradientMode.Horizontal;
            ButtonSave.HoverBackColor = Color.MediumBlue;
            ButtonSave.HoverBackColor2 = Color.DarkOrchid;
            ButtonSave.HoverBorderColor = Color.Transparent;
            ButtonSave.HoverFontScale = 1.02F;
            ButtonSave.HoverForeColor = Color.White;
            ButtonSave.IconImage = null;
            ButtonSave.IconPermanent = true;
            ButtonSave.IconSize = 18;
            ButtonSave.IconTextSpacing = 8;
            ButtonSave.Location = new Point(203, 10);
            ButtonSave.Margin = new Padding(0);
            ButtonSave.Name = "ButtonSave";
            ButtonSave.Padding = new Padding(14, 8, 14, 8);
            ButtonSave.Preset = ExtendedButton.ButtonVisualPreset.Normal;
            ButtonSave.Selected = false;
            ButtonSave.ShadowOpacity = 40;
            ButtonSave.ShadowSize = 6;
            ButtonSave.ShowShadow = false;
            ButtonSave.SidebarMode = ExtendedButton.SidebarSize.Large;
            ButtonSave.Size = new Size(183, 51);
            ButtonSave.TabIndex = 4;
            ButtonSave.Text = "Save";
            ButtonSave.UsedInSidebar = false;
            ButtonSave.UseVisualStyleBackColor = false;
            ButtonSave.Click += ButtonSave_Click;
            // 
            // PanelSelection
            // 
            PanelSelection.AlignItems = AlignItems.Center;
            PanelSelection.BackColor = Color.White;
            PanelSelection.BlurTint = Color.FromArgb(40, 255, 255, 255);
            PanelSelection.BorderColor = Color.Transparent;
            PanelSelection.BorderWidth = 1;
            PanelSelection.Controls.Add(LabelSelection);
            PanelSelection.Controls.Add(comboBoxSelection);
            PanelSelection.CornerRadius = 0;
            PanelSelection.CornerRadiusBottomLeft = 0;
            PanelSelection.CornerRadiusBottomRight = 0;
            PanelSelection.CornerRadiusTopLeft = 0;
            PanelSelection.CornerRadiusTopRight = 0;
            PanelSelection.DisplayMode = DisplayMode.Flex;
            PanelSelection.Dock = DockStyle.Top;
            PanelSelection.ForeColor = Color.MediumBlue;
            PanelSelection.GradientColors.Add(Color.DeepSkyBlue);
            PanelSelection.GradientColors.Add(Color.MediumBlue);
            PanelSelection.GradientOpacity = 0.1F;
            PanelSelection.Location = new Point(20, 120);
            PanelSelection.Name = "PanelSelection";
            PanelSelection.Padding = new Padding(20, 6, 6, 6);
            PanelSelection.Size = new Size(1044, 66);
            PanelSelection.TabIndex = 2;
            // 
            // LabelSelection
            // 
            LabelSelection.AutoSize = true;
            LabelSelection.BackColor = Color.Transparent;
            LabelSelection.Dock = DockStyle.Fill;
            LabelSelection.Font = new Font("Gadugi", 18F, FontStyle.Bold, GraphicsUnit.Point, 0);
            LabelSelection.Location = new Point(23, 19);
            LabelSelection.Name = "LabelSelection";
            LabelSelection.Size = new Size(0, 28);
            LabelSelection.TabIndex = 1;
            LabelSelection.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // comboBoxSelection
            // 
            comboBoxSelection.Font = new Font("Gadugi", 18F, FontStyle.Regular, GraphicsUnit.Point, 0);
            comboBoxSelection.FormattingEnabled = true;
            comboBoxSelection.Location = new Point(26, 15);
            comboBoxSelection.Margin = new Padding(0);
            comboBoxSelection.Name = "comboBoxSelection";
            comboBoxSelection.Size = new Size(288, 36);
            comboBoxSelection.TabIndex = 2;
            // 
            // extendedPanel2
            // 
            extendedPanel2.AlignItems = AlignItems.Center;
            extendedPanel2.BackColor = Color.White;
            extendedPanel2.BlurTint = Color.FromArgb(40, 255, 255, 255);
            extendedPanel2.BorderColor = Color.Transparent;
            extendedPanel2.BorderWidth = 1;
            extendedPanel2.CornerRadius = 0;
            extendedPanel2.CornerRadiusBottomLeft = 0;
            extendedPanel2.CornerRadiusBottomRight = 0;
            extendedPanel2.CornerRadiusTopLeft = 0;
            extendedPanel2.CornerRadiusTopRight = 0;
            extendedPanel2.DisplayMode = DisplayMode.Flex;
            extendedPanel2.Dock = DockStyle.Top;
            extendedPanel2.Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point, 0);
            extendedPanel2.ForeColor = Color.MediumBlue;
            extendedPanel2.GradientColors.Add(Color.DeepSkyBlue);
            extendedPanel2.GradientColors.Add(Color.MediumBlue);
            extendedPanel2.Location = new Point(20, 186);
            extendedPanel2.Name = "extendedPanel2";
            extendedPanel2.Padding = new Padding(6);
            extendedPanel2.Size = new Size(1044, 5);
            extendedPanel2.TabIndex = 3;
            // 
            // PanelHolder
            // 
            PanelHolder.AlignItems = AlignItems.Center;
            PanelHolder.AutoScroll = true;
            PanelHolder.BackColor = Color.White;
            PanelHolder.BlurTint = Color.FromArgb(40, 255, 255, 255);
            PanelHolder.BorderColor = Color.Transparent;
            PanelHolder.BorderWidth = 1;
            PanelHolder.CornerRadius = 0;
            PanelHolder.CornerRadiusBottomLeft = 0;
            PanelHolder.CornerRadiusBottomRight = 0;
            PanelHolder.CornerRadiusTopLeft = 0;
            PanelHolder.CornerRadiusTopRight = 0;
            PanelHolder.Direction = FlexDirection.Vertical;
            PanelHolder.DisplayMode = DisplayMode.Flex;
            PanelHolder.Dock = DockStyle.Fill;
            PanelHolder.EnableAutoScrollY = true;
            PanelHolder.GradientColors.Add(Color.DeepSkyBlue);
            PanelHolder.GradientColors.Add(Color.MediumBlue);
            PanelHolder.GradientOpacity = 0.1F;
            PanelHolder.GridAutoColumnWidth = false;
            PanelHolder.GridColumnCount = 1;
            PanelHolder.GridColumnGap = 1;
            PanelHolder.GridInverse = true;
            PanelHolder.GridRowGap = 1;
            PanelHolder.JustifyContent = JustifyContent.Center;
            PanelHolder.Location = new Point(20, 191);
            PanelHolder.Name = "PanelHolder";
            PanelHolder.Padding = new Padding(6);
            PanelHolder.Size = new Size(1044, 564);
            PanelHolder.TabIndex = 5;
            // 
            // DynamicAdderForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.White;
            ClientSize = new Size(1084, 866);
            Controls.Add(PanelHolder);
            Controls.Add(extendedPanel2);
            Controls.Add(PanelSelection);
            Controls.Add(PanelFooter);
            Controls.Add(PanelHeader);
            MaximizeBox = false;
            MdiChildrenMinimizedAnchorBottom = false;
            MinimizeBox = false;
            Name = "DynamicAdderForm";
            Padding = new Padding(20);
            Text = "DynamicAdderForm";
            WindowState = FormWindowState.Maximized;
            PanelHeader.ResumeLayout(false);
            PanelFooter.ResumeLayout(false);
            extendedPanel1.ResumeLayout(false);
            PanelSelection.ResumeLayout(false);
            PanelSelection.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private ExtendedPanel PanelHeader;
        private ExtendedPanel PanelFooter;
        private Label LabelHeading;
        private ExtendedPanel extendedPanel1;
        private ExtendedButton buttonCancel;
        private ExtendedButton ButtonSave;
        private ExtendedPanel PanelSelection;
        private Label LabelSelection;
        private ComboBox comboBoxSelection;
        private ExtendedPanel extendedPanel2;
        private ExtendedPanel PanelHolder;
    }
}  
