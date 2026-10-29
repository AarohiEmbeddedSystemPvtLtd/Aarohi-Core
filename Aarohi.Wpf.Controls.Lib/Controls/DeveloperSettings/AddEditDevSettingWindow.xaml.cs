using Aarohi.Configuration.DeveloperSettings;
using Aarohi.ExtendedUI;
using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AarohiWpfControls.Controls.DeveloperSettings
{
    public partial class AddEditDevSettingWindow : Window
    {
        private readonly DeveloperSettingsRegistryService _registryService;
        private readonly DeveloperSetting? _existingSetting;
        private readonly bool _isEditMode;

        public AddEditDevSettingWindow(
            DeveloperSettingsRegistryService? registryService = null,
            DeveloperSetting? existingSetting = null)
        {
            InitializeComponent();

            _registryService = registryService ?? new DeveloperSettingsRegistryService(showDialogs: false);
            _existingSetting = existingSetting;
            _isEditMode = existingSetting != null;

            if (_isEditMode && existingSetting != null)
            {
                Title = "Edit Developer Setting";
                WindowHeading.Text = "Edit Developer Setting";

                VariableNameTextBox.Text = existingSetting.VariableName;
                DisplayNameTextBox.Text = existingSetting.DisplayName;
                ValueTextBox.Text = existingSetting.Value;

                VariableNameTextBox.IsReadOnly = true;
                VariableNameTextBox.Background = Brushes.LightGray;
            }

            VariableNameTextBox.TextChanged += VariableNameTextBox_TextChanged;
            UpdateRegistryPreview();
        }

        private void VariableNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateRegistryPreview();
        }

        private void UpdateRegistryPreview()
        {
            string variableName = VariableNameTextBox.Text.Trim();
            RegistryPreviewText.Text = $@"HKEY_CURRENT_USER\{_registryService.RegistryPath}\{variableName}";
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            string variableName = VariableNameTextBox.Text.Trim();
            string displayName = DisplayNameTextBox.Text.Trim();
            string value = ValueTextBox.Text;

            if (string.IsNullOrWhiteSpace(variableName))
            {
                ShowValidation("Variable Name is required.");
                VariableNameTextBox.Focus();
                return;
            }

            if (!Regex.IsMatch(variableName, @"^[A-Za-z_][A-Za-z0-9_]*$"))
            {
                ShowValidation("Variable Name must start with a letter or underscore and contain only letters, numbers or underscores.");
                VariableNameTextBox.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                ShowValidation("Display Name is required.");
                DisplayNameTextBox.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                ShowValidation("Value is required.");
                ValueTextBox.Focus();
                return;
            }

            var setting = new DeveloperSetting
            {
                VariableName = variableName,
                DisplayName = displayName,
                Value = value
            };

            try
            {
                bool isSaved;
                string? oldValue = _existingSetting?.Value;

                if (_isEditMode)
                {
                    isSaved = _registryService.UpdateSetting(setting);
                }
                else
                {
                    isSaved = _registryService.AddSetting(setting);
                }

                if (!isSaved)
                {
                    ExtendedMessageBox.Show(
                        _registryService.LastError ?? "Unable to save Developer Setting.",
                        "Save Error",
                        MessageType.Error,
                        MessageButtons.OK);
                    return;
                }

                // Notify DevConfig to update cache and fire SettingChanged live
                DevConfig.NotifySettingChanged(variableName, oldValue, value);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                ExtendedMessageBox.Show(
                    "Unable to save Developer Setting.\n" + ex.Message,
                    "Error",
                    MessageType.Error,
                    MessageButtons.OK);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private static void ShowValidation(string message)
        {
            ExtendedMessageBox.Show(
                message,
                "Validation Error",
                MessageType.Warning,
                MessageButtons.OK);
        }
    }
}
