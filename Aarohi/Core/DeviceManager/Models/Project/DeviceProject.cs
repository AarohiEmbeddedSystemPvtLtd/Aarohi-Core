using System.Collections.Generic;

namespace Aarohi.Core.DeviceManager.Models
{
    /// <summary>
    /// Main project file containing full setup.
    /// </summary>
    public class DeviceProject : FileModelBase
    {
        public string ProjectCode { get; set; } = string.Empty;

        public string CustomerName { get; set; } = string.Empty;

        public string SiteName { get; set; } = string.Empty;

        public string SiteAddress { get; set; } = string.Empty;

        public string PlantName { get; set; } = string.Empty;

        public string ProjectPath { get; set; } = string.Empty;

        public List<string> ProfileFiles { get; set; } = new();

        public List<string> DeviceFiles { get; set; } = new();

        public List<DeviceProfile> Profiles { get; set; } = new();

        public List<DeviceInstance> Devices { get; set; } = new();

        public List<CommunicationBus> Buses { get; set; } = new();

        public LoggingSettings LoggingSettings { get; set; } = new();

        public string UniqueKey =>
            string.IsNullOrWhiteSpace(ProjectCode) ? Name : ProjectCode;
    }
}
