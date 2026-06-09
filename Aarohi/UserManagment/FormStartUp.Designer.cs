using System.Drawing;
using System.Windows.Forms;

namespace Aarohi.UserManagment
{
    partial class FormStartUp
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
            PanelMainWrapperBorder = new Aarohi.ExtendedUI.ExtendedPanel();
            PanelMainWrapper = new Aarohi.ExtendedUI.ExtendedPanel();
            PanelForm = new Panel();
            LoadingWrapper = new Panel();
            panel2 = new Panel();
            lblStatus = new Label();
            progressBar1 = new ProgressBar();
            LoginWrapper = new Aarohi.ExtendedUI.ExtendedPanel();
            LoginElementWrapper = new Aarohi.ExtendedUI.ExtendedPanel();
            PanelLoginElementWrapper = new Aarohi.ExtendedUI.ExtendedPanel();
            LoginUsernameWrapper = new Aarohi.ExtendedUI.ExtendedPanel();
            labelUsername = new Label();
            comboBoxUsername = new ComboBox();
            extendedPanel1 = new Aarohi.ExtendedUI.ExtendedPanel();
            labelPassword = new Label();
            textBox2 = new TextBox();
            button1 = new Button();
            PanelRememberMeWrapper = new Aarohi.ExtendedUI.ExtendedPanel();
            checkBoxRememberMe = new CheckBox();
            LoginButtonWrapper = new Aarohi.ExtendedUI.ExtendedPanel();
            LoginButtonPanel = new Aarohi.ExtendedUI.ExtendedPanel();
            LoginButton = new Aarohi.ExtendedUI.ExtendedButton();
            PanelLoginLabelHolder = new Aarohi.ExtendedUI.ExtendedPanel();
            labelLogin = new Label();
            labelSoftName = new Label();
            panelLoadder = new Panel();
            PanelMainWrapperBorder.SuspendLayout();
            PanelMainWrapper.SuspendLayout();
            PanelForm.SuspendLayout();
            LoadingWrapper.SuspendLayout();
            panel2.SuspendLayout();
            LoginWrapper.SuspendLayout();
            LoginElementWrapper.SuspendLayout();
            PanelLoginElementWrapper.SuspendLayout();
            LoginUsernameWrapper.SuspendLayout();
            extendedPanel1.SuspendLayout();
            PanelRememberMeWrapper.SuspendLayout();
            LoginButtonWrapper.SuspendLayout();
            LoginButtonPanel.SuspendLayout();
            PanelLoginLabelHolder.SuspendLayout();
            SuspendLayout();
            // 
            // PanelMainWrapperBorder
            // 
            PanelMainWrapperBorder.BackColor = Color.White;
            PanelMainWrapperBorder.BlurTint = Color.FromArgb(40, 255, 255, 255);
            PanelMainWrapperBorder.BorderColor = Color.Transparent;
            PanelMainWrapperBorder.BorderWidth = 1;
            PanelMainWrapperBorder.Controls.Add(PanelMainWrapper);
            PanelMainWrapperBorder.CornerRadius = 75;
            PanelMainWrapperBorder.CornerRadiusBottomLeft = 75;
            PanelMainWrapperBorder.CornerRadiusBottomRight = 75;
            PanelMainWrapperBorder.CornerRadiusTopLeft = 75;
            PanelMainWrapperBorder.CornerRadiusTopRight = 75;
            PanelMainWrapperBorder.Dock = DockStyle.Fill;
            PanelMainWrapperBorder.GradientColors.Add(Color.Orange);
            PanelMainWrapperBorder.GradientColors.Add(Color.MediumBlue);
            PanelMainWrapperBorder.Location = new Point(20, 20);
            PanelMainWrapperBorder.Name = "PanelMainWrapperBorder";
            PanelMainWrapperBorder.Padding = new Padding(10);
            PanelMainWrapperBorder.Size = new Size(1153, 458);
            PanelMainWrapperBorder.TabIndex = 0;
            // 
            // PanelMainWrapper
            // 
            PanelMainWrapper.BackColor = Color.White;
            PanelMainWrapper.BackgroundMode = ExtendedUI.BgMode.None;
            PanelMainWrapper.BlurOpacity = 0.8F;
            PanelMainWrapper.BlurRadius = 0;
            PanelMainWrapper.BlurTint = Color.FromArgb(40, 255, 255, 255);
            PanelMainWrapper.BorderColor = Color.Transparent;
            PanelMainWrapper.BorderWidth = 1;
            PanelMainWrapper.Controls.Add(PanelForm);
            PanelMainWrapper.Controls.Add(panelLoadder);
            PanelMainWrapper.CornerRadius = 70;
            PanelMainWrapper.CornerRadiusBottomLeft = 70;
            PanelMainWrapper.CornerRadiusBottomRight = 70;
            PanelMainWrapper.CornerRadiusTopLeft = 70;
            PanelMainWrapper.CornerRadiusTopRight = 70;
            PanelMainWrapper.Dock = DockStyle.Fill;
            PanelMainWrapper.GradientColors.Add(Color.DeepSkyBlue);
            PanelMainWrapper.GradientColors.Add(Color.MediumBlue);
            PanelMainWrapper.Location = new Point(10, 10);
            PanelMainWrapper.Name = "PanelMainWrapper";
            PanelMainWrapper.Padding = new Padding(6);
            PanelMainWrapper.Size = new Size(1133, 438);
            PanelMainWrapper.TabIndex = 1;
            // 
            // PanelForm
            // 
            PanelForm.BackColor = Color.White;
            PanelForm.Controls.Add(LoadingWrapper);
            PanelForm.Controls.Add(LoginWrapper);
            PanelForm.Dock = DockStyle.Fill;
            PanelForm.Location = new Point(506, 6);
            PanelForm.Name = "PanelForm";
            PanelForm.Padding = new Padding(10);
            PanelForm.Size = new Size(621, 426);
            PanelForm.TabIndex = 0;
            // 
            // LoadingWrapper
            // 
            LoadingWrapper.BackColor = Color.White;
            LoadingWrapper.Controls.Add(panel2);
            LoadingWrapper.Dock = DockStyle.Fill;
            LoadingWrapper.Location = new Point(10, 10);
            LoadingWrapper.Name = "LoadingWrapper";
            LoadingWrapper.Padding = new Padding(100, 190, 100, 190);
            LoadingWrapper.Size = new Size(601, 406);
            LoadingWrapper.TabIndex = 8;
            LoadingWrapper.Visible = false;
            // 
            // panel2
            // 
            panel2.Controls.Add(lblStatus);
            panel2.Controls.Add(progressBar1);
            panel2.Dock = DockStyle.Fill;
            panel2.Location = new Point(100, 190);
            panel2.Name = "panel2";
            panel2.Size = new Size(401, 26);
            panel2.TabIndex = 0;
            // 
            // lblStatus
            // 
            lblStatus.BackColor = Color.White;
            lblStatus.Dock = DockStyle.Fill;
            lblStatus.FlatStyle = FlatStyle.System;
            lblStatus.ForeColor = Color.MediumBlue;
            lblStatus.Location = new Point(0, 0);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(401, 14);
            lblStatus.TabIndex = 2;
            // 
            // progressBar1
            // 
            progressBar1.BackColor = Color.White;
            progressBar1.Dock = DockStyle.Bottom;
            progressBar1.Location = new Point(0, 14);
            progressBar1.Name = "progressBar1";
            progressBar1.Size = new Size(401, 12);
            progressBar1.TabIndex = 1;
            // 
            // LoginWrapper
            // 
            LoginWrapper.BackColor = Color.White;
            LoginWrapper.BlurTint = Color.FromArgb(40, 255, 255, 255);
            LoginWrapper.BorderColor = Color.Transparent;
            LoginWrapper.BorderWidth = 1;
            LoginWrapper.Controls.Add(LoginElementWrapper);
            LoginWrapper.Controls.Add(LoginButtonWrapper);
            LoginWrapper.Controls.Add(PanelLoginLabelHolder);
            LoginWrapper.Controls.Add(labelSoftName);
            LoginWrapper.CornerRadius = 50;
            LoginWrapper.CornerRadiusBottomLeft = 0;
            LoginWrapper.CornerRadiusBottomRight = 50;
            LoginWrapper.CornerRadiusTopLeft = 0;
            LoginWrapper.CornerRadiusTopRight = 50;
            LoginWrapper.Dock = DockStyle.Fill;
            LoginWrapper.GradientColors.Add(Color.White);
            LoginWrapper.GradientColors.Add(Color.MediumBlue);
            LoginWrapper.GradientOpacity = 0.3F;
            LoginWrapper.Location = new Point(10, 10);
            LoginWrapper.Name = "LoginWrapper";
            LoginWrapper.Padding = new Padding(20);
            LoginWrapper.Size = new Size(601, 406);
            LoginWrapper.TabIndex = 0;
            // 
            // LoginElementWrapper
            // 
            LoginElementWrapper.BackColor = Color.Transparent;
            LoginElementWrapper.BlurRadius = 0;
            LoginElementWrapper.BlurTint = Color.FromArgb(40, 255, 255, 255);
            LoginElementWrapper.BorderColor = Color.Transparent;
            LoginElementWrapper.BorderWidth = 1;
            LoginElementWrapper.Controls.Add(PanelLoginElementWrapper);
            LoginElementWrapper.Controls.Add(PanelRememberMeWrapper);
            LoginElementWrapper.CornerRadius = 30;
            LoginElementWrapper.CornerRadiusBottomLeft = 30;
            LoginElementWrapper.CornerRadiusBottomRight = 30;
            LoginElementWrapper.CornerRadiusTopLeft = 0;
            LoginElementWrapper.CornerRadiusTopRight = 0;
            LoginElementWrapper.Dock = DockStyle.Fill;
            LoginElementWrapper.GradientColors.Add(Color.Orange);
            LoginElementWrapper.GradientColors.Add(Color.MediumBlue);
            LoginElementWrapper.GradientOpacity = 0.2F;
            LoginElementWrapper.Location = new Point(20, 130);
            LoginElementWrapper.Name = "LoginElementWrapper";
            LoginElementWrapper.Padding = new Padding(6);
            LoginElementWrapper.Size = new Size(561, 184);
            LoginElementWrapper.TabIndex = 2;
            // 
            // PanelLoginElementWrapper
            // 
            PanelLoginElementWrapper.BackColor = Color.Transparent;
            PanelLoginElementWrapper.BlurTint = Color.FromArgb(40, 255, 255, 255);
            PanelLoginElementWrapper.BorderColor = Color.Transparent;
            PanelLoginElementWrapper.BorderWidth = 1;
            PanelLoginElementWrapper.Controls.Add(LoginUsernameWrapper);
            PanelLoginElementWrapper.Controls.Add(extendedPanel1);
            PanelLoginElementWrapper.DisplayMode = ExtendedUI.DisplayMode.Grid;
            PanelLoginElementWrapper.Dock = DockStyle.Fill;
            PanelLoginElementWrapper.GridAutoColumnWidth = false;
            PanelLoginElementWrapper.GridAutoRowHeight = false;
            PanelLoginElementWrapper.GridColumnCount = 1;
            PanelLoginElementWrapper.GridRowCount = 2;
            PanelLoginElementWrapper.Location = new Point(6, 6);
            PanelLoginElementWrapper.Name = "PanelLoginElementWrapper";
            PanelLoginElementWrapper.Padding = new Padding(6);
            PanelLoginElementWrapper.Size = new Size(549, 142);
            PanelLoginElementWrapper.TabIndex = 0;
            PanelLoginElementWrapper.Paint += PanelLoginElementWrapper_Paint;
            // 
            // LoginUsernameWrapper
            // 
            LoginUsernameWrapper.AlignItems = ExtendedUI.AlignItems.Center;
            LoginUsernameWrapper.BackColor = Color.Transparent;
            LoginUsernameWrapper.BlurTint = Color.FromArgb(40, 255, 255, 255);
            LoginUsernameWrapper.BorderColor = Color.Transparent;
            LoginUsernameWrapper.BorderWidth = 1;
            LoginUsernameWrapper.Controls.Add(labelUsername);
            LoginUsernameWrapper.Controls.Add(comboBoxUsername);
            LoginUsernameWrapper.DisplayMode = ExtendedUI.DisplayMode.Flex;
            LoginUsernameWrapper.Dock = DockStyle.Fill;
            LoginUsernameWrapper.GridAutoColumnWidth = false;
            LoginUsernameWrapper.GridAutoRowHeight = false;
            LoginUsernameWrapper.GridColumnCount = 1;
            LoginUsernameWrapper.GridRowCount = 2;
            LoginUsernameWrapper.Location = new Point(9, 9);
            LoginUsernameWrapper.Name = "LoginUsernameWrapper";
            LoginUsernameWrapper.Padding = new Padding(6);
            LoginUsernameWrapper.Size = new Size(531, 56);
            LoginUsernameWrapper.TabIndex = 0;
            // 
            // labelUsername
            // 
            labelUsername.AutoSize = true;
            labelUsername.Font = new Font("Gadugi", 14.25F, FontStyle.Bold, GraphicsUnit.Point, 0);
            labelUsername.ForeColor = Color.MediumBlue;
            labelUsername.Location = new Point(9, 17);
            labelUsername.Name = "labelUsername";
            labelUsername.Size = new Size(117, 21);
            labelUsername.TabIndex = 0;
            labelUsername.Text = "User Name :";
            // 
            // comboBoxUsername
            // 
            comboBoxUsername.Font = new Font("Gadugi", 14.25F);
            comboBoxUsername.FormattingEnabled = true;
            comboBoxUsername.Location = new Point(129, 13);
            comboBoxUsername.Margin = new Padding(0);
            comboBoxUsername.Name = "comboBoxUsername";
            comboBoxUsername.Size = new Size(390, 30);
            comboBoxUsername.TabIndex = 1;
            comboBoxUsername.SelectedIndexChanged += comboBoxUsername_SelectedIndexChanged;
            // 
            // extendedPanel1
            // 
            extendedPanel1.AlignItems = ExtendedUI.AlignItems.Center;
            extendedPanel1.BackColor = Color.Transparent;
            extendedPanel1.BlurTint = Color.FromArgb(40, 255, 255, 255);
            extendedPanel1.BorderColor = Color.Transparent;
            extendedPanel1.BorderWidth = 1;
            extendedPanel1.Controls.Add(labelPassword);
            extendedPanel1.Controls.Add(textBox2);
            extendedPanel1.Controls.Add(button1);
            extendedPanel1.DisplayMode = ExtendedUI.DisplayMode.Flex;
            extendedPanel1.Dock = DockStyle.Fill;
            extendedPanel1.FlexLineGap = 10;
            extendedPanel1.GridAutoColumnWidth = false;
            extendedPanel1.GridAutoRowHeight = false;
            extendedPanel1.GridColumnCount = 1;
            extendedPanel1.GridRowCount = 2;
            extendedPanel1.Location = new Point(9, 77);
            extendedPanel1.Name = "extendedPanel1";
            extendedPanel1.Padding = new Padding(6);
            extendedPanel1.Size = new Size(531, 56);
            extendedPanel1.TabIndex = 1;
            // 
            // labelPassword
            // 
            labelPassword.Font = new Font("Gadugi", 14.25F, FontStyle.Bold, GraphicsUnit.Point, 0);
            labelPassword.ForeColor = Color.MediumBlue;
            labelPassword.Location = new Point(9, 17);
            labelPassword.Name = "labelPassword";
            labelPassword.Size = new Size(115, 21);
            labelPassword.TabIndex = 0;
            labelPassword.Text = "Password :  ";
            // 
            // textBox2
            // 
            textBox2.Font = new Font("Gadugi", 14.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
            textBox2.Location = new Point(127, 11);
            textBox2.Margin = new Padding(0);
            textBox2.Name = "textBox2";
            textBox2.Size = new Size(325, 33);
            textBox2.TabIndex = 1;
            textBox2.KeyDown += textBox2_KeyDown;
            // 
            // button1
            // 
            button1.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold, GraphicsUnit.Point, 0);
            button1.Location = new Point(452, 11);
            button1.Margin = new Padding(0);
            button1.Name = "button1";
            button1.Size = new Size(75, 33);
            button1.TabIndex = 2;
            button1.Text = "button1";
            button1.UseVisualStyleBackColor = true;
            button1.Click += button1_Click;
            // 
            // PanelRememberMeWrapper
            // 
            PanelRememberMeWrapper.AlignItems = ExtendedUI.AlignItems.Center;
            PanelRememberMeWrapper.BackColor = Color.Transparent;
            PanelRememberMeWrapper.BlurTint = Color.FromArgb(40, 255, 255, 255);
            PanelRememberMeWrapper.BorderColor = Color.Transparent;
            PanelRememberMeWrapper.BorderWidth = 1;
            PanelRememberMeWrapper.Controls.Add(checkBoxRememberMe);
            PanelRememberMeWrapper.DisplayMode = ExtendedUI.DisplayMode.Flex;
            PanelRememberMeWrapper.Dock = DockStyle.Bottom;
            PanelRememberMeWrapper.JustifyContent = ExtendedUI.JustifyContent.Center;
            PanelRememberMeWrapper.Location = new Point(6, 148);
            PanelRememberMeWrapper.Name = "PanelRememberMeWrapper";
            PanelRememberMeWrapper.Padding = new Padding(6);
            PanelRememberMeWrapper.Size = new Size(549, 30);
            PanelRememberMeWrapper.TabIndex = 1;
            // 
            // checkBoxRememberMe
            // 
            checkBoxRememberMe.AutoSize = true;
            checkBoxRememberMe.Location = new Point(220, 6);
            checkBoxRememberMe.Margin = new Padding(0);
            checkBoxRememberMe.Name = "checkBoxRememberMe";
            checkBoxRememberMe.Size = new Size(109, 19);
            checkBoxRememberMe.TabIndex = 0;
            checkBoxRememberMe.Text = "Remember me?";
            checkBoxRememberMe.UseVisualStyleBackColor = true;
            checkBoxRememberMe.Visible = false;
            // 
            // LoginButtonWrapper
            // 
            LoginButtonWrapper.AlignItems = ExtendedUI.AlignItems.FlexEnd;
            LoginButtonWrapper.BackColor = Color.White;
            LoginButtonWrapper.BlurTint = Color.FromArgb(40, 255, 255, 255);
            LoginButtonWrapper.BorderColor = Color.Transparent;
            LoginButtonWrapper.BorderWidth = 1;
            LoginButtonWrapper.Controls.Add(LoginButtonPanel);
            LoginButtonWrapper.CornerRadius = 0;
            LoginButtonWrapper.CornerRadiusBottomLeft = 0;
            LoginButtonWrapper.CornerRadiusBottomRight = 0;
            LoginButtonWrapper.CornerRadiusTopLeft = 0;
            LoginButtonWrapper.CornerRadiusTopRight = 0;
            LoginButtonWrapper.DisplayMode = ExtendedUI.DisplayMode.Flex;
            LoginButtonWrapper.Dock = DockStyle.Bottom;
            LoginButtonWrapper.GradientColors.Add(Color.White);
            LoginButtonWrapper.GradientColors.Add(Color.MediumBlue);
            LoginButtonWrapper.GradientOpacity = 0.3F;
            LoginButtonWrapper.JustifyContent = ExtendedUI.JustifyContent.Center;
            LoginButtonWrapper.Location = new Point(20, 314);
            LoginButtonWrapper.Name = "LoginButtonWrapper";
            LoginButtonWrapper.Padding = new Padding(0, 1, 0, 0);
            LoginButtonWrapper.Size = new Size(561, 72);
            LoginButtonWrapper.TabIndex = 3;
            // 
            // LoginButtonPanel
            // 
            LoginButtonPanel.AlignItems = ExtendedUI.AlignItems.Center;
            LoginButtonPanel.BackColor = Color.White;
            LoginButtonPanel.BackgroundMode = ExtendedUI.BgMode.None;
            LoginButtonPanel.BlurTint = Color.FromArgb(40, 255, 255, 255);
            LoginButtonPanel.BorderColor = Color.Transparent;
            LoginButtonPanel.BorderWidth = 1;
            LoginButtonPanel.Controls.Add(LoginButton);
            LoginButtonPanel.CornerRadius = 20;
            LoginButtonPanel.CornerRadiusBottomLeft = 20;
            LoginButtonPanel.CornerRadiusBottomRight = 20;
            LoginButtonPanel.CornerRadiusTopLeft = 20;
            LoginButtonPanel.CornerRadiusTopRight = 20;
            LoginButtonPanel.GradientColors.Add(Color.White);
            LoginButtonPanel.GradientColors.Add(Color.MediumBlue);
            LoginButtonPanel.GradientOpacity = 0.3F;
            LoginButtonPanel.JustifyContent = ExtendedUI.JustifyContent.Center;
            LoginButtonPanel.Location = new Point(89, 12);
            LoginButtonPanel.Margin = new Padding(0);
            LoginButtonPanel.Name = "LoginButtonPanel";
            LoginButtonPanel.Padding = new Padding(5);
            LoginButtonPanel.Size = new Size(383, 60);
            LoginButtonPanel.TabIndex = 0;
            // 
            // LoginButton
            // 
            LoginButton.AutoLog = true;
            LoginButton.AutoSize = true;
            LoginButton.BackColor = Color.Orange;
            LoginButton.BackColor2 = Color.MediumBlue;
            LoginButton.BorderColor = Color.Transparent;
            LoginButton.BorderThickness = 3;
            LoginButton.CornerRadius = 12;
            LoginButton.CornerRadiusBottomLeft = 0;
            LoginButton.CornerRadiusBottomRight = 0;
            LoginButton.CornerRadiusTopLeft = 0;
            LoginButton.CornerRadiusTopRight = 0;
            LoginButton.Dock = DockStyle.Fill;
            LoginButton.FlatAppearance.BorderSize = 0;
            LoginButton.FlatStyle = FlatStyle.Flat;
            LoginButton.FocusBorderColor = Color.DarkOrchid;
            LoginButton.Font = new Font("Gadugi", 15.75F, FontStyle.Bold, GraphicsUnit.Point, 0);
            LoginButton.ForeColor = Color.White;
            LoginButton.GradientMode = System.Drawing.Drawing2D.LinearGradientMode.Horizontal;
            LoginButton.HoverBackColor = Color.MediumBlue;
            LoginButton.HoverBackColor2 = Color.Orange;
            LoginButton.HoverBorderColor = Color.Transparent;
            LoginButton.HoverFontScale = 1.02F;
            LoginButton.HoverForeColor = Color.White;
            LoginButton.IconImage = null;
            LoginButton.IconPermanent = true;
            LoginButton.IconSize = 18;
            LoginButton.IconTextSpacing = 8;
            LoginButton.Location = new Point(5, 5);
            LoginButton.Margin = new Padding(0);
            LoginButton.Name = "LoginButton";
            LoginButton.Padding = new Padding(14, 8, 14, 8);
            LoginButton.Preset = ExtendedUI.ExtendedButton.ButtonVisualPreset.Normal;
            LoginButton.Selected = false;
            LoginButton.ShadowOpacity = 40;
            LoginButton.ShadowSize = 6;
            LoginButton.ShowShadow = false;
            LoginButton.SidebarMode = ExtendedUI.ExtendedButton.SidebarSize.Large;
            LoginButton.Size = new Size(373, 50);
            LoginButton.TabIndex = 0;
            LoginButton.Text = "Login";
            LoginButton.UsedInSidebar = false;
            LoginButton.UseVisualStyleBackColor = false;
            LoginButton.Click += LoginButton_Click;
            // 
            // PanelLoginLabelHolder
            // 
            PanelLoginLabelHolder.BackColor = Color.Transparent;
            PanelLoginLabelHolder.BlurRadius = 0;
            PanelLoginLabelHolder.BlurTint = Color.FromArgb(40, 255, 255, 255);
            PanelLoginLabelHolder.BorderColor = Color.Transparent;
            PanelLoginLabelHolder.BorderWidth = 1;
            PanelLoginLabelHolder.Controls.Add(labelLogin);
            PanelLoginLabelHolder.CornerRadius = 30;
            PanelLoginLabelHolder.CornerRadiusBottomLeft = 0;
            PanelLoginLabelHolder.CornerRadiusBottomRight = 0;
            PanelLoginLabelHolder.CornerRadiusTopLeft = 30;
            PanelLoginLabelHolder.CornerRadiusTopRight = 30;
            PanelLoginLabelHolder.Dock = DockStyle.Top;
            PanelLoginLabelHolder.GradientColors.Add(Color.Orange);
            PanelLoginLabelHolder.GradientColors.Add(Color.MediumBlue);
            PanelLoginLabelHolder.GradientOpacity = 0.5F;
            PanelLoginLabelHolder.Location = new Point(20, 68);
            PanelLoginLabelHolder.Name = "PanelLoginLabelHolder";
            PanelLoginLabelHolder.Padding = new Padding(6);
            PanelLoginLabelHolder.Size = new Size(561, 62);
            PanelLoginLabelHolder.TabIndex = 1;
            // 
            // labelLogin
            // 
            labelLogin.Dock = DockStyle.Fill;
            labelLogin.Font = new Font("Georgia", 18F, FontStyle.Bold, GraphicsUnit.Point, 0);
            labelLogin.ForeColor = Color.MediumBlue;
            labelLogin.Location = new Point(6, 6);
            labelLogin.Name = "labelLogin";
            labelLogin.Size = new Size(549, 50);
            labelLogin.TabIndex = 0;
            labelLogin.Text = "Login";
            labelLogin.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // labelSoftName
            // 
            labelSoftName.BackColor = Color.Transparent;
            labelSoftName.Dock = DockStyle.Top;
            labelSoftName.Font = new Font("Gadugi", 20.25F, FontStyle.Bold, GraphicsUnit.Point, 0);
            labelSoftName.ForeColor = Color.MediumBlue;
            labelSoftName.Location = new Point(20, 20);
            labelSoftName.Name = "labelSoftName";
            labelSoftName.Size = new Size(561, 48);
            labelSoftName.TabIndex = 0;
            labelSoftName.Text = "Soft Namw";
            labelSoftName.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // panelLoadder
            // 
            panelLoadder.Dock = DockStyle.Left;
            panelLoadder.Location = new Point(6, 6);
            panelLoadder.Name = "panelLoadder";
            panelLoadder.Padding = new Padding(80);
            panelLoadder.Size = new Size(500, 426);
            panelLoadder.TabIndex = 0;
            // 
            // FormStartUp
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.White;
            ClientSize = new Size(1193, 498);
            Controls.Add(PanelMainWrapperBorder);
            FormBorderStyle = FormBorderStyle.None;
            Name = "FormStartUp";
            Padding = new Padding(20);
            StartPosition = FormStartPosition.CenterScreen;
            Text = "FormStartUp";
            Load += FormStartUp_Load;
            PanelMainWrapperBorder.ResumeLayout(false);
            PanelMainWrapper.ResumeLayout(false);
            PanelForm.ResumeLayout(false);
            LoadingWrapper.ResumeLayout(false);
            panel2.ResumeLayout(false);
            LoginWrapper.ResumeLayout(false);
            LoginElementWrapper.ResumeLayout(false);
            PanelLoginElementWrapper.ResumeLayout(false);
            LoginUsernameWrapper.ResumeLayout(false);
            LoginUsernameWrapper.PerformLayout();
            extendedPanel1.ResumeLayout(false);
            extendedPanel1.PerformLayout();
            PanelRememberMeWrapper.ResumeLayout(false);
            PanelRememberMeWrapper.PerformLayout();
            LoginButtonWrapper.ResumeLayout(false);
            LoginButtonPanel.ResumeLayout(false);
            LoginButtonPanel.PerformLayout();
            PanelLoginLabelHolder.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private Aarohi.ExtendedUI.ExtendedPanel PanelMainWrapper;
        private Aarohi.ExtendedUI.ExtendedPanel PanelMainWrapperBorder;
        private Panel PanelForm;
        private Panel panelLoadder;
        private Aarohi.ExtendedUI.ExtendedPanel LoginWrapper;
        private Aarohi.ExtendedUI.ExtendedPanel LoginElementWrapper;
        private Aarohi.ExtendedUI.ExtendedPanel LoginButtonWrapper;
        private Aarohi.ExtendedUI.ExtendedPanel LoginButtonPanel;
        private Aarohi.ExtendedUI.ExtendedButton LoginButton;
        private Aarohi.ExtendedUI.ExtendedPanel PanelLoginLabelHolder;
        private Label labelLogin;
        private Aarohi.ExtendedUI.ExtendedPanel PanelRememberMeWrapper;
        private CheckBox checkBoxRememberMe;
        private ExtendedUI.ExtendedPanel PanelLoginElementWrapper;
        private ExtendedUI.ExtendedPanel LoginUsernameWrapper;
        private Label labelUsername;
        private ExtendedUI.ExtendedPanel extendedPanel1;
        private Label labelPassword;
        private TextBox textBox2;
        private Label labelSoftName;
        private Button button1;
        private ComboBox comboBoxUsername;
        private Panel LoadingWrapper;
        private Panel panel2;
        private Label lblStatus;
        private ProgressBar progressBar1;
    }
}