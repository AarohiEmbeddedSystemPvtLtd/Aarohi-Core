using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Aarohi.Wpf.Controls.Lib.Controls.Emergency
{
    public partial class EmergencyControl : UserControl
    {
        private bool _emergency;
        private bool _overCurrent;
        private bool _overLoad;
        private bool _rpmValidation;
        private bool _driveCommunication;
        private bool _powerFailure;
        private bool _driveStatus;
        private bool _transducerStatus;
        public event EventHandler? StopRequested;

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            StopRequested?.Invoke(this, EventArgs.Empty);
        }

        public bool Emergency
        {
            get => _emergency;
            set
            {
                _emergency = value;
                SetStatus(cardEmergency, txtEmergency, value, "ACTIVE", "OK");
                //UpdateMainStatus();
            }
        }

        public bool OverCurrent
        {
            get => _overCurrent;
            set
            {
                _overCurrent = value;
                SetStatus(cardOverCurrent, txtOverCurrent, value, "HIGH", "OK");
                //UpdateMainStatus();
            }
        }

        public bool OverLoad
        {
            get => _overLoad;
            set
            {
                _overLoad = value;
                SetStatus(cardOverLoad, txtOverLoad, value, "TRIP", "OK");
               // UpdateMainStatus();
            }
        }

        public bool RPMValidation
        {
            get => _rpmValidation;
            set
            {
                _rpmValidation = value;
                SetStatus(cardRPMValidation, txtRPMValidation, value, "ERROR", "OK");
               // UpdateMainStatus();
            }
        }

        public bool DriveCommunication
        {
            get => _driveCommunication;
            set
            {
                _driveCommunication = value;
                SetStatus(cardDriveCommunication, txtDriveCommunication, value, "ERROR", "OK");
                //UpdateMainStatus();
            }
        }

        public bool PowerFailure
        {
            get => _powerFailure;
            set
            {
                _powerFailure = value;
                SetStatus(cardPowerFailure, txtPowerFailure, value, "FAIL", "OK");
                //UpdateMainStatus();
            }
        }

        public bool DriveStatus
        {
            get => _driveStatus;
            set
            {
                _driveStatus = value;

                // Your XAML name is txtDriveSattus, so I used same name here.
                SetStatus(cardDriveStatus, txtDriveSattus, value, "FAULT", "OK");

                //UpdateMainStatus();
            }
        }

        public bool TransducerStatus
        {
            get => _transducerStatus;
            set
            {
                _transducerStatus = value;

                // Your XAML name is cardTranducerStatus / txtTranducerStatus, so I used same name here.
                SetStatus(cardTranducerStatus, txtTranducerStatus, value, "FAULT", "OK");

                //UpdateMainStatus();
            }
        }

        public bool IsAnyEmergencyActive =>
            Emergency ||
            OverCurrent ||
            OverLoad ||
            RPMValidation ||
            DriveCommunication ||
            PowerFailure ||
            DriveStatus ||
            TransducerStatus;

        public EmergencyControl()
        {
            InitializeComponent();

            //UpdateMainStatus();
        }

        private void SetStatus(Border card, TextBlock textBlock, bool isFault, string faultText, string okText)
        {
            if (card == null || textBlock == null)
                return;

            if (isFault)
            {
                card.Background = GetBrush("AppBrush.DangerLight", "#FEE2E2");
                card.BorderBrush = GetBrush("AppBrush.Danger", "#DC2626");

                textBlock.Text = faultText;
                textBlock.Foreground = GetBrush("AppBrush.Danger", "#DC2626");
            }
            else
            {
                card.Background = GetBrush("AppBrush.SurfaceAlt", "#FFFFFF");
                card.BorderBrush = GetBrush("AppBrush.Border", "#E5E7EB");

                textBlock.Text = okText;
                textBlock.Foreground = GetBrush("AppBrush.Success", "#22C55E");
            }
        }

        //private void UpdateMainStatus()
        //{
        //    if (txtMainStatus == null || MainStatusBorder == null || txtValidationMessage == null)
        //        return;

        //    if (IsAnyEmergencyActive)
        //    {
        //        txtMainStatus.Text = "ERROR DETECTED";
        //        MainStatusBorder.Background = GetBrush("AppBrush.Danger", "#DC2626");

        //        txtValidationMessage.Text = "Machine cannot start. Please clear all emergency conditions.";
        //        txtValidationMessage.Foreground = GetBrush("AppBrush.Danger", "#DC2626");
        //    }
        //    else
        //    {
        //        txtMainStatus.Text = "SYSTEM OK";
        //        MainStatusBorder.Background = GetBrush("AppBrush.Success", "#22C55E");

        //        txtValidationMessage.Text = "No emergency condition detected. Machine is ready.";
        //        txtValidationMessage.Foreground = GetBrush("AppBrush.Success", "#22C55E");
        //    }
        //}

        private Brush GetBrush(string resourceKey, string fallbackColor)
        {
            return TryFindResource(resourceKey) as Brush
                   ?? new SolidColorBrush((Color)ColorConverter.ConvertFromString(fallbackColor));
        }

        public bool CanMachineStart()
        {
            return !IsAnyEmergencyActive;
        }
    }
}