using System;
using System.Collections.Generic;

namespace Aarohi.Core.DeviceManager.Models
{
    /// <summary>
    /// Actual installed device in plant/field.
    /// Example: Down Stream Flow Meter, Up Stream Flow Meter.
    /// </summary>
    public class DeviceInstance : FileModelBase
    {
        public string TagName { get; set; } = string.Empty;

        public string Location { get; set; } = string.Empty;

        public string AreaName { get; set; } = string.Empty;

        public string PanelName { get; set; } = string.Empty;

        public string LineName { get; set; } = string.Empty;

        /// <summary>
        /// Profile reference if profile objects are loaded in memory.
        /// </summary>
        public Guid? ProfileId { get; set; }

        /// <summary>
        /// Optional file reference to the saved profile.
        /// Example: Profiles/Aarohi Flow Meter.adpf
        /// </summary>
        public string ProfileFileName { get; set; } = string.Empty;

        public string ProfileName { get; set; } = string.Empty;

        public string AssetCode { get; set; } = string.Empty;

        /// <summary>
        /// Actual communication used by this installed device.
        /// Can override profile defaults.
        /// </summary>
        public CommunicationSettings Communication { get; set; } = new();

        /// <summary>
        /// Optional override values for specific register settings.
        /// Key example: RegisterDefinition.UniqueKey
        /// </summary>
        public Dictionary<string, string> RegisterOverrides { get; set; } = new();

        public bool IsOnline { get; set; }

        public DateTime? LastSeenOn { get; set; }

        public DateTime? LastSuccessfulCommunication { get; set; }

        public string InstallationRemarks { get; set; } = string.Empty;

        public string UniqueKey =>
            $"{TagName}_{Communication?.Mode}_{Communication?.Serial?.PortName}_{Communication?.Modbus?.SlaveId}";
    }
}
