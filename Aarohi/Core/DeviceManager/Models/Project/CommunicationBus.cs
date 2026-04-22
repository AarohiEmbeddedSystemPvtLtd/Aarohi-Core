using System;
using System.Collections.Generic;

namespace Aarohi.Core.DeviceManager.Models
{
    /// <summary>
    /// Logical communication grouping.
    /// Useful especially for one RS485 bus having multiple devices.
    /// </summary>
    public class CommunicationBus
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string BusName { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public CommunicationMode Mode { get; set; } = CommunicationMode.ModbusRs485;

        /// <summary>
        /// Common/shared communication settings for this bus.
        /// Device-level settings may still override where required.
        /// </summary>
        public CommunicationSettings Communication { get; set; } = new();

        public bool IsEnabled { get; set; } = true;

        public string Remarks { get; set; } = string.Empty;

        /// <summary>
        /// Device IDs attached to this bus.
        /// </summary>
        public List<Guid> DeviceIds { get; set; } = new();

        /// <summary>
        /// Optional device file references attached to this bus.
        /// </summary>
        public List<string> DeviceFileNames { get; set; } = new();
    }
}
