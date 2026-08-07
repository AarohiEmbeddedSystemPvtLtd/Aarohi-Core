using DocumentFormat.OpenXml.Presentation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Drawing;
using System.Drawing.Design;
using System.Globalization;
using System.Linq;
using System.Security.Policy;
using System.Windows.Forms;
using Font = System.Drawing.Font;

namespace Aarohi.Classes
{
    [DefaultEvent(nameof(ValuePairChanged))]
    public class ExtendedTextBox : UserControl
    {
        // -------------------------------------------------
        // Designer editor for string items
        // -------------------------------------------------
        public sealed class StringListEditor : CollectionEditor
        {
            public StringListEditor(Type type) : base(type) { }
            protected override Type CreateCollectionItemType() => typeof(string);
            protected override object CreateInstance(Type itemType) => string.Empty;
        }

        // -------------------------------------------------
        // Items collection
        // -------------------------------------------------
        public sealed class StringItemCollection : Collection<string>
        {
            private readonly ExtendedTextBox _owner;
            public StringItemCollection(ExtendedTextBox owner) => _owner = owner;

            protected override void InsertItem(int index, string item)
            {
                base.InsertItem(index, item ?? string.Empty);
                _owner.OnItemsChanged();
            }

            protected override void SetItem(int index, string item)
            {
                base.SetItem(index, item ?? string.Empty);
                _owner.OnItemsChanged();
            }

            protected override void RemoveItem(int index)
            {
                base.RemoveItem(index);
                _owner.OnItemsChanged();
            }

            protected override void ClearItems()
            {
                base.ClearItems();
                _owner.OnItemsChanged();
            }
        }

        // -------------------------------------------------
        // Controls
        // -------------------------------------------------
        private readonly TextBox _textBox;
        private readonly ComboBox _comboBox;
        private readonly StringItemCollection _items;
        private readonly Label _unitLabel;

        // -------------------------------------------------
        // State
        // -------------------------------------------------
        private bool _suppressTextChanged;
        private bool _suppressComboChanged;
        private bool _autoConverting;
        private bool _bulkItemsUpdate;

        private double _rawValue;
        private bool _hasRawValue;
        private string? _rawUnit;

        private int _leftWidth = 100;
        private int _rightWidth = 90;
        private bool _useRightWidth = true;

        private int _lastGoodSelectedIndex = -1;
        private string? _previousUnit;

        private string? _quantityName;
        private string? _defaultUnit;
        private string? _parameterName;
        private string _numberFormat = "0.###";

        private bool _leftEditable = true;
        private bool _leftNumericOnly = false;
        private bool _leftAllowDecimal = true;

        private bool _useSingleUnitLabel;

        // -------------------------------------------------
        // Events
        // -------------------------------------------------
        public event EventHandler? LeftTextChanged;
        public event EventHandler? SelectedIndexChanged;
        public event EventHandler? ValuePairChanged;

        // -------------------------------------------------
        // Constructor
        // -------------------------------------------------
        public ExtendedTextBox()
        {
            _items = new StringItemCollection(this);

            _textBox = new TextBox
            {
                BorderStyle = BorderStyle.FixedSingle
            };

            //_comboBox = new ComboBox
            //{
            //    DropDownStyle = ComboBoxStyle.DropDownList,
            //    IntegralHeight = false
            //};

            //Controls.Add(_textBox);
            //Controls.Add(_comboBox);

            _comboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                IntegralHeight = false
            };

            _unitLabel = new Label
            {
                AutoSize = false,
                BorderStyle = BorderStyle.FixedSingle,
                TextAlign = ContentAlignment.MiddleCenter,
                Visible = false
            };

            Controls.Add(_textBox);
            Controls.Add(_comboBox);
            Controls.Add(_unitLabel);

            Font = new Font("Segoe UI", 9F);
            Size = new Size(190, 26);
            MinimumSize = new Size(80, 24);

            _textBox.TextChanged += TextBox_TextChanged;
            _textBox.KeyPress += TextBox_KeyPress;
            _comboBox.SelectedIndexChanged += ComboBox_SelectedIndexChanged;

            UpdateChildFonts();
            UpdateLayoutParts();
            UpdateComboVisibility();
        }

        // -------------------------------------------------
        // Public properties
        // -------------------------------------------------

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        [Editor(typeof(StringListEditor), typeof(UITypeEditor))]
        public StringItemCollection Items => _items;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public string LeftText
        {
            get => _textBox.Text;
            set
            {
                string newValue = value ?? string.Empty;
                if (_textBox.Text == newValue) return;

                _suppressTextChanged = true;
                _textBox.Text = newValue;
                _suppressTextChanged = false;

                if (UseHighPrecisionConversion)
                    CaptureRawValueFromText();

                LeftTextChanged?.Invoke(this, EventArgs.Empty);
                ValuePairChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public override string Text
        {
            get => LeftText;
            set => LeftText = value;
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public int LeftWidth
        {
            get => _leftWidth;
            set
            {
                _leftWidth = Math.Max(40, value);
                UpdateLayoutParts();
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public bool UseRightWidth
        {
            get => _useRightWidth;
            set
            {
                _useRightWidth = value;
                UpdateLayoutParts();
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public int RightWidth
        {
            get => _rightWidth;
            set
            {
                _rightWidth = Math.Max(50, value);
                UpdateLayoutParts();
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public bool LeftEditable
        {
            get => _leftEditable;
            set
            {
                _leftEditable = value;
                _textBox.ReadOnly = !_leftEditable;
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public bool LeftNumericOnly
        {
            get => _leftNumericOnly;
            set => _leftNumericOnly = value;
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public bool LeftAllowDecimal
        {
            get => _leftAllowDecimal;
            set => _leftAllowDecimal = value;
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public bool AutoConvertOnUnitChange { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to use a high-precision unformatted backing field as the source of truth for unit conversion.
        /// When true, prevents decimal truncation/drift caused by converting formatted textbox values.
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        [Description("Enables high-precision raw backing field for unit conversion to prevent formatting drift.")]
        public bool UseHighPrecisionConversion { get; set; } = false;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public bool AutoLoadUnitsFromParameter { get; set; } = false;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public bool ShowConversionErrorMessageBox { get; set; } = true;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public string ConversionErrorTitle { get; set; } = "Unit Conversion Error";

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public string? QuantityName
        {
            get => _quantityName;
            set => _quantityName = (value ?? string.Empty).Trim();
        }

        /// <summary>
        /// Gets or sets whether a single configured unit is displayed using a
        /// read-only Label instead of the unit ComboBox.
        /// </summary>
        /// <remarks>
        /// The default value is <see langword="false"/> to preserve the original
        /// ExtendedTextBox behaviour.
        ///
        /// When <see langword="false"/>, any non-empty unit collection uses the
        /// existing ComboBox.
        ///
        /// When <see langword="true"/> and exactly one unit exists, the ComboBox
        /// remains internally synchronized but a Label displays the unit.
        ///
        /// Collections containing multiple units always use the ComboBox.
        /// </remarks>
        [DefaultValue(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public bool UseSingleUnitLabel
        {
            get => _useSingleUnitLabel;
            set
            {
                if (_useSingleUnitLabel == value)
                    return;

                _useSingleUnitLabel = value;
                UpdateComboVisibility();
            }
        }

        /// <summary>
        /// Gets or sets the background colour used by the single-unit Label.
        /// </summary>
        /// <remarks>
        /// This property only affects the unit Label shown when
        /// <see cref="UseSingleUnitLabel"/> is enabled and exactly one unit exists.
        /// </remarks>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public Color SingleUnitLabelBackColor
        {
            get => _unitLabel.BackColor;
            set => _unitLabel.BackColor = value;
        }

        /// <summary>
        /// Gets or sets the text colour used by the single-unit Label.
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public Color SingleUnitLabelForeColor
        {
            get => _unitLabel.ForeColor;
            set => _unitLabel.ForeColor = value;
        }

        /// <summary>
        /// Gets or sets the alignment of text displayed by the single-unit Label.
        /// </summary>
        [DefaultValue(typeof(ContentAlignment), nameof(ContentAlignment.MiddleCenter))]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public ContentAlignment SingleUnitLabelTextAlign
        {
            get => _unitLabel.TextAlign;
            set => _unitLabel.TextAlign = value;
        }

        /// <summary>
        /// Gets or sets the border style used by the single-unit Label.
        /// </summary>
        [DefaultValue(typeof(BorderStyle), nameof(BorderStyle.FixedSingle))]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public BorderStyle SingleUnitLabelBorderStyle
        {
            get => _unitLabel.BorderStyle;
            set => _unitLabel.BorderStyle = value;
        }

        //[DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        //public string? DefaultUnit
        //{
        //    get => _defaultUnit;
        //    set
        //    {
        //        _defaultUnit = (value ?? string.Empty).Trim();

        //        if (!string.IsNullOrWhiteSpace(_defaultUnit) && HasRightPart)
        //        {
        //            int idx = FindUnitIndex(_defaultUnit);
        //            if (idx >= 0)
        //            {
        //                _suppressComboChanged = true;
        //                _comboBox.SelectedIndex = idx;
        //                _suppressComboChanged = false;
        //                _lastGoodSelectedIndex = idx;
        //                _previousUnit = SelectedItem;
        //            }
        //        }
        //    }
        //}

        /// <summary>
        /// Gets or sets the unit in which stored or supplied default values are
        /// interpreted.
        /// </summary>
        /// <remarks>
        /// When the configured default unit exists in <see cref="Items"/>, it becomes
        /// the currently selected unit.
        ///
        /// For a single-unit control, the underlying ComboBox selection is retained
        /// for backward compatibility while the selected unit is displayed through
        /// the read-only unit Label.
        /// </remarks>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public string? DefaultUnit
        {
            get => _defaultUnit;
            set
            {
                _defaultUnit =
                    (value ?? string.Empty).Trim();

                if (!string.IsNullOrWhiteSpace(_defaultUnit) &&
                    HasRightPart)
                {
                    int index =
                        FindUnitIndex(_defaultUnit);

                    if (index >= 0)
                    {
                        _suppressComboChanged = true;

                        try
                        {
                            _comboBox.SelectedIndex = index;
                        }
                        finally
                        {
                            _suppressComboChanged = false;
                        }

                        _lastGoodSelectedIndex = index;
                        _previousUnit = SelectedItem;
                    }
                }

                UpdateComboVisibility();
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public string? ParameterName
        {
            get => _parameterName;
            set
            {
                _parameterName = (value ?? string.Empty).Trim();

                if (AutoLoadUnitsFromParameter && !DesignMode)
                    RefreshUnitsFromParameter();
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public string NumberFormat
        {
            get => _numberFormat;
            set => _numberFormat = string.IsNullOrWhiteSpace(value) ? "0.###" : value;
        }

        public string? CurrentUnit => SelectedItem;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public int SelectedIndex
        {
            get => _comboBox.SelectedIndex;
            set
            {
                if (_comboBox.Items.Count == 0)
                {
                    _suppressComboChanged = true;
                    _comboBox.SelectedIndex = -1;
                    _suppressComboChanged = false;
                    return;
                }

                int newValue = Math.Max(0, Math.Min(_comboBox.Items.Count - 1, value));
                if (_comboBox.SelectedIndex == newValue) return;

                _comboBox.SelectedIndex = newValue;
            }
        }

        public string? SelectedItem => _comboBox.SelectedItem?.ToString();

        private bool HasRightPart => _items.Count > 0;

        // -------------------------------------------------
        // Layout
        // -------------------------------------------------
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateLayoutParts();
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            UpdateChildFonts();
            UpdateLayoutParts();
        }

        private void UpdateChildFonts()
        {
            _textBox.Font = Font;
            _comboBox.Font = Font;
            _unitLabel.Font = Font;
        }

        //private void UpdateLayoutParts()
        //{
        //    SuspendLayout();

        //    try
        //    {
        //        int gap = 4;
        //        int cw = ClientSize.Width;
        //        int ch = ClientSize.Height;

        //        if (cw <= 0 || ch <= 0) return;

        //        int tbHeight = _textBox.PreferredHeight;
        //        int cbHeight = _comboBox.PreferredSize.Height;

        //        int tbY = Math.Max(0, (ch - tbHeight) / 2);
        //        int cbY = Math.Max(0, (ch - cbHeight) / 2);

        //        if (!HasRightPart)
        //        {
        //            _textBox.SetBounds(0, tbY, cw, tbHeight);
        //            _comboBox.Visible = false;
        //            return;
        //        }

        //        int textWidth;
        //        int comboWidth;

        //        if (_useRightWidth)
        //        {
        //            comboWidth = Math.Max(50, Math.Min(_rightWidth, Math.Max(50, cw - 50)));
        //            textWidth = cw - comboWidth - gap;
        //        }
        //        else
        //        {
        //            textWidth = Math.Max(40, Math.Min(_leftWidth, Math.Max(40, cw - 50)));
        //            comboWidth = cw - textWidth - gap;
        //        }

        //        if (textWidth < 40)
        //        {
        //            textWidth = Math.Max(40, (cw - gap) / 2);
        //            comboWidth = cw - textWidth - gap;
        //        }

        //        if (comboWidth < 50)
        //        {
        //            comboWidth = Math.Max(50, (cw - gap) / 2);
        //            textWidth = cw - comboWidth - gap;
        //        }

        //        _textBox.SetBounds(0, tbY, textWidth, tbHeight);
        //        _comboBox.SetBounds(textWidth + gap, cbY, comboWidth, cbHeight);
        //        _comboBox.Visible = true;
        //    }
        //    finally
        //    {
        //        ResumeLayout();
        //    }
        //}

        /// <summary>
        /// Arranges the value TextBox and the applicable unit control within the
        /// available client area.
        /// </summary>
        /// <remarks>
        /// With no configured units, the TextBox occupies the complete width.
        ///
        /// With one configured unit, the unit Label occupies the same right-side
        /// area previously reserved for the ComboBox.
        ///
        /// With multiple configured units, the existing ComboBox layout and
        /// selection behaviour is preserved.
        /// </remarks>
        private void UpdateLayoutParts()
        {
            SuspendLayout();

            try
            {
                const int gap = 4;

                int clientWidth = ClientSize.Width;
                int clientHeight = ClientSize.Height;

                if (clientWidth <= 0 || clientHeight <= 0)
                    return;

                int textBoxHeight = _textBox.PreferredHeight;
                int unitControlHeight = _comboBox.PreferredSize.Height;

                int textBoxY =
                    Math.Max(0, (clientHeight - textBoxHeight) / 2);

                int unitControlY =
                    Math.Max(0, (clientHeight - unitControlHeight) / 2);

                int unitCount = _comboBox.Items.Count;

                if (unitCount == 0)
                {
                    _textBox.SetBounds(
                        0,
                        textBoxY,
                        clientWidth,
                        textBoxHeight);

                    _comboBox.Visible = false;
                    _unitLabel.Visible = false;
                    return;
                }

                int textWidth;
                int unitControlWidth;

                if (_useRightWidth)
                {
                    unitControlWidth = Math.Max(
                        50,
                        Math.Min(
                            _rightWidth,
                            Math.Max(50, clientWidth - 50)));

                    textWidth =
                        clientWidth -
                        unitControlWidth -
                        gap;
                }
                else
                {
                    textWidth = Math.Max(
                        40,
                        Math.Min(
                            _leftWidth,
                            Math.Max(40, clientWidth - 50)));

                    unitControlWidth =
                        clientWidth -
                        textWidth -
                        gap;
                }

                if (textWidth < 40)
                {
                    textWidth =
                        Math.Max(
                            40,
                            (clientWidth - gap) / 2);

                    unitControlWidth =
                        clientWidth -
                        textWidth -
                        gap;
                }

                if (unitControlWidth < 50)
                {
                    unitControlWidth =
                        Math.Max(
                            50,
                            (clientWidth - gap) / 2);

                    textWidth =
                        clientWidth -
                        unitControlWidth -
                        gap;
                }

                _textBox.SetBounds(
                    0,
                    textBoxY,
                    textWidth,
                    textBoxHeight);

                _comboBox.SetBounds(
                    textWidth + gap,
                    unitControlY,
                    unitControlWidth,
                    unitControlHeight);

                _unitLabel.SetBounds(
                    textWidth + gap,
                    unitControlY,
                    unitControlWidth,
                    unitControlHeight);

                bool showSingleUnitLabel = UseSingleUnitLabel && unitCount == 1;

                _comboBox.Visible = unitCount > 0 && !showSingleUnitLabel;
                _unitLabel.Visible = showSingleUnitLabel;
            }
            finally
            {
                ResumeLayout();
            }
        }

        //private void UpdateComboVisibility()
        //{
        //    _comboBox.Visible = HasRightPart;
        //    UpdateLayoutParts();
        //}

        /// <summary>
        /// Updates the visible unit control according to the configured units and
        /// the <see cref="UseSingleUnitLabel"/> setting.
        /// </summary>
        /// <remarks>
        /// With no units, both right-side controls are hidden.
        ///
        /// When <see cref="UseSingleUnitLabel"/> is enabled and exactly one unit is
        /// configured, the unit is displayed through the read-only Label.
        ///
        /// In all other non-empty cases, the original ComboBox behaviour is used.
        /// </remarks>
        private void UpdateComboVisibility()
        {
            int unitCount = _comboBox.Items.Count;

            bool showSingleUnitLabel =
                UseSingleUnitLabel &&
                unitCount == 1;

            if (showSingleUnitLabel)
            {
                _unitLabel.Text =
                    _comboBox.SelectedItem?.ToString()
                    ?? _items[0];
            }
            else
            {
                _unitLabel.Text = string.Empty;
            }

            _unitLabel.Visible =
                showSingleUnitLabel;

            _comboBox.Visible =
                unitCount > 0 &&
                !showSingleUnitLabel;

            UpdateLayoutParts();
        }

        // -------------------------------------------------
        // Items handling
        // -------------------------------------------------
        private void OnItemsChanged()
        {
            if (_bulkItemsUpdate) return;

            SyncComboFromItems();
        }

        private void SyncComboFromItems()
        {
            string? oldSelected = SelectedItem;

            _suppressComboChanged = true;

            try
            {
                _comboBox.BeginUpdate();
                _comboBox.Items.Clear();

                foreach (string item in _items)
                    _comboBox.Items.Add(item);

                if (_comboBox.Items.Count == 0)
                {
                    _comboBox.SelectedIndex = -1;
                    _lastGoodSelectedIndex = -1;
                    _previousUnit = null;
                }
                else
                {
                    int idx = -1;

                    if (!string.IsNullOrWhiteSpace(DefaultUnit))
                        idx = FindUnitIndex(DefaultUnit);

                    if (idx < 0 && !string.IsNullOrWhiteSpace(oldSelected))
                        idx = FindUnitIndex(oldSelected);

                    if (idx < 0)
                        idx = 0;

                    _comboBox.SelectedIndex = idx;
                    _lastGoodSelectedIndex = idx;
                    _previousUnit = _comboBox.SelectedItem?.ToString();
                }
            }
            finally
            {
                _comboBox.EndUpdate();
                _suppressComboChanged = false;
            }

            UpdateComboVisibility();
            ValuePairChanged?.Invoke(this, EventArgs.Empty);
        }

        private int FindUnitIndex(string? unit)
        {
            if (string.IsNullOrWhiteSpace(unit)) return -1;

            for (int i = 0; i < _comboBox.Items.Count; i++)
            {
                if (string.Equals(
                    Convert.ToString(_comboBox.Items[i])?.Trim(),
                    unit.Trim(),
                    StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        public void SetUnits(IEnumerable<string>? units)
        {
            _bulkItemsUpdate = true;

            try
            {
                _items.Clear();

                if (units != null)
                {
                    foreach (string unit in units)
                    {
                        if (!string.IsNullOrWhiteSpace(unit))
                            _items.Add(unit.Trim());
                    }
                }
            }
            finally
            {
                _bulkItemsUpdate = false;
            }

            SyncComboFromItems();
        }

        public void SetUnitsFromCsv(string? unitsCsv)
        {
            if (string.IsNullOrWhiteSpace(unitsCsv))
            {
                SetUnits(null);
                return;
            }

            SetUnits(
                unitsCsv
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x)));
        }

        public void SetConversionContext(string? quantityName, string? parameterName, string? defaultUnit)
        {
            _quantityName = (quantityName ?? string.Empty).Trim();
            _parameterName = (parameterName ?? string.Empty).Trim();
            DefaultUnit = defaultUnit;
        }

        public bool SetCurrentUnit(string? unit, bool convertCurrentValue = true)
        {
            if (string.IsNullOrWhiteSpace(unit))
                return false;

            int idx = FindUnitIndex(unit);
            if (idx < 0)
                return false;

            if (_comboBox.SelectedIndex == idx)
                return true;

            if (convertCurrentValue)
            {
                _comboBox.SelectedIndex = idx;
                return true;
            }

            _suppressComboChanged = true;
            try
            {
                _comboBox.SelectedIndex = idx;
            }
            finally
            {
                _suppressComboChanged = false;
            }

            _lastGoodSelectedIndex = idx;
            _previousUnit = SelectedItem;
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
            ValuePairChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }

        public bool RefreshUnitsFromParameter()
        {
            if (string.IsNullOrWhiteSpace(ParameterName))
                return false;

            try
            {
                List<string> units = UnitConverisonEngine.GetUnitsFromParameter(ParameterName);
                SetUnits(units);
                return units.Count > 0;
            }
            catch
            {
                SetUnits(null);
                throw;
            }
        }

        public bool SetParameterAndLoadUnits(string parameter, string? quantityName = null, string? defaultUnit = null)
        {
            ParameterName = parameter;

            if (!string.IsNullOrWhiteSpace(quantityName))
                QuantityName = quantityName;

            if (!string.IsNullOrWhiteSpace(defaultUnit))
                DefaultUnit = defaultUnit;

            return RefreshUnitsFromParameter();
        }

        // -------------------------------------------------
        // TextBox events
        // -------------------------------------------------
        private void TextBox_TextChanged(object? sender, EventArgs e)
        {
            if (_suppressTextChanged) return;

            if (UseHighPrecisionConversion)
            {
                CaptureRawValueFromText();
            }

            LeftTextChanged?.Invoke(this, EventArgs.Empty);
            ValuePairChanged?.Invoke(this, EventArgs.Empty);
        }

        private void TextBox_KeyPress(object? sender, KeyPressEventArgs e)
        {
            if (!_leftNumericOnly)
                return;

            if (char.IsControl(e.KeyChar))
                return;

            char dec = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator[0];

            bool isDigit = char.IsDigit(e.KeyChar);
            bool isDecimal = _leftAllowDecimal && (e.KeyChar == '.' || e.KeyChar == dec);

            if (!isDigit && !isDecimal)
            {
                e.Handled = true;
                return;
            }

            if (isDecimal)
            {
                if (e.KeyChar == '.')
                    e.KeyChar = dec;

                if (_textBox.Text.Contains(dec))
                {
                    e.Handled = true;
                    return;
                }
            }
        }

        // -------------------------------------------------
        // ComboBox events
        // -------------------------------------------------
        private void ComboBox_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_suppressComboChanged) return;

            string? oldUnit = _previousUnit;
            string? newUnit = SelectedItem;

            if (AutoConvertOnUnitChange)
            {
                string? error;
                bool ok = UseHighPrecisionConversion
                    ? TryConvertRawValueOnUnitChange(oldUnit, newUnit, out error)
                    : TryConvertLeftTextOnUnitChange_WithResult(oldUnit, newUnit, out error);

                if (!ok)
                {
                    _suppressComboChanged = true;
                    try
                    {
                        _comboBox.SelectedIndex = _lastGoodSelectedIndex;
                    }
                    finally
                    {
                        _suppressComboChanged = false;
                    }

                    if (ShowConversionErrorMessageBox && !string.IsNullOrWhiteSpace(error))
                    {
                        IWin32Window owner = FindForm();
                        MessageBox.Show(owner, error, ConversionErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }

                    return;
                }
            }

            _lastGoodSelectedIndex = _comboBox.SelectedIndex;
            _previousUnit = SelectedItem;
            // Notify consumers only after the displayed value and selected unit agree.
            // Calculated dependants (for example Duty Head Min/Max) must never observe
            // the new unit with the previous unit's numeric value.
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
            ValuePairChanged?.Invoke(this, EventArgs.Empty);
        }

        // -------------------------------------------------
        // Conversion methods
        // -------------------------------------------------
        private bool TryConvertLeftTextOnUnitChange_WithResult(string? oldUnit, string? newUnit, out string? error)
        {
            error = null;

            if (_autoConverting) return true;
            if (string.IsNullOrWhiteSpace(_quantityName)) return true;
            if (string.IsNullOrWhiteSpace(oldUnit)) return true;
            if (string.IsNullOrWhiteSpace(newUnit)) return true;

            if (oldUnit.Equals(newUnit, StringComparison.OrdinalIgnoreCase))
                return true;

            if (!TryGetCurrentValue(out double oldValue))
                return true;

            try
            {
                _autoConverting = true;

                var result = UnitConverisonEngine.convert(
                    _quantityName!,
                    oldValue,
                    oldUnit!,
                    newUnit!
                );

                _suppressTextChanged = true;
                _textBox.Text = result.value.ToString(_numberFormat, CultureInfo.CurrentCulture);
                _suppressTextChanged = false;

                LeftTextChanged?.Invoke(this, EventArgs.Empty);
                return true;
            }
            catch (Exception ex)
            {
                error =
                    $"Conversion failed.\n\n" +
                    $"Quantity: {_quantityName}\n" +
                    $"From: {oldUnit}\n" +
                    $"To: {newUnit}\n" +
                    $"Value: {oldValue.ToString(_numberFormat, CultureInfo.CurrentCulture)}\n\n" +
                    $"Reason: {ex.Message}";

                return false;
            }
            finally
            {
                _autoConverting = false;
            }
        }

        public bool TryGetCurrentValue(out double value)
        {
            return TryParseDoubleAnyCulture(LeftText, out value);
        }

        public bool TryGetBothValues(
            out double currentValue, out string currentUnit,
            out double defaultValue, out string defaultUnit,
            out string? error)
        {
            currentValue = 0;
            defaultValue = 0;
            currentUnit = (CurrentUnit ?? string.Empty).Trim();
            defaultUnit = (DefaultUnit ?? string.Empty).Trim();
            error = null;

            try
            {
                if (string.IsNullOrWhiteSpace(QuantityName))
                {
                    error = "QuantityName is empty.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(currentUnit))
                {
                    error = "CurrentUnit is empty.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(defaultUnit))
                {
                    error = "DefaultUnit is empty.";
                    return false;
                }

                bool parsed = UseHighPrecisionConversion
                    ? TryGetRawValueInUnit(currentUnit, out currentValue)
                    : TryGetCurrentValue(out currentValue);

                if (!parsed)
                {
                    error = $"LeftText is not numeric: '{LeftText}'";
                    return false;
                }

                if (currentUnit.Equals(defaultUnit, StringComparison.OrdinalIgnoreCase))
                {
                    defaultValue = currentValue;
                    return true;
                }

                if (UseHighPrecisionConversion)
                    return TryGetRawValueInUnit(defaultUnit, out defaultValue);

                var result = UnitConverisonEngine.convert(
                    QuantityName!,
                    currentValue,
                    currentUnit,
                    defaultUnit
                );

                defaultValue = result.value;
                return true;
            }
            catch (Exception ex)
            {
                error =
                    "TryGetBothValues failed.\n\n" +
                    $"Quantity: '{QuantityName}'\n" +
                    $"CurrentUnit: '{currentUnit}'\n" +
                    $"DefaultUnit: '{defaultUnit}'\n" +
                    $"LeftText: '{LeftText}'\n\n" +
                    ex.Message;

                return false;
            }
        }

        public bool SetDefaultValue(double defaultValue)
        {
            double finalValue = defaultValue;

            bool hasUnitContext =
                !string.IsNullOrWhiteSpace(QuantityName) &&
                !string.IsNullOrWhiteSpace(DefaultUnit) &&
                !string.IsNullOrWhiteSpace(CurrentUnit);

            if (hasUnitContext && !string.Equals(CurrentUnit, DefaultUnit, StringComparison.OrdinalIgnoreCase))
            {
                var result = UnitConverisonEngine.convert(
                    QuantityName!,
                    defaultValue,
                    DefaultUnit!,
                    CurrentUnit!
                );

                finalValue = result.value;
            }

            _autoConverting = true;
            try
            {
                _suppressTextChanged = true;
                _textBox.Text = finalValue.ToString(NumberFormat, CultureInfo.CurrentCulture);
                _suppressTextChanged = false;

                if (UseHighPrecisionConversion)
                {
                    _rawValue = finalValue;
                    _hasRawValue = true;
                    _rawUnit = CurrentUnit;
                }

                LeftTextChanged?.Invoke(this, EventArgs.Empty);
                ValuePairChanged?.Invoke(this, EventArgs.Empty);
                return true;
            }
            finally
            {
                _autoConverting = false;
            }
        }

        public bool SetValueAndUnit(double value, string unit, bool convertFromDefaultUnit = false)
        {
            if (string.IsNullOrWhiteSpace(unit))
                return false;

            int idx = FindUnitIndex(unit);
            if (idx < 0)
                return false;

            double finalValue = value;

            if (convertFromDefaultUnit)
            {
                if (string.IsNullOrWhiteSpace(QuantityName)) return false;
                if (string.IsNullOrWhiteSpace(DefaultUnit)) return false;

                if (!string.Equals(unit, DefaultUnit, StringComparison.OrdinalIgnoreCase))
                {
                    var result = UnitConverisonEngine.convert(
                        QuantityName!,
                        value,
                        DefaultUnit!,
                        unit
                    );

                    finalValue = result.value;
                }
            }

            _autoConverting = true;
            try
            {
                _suppressComboChanged = true;
                _comboBox.SelectedIndex = idx;
                _suppressComboChanged = false;

                _lastGoodSelectedIndex = idx;
                _previousUnit = SelectedItem;

                _suppressTextChanged = true;
                _textBox.Text = finalValue.ToString(NumberFormat, CultureInfo.CurrentCulture);
                _suppressTextChanged = false;

                if (UseHighPrecisionConversion)
                {
                    _rawValue = finalValue;
                    _hasRawValue = true;
                    _rawUnit = CurrentUnit;
                }

                SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
                LeftTextChanged?.Invoke(this, EventArgs.Empty);
                ValuePairChanged?.Invoke(this, EventArgs.Empty);

                return true;
            }
            finally
            {
                _autoConverting = false;
            }
        }

        public void SetLeftTextRaw(string? text, bool numericMode = false, bool treatNumericAsDefaultUnit = true)
        {
            text = (text ?? string.Empty).Trim();

            if (text.Length == 0)
            {
                LeftText = string.Empty;
                return;
            }

            if (numericMode && TryParseDoubleAnyCulture(text, out double num))
            {
                if (treatNumericAsDefaultUnit)
                {
                    if (!SetDefaultValue(num))
                        LeftText = num.ToString(NumberFormat, CultureInfo.CurrentCulture);
                }
                else
                {
                    LeftText = num.ToString(NumberFormat, CultureInfo.CurrentCulture);
                }

                return;
            }

            LeftText = text;
        }

        private static bool TryParseDoubleAnyCulture(string? s, out double value)
        {
            value = 0;

            if (string.IsNullOrWhiteSpace(s))
                return false;

            if (double.TryParse(s, NumberStyles.Any, CultureInfo.CurrentCulture, out value))
                return true;

            if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out value))
                return true;

            string swapped = s.Replace(',', '.');
            if (double.TryParse(swapped, NumberStyles.Any, CultureInfo.InvariantCulture, out value))
                return true;

            return false;
        }

        // -------------------------------------------------
        // High-Precision Methods (Opt-In / V0.1.34+)
        // -------------------------------------------------

        /// <summary>
        /// Sets a high-precision numeric value and its corresponding unit. This acts as the raw source of truth,
        /// formatting the display text to the TextBox while preventing rounding decay in subsequent conversions.
        /// </summary>
        /// <param name="value">The raw, high-precision numeric value.</param>
        /// <param name="unit">The target engineering unit symbol.</param>
        public void SetRawValueAndUnit(double value, string unit)
        {
            _rawValue = value;
            _hasRawValue = true;
            _rawUnit = unit.Trim();

            int idx = FindUnitIndex(unit);
            if (idx >= 0)
            {
                _suppressComboChanged = true;
                try
                {
                    _comboBox.SelectedIndex = idx;
                }
                finally
                {
                    _suppressComboChanged = false;
                }
                _lastGoodSelectedIndex = idx;
                _previousUnit = SelectedItem;
            }

            _suppressTextChanged = true;
            _textBox.Text = value.ToString(_numberFormat, CultureInfo.CurrentCulture);
            _suppressTextChanged = false;

            ValuePairChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Attempts to retrieve the unformatted high-precision raw value. If high-precision mode is enabled
        /// and a raw value has been set/tracked, it returns the raw value; otherwise, it parses the current display text.
        /// </summary>
        /// <param name="value">Output parameter containing the unformatted high-precision value.</param>
        /// <returns>True if the value was successfully retrieved/parsed; otherwise, false.</returns>
        public bool TryGetRawValue(out double value)
        {
            if (UseHighPrecisionConversion && _hasRawValue)
            {
                value = _rawValue;
                return true;
            }

            bool success = TryParseDoubleAnyCulture(LeftText, out value);
            if (success && UseHighPrecisionConversion)
            {
                _rawValue = value;
                _hasRawValue = true;
                _rawUnit = CurrentUnit;
            }
            return success;
        }

        private void CaptureRawValueFromText()
        {
            if (TryParseDoubleAnyCulture(_textBox.Text, out double value))
            {
                _rawValue = value;
                _hasRawValue = true;
                _rawUnit = CurrentUnit;
            }
            else
            {
                _hasRawValue = false;
                _rawUnit = null;
            }
        }

        private bool TryGetRawValueInUnit(string targetUnit, out double value)
        {
            value = 0;
            if (!TryGetRawValue(out double rawValue))
                return false;

            string sourceUnit = string.IsNullOrWhiteSpace(_rawUnit)
                ? (CurrentUnit ?? string.Empty).Trim()
                : _rawUnit.Trim();
            if (string.IsNullOrWhiteSpace(sourceUnit) ||
                sourceUnit.Equals(targetUnit, StringComparison.OrdinalIgnoreCase))
            {
                value = rawValue;
                return true;
            }

            value = UnitConverisonEngine.convert(
                QuantityName!, rawValue, sourceUnit, targetUnit).value;
            return true;
        }

        /// <summary>
        /// Internal helper that performs unit conversion on the high-precision backing raw value,
        /// avoiding compounding rounding errors from formatted display text.
        /// </summary>
        private bool TryConvertRawValueOnUnitChange(string? oldUnit, string? newUnit, out string? error)
        {
            error = null;

            if (_autoConverting) return true;
            if (string.IsNullOrWhiteSpace(_quantityName)) return true;
            if (string.IsNullOrWhiteSpace(oldUnit)) return true;
            if (string.IsNullOrWhiteSpace(newUnit)) return true;

            if (oldUnit.Equals(newUnit, StringComparison.OrdinalIgnoreCase))
                return true;

            if (!TryGetRawValue(out double oldValue))
                return true;

            try
            {
                _autoConverting = true;

                string sourceUnit = string.IsNullOrWhiteSpace(_rawUnit) ? oldUnit! : _rawUnit!;
                double convertedValue = sourceUnit.Equals(newUnit, StringComparison.OrdinalIgnoreCase)
                    ? oldValue
                    : UnitConverisonEngine.convert(
                        _quantityName!, oldValue, sourceUnit, newUnit!).value;

                _suppressTextChanged = true;
                _textBox.Text = convertedValue.ToString(_numberFormat, CultureInfo.CurrentCulture);
                _suppressTextChanged = false;

                LeftTextChanged?.Invoke(this, EventArgs.Empty);
                return true;
            }
            catch (Exception ex)
            {
                error =
                    $"Conversion failed.\n\n" +
                    $"Quantity: {_quantityName}\n" +
                    $"From: {oldUnit} -> To: {newUnit}\n\n" +
                    $"Details: {ex.Message}";

                return false;
            }
            finally
            {
                _autoConverting = false;
            }
        }
    }
}
