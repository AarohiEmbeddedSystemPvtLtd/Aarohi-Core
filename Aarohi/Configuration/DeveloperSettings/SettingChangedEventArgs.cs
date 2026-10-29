using System;

namespace Aarohi.Configuration.DeveloperSettings
{
    /// <summary>
    /// Event data for live developer setting change notifications.
    /// </summary>
    public class SettingChangedEventArgs : EventArgs
    {
        public string VariableName { get; }
        public string? OldValue { get; }
        public string NewValue { get; }

        public SettingChangedEventArgs(string variableName, string? oldValue, string newValue)
        {
            VariableName = variableName;
            OldValue = oldValue;
            NewValue = newValue;
        }
    }
}
