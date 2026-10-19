using System.Drawing;
using System.Windows.Forms;

namespace Aarohi.Classes
{
    partial class DynamicCrudForm
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
            PanelTestUCHolder = new Aarohi.ExtendedUI.ExtendedPanel();
            extendedPanel1 = new Aarohi.ExtendedUI.ExtendedPanel();
            panel1 = new Panel();
            PanelFooter = new Aarohi.ExtendedUI.ExtendedPanel();
            extendedPanel3 = new Aarohi.ExtendedUI.ExtendedPanel();
            ButtonEdit = new Aarohi.ExtendedUI.ExtendedButton();
            ButtonDelete = new Aarohi.ExtendedUI.ExtendedButton();
            extendedPanel2 = new Aarohi.ExtendedUI.ExtendedPanel();
            ButtonAdd = new Aarohi.ExtendedUI.ExtendedButton();
            PanelHeader = new Aarohi.ExtendedUI.ExtendedPanel();
            LabelHeading = new Label();
            extendedPanel1.SuspendLayout();
            panel1.SuspendLayout();
            PanelFooter.SuspendLayout();
            extendedPanel3.SuspendLayout();
            extendedPanel2.SuspendLayout();
            PanelHeader.SuspendLayout();
            SuspendLayout();
            // 
            // PanelTestUCHolder
            // 
            PanelTestUCHolder.AutoScroll = true;
            PanelTestUCHolder.BackColor = Color.White;
            PanelTestUCHolder.BlurTint = Color.FromArgb(40, 255, 255, 255);
            PanelTestUCHolder.BorderColor = Color.Transparent;
            PanelTestUCHolder.BorderWidth = 1;
            PanelTestUCHolder.CornerRadius = 15;
            PanelTestUCHolder.CornerRadiusBottomLeft = 15;
            PanelTestUCHolder.CornerRadiusBottomRight = 15;
            PanelTestUCHolder.CornerRadiusTopLeft = 15;
            PanelTestUCHolder.CornerRadiusTopRight = 15;
            PanelTestUCHolder.DisplayMode = ExtendedUI.DisplayMode.Grid;
            PanelTestUCHolder.Dock = DockStyle.Fill;
            PanelTestUCHolder.EnableAutoScrollY = true;
            PanelTestUCHolder.GradientColors.Add(Color.DeepSkyBlue);
            PanelTestUCHolder.GradientColors.Add(Color.MediumBlue);
            PanelTestUCHolder.GradientOpacity = 0.1F;
            PanelTestUCHolder.GridAutoColumnWidth = false;
            PanelTestUCHolder.GridColumnCount = 1;
            PanelTestUCHolder.Location = new Point(0, 20);
            PanelTestUCHolder.Name = "PanelTestUCHolder";
            PanelTestUCHolder.Padding = new Padding(6);
            PanelTestUCHolder.Size = new Size(1044, 533);
            PanelTestUCHolder.TabIndex = 7;
            // 
            // extendedPanel1
            // 
            extendedPanel1.BackColor = Color.White;
            extendedPanel1.BackgroundMode = ExtendedUI.BgMode.None;
            extendedPanel1.BlurTint = Color.FromArgb(40, 255, 255, 255);
            extendedPanel1.BorderColor = Color.Transparent;
            extendedPanel1.BorderWidth = 1;
            extendedPanel1.Controls.Add(panel1);
            extendedPanel1.Controls.Add(PanelFooter);
            extendedPanel1.Controls.Add(PanelHeader);
            extendedPanel1.CornerRadius = 0;
            extendedPanel1.CornerRadiusBottomLeft = 0;
            extendedPanel1.CornerRadiusBottomRight = 0;
            extendedPanel1.CornerRadiusTopLeft = 0;
            extendedPanel1.CornerRadiusTopRight = 0;
            extendedPanel1.Dock = DockStyle.Fill;
            extendedPanel1.GradientColors.Add(Color.DeepSkyBlue);
            extendedPanel1.GradientColors.Add(Color.MediumBlue);
            extendedPanel1.GradientOpacity = 0.1F;
            extendedPanel1.Location = new Point(0, 0);
            extendedPanel1.Margin = new Padding(0);
            extendedPanel1.Name = "extendedPanel1";
            extendedPanel1.Padding = new Padding(20);
            extendedPanel1.Size = new Size(1084, 804);
            extendedPanel1.TabIndex = 2;
            // 
            // panel1
            // 
            panel1.BackColor = Color.Transparent;
            panel1.Controls.Add(PanelTestUCHolder);
            panel1.Dock = DockStyle.Fill;
            panel1.Location = new Point(20, 120);
            panel1.Name = "panel1";
            panel1.Padding = new Padding(0, 20, 0, 20);
            panel1.Size = new Size(1044, 573);
            panel1.TabIndex = 6;
            // 
            // PanelFooter
            // 
            PanelFooter.BackColor = Color.White;
            PanelFooter.BlurTint = Color.FromArgb(40, 255, 255, 255);
            PanelFooter.BorderColor = Color.Transparent;
            PanelFooter.BorderWidth = 1;
            PanelFooter.Controls.Add(extendedPanel3);
            PanelFooter.Controls.Add(extendedPanel2);
            PanelFooter.CornerRadius = 20;
            PanelFooter.CornerRadiusBottomLeft = 20;
            PanelFooter.CornerRadiusBottomRight = 20;
            PanelFooter.CornerRadiusTopLeft = 20;
            PanelFooter.CornerRadiusTopRight = 20;
            PanelFooter.Dock = DockStyle.Bottom;
            PanelFooter.GradientColors.Add(Color.DeepSkyBlue);
            PanelFooter.GradientColors.Add(Color.MediumBlue);
            PanelFooter.GradientOpacity = 0.5F;
            PanelFooter.Location = new Point(20, 693);
            PanelFooter.Name = "PanelFooter";
            PanelFooter.Padding = new Padding(10);
            PanelFooter.Size = new Size(1044, 91);
            PanelFooter.TabIndex = 5;
            // 
            // extendedPanel3
            // 
            extendedPanel3.BackColor = Color.White;
            extendedPanel3.BlurTint = Color.FromArgb(40, 255, 255, 255);
            extendedPanel3.BorderColor = Color.Transparent;
            extendedPanel3.BorderWidth = 1;
            extendedPanel3.Controls.Add(ButtonEdit);
            extendedPanel3.Controls.Add(ButtonDelete);
            extendedPanel3.CornerRadius = 17;
            extendedPanel3.CornerRadiusBottomLeft = 17;
            extendedPanel3.CornerRadiusBottomRight = 17;
            extendedPanel3.CornerRadiusTopLeft = 17;
            extendedPanel3.CornerRadiusTopRight = 17;
            extendedPanel3.DisplayMode = ExtendedUI.DisplayMode.Grid;
            extendedPanel3.Dock = DockStyle.Left;
            extendedPanel3.GridAutoColumnWidth = false;
            extendedPanel3.GridAutoRowHeight = false;
            extendedPanel3.GridColumnCount = 2;
            extendedPanel3.GridColumnGap = 10;
            extendedPanel3.Location = new Point(10, 10);
            extendedPanel3.Name = "extendedPanel3";
            extendedPanel3.Padding = new Padding(10);
            extendedPanel3.Size = new Size(420, 71);
            extendedPanel3.TabIndex = 6;
            // 
            // ButtonEdit
            // 
            ButtonEdit.AutoLog = true;
            ButtonEdit.BackColor = Color.DeepSkyBlue;
            ButtonEdit.BackColor2 = Color.MediumBlue;
            ButtonEdit.BorderColor = Color.Transparent;
            ButtonEdit.BorderThickness = 2;
            ButtonEdit.CornerRadius = 12;
            ButtonEdit.CornerRadiusBottomLeft = 0;
            ButtonEdit.CornerRadiusBottomRight = 0;
            ButtonEdit.CornerRadiusTopLeft = 0;
            ButtonEdit.CornerRadiusTopRight = 0;
            ButtonEdit.FlatAppearance.BorderSize = 0;
            ButtonEdit.FlatStyle = FlatStyle.Flat;
            ButtonEdit.FocusBorderColor = Color.DeepSkyBlue;
            ButtonEdit.Font = new Font("Gadugi", 12F, FontStyle.Bold);
            ButtonEdit.ForeColor = Color.White;
            ButtonEdit.GradientMode = System.Drawing.Drawing2D.LinearGradientMode.Horizontal;
            ButtonEdit.HoverBackColor = Color.MediumBlue;
            ButtonEdit.HoverBackColor2 = Color.DarkOrchid;
            ButtonEdit.HoverBorderColor = Color.Transparent;
            ButtonEdit.HoverFontScale = 1.02F;
            ButtonEdit.HoverForeColor = Color.White;
            ButtonEdit.IconImage = null;
            ButtonEdit.IconPermanent = true;
            ButtonEdit.IconSize = 18;
            ButtonEdit.IconTextSpacing = 8;
            ButtonEdit.Location = new Point(10, 10);
            ButtonEdit.Margin = new Padding(0);
            ButtonEdit.Name = "ButtonEdit";
            ButtonEdit.Padding = new Padding(14, 8, 14, 8);
            ButtonEdit.Preset = ExtendedUI.ExtendedButton.ButtonVisualPreset.Normal;
            ButtonEdit.Selected = false;
            ButtonEdit.ShadowOpacity = 40;
            ButtonEdit.ShadowSize = 6;
            ButtonEdit.ShowShadow = false;
            ButtonEdit.SidebarMode = ExtendedUI.ExtendedButton.SidebarSize.Large;
            ButtonEdit.Size = new Size(195, 51);
            ButtonEdit.TabIndex = 5;
            ButtonEdit.Text = "Edit Station";
            ButtonEdit.UsedInSidebar = false;
            ButtonEdit.UseVisualStyleBackColor = false;
            ButtonEdit.Click += ButtonEdit_Click;
            // 
            // ButtonDelete
            // 
            ButtonDelete.AutoLog = true;
            ButtonDelete.BackColor = Color.HotPink;
            ButtonDelete.BackColor2 = Color.OrangeRed;
            ButtonDelete.BorderColor = Color.Transparent;
            ButtonDelete.BorderThickness = 2;
            ButtonDelete.CornerRadius = 12;
            ButtonDelete.CornerRadiusBottomLeft = 0;
            ButtonDelete.CornerRadiusBottomRight = 0;
            ButtonDelete.CornerRadiusTopLeft = 0;
            ButtonDelete.CornerRadiusTopRight = 0;
            ButtonDelete.Dock = DockStyle.Fill;
            ButtonDelete.FlatAppearance.BorderSize = 0;
            ButtonDelete.FlatStyle = FlatStyle.Popup;
            ButtonDelete.FocusBorderColor = Color.DeepSkyBlue;
            ButtonDelete.Font = new Font("Gadugi", 12F, FontStyle.Bold);
            ButtonDelete.ForeColor = Color.White;
            ButtonDelete.GradientMode = System.Drawing.Drawing2D.LinearGradientMode.Horizontal;
            ButtonDelete.HoverBackColor = Color.OrangeRed;
            ButtonDelete.HoverBackColor2 = Color.Fuchsia;
            ButtonDelete.HoverBorderColor = Color.Transparent;
            ButtonDelete.HoverFontScale = 1.02F;
            ButtonDelete.HoverForeColor = Color.White;
            ButtonDelete.IconImage = null;
            ButtonDelete.IconPermanent = true;
            ButtonDelete.IconSize = 18;
            ButtonDelete.IconTextSpacing = 8;
            ButtonDelete.Location = new Point(215, 10);
            ButtonDelete.Margin = new Padding(0);
            ButtonDelete.Name = "ButtonDelete";
            ButtonDelete.Padding = new Padding(14, 8, 14, 8);
            ButtonDelete.Preset = ExtendedUI.ExtendedButton.ButtonVisualPreset.Normal;
            ButtonDelete.Selected = false;
            ButtonDelete.ShadowOpacity = 40;
            ButtonDelete.ShadowSize = 6;
            ButtonDelete.ShowShadow = false;
            ButtonDelete.SidebarMode = ExtendedUI.ExtendedButton.SidebarSize.Large;
            ButtonDelete.Size = new Size(195, 51);
            ButtonDelete.TabIndex = 7;
            ButtonDelete.Text = "Delete Test";
            ButtonDelete.UsedInSidebar = false;
            ButtonDelete.UseVisualStyleBackColor = false;
            ButtonDelete.Click += ButtonDelete_Click;
            // 
            // extendedPanel2
            // 
            extendedPanel2.BackColor = Color.White;
            extendedPanel2.BlurTint = Color.FromArgb(40, 255, 255, 255);
            extendedPanel2.BorderColor = Color.Transparent;
            extendedPanel2.BorderWidth = 1;
            extendedPanel2.Controls.Add(ButtonAdd);
            extendedPanel2.CornerRadius = 17;
            extendedPanel2.CornerRadiusBottomLeft = 17;
            extendedPanel2.CornerRadiusBottomRight = 17;
            extendedPanel2.CornerRadiusTopLeft = 17;
            extendedPanel2.CornerRadiusTopRight = 17;
            extendedPanel2.DisplayMode = ExtendedUI.DisplayMode.Grid;
            extendedPanel2.Dock = DockStyle.Right;
            extendedPanel2.GridAutoColumnWidth = false;
            extendedPanel2.GridAutoRowHeight = false;
            extendedPanel2.GridColumnCount = 1;
            extendedPanel2.GridColumnGap = 10;
            extendedPanel2.Location = new Point(786, 10);
            extendedPanel2.Name = "extendedPanel2";
            extendedPanel2.Padding = new Padding(10);
            extendedPanel2.Size = new Size(248, 71);
            extendedPanel2.TabIndex = 4;
            // 
            // ButtonAdd
            // 
            ButtonAdd.AutoLog = true;
            ButtonAdd.BackColor = Color.DeepSkyBlue;
            ButtonAdd.BackColor2 = Color.MediumBlue;
            ButtonAdd.BorderColor = Color.Transparent;
            ButtonAdd.BorderThickness = 2;
            ButtonAdd.CornerRadius = 12;
            ButtonAdd.CornerRadiusBottomLeft = 0;
            ButtonAdd.CornerRadiusBottomRight = 0;
            ButtonAdd.CornerRadiusTopLeft = 0;
            ButtonAdd.CornerRadiusTopRight = 0;
            ButtonAdd.FlatAppearance.BorderSize = 0;
            ButtonAdd.FlatStyle = FlatStyle.Flat;
            ButtonAdd.FocusBorderColor = Color.DeepSkyBlue;
            ButtonAdd.Font = new Font("Gadugi", 12F, FontStyle.Bold);
            ButtonAdd.ForeColor = Color.White;
            ButtonAdd.GradientMode = System.Drawing.Drawing2D.LinearGradientMode.Horizontal;
            ButtonAdd.HoverBackColor = Color.MediumBlue;
            ButtonAdd.HoverBackColor2 = Color.DarkOrchid;
            ButtonAdd.HoverBorderColor = Color.Transparent;
            ButtonAdd.HoverFontScale = 1.02F;
            ButtonAdd.HoverForeColor = Color.White;
            ButtonAdd.IconImage = null;
            ButtonAdd.IconPermanent = true;
            ButtonAdd.IconSize = 18;
            ButtonAdd.IconTextSpacing = 8;
            ButtonAdd.Location = new Point(10, 10);
            ButtonAdd.Margin = new Padding(0);
            ButtonAdd.Name = "ButtonAdd";
            ButtonAdd.Padding = new Padding(14, 8, 14, 8);
            ButtonAdd.Preset = ExtendedUI.ExtendedButton.ButtonVisualPreset.Normal;
            ButtonAdd.Selected = false;
            ButtonAdd.ShadowOpacity = 40;
            ButtonAdd.ShadowSize = 6;
            ButtonAdd.ShowShadow = false;
            ButtonAdd.SidebarMode = ExtendedUI.ExtendedButton.SidebarSize.Large;
            ButtonAdd.Size = new Size(228, 51);
            ButtonAdd.TabIndex = 4;
            ButtonAdd.Text = "Add Station";
            ButtonAdd.UsedInSidebar = false;
            ButtonAdd.UseVisualStyleBackColor = false;
            ButtonAdd.Click += ButtonAdd_Click;
            // 
            // PanelHeader
            // 
            PanelHeader.AlignItems = ExtendedUI.AlignItems.Center;
            PanelHeader.BackColor = Color.White;
            PanelHeader.BlurTint = Color.FromArgb(40, 255, 255, 255);
            PanelHeader.BorderColor = Color.Transparent;
            PanelHeader.BorderWidth = 1;
            PanelHeader.Controls.Add(LabelHeading);
            PanelHeader.CornerRadius = 20;
            PanelHeader.CornerRadiusBottomLeft = 20;
            PanelHeader.CornerRadiusBottomRight = 20;
            PanelHeader.CornerRadiusTopLeft = 20;
            PanelHeader.CornerRadiusTopRight = 20;
            PanelHeader.DisplayMode = ExtendedUI.DisplayMode.Flex;
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
            PanelHeader.TabIndex = 4;
            // 
            // LabelHeading
            // 
            LabelHeading.BackColor = Color.Transparent;
            LabelHeading.Dock = DockStyle.Fill;
            LabelHeading.Font = new Font("Gadugi", 18F, FontStyle.Bold, GraphicsUnit.Point, 0);
            LabelHeading.ForeColor = Color.MediumBlue;
            LabelHeading.Location = new Point(23, 20);
            LabelHeading.Name = "LabelHeading";
            LabelHeading.Size = new Size(1004, 60);
            LabelHeading.TabIndex = 0;
            LabelHeading.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // DynamicCrudForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1084, 804);
            Controls.Add(extendedPanel1);
            Name = "DynamicCrudForm";
            Text = "DynamicCrudForm";
            WindowState = FormWindowState.Maximized;
            Load += DynamicCrudForm_Load;
            extendedPanel1.ResumeLayout(false);
            panel1.ResumeLayout(false);
            PanelFooter.ResumeLayout(false);
            extendedPanel3.ResumeLayout(false);
            extendedPanel2.ResumeLayout(false);
            PanelHeader.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private Aarohi.ExtendedUI.ExtendedPanel PanelTestUCHolder;
        private Aarohi.ExtendedUI.ExtendedPanel extendedPanel1;
        private Panel panel1;
        private Aarohi.ExtendedUI.ExtendedPanel PanelFooter;
        private Aarohi.ExtendedUI.ExtendedPanel extendedPanel3;
        private Aarohi.ExtendedUI.ExtendedButton ButtonEdit;
        private Aarohi.ExtendedUI.ExtendedButton ButtonDelete;
        private Aarohi.ExtendedUI.ExtendedPanel extendedPanel2;
        private Aarohi.ExtendedUI.ExtendedButton ButtonAdd;
        private Aarohi.ExtendedUI.ExtendedPanel PanelHeader;
        private Label LabelHeading;
    }
}