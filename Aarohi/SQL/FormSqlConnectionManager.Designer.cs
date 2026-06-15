using System.Drawing;
using System.Windows.Forms;

namespace Aarohi.SQL
{
    partial class FormSqlConnectionManager
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
            PanelWrapper = new Aarohi.ExtendedUI.ExtendedPanel();
            PanelDataHolderWrapper = new Aarohi.ExtendedUI.ExtendedPanel();
            extendedPanel1 = new Aarohi.ExtendedUI.ExtendedPanel();
            comboBoxHostname = new ComboBox();
            label2 = new Label();
            extendedPanel5 = new Aarohi.ExtendedUI.ExtendedPanel();
            comboBoxAuth = new ComboBox();
            label1 = new Label();
            extendedPanel4 = new Aarohi.ExtendedUI.ExtendedPanel();
            textBoxUserName = new TextBox();
            label5 = new Label();
            extendedPanel3 = new Aarohi.ExtendedUI.ExtendedPanel();
            textBoxPassword = new TextBox();
            label4 = new Label();
            extendedPanel2 = new Aarohi.ExtendedUI.ExtendedPanel();
            ComboboxDatabaseName = new ComboBox();
            label3 = new Label();
            PanelFooter = new Aarohi.ExtendedUI.ExtendedPanel();
            panelDiscovery = new Panel();
            toggleNetworkDiscovery = new ToggleSwitch();
            extendedPanel6 = new Aarohi.ExtendedUI.ExtendedPanel();
            ButtonTestConnection = new Aarohi.ExtendedUI.ExtendedButton();
            ButtonSave = new Aarohi.ExtendedUI.ExtendedButton();
            PanelHeader = new Aarohi.ExtendedUI.ExtendedPanel();
            LabelHeader = new Label();
            PanelWrapper.SuspendLayout();
            PanelDataHolderWrapper.SuspendLayout();
            extendedPanel1.SuspendLayout();
            extendedPanel5.SuspendLayout();
            extendedPanel4.SuspendLayout();
            extendedPanel3.SuspendLayout();
            extendedPanel2.SuspendLayout();
            PanelFooter.SuspendLayout();
            panelDiscovery.SuspendLayout();
            extendedPanel6.SuspendLayout();
            PanelHeader.SuspendLayout();
            SuspendLayout();
            // 
            // PanelWrapper
            // 
            PanelWrapper.BackColor = Color.White;
            PanelWrapper.BlurTint = Color.FromArgb(40, 255, 255, 255);
            PanelWrapper.BorderColor = Color.Transparent;
            PanelWrapper.BorderWidth = 1;
            PanelWrapper.Controls.Add(PanelDataHolderWrapper);
            PanelWrapper.Controls.Add(PanelFooter);
            PanelWrapper.Controls.Add(PanelHeader);
            PanelWrapper.CornerRadius = 0;
            PanelWrapper.CornerRadiusBottomLeft = 0;
            PanelWrapper.CornerRadiusBottomRight = 0;
            PanelWrapper.CornerRadiusTopLeft = 0;
            PanelWrapper.CornerRadiusTopRight = 0;
            PanelWrapper.Dock = DockStyle.Fill;
            PanelWrapper.Location = new Point(0, 0);
            PanelWrapper.Name = "PanelWrapper";
            PanelWrapper.Padding = new Padding(10);
            PanelWrapper.Size = new Size(1009, 552);
            PanelWrapper.TabIndex = 0;
            // 
            // PanelDataHolderWrapper
            // 
            PanelDataHolderWrapper.BackColor = Color.White;
            PanelDataHolderWrapper.BlurTint = Color.FromArgb(40, 255, 255, 255);
            PanelDataHolderWrapper.BorderColor = Color.Transparent;
            PanelDataHolderWrapper.BorderWidth = 1;
            PanelDataHolderWrapper.Controls.Add(extendedPanel1);
            PanelDataHolderWrapper.Controls.Add(extendedPanel5);
            PanelDataHolderWrapper.Controls.Add(extendedPanel4);
            PanelDataHolderWrapper.Controls.Add(extendedPanel3);
            PanelDataHolderWrapper.Controls.Add(extendedPanel2);
            PanelDataHolderWrapper.CornerRadius = 0;
            PanelDataHolderWrapper.CornerRadiusBottomLeft = 0;
            PanelDataHolderWrapper.CornerRadiusBottomRight = 0;
            PanelDataHolderWrapper.CornerRadiusTopLeft = 0;
            PanelDataHolderWrapper.CornerRadiusTopRight = 0;
            PanelDataHolderWrapper.DisplayMode = ExtendedUI.DisplayMode.Grid;
            PanelDataHolderWrapper.Dock = DockStyle.Fill;
            PanelDataHolderWrapper.GradientColors.Add(Color.DeepSkyBlue);
            PanelDataHolderWrapper.GradientColors.Add(Color.MediumBlue);
            PanelDataHolderWrapper.GradientOpacity = 0.2F;
            PanelDataHolderWrapper.GridAutoColumnWidth = false;
            PanelDataHolderWrapper.GridAutoRowHeight = false;
            PanelDataHolderWrapper.GridColumnCount = 1;
            PanelDataHolderWrapper.GridRowCount = 5;
            PanelDataHolderWrapper.Location = new Point(10, 123);
            PanelDataHolderWrapper.Margin = new Padding(0);
            PanelDataHolderWrapper.Name = "PanelDataHolderWrapper";
            PanelDataHolderWrapper.Padding = new Padding(6);
            PanelDataHolderWrapper.Size = new Size(989, 314);
            PanelDataHolderWrapper.TabIndex = 3;
            // 
            // extendedPanel1
            // 
            extendedPanel1.AlignItems = ExtendedUI.AlignItems.Center;
            extendedPanel1.BackColor = Color.Transparent;
            extendedPanel1.BlurTint = Color.FromArgb(40, 255, 255, 255);
            extendedPanel1.BorderColor = Color.Transparent;
            extendedPanel1.BorderWidth = 1;
            extendedPanel1.Controls.Add(comboBoxHostname);
            extendedPanel1.Controls.Add(label2);
            extendedPanel1.DisplayMode = ExtendedUI.DisplayMode.Flex;
            extendedPanel1.FlexInvert = true;
            extendedPanel1.JustifyContent = ExtendedUI.JustifyContent.Center;
            extendedPanel1.Location = new Point(9, 9);
            extendedPanel1.Name = "extendedPanel1";
            extendedPanel1.Padding = new Padding(6);
            extendedPanel1.Size = new Size(971, 49);
            extendedPanel1.TabIndex = 5;
            // 
            // comboBoxHostname
            // 
            comboBoxHostname.Font = new Font("Gadugi", 15.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
            comboBoxHostname.FormattingEnabled = true;
            comboBoxHostname.Location = new Point(385, 8);
            comboBoxHostname.Margin = new Padding(0);
            comboBoxHostname.Name = "comboBoxHostname";
            comboBoxHostname.Size = new Size(407, 33);
            comboBoxHostname.TabIndex = 2;
            // 
            // label2
            // 
            label2.Font = new Font("Gadugi", 15.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
            label2.ForeColor = Color.MediumBlue;
            label2.Location = new Point(182, 5);
            label2.Name = "label2";
            label2.Size = new Size(200, 40);
            label2.TabIndex = 0;
            label2.Text = "Host name :";
            label2.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // extendedPanel5
            // 
            extendedPanel5.AlignItems = ExtendedUI.AlignItems.Center;
            extendedPanel5.BackColor = Color.Transparent;
            extendedPanel5.BlurTint = Color.FromArgb(40, 255, 255, 255);
            extendedPanel5.BorderColor = Color.Transparent;
            extendedPanel5.BorderWidth = 1;
            extendedPanel5.Controls.Add(comboBoxAuth);
            extendedPanel5.Controls.Add(label1);
            extendedPanel5.DisplayMode = ExtendedUI.DisplayMode.Flex;
            extendedPanel5.FlexInvert = true;
            extendedPanel5.JustifyContent = ExtendedUI.JustifyContent.Center;
            extendedPanel5.Location = new Point(9, 70);
            extendedPanel5.Name = "extendedPanel5";
            extendedPanel5.Padding = new Padding(6);
            extendedPanel5.Size = new Size(971, 49);
            extendedPanel5.TabIndex = 4;
            // 
            // comboBoxAuth
            // 
            comboBoxAuth.Font = new Font("Gadugi", 15.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
            comboBoxAuth.FormattingEnabled = true;
            comboBoxAuth.Items.AddRange(new object[] { "Windows Authentication", "Sql Server Authentication" });
            comboBoxAuth.Location = new Point(385, 8);
            comboBoxAuth.Margin = new Padding(0);
            comboBoxAuth.Name = "comboBoxAuth";
            comboBoxAuth.Size = new Size(407, 33);
            comboBoxAuth.TabIndex = 2;
            comboBoxAuth.SelectedIndexChanged += comboBoxAuth_SelectedIndexChanged;
            // 
            // label1
            // 
            label1.Font = new Font("Gadugi", 15.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
            label1.ForeColor = Color.MediumBlue;
            label1.Location = new Point(182, 5);
            label1.Name = "label1";
            label1.Size = new Size(200, 40);
            label1.TabIndex = 0;
            label1.Text = "Authentication :";
            label1.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // extendedPanel4
            // 
            extendedPanel4.AlignItems = ExtendedUI.AlignItems.Center;
            extendedPanel4.BackColor = Color.Transparent;
            extendedPanel4.BlurTint = Color.FromArgb(40, 255, 255, 255);
            extendedPanel4.BorderColor = Color.Transparent;
            extendedPanel4.BorderWidth = 1;
            extendedPanel4.Controls.Add(textBoxUserName);
            extendedPanel4.Controls.Add(label5);
            extendedPanel4.DisplayMode = ExtendedUI.DisplayMode.Flex;
            extendedPanel4.FlexInvert = true;
            extendedPanel4.JustifyContent = ExtendedUI.JustifyContent.Center;
            extendedPanel4.Location = new Point(9, 131);
            extendedPanel4.Name = "extendedPanel4";
            extendedPanel4.Padding = new Padding(6);
            extendedPanel4.Size = new Size(971, 49);
            extendedPanel4.TabIndex = 8;
            // 
            // textBoxUserName
            // 
            textBoxUserName.Font = new Font("Gadugi", 15.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
            textBoxUserName.Location = new Point(385, 7);
            textBoxUserName.Margin = new Padding(0);
            textBoxUserName.Name = "textBoxUserName";
            textBoxUserName.Size = new Size(407, 35);
            textBoxUserName.TabIndex = 2;
            // 
            // label5
            // 
            label5.Font = new Font("Gadugi", 15.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
            label5.ForeColor = Color.MediumBlue;
            label5.Location = new Point(182, 5);
            label5.Name = "label5";
            label5.Size = new Size(200, 40);
            label5.TabIndex = 0;
            label5.Text = "User Name :";
            label5.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // extendedPanel3
            // 
            extendedPanel3.AlignItems = ExtendedUI.AlignItems.Center;
            extendedPanel3.BackColor = Color.Transparent;
            extendedPanel3.BlurTint = Color.FromArgb(40, 255, 255, 255);
            extendedPanel3.BorderColor = Color.Transparent;
            extendedPanel3.BorderWidth = 1;
            extendedPanel3.Controls.Add(textBoxPassword);
            extendedPanel3.Controls.Add(label4);
            extendedPanel3.DisplayMode = ExtendedUI.DisplayMode.Flex;
            extendedPanel3.FlexInvert = true;
            extendedPanel3.JustifyContent = ExtendedUI.JustifyContent.Center;
            extendedPanel3.Location = new Point(9, 192);
            extendedPanel3.Name = "extendedPanel3";
            extendedPanel3.Padding = new Padding(6);
            extendedPanel3.Size = new Size(971, 49);
            extendedPanel3.TabIndex = 7;
            // 
            // textBoxPassword
            // 
            textBoxPassword.Font = new Font("Gadugi", 15.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
            textBoxPassword.Location = new Point(385, 7);
            textBoxPassword.Margin = new Padding(0);
            textBoxPassword.Name = "textBoxPassword";
            textBoxPassword.Size = new Size(407, 35);
            textBoxPassword.TabIndex = 2;
            // 
            // label4
            // 
            label4.Font = new Font("Gadugi", 15.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
            label4.ForeColor = Color.MediumBlue;
            label4.Location = new Point(182, 5);
            label4.Name = "label4";
            label4.Size = new Size(200, 40);
            label4.TabIndex = 0;
            label4.Text = "Password :";
            label4.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // extendedPanel2
            // 
            extendedPanel2.AlignItems = ExtendedUI.AlignItems.Center;
            extendedPanel2.BackColor = Color.Transparent;
            extendedPanel2.BlurTint = Color.FromArgb(40, 255, 255, 255);
            extendedPanel2.BorderColor = Color.Transparent;
            extendedPanel2.BorderWidth = 1;
            extendedPanel2.Controls.Add(ComboboxDatabaseName);
            extendedPanel2.Controls.Add(label3);
            extendedPanel2.DisplayMode = ExtendedUI.DisplayMode.Flex;
            extendedPanel2.FlexInvert = true;
            extendedPanel2.JustifyContent = ExtendedUI.JustifyContent.Center;
            extendedPanel2.Location = new Point(9, 253);
            extendedPanel2.Name = "extendedPanel2";
            extendedPanel2.Padding = new Padding(6);
            extendedPanel2.Size = new Size(971, 49);
            extendedPanel2.TabIndex = 6;
            // 
            // ComboboxDatabaseName
            // 
            ComboboxDatabaseName.Font = new Font("Gadugi", 15.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
            ComboboxDatabaseName.FormattingEnabled = true;
            ComboboxDatabaseName.Location = new Point(385, 8);
            ComboboxDatabaseName.Margin = new Padding(0);
            ComboboxDatabaseName.Name = "ComboboxDatabaseName";
            ComboboxDatabaseName.Size = new Size(407, 33);
            ComboboxDatabaseName.TabIndex = 1;
            ComboboxDatabaseName.DropDown += ComboboxDatabaseName_DropDown;
            // 
            // label3
            // 
            label3.Font = new Font("Gadugi", 15.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
            label3.ForeColor = Color.MediumBlue;
            label3.Location = new Point(182, 5);
            label3.Name = "label3";
            label3.Size = new Size(200, 40);
            label3.TabIndex = 0;
            label3.Text = "Database name :";
            label3.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // PanelFooter
            // 
            PanelFooter.BackColor = Color.White;
            PanelFooter.BlurTint = Color.FromArgb(40, 255, 255, 255);
            PanelFooter.BorderColor = Color.Transparent;
            PanelFooter.BorderWidth = 1;
            PanelFooter.Controls.Add(panelDiscovery);
            PanelFooter.Controls.Add(extendedPanel6);
            PanelFooter.CornerRadius = 0;
            PanelFooter.CornerRadiusBottomLeft = 20;
            PanelFooter.CornerRadiusBottomRight = 20;
            PanelFooter.CornerRadiusTopLeft = 0;
            PanelFooter.CornerRadiusTopRight = 0;
            PanelFooter.Dock = DockStyle.Bottom;
            PanelFooter.GradientColors.Add(Color.DeepSkyBlue);
            PanelFooter.GradientColors.Add(Color.MediumBlue);
            PanelFooter.GradientOpacity = 0.5F;
            PanelFooter.Location = new Point(10, 437);
            PanelFooter.Name = "PanelFooter";
            PanelFooter.Padding = new Padding(18);
            PanelFooter.Size = new Size(989, 105);
            PanelFooter.TabIndex = 2;
            // 
            // panelDiscovery
            // 
            panelDiscovery.BackColor = Color.Transparent;
            panelDiscovery.Controls.Add(toggleNetworkDiscovery);
            panelDiscovery.Dock = DockStyle.Left;
            panelDiscovery.Location = new Point(18, 18);
            panelDiscovery.Name = "panelDiscovery";
            panelDiscovery.Size = new Size(350, 69);
            panelDiscovery.TabIndex = 6;
            // 
            // toggleNetworkDiscovery
            // 
            toggleNetworkDiscovery.BackColor = Color.Transparent;
            toggleNetworkDiscovery.Font = new Font("Gadugi", 12F, FontStyle.Bold);
            toggleNetworkDiscovery.ForeColor = Color.MediumBlue;
            toggleNetworkDiscovery.Location = new Point(10, 19);
            toggleNetworkDiscovery.Name = "toggleNetworkDiscovery";
            toggleNetworkDiscovery.Size = new Size(300, 30);
            toggleNetworkDiscovery.TabIndex = 0;
            toggleNetworkDiscovery.Text = "Network Server Discovery";
            toggleNetworkDiscovery.UseVisualStyleBackColor = true;
            toggleNetworkDiscovery.CheckedChanged += toggleNetworkDiscovery_CheckedChanged;
            // 
            // extendedPanel6
            // 
            extendedPanel6.BackColor = Color.White;
            extendedPanel6.BlurTint = Color.FromArgb(40, 255, 255, 255);
            extendedPanel6.BorderColor = Color.Transparent;
            extendedPanel6.BorderWidth = 1;
            extendedPanel6.Controls.Add(ButtonTestConnection);
            extendedPanel6.Controls.Add(ButtonSave);
            extendedPanel6.CornerRadius = 17;
            extendedPanel6.CornerRadiusBottomLeft = 17;
            extendedPanel6.CornerRadiusBottomRight = 17;
            extendedPanel6.CornerRadiusTopLeft = 17;
            extendedPanel6.CornerRadiusTopRight = 17;
            extendedPanel6.DisplayMode = ExtendedUI.DisplayMode.Grid;
            extendedPanel6.Dock = DockStyle.Right;
            extendedPanel6.GridAutoColumnWidth = false;
            extendedPanel6.GridAutoRowHeight = false;
            extendedPanel6.GridColumnCount = 2;
            extendedPanel6.GridColumnGap = 10;
            extendedPanel6.Location = new Point(575, 18);
            extendedPanel6.Name = "extendedPanel6";
            extendedPanel6.Padding = new Padding(10);
            extendedPanel6.Size = new Size(396, 69);
            extendedPanel6.TabIndex = 5;
            // 
            // ButtonTestConnection
            // 
            ButtonTestConnection.AutoLog = true;
            ButtonTestConnection.BackColor = Color.DeepSkyBlue;
            ButtonTestConnection.BackColor2 = Color.MediumBlue;
            ButtonTestConnection.BorderColor = Color.Transparent;
            ButtonTestConnection.BorderThickness = 2;
            ButtonTestConnection.CornerRadius = 12;
            ButtonTestConnection.CornerRadiusBottomLeft = 0;
            ButtonTestConnection.CornerRadiusBottomRight = 0;
            ButtonTestConnection.CornerRadiusTopLeft = 0;
            ButtonTestConnection.CornerRadiusTopRight = 0;
            ButtonTestConnection.FlatAppearance.BorderSize = 0;
            ButtonTestConnection.FlatStyle = FlatStyle.Flat;
            ButtonTestConnection.FocusBorderColor = Color.DeepSkyBlue;
            ButtonTestConnection.Font = new Font("Gadugi", 12F, FontStyle.Bold);
            ButtonTestConnection.ForeColor = Color.White;
            ButtonTestConnection.GradientMode = System.Drawing.Drawing2D.LinearGradientMode.Horizontal;
            ButtonTestConnection.HoverBackColor = Color.MediumBlue;
            ButtonTestConnection.HoverBackColor2 = Color.DarkOrchid;
            ButtonTestConnection.HoverBorderColor = Color.Transparent;
            ButtonTestConnection.HoverFontScale = 1.02F;
            ButtonTestConnection.HoverForeColor = Color.White;
            ButtonTestConnection.IconImage = null;
            ButtonTestConnection.IconPermanent = true;
            ButtonTestConnection.IconSize = 18;
            ButtonTestConnection.IconTextSpacing = 8;
            ButtonTestConnection.Location = new Point(10, 10);
            ButtonTestConnection.Margin = new Padding(0);
            ButtonTestConnection.Name = "ButtonTestConnection";
            ButtonTestConnection.Padding = new Padding(14, 8, 14, 8);
            ButtonTestConnection.Preset = ExtendedUI.ExtendedButton.ButtonVisualPreset.Normal;
            ButtonTestConnection.Selected = false;
            ButtonTestConnection.ShadowOpacity = 40;
            ButtonTestConnection.ShadowSize = 6;
            ButtonTestConnection.ShowShadow = false;
            ButtonTestConnection.SidebarMode = ExtendedUI.ExtendedButton.SidebarSize.Large;
            ButtonTestConnection.Size = new Size(183, 49);
            ButtonTestConnection.TabIndex = 5;
            ButtonTestConnection.Text = "Test Connection";
            ButtonTestConnection.UsedInSidebar = false;
            ButtonTestConnection.UseVisualStyleBackColor = false;
            ButtonTestConnection.Click += ButtonTestConnection_Click;
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
            ButtonSave.Preset = ExtendedUI.ExtendedButton.ButtonVisualPreset.Normal;
            ButtonSave.Selected = false;
            ButtonSave.ShadowOpacity = 40;
            ButtonSave.ShadowSize = 6;
            ButtonSave.ShowShadow = false;
            ButtonSave.SidebarMode = ExtendedUI.ExtendedButton.SidebarSize.Large;
            ButtonSave.Size = new Size(183, 49);
            ButtonSave.TabIndex = 4;
            ButtonSave.Text = "Save";
            ButtonSave.UsedInSidebar = false;
            ButtonSave.UseVisualStyleBackColor = false;
            ButtonSave.Click += ButtonSave_Click;
            // 
            // PanelHeader
            // 
            PanelHeader.BackColor = Color.White;
            PanelHeader.BlurTint = Color.FromArgb(40, 255, 255, 255);
            PanelHeader.BorderColor = Color.Transparent;
            PanelHeader.BorderWidth = 1;
            PanelHeader.Controls.Add(LabelHeader);
            PanelHeader.CornerRadius = 0;
            PanelHeader.CornerRadiusBottomLeft = 0;
            PanelHeader.CornerRadiusBottomRight = 0;
            PanelHeader.CornerRadiusTopLeft = 20;
            PanelHeader.CornerRadiusTopRight = 20;
            PanelHeader.Dock = DockStyle.Top;
            PanelHeader.GradientColors.Add(Color.DeepSkyBlue);
            PanelHeader.GradientColors.Add(Color.MediumBlue);
            PanelHeader.GradientOpacity = 0.5F;
            PanelHeader.Location = new Point(10, 10);
            PanelHeader.Name = "PanelHeader";
            PanelHeader.Padding = new Padding(6);
            PanelHeader.Size = new Size(989, 113);
            PanelHeader.TabIndex = 1;
            // 
            // LabelHeader
            // 
            LabelHeader.BackColor = Color.Transparent;
            LabelHeader.Dock = DockStyle.Fill;
            LabelHeader.Font = new Font("Gadugi", 26.25F, FontStyle.Bold, GraphicsUnit.Point, 0);
            LabelHeader.ForeColor = Color.MediumBlue;
            LabelHeader.Location = new Point(6, 6);
            LabelHeader.Name = "LabelHeader";
            LabelHeader.Size = new Size(977, 101);
            LabelHeader.TabIndex = 0;
            LabelHeader.Text = "Sql Manager";
            LabelHeader.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // FormSqlConnectionManager
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1009, 552);
            Controls.Add(PanelWrapper);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            Name = "FormSqlConnectionManager";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "FormSqlConnectionManager";
            FormClosing += FormSqlConnectionManager_FormClosing;
            Load += FormSqlConnectionManager_Load;
            PanelWrapper.ResumeLayout(false);
            PanelDataHolderWrapper.ResumeLayout(false);
            extendedPanel1.ResumeLayout(false);
            extendedPanel5.ResumeLayout(false);
            extendedPanel4.ResumeLayout(false);
            extendedPanel4.PerformLayout();
            extendedPanel3.ResumeLayout(false);
            extendedPanel3.PerformLayout();
            extendedPanel2.ResumeLayout(false);
            PanelFooter.ResumeLayout(false);
            panelDiscovery.ResumeLayout(false);
            extendedPanel6.ResumeLayout(false);
            PanelHeader.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private Aarohi.ExtendedUI.ExtendedPanel PanelWrapper;
        private Aarohi.ExtendedUI.ExtendedPanel PanelHeader;
        private Aarohi.ExtendedUI.ExtendedPanel PanelDataHolderWrapper;
        private Aarohi.ExtendedUI.ExtendedPanel PanelFooter;
        private Label LabelHeader;
        private Aarohi.ExtendedUI.ExtendedPanel extendedPanel5;
        private Label label1;
        private Aarohi.ExtendedUI.ExtendedPanel extendedPanel1;
        private Label label2;
        private Aarohi.ExtendedUI.ExtendedPanel extendedPanel4;
        private TextBox textBoxUserName;
        private Label label5;
        private Aarohi.ExtendedUI.ExtendedPanel extendedPanel3;
        private TextBox textBoxPassword;
        private Label label4;
        private Aarohi.ExtendedUI.ExtendedPanel extendedPanel2;
        private ComboBox ComboboxDatabaseName;
        private Label label3;
        private ComboBox comboBoxAuth;
        private ComboBox comboBoxHostname;
        private Aarohi.ExtendedUI.ExtendedPanel extendedPanel6;
        private Aarohi.ExtendedUI.ExtendedButton ButtonTestConnection;
        private Aarohi.ExtendedUI.ExtendedButton ButtonSave;
        private System.Windows.Forms.Panel panelDiscovery;
        private Aarohi.SQL.ToggleSwitch toggleNetworkDiscovery;
    }
}