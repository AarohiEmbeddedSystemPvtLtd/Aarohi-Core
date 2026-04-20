using System;

namespace Aarohi.Core.DeviceManager.Models
{
    public abstract class FileModelBase
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// User-friendly name of the file object.
        /// Example: Aarohi Flow Meter, Down Stream Flow Meter, Plant-01
        /// </summary>
        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string Version { get; set; } = "1.0";

        public bool IsEnabled { get; set; } = true;

        public string CreatedBy { get; set; } = string.Empty;

        public DateTime CreatedOn { get; set; } = DateTime.Now;

        public string ModifiedBy { get; set; } = string.Empty;

        public DateTime? ModifiedOn { get; set; }

        public string Notes { get; set; } = string.Empty;
    }
}
