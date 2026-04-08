using System.Drawing;
using System.Windows.Forms;

namespace Aarohi.Classes
{
    partial class DataInput
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
        private void InitializeComponent()
        {
            panelLabel = new Panel();
            labelName = new Label();
            panelInput = new Panel();
            panelLabel.SuspendLayout();
            SuspendLayout();
            // 
            // panelLabel
            // 
            panelLabel.Controls.Add(labelName);
            panelLabel.Dock = DockStyle.Top;
            panelLabel.Location = new Point(5, 5);
            panelLabel.Name = "panelLabel";
            panelLabel.Size = new Size(635, 40);
            panelLabel.TabIndex = 0;
            // 
            // labelName
            // 
            labelName.Dock = DockStyle.Fill;
            labelName.Font = new Font("Gadugi", 15.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
            labelName.Location = new Point(0, 0);
            labelName.Name = "labelName";
            labelName.Padding = new Padding(10, 0, 0, 0);
            labelName.Size = new Size(635, 40);
            labelName.TabIndex = 0;
            labelName.Text = "label1";
            labelName.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // panelInput
            // 
            panelInput.Dock = DockStyle.Fill;
            panelInput.Location = new Point(5, 45);
            panelInput.Name = "panelInput";
            panelInput.Padding = new Padding(10, 0, 0, 0);
            panelInput.Size = new Size(635, 50);
            panelInput.TabIndex = 1;
            // 
            // DataInput
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.White;
            Controls.Add(panelInput);
            Controls.Add(panelLabel);
            Name = "DataInput";
            Padding = new Padding(5);
            Size = new Size(645, 100);
            panelLabel.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private Panel panelLabel;
        private Panel panelInput;
        public Label labelName;
    }
}

