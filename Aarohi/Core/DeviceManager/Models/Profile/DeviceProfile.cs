using System;
using System.Collections.Generic;

namespace Aarohi.Core.DeviceManager.Models
{
    /// <summary>
    /// Reusable device definition.
    /// Example: Aarohi Flow Meter profile.
    /// Same profile can be used by many field devices.
    /// </summary>
    public class DeviceProfile : FileModelBase
    {
        public string ProfileCode { get; set; } = string.Empty;

        public string Manufacturer { get; set; } = string.Empty;

        public string ModelNumber { get; set; } = string.Empty;

        public DeviceType DeviceType { get; set; } = DeviceType.Unknown;

        public DeviceProtocol Protocol { get; set; } = DeviceProtocol.Unknown;

        /// <summary>
        /// Default communication expected by this device type.
        /// Instance-level communication can override this.
        /// </summary>
        public CommunicationSettings DefaultCommunication { get; set; } = new();

        /// <summary>
        /// Register map for Modbus-capable devices.
        /// </summary>
        public List<RegisterDefinition> Registers { get; set; } = new();

        public string FirmwareVersion { get; set; } = string.Empty;

        public string HardwareVersion { get; set; } = string.Empty;

        public bool SupportsRegisterRead { get; set; } = true;

        public bool SupportsRegisterWrite { get; set; } = true;

        public string Category { get; set; } = string.Empty;

        public string UniqueKey =>
            string.IsNullOrWhiteSpace(ProfileCode)
                ? $"{Manufacturer}_{ModelNumber}_{Name}"
                : ProfileCode;
    }
}
