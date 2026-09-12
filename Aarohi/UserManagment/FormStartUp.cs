using Aarohi.Classes;
using Aarohi.Classes.Common;
using Aarohi.Classes.Healper;
using Aarohi.Globals;
using Aarohi.Loadder;
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

        // IMPORTANT:
        // These values and the animation methods below intentionally follow
        // the old backup version that was smooth on the actual machine.
        private readonly Timer _timer;

        private int _targetWidth = 1200;
        private int _startWidth = 200;

        private readonly int _durationMs = 400;
        private readonly int _cornerRadius = 190;

        private readonly double _loaderStartPercent = 0.8;
        private readonly int _loaderWaitMs = 4500;

        private int _panelTargetWidth = 500;
        private readonly int _panelShrinkDurationMs = 400;

        private readonly Stopwatch _stopwatch =
            new Stopwatch();

        private bool _loaderStarted = false;

        private int _panelStartWidth;
        private bool _panelShrinkInitialized = false;

        private readonly int _loginFadeDurationMs = 1000;
        private float _loginStartOpacity = 0.0f;
        private float _loginEndOpacity = 0.30f;
        private bool _loginFadeInitialized = false;

        private readonly DynamicClass _userClass;

        private string _LoginDataColumnName =
            string.Empty;

        private string _PasswordDataColumnName =
            string.Empty;

        private readonly Timer _usernameDebounceTimer;

        private readonly List<string> _allUserNames =
            new List<string>();

        private enum StartupStage
        {
            FormExpand,
            LoaderWait,
            PanelShrink,
            LoginFadeIn,
            Finished
        }

        private StartupStage _stage =
            StartupStage.FormExpand;

        private AarohiLoadder? loader;

        private readonly Button _buttonClose;

        // Responsive values.
        private const int ResponsiveBaseWidth = 1200;
        private const int ResponsiveBaseHeightWithShift = 570;
        private const int ResponsiveBaseHeightWithoutShift = 500;

        private float _currentUiScale = 1.0f;
        private bool _applyingResponsiveScale;
        private bool _firstDisplayPrepared;

        [DesignerSerializationVisibility(
            DesignerSerializationVisibility.Hidden)]
        public double GuideFadeDelay { get; set; } = 0.10;

        [DesignerSerializationVisibility(
            DesignerSerializationVisibility.Hidden)]
        public double GuideFadeDuration { get; set; } = 0.60;

        [DefaultValue(false)]
        [DesignerSerializationVisibility(
            DesignerSerializationVisibility.Visible)]
        public bool ShowCloseButton
        {
            get => _buttonClose.Visible;
            set
            {
                _buttonClose.Visible = value;

                if (value)
                    _buttonClose.BringToFront();
            }
        }

        #endregion

        #region Shift Selection

        private bool _showShiftSelection = true;

        [DefaultValue(true)]
        [DesignerSerializationVisibility(
            DesignerSerializationVisibility.Visible)]
        public bool ShowShiftSelection
        {
            get => _showShiftSelection;
            set
            {
                _showShiftSelection = value;

                LoginShiftWrapper.Visible = value;

                PanelLoginElementWrapper.GridRowCount =
                    value ? 3 : 2;

                if (_stage == StartupStage.Finished)
                {
                    ApplyResponsiveScale();
                }

                PanelLoginElementWrapper.PerformLayout();
                LoginElementWrapper.PerformLayout();
                LoginWrapper.PerformLayout();
            }
        }

        private readonly List<ShiftLoginItem> _configuredShifts =
            new List<ShiftLoginItem>();

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
        public int SelectedShiftId =>
            GetSelectedShiftItem()?.ShiftId ?? 0;

        [DesignerSerializationVisibility(
            DesignerSerializationVisibility.Hidden)]
        public string SelectedShiftName =>
            GetSelectedShiftItem()?.ShiftName ??
            string.Empty;

        [DesignerSerializationVisibility(
            DesignerSerializationVisibility.Hidden)]
        public TimeSpan SelectedShiftStartTime =>
            GetSelectedShiftItem()?.StartTime ??
            TimeSpan.Zero;

        [DesignerSerializationVisibility(
            DesignerSerializationVisibility.Hidden)]
        public TimeSpan SelectedShiftEndTime =>
            GetSelectedShiftItem()?.EndTime ??
            TimeSpan.Zero;

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
                        .Select(g => g.First())
                        .OrderBy(x => x.StartTime)
                        .ThenBy(x => x.ShiftName));
            }

            comboBoxShiftLogin.BeginUpdate();

            try
            {
                comboBoxShiftLogin.Items.Clear();

                foreach (ShiftLoginItem shift
                         in _configuredShifts)
                {
                    comboBoxShiftLogin.Items.Add(
                        shift);
                }

                int selectedIndex = 0;

                if (preferredShiftId > 0)
                {
                    for (int index = 0;
                         index <
                         comboBoxShiftLogin.Items.Count;
                         index++)
                    {
                        if (comboBoxShiftLogin.Items[index]
                                is ShiftLoginItem item &&
                            item.ShiftId ==
                            preferredShiftId)
                        {
                            selectedIndex =
                                index;

                            break;
                        }
                    }
                }

                if (comboBoxShiftLogin.Items.Count > 0)
                {
                    comboBoxShiftLogin.SelectedIndex =
                        selectedIndex;
                }
            }
            finally
            {
                comboBoxShiftLogin.EndUpdate();
            }

            PanelLoginElementWrapper.GridRowCount =
                ShowShiftSelection ? 3 : 2;

            LoginShiftWrapper.Visible =
                ShowShiftSelection;

            // Do NOT resize/recenter here while startup is being prepared.
            // PrepareForFirstDisplay() handles the correct first size.
            if (_stage == StartupStage.Finished)
            {
                ApplyResponsiveScale();
            }

            PanelLoginElementWrapper.PerformLayout();
            LoginElementWrapper.PerformLayout();
            LoginWrapper.PerformLayout();
        }

        public bool TryGetSelectedShift(
            out ShiftLoginItem selectedShift)
        {
            ShiftLoginItem? item =
                GetSelectedShiftItem();

            if (item == null ||
                item.ShiftId <= 0 ||
                string.IsNullOrWhiteSpace(
                    item.ShiftName))
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

        public sealed class LoginSuccessEventArgs :
            EventArgs
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

            _usernameDebounceTimer =
                new Timer
                {
                    Interval = 250
                };

            _usernameDebounceTimer.Tick +=
                UsernameDebounceTimer_Tick;

            _buttonClose =
                CreateCloseButton();

            LoginWrapper.Controls.Add(
                _buttonClose);

            _buttonClose.BringToFront();

            Microsoft.Win32.SystemEvents
                .DisplaySettingsChanged +=
                SystemEvents_DisplaySettingsChanged;

            textBox2.UseSystemPasswordChar =
                true;

            button1.Text = "";

            button1.FlatStyle =
                FlatStyle.Flat;

            button1.FlatAppearance.BorderSize =
                0;

            button1.BackColor =
                Color.Transparent;

            button1.FlatAppearance
                .MouseOverBackColor =
                Color.Transparent;

            button1.FlatAppearance
                .MouseDownBackColor =
                Color.Transparent;

            button1.UseVisualStyleBackColor =
                false;

            button1.Cursor =
                Cursors.Hand;

            button1.Paint +=
                button1_Paint;

            checkBoxRememberMe.Visible =
                WantRememberMe;

            LoginShiftWrapper.Visible =
                false;

            if (string.IsNullOrEmpty(dbo))
                throw new ArgumentNullException(
                    nameof(dbo));

            if (string.IsNullOrEmpty(
                userTabelName))
            {
                throw new ArgumentNullException(
                    nameof(userTabelName));
            }

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

            // Use supplied dbo.
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

            // Same old wrapper behavior.
            if (LoginWrapper != null)
            {
                LoginWrapper.Dock =
                    DockStyle.Fill;
            }

            if (LoadingWrapper != null)
            {
                LoadingWrapper.Dock =
                    DockStyle.Fill;
            }

            // IMPORTANT:
            // Keep same animation starting loader width as backup.
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
                FormStartPosition.Manual;

            DoubleBuffered = true;

            // This gets replaced by PrepareForFirstDisplay()
            // before Application.Run().
            Width = _startWidth;
            Height =
                ResponsiveBaseHeightWithoutShift;

            // EXACT backup timer frequency.
            _timer =
                new Timer
                {
                    Interval = 15
                };

            _timer.Tick +=
                Timer_Tick;

            Shown +=
                FormStartUp_Shown;

            Load +=
                FormStartUp_Load;
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

        // IMPORTANT:
        // Restore the exact backup behavior.
        // This was present in the smooth backup code.
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp =
                    base.CreateParams;

                cp.ExStyle |=
                    0x02000000; // WS_EX_COMPOSITED

                return cp;
            }
        }

        /// <summary>
        /// Must be called AFTER ConfigureShiftSelection()
        /// and BEFORE Application.Run(loginForm).
        ///
        /// It only prepares the correct initial scale/size.
        /// It does NOT change the original animation logic.
        /// </summary>
        public void PrepareForFirstDisplay()
        {
            if (IsDisposed ||
                Disposing)
            {
                return;
            }

            Rectangle workArea =
                GetCurrentWorkingArea();

            int baseHeight =
                LoginShiftWrapper.Visible
                    ? ResponsiveBaseHeightWithShift
                    : ResponsiveBaseHeightWithoutShift;

            const int screenMargin = 24;

            float widthScale =
                (workArea.Width -
                 (screenMargin * 2)) /
                (float)ResponsiveBaseWidth;

            float heightScale =
                (workArea.Height -
                 (screenMargin * 2)) /
                (float)baseHeight;

            float desiredScale =
                Math.Min(
                    1.0f,
                    Math.Min(
                        widthScale,
                        heightScale));

            desiredScale =
                Math.Max(
                    0.55f,
                    desiredScale);

            SuspendLayout();

            try
            {
                if (Math.Abs(
                    desiredScale -
                    _currentUiScale) >
                    0.001f)
                {
                    float relativeScale =
                        desiredScale /
                        _currentUiScale;

                    Scale(
                        new SizeF(
                            relativeScale,
                            relativeScale));

                    _currentUiScale =
                        desiredScale;
                }

                _targetWidth =
                    (int)Math.Round(
                        ResponsiveBaseWidth *
                        desiredScale);

                _startWidth =
                    Math.Max(
                        120,
                        (int)Math.Round(
                            200 *
                            desiredScale));

                _panelTargetWidth =
                    Math.Max(
                        250,
                        (int)Math.Round(
                            500 *
                            desiredScale));

                int targetHeight =
                    (int)Math.Round(
                        baseHeight *
                        desiredScale);

                Width =
                    _startWidth;

                Height =
                    targetHeight;

                StartPosition =
                    FormStartPosition.Manual;

                Left =
                    workArea.Left +
                    ((workArea.Width -
                      Width) / 2);

                Top =
                    workArea.Top +
                    ((workArea.Height -
                      Height) / 2);

                ApplyRoundedCorners(
                    Math.Max(
                        30,
                        (int)Math.Round(
                            _cornerRadius *
                            desiredScale)));

                // Pre-correct the row metrics for this DPI/scale.
                // ShowLoginUi() will run it again after panel shrinking,
                // when the final login-side width is known.
                NormalizeLoginInputRows();

                _firstDisplayPrepared =
                    true;
            }
            finally
            {
                ResumeLayout(true);
            }
        }

        private Rectangle GetCurrentWorkingArea()
        {
            if (Owner != null)
            {
                return Screen
                    .FromControl(Owner)
                    .WorkingArea;
            }

            if (Screen.AllScreens.Length > 0)
            {
                Point cursorPosition =
                    System.Windows.Forms.Cursor.Position;

                return Screen
                    .FromPoint(cursorPosition)
                    .WorkingArea;
            }

            return Screen.PrimaryScreen
                       ?.WorkingArea ??
                   new Rectangle(
                       0,
                       0,
                       1920,
                       1080);
        }

        private void FormStartUp_Load(
            object? sender,
            EventArgs e)
        {
            if (!_firstDisplayPrepared)
            {
                PrepareForFirstDisplay();
            }

            loader =
                new AarohiLoadder
                {
                    Dock =
                        DockStyle.Fill,

                    BackColor =
                        Color.White,

                    // Same as smooth backup.
                    Size =
                        new Size(
                            Width,
                            1000),

                    ShowGuideLines =
                        false
                };

            if (!panelLoadder.Controls
                .Contains(loader))
            {
                panelLoadder.Controls.Add(
                    loader);
            }

            ApplyRoundedCorners(
                Math.Max(
                    30,
                    (int)Math.Round(
                        _cornerRadius *
                        _currentUiScale)));

            _stage =
                StartupStage.FormExpand;

            _stopwatch.Restart();
            _timer.Start();
        }

        private void FormStartUp_Shown(
            object? sender,
            EventArgs e)
        {
            Shown -=
                FormStartUp_Shown;

            // IMPORTANT:
            // Keep the backup behavior here too.
            // On the user's working backup this synchronous population
            // did not disturb animation and avoids extra cross-thread timing.
            string[] users =
                _userClass.GetColumnValues(
                    _LoginDataColumnName);

            _allUserNames.Clear();
            _allUserNames.AddRange(users);

            comboBoxUsername.Items.AddRange(
                users);

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
                // Keep existing behavior.
            }
        }

        #region Animation

        // ==========================================================
        // DO NOT "OPTIMIZE" THESE FOUR METHODS.
        // They deliberately follow the smooth backup implementation.
        // ==========================================================

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
                    elapsed /
                    _durationMs);

            double eased =
                EaseInOut(t);

            int newWidth =
                _startWidth +
                (int)(
                    (_targetWidth -
                     _startWidth) *
                    eased);

            // Exact backup-style center expansion.
            int centerX =
                Left +
                (Width / 2);

            Width =
                newWidth;

            Left =
                centerX -
                (Width / 2);

            // Exact backup behavior.
            ApplyRoundedCorners(
                Math.Max(
                    30,
                    (int)Math.Round(
                        _cornerRadius *
                        _currentUiScale)));

            if (!_loaderStarted &&
                t >=
                _loaderStartPercent)
            {
                _loaderStarted =
                    true;

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

                _panelShrinkInitialized =
                    false;
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
                     panelLoadder.Width) /
                    2;

                _panelShrinkInitialized =
                    true;
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
                (panelLoadder.Width / 2);

            panelLoadder.Width =
                newWidth;

            panelLoadder.Left =
                centerX -
                (panelLoadder.Width / 2);

            if (t >= 1.0)
            {
                _stage =
                    StartupStage.LoginFadeIn;

                _stopwatch.Restart();

                _loginFadeInitialized =
                    false;
            }
        }

        private void HandleLoginFadeIn()
        {
            if (!_loginFadeInitialized)
            {
                ShowLoginUi();

                LoginWrapper.GradientOpacity =
                    _loginStartOpacity;

                _loginFadeInitialized =
                    true;
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
            return
                0.5 -
                0.5 *
                Math.Cos(
                    Math.PI *
                    t);
        }

        private void ApplyRoundedCorners(
            int radius)
        {
            if (Width <= 0 ||
                Height <= 0)
            {
                return;
            }

            radius =
                Math.Max(
                    1,
                    Math.Min(
                        radius,
                        Math.Min(
                            Width,
                            Height)));

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

            Region =
                new Region(path);
        }

        private void StartLoaderAnimation()
        {
            if (loader == null)
                return;

            // Exact smooth-backup settings.
            loader.SetEasing(
                AarohiLoadder
                    .EasingType
                    .EaseOut);

            loader.fillRevealMode =
                AarohiLoadder
                    .RevealMode
                    .Radial;

            loader.SetFillTiming(
                AarohiLoadder
                    .FillTimingMode
                    .AfterStrokes,
                0.1);

            loader.SetFillEasing(
                AarohiLoadder
                    .EasingType
                    .EaseInOut);

            loader.SetFillOnTop(
                true);

            loader.SetGlobalDuration(
                2.0);

            loader.FadeStrokesAfterFill =
                true;

            loader.StrokeFadeDuration =
                0.9;

            loader.StrokeFadeDelay =
                0.2;

            loader.ReflectionSpeed =
                0.3;

            loader.ReflectionThickness =
                0.22;

            loader.ReflectionIntensity =
                0.15f;

            loader.ReflectionAngle =
                -30f;

            loader.StartAnimation();
            loader.StartReflection();
        }

        #endregion

        #region UI Switching

        private void ShowLoginUi()
        {
            LoadingWrapper.Visible =
                false;

            LoadingWrapper.Enabled =
                false;

            LoginWrapper.Visible =
                true;

            LoginWrapper.Enabled =
                true;

            LoginWrapper.BringToFront();

            // At this moment panel shrinking is complete and the login side
            // has its real final width. Correct only the three input rows.
            NormalizeLoginInputRows();

            if (_buttonClose.Visible)
            {
                _buttonClose
                    .BringToFront();
            }

            // Exact backup redraw behavior.
            Invalidate(true);
            Update();

            LoginWrapper
                .Invalidate(true);

            LoginWrapper
                .Refresh();
        }

        private void ShowLoadingUi()
        {
            LoginWrapper.Visible =
                false;

            LoginWrapper.Enabled =
                false;

            LoadingWrapper.Visible =
                true;

            LoadingWrapper.Enabled =
                true;

            LoadingWrapper.BringToFront();

            // Exact backup redraw behavior.
            Invalidate(true);
            Update();

            LoadingWrapper
                .Invalidate(true);

            LoadingWrapper
                .Refresh();
        }

        #endregion

        /// <summary>
        /// Keeps Username / Password / Shift rows visually consistent after
        /// responsive scaling. This does NOT change the animation or page layout.
        ///
        /// WinForms font rendering can round differently from Control.Scale(),
        /// especially on portrait/high-DPI screens. We therefore measure the
        /// actual rendered label text and give all three labels one common width,
        /// then use the remaining row width for the input controls.
        /// </summary>
        private void NormalizeLoginInputRows()
        {
            if (IsDisposed || Disposing)
                return;

            // Make sure the custom Flex/Grid containers have their current sizes.
            PanelLoginElementWrapper.PerformLayout();
            LoginUsernameWrapper.PerformLayout();
            extendedPanel1.PerformLayout();

            if (LoginShiftWrapper.Visible)
                LoginShiftWrapper.PerformLayout();

            int usernameTextWidth =
                TextRenderer.MeasureText(
                    labelUsername.Text,
                    labelUsername.Font).Width;

            int passwordTextWidth =
                TextRenderer.MeasureText(
                    labelPassword.Text,
                    labelPassword.Font).Width;

            int shiftTextWidth =
                TextRenderer.MeasureText(
                    labelShift.Text,
                    labelShift.Font).Width;

            // One common label width keeps every ":" and every input aligned.
            int commonLabelWidth =
                Math.Max(
                    usernameTextWidth,
                    Math.Max(
                        passwordTextWidth,
                        shiftTextWidth));

            int labelExtra =
                Math.Max(
                    8,
                    (int)Math.Round(
                        12 * _currentUiScale));

            commonLabelWidth +=
                labelExtra;

            // Never let scaling make the label narrower than its real text.
            labelUsername.AutoSize = false;
            labelPassword.AutoSize = false;

            labelUsername.Width =
                commonLabelWidth;

            labelPassword.Width =
                commonLabelWidth;

            labelUsername.TextAlign =
                ContentAlignment.MiddleLeft;

            labelPassword.TextAlign =
                ContentAlignment.MiddleLeft;

            if (LoginShiftWrapper.Visible)
            {
                labelShift.AutoSize = false;
                labelShift.Width =
                    commonLabelWidth;

                labelShift.TextAlign =
                    ContentAlignment.MiddleLeft;
            }

            int gap =
                Math.Max(
                    6,
                    (int)Math.Round(
                        10 * _currentUiScale));

            // -------------------------
            // USERNAME ROW
            // -------------------------
            int usernameUsableWidth =
                LoginUsernameWrapper.ClientSize.Width -
                LoginUsernameWrapper.Padding.Horizontal;

            if (usernameUsableWidth > 0)
            {
                int usernameInputWidth =
                    Math.Max(
                        120,
                        usernameUsableWidth -
                        commonLabelWidth -
                        gap);

                comboBoxUsername.Width =
                    usernameInputWidth;

                comboBoxUsername.DropDownWidth =
                    Math.Max(
                        comboBoxUsername.Width,
                        220);
            }

            // -------------------------
            // PASSWORD ROW
            // -------------------------
            int passwordUsableWidth =
                extendedPanel1.ClientSize.Width -
                extendedPanel1.Padding.Horizontal;

            if (passwordUsableWidth > 0)
            {
                // Keep the eye button compact, but never too small to click.
                int eyeButtonWidth =
                    Math.Max(
                        34,
                        (int)Math.Round(
                            48 * _currentUiScale));

                int passwordInputWidth =
                    Math.Max(
                        100,
                        passwordUsableWidth -
                        commonLabelWidth -
                        eyeButtonWidth -
                        (gap * 2));

                textBox2.Width =
                    passwordInputWidth;

                button1.Width =
                    eyeButtonWidth;

                // Keep the eye button height matched to the password TextBox.
                button1.Height =
                    Math.Max(
                        textBox2.Height,
                        30);
            }

            // -------------------------
            // SHIFT ROW
            // -------------------------
            if (LoginShiftWrapper.Visible)
            {
                int shiftUsableWidth =
                    LoginShiftWrapper.ClientSize.Width -
                    LoginShiftWrapper.Padding.Horizontal;

                if (shiftUsableWidth > 0)
                {
                    int shiftInputWidth =
                        Math.Max(
                            120,
                            shiftUsableWidth -
                            commonLabelWidth -
                            gap);

                    comboBoxShiftLogin.Width =
                        shiftInputWidth;

                    comboBoxShiftLogin.DropDownWidth =
                        Math.Max(
                            comboBoxShiftLogin.Width,
                            220);
                }
            }

            // Let the custom Flex containers recalculate positions once,
            // after all widths are corrected.
            LoginUsernameWrapper.PerformLayout();
            extendedPanel1.PerformLayout();

            if (LoginShiftWrapper.Visible)
                LoginShiftWrapper.PerformLayout();

            PanelLoginElementWrapper.PerformLayout();
        }

        #region Optional Close Button / Responsive

        private Button CreateCloseButton()
        {
            Button button =
                new Button
                {
                    Name =
                        "buttonClose",

                    Text =
                        "×",

                    Size =
                        new Size(
                            32,
                            30),

                    Anchor =
                        AnchorStyles.Top |
                        AnchorStyles.Right,

                    Location =
                        new Point(
                            Math.Max(
                                0,
                                LoginWrapper
                                    .ClientSize
                                    .Width -
                                48),
                            10),

                    BackColor =
                        Color.Transparent,

                    ForeColor =
                        Color.FromArgb(
                            45,
                            45,
                            45),

                    FlatStyle =
                        FlatStyle.Flat,

                    Font =
                        new Font(
                            "Segoe UI",
                            11F,
                            FontStyle.Bold,
                            GraphicsUnit.Point,
                            0),

                    Cursor =
                        Cursors.Hand,

                    TabStop =
                        false,

                    UseVisualStyleBackColor =
                        false,

                    Visible =
                        false
                };

            button.FlatAppearance.BorderSize =
                0;

            button.FlatAppearance
                .MouseOverBackColor =
                Color.Transparent;

            button.FlatAppearance
                .MouseDownBackColor =
                Color.Transparent;

            button.FlatAppearance
                .CheckedBackColor =
                Color.Transparent;

            button.Click +=
                (_, _) =>
                    Close();

            return button;
        }

        private void ApplyResponsiveScale()
        {
            if (IsDisposed ||
                Disposing ||
                _applyingResponsiveScale ||
                _stage !=
                StartupStage.Finished)
            {
                return;
            }

            try
            {
                _applyingResponsiveScale =
                    true;

                Rectangle workArea =
                    Screen
                        .FromControl(this)
                        .WorkingArea;

                int baseHeight =
                    LoginShiftWrapper.Visible
                        ? ResponsiveBaseHeightWithShift
                        : ResponsiveBaseHeightWithoutShift;

                const int screenMargin =
                    24;

                float widthScale =
                    (workArea.Width -
                     (screenMargin * 2)) /
                    (float)
                    ResponsiveBaseWidth;

                float heightScale =
                    (workArea.Height -
                     (screenMargin * 2)) /
                    (float)
                    baseHeight;

                float desiredScale =
                    Math.Min(
                        1.0f,
                        Math.Min(
                            widthScale,
                            heightScale));

                desiredScale =
                    Math.Max(
                        0.55f,
                        desiredScale);

                float relativeScale =
                    desiredScale /
                    _currentUiScale;

                if (Math.Abs(
                    relativeScale -
                    1.0f) >
                    0.001f)
                {
                    SuspendLayout();

                    Scale(
                        new SizeF(
                            relativeScale,
                            relativeScale));

                    ResumeLayout(true);

                    _currentUiScale =
                        desiredScale;
                }

                _targetWidth =
                    (int)Math.Round(
                        ResponsiveBaseWidth *
                        desiredScale);

                _startWidth =
                    Math.Max(
                        120,
                        (int)Math.Round(
                            200 *
                            desiredScale));

                _panelTargetWidth =
                    Math.Max(
                        250,
                        (int)Math.Round(
                            500 *
                            desiredScale));

                Size =
                    new Size(
                        _targetWidth,
                        (int)Math.Round(
                            baseHeight *
                            desiredScale));

                StartPosition =
                    FormStartPosition.Manual;

                Left =
                    workArea.Left +
                    ((workArea.Width -
                      Width) /
                     2);

                Top =
                    workArea.Top +
                    ((workArea.Height -
                      Height) /
                     2);

                ApplyRoundedCorners(
                    Math.Max(
                        30,
                        (int)Math.Round(
                            _cornerRadius *
                            desiredScale)));

                _buttonClose
                    .BringToFront();

                PanelMainWrapperBorder
                    .PerformLayout();

                PanelMainWrapper
                    .PerformLayout();

                PanelForm
                    .PerformLayout();

                LoginWrapper
                    .PerformLayout();

                LoginElementWrapper
                    .PerformLayout();

                PanelLoginElementWrapper
                    .PerformLayout();

                NormalizeLoginInputRows();

                Invalidate(true);
            }
            finally
            {
                _applyingResponsiveScale =
                    false;
            }
        }

        private void SystemEvents_DisplaySettingsChanged(
            object? sender,
            EventArgs e)
        {
            if (IsDisposed ||
                Disposing)
            {
                return;
            }

            if (InvokeRequired)
            {
                BeginInvoke(
                    new Action(
                        ApplyResponsiveScale));

                return;
            }

            ApplyResponsiveScale();
        }

        protected override void OnFormClosed(
            FormClosedEventArgs e)
        {
            Microsoft.Win32.SystemEvents
                .DisplaySettingsChanged -=
                SystemEvents_DisplaySettingsChanged;

            _timer.Stop();
            _usernameDebounceTimer.Stop();

            base.OnFormClosed(e);
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
                userName !=
                AGLobals.Utils.DevName)
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

            _loginFlowRunning =
                true;

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

                    if (File.Exists(
                        LoginInfoPath))
                    {
                        File.Delete(
                            LoginInfoPath);
                    }
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

                Dictionary<string, object>
                    values =
                        _userClass
                            .GetRowAsDictionary(
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
                    values[
                        _LoginDataColumnName]
                        ?.ToString() ??
                    string.Empty;

                string dbPassword =
                    values[
                        _PasswordDataColumnName]
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
                if (!File.Exists(
                    LoginInfoPath))
                {
                    return false;
                }

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

                if (string.IsNullOrWhiteSpace(
                        realName) ||
                    string.IsNullOrWhiteSpace(
                        realPassword))
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
                    File.Delete(
                        LoginInfoPath);

                    return false;
                }

                if (_loginFlowRunning)
                    return true;

                _loginFlowRunning =
                    true;

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
                    File.Delete(
                        LoginInfoPath);
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

            if (!string.IsNullOrEmpty(
                    folder) &&
                !Directory.Exists(
                    folder))
            {
                Directory.CreateDirectory(
                    folder);
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
                .Replace(
                    "-",
                    "");

            byte[] passwordBytes =
                Encoding.UTF8.GetBytes(
                    password ??
                    string.Empty);

            string passwordHash =
                BitConverter.ToString(
                    sha.ComputeHash(
                        passwordBytes))
                .Replace(
                    "-",
                    "");

            RegistryHelper.SaveString(
                RegistryHelper
                    .storeLocs
                    .Credentials,
                "AESPLXU",
                userHash);

            RegistryHelper.SaveString(
                RegistryHelper
                    .storeLocs
                    .Credentials,
                "AESPLXP",
                passwordHash);
        }

        public void ResetLoginTrigger()
        {
            _loginFlowRunning =
                false;
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

            progressBar1.Minimum =
                0;

            progressBar1.Maximum =
                100;

            progressBar1.Value =
                0;

            lblStatus.Text =
                "Starting...";

            Progress<StartupProgress>
                progress =
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
                await loadFunc(
                    progress);

                progressBar1.Value =
                    100;

                lblStatus.Text =
                    "Done.";

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

                _loginFlowRunning =
                    false;

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

        private void LoadingWrapper_Paint(
            object sender,
            PaintEventArgs e)
        {
        }

        private void label1_Click(
            object sender,
            EventArgs e)
        {
        }

        private void PanelLoginElementWrapper_Paint(
            object sender,
            PaintEventArgs e)
        {
        }

        private void button1_Click(
            object sender,
            EventArgs e)
        {
            _isPasswordVisible =
                !_isPasswordVisible;

            textBox2.UseSystemPasswordChar =
                !_isPasswordVisible;

            button1.Invalidate();
        }

        private void textBox2_KeyDown(
            object sender,
            KeyEventArgs e)
        {
            if (e.KeyCode ==
                Keys.Enter)
            {
                e.SuppressKeyPress =
                    true;

                LoginButton_Click(
                    LoginButton,
                    EventArgs.Empty);
            }
        }

        private void comboBoxShiftLogin_KeyDown(
            object sender,
            KeyEventArgs e)
        {
            if (e.KeyCode ==
                Keys.Enter)
            {
                e.SuppressKeyPress =
                    true;

                LoginButton_Click(
                    LoginButton,
                    EventArgs.Empty);
            }
        }

        private void comboBoxUsername_SelectedIndexChanged(
            object sender,
            EventArgs e)
        {
            textBox2.Text =
                string.Empty;
        }

        private void button1_Paint(
            object sender,
            PaintEventArgs e)
        {
            e.Graphics.SmoothingMode =
                SmoothingMode.AntiAlias;

            int width =
                button1.ClientSize.Width;

            int height =
                button1.ClientSize.Height;

            float cx =
                width / 2f;

            float cy =
                height / 2f;

            using Pen pen =
                new Pen(
                    Color.FromArgb(
                        45,
                        50,
                        65),
                    2.2f);

            using SolidBrush brush =
                new SolidBrush(
                    Color.FromArgb(
                        45,
                        50,
                        65));

            RectangleF eyeRect =
                new RectangleF(
                    cx - 11,
                    cy - 7,
                    22,
                    14);

            using GraphicsPath eyePath =
                new GraphicsPath();

            eyePath.AddBezier(
                eyeRect.Left,
                cy,
                cx - 6,
                cy - 8,
                cx + 6,
                cy - 8,
                eyeRect.Right,
                cy);

            eyePath.AddBezier(
                eyeRect.Right,
                cy,
                cx + 6,
                cy + 8,
                cx - 6,
                cy + 8,
                eyeRect.Left,
                cy);

            e.Graphics.DrawPath(
                pen,
                eyePath);

            e.Graphics.FillEllipse(
                brush,
                cx - 3,
                cy - 3,
                6,
                6);

            if (_isPasswordVisible)
            {
                using Pen slashPen =
                    new Pen(
                        Color.FromArgb(
                            45,
                            50,
                            65),
                        2.5f)
                    {
                        StartCap =
                            LineCap.Round,

                        EndCap =
                            LineCap.Round
                    };

                e.Graphics.DrawLine(
                    slashPen,
                    cx - 12,
                    cy - 10,
                    cx + 12,
                    cy + 10);
            }
        }

        private void comboBoxUsername_TextUpdate(
            object sender,
            EventArgs e)
        {
            _usernameDebounceTimer.Stop();
            _usernameDebounceTimer.Start();
        }

        private void UsernameDebounceTimer_Tick(
            object? sender,
            EventArgs e)
        {
            _usernameDebounceTimer.Stop();

            string enteredText =
                comboBoxUsername.Text;

            List<string> matchedUsers =
                _allUserNames
                    .Where(
                        x =>
                            x.StartsWith(
                                enteredText,
                                StringComparison
                                    .OrdinalIgnoreCase))
                    .ToList();

            comboBoxUsername.BeginUpdate();

            try
            {
                comboBoxUsername.Items.Clear();

                comboBoxUsername.Items.AddRange(
                    matchedUsers
                        .Cast<object>()
                        .ToArray());
            }
            finally
            {
                comboBoxUsername.EndUpdate();
            }

            comboBoxUsername.Cursor =
                Cursors.Default;

            comboBoxUsername.Text =
                enteredText;

            comboBoxUsername.SelectionStart =
                enteredText.Length;

            comboBoxUsername.SelectionLength =
                0;

            comboBoxUsername.DroppedDown =
                matchedUsers.Count > 0;
        }
    }
}
