
using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Aarohi.Classes
{
    public partial class DualTextbox : UserControl
    {
        #region Fields

        private string _prefixValue = string.Empty;
        private string _suffixValue = string.Empty;
        private string _splitter = string.Empty;

        private int _collapsedHeight = 45;
        private int _expandedHeight = 45;

        private readonly Color _bgColor = Color.White;
        private readonly Color _borderNormal = Color.FromArgb(220, 220, 220);
        private readonly Color _borderFocused = Color.FromArgb(0, 122, 204);
        private readonly Color _textColor = Color.FromArgb(40, 40, 40);
        private readonly Color _dividerColor = Color.FromArgb(220, 220, 220);

        private bool _isExpanded;
        private bool _isFocused;
        private bool _isInternalUpdate;
        private bool _eventsAttached;

        private readonly Timer _collapseTimer;

        #endregion

        #region Constructor

        public DualTextbox()
        {
            InitializeComponent();

            _collapseTimer = new Timer();
            _collapseTimer.Interval = 150;
            _collapseTimer.Tick += CollapseTimer_Tick;

            ApplyStyling();
            SetupEvents();
            AdjustLayout();
            CollapseInternal(force: true);
        }

        #endregion

        #region Public Properties

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string FullText
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_splitter))
                    return $"{_prefixValue}{_suffixValue}".Trim();

                if (string.IsNullOrWhiteSpace(_prefixValue))
                    return _suffixValue ?? string.Empty;

                if (string.IsNullOrWhiteSpace(_suffixValue))
                    return _prefixValue ?? string.Empty;

                return $"{_prefixValue}{_splitter}{_suffixValue}";
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Prefix
        {
            get => _prefixValue;
            set
            {
                _prefixValue = value ?? string.Empty;
                SyncUiFromValues();
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Suffix
        {
            get => _suffixValue;
            set
            {
                _suffixValue = value ?? string.Empty;
                SyncUiFromValues();
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Splitter
        {
            get => _splitter;
            set
            {
                _splitter = value ?? string.Empty;
                SyncUiFromValues();
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string PrefixPlaceholder
        {
            get => textBoxPrefix.PlaceholderText;
            set => textBoxPrefix.PlaceholderText = value ?? string.Empty;
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string SuffixPlaceholder
        {
            get => textBoxSuffix.PlaceholderText;
            set => textBoxSuffix.PlaceholderText = value ?? string.Empty;
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Font PrefixFont
        {
            get => textBoxPrefix.Font;
            set
            {
                if (value == null) return;
                textBoxPrefix.Font = value;
                AdjustLayout();
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Font SuffixFont
        {
            get => textBoxSuffix.Font;
            set
            {
                if (value == null) return;
                textBoxSuffix.Font = value;
                AdjustLayout();
            }
        }

        [DefaultValue(true)]
        public bool AutoCollapse { get; set; } = true;

        [DefaultValue(150)]
        public int CollapseDelay
        {
            get => _collapseTimer.Interval;
            set => _collapseTimer.Interval = Math.Max(1, value);
        }

        [Browsable(false)]
        public bool IsExpanded => _isExpanded;

        #endregion

        #region Public Events

        public event EventHandler? ValueChanged;
        public event EventHandler? Expanded;
        public event EventHandler? Collapsed;

        #endregion

        #region Styling

        private void ApplyStyling()
        {
            BackColor = _bgColor;
            DoubleBuffered = true;

            if (panel1 != null)
                panel1.BackColor = _bgColor;

            foreach (var tb in new[] { textBoxPrefix, textBoxSuffix })
            {
                if (tb == null) continue;

                tb.BackColor = _bgColor;
                tb.ForeColor = _textColor;
                tb.BorderStyle = BorderStyle.None;
                tb.Font = new Font("Segoe UI", 11F, FontStyle.Regular);
            }

            if (divider != null)
                divider.BackColor = _dividerColor;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = new Rectangle(1, 1, Width - 2, Height - 2);
            using var pen = new Pen(_isFocused ? _borderFocused : _borderNormal, 1.5f);
            using var path = CreateRoundedPath(rect, 4);
            e.Graphics.DrawPath(pen, path);
        }

        private static GraphicsPath CreateRoundedPath(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            int diameter = radius * 2;

            if (diameter > rect.Width) diameter = rect.Width;
            if (diameter > rect.Height) diameter = rect.Height;

            var arc = new Rectangle(rect.Location, new Size(diameter, diameter));

            path.AddArc(arc, 180, 90);
            arc.X = rect.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = rect.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = rect.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();

            return path;
        }

        #endregion

        #region Layout

        private void AdjustLayout()
        {
            if (textBoxPrefix == null || textBoxSuffix == null)
                return;

            int padH = 10;
            int padV = 6;
            int gap = 1;

            int tbH = Math.Max(textBoxPrefix.PreferredHeight, textBoxSuffix.PreferredHeight);
            int totalW = Math.Max(Width, 120);
            int innerW = Math.Max(20, totalW - (padH * 2));
            int halfW = Math.Max(10, (innerW - gap) / 2);

            textBoxPrefix.Height = tbH;
            textBoxSuffix.Height = tbH;

            if (_isExpanded)
            {
                textBoxPrefix.Location = new Point(padH, padV);
                textBoxPrefix.Size = new Size(halfW, tbH);

                divider.Location = new Point(padH + halfW, padV);
                divider.Size = new Size(gap, tbH);
                divider.Visible = true;

                int rightX = padH + halfW + gap + 4;
                int rightW = Math.Max(10, innerW - halfW - gap - 4);
                textBoxSuffix.Location = new Point(rightX, padV);
                textBoxSuffix.Size = new Size(rightW, tbH);
                textBoxSuffix.Visible = true;

                _expandedHeight = padV + tbH + padV;
                ApplyHeight(_expandedHeight);
            }
            else
            {
                textBoxPrefix.Location = new Point(padH, padV);
                textBoxPrefix.Size = new Size(innerW, tbH);

                textBoxSuffix.Visible = false;
                divider.Visible = false;

                _collapsedHeight = padV + tbH + padV;
                _expandedHeight = _collapsedHeight;
                ApplyHeight(_collapsedHeight);
            }

            Invalidate();
        }

        private void ApplyHeight(int height)
        {
            Height = height;

            if (panel1 != null)
                panel1.Height = height;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            AdjustLayout();
        }

        #endregion

        #region Expand / Collapse

        public void Expand()
        {
            ExpandInternal();
        }

        public void Collapse()
        {
            CollapseInternal(force: true);
        }

        private void ExpandInternal()
        {
            if (_isExpanded)
            {
                SetFocusState(true);
                return;
            }

            _collapseTimer.Stop();
            _isExpanded = true;

            _isInternalUpdate = true;
            try
            {
                textBoxPrefix.Text = _prefixValue;
                textBoxSuffix.Text = _suffixValue;
            }
            finally
            {
                _isInternalUpdate = false;
            }

            textBoxSuffix.Visible = true;
            SetFocusState(true);
            AdjustLayout();
            Expanded?.Invoke(this, EventArgs.Empty);
        }

        private void CollapseInternal(bool force = false)
        {
            if (!force && !AutoCollapse)
                return;

            ReadCurrentTextboxValues();

            _isExpanded = false;

            _isInternalUpdate = true;
            try
            {
                textBoxPrefix.Text = FullText;
            }
            finally
            {
                _isInternalUpdate = false;
            }

            textBoxSuffix.Visible = false;
            divider.Visible = false;
            SetFocusState(false);
            AdjustLayout();
            Collapsed?.Invoke(this, EventArgs.Empty);
        }

        private void StartCollapseTimer()
        {
            if (!AutoCollapse)
                return;

            _collapseTimer.Stop();
            _collapseTimer.Start();
        }

        private void CollapseTimer_Tick(object? sender, EventArgs e)
        {
            _collapseTimer.Stop();

            if (IsDisposed || Disposing)
                return;

            if (!ContainsFocus)
                CollapseInternal();
        }

        private void SetFocusState(bool focused)
        {
            _isFocused = focused;
            Invalidate();
        }

        #endregion

        #region Event Wiring

        private void SetupEvents()
        {
            if (_eventsAttached)
                return;

            _eventsAttached = true;

            textBoxPrefix.Enter += TextBox_Enter;
            textBoxSuffix.Enter += TextBox_Enter;

            textBoxPrefix.Leave += TextBox_Leave;
            textBoxSuffix.Leave += TextBox_Leave;

            textBoxPrefix.TextChanged += TextBox_TextChanged;
            textBoxSuffix.TextChanged += TextBox_TextChanged;

            textBoxPrefix.KeyDown += TextBoxPrefix_KeyDown;
            textBoxSuffix.KeyDown += TextBoxSuffix_KeyDown;

            Enter += DualTextbox_Enter;
            Leave += DualTextbox_Leave;
        }

        private void DualTextbox_Enter(object? sender, EventArgs e)
        {
            SetFocusState(true);
        }

        private void DualTextbox_Leave(object? sender, EventArgs e)
        {
            StartCollapseTimer();
        }

        private void TextBox_Enter(object? sender, EventArgs e)
        {
            ExpandInternal();
        }

        private void TextBox_Leave(object? sender, EventArgs e)
        {
            StartCollapseTimer();
        }

        private void TextBox_TextChanged(object? sender, EventArgs e)
        {
            if (_isInternalUpdate)
                return;

            if (_isExpanded)
                ReadCurrentTextboxValues();

            ValueChanged?.Invoke(this, EventArgs.Empty);
        }

        private void TextBoxPrefix_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Right)
            {
                if (_isExpanded && textBoxSuffix.Visible)
                {
                    textBoxSuffix.Focus();
                    textBoxSuffix.SelectionStart = textBoxSuffix.TextLength;
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            }
        }

        private void TextBoxSuffix_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                CollapseInternal(force: true);
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            if (e.KeyCode == Keys.Left && textBoxSuffix.SelectionStart == 0)
            {
                textBoxPrefix.Focus();
                textBoxPrefix.SelectionStart = textBoxPrefix.TextLength;
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        #endregion

        #region Value Helpers

        private void ReadCurrentTextboxValues()
        {
            _prefixValue = textBoxPrefix.Text ?? string.Empty;
            _suffixValue = textBoxSuffix.Text ?? string.Empty;
        }

        private void SyncUiFromValues()
        {
            if (_isExpanded)
            {
                _isInternalUpdate = true;
                try
                {
                    textBoxPrefix.Text = _prefixValue;
                    textBoxSuffix.Text = _suffixValue;
                }
                finally
                {
                    _isInternalUpdate = false;
                }
            }
            else
            {
                _isInternalUpdate = true;
                try
                {
                    textBoxPrefix.Text = FullText;
                    textBoxSuffix.Text = _suffixValue;
                }
                finally
                {
                    _isInternalUpdate = false;
                }
            }

            AdjustLayout();
            Invalidate();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SetValues(string prefix, string suffix)
        {
            _prefixValue = prefix ?? string.Empty;
            _suffixValue = suffix ?? string.Empty;
            SyncUiFromValues();
        }

        public void Clear()
        {
            _prefixValue = string.Empty;
            _suffixValue = string.Empty;
            SyncUiFromValues();
        }

        #endregion

        #region Cleanup

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_collapseTimer != null)
                {
                    _collapseTimer.Stop();
                    _collapseTimer.Tick -= CollapseTimer_Tick;
                    _collapseTimer.Dispose();
                }
            }

            base.Dispose(disposing);
        }

        #endregion
    }
}
