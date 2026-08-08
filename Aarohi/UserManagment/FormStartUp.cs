using Aarohi.Classes;
using Aarohi.Classes.Common;
using Aarohi.Classes.Healper;
using Aarohi.Globals;
using Aarohi.Loadder;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace Aarohi.UserManagment
{
    public partial class FormStartUp : Form
    {
        #region Animation Fields

        private readonly Timer _timer;

        private readonly int _targetWidth = 1200;
        private readonly int _startWidth = 200;
        private readonly int _durationMs = 400;
        private readonly int _cornerRadius = 190;

        private readonly double _loaderStartPercent = 0.8;
        private readonly int _loaderWaitMs = 4500;

        private readonly int _panelTargetWidth = 500;
        private readonly int _panelShrinkDurationMs = 400;

        private readonly Stopwatch _stopwatch = new Stopwatch();

        private bool _loaderStarted = false;

        private int _panelStartWidth;
        private bool _panelShrinkInitialized = false;

        private readonly int _loginFadeDurationMs = 1000;
        private float _loginStartOpacity = 0.0f;
        private float _loginEndOpacity = 0.30f;
        private bool _loginFadeInitialized = false;

        private readonly DynamicClass _userClass;
        private string _LoginDataColumnName = string.Empty;
        private string _PasswordDataColumnName = string.Empty;

        private readonly Timer _usernameDebounceTimer;
        private readonly List<string> _allUserNames = new();

        private enum StartupStage
        {
            FormExpand,
            LoaderWait,
            PanelShrink,
            LoginFadeIn,
            Finished
        }

        private StartupStage _stage = StartupStage.FormExpand;

        private AarohiLoadder? loader;

        [DesignerSerializationVisibility(
            DesignerSerializationVisibility.Hidden)]
        public double GuideFadeDelay { get; set; } = 0.10;

        [DesignerSerializationVisibility(
            DesignerSerializationVisibility.Hidden)]
        public double GuideFadeDuration { get; set; } = 0.60;

        #endregion

        #region Shift Selection

        private readonly List<ShiftLoginItem> _configuredShifts =
            new List<ShiftLoginItem>();

        /// <summary>
        /// Generic shift information supplied by the main application.
        /// The class library does not access IMTS Globals or Shift_Master.
        /// </summary>
        public sealed class ShiftLoginItem
        {
            public int ShiftId { get; set; }

            public string ShiftName { get; set; } =
                string.Empty;

            public TimeSpan StartTime { get; set; }

            public TimeSpan EndTime { get; set; }

            public override string ToString()
            {
                return ShiftName;
            }
        }

        [DesignerSerializationVisibility(
            DesignerSerializationVisibility.Hidden)]
        public int SelectedShiftId
        {
            get
            {
                return GetSelectedShiftItem()?.ShiftId ?? 0;
            }
        }

        [DesignerSerializationVisibility(
            DesignerSerializationVisibility.Hidden)]
        public string SelectedShiftName
        {
            get
            {
                return GetSelectedShiftItem()?.ShiftName ??
                       string.Empty;
            }
        }

        [DesignerSerializationVisibility(
            DesignerSerializationVisibility.Hidden)]
        public TimeSpan SelectedShiftStartTime
        {
            get
            {
                return GetSelectedShiftItem()?.StartTime ??
                       TimeSpan.Zero;
            }
        }

        [DesignerSerializationVisibility(
            DesignerSerializationVisibility.Hidden)]
        public TimeSpan SelectedShiftEndTime
        {
            get
            {
                return GetSelectedShiftItem()?.EndTime ??
                       TimeSpan.Zero;
            }
        }

        /// <summary>
        /// Called by the consuming application after FormStartUp is created.
        /// It enables the shift row and fills the dropdown.
        /// </summary>
        public void ConfigureShiftSelection(
            IEnumerable<ShiftLoginItem> shifts,
            int preferredShiftId = 0)
        {
            _configuredShifts.Clear();

            if (shifts != null)
            {
                _configuredShifts.AddRange(
                    shifts
                        .Where(x =>
                            x != null &&
                            x.ShiftId > 0 &&
                            !string.IsNullOrWhiteSpace(
                                x.ShiftName))
                        .GroupBy(x => x.ShiftId)
                        .Select(group => group.First())
                        .OrderBy(x => x.StartTime)
                        .ThenBy(x => x.ShiftName));
            }

            comboBoxShiftLogin.BeginUpdate();

            try
            {
                comboBoxShiftLogin.Items.Clear();

                //comboBoxShiftLogin.Items.Add(
                //    new ShiftLoginItem
                //    {
                //        ShiftId = 0,
                //        ShiftName = "-- Select Shift --"
                //    });

                foreach (ShiftLoginItem shift in _configuredShifts)
                    comboBoxShiftLogin.Items.Add(shift);

                int selectedIndex = 0;

                if (preferredShiftId > 0)
                {
                    for (int index = 1;
                         index < comboBoxShiftLogin.Items.Count;
                         index++)
                    {
                        if (comboBoxShiftLogin.Items[index]
                            is ShiftLoginItem item &&
                            item.ShiftId == preferredShiftId)
                        {
                            selectedIndex = index;
                            break;
                        }
                    }
                }

                comboBoxShiftLogin.SelectedIndex =
                    selectedIndex;
            }
            finally
            {
                comboBoxShiftLogin.EndUpdate();
            }

            PanelLoginElementWrapper.GridRowCount = 3;
            LoginShiftWrapper.Visible = true;

            // Give the new third row enough vertical space.
            Height = 570;
            CenterToScreen();

            PanelLoginElementWrapper.PerformLayout();
            LoginElementWrapper.PerformLayout();
            LoginWrapper.PerformLayout();
        }

        /// <summary>
        /// Shift selection is validated separately from username/password.
        /// </summary>
        public bool TryGetSelectedShift(
            out ShiftLoginItem selectedShift)
        {
            ShiftLoginItem? item =
                GetSelectedShiftItem();

            if (item == null ||
                item.ShiftId <= 0 ||
                string.IsNullOrWhiteSpace(item.ShiftName))
            {
                selectedShift =
                    new ShiftLoginItem();

                return false;
            }

            selectedShift = item;
            return true;
        }

        private ShiftLoginItem? GetSelectedShiftItem()
        {
            return comboBoxShiftLogin.SelectedItem
                as ShiftLoginItem;
        }

        #endregion

        private readonly string LoginInfoPath =
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ApplicationData),
                "Aarohi",
                "IPTS_Git",
                "Login.info");

        public event EventHandler<LoginSuccessEventArgs>?
            LoginSuccess;

        private bool _hashingEnabled = true;

        [DesignerSerializationVisibility(
            DesignerSerializationVisibility.Hidden)]
        public bool HashingEnabled
        {
            get => _hashingEnabled;
            set => _hashingEnabled = value;
        }

        public sealed class LoginSuccessEventArgs : EventArgs
        {
            public string UserName { get; }

            public string Password { get; }

            public LoginSuccessEventArgs(
                string userName,
                string passWord)
            {
                UserName = userName;
                Password = passWord;
            }
        }

        private bool _loginFlowRunning = false;
        private bool _isPasswordVisible = false;

        public FormStartUp(
            string dbo,
            string userTabelName,
            string LoginDataColumnName,
            string PasswordDataColumnName,
            bool WantRememberMe = false)
        {
            InitializeComponent();
            _usernameDebounceTimer = new Timer
            {
                Interval = 250
            };

            _usernameDebounceTimer.Tick += UsernameDebounceTimer_Tick;

            textBox2.UseSystemPasswordChar = true;
            //button1.Text = "Show";

            button1.Text = "";

            button1.FlatStyle = FlatStyle.Flat;
            button1.FlatAppearance.BorderSize = 0;
            button1.Cursor = Cursors.Hand;

            button1.Paint += button1_Paint;

            checkBoxRememberMe.Visible = WantRememberMe;

            // Hidden until the main project supplies shift records.
            LoginShiftWrapper.Visible = false;

            if (string.IsNullOrEmpty(dbo))
                throw new ArgumentNullException(nameof(dbo));

            if (string.IsNullOrEmpty(userTabelName))
                throw new ArgumentNullException(
                    nameof(userTabelName));

            if (string.IsNullOrEmpty(
                LoginDataColumnName))
            {
                throw new ArgumentNullException(
                    nameof(LoginDataColumnName));
            }

            if (string.IsNullOrEmpty(
                PasswordDataColumnName))
            {
                throw new ArgumentNullException(
                    nameof(PasswordDataColumnName));
            }

            if (string.IsNullOrEmpty(
                DynamicClass.Soft_Name))
            {
                throw new ArgumentNullException(
                    nameof(DynamicClass.Soft_Name));
            }

            _userClass =
                new DynamicClass(
                    dbo,
                    userTabelName);

            labelSoftName.Text =
                DynamicClass.Soft_Name;

            _LoginDataColumnName =
                LoginDataColumnName;

            _PasswordDataColumnName =
                PasswordDataColumnName;

            if (LoginWrapper != null)
                LoginWrapper.Dock = DockStyle.Fill;

            if (LoadingWrapper != null)
                LoadingWrapper.Dock = DockStyle.Fill;

            panelLoadder.Width = 1130;

            LoginWrapper.Visible = false;
            LoginWrapper.Enabled = false;
            LoginWrapper.GradientOpacity =
                _loginStartOpacity;

            LoadingWrapper.Visible = false;
            LoadingWrapper.Enabled = false;

            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint,
                true);

            UpdateStyles();

            FormBorderStyle =
                FormBorderStyle.None;

            StartPosition =
                FormStartPosition.CenterScreen;

            DoubleBuffered = true;

            Width = _startWidth;
            Height = 500;
            CenterToScreen();

            _timer =
                new Timer
                {
                    Interval = 15
                };

            _timer.Tick += Timer_Tick;

            Shown += FormStartUp_Shown;
            Load += FormStartUp_Load;
        }

        public FormStartUp(
            double guideFadeDuration,
            string dbo,
            string userTabelName,
            string LoginDataColumnName,
            string PasswordDataColumnName)
            : this(
                dbo,
                userTabelName,
                LoginDataColumnName,
                PasswordDataColumnName)
        {
            GuideFadeDuration =
                guideFadeDuration;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp =
                    base.CreateParams;

                cp.ExStyle |=
                    0x02000000;

                return cp;
            }
        }

        private void FormStartUp_Load(
            object? sender,
            EventArgs e)
        {
            loader =
                new AarohiLoadder
                {
                    Dock = DockStyle.Fill,
                    BackColor = Color.White,
                    Size = new Size(
                        Width,
                        1000),
                    ShowGuideLines = false
                };

            if (!panelLoadder.Controls.Contains(loader))
                panelLoadder.Controls.Add(loader);

            ApplyRoundedCorners(
                _cornerRadius);

            _stage =
                StartupStage.FormExpand;

            _stopwatch.Restart();
            _timer.Start();
        }

        private void FormStartUp_Shown(
            object? sender,
            EventArgs e)
        {
            Shown -= FormStartUp_Shown;

            //comboBoxUsername.Items.AddRange(
            //    _userClass.GetColumnValues(
            //        _LoginDataColumnName));

            string[] users = _userClass.GetColumnValues(_LoginDataColumnName);

            _allUserNames.Clear();
            _allUserNames.AddRange(users);

            comboBoxUsername.Items.AddRange(users);


            if (RegistryHelper.LoadBool(
                RegistryHelper.storeLocs.Credentials,
                "IsDevPC",
                false))
            {
                comboBoxUsername.Items.Add(
                    AGLobals.Utils.DevName);

                comboBoxUsername.SelectedItem =
                    AGLobals.Utils.DevName;

                textBox2.Text =
                    DateTime.Now.ToString(
                        "ddMMyyyyHH");
            }

            _ = TryAutoLoginAsync();
        }

        private async Task TryAutoLoginAsync()
        {
            try
            {
                await Task.Delay(50);

                if (IsDisposed)
                    return;

                LoadStoredValues();
            }
            catch
            {
                // Existing behavior: ignore stored-login errors.
            }
        }

        #region Animation

        private void Timer_Tick(
            object? sender,
            EventArgs e)
        {
            switch (_stage)
            {
                case StartupStage.FormExpand:
                    HandleFormExpand();
                    break;

                case StartupStage.LoaderWait:
                    HandleLoaderWait();
                    break;

                case StartupStage.PanelShrink:
                    HandlePanelShrink();
                    break;

                case StartupStage.LoginFadeIn:
                    HandleLoginFadeIn();
                    break;

                case StartupStage.Finished:
                    _timer.Stop();
                    _stopwatch.Stop();
                    break;
            }
        }

        private void HandleFormExpand()
        {
            double elapsed =
                _stopwatch.ElapsedMilliseconds;

            double t =
                Math.Min(
                    1.0,
                    elapsed / _durationMs);

            double eased =
                EaseInOut(t);

            int newWidth =
                _startWidth +
                (int)(
                    (_targetWidth - _startWidth) *
                    eased);

            int centerX =
                Left + Width / 2;

            Width = newWidth;
            Left = centerX - Width / 2;

            ApplyRoundedCorners(
                _cornerRadius);

            if (!_loaderStarted &&
                t >= _loaderStartPercent)
            {
                _loaderStarted = true;
                StartLoaderAnimation();
            }

            if (t >= 1.0)
            {
                _stage =
                    StartupStage.LoaderWait;

                _stopwatch.Restart();
            }
        }

        private void HandleLoaderWait()
        {
            if (_stopwatch.ElapsedMilliseconds >=
                _loaderWaitMs)
            {
                _stage =
                    StartupStage.PanelShrink;

                _stopwatch.Restart();
                _panelShrinkInitialized = false;
            }
        }

        private void HandlePanelShrink()
        {
            if (!_panelShrinkInitialized)
            {
                panelLoadder.Dock =
                    DockStyle.Left;

                _panelStartWidth =
                    panelLoadder.Width;

                panelLoadder.Left =
                    (ClientSize.Width -
                     panelLoadder.Width) / 2;

                _panelShrinkInitialized = true;
            }

            double elapsed =
                _stopwatch.ElapsedMilliseconds;

            double t =
                Math.Min(
                    1.0,
                    elapsed /
                    _panelShrinkDurationMs);

            double eased =
                EaseInOut(t);

            int newWidth =
                _panelStartWidth +
                (int)(
                    (_panelTargetWidth -
                     _panelStartWidth) *
                    eased);

            int centerX =
                panelLoadder.Left +
                panelLoadder.Width / 2;

            panelLoadder.Width =
                newWidth;

            panelLoadder.Left =
                centerX -
                panelLoadder.Width / 2;

            if (t >= 1.0)
            {
                _stage =
                    StartupStage.LoginFadeIn;

                _stopwatch.Restart();
                _loginFadeInitialized = false;
            }
        }

        private void HandleLoginFadeIn()
        {
            if (!_loginFadeInitialized)
            {
                ShowLoginUi();

                LoginWrapper.GradientOpacity =
                    _loginStartOpacity;

                _loginFadeInitialized = true;
            }

            double elapsed =
                _stopwatch.ElapsedMilliseconds;

            double t =
                Math.Min(
                    1.0,
                    elapsed /
                    _loginFadeDurationMs);

            double eased =
                EaseInOut(t);

            LoginWrapper.GradientOpacity =
                (float)(
                    _loginStartOpacity +
                    (_loginEndOpacity -
                     _loginStartOpacity) *
                    eased);

            if (t >= 1.0)
            {
                LoginWrapper.GradientOpacity =
                    _loginEndOpacity;

                _stage =
                    StartupStage.Finished;

                _stopwatch.Stop();
            }
        }

        private static double EaseInOut(
            double t)
        {
            return 0.5 -
                   0.5 *
                   Math.Cos(Math.PI * t);
        }

        private void ApplyRoundedCorners(
            int radius)
        {
            if (Width <= 0 ||
                Height <= 0)
            {
                return;
            }

            Rectangle rect =
                new Rectangle(
                    0,
                    0,
                    Width,
                    Height);

            using GraphicsPath path =
                new GraphicsPath();

            path.AddArc(
                rect.X,
                rect.Y,
                radius,
                radius,
                180,
                90);

            path.AddArc(
                rect.Right - radius,
                rect.Y,
                radius,
                radius,
                270,
                90);

            path.AddArc(
                rect.Right - radius,
                rect.Bottom - radius,
                radius,
                radius,
                0,
                90);

            path.AddArc(
                rect.X,
                rect.Bottom - radius,
                radius,
                radius,
                90,
                90);

            path.CloseAllFigures();

            Region?.Dispose();
            Region = new Region(path);
        }

        private void StartLoaderAnimation()
        {
            if (loader == null)
                return;

            loader.SetEasing(
                AarohiLoadder.EasingType.EaseOut);

            loader.fillRevealMode =
                AarohiLoadder.RevealMode.Radial;

            loader.SetFillTiming(
                AarohiLoadder.FillTimingMode.AfterStrokes,
                0.1);

            loader.SetFillEasing(
                AarohiLoadder.EasingType.EaseInOut);

            loader.SetFillOnTop(true);
            loader.SetGlobalDuration(2.0);

            loader.FadeStrokesAfterFill = true;
            loader.StrokeFadeDuration = 0.9;
            loader.StrokeFadeDelay = 0.2;

            loader.ReflectionSpeed = 0.3;
            loader.ReflectionThickness = 0.22;
            loader.ReflectionIntensity = 0.15f;
            loader.ReflectionAngle = -30f;

            loader.StartAnimation();
            loader.StartReflection();
        }

        #endregion

        #region UI Switching

        private void ShowLoginUi()
        {
            LoadingWrapper.Visible = false;
            LoadingWrapper.Enabled = false;

            LoginWrapper.Visible = true;
            LoginWrapper.Enabled = true;
            LoginWrapper.BringToFront();

            Invalidate(true);
            Update();

            LoginWrapper.Invalidate(true);
            LoginWrapper.Refresh();
        }

        private void ShowLoadingUi()
        {
            LoginWrapper.Visible = false;
            LoginWrapper.Enabled = false;

            LoadingWrapper.Visible = true;
            LoadingWrapper.Enabled = true;
            LoadingWrapper.BringToFront();

            Invalidate(true);
            Update();

            LoadingWrapper.Invalidate(true);
            LoadingWrapper.Refresh();
        }

        #endregion

        #region Login

        private void LoginButton_Click(
            object sender,
            EventArgs e)
        {
            if (_loginFlowRunning)
                return;

            string userName =
                comboBoxUsername.Text;

            string password =
                textBox2.Text.Trim();

            if (string.IsNullOrWhiteSpace(
                userName))
            {
                MessageBox.Show(
                    "Please enter user name.",
                    "Validation",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return;
            }

            if (string.IsNullOrWhiteSpace(
                password))
            {
                MessageBox.Show(
                    "Please enter password.",
                    "Validation",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return;
            }

            if (HashingEnabled &&
                userName != AGLobals.Utils.DevName)
            {
                password =
                    UserManager.HashPassword(
                        password);
            }

            if (!TryAuthenticate(
                userName,
                password))
            {
                return;
            }

            HandleRememberMe(
                userName,
                password);

            _loginFlowRunning = true;

            LoginSuccess?.Invoke(
                this,
                new LoginSuccessEventArgs(
                    userName,
                    password));
        }

        private void HandleRememberMe(
            string userName,
            string password)
        {
            if (checkBoxRememberMe.Checked)
            {
                if (!string.Equals(
                    userName,
                    AGLobals.Utils.DevName,
                    StringComparison.OrdinalIgnoreCase))
                {
                    SetRegistryHashes(
                        userName,
                        password);

                    SaveInfo(
                        userName,
                        password);
                }
                else
                {
                    SetRegistryHashes(
                        string.Empty,
                        string.Empty);

                    if (File.Exists(LoginInfoPath))
                        File.Delete(LoginInfoPath);
                }
            }
            else
            {
                SetRegistryHashes(
                    string.Empty,
                    string.Empty);
            }
        }

        private bool TryAuthenticate(
            string userName,
            string password)
        {
            try
            {
                if (userName ==
                    AGLobals.Utils.DevName)
                {
                    if (password ==
                        DateTime.Now.ToString(
                            "ddMMyyyyHH"))
                    {
                        return true;
                    }

                    MessageBox.Show(
                        "Incorrect password.",
                        "Login Failed",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);

                    return false;
                }

                Dictionary<string, object> values =
                    _userClass.GetRowAsDictionary(
                        _LoginDataColumnName,
                        userName);

                if (values == null ||
                    values.Count == 0)
                {
                    MessageBox.Show(
                        "Username not found.",
                        "Login Failed",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);

                    return false;
                }

                string dbUserName =
                    values[_LoginDataColumnName]
                        ?.ToString() ??
                    string.Empty;

                string dbPassword =
                    values[_PasswordDataColumnName]
                        ?.ToString() ??
                    string.Empty;

                if (!string.Equals(
                    userName,
                    dbUserName,
                    StringComparison.Ordinal))
                {
                    MessageBox.Show(
                        "Username does not match.",
                        "Login Failed",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);

                    return false;
                }

                if (!string.Equals(
                    password,
                    dbPassword,
                    StringComparison.Ordinal))
                {
                    MessageBox.Show(
                        "Incorrect password.",
                        "Login Failed",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);

                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "An error occurred while checking login. " +
                    "Please contact support.\n\n" +
                    ex.Message,
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                return false;
            }
        }

        private bool LoadStoredValues()
        {
            try
            {
                if (!File.Exists(LoginInfoPath))
                    return false;

                string[] lines =
                    File.ReadAllLines(
                        LoginInfoPath);

                if (lines.Length < 2)
                    return false;

                string encryptedName =
                    lines[0];

                string encryptedPassword =
                    lines[1];

                string realName =
                    RegistryHelper.Decrypt(
                        encryptedName);

                string realPassword =
                    RegistryHelper.Decrypt(
                        encryptedPassword);

                if (string.IsNullOrWhiteSpace(realName) ||
                    string.IsNullOrWhiteSpace(realPassword))
                {
                    return false;
                }

                comboBoxUsername.SelectedItem =
                    realName;

                textBox2.Text =
                    realPassword;

                checkBoxRememberMe.Checked =
                    true;

                if (!TryAuthenticate(
                    realName,
                    realPassword))
                {
                    File.Delete(LoginInfoPath);
                    return false;
                }

                if (_loginFlowRunning)
                    return true;

                _loginFlowRunning = true;

                LoginSuccess?.Invoke(
                    this,
                    new LoginSuccessEventArgs(
                        realName,
                        realPassword));

                return true;
            }
            catch
            {
                try
                {
                    File.Delete(LoginInfoPath);
                }
                catch
                {
                }

                return false;
            }
        }

        public void SaveInfo(
            string userName,
            string password)
        {
            string encryptedName =
                RegistryHelper.Encrypt(
                    userName);

            string encryptedPassword =
                RegistryHelper.Encrypt(
                    password);

            string? folder =
                Path.GetDirectoryName(
                    LoginInfoPath);

            if (!string.IsNullOrEmpty(folder) &&
                !Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            File.WriteAllText(
                LoginInfoPath,
                encryptedName +
                Environment.NewLine +
                encryptedPassword);
        }

        private void SetRegistryHashes(
            string userName,
            string password)
        {
            using SHA256 sha =
                SHA256.Create();

            byte[] userBytes =
                Encoding.UTF8.GetBytes(
                    userName ??
                    string.Empty);

            string userHash =
                BitConverter.ToString(
                    sha.ComputeHash(
                        userBytes))
                .Replace("-", "");

            byte[] passwordBytes =
                Encoding.UTF8.GetBytes(
                    password ??
                    string.Empty);

            string passwordHash =
                BitConverter.ToString(
                    sha.ComputeHash(
                        passwordBytes))
                .Replace("-", "");

            RegistryHelper.SaveString(
                RegistryHelper.storeLocs.Credentials,
                "AESPLXU",
                userHash);

            RegistryHelper.SaveString(
                RegistryHelper.storeLocs.Credentials,
                "AESPLXP",
                passwordHash);
        }

        public void ResetLoginTrigger()
        {
            _loginFlowRunning = false;
        }

        #endregion

        #region Post-login Loading

        public async Task<bool>
            StartPostLoginLoadingAsync(
                Func<IProgress<StartupProgress>, Task>
                    loadFunc)
        {
            if (InvokeRequired)
            {
                return await
                    (Task<bool>)Invoke(
                        new Func<Task<bool>>(
                            () =>
                                StartPostLoginLoadingAsync(
                                    loadFunc)));
            }

            ShowLoadingUi();

            progressBar1.Minimum = 0;
            progressBar1.Maximum = 100;
            progressBar1.Value = 0;

            lblStatus.Text =
                "Starting...";

            Progress<StartupProgress> progress =
                new Progress<StartupProgress>(
                    p =>
                    {
                        int value =
                            Math.Max(
                                0,
                                Math.Min(
                                    100,
                                    p.Percent));

                        progressBar1.Value =
                            value;

                        lblStatus.Text =
                            p.Message ??
                            string.Empty;
                    });

            try
            {
                await loadFunc(progress);

                progressBar1.Value = 100;
                lblStatus.Text = "Done.";

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Startup loading failed:\n\n" +
                    ex.Message,
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                ShowLoginUi();
                _loginFlowRunning = false;

                return false;
            }
        }

        public sealed class StartupProgress
        {
            public int Percent { get; }

            public string Message { get; }

            public StartupProgress(
                int percent,
                string message)
            {
                Percent = percent;
                Message = message;
            }
        }

        #endregion

        private void LoadingWrapper_Paint(object sender,PaintEventArgs e)
        {
        }

        private void label1_Click(object sender,EventArgs e)
        {
        }

        private void PanelLoginElementWrapper_Paint(object sender,PaintEventArgs e)
        {
        }

        private void button1_Click(object sender, EventArgs e)
        {
            _isPasswordVisible =!_isPasswordVisible;

            textBox2.UseSystemPasswordChar =!_isPasswordVisible;

            button1.Invalidate(); //marks button1 as needing repainting. Windows then calls your Paint event again:

            //button1.Text =
            //    _isPasswordVisible
            //        ? "Hide"
            //        : "Show";
        }

        private void textBox2_KeyDown(object sender,KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;

                LoginButton_Click(LoginButton,EventArgs.Empty);
            }
        }

        private void comboBoxShiftLogin_KeyDown(object sender,KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;

                LoginButton_Click(LoginButton,EventArgs.Empty);
            }
        }
      
        private void comboBoxUsername_SelectedIndexChanged(object sender,EventArgs e)
        {
            textBox2.Text =string.Empty;
        }
        private void button1_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            int width = button1.ClientSize.Width;
            int height = button1.ClientSize.Height;

            float cx = width / 2f;
            float cy = height / 2f;

            using Pen pen = new Pen(Color.FromArgb(45, 50, 65), 2.2f);
            using SolidBrush brush = new SolidBrush(Color.FromArgb(45, 50, 65));

            // Eye shape
            RectangleF eyeRect = new RectangleF(
                cx - 11,
                cy - 7,
                22,
                14);

            using GraphicsPath eyePath = new GraphicsPath();

            eyePath.AddBezier(
                eyeRect.Left, cy,
                cx - 6, cy - 8,
                cx + 6, cy - 8,
                eyeRect.Right, cy);

            eyePath.AddBezier(
                eyeRect.Right, cy,
                cx + 6, cy + 8,
                cx - 6, cy + 8,
                eyeRect.Left, cy);

            e.Graphics.DrawPath(pen, eyePath);

            // Pupil
            e.Graphics.FillEllipse(
                brush,
                cx - 3,
                cy - 3,
                6,
                6);

            // If password is VISIBLE → draw slash
            if (_isPasswordVisible)
            {
                using Pen slashPen =
                    new Pen(Color.FromArgb(45, 50, 65), 2.5f)
                    {
                        StartCap = LineCap.Round,
                        EndCap = LineCap.Round
                    };

                e.Graphics.DrawLine(
                    slashPen,
                    cx - 12,
                    cy - 10,
                    cx + 12,
                    cy + 10);
            }
        }

        private void comboBoxUsername_TextUpdate(object sender, EventArgs e)
        {
            _usernameDebounceTimer.Stop();
            _usernameDebounceTimer.Start();
        }

        private void UsernameDebounceTimer_Tick(object? sender,EventArgs e)
        {
            _usernameDebounceTimer.Stop();

            string enteredText = comboBoxUsername.Text;

            var matchedUsers = _allUserNames.Where(x => x.StartsWith(enteredText,StringComparison.OrdinalIgnoreCase)).ToList();

            comboBoxUsername.BeginUpdate();

            comboBoxUsername.Items.Clear();
            comboBoxUsername.Items.AddRange(matchedUsers.Cast<object>().ToArray());

            comboBoxUsername.EndUpdate();
            comboBoxUsername.Cursor = Cursors.Default;

            comboBoxUsername.Text = enteredText;

            comboBoxUsername.SelectionStart =enteredText.Length;

            comboBoxUsername.SelectionLength = 0;

            comboBoxUsername.DroppedDown =matchedUsers.Count > 0;
        }
    }
}
