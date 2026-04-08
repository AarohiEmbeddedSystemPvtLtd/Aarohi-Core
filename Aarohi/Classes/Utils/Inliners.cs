using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Aarohi.Classes.Utils
{
    public static class Inliners
    {
        public static class InlineTextInput
        {
            /// <summary>
            /// Shows a simple input dialog. Returns the entered text on OK/Enter, or null on Cancel/Esc.
            /// </summary>
            public static string? Show(
                string title = "Input",
                string prompt = "Enter value:",
                string? defaultValue = null,
                IWin32Window? owner = null,
                bool requireNonEmpty = false)
            {
                using var form = new Form
                {
                    Text = string.IsNullOrWhiteSpace(title) ? "Input" : title,
                    FormBorderStyle = FormBorderStyle.FixedDialog,
                    StartPosition = FormStartPosition.CenterParent,
                    MinimizeBox = false,
                    MaximizeBox = false,
                    ShowInTaskbar = false,
                    KeyPreview = true,
                    AutoScaleMode = AutoScaleMode.Dpi,
                    ClientSize = new Size(420, 140)
                };

                var lbl = new Label
                {
                    AutoSize = false,
                    Text = string.IsNullOrWhiteSpace(prompt) ? "Enter value:" : prompt,
                    Location = new Point(12, 12),
                    Size = new Size(form.ClientSize.Width - 24, 36)
                };

                var txt = new TextBox
                {
                    Location = new Point(12, 52),
                    Size = new Size(form.ClientSize.Width - 24, 24),
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
                };
                if (!string.IsNullOrEmpty(defaultValue))
                    txt.Text = defaultValue;

                var btnOK = new Button
                {
                    Text = "OK",
                    DialogResult = DialogResult.OK,
                    Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                    Size = new Size(90, 28),
                    Location = new Point(form.ClientSize.Width - 198, form.ClientSize.Height - 44)
                };

                var btnCancel = new Button
                {
                    Text = "Cancel",
                    DialogResult = DialogResult.Cancel,
                    Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                    Size = new Size(90, 28),
                    Location = new Point(form.ClientSize.Width - 102, form.ClientSize.Height - 44)
                };

                // Enter submits, Esc cancels
                form.AcceptButton = btnOK;
                form.CancelButton = btnCancel;

                // Optional: block empty submission if requireNonEmpty=true
                btnOK.Click += (_, __) =>
                {
                    if (requireNonEmpty && string.IsNullOrWhiteSpace(txt.Text))
                    {
                        System.Media.SystemSounds.Beep.Play();
                        txt.Focus();
                        txt.SelectAll();
                        // Prevent dialog from closing when text is empty
                        form.DialogResult = DialogResult.None;
                    }
                };

                // Pressing Enter inside the textbox triggers OK already via AcceptButton,
                // but we also ensure IME/Multiline-off behavior stays crisp:
                txt.ShortcutsEnabled = true;
                txt.Multiline = false;

                form.Controls.Add(lbl);
                form.Controls.Add(txt);
                form.Controls.Add(btnOK);
                form.Controls.Add(btnCancel);

                // Resize handling to keep layout neat
                form.Load += (_, __) =>
                {
                    // Ensure buttons stay pinned even on different DPI
                    btnOK.Left = form.ClientSize.Width - 198;
                    btnCancel.Left = form.ClientSize.Width - 102;
                    btnOK.Top = btnCancel.Top = form.ClientSize.Height - 44;

                    txt.Width = form.ClientSize.Width - 24;
                };

                var result = owner is null ? form.ShowDialog() : form.ShowDialog(owner);
                return result == DialogResult.OK ? txt.Text : null;
            }

            /// <summary>
            /// Convenience overload returning (ok, value).
            /// </summary>
            public static (bool ok, string? value) ShowWithOk(
                string title = "Input", string prompt = "Enter value:", string? defaultValue = null,
                IWin32Window? owner = null, bool requireNonEmpty = false)
            {
                var v = Show(title, prompt, defaultValue, owner, requireNonEmpty);
                return (v != null, v);
            }
        }
        public static class InlineComboInput
        {
            public static string? Show(
                string title = "Select",
                string prompt = "Choose value:",
                IEnumerable<string>? items = null,
                string? defaultValue = null,
                IWin32Window? owner = null)
            {
                using var form = new Form
                {
                    Text = string.IsNullOrWhiteSpace(title) ? "Select" : title,
                    FormBorderStyle = FormBorderStyle.FixedDialog,
                    StartPosition = FormStartPosition.CenterParent,
                    MinimizeBox = false,
                    MaximizeBox = false,
                    ShowInTaskbar = false,
                    KeyPreview = true,
                    AutoScaleMode = AutoScaleMode.Dpi,
                    ClientSize = new Size(420, 140)
                };

                var lbl = new Label
                {
                    AutoSize = false,
                    Text = string.IsNullOrWhiteSpace(prompt) ? "Choose value:" : prompt,
                    Location = new Point(12, 12),
                    Size = new Size(form.ClientSize.Width - 24, 36)
                };

                var cmb = new ComboBox
                {
                    Location = new Point(12, 52),
                    Size = new Size(form.ClientSize.Width - 24, 24),
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                    DropDownStyle = ComboBoxStyle.DropDownList
                };

                cmb.Items.Add("--Select--");

                if (items != null)
                    foreach (var i in items)
                        cmb.Items.Add(i);

                if (!string.IsNullOrWhiteSpace(defaultValue) && cmb.Items.Contains(defaultValue))
                    cmb.SelectedItem = defaultValue;
                else
                    cmb.SelectedIndex = 0;

                var btnOK = new Button
                {
                    Text = "OK",
                    DialogResult = DialogResult.OK,
                    Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                    Size = new Size(90, 28),
                    Location = new Point(form.ClientSize.Width - 198, form.ClientSize.Height - 44)
                };

                var btnCancel = new Button
                {
                    Text = "Cancel",
                    DialogResult = DialogResult.Cancel,
                    Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                    Size = new Size(90, 28),
                    Location = new Point(form.ClientSize.Width - 102, form.ClientSize.Height - 44)
                };

                form.AcceptButton = btnOK;
                form.CancelButton = btnCancel;

                btnOK.Click += (_, __) =>
                {
                    if (cmb.SelectedIndex == 0)
                    {
                        MessageBox.Show("Please select a valid option.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        form.DialogResult = DialogResult.None;
                    }
                };

                form.Controls.Add(lbl);
                form.Controls.Add(cmb);
                form.Controls.Add(btnOK);
                form.Controls.Add(btnCancel);

                form.Load += (_, __) =>
                {
                    btnOK.Left = form.ClientSize.Width - 198;
                    btnCancel.Left = form.ClientSize.Width - 102;
                    btnOK.Top = btnCancel.Top = form.ClientSize.Height - 44;
                    cmb.Width = form.ClientSize.Width - 24;
                };

                var result = owner is null ? form.ShowDialog() : form.ShowDialog(owner);
                return result == DialogResult.OK ? cmb.SelectedItem?.ToString() : null;
            }

            public static (bool ok, string? value) ShowWithOk(
                string title = "Select",
                string prompt = "Choose value:",
                IEnumerable<string>? items = null,
                string? defaultValue = null,
                IWin32Window? owner = null)
            {
                var v = Show(title, prompt, items, defaultValue, owner);
                return (v != null, v);
            }
        }


        public static class InlineButtonGrid
        {
            public static string? Show(
                IEnumerable<string> buttonLabels,
                string title = "Select",
                IWin32Window? owner = null,
                int maxColumns = 4)
            {
                if (buttonLabels == null)
                    return null;

                var labels = buttonLabels.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
                if (labels.Count == 0)
                    return null;

                const int buttonWidth = 110;
                const int buttonHeight = 32;
                const int margin = 12;
                const int spacingX = 8;
                const int spacingY = 8;

                int cols = Math.Min(maxColumns <= 0 ? 4 : maxColumns, labels.Count);
                int rows = (int)Math.Ceiling(labels.Count / (double)cols);

                int clientWidth = margin * 2 + cols * buttonWidth + (cols - 1) * spacingX;
                int clientHeight = margin * 2 + rows * buttonHeight + (rows - 1) * spacingY;

                using var form = new Form
                {
                    Text = string.IsNullOrWhiteSpace(title) ? "Select" : title,
                    FormBorderStyle = FormBorderStyle.FixedDialog,
                    StartPosition = FormStartPosition.CenterParent,
                    MinimizeBox = false,
                    MaximizeBox = false,
                    ShowInTaskbar = false,
                    KeyPreview = true,
                    AutoScaleMode = AutoScaleMode.Dpi,
                    ClientSize = new Size(clientWidth, clientHeight)
                };

                string? selected = null;

                for (int i = 0; i < labels.Count; i++)
                {
                    int row = i / cols;
                    int col = i % cols;

                    var btn = new Button
                    {
                        Text = labels[i],
                        Size = new Size(buttonWidth, buttonHeight),
                        Location = new Point(
                            margin + col * (buttonWidth + spacingX),
                            margin + row * (buttonHeight + spacingY)),
                        Anchor = AnchorStyles.Top | AnchorStyles.Left
                    };

                    btn.Click += (_, __) =>
                    {
                        selected = btn.Text;
                        form.DialogResult = DialogResult.OK;
                        form.Close();
                    };

                    form.Controls.Add(btn);
                }

                form.KeyDown += (_, e) =>
                {
                    if (e.KeyCode == Keys.Escape)
                    {
                        selected = null;
                        form.DialogResult = DialogResult.Cancel;
                        form.Close();
                    }
                };

                var result = owner is null ? form.ShowDialog() : form.ShowDialog(owner);
                return result == DialogResult.OK ? selected : null;
            }

            public static (bool ok, string? value) ShowWithOk(
                IEnumerable<string> buttonLabels,
                string title = "Select",
                IWin32Window? owner = null,
                int maxColumns = 4)
            {
                var v = Show(buttonLabels, title, owner, maxColumns);
                return (v != null, v);
            }
        }

    }
}
