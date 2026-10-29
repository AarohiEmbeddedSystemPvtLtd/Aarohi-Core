using System;
using System.Windows.Forms;

namespace Aarohi.Configuration.DeveloperSettings
{
    /// <summary>
    /// Application-wide message filter that intercepts WM_KEYDOWN to detect CTRL + SHIFT + D.
    /// Operates transparently across all forms, user controls, and modal dialogs (Form.ShowDialog).
    /// Only triggers if the current logged-in user satisfies DevConfig.CanUseDevShortcut (strictly 'Dev@Aarohi').
    /// </summary>
    public class GlobalDevShortcutFilter : IMessageFilter
    {
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;

        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg == WM_KEYDOWN || m.Msg == WM_SYSKEYDOWN)
            {
                Keys keyCode = (Keys)(int)m.WParam & Keys.KeyCode;
                if (keyCode == Keys.D)
                {
                    bool ctrl = (Control.ModifierKeys & Keys.Control) == Keys.Control;
                    bool shift = (Control.ModifierKeys & Keys.Shift) == Keys.Shift;

                    if (ctrl && shift)
                    {
                        if (DevConfig.CanUseDevShortcut)
                        {
                            DevConfig.OpenOrActivateSettingsWindow();
                            return true; // Handled, suppress beep or further control handling
                        }
                    }
                }
            }
            return false;
        }
    }
}
