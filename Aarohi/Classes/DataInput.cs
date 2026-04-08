using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Aarohi.Classes
{
    public partial class DataInput : UserControl
    {
        #region === Fields & Constants ===
        private Control? _editor;
        private Type _declaredType = typeof(string);   // e.g., typeof(decimal?), typeof(MyEnum)
        private Type _effectiveType = typeof(string);  // non-nullable underlying type

        private Font _inputFont = new Font("Gadugi", 18f, FontStyle.Regular);
        private const int _minControlHeight = 44;
        private readonly Padding _inputMargin = new Padding(6, 6, 6, 6);
        private string? _originalLabelText;

        private BindingList<string>? _boundOptions;    // live-binding source for ComboBox
        private Panel? _binderPanel;
        private Label? _binderLabel;
        private Button? _binderButton;
        private object? _binderValue;

        public delegate (DialogResult result, object? selectedValue, string? displayText)
            BinderPicker(IWin32Window owner, object? current);

        private BinderPicker? _binderPicker;
        private readonly ToolTip _tip = new ToolTip();
        #endregion

        #region === Events ===
        /// <summary>Raised whenever inner control value changes.</summary>
        public event EventHandler? ValueChanged;
        #endregion

        #region === Public Properties ===
        private string _columnName = string.Empty;

        [Browsable(true)]
        [Category("Data")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string ColumnName
        {
            get => _columnName;
            set => _columnName = value;
        }

        [Browsable(true)]
        [Category("Data")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string DisplayName
        {
            get => labelName.Text;
            set => labelName.Text = ((string.IsNullOrEmpty(value) || value == "") ? _columnName : value).Replace('_', ' ');
        }

        [Browsable(false)]
        public Type DeclaredType => _declaredType;

        /// <summary>Current editor value (boxed). Returns null for empty nullable fields.</summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public object? Value
        {
            get => ReadEditorValue();
            set => WriteEditorValue(value);
        }

        [Browsable(true)]
        [Category("Appearance")]
        [Description("Font used for the input editor(s). Default: Gadugi, 18pt")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Font InputFont
        {
            get => _inputFont;
            set
            {
                _inputFont = value ?? new Font("Gadugi", 18f, FontStyle.Regular);
                if (_editor != null) ApplyCommonStyles(_editor);
                UpdateTooltip();
            }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public object? BinderValue
        {
            get => _binderValue;
            set => WriteEditorValue(value);
        }

        /// <summary>Current binder display text.</summary>
        [Browsable(false)]
        public string BinderDisplayText => _binderLabel?.Text ?? string.Empty;
        #endregion

        #region === Public API: Required/Optional Markers ===
        public void set_Required()
        {
            if (_originalLabelText == null)
                _originalLabelText = labelName.Text;

            if (!labelName.Text.EndsWith("*"))
            {
                labelName.ForeColor = Color.Red;
                labelName.Text += "*";
            }
        }

        public void unset_Required()
        {
            if (_originalLabelText != null)
            {
                labelName.ForeColor = SystemColors.ControlText;
                labelName.Text = _originalLabelText;
            }
        }
        #endregion

        #region === Public API: Value Helpers ===
        public T? GetValue<T>() => (T?)ConvertTo(typeof(T), Value);

        /// <summary>Returns a string describing which editor type is currently in use.</summary>
        public string GetEditorType()
        {
            if (_editor is NumericUpDown) return "NumericUpDown";
            else if (_editor is ComboBox) return "ComboBox";
            else if (_editor is TextBox) return "TextBox";
            else if (_editor is DateTimePicker) return "DateTimePicker";
            else if (_editor is CheckBox) return "CheckBox";
            else if (_editor == _binderPanel) return "Binder";
            return "Unknown";
        }
        #endregion

        #region === Constructors ===
        public DataInput()
        {
            InitializeComponent();
            DoubleBuffered = true;

            _tip.AutoPopDelay = 8000;
            _tip.InitialDelay = 400;
            _tip.ReshowDelay = 100;
            _tip.ShowAlways = true;

            this.ValueChanged += (_, __) => UpdateTooltip();
            this.HandleCreated += (_, __) => UpdateTooltip();
        }

        public DataInput(string columnName, Type propertyType, object? initialValue = null, string displayName = "") : this()
        {
            Configure(columnName, propertyType, initialValue, displayName);
        }

        public DataInput(string columnName, string[] options, string? initialValue = null, string displayName = "") : this()
        {
            ColumnName = columnName ?? string.Empty;
            DisplayName = displayName ?? string.Empty;

            // Treat as string input type
            _declaredType = typeof(string);
            _effectiveType = typeof(string);

            // Build a ComboBox with given options
            panelInput.Controls.Clear();

            var cb = new NoWheelComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                IntegralHeight = false,
                MaxDropDownItems = 20
            };

            // bind a snapshot list (non-live by default)
            cb.DataSource = (options ?? Array.Empty<string>()).ToList();

            cb.SelectedIndexChanged += (_, __) => ValueChanged?.Invoke(this, EventArgs.Empty);

            _editor = cb;
            ApplyCommonStyles(cb);
            panelInput.Controls.Add(cb);

            // initial selection
            if (!string.IsNullOrWhiteSpace(initialValue))
            {
                int idx = cb.FindStringExact(initialValue);
                cb.SelectedIndex = idx >= 0 ? idx : (cb.Items.Count > 0 ? 0 : -1);
            }
            else
            {
                cb.SelectedIndex = cb.Items.Count > 0 ? 0 : -1;
            }

            UpdateTooltip();
        }

        public DataInput(string columnName,
                         BinderPicker binderPicker,
                         object? initialValue = null,
                         string? initialDisplayText = null,
                         string displayName = "") : this()
        {
            ColumnName = columnName ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            ConfigureBinder(binderPicker, initialValue, initialDisplayText);
        }
        #endregion

        #region === Public API: Configuration & Options ===

        /// <summary>
        /// Configure this control to use Binder mode (Label + "..." button).
        /// binderPicker should open your form and return the chosen value and a display string.
        /// </summary>
        public void ConfigureBinder(BinderPicker binderPicker,
                                    object? initialValue = null,
                                    string? initialDisplayText = null)
        {
            _declaredType = typeof(object);
            _effectiveType = typeof(object);
            _binderPicker = binderPicker;

            panelInput.Controls.Clear();
            _binderPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0) };

            _binderLabel = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(6),
                Font = _inputFont
            };

            _binderButton = new Button
            {
                Dock = DockStyle.Right,
                Width = 46,
                Text = "...",
                Margin = new Padding(6),
                FlatStyle = FlatStyle.Flat,
                Font = _inputFont
            };

            _binderButton.Click += (_, __) =>
            {
                if (_binderPicker == null) return;
                var (result, selected, display) = _binderPicker(this, _binderValue);
                if (result == DialogResult.OK)
                {
                    _binderValue = selected;
                    if (_binderLabel != null) _binderLabel.Text = display ?? selected?.ToString() ?? string.Empty;
                    ValueChanged?.Invoke(this, EventArgs.Empty);
                }
            };

            _binderPanel.Controls.Add(_binderLabel);
            _binderPanel.Controls.Add(_binderButton);

            _editor = _binderPanel;                 // keep existing plumbing happy
            ApplyCommonStyles(_binderPanel);        // font/height, etc.
            panelInput.Controls.Add(_binderPanel);

            // initial
            _binderValue = initialValue;
            if (_binderLabel != null)
                _binderLabel.Text = initialDisplayText ?? initialValue?.ToString() ?? string.Empty;

            // Focus/hover visuals like other editors
            _binderPanel.GotFocus += (_, __) =>
            {
                BorderStyle = BorderStyle.FixedSingle;
                BackColor = Color.AliceBlue;
            };
            _binderPanel.LostFocus += (_, __) =>
            {
                BackColor = Color.Transparent;
                BorderStyle = BorderStyle.None;
            };

            UpdateTooltip();
        }

        public void Configure(string columnName, Type propertyType, object? initialValue = null, string displayName = "")
        {
            ColumnName = columnName;
            DisplayName = displayName;
            _declaredType = propertyType ?? typeof(string);
            _effectiveType = Nullable.GetUnderlyingType(_declaredType) ?? _declaredType;

            CreateEditorForType(_effectiveType);
            if (initialValue != null) WriteEditorValue(initialValue);

            UpdateTooltip();
        }

        /// <summary>Replace options immediately (non-live). Preserves selection if possible.</summary>
        public void UpdateOptions(IEnumerable<string> options, bool preserveSelection = true, string? defaultSelect = null)
        {
            if (_editor is not ComboBox cb) return;

            object? prev = preserveSelection ? cb.SelectedItem : null;

            var list = (options ?? Array.Empty<string>()).ToList();
            cb.DataSource = list;

            // Restore selection if possible
            if (preserveSelection && prev != null)
            {
                var prevText = cb.GetItemText(prev);
                int idx = cb.FindStringExact(prevText);
                if (idx >= 0) { cb.SelectedIndex = idx; return; }
            }

            // Or apply default selection
            if (!string.IsNullOrWhiteSpace(defaultSelect))
            {
                int idx = cb.FindStringExact(defaultSelect);
                cb.SelectedIndex = idx >= 0 ? idx : (cb.Items.Count > 0 ? 0 : -1);
            }
            else
            {
                cb.SelectedIndex = cb.Items.Count > 0 ? 0 : -1;
            }
        }

        public void UpdateOptions(BindingList<string> newOptions, bool preserveSelection = true)
        {
            if (_editor is not ComboBox cb) return;
            if (newOptions == null) return;

            string? prevText = preserveSelection ? cb.GetItemText(cb.SelectedItem) : null;

            _boundOptions = newOptions;
            cb.DataSource = _boundOptions;

            if (preserveSelection && !string.IsNullOrWhiteSpace(prevText))
            {
                int idx = cb.FindStringExact(prevText);
                if (idx >= 0)
                    cb.SelectedIndex = idx;
                else if (cb.Items.Count > 0)
                    cb.SelectedIndex = 0;
            }
            else
            {
                cb.SelectedIndex = cb.Items.Count > 0 ? 0 : -1;
            }
        }

        public void SetOptions(IEnumerable<string> options, string? select = null)
            => UpdateOptions(options, preserveSelection: false, defaultSelect: select);

        public void BindOptions(BindingList<string> options, string? select = null)
        {
            if (_editor is not ComboBox cb) return;

            _boundOptions = options ?? new BindingList<string>();
            cb.DataSource = _boundOptions;

            if (!string.IsNullOrWhiteSpace(select))
            {
                int idx = cb.FindStringExact(select);
                cb.SelectedIndex = idx >= 0 ? idx : (cb.Items.Count > 0 ? 0 : -1);
            }
            else
            {
                cb.SelectedIndex = cb.Items.Count > 0 ? 0 : -1;
            }
        }

        public void UnbindOptionsKeepSnapshot()
        {
            if (_editor is not ComboBox cb) return;
            if (cb.DataSource is BindingList<string> bl)
            {
                var snapshot = bl.ToList();
                cb.DataSource = snapshot;
            }
            _boundOptions = null;
        }

        /// <summary>Add an option (works for both bound and non-bound modes).</summary>
        public void AddOption(string option, bool distinctIgnoreCase = true)
        {
            if (_editor is not ComboBox cb) return;

            if (_boundOptions != null)
            {
                if (!distinctIgnoreCase || !_boundOptions.Any(x => string.Equals(x, option, StringComparison.OrdinalIgnoreCase)))
                    _boundOptions.Add(option);
            }
            else
            {
                var list = (cb.DataSource as IEnumerable<string>)?.ToList() ?? new List<string>();
                if (!distinctIgnoreCase || !list.Any(x => string.Equals(x, option, StringComparison.OrdinalIgnoreCase)))
                    list.Add(option);
                UpdateOptions(list, preserveSelection: true);
            }
        }

        /// <summary>Remove options by predicate (works for both bound and non-bound modes).</summary>
        public void RemoveOptions(Predicate<string> match)
        {
            if (_editor is not ComboBox cb) return;

            if (_boundOptions != null)
            {
                for (int i = _boundOptions.Count - 1; i >= 0; i--)
                    if (match(_boundOptions[i])) _boundOptions.RemoveAt(i);
            }
            else
            {
                var list = (cb.DataSource as IEnumerable<string>)?.ToList() ?? new List<string>();
                list.RemoveAll(match);
                UpdateOptions(list, preserveSelection: true);
            }
        }

        /// <summary>Get current options (snapshot) regardless of binding mode.</summary>
        public List<string> GetOptions()
        {
            if (_editor is not ComboBox cb) return new List<string>();
            return (cb.DataSource as IEnumerable<string>)?.Select(cb.GetItemText).ToList() ?? new List<string>();
        }

        /// <summary>Optional: set numeric constraints if current editor is a NumericUpDown.</summary>
        public void SetNumericConstraints(int decimalPlaces = 2, decimal? min = null, decimal? max = null, decimal? increment = null)
        {
            if (_editor is NumericUpDown nud)
            {
                nud.DecimalPlaces = decimalPlaces;
                if (min.HasValue) nud.Minimum = min.Value;
                if (max.HasValue) nud.Maximum = max.Value;
                if (increment.HasValue) nud.Increment = increment.Value;

                if (nud.Value < nud.Minimum) nud.Value = nud.Minimum;
                if (nud.Value > nud.Maximum) nud.Value = nud.Maximum;
            }
        }

        /// <summary>
        /// Generic min/max setter (NumericUpDown + DateTimePicker + TextBox(MaxLength)).
        /// </summary>
        public void SetMinMax(object? min = null, object? max = null)
        {
            if (_editor == null) return;

            if (_editor is NumericUpDown nud)
            {
                if (min != null) nud.Minimum = Convert.ToDecimal(min);
                if (max != null) nud.Maximum = Convert.ToDecimal(max);

                if (nud.Value < nud.Minimum) nud.Value = nud.Minimum;
                if (nud.Value > nud.Maximum) nud.Value = nud.Maximum;
                return;
            }

            if (_editor is DateTimePicker dtp)
            {
                if (min is DateTime minDt) dtp.MinDate = minDt;
                if (max is DateTime maxDt) dtp.MaxDate = maxDt;

                if (dtp.Value < dtp.MinDate) dtp.Value = dtp.MinDate;
                if (dtp.Value > dtp.MaxDate) dtp.Value = dtp.MaxDate;
                return;
            }

            if (_editor is TextBox tb)
            {
                if (max != null && int.TryParse(Convert.ToString(max), out int maxLen))
                    tb.MaxLength = Math.Max(0, maxLen);
            }
        }

        public void SetPlaceholder(string placeholder)
        {
            if (_editor is TextBox tb)
            {
                tb.GotFocus += (_, __) => { if (tb.Text == placeholder) tb.Text = ""; };
                tb.LostFocus += (_, __) => { if (string.IsNullOrWhiteSpace(tb.Text)) tb.Text = placeholder; };
                if (string.IsNullOrWhiteSpace(tb.Text)) tb.Text = placeholder;
            }
        }
        #endregion

        #region === ToolTip ===
        private string BuildTooltipText()
        {
            var name = ColumnName;
            var valueObj = ReadEditorValue();
            var text = FormatValueForTip(valueObj, _effectiveType);
            return $"{name}: {text}";
        }

        private static string FormatValueForTip(object? value, Type effectiveType)
        {
            if (value is null) return "(null)";

            if (effectiveType.IsEnum) return value.ToString() ?? string.Empty;

            if (value is bool b) return b ? "TRUE" : "FALSE";

            if (value is decimal or double or float or long or int or short or byte or sbyte
                or ulong or uint or ushort)
            {
                try
                {
                    var dec = Convert.ToDecimal(value);
                    return dec.ToString("0.######");
                }
                catch { return value.ToString() ?? string.Empty; }
            }

            if (value is DateTime dt) return dt.ToString("yyyy-MM-dd HH:mm:ss");

            return value.ToString() ?? string.Empty;
        }

        private void WireTooltipTargets()
        {
            _tip.RemoveAll();

            var targets = new List<Control?> { labelName };

            if (_editor is Control ec) targets.Add(ec);
            if (_binderPanel != null)
            {
                targets.Add(_binderPanel);
                if (_binderLabel != null) targets.Add(_binderLabel);
                if (_binderButton != null) targets.Add(_binderButton);
            }

            var tipText = BuildTooltipText();
            foreach (var ctl in targets.Where(c => c != null).Distinct())
                _tip.SetToolTip(ctl!, tipText);
        }

        private void UpdateTooltip() => WireTooltipTargets();
        #endregion

        #region === Internal: NO WHEEL (blocks value change on mouse scroll) ===
        private const int WM_MOUSEWHEEL = 0x020A;

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private static Control? FindScrollableParent(Control c)
        {
            Control? p = c.Parent;
            while (p != null)
            {
                if (p is ScrollableControl) return p;
                p = p.Parent;
            }
            return c.Parent;
        }

        private static void ForwardWheel(Control ctrl, ref Message m)
        {
            var target = FindScrollableParent(ctrl);
            if (target != null && target.IsHandleCreated)
                SendMessage(target.Handle, m.Msg, m.WParam, m.LParam);
        }

        private sealed class NoWheelComboBox : ComboBox
        {
            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WM_MOUSEWHEEL)
                {
                    ForwardWheel(this, ref m);
                    return; // ✅ prevent selection change by wheel
                }
                base.WndProc(ref m);
            }
        }

        private sealed class NoWheelNumericUpDown : NumericUpDown
        {
            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WM_MOUSEWHEEL)
                {
                    ForwardWheel(this, ref m);
                    return; // ✅ prevent value change by wheel
                }
                base.WndProc(ref m);
            }
        }

        private sealed class NoWheelDateTimePicker : DateTimePicker
        {
            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WM_MOUSEWHEEL)
                {
                    ForwardWheel(this, ref m);
                    return; // ✅ prevent date/time change by wheel
                }
                base.WndProc(ref m);
            }
        }
        #endregion

        #region === Internal: Editor Creation & Styling ===
        private void CreateEditorForType(Type t)
        {
            panelInput.Controls.Clear();
            _editor = null;

            if (t.IsEnum)
            {
                var values = Enum.GetValues(t).Cast<object>()
                    .Select(v => new { Value = v, Text = v.ToString()!.Replace("_", " ") })
                    .ToList();

                var cb = new NoWheelComboBox
                {
                    Dock = DockStyle.Fill,
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    DataSource = values,
                    DisplayMember = "Text",
                    ValueMember = "Value"
                };

                cb.SelectedIndexChanged += (_, __) => ValueChanged?.Invoke(this, EventArgs.Empty);
                _editor = cb;
            }
            else if (t == typeof(bool))
            {
                var chk = new CheckBox { Text = "" };
                chk.CheckedChanged += (_, __) => ValueChanged?.Invoke(this, EventArgs.Empty);
                _editor = chk;
            }
            else if (t == typeof(DateTime))
            {
                var dtp = new NoWheelDateTimePicker
                {
                    Format = DateTimePickerFormat.Custom,
                    CustomFormat = "yyyy-MM-dd HH:mm:ss",
                    ShowUpDown = true
                };
                dtp.ValueChanged += (_, __) => ValueChanged?.Invoke(this, EventArgs.Empty);
                _editor = dtp;
            }
            else if (IsIntegerLike(t))
            {
                var nud = new NoWheelNumericUpDown
                {
                    DecimalPlaces = 0,
                    Minimum = -2147483648M,
                    Maximum = 2147483647M
                };
                nud.ValueChanged += (_, __) => ValueChanged?.Invoke(this, EventArgs.Empty);
                _editor = nud;
            }
            else if (IsDecimalLike(t))
            {
                var nud = new NoWheelNumericUpDown
                {
                    DecimalPlaces = 4,
                    Increment = 0.1M,
                    Minimum = -1000000000M,
                    Maximum = 1000000000M
                };
                nud.ValueChanged += (_, __) => ValueChanged?.Invoke(this, EventArgs.Empty);
                _editor = nud;
            }
            else
            {
                var tb = new TextBox { BorderStyle = BorderStyle.FixedSingle };
                tb.TextChanged += (_, __) => ValueChanged?.Invoke(this, EventArgs.Empty);
                _editor = tb;
            }

            ApplyCommonStyles(_editor!);
            panelInput.Controls.Add(_editor!);

            if (_editor is Control ctrl)
            {
                ctrl.GotFocus += (_, __) =>
                {
                    if (ctrl is TextBox tb) tb.SelectAll();
                    if (ctrl is NumericUpDown nud)
                    {
                        nud.Select(0, 0);
                        nud.Select(0, nud.Text.Length);
                    }

                    BorderStyle = BorderStyle.FixedSingle;
                    BackColor = Color.AliceBlue;
                    ctrl.BackColor = Color.DeepSkyBlue;
                };

                ctrl.LostFocus += (_, __) =>
                {
                    ctrl.BackColor = Color.White;
                    BackColor = Color.Transparent;
                    BorderStyle = BorderStyle.None;
                };
            }
        }

        private void ApplyCommonStyles(Control c)
        {
            c.Font = _inputFont;
            c.Margin = _inputMargin;
            c.Dock = DockStyle.Fill;
            c.MinimumSize = new Size(0, Math.Max(_minControlHeight, _inputFont.Height + 16));

            if (c is TextBox tb)
            {
                tb.BorderStyle = BorderStyle.FixedSingle;
            }
            else if (c is ComboBox cb)
            {
                cb.FlatStyle = FlatStyle.Flat;
                cb.IntegralHeight = false;
                cb.DropDownHeight = Math.Max(200, _inputFont.Height * 8);
                cb.DrawMode = DrawMode.OwnerDrawFixed;
                cb.ItemHeight = Math.Max(_minControlHeight - 6, _inputFont.Height + 6);

                cb.DrawItem -= ComboBox_DrawItem;
                cb.DrawItem += ComboBox_DrawItem;
            }
            else if (c is NumericUpDown nud)
            {
                nud.ThousandsSeparator = true;
                nud.TextAlign = HorizontalAlignment.Right;
                nud.BorderStyle = BorderStyle.FixedSingle;
                try { nud.Controls[0].Width = 28; } catch { /* ignore */ }
            }
            else if (c is DateTimePicker dtp)
            {
                dtp.CalendarFont = _inputFont;
            }
            else if (c is CheckBox chk)
            {
                chk.AutoSize = false;
                chk.TextAlign = ContentAlignment.MiddleLeft;
                chk.MinimumSize = new Size(0, Math.Max(_minControlHeight, _inputFont.Height + 12));
            }

            if (_binderLabel != null) _binderLabel.Font = _inputFont;
            if (_binderButton != null) _binderButton.Font = _inputFont;
        }

        private void ComboBox_DrawItem(object? sender, DrawItemEventArgs e)
        {
            if (sender is not ComboBox cb) return;

            e.DrawBackground();
            if (e.Index >= 0)
            {
                var text = cb.GetItemText(cb.Items[e.Index]);
                TextRenderer.DrawText(e.Graphics, text, _inputFont, e.Bounds,
                    SystemColors.WindowText, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
            }
            e.DrawFocusRectangle();
        }
        #endregion

        #region === Internal: Value Read/Write & Converters ===
        private object? ReadEditorValue()
        {
            if (_editor == null) return null;

            if (_editor == _binderPanel)
                return _binderValue;

            object? raw = null;

            switch (_editor)
            {
                case ComboBox cb:
                    // if databound with ValueMember, use SelectedValue
                    raw = (!string.IsNullOrWhiteSpace(cb.ValueMember) ? cb.SelectedValue : cb.SelectedItem);
                    break;
                case CheckBox chk:
                    raw = chk.Checked;
                    break;
                case DateTimePicker dtp:
                    raw = dtp.Value;
                    break;
                case NumericUpDown nud:
                    raw = ConvertFromDecimal(nud.Value, _effectiveType);
                    break;
                case TextBox tb:
                    raw = tb.Text;
                    break;
            }

            // Nullables: treat empty TextBox as null
            if (Nullable.GetUnderlyingType(_declaredType) != null)
            {
                if (_editor is TextBox tb2 && string.IsNullOrWhiteSpace(tb2.Text))
                    return null;
            }

            if (_effectiveType.IsEnum && raw is string s)
                return Enum.Parse(_effectiveType, s, true);

            return ConvertTo(_declaredType, raw);
        }

        private void WriteEditorValue(object? value)
        {
            if (_editor == null) return;

            try
            {
                if (_editor == _binderPanel)
                {
                    _binderValue = value;
                    if (_binderLabel != null)
                        _binderLabel.Text = value?.ToString() ?? string.Empty;
                    UpdateTooltip();
                    return;
                }

                switch (_editor)
                {
                    case ComboBox cb:
                        if (value == null)
                        {
                            cb.SelectedIndex = cb.Items.Count > 0 ? 0 : -1;
                        }
                        else
                        {
                            object v = value;

                            if (_effectiveType.IsEnum && value is string s)
                                v = Enum.Parse(_effectiveType, s, true);

                            if (!string.IsNullOrWhiteSpace(cb.ValueMember))
                            {
                                cb.SelectedValue = v;
                            }
                            else
                            {
                                cb.SelectedItem = v;
                                // fallback for string lists
                                if (cb.SelectedItem == null)
                                {
                                    var txt = v.ToString() ?? "";
                                    int idx = cb.FindStringExact(txt);
                                    if (idx >= 0) cb.SelectedIndex = idx;
                                }
                            }
                        }
                        break;

                    case CheckBox chk:
                        chk.Checked = value != null && Convert.ToBoolean(value);
                        break;

                    case DateTimePicker dtp:
                        dtp.Value = value is DateTime dt ? dt : DateTime.Now;
                        break;

                    case NumericUpDown nud:
                        {
                            decimal v = 0M;

                            if (value == null || value == DBNull.Value)
                                v = nud.Minimum;
                            else
                                v = ConvertToDecimal(value);

                            if (v < nud.Minimum) v = nud.Minimum;
                            if (v > nud.Maximum) v = nud.Maximum;

                            nud.Value = v;
                            break;
                        }


                    case TextBox tb:
                        tb.Text = value?.ToString() ?? string.Empty;
                        break;
                }

                UpdateTooltip();
            }
            catch
            {
                // fallback to TextBox editor without breaking control usability
                if (!(_editor is TextBox))
                {
                    panelInput.Controls.Clear();
                    var tb = new TextBox { Dock = DockStyle.Fill, Text = value?.ToString() ?? "" };
                    tb.TextChanged += (_, __) => ValueChanged?.Invoke(this, EventArgs.Empty);
                    _editor = tb;
                    ApplyCommonStyles(_editor);
                    panelInput.Controls.Add(_editor);
                    UpdateTooltip();
                }
            }
        }

        private static bool IsIntegerLike(Type t) =>
            t == typeof(int) || t == typeof(long) || t == typeof(short) ||
            t == typeof(uint) || t == typeof(ulong) || t == typeof(ushort) ||
            t == typeof(byte) || t == typeof(sbyte);

        private static bool IsDecimalLike(Type t) =>
            t == typeof(decimal) || t == typeof(double) || t == typeof(float);

        private static decimal ConvertToDecimal(object value)
        {
            if (value is decimal d) return d;
            if (value is double db) return (decimal)db;
            if (value is float f) return (decimal)f;
            if (value is long l) return l;
            if (value is int i) return i;
            if (value is short s) return s;
            if (decimal.TryParse(Convert.ToString(value), out var res)) return res;
            return 0M;
        }

        private static object? ConvertFromDecimal(decimal val, Type target)
        {
            if (target == typeof(decimal)) return val;
            if (target == typeof(double)) return (double)val;
            if (target == typeof(float)) return (float)val;
            if (target == typeof(long)) return (long)val;
            if (target == typeof(int)) return (int)val;
            if (target == typeof(short)) return (short)val;
            return val; // fallback
        }

        private static object? ConvertTo(Type targetType, object? value)
        {
            if (value == null) return null;

            var nt = Nullable.GetUnderlyingType(targetType);
            if (nt != null) // nullable
            {
                if (value is string s && string.IsNullOrWhiteSpace(s))
                    return null;
                targetType = nt;
            }

            try
            {
                if (targetType.IsEnum)
                {
                    if (value is string es) return Enum.Parse(targetType, es, true);
                    return Enum.ToObject(targetType, value);
                }

                if (targetType == typeof(decimal)) return ConvertToDecimal(value);
                if (targetType == typeof(double)) return (double)ConvertToDecimal(value);
                if (targetType == typeof(float)) return (float)ConvertToDecimal(value);
                if (targetType == typeof(long)) return (long)ConvertToDecimal(value);
                if (targetType == typeof(int)) return (int)ConvertToDecimal(value);
                if (targetType == typeof(short)) return (short)ConvertToDecimal(value);

                if (targetType == typeof(bool)) return Convert.ToBoolean(value);
                if (targetType == typeof(DateTime)) return Convert.ToDateTime(value);
                if (targetType == typeof(string)) return Convert.ToString(value);

                return System.Convert.ChangeType(value, targetType);
            }
            catch
            {
                return value; // leave as-is if convert fails
            }
        }
        #endregion
    }
}
