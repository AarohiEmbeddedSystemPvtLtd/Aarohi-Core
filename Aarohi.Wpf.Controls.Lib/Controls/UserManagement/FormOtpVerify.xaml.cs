using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Aarohi.UserManagment;
using Aarohi.Wpf.Controls.Lib.Controls.MessageBox;

namespace Aarohi.Wpf.Controls.Lib.Controls.UserManagement
{
    public partial class FormOtpVerify : Window
    {
        public string Otp { get; private set; } = string.Empty;

        public bool IsOtpVerified { get; private set; } = false;

        public FormOtpVerify()
        {
            InitializeComponent();
            Loaded += FormOtpVerify_Loaded;
        }

        private void FormOtpVerify_Loaded(object sender, RoutedEventArgs e)
        {
            TextBoxOtp.Focus();
        }

        private void ButtonVerify_Click(object sender, RoutedEventArgs e)
        {
            VerifyOtp();
        }

        private void TextBoxOtp_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                VerifyOtp();
            }
        }

        private void TextBoxOtp_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !e.Text.All(char.IsDigit);
        }

        private void VerifyOtp()
        {
            Otp = TextBoxOtp.Text.Trim();

            if (string.IsNullOrWhiteSpace(Otp))
            {
                ShowMessage("Please enter OTP.", false);
                TextBoxOtp.Focus();
                return;
            }

            if (Otp.Length < 6)
            {
                ShowMessage("Please enter valid 6 digit OTP.", false);
                TextBoxOtp.Focus();
                TextBoxOtp.SelectAll();
                return;
            }

            try
            {
                bool isVerified = UserManager.VerifyOtp(Otp);

                if (!isVerified)
                {
                    IsOtpVerified = false;
                    ShowMessage("Invalid OTP. Please enter correct OTP.", false);
                    TextBoxOtp.Focus();
                    TextBoxOtp.SelectAll();
                    return;
                }

                IsOtpVerified = true;
                ShowMessage("OTP verified successfully.", true);

                ModernMessageBox.Show(
                    "OTP verified successfully.",
                    "OTP Verification",
                    ModernMessageBoxButton.OK,
                    ModernMessageBoxIcon.Information);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                IsOtpVerified = false;
                ShowMessage(ex.Message, false);

                ModernMessageBox.Show(
                    ex.Message,
                    "OTP Verification",
                    ModernMessageBoxButton.OK,
                    ModernMessageBoxIcon.Warning);
            }
        }

        private void ShowMessage(string message, bool success)
        {
            TextBlockMessage.Text = message;

            if (success)
            {
                TextBlockMessage.SetResourceReference(
                    TextBlock.ForegroundProperty,
                    "AppBrush.Primary");
            }
            else
            {
                TextBlockMessage.Foreground =
                    new SolidColorBrush(Color.FromRgb(220, 38, 38));
            }
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            IsOtpVerified = false;
            DialogResult = false;
            Close();
        }

        private void ButtonClose_Click(object sender, RoutedEventArgs e)
        {
            IsOtpVerified = false;
            DialogResult = false;
            Close();
        }
    }
}
