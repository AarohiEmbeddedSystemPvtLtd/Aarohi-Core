using System.Drawing;
using System.Windows.Forms;

namespace Aarohi.Classes
{ 
    partial class DualTextbox
    {
        private System.ComponentModel.IContainer components = null;

        #region Component Designer generated code
        private void InitializeComponent()
        {
            panel1 = new Panel();
            textBoxPrefix = new TextBox();
            divider = new Panel();
            textBoxSuffix = new TextBox();
            panel1.SuspendLayout();
            SuspendLayout();
            panel1.Controls.Add(textBoxSuffix);
            panel1.Controls.Add(divider);
            panel1.Controls.Add(textBoxPrefix);
            panel1.Dock = DockStyle.Fill;
            panel1.Name = "panel1";
            panel1.TabIndex = 0;
            textBoxPrefix.Name = "textBoxPrefix";
            textBoxPrefix.PlaceholderText = "Enter prefix...";
            textBoxPrefix.TabIndex = 0;
            divider.Name = "divider";
            divider.Visible = false;
            textBoxSuffix.Name = "textBoxSuffix";
            textBoxSuffix.PlaceholderText = "Enter suffix...";
            textBoxSuffix.TabIndex = 1;
            textBoxSuffix.Visible = false;
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            Controls.Add(panel1);
            Name = "DualTextbox";
            Size = new Size(353, 45);
            panel1.ResumeLayout(false);
            ResumeLayout(false);
        }

        private Panel panel1;
        private Panel divider;
        private TextBox textBoxPrefix;
        private TextBox textBoxSuffix;

        #endregion
    }
}