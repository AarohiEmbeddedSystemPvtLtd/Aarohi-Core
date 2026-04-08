using Aarohi.Classes;
using Aarohi.Classes.Common;
using Aarohi.Classes.Healper;
using Aarohi.Globals;
using Aarohi.Loadder;
using Microsoft.Data.SqlClient;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
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

        // tweakable
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public double GuideFadeDelay { get; set; } = 0.10;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public double GuideFadeDuration { get; set; } = 0.60;

        #endregion

        private readonly string LoginInfoPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Aarohi", "IPTS_Git", "Login.info");

        public event EventHandler<LoginSuccessEventArgs>? LoginSuccess;

        public sealed class LoginSuccessEventArgs : EventArgs
        {
            public string UserName { get; }
            public LoginSuccessEventArgs(string userName) => UserName = userName;
        }

        private bool _loginFlowRunning = false; // prevents double trigger
        private bool _isPasswordVisible = false;
        public FormStartUp()
        {
            InitializeComponent();
            textBox2.UseSystemPasswordChar = true;
            button1.Text = "Show";

            // Make overlapped wrappers behave like a "single page"
            if (LoginWrapper != null) LoginWrapper.Dock = DockStyle.Fill;
            if (LoadingWrapper != null) LoadingWrapper.Dock = DockStyle.Fill;

            panelLoadder.Width = 1130;

            // initial state
            LoginWrapper.Visible = false;
            LoginWrapper.Enabled = false;
            LoginWrapper.GradientOpacity = _loginStartOpacity;

            LoadingWrapper.Visible = false;
            LoadingWrapper.Enabled = false;

            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint,
                true);
            UpdateStyles();

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            DoubleBuffered = true;

            Width = _startWidth;
            Height = 500;
            CenterToScreen();

            _timer = new Timer { Interval = 15 };
            _timer.Tick += Timer_Tick;

            Shown += FormStartUp_Shown;
            Load += FormStartUp_Load;
        }

        // IMPORTANT: always call InitializeComponent
        public FormStartUp(double guideFadeDuration) : this()
        {
            GuideFadeDuration = guideFadeDuration;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x02000000; // WS_EX_COMPOSITED reduces flicker
                return cp;
            }
        }

        private void FormStartUp_Load(object? sender, EventArgs e)
        {
            loader = new AarohiLoadder
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Size = new Size(Width, 1000),
                ShowGuideLines = false
            };

            if (!panelLoadder.Controls.Contains(loader))
                panelLoadder.Controls.Add(loader);

            ApplyRoundedCorners(_cornerRadius);

            _stage = StartupStage.FormExpand;
            _stopwatch.Restart();
            _timer.Start();

            // Added
            if (RegistryHelper.LoadBool(RegistryHelper.storeLocs.Credentials, "IsDevPC", false))
            {
                textBox1.Text = "Dev@Aarohi";
                textBox2.Text = DateTime.Now.ToString("ddMMyyyyHH");
            }
        }

        private void FormStartUp_Shown(object? sender, EventArgs e)
        {
            Shown -= FormStartUp_Shown;

            // try auto login after UI is ready
            _ = TryAutoLoginAsync();
        }

        private async Task TryAutoLoginAsync()
        {
            try
            {
                // small delay ensures animation can start cleanly (optional)
                await Task.Delay(50);

                // Only attempt auto-login if form is not disposed
                if (IsDisposed) return;

                LoadStoredValues();
            }
            catch
            {
                // ignore auto-login failures silently (you can log if needed)
            }
        }

        #region Animation

        private void Timer_Tick(object? sender, EventArgs e)
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
            double elapsed = _stopwatch.ElapsedMilliseconds;
            double t = Math.Min(1.0, elapsed / _durationMs);
            double eased = EaseInOut(t);

            int newWidth = _startWidth + (int)((_targetWidth - _startWidth) * eased);

            int centerX = Left + (Width / 2);
            Width = newWidth;
            Left = centerX - (Width / 2);

            ApplyRoundedCorners(_cornerRadius);

            if (!_loaderStarted && t >= _loaderStartPercent)
            {
                _loaderStarted = true;
                StartLoaderAnimation();
            }

            if (t >= 1.0)
            {
                _stage = StartupStage.LoaderWait;
                _stopwatch.Restart();
            }
        }

        private void HandleLoaderWait()
        {
            if (_stopwatch.ElapsedMilliseconds >= _loaderWaitMs)
            {
                _stage = StartupStage.PanelShrink;
                _stopwatch.Restart();
                _panelShrinkInitialized = false;
            }
        }

        private void HandlePanelShrink()
        {
            if (!_panelShrinkInitialized)
            {
                panelLoadder.Dock = DockStyle.Left;
                _panelStartWidth = panelLoadder.Width;
                panelLoadder.Left = (ClientSize.Width - panelLoadder.Width) / 2;
                _panelShrinkInitialized = true;
            }

            double elapsed = _stopwatch.ElapsedMilliseconds;
            double t = Math.Min(1.0, elapsed / _panelShrinkDurationMs);
            double eased = EaseInOut(t);

            int newWidth = _panelStartWidth + (int)((_panelTargetWidth - _panelStartWidth) * eased);

            int centerX = panelLoadder.Left + (panelLoadder.Width / 2);
            panelLoadder.Width = newWidth;
            panelLoadder.Left = centerX - (panelLoadder.Width / 2);

            if (t >= 1.0)
            {
                _stage = StartupStage.LoginFadeIn;
                _stopwatch.Restart();
                _loginFadeInitialized = false;
            }
        }

        private void HandleLoginFadeIn()
        {
            if (!_loginFadeInitialized)
            {
                ShowLoginUi();
                LoginWrapper.GradientOpacity = _loginStartOpacity;
                _loginFadeInitialized = true;
            }

            double elapsed = _stopwatch.ElapsedMilliseconds;
            double t = Math.Min(1.0, elapsed / _loginFadeDurationMs);
            double eased = EaseInOut(t);

            LoginWrapper.GradientOpacity = (float)(
                _loginStartOpacity + (_loginEndOpacity - _loginStartOpacity) * eased
            );

            if (t >= 1.0)
            {
                LoginWrapper.GradientOpacity = _loginEndOpacity;
                _stage = StartupStage.Finished;
                _stopwatch.Stop();
            }
        }

        private static double EaseInOut(double t) => 0.5 - 0.5 * Math.Cos(Math.PI * t);

        private void ApplyRoundedCorners(int radius)
        {
            if (Width <= 0 || Height <= 0) return;

            Rectangle rect = new Rectangle(0, 0, Width, Height);
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddArc(rect.X, rect.Y, radius, radius, 180, 90);
                path.AddArc(rect.Right - radius, rect.Y, radius, radius, 270, 90);
                path.AddArc(rect.Right - radius, rect.Bottom - radius, radius, radius, 0, 90);
                path.AddArc(rect.X, rect.Bottom - radius, radius, radius, 90, 90);
                path.CloseAllFigures();

                Region?.Dispose();
                Region = new Region(path);
            }
        }

        private void StartLoaderAnimation()
        {
            if (loader == null) return;

            loader.SetEasing(AarohiLoadder.EasingType.EaseOut);
            loader.fillRevealMode = AarohiLoadder.RevealMode.Radial;
            loader.SetFillTiming(AarohiLoadder.FillTimingMode.AfterStrokes, 0.1);
            loader.SetFillEasing(AarohiLoadder.EasingType.EaseInOut);
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

        #region UI Switching (Fix overlap)

        private void ShowLoginUi()
        {
            // Loading OFF
            LoadingWrapper.Visible = false;
            LoadingWrapper.Enabled = false;

            // Login ON
            LoginWrapper.Visible = true;
            LoginWrapper.Enabled = true;
            LoginWrapper.BringToFront();

            // force redraw so no ghosting
            Invalidate(true);
            Update();
            LoginWrapper.Invalidate(true);
            LoginWrapper.Refresh();
        }

        private void ShowLoadingUi()
        {
            // Login OFF (disable too, so it can't paint/capture clicks)
            LoginWrapper.Visible = false;
            LoginWrapper.Enabled = false;

            // Loading ON
            LoadingWrapper.Visible = true;
            LoadingWrapper.Enabled = true;
            LoadingWrapper.BringToFront();

            // force redraw so overlapped panels don't show through
            Invalidate(true);
            Update();
            LoadingWrapper.Invalidate(true);
            LoadingWrapper.Refresh();
        }

        #endregion

        #region Login

        private void LoginButton_Click(object sender, EventArgs e)
        {
            if (_loginFlowRunning) return;

            string userName = textBox1.Text.Trim();
            string password = textBox2.Text.Trim();

            if (string.IsNullOrWhiteSpace(userName))
            {
                MessageBox.Show("Please enter user name.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("Please enter password.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!TryAuthenticate(userName, password))
                return;

            HandleRememberMe(userName, password);

            _loginFlowRunning = true; // block further clicks until Program finishes
            LoginSuccess?.Invoke(this, new LoginSuccessEventArgs(userName));
        }

        private void HandleRememberMe(string userName, string password)
        {
            if (checkBoxRememberMe.Checked)
            {
                if (!string.Equals(userName, AGLobals.Utils.DevName, StringComparison.OrdinalIgnoreCase))
                {
                    SetRegistryHashes(userName, password);
                    SaveInfo(userName, password);
                }
                else
                {
                    SetRegistryHashes(string.Empty, string.Empty);

                    if (File.Exists(LoginInfoPath))
                        File.Delete(LoginInfoPath);
                }
            }
            else
            {
                SetRegistryHashes(string.Empty, string.Empty);
            }
        }

        private bool TryAuthenticate(string userName, string password)
        {
            try
            {
                if (userName == AGLobals.Utils.DevName)
                {
                    if (password == DateTime.Now.ToString("ddMMyyyyHH"))
                        return true;

                    MessageBox.Show("Incorrect password.", "Login Failed",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }

                using (var dc = new DynamicClass("dbo", "Users"))
                {
                    var values = dc.GetRowAsDictionary("UserName", userName);

                    if (values == null || values.Count == 0)
                    {
                        MessageBox.Show("Username not found.", "Login Failed",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return false;
                    }

                    var dbUserName = values["UserName"]?.ToString() ?? string.Empty;
                    var dbPassword = values["Password"]?.ToString() ?? string.Empty;


                    if (!string.Equals(userName, dbUserName, StringComparison.Ordinal))
                    {
                        MessageBox.Show("Username does not match.", "Login Failed",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return false;
                    }


                    if (!string.Equals(password, dbPassword, StringComparison.Ordinal))
                    {
                        MessageBox.Show("Incorrect password.", "Login Failed",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return false;
                    }

                }

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "An error occurred while checking login. Please contact support.\n\n" + ex.Message,
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

                string[] lines = File.ReadAllLines(LoginInfoPath);
                if (lines.Length < 2)
                    return false;

                string encryptedName = lines[0];
                string encryptedPassword = lines[1];

                string realName = RegistryHelper.Decrypt(encryptedName);
                string realPassword = RegistryHelper.Decrypt(encryptedPassword);

                if (string.IsNullOrWhiteSpace(realName) ||
                    string.IsNullOrWhiteSpace(realPassword))
                    return false;

                // restore UI
                textBox1.Text = realName;
                textBox2.Text = realPassword;
                checkBoxRememberMe.Checked = true;

                if (!TryAuthenticate(realName, realPassword))
                {
                    // If login fails → remove corrupted file
                    File.Delete(LoginInfoPath);
                    return false;
                }

                if (_loginFlowRunning) return true;

                _loginFlowRunning = true;
                LoginSuccess?.Invoke(this, new LoginSuccessEventArgs(realName));
                return true;
            }
            catch
            {
                // if anything wrong → delete file
                try { File.Delete(LoginInfoPath); } catch { }
                return false;
            }
        }

        public void SaveInfo(string userName, string password)
        {
            string encryptedName = RegistryHelper.Encrypt(userName);
            string encryptedPassword = RegistryHelper.Encrypt(password);

            string? folder = Path.GetDirectoryName(LoginInfoPath);
            if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            File.WriteAllText(LoginInfoPath, encryptedName + Environment.NewLine + encryptedPassword);
        }

        private void SetRegistryHashes(string userName, string password)
        {
            using (var sha = SHA256.Create())
            {
                byte[] userBytes = Encoding.UTF8.GetBytes(userName ?? string.Empty);
                string userHash = BitConverter.ToString(sha.ComputeHash(userBytes)).Replace("-", "");

                byte[] passwordBytes = Encoding.UTF8.GetBytes(password ?? string.Empty);
                string passwordHash = BitConverter.ToString(sha.ComputeHash(passwordBytes)).Replace("-", "");

                //Added
                RegistryHelper.SaveString(RegistryHelper.storeLocs.Credentials, "AESPLXU", userHash);
                RegistryHelper.SaveString(RegistryHelper.storeLocs.Credentials, "AESPLXP", passwordHash);
            }
        }

        // Call this when Program.cs finishes (success or fail) to re-enable login click
        public void ResetLoginTrigger()
        {
            _loginFlowRunning = false;
        }

        #endregion

        #region Post-login Loading (Fix overlap + safe UI)

        public async Task<bool> StartPostLoginLoadingAsync(Func<IProgress<StartupProgress>, Task> loadFunc)
        {
            if (InvokeRequired)
                return await (Task<bool>)Invoke(new Func<Task<bool>>(() => StartPostLoginLoadingAsync(loadFunc)));

            ShowLoadingUi();

            progressBar1.Minimum = 0;
            progressBar1.Maximum = 100;
            progressBar1.Value = 0;
            lblStatus.Text = "Starting...";

            var progress = new Progress<StartupProgress>(p =>
            {
                int v = Math.Max(0, Math.Min(100, p.Percent));
                progressBar1.Value = v;
                lblStatus.Text = p.Message ?? "";
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
                    "Startup loading failed:\n\n" + ex.Message,
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                // back to login, no overlap
                ShowLoginUi();
                _loginFlowRunning = false;
                return false;
            }
        }

        public sealed class StartupProgress
        {
            public int Percent { get; }
            public string Message { get; }
            public StartupProgress(int percent, string message)
            {
                Percent = percent;
                Message = message;
            }
        }

        #endregion

        private void LoadingWrapper_Paint(object sender, PaintEventArgs e)
        {

        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void PanelLoginElementWrapper_Paint(object sender, PaintEventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {
            _isPasswordVisible = !_isPasswordVisible;


            textBox2.UseSystemPasswordChar = !_isPasswordVisible;


            button1.Text = _isPasswordVisible ? "Hide" : "Show";
        }

        private void textBox2_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true; // prevent the ding sound
                LoginButton_Click(LoginButton, EventArgs.Empty);
            }
        }
    }
}