using Aarohi.Configuration.DeveloperSettings;
using Aarohi.ExtendedUI;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace AarohiWpfControls.Controls.DeveloperSettings
{
    public partial class DeveloperSettingsWindow : Window
    {
        private static DeveloperSettingsWindow? _activeInstance;
        private readonly DeveloperSettingsRegistryService _registryService;

        static DeveloperSettingsWindow()
        {
            // Automatically wire the static facade action
            DevConfig.OpenSettingsWindowAction = () => OpenOrActivate();
        }

        public DeveloperSettingsWindow()
        {
            InitializeComponent();
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            _registryService = new DeveloperSettingsRegistryService(showDialogs: false);

            Loaded += DeveloperSettingsWindow_Loaded;
        }

        /// <summary>
        /// Opens or brings to front an existing DeveloperSettingsWindow instance.
        /// Can seamlessly attach to either a WPF Window owner or a WinForms Form handle.
        /// </summary>
        public static void OpenOrActivate(Window? owner = null, IntPtr? parentHwnd = null)
        {
            // Ensure running on WPF Dispatcher if called across threads
            if (Application.Current != null && !Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(() => OpenOrActivate(owner, parentHwnd));
                return;
            }

            if (_activeInstance != null && _activeInstance.IsLoaded)
            {
                if (_activeInstance.WindowState == WindowState.Minimized)
                {
                    _activeInstance.WindowState = WindowState.Normal;
                }
                _activeInstance.Activate();
                _activeInstance.Focus();
                return;
            }

            var win = new DeveloperSettingsWindow();

            if (owner != null)
            {
                win.Owner = owner;
            }
            else
            {
                IntPtr hwnd = parentHwnd ?? (System.Windows.Forms.Form.ActiveForm?.Handle ?? IntPtr.Zero);
                if (hwnd != IntPtr.Zero)
                {
                    new WindowInteropHelper(win).Owner = hwnd;
                }
            }

            _activeInstance = win;
            win.Closed += (s, e) =>
            {
                if (_activeInstance == win)
                {
                    _activeInstance = null;
                }
            };

            win.Show();
            win.Activate();
            win.Focus();
        }

        private void DeveloperSettingsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSettings();
        }

        public void LoadSettings()
        {
            try
            {
                SettingsDataGrid.ItemsSource = _registryService.GetAllSettings();
            }
            catch (Exception ex)
            {
                ExtendedMessageBox.Show(
                    "Unable to load developer settings.\n" + ex.Message,
                    "Registry Error",
                    MessageType.Error,
                    MessageButtons.OK);
            }
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new AddEditDevSettingWindow(_registryService)
            {
                Owner = this
            };

            if (dialog.ShowDialog() == true)
            {
                LoadSettings();
            }
        }

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var selectedSetting = button?.DataContext as DeveloperSetting;

            if (selectedSetting == null)
                return;

            var dialog = new AddEditDevSettingWindow(_registryService, selectedSetting)
            {
                Owner = this
            };

            if (dialog.ShowDialog() == true)
            {
                LoadSettings();
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var selectedSetting = button?.DataContext as DeveloperSetting;

            if (selectedSetting == null)
                return;

            var result = ExtendedMessageBox.Show(
                $"Are you sure you want to delete '{selectedSetting.DisplayName}' ({selectedSetting.VariableName})?",
                "Confirm Delete",
                MessageType.Warning,
                MessageButtons.YesNo);

            if (result == System.Windows.Forms.DialogResult.No)
                return;

            try
            {
                bool isDeleted = _registryService.DeleteSetting(selectedSetting.VariableName);

                if (!isDeleted)
                {
                    ExtendedMessageBox.Show(
                        _registryService.LastError ?? "Failed to delete setting.",
                        "Delete Error",
                        MessageType.Error,
                        MessageButtons.OK);
                    return;
                }

                // Notify DevConfig to update cache and fire SettingChanged
                DevConfig.NotifySettingDeleted(selectedSetting.VariableName);

                LoadSettings();

                ExtendedMessageBox.Show(
                    "Setting deleted successfully.",
                    "Success",
                    MessageType.Information,
                    MessageButtons.OK);
            }
            catch (Exception ex)
            {
                ExtendedMessageBox.Show(
                    "Unable to delete setting.\n" + ex.Message,
                    "Registry Error",
                    MessageType.Error,
                    MessageButtons.OK);
            }
        }
    }
}
