using Aarohi.Classes.Healper;
using Aarohi.ExtendedUI;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Aarohi.Configuration.DeveloperSettings
{
    /// <summary>
    /// Thread-safe registry storage provider for developer settings.
    /// Settings are stored under Software\Aarohi Embedded Systems Pvt Ltd\{AppName}\DeveloperSettingsManager.
    /// Each setting is stored as a string value where ValueName is the VariableName and ValueData is JSON.
    /// </summary>
    public class DeveloperSettingsRegistryService : ISettingsStorageProvider
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        private static readonly Regex VariableNameRegex = new Regex(
            @"^[A-Za-z_][A-Za-z0-9_]*$",
            RegexOptions.Compiled);

        private static readonly object RegistryLock = new object();

        /// <summary>
        /// Gets the dynamic registry path for the current application.
        /// </summary>
        public string RegistryPath => RegistryHelper.GetRegistryPath(RegistryHelper.storeLocs.DeveloperSettings);

        /// <summary>
        /// When true, displays user-facing ExtendedMessageBox on validation or IO errors.
        /// Defaults to false for headless / background queries.
        /// </summary>
        public bool ShowDialogs { get; set; } = false;

        /// <summary>
        /// Last error message encountered during storage operations.
        /// </summary>
        public string? LastError { get; private set; }

        public DeveloperSettingsRegistryService(bool showDialogs = false)
        {
            ShowDialogs = showDialogs;
        }

        #region Read All Settings

        public List<DeveloperSetting> GetAllSettings()
        {
            var settings = new List<DeveloperSetting>();

            lock (RegistryLock)
            {
                try
                {
                    using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(RegistryPath, false))
                    {
                        if (key == null)
                            return settings;

                        foreach (string name in key.GetValueNames())
                        {
                            try
                            {
                                if (key.GetValueKind(name) != RegistryValueKind.String)
                                    continue;

                                string? json = key.GetValue(name) as string;

                                if (TryDeserializeSetting(json, name, out DeveloperSetting? setting) && setting != null)
                                {
                                    settings.Add(setting);
                                }
                            }
                            catch (Exception ex) when (IsRegistryException(ex))
                            {
                                System.Diagnostics.Debug.WriteLine($"Developer Setting '{name}': {ex.Message}");
                            }
                        }
                    }
                }
                catch (Exception ex) when (IsRegistryException(ex))
                {
                    HandleError("Unable to load Developer Settings.", ex);
                    return new List<DeveloperSetting>();
                }
            }

            return settings
                .OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.VariableName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        #endregion

        #region Read Single Setting

        public DeveloperSetting? GetSetting(string variableName)
        {
            if (string.IsNullOrWhiteSpace(variableName))
                return null;

            lock (RegistryLock)
            {
                try
                {
                    using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(RegistryPath, false))
                    {
                        if (key == null)
                            return null;

                        string? existingName = FindSettingName(key, variableName);
                        if (existingName == null)
                            return null;

                        if (key.GetValueKind(existingName) != RegistryValueKind.String)
                            return null;

                        string? json = key.GetValue(existingName) as string;
                        if (TryDeserializeSetting(json, existingName, out DeveloperSetting? setting))
                        {
                            return setting;
                        }

                        return null;
                    }
                }
                catch (Exception ex) when (IsRegistryException(ex))
                {
                    HandleError("Unable to read Developer Setting.", ex);
                    return null;
                }
            }
        }

        #endregion

        #region Check Setting Exists

        public bool SettingExists(string variableName)
        {
            if (string.IsNullOrWhiteSpace(variableName))
                return false;

            lock (RegistryLock)
            {
                try
                {
                    using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(RegistryPath, false))
                    {
                        if (key == null)
                            return false;

                        return FindSettingName(key, variableName) != null;
                    }
                }
                catch (Exception ex) when (IsRegistryException(ex))
                {
                    HandleError("Unable to check Developer Setting.", ex);
                    return false;
                }
            }
        }

        #endregion

        #region Add Setting

        public bool AddSetting(DeveloperSetting setting)
        {
            if (!ValidateSetting(setting))
                return false;

            lock (RegistryLock)
            {
                try
                {
                    using (RegistryKey? key = Registry.CurrentUser.CreateSubKey(RegistryPath))
                    {
                        if (key == null)
                        {
                            HandleError("Unable to create Registry path.");
                            return false;
                        }

                        string? existingName = FindSettingName(key, setting.VariableName);
                        if (existingName != null)
                        {
                            HandleWarning($"A setting with Variable Name '{setting.VariableName}' already exists.");
                            return false;
                        }

                        string json = SerializeSetting(setting);
                        key.SetValue(setting.VariableName, json, RegistryValueKind.String);
                        return true;
                    }
                }
                catch (Exception ex) when (IsRegistryException(ex))
                {
                    HandleError("Unable to add Developer Setting.", ex);
                    return false;
                }
            }
        }

        #endregion

        #region Update Setting

        public bool UpdateSetting(DeveloperSetting setting)
        {
            if (!ValidateSetting(setting))
                return false;

            lock (RegistryLock)
            {
                try
                {
                    using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(RegistryPath, true))
                    {
                        if (key == null)
                        {
                            HandleWarning("Developer Settings Registry path does not exist.");
                            return false;
                        }

                        string? existingName = FindSettingName(key, setting.VariableName);
                        if (existingName == null)
                        {
                            HandleWarning("The selected setting no longer exists.");
                            return false;
                        }

                        if (key.GetValueKind(existingName) != RegistryValueKind.String)
                        {
                            HandleWarning("The selected Registry entry is not a string.");
                            return false;
                        }

                        string? oldJson = key.GetValue(existingName) as string;
                        if (!TryDeserializeSetting(oldJson, existingName, out _))
                        {
                            HandleWarning("The selected Registry entry is not a valid Developer Setting.");
                            return false;
                        }

                        key.SetValue(existingName, SerializeSetting(setting), RegistryValueKind.String);
                        return true;
                    }
                }
                catch (Exception ex) when (IsRegistryException(ex))
                {
                    HandleError("Unable to update Developer Setting.", ex);
                    return false;
                }
            }
        }

        #endregion

        #region Delete Setting

        public bool DeleteSetting(string variableName)
        {
            if (string.IsNullOrWhiteSpace(variableName))
            {
                HandleWarning("Variable Name is required.");
                return false;
            }

            lock (RegistryLock)
            {
                try
                {
                    using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(RegistryPath, true))
                    {
                        if (key == null)
                        {
                            HandleWarning("Developer Settings Registry path does not exist.");
                            return false;
                        }

                        string? existingName = FindSettingName(key, variableName);
                        if (existingName == null)
                        {
                            HandleWarning("The selected setting no longer exists.");
                            return false;
                        }

                        if (key.GetValueKind(existingName) != RegistryValueKind.String)
                        {
                            HandleWarning("Cannot delete this Registry entry because it is not a string.");
                            return false;
                        }

                        string? json = key.GetValue(existingName) as string;
                        if (!TryDeserializeSetting(json, existingName, out _))
                        {
                            HandleWarning("Cannot delete an invalid or unrelated Registry entry.");
                            return false;
                        }

                        key.DeleteValue(existingName, false);
                        return true;
                    }
                }
                catch (Exception ex) when (IsRegistryException(ex))
                {
                    HandleError("Unable to delete Developer Setting.", ex);
                    return false;
                }
            }
        }

        #endregion

        #region Validation

        public bool ValidateSetting(DeveloperSetting setting)
        {
            if (setting == null)
            {
                HandleWarning("Setting information is missing.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(setting.VariableName))
            {
                HandleWarning("Variable Name is required.");
                return false;
            }

            if (!VariableNameRegex.IsMatch(setting.VariableName))
            {
                HandleWarning("Variable Name must start with a letter or underscore and contain only letters, numbers or underscores.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(setting.DisplayName))
            {
                HandleWarning("Display Name is required.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(setting.Value))
            {
                HandleWarning("Value is required.");
                return false;
            }

            return true;
        }

        #endregion

        #region JSON Serialization

        public static string SerializeSetting(DeveloperSetting setting)
        {
            return JsonSerializer.Serialize(new
            {
                DisplayName = setting.DisplayName,
                Value = setting.Value
            });
        }

        public static bool TryDeserializeSetting(string? json, string variableName, out DeveloperSetting? setting)
        {
            setting = null;

            if (string.IsNullOrWhiteSpace(json))
                return false;

            try
            {
                using (JsonDocument document = JsonDocument.Parse(json))
                {
                    if (document.RootElement.ValueKind != JsonValueKind.Object)
                        return false;

                    JsonElement root = document.RootElement;
                    var properties = root.EnumerateObject().ToList();

                    int displayNameCount = properties.Count(p => string.Equals(p.Name, "DisplayName", StringComparison.OrdinalIgnoreCase));
                    int valueCount = properties.Count(p => string.Equals(p.Name, "Value", StringComparison.OrdinalIgnoreCase));

                    if (displayNameCount != 1 || valueCount != 1)
                        return false;

                    JsonElement displayNameElement = properties.First(p => string.Equals(p.Name, "DisplayName", StringComparison.OrdinalIgnoreCase)).Value;
                    JsonElement valueElement = properties.First(p => string.Equals(p.Name, "Value", StringComparison.OrdinalIgnoreCase)).Value;

                    if (displayNameElement.ValueKind != JsonValueKind.String || valueElement.ValueKind != JsonValueKind.String)
                        return false;

                    string? displayName = displayNameElement.GetString();
                    string? value = valueElement.GetString();

                    if (string.IsNullOrWhiteSpace(displayName) || value == null)
                        return false;

                    setting = new DeveloperSetting
                    {
                        VariableName = variableName,
                        DisplayName = displayName,
                        Value = value
                    };

                    return true;
                }
            }
            catch (JsonException)
            {
                return false;
            }
        }

        #endregion

        #region Registry Helpers

        private static string? FindSettingName(RegistryKey key, string variableName)
        {
            if (key == null || string.IsNullOrWhiteSpace(variableName))
                return null;

            return key.GetValueNames().FirstOrDefault(name =>
                string.Equals(name, variableName, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsRegistryException(Exception ex)
        {
            return ex is IOException ||
                   ex is UnauthorizedAccessException ||
                   ex is SecurityException ||
                   ex is ArgumentException ||
                   ex is InvalidOperationException ||
                   ex is ObjectDisposedException ||
                   ex is JsonException;
        }

        private void HandleWarning(string message)
        {
            LastError = message;
            if (ShowDialogs)
            {
                ExtendedMessageBox.Show(
                    message,
                    "Developer Settings",
                    MessageType.Warning,
                    MessageButtons.OK);
            }
        }

        private void HandleError(string message, Exception? ex = null)
        {
            string details = ex?.Message ?? string.Empty;
            LastError = string.IsNullOrEmpty(details) ? message : $"{message} {details}";

            if (ShowDialogs)
            {
                string text = string.IsNullOrEmpty(details) ? message : $"{message}\n{details}";
                ExtendedMessageBox.Show(
                    text,
                    "Developer Settings Error",
                    MessageType.Error,
                    MessageButtons.OK);
            }
        }

        #endregion
    }
}
