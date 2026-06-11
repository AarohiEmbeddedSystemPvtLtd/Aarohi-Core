using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Drawing;
using System.Drawing.Design;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

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

        // -------------------------------------------------
        // State
        // -------------------------------------------------
        private bool _suppressTextChanged;
        private bool _suppressComboChanged;
        private bool _autoConverting;
        private bool _bulkItemsUpdate;

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

            _comboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                IntegralHeight = false
            };

            Controls.Add(_textBox);
            Controls.Add(_comboBox);

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

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public string? DefaultUnit
        {
            get => _defaultUnit;
            set
            {
                _defaultUnit = (value ?? string.Empty).Trim();

                if (!string.IsNullOrWhiteSpace(_defaultUnit) && HasRightPart)
                {
                    int idx = FindUnitIndex(_defaultUnit);
                    if (idx >= 0)
                    {
                        _suppressComboChanged = true;
                        _comboBox.SelectedIndex = idx;
                        _suppressComboChanged = false;
                        _lastGoodSelectedIndex = idx;
                        _previousUnit = SelectedItem;
                    }
                }
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
        }

        private void UpdateLayoutParts()
        {
            SuspendLayout();

            try
            {
                int gap = 4;
                int cw = ClientSize.Width;
                int ch = ClientSize.Height;

                if (cw <= 0 || ch <= 0) return;

                int tbHeight = _textBox.PreferredHeight;
                int cbHeight = _comboBox.PreferredSize.Height;

                int tbY = Math.Max(0, (ch - tbHeight) / 2);
                int cbY = Math.Max(0, (ch - cbHeight) / 2);

                if (!HasRightPart)
                {
                    _textBox.SetBounds(0, tbY, cw, tbHeight);
                    _comboBox.Visible = false;
                    return;
                }

                int textWidth;
                int comboWidth;

                if (_useRightWidth)
                {
                    comboWidth = Math.Max(50, Math.Min(_rightWidth, Math.Max(50, cw - 50)));
                    textWidth = cw - comboWidth - gap;
                }
                else
                {
                    textWidth = Math.Max(40, Math.Min(_leftWidth, Math.Max(40, cw - 50)));
                    comboWidth = cw - textWidth - gap;
                }

                if (textWidth < 40)
                {
                    textWidth = Math.Max(40, (cw - gap) / 2);
                    comboWidth = cw - textWidth - gap;
                }

                if (comboWidth < 50)
                {
                    comboWidth = Math.Max(50, (cw - gap) / 2);
                    textWidth = cw - comboWidth - gap;
                }

                _textBox.SetBounds(0, tbY, textWidth, tbHeight);
                _comboBox.SetBounds(textWidth + gap, cbY, comboWidth, cbHeight);
                _comboBox.Visible = true;
            }
            finally
            {
                ResumeLayout();
            }
        }

        private void UpdateComboVisibility()
        {
            _comboBox.Visible = HasRightPart;
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

            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);

            if (AutoConvertOnUnitChange)
            {
                bool ok = TryConvertLeftTextOnUnitChange_WithResult(oldUnit, newUnit, out string? error);

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

                if (!TryGetCurrentValue(out currentValue))
                {
                    error = $"LeftText is not numeric: '{LeftText}'";
                    return false;
                }

                if (currentUnit.Equals(defaultUnit, StringComparison.OrdinalIgnoreCase))
                {
                    defaultValue = currentValue;
                    return true;
                }

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
    }
}