using System;

namespace Aarohi.Configuration.DeveloperSettings
{
    /// <summary>
    /// Represents a single configurable developer setting.
    /// </summary>
    public class DeveloperSetting
    {
        /// <summary>
        /// Unique variable identifier used in application code (e.g. "GraphSmoothingFactor").
        /// </summary>
        public string VariableName { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable display label shown in the settings window.
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Actual string-serialized configuration value.
        /// </summary>
        public string Value { get; set; } = string.Empty;
    }
}
