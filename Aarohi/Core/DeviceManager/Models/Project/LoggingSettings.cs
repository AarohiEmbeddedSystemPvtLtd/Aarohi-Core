namespace Aarohi.Core.DeviceManager.Models
{
    public class LoggingSettings
    {
        public bool EnableLogging { get; set; } = true;

        public bool EnableDataLogging { get; set; } = true;

        public bool LogCommunicationFrames { get; set; } = false;

        public bool LogErrorsOnly { get; set; } = true;

        public bool AutoCreateDailyFolder { get; set; } = true;

        public int DataStoreIntervalMs { get; set; } = 1000;

        public int CommunicationLogFlushIntervalMs { get; set; } = 1000;

        public string LogFolderPath { get; set; } = string.Empty;

        public string DataFilePrefix { get; set; } = "DataLog";

        public string CommunicationFilePrefix { get; set; } = "CommLog";

        public int MaxLogFileSizeMb { get; set; } = 50;
    }
}
