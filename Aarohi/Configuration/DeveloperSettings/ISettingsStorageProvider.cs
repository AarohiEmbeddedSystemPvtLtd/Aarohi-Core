using System.Collections.Generic;

namespace Aarohi.Configuration.DeveloperSettings
{
    /// <summary>
    /// Storage contract for developer settings persistence (Registry, JSON file, SQLite, etc.).
    /// </summary>
    public interface ISettingsStorageProvider
    {
        /// <summary>
        /// Reads all stored developer settings.
        /// </summary>
        List<DeveloperSetting> GetAllSettings();

        /// <summary>
        /// Reads a single developer setting by its variable name, or null if not found.
        /// </summary>
        DeveloperSetting? GetSetting(string variableName);

        /// <summary>
        /// Checks whether a setting with the specified variable name exists.
        /// </summary>
        bool SettingExists(string variableName);

        /// <summary>
        /// Adds a new developer setting to persistent storage.
        /// </summary>
        bool AddSetting(DeveloperSetting setting);

        /// <summary>
        /// Updates an existing developer setting in persistent storage.
        /// </summary>
        bool UpdateSetting(DeveloperSetting setting);

        /// <summary>
        /// Deletes a developer setting from persistent storage.
        /// </summary>
        bool DeleteSetting(string variableName);
    }
}
