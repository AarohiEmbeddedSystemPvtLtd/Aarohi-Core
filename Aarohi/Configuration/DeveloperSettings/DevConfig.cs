using Aarohi.Globals;
using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Windows.Forms;

namespace Aarohi.Configuration.DeveloperSettings
{
    /// <summary>
    /// Static facade and central runtime engine for developer settings.
    /// Provides zero-allocation in-memory cached reads, automatic default-value self-seeding,
    /// dynamic security gates, and live cross-screen change notifications.
    /// </summary>
    public static class DevConfig
    {
        private static readonly ConcurrentDictionary<string, string> _cache =
            new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private static ISettingsStorageProvider _storage = new DeveloperSettingsRegistryService();
        private static bool _filterRegistered = false;
        private static readonly object _initLock = new object();

        /// <summary>
        /// Event fired whenever any developer setting is added, modified, or deleted at runtime.
        /// Consuming screens can subscribe to this event to perform instant live re-renders.
        /// </summary>
        public static event EventHandler<SettingChangedEventArgs>? SettingChanged;

        /// <summary>
        /// Delegate providing the currently logged-in username in the consuming host application.
        /// </summary>
        public static Func<string?>? CurrentUserProvider { get; set; }

        /// <summary>
        /// Delegate providing whether the current user has Administrator or Developer Access in the consuming host application.
        /// </summary>
        public static Func<bool>? HasAdminOrDevAccessProvider { get; set; }

        /// <summary>
        /// Action invoked to display or bring to front the Developer Settings Manager window.
        /// Configured by Aarohi.Wpf.Controls or consuming applications.
        /// </summary>
        public static Action? OpenSettingsWindowAction { get; set; }

        /// <summary>
        /// Configures and initializes the Developer Settings engine.
        /// </summary>
        /// <param name="enableGlobalShortcut">If true, registers the global Ctrl+Shift+D keyboard filter.</param>
        /// <param name="storageProvider">Optional custom storage provider (defaults to Registry).</param>
        public static void Initialize(bool enableGlobalShortcut = true, ISettingsStorageProvider? storageProvider = null)
        {
            lock (_initLock)
            {
                if (storageProvider != null)
                {
                    _storage = storageProvider;
                }

                RefreshCache();

                if (enableGlobalShortcut && !_filterRegistered)
                {
                    Application.AddMessageFilter(new GlobalDevShortcutFilter());
                    _filterRegistered = true;
                }
            }
        }

        /// <summary>
        /// Reloads the in-memory cache directly from persistent storage.
        /// </summary>
        public static void RefreshCache()
        {
            try
            {
                var settings = _storage.GetAllSettings();
                _cache.Clear();
                foreach (var s in settings)
                {
                    if (!string.IsNullOrWhiteSpace(s.VariableName))
                    {
                        _cache[s.VariableName] = s.Value;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DevConfig.RefreshCache] Error: {ex.Message}");
            }
        }

        #region Security & Access Hierarchy

        /// <summary>
        /// True only if the currently logged-in user is 'Dev@Aarohi' (AGLobals.Utils.DevName).
        /// </summary>
        public static bool IsDevAarohi
        {
            get
            {
                try
                {
                    string? user = CurrentUserProvider?.Invoke();
                    return string.Equals(user, AGLobals.Utils.DevName, StringComparison.OrdinalIgnoreCase);
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Security Gate Tier 1: Only 'Dev@Aarohi' can use the global Ctrl+Shift+D shortcut.
        /// </summary>
        public static bool CanUseDevShortcut => IsDevAarohi;

        /// <summary>
        /// Security Gate Tier 2: Can access Developer Settings Manager from Settings menu.
        /// Accessible by 'Dev@Aarohi' or users with Admin/DevAccess permissions.
        /// Regular operators are blocked.
        /// </summary>
        public static bool CanAccessSettingsUI
        {
            get
            {
                try
                {
                    if (IsDevAarohi)
                        return true;

                    return HasAdminOrDevAccessProvider?.Invoke() ?? false;
                }
                catch
                {
                    return false;
                }
            }
        }

        #endregion

        #region UI Invocation

        /// <summary>
        /// Opens or brings to focus the Developer Settings Manager window.
        /// </summary>
        public static void OpenOrActivateSettingsWindow()
        {
            try
            {
                OpenSettingsWindowAction?.Invoke();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DevConfig.OpenOrActivateSettingsWindow] Error: {ex.Message}");
            }
        }

        #endregion

        #region Typed Getters (Fail-Safe & Auto-Seeding)

        /// <summary>
        /// Retrieves a string configuration value. If not found in storage, auto-seeds with defaultValue.
        /// </summary>
        public static string GetString(string variableName, string defaultValue = "", string? displayName = null)
        {
            if (string.IsNullOrWhiteSpace(variableName))
                return defaultValue;

            if (_cache.TryGetValue(variableName, out string? cached) && cached != null)
                return cached;

            return ResolveAndSeed(variableName, defaultValue, displayName);
        }

        /// <summary>
        /// Retrieves a double configuration value. If not found in storage, auto-seeds with defaultValue.
        /// </summary>
        public static double GetDouble(string variableName, double defaultValue = 0.0, string? displayName = null)
        {
            string raw = GetString(variableName, defaultValue.ToString(CultureInfo.InvariantCulture), displayName);
            if (double.TryParse(raw, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out double parsed))
            {
                return parsed;
            }
            return defaultValue;
        }

        /// <summary>
        /// Retrieves an integer configuration value. If not found in storage, auto-seeds with defaultValue.
        /// </summary>
        public static int GetInt(string variableName, int defaultValue = 0, string? displayName = null)
        {
            string raw = GetString(variableName, defaultValue.ToString(CultureInfo.InvariantCulture), displayName);
            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            {
                return parsed;
            }
            return defaultValue;
        }

        /// <summary>
        /// Retrieves a boolean configuration value. If not found in storage, auto-seeds with defaultValue.
        /// </summary>
        public static bool GetBool(string variableName, bool defaultValue = false, string? displayName = null)
        {
            string raw = GetString(variableName, defaultValue ? "true" : "false", displayName);
            if (bool.TryParse(raw, out bool parsed))
            {
                return parsed;
            }
            if (raw == "1") return true;
            if (raw == "0") return false;
            return defaultValue;
        }

        #endregion

        #region Setters & Live Event Notifications

        /// <summary>
        /// Updates or creates a setting, persists it to storage, updates cache, and raises SettingChanged.
        /// </summary>
        public static bool SetValue(string variableName, string value, string? displayName = null)
        {
            if (string.IsNullOrWhiteSpace(variableName))
                return false;

            try
            {
                _cache.TryGetValue(variableName, out string? oldValue);

                var setting = new DeveloperSetting
                {
                    VariableName = variableName,
                    DisplayName = displayName ?? variableName,
                    Value = value
                };

                bool saved;
                if (_storage.SettingExists(variableName))
                {
                    saved = _storage.UpdateSetting(setting);
                }
                else
                {
                    saved = _storage.AddSetting(setting);
                }

                if (saved)
                {
                    _cache[variableName] = value;
                    NotifySettingChanged(variableName, oldValue, value);
                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DevConfig.SetValue] Error: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// Notifies the system that a setting was updated (e.g. from the UI editor), updating cache and triggering SettingChanged.
        /// </summary>
        public static void NotifySettingChanged(string variableName, string? oldValue, string newValue)
        {
            if (string.IsNullOrWhiteSpace(variableName))
                return;

            _cache[variableName] = newValue;

            try
            {
                SettingChanged?.Invoke(null, new SettingChangedEventArgs(variableName, oldValue, newValue));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DevConfig.NotifySettingChanged] Subscriber exception: {ex.Message}");
            }
        }

        /// <summary>
        /// Notifies the system that a setting was deleted, updating cache and triggering SettingChanged with empty string.
        /// </summary>
        public static void NotifySettingDeleted(string variableName)
        {
            if (string.IsNullOrWhiteSpace(variableName))
                return;

            _cache.TryRemove(variableName, out string? oldValue);

            try
            {
                SettingChanged?.Invoke(null, new SettingChangedEventArgs(variableName, oldValue, string.Empty));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DevConfig.NotifySettingDeleted] Subscriber exception: {ex.Message}");
            }
        }

        #endregion

        #region Private Helpers

        private static string ResolveAndSeed(string variableName, string defaultValue, string? displayName)
        {
            try
            {
                var existing = _storage.GetSetting(variableName);
                if (existing != null)
                {
                    _cache[variableName] = existing.Value;
                    return existing.Value;
                }

                // First time encounter: seed into persistent storage silently
                var newSetting = new DeveloperSetting
                {
                    VariableName = variableName,
                    DisplayName = string.IsNullOrWhiteSpace(displayName) ? variableName : displayName,
                    Value = defaultValue
                };

                _storage.AddSetting(newSetting);
                _cache[variableName] = defaultValue;
                return defaultValue;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DevConfig.ResolveAndSeed] Error: {ex.Message}");
                _cache[variableName] = defaultValue;
                return defaultValue;
            }
        }

        #endregion
    }
}
