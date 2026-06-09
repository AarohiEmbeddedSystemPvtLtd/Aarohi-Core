using System;

namespace Aarohi.Core.DeviceManager.Models
{
    /// <summary>
    /// Runtime value read from a device register/tag.
    /// This is usually not a profile file model, but useful in communication engine/runtime layer.
    /// </summary>
    public class DeviceRegisterValue
    {
        public Guid DeviceId { get; set; }

        public string DeviceName { get; set; } = string.Empty;

        public string DeviceTagName { get; set; } = string.Empty;

        public Guid? ProfileId { get; set; }

        public Guid? RegisterId { get; set; }

        public string RegisterName { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public RegisterArea RegisterArea { get; set; } = RegisterArea.HoldingRegister;

        public int Address { get; set; }

        public object? RawValue { get; set; }

        public object? ParsedValue { get; set; }

        public double? NumericValue { get; set; }

        public string Unit { get; set; } = string.Empty;

        public string Quality { get; set; } = "Unknown";

        public DateTime TimeStamp { get; set; } = DateTime.Now;

        public bool IsCommunicationOk { get; set; }

        public string ErrorMessage { get; set; } = string.Empty;
    }
}
