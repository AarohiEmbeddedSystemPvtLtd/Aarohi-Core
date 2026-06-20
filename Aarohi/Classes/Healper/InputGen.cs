using Aarohi.Classes;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace Aarohi.Classes.Healper
{
    public static class InputGen
    {
        public enum TextInputMode
        {
            TextBox = 0,
            ExtendedTextBox = 1
        }

        public sealed class InputTypeInfo
        {
            public string BaseType = "varchar";
            public int? MaxLen;
            public int? Precision;
            public int? Scale;

            public override string ToString()
            {
                if (MaxLen.HasValue)
                    return $"{BaseType}({MaxLen.Value})";

                if (Precision.HasValue && Scale.HasValue)
                    return $"{BaseType}({Precision.Value},{Scale.Value})";

                return BaseType ?? "varchar";
            }
        }

        public sealed class FieldMeta
        {
            public string Table = string.Empty;
            public string Column = string.Empty;
            public bool Required;
            public InputTypeInfo? TypeInfo;
            public Color OriginalBackColor;
            public object? OriginalTag;
            public string Parameter = string.Empty;
            public string Format = string.Empty;
            public string Unit = string.Empty;
        }

        public sealed class Context : IDisposable
        {
            private readonly Dictionary<string, Control> _inputs =
                new Dictionary<string, Control>(StringComparer.OrdinalIgnoreCase);

            private readonly Dictionary<Control, FieldMeta> _metaByControl =
                new Dictionary<Control, FieldMeta>();

            private readonly ErrorProvider _ep;
            private bool _disposed;

            public bool SuppressChange { get; set; }

            public Action<string, string, string>? OnInputChanged { get; set; }

            public IReadOnlyDictionary<string, Control> Inputs => _inputs;

            public TextInputMode TextMode { get; set; }

            internal Context(
                ContainerControl container,
                TextInputMode textMode = TextInputMode.ExtendedTextBox)
            {
                _ep = new ErrorProvider
                {
                    ContainerControl = container,
                    BlinkStyle = ErrorBlinkStyle.NeverBlink
                };

                TextMode = textMode;
            }

            #region Lifetime

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _ep.Dispose();
            }

            #endregion

            #region Key / Meta Helpers

            private static string K(string table, string col) =>
                $"{(table ?? string.Empty).Trim()}::{(col ?? string.Empty).Trim()}";

            private bool TryGetMeta(Control? c, out FieldMeta meta)
            {
                if (c != null && _metaByControl.TryGetValue(c, out meta!))
                    return true;

                meta = null!;
                return false;
            }

            public bool TryGetFieldMeta(Control ctrl, out FieldMeta? meta)
            {
                if (_metaByControl.TryGetValue(ctrl, out var found))
                {
                    meta = found;
                    return true;
                }

                meta = null;
                return false;
            }

            private static string GetControlValueText(Control c)
            {
                switch (c)
                {
                    case ExtendedTextBox etb:
                        return (etb.LeftText ?? string.Empty).Trim();

                    case ComboBox cb:
                        if (cb.SelectedValue != null && cb.SelectedValue != DBNull.Value)
                            return Convert.ToString(cb.SelectedValue, CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;

                        return (cb.SelectedItem?.ToString() ?? cb.Text ?? string.Empty).Trim();

                    default:
                        return (c.Text ?? string.Empty).Trim();
                }
            }

            private static int FindItemIndex(ComboBox cb, string value)
            {
                value = (value ?? string.Empty).Trim();

                for (int i = 0; i < cb.Items.Count; i++)
                {
                    if (string.Equals(cb.Items[i]?.ToString()?.Trim(), value, StringComparison.OrdinalIgnoreCase))
                        return i;
                }

                return -1;
            }

            private static string SafeToText(object? value)
            {
                if (value == null || value == DBNull.Value)
                    return string.Empty;

                return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            }

            private static List<string> SplitUnits(string? unitCsv)
            {
                return (unitCsv ?? string.Empty)
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            private static List<string> ResolveUnits(string? unitCsv, string? parameter)
            {
                List<string> units = SplitUnits(unitCsv);
                if (units.Count > 0)
                    return units;

                if (!string.IsNullOrWhiteSpace(parameter))
                {
                    try
                    {
                        units = UnitConverisonEngine.GetUnitsFromParameter(parameter);
                    }
                    catch
                    {
                        // Keep control creation resilient even if mapping is missing.
                    }
                }

                return units;
            }

            private static string ResolveQuantityName(string column, string? parameter)
            {
                return !string.IsNullOrWhiteSpace(parameter)
                    ? parameter.Trim()
                    : (column ?? string.Empty).Trim();
            }

            private static void ConfigureExtendedTextBoxConversion(ExtendedTextBox etb, FieldMeta meta)
            {
                if (etb == null) return;

                List<string> units = ResolveUnits(meta.Unit, meta.Parameter);
                etb.SetUnits(units);

                string format = string.IsNullOrWhiteSpace(meta.Format) ? "0.###" : meta.Format;

                etb.AutoConvertOnUnitChange = true;
                etb.AutoLoadUnitsFromParameter = false;
                etb.ShowConversionErrorMessageBox = true;

                if (units.Count > 0)
                {
                    string defaultUnit = units[0];
                    string quantityName = ResolveQuantityName(meta.Column, meta.Parameter);
                    etb.SetConversionContext(quantityName, meta.Parameter, defaultUnit, format);
                }
                else
                {
                    // Plain numeric field, no unit conversion context
                    etb.SetConversionContext(null, null, null, format);
                }
            }

            #endregion

            #region Register / Get Controls

            private static InputTypeInfo? BuildTypeInfo(string? dataType, int? precision, int? scale)
            {
                if (string.IsNullOrWhiteSpace(dataType))
                    return null;

                InputTypeInfo info = ParseSqlType(dataType);

                if (precision.HasValue)
                    info.Precision = precision.Value;

                if (scale.HasValue)
                    info.Scale = scale.Value;

                return info;
            }

            public void RegisterExistingControl(string table, string col, Control ctrl, bool required, string? dataType = null)
            {
                RegisterExistingControl(table, col, ctrl, required, dataType, unit: null, parameter: null, format: null, precision: null, scale: null);
            }

            public void RegisterExistingControl(
                string table,
                string col,
                Control ctrl,
                bool required,
                string? dataType,
                string? unit,
                string? parameter,
                string? format,
                int? precision = null,
                int? scale = null)
            {
                if (ctrl == null) return;

                string key = K(table, col);
                DetachHandlers(ctrl);

                var meta = new FieldMeta
                {
                    Table = table ?? string.Empty,
                    Column = col ?? string.Empty,
                    Required = required,
                    TypeInfo = (ctrl is ExtendedTextBox || ctrl is TextBox)
                        ? BuildTypeInfo(dataType, precision, scale)
                        : null,
                    OriginalBackColor = ctrl.BackColor,
                    OriginalTag = ctrl.Tag,
                    Unit = unit ?? string.Empty,
                    Parameter = parameter ?? string.Empty,
                    Format = format ?? string.Empty
                };

                _metaByControl[ctrl] = meta;
                _inputs[key] = ctrl;

                if (ctrl is ExtendedTextBox etb)
                {
                    ConfigureExtendedTextBoxForType(etb, meta.TypeInfo);
                    ConfigureExtendedTextBoxConversion(etb, meta);
                    AttachExtendedTextBoxNotify(etb);
                    ValidateControl(etb);
                }
                else if (ctrl is TextBox tb)
                {
                    ConfigureTextBoxForType(tb, meta.TypeInfo);
                    AttachTextBoxNotify(tb);
                    ValidateControl(tb);
                }
                else if (ctrl is ComboBox cb)
                {
                    AttachComboNotify(cb);
                    ValidateControl(cb);
                }
                else
                {
                    ctrl.TextChanged -= GenericControl_TextChanged;
                    ctrl.TextChanged += GenericControl_TextChanged;
                    ValidateControl(ctrl);
                }
            }

            private void GenericControl_TextChanged(object? sender, EventArgs e)
            {
                if (SuppressChange || sender is not Control ctrl) return;

                ValidateControl(ctrl);
                NotifyChanged(ctrl);
            }

            public Control? GetControl(string table, string col) =>
                _inputs.TryGetValue(K(table, col), out var c) ? c : null;

            public TextBox? GetTextBox(string table, string col) =>
                _inputs.TryGetValue(K(table, col), out var c) ? c as TextBox : null;

            public ExtendedTextBox? GetExtendedTextBox(string table, string col) =>
                _inputs.TryGetValue(K(table, col), out var c) ? c as ExtendedTextBox : null;

            public ComboBox? GetComboBox(string table, string col) =>
                _inputs.TryGetValue(K(table, col), out var c) ? c as ComboBox : null;

            public ComboBox? GetDropDown(string table, string col) =>
                _inputs.TryGetValue(K(table, col), out var c) ? c as ComboBox : null;

            public Control? GetTextInput(string table, string col)
            {
                var c = GetControl(table, col);
                return c is TextBox || c is ExtendedTextBox ? c : null;
            }

            public Control? GetDropDownInput(string table, string col)
            {
                var c = GetControl(table, col);
                return c is ComboBox || c is ExtendedTextBox ? c : null;
            }

            #endregion

            #region Event Wiring

            private void DetachHandlers(Control ctrl)
            {
                switch (ctrl)
                {
                    case ExtendedTextBox etb:
                        etb.LeftTextChanged -= ExtendedTextBox_LeftTextChanged;
                        etb.Validating -= ExtendedTextBox_Validating;
                        break;

                    case TextBox tb:
                        tb.GotFocus -= TextBox_GotFocus;
                        tb.Enter -= TextBox_Enter;
                        tb.MouseDown -= TextBox_MouseDown;
                        tb.TextChanged -= TextBox_TextChanged;
                        tb.Validating -= TextBox_Validating;
                        tb.KeyPress -= TextBox_KeyPress_ByType;
                        break;

                    case ComboBox cb:
                        cb.SelectedIndexChanged -= ComboBox_SelectedIndexChanged;
                        cb.TextChanged -= ComboBox_TextChanged;
                        break;
                }

                ctrl.TextChanged -= GenericControl_TextChanged;
            }

            private void AttachExtendedTextBoxNotify(ExtendedTextBox etb)
            {
                etb.LeftTextChanged -= ExtendedTextBox_LeftTextChanged;
                etb.LeftTextChanged += ExtendedTextBox_LeftTextChanged;

                etb.Validating -= ExtendedTextBox_Validating;
                etb.Validating += ExtendedTextBox_Validating;
            }

            private void ExtendedTextBox_LeftTextChanged(object? sender, EventArgs e)
            {
                if (SuppressChange || sender is not ExtendedTextBox etb) return;

                if (TryGetMeta(etb, out var meta))
                    EnforceMaxLen(etb, meta);

                ValidateControl(etb);
                NotifyChanged(etb);
            }

            private void ExtendedTextBox_Validating(object? sender, CancelEventArgs e)
            {
                if (sender is ExtendedTextBox etb)
                    ValidateTypeOnly(etb);
            }

            private void AttachTextBoxNotify(TextBox tb)
            {
                tb.GotFocus -= TextBox_GotFocus;
                tb.GotFocus += TextBox_GotFocus;

                tb.Enter -= TextBox_Enter;
                tb.Enter += TextBox_Enter;

                tb.MouseDown -= TextBox_MouseDown;
                tb.MouseDown += TextBox_MouseDown;

                tb.TextChanged -= TextBox_TextChanged;
                tb.TextChanged += TextBox_TextChanged;

                tb.Validating -= TextBox_Validating;
                tb.Validating += TextBox_Validating;
            }

            private void TextBox_GotFocus(object? sender, EventArgs e)
            {
                if (sender is TextBox tb && !tb.ReadOnly)
                    tb.SelectAll();
            }

            private void TextBox_Enter(object? sender, EventArgs e)
            {
                if (sender is TextBox tb && !tb.ReadOnly)
                    tb.BeginInvoke((Action)(() => tb.SelectAll()));
            }

            private void TextBox_MouseDown(object? sender, MouseEventArgs e)
            {
                if (sender is TextBox tb && !tb.ReadOnly && !tb.Focused)
                    tb.BeginInvoke((Action)(() => tb.SelectAll()));
            }

            private void TextBox_TextChanged(object? sender, EventArgs e)
            {
                if (SuppressChange || sender is not TextBox tb) return;

                if (TryGetMeta(tb, out var meta))
                    EnforceMaxLen(tb, meta);

                ValidateControl(tb);
                NotifyChanged(tb);
            }

            private void TextBox_Validating(object? sender, CancelEventArgs e)
            {
                if (sender is TextBox tb)
                    ValidateTypeOnly(tb);
            }

            private void AttachComboNotify(ComboBox cb)
            {
                cb.SelectedIndexChanged -= ComboBox_SelectedIndexChanged;
                cb.SelectedIndexChanged += ComboBox_SelectedIndexChanged;

                cb.TextChanged -= ComboBox_TextChanged;
                cb.TextChanged += ComboBox_TextChanged;
            }

            private void ComboBox_SelectedIndexChanged(object? sender, EventArgs e)
            {
                if (SuppressChange || sender is not ComboBox cb) return;

                ValidateControl(cb);
                NotifyChanged(cb);
            }

            private void ComboBox_TextChanged(object? sender, EventArgs e)
            {
                if (SuppressChange || sender is not ComboBox cb) return;

                ValidateControl(cb);
                NotifyChanged(cb);
            }

            #endregion

            #region Configure by Type

            private void ConfigureExtendedTextBoxForType(ExtendedTextBox etb, InputTypeInfo? info)
            {
                etb.LeftEditable = true;

                if (info == null)
                {
                    etb.LeftNumericOnly = false;
                    return;
                }

                string t = NormalizeType(info.BaseType);

                if (IsIntegerType(t))
                {
                    etb.LeftNumericOnly = true;
                    etb.LeftAllowDecimal = false;
                }
                else if (IsDecimalType(t))
                {
                    etb.LeftNumericOnly = true;
                    etb.LeftAllowDecimal = true;
                }
                else
                {
                    etb.LeftNumericOnly = false;
                }
            }

            private void ConfigureTextBoxForType(TextBox tb, InputTypeInfo? info)
            {
                tb.KeyPress -= TextBox_KeyPress_ByType;

                if (info == null)
                {
                    tb.MaxLength = 0;
                    return;
                }

                string t = NormalizeType(info.BaseType);

                if (IsTextType(t) && info.MaxLen.HasValue && info.MaxLen.Value > 0)
                    tb.MaxLength = info.MaxLen.Value;
                else
                    tb.MaxLength = 0;

                if (IsIntegerType(t) || IsDecimalType(t))
                    tb.KeyPress += TextBox_KeyPress_ByType;
            }

            private void TextBox_KeyPress_ByType(object? sender, KeyPressEventArgs e)
            {
                if (sender is not TextBox tb || !TryGetMeta(tb, out var meta) || meta.TypeInfo == null)
                    return;

                string t = NormalizeType(meta.TypeInfo.BaseType);

                if (char.IsControl(e.KeyChar))
                    return;

                if (IsIntegerType(t))
                {
                    if (char.IsDigit(e.KeyChar))
                        return;

                    if (e.KeyChar == '-' && tb.SelectionStart == 0 && !tb.Text.Contains("-"))
                        return;

                    e.Handled = true;
                    return;
                }

                if (IsDecimalType(t))
                {
                    if (char.IsDigit(e.KeyChar))
                        return;

                    if (e.KeyChar == '-' && tb.SelectionStart == 0 && !tb.Text.Contains("-"))
                        return;

                    char decimalSeparator = '.';
                    if (e.KeyChar == decimalSeparator)
                    {
                        if (tb.Text.Contains(decimalSeparator.ToString()))
                        {
                            e.Handled = true;
                            return;
                        }

                        return;
                    }

                    e.Handled = true;
                }
            }

            #endregion

            #region Validation

            private void NotifyChanged(Control ctrl)
            {
                if (!TryGetMeta(ctrl, out var meta)) return;
                OnInputChanged?.Invoke(meta.Table, meta.Column, GetControlValueText(ctrl));
            }

            private void ValidateControl(Control ctrl)
            {
                if (!TryGetMeta(ctrl, out var meta)) return;

                if (meta.Required && IsEmpty(ctrl))
                {
                    MarkInvalid(ctrl, "Required");
                    return;
                }

                if (!meta.Required && IsEmpty(ctrl))
                {
                    ClearInvalid(ctrl);
                    return;
                }

                ValidateTypeOnly(ctrl);
            }

            public void ValidateRequiredLive(Control c)
            {
                ValidateControl(c);
            }

            private void ValidateTypeOnly(Control ctrl)
            {
                if (!TryGetMeta(ctrl, out var meta)) return;

                if (meta.TypeInfo == null)
                {
                    ClearInvalid(ctrl);
                    return;
                }

                string raw = GetControlValueText(ctrl);
                if (string.IsNullOrWhiteSpace(raw))
                {
                    if (meta.Required)
                        MarkInvalid(ctrl, "Required");
                    else
                        ClearInvalid(ctrl);

                    return;
                }

                string t = NormalizeType(meta.TypeInfo.BaseType);

                if (IsIntegerType(t))
                {
                    if (!TryValidateInteger(raw, t, out string msg))
                        MarkInvalid(ctrl, msg);
                    else
                        ClearInvalid(ctrl);

                    return;
                }

                if (IsDecimalType(t))
                {
                    if (!TryValidateDecimal(raw, meta.TypeInfo, out string msg))
                        MarkInvalid(ctrl, msg);
                    else
                        ClearInvalid(ctrl);

                    return;
                }

                if (t == "bit")
                {
                    if (!TryParseBit(raw, out _))
                        MarkInvalid(ctrl, "Enter true/false or 1/0");
                    else
                        ClearInvalid(ctrl);

                    return;
                }

                if (IsDateType(t))
                {
                    if (!DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out _)
                        && !DateTime.TryParse(raw, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out _))
                    {
                        MarkInvalid(ctrl, "Enter a valid date/time");
                    }
                    else
                    {
                        ClearInvalid(ctrl);
                    }

                    return;
                }

                if (IsTextType(t) && meta.TypeInfo.MaxLen.HasValue && raw.Length > meta.TypeInfo.MaxLen.Value)
                {
                    MarkInvalid(ctrl, $"Maximum {meta.TypeInfo.MaxLen.Value} characters allowed");
                    return;
                }

                ClearInvalid(ctrl);
            }

            private static bool TryValidateInteger(string raw, string sqlType, out string message)
            {
                message = string.Empty;

                if (!long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out long lv))
                {
                    message = "Enter a valid integer";
                    return false;
                }

                switch (sqlType)
                {
                    case "tinyint":
                        if (lv < byte.MinValue || lv > byte.MaxValue)
                        {
                            message = $"Range: {byte.MinValue} to {byte.MaxValue}";
                            return false;
                        }
                        break;

                    case "smallint":
                        if (lv < short.MinValue || lv > short.MaxValue)
                        {
                            message = $"Range: {short.MinValue} to {short.MaxValue}";
                            return false;
                        }
                        break;

                    case "int":
                        if (lv < int.MinValue || lv > int.MaxValue)
                        {
                            message = $"Range: {int.MinValue} to {int.MaxValue}";
                            return false;
                        }
                        break;
                }

                return true;
            }

            private static bool TryValidateDecimal(string raw, InputTypeInfo info, out string message)
            {
                message = string.Empty;

                if (raw.Equals("Infinity", StringComparison.OrdinalIgnoreCase) ||
                    raw.Equals("+Infinity", StringComparison.OrdinalIgnoreCase) ||
                    raw.Equals("-Infinity", StringComparison.OrdinalIgnoreCase) ||
                    raw.Equals("NaN", StringComparison.OrdinalIgnoreCase))
                {
                    message = "Invalid numeric value";
                    return false;
                }

                if (!decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
                {
                    message = "Enter a valid decimal";
                    return false;
                }

                string normalized = raw.Trim();
                if (normalized.StartsWith("-", StringComparison.Ordinal))
                    normalized = normalized.Substring(1);

                string[] parts = normalized.Split('.');
                int digitsBefore = parts[0].Replace(",", string.Empty).Length;
                int digitsAfter = parts.Length > 1 ? parts[1].Length : 0;
                int totalDigits = digitsBefore + digitsAfter;

                if (info.Scale.HasValue && digitsAfter > info.Scale.Value)
                {
                    message = $"Only {info.Scale.Value} digits allowed after decimal";
                    return false;
                }

                if (info.Precision.HasValue && totalDigits > info.Precision.Value)
                {
                    message = $"Maximum {info.Precision.Value} digits allowed";
                    return false;
                }

                if (info.Precision.HasValue && info.Scale.HasValue)
                {
                    int maxIntegerDigits = info.Precision.Value - info.Scale.Value;
                    if (digitsBefore > maxIntegerDigits)
                    {
                        message = $"Maximum {maxIntegerDigits} digits allowed before decimal";
                        return false;
                    }
                }

                return true;
            }

            public bool ValidateAllRequired()
            {
                bool ok = true;

                foreach (var ctrl in _inputs.Values.Where(x => x != null))
                {
                    ValidateControl(ctrl);
                    if (_ep.GetError(ctrl).Length > 0)
                        ok = false;
                }

                return ok;
            }

            private static bool IsEmpty(Control c)
            {
                return string.IsNullOrWhiteSpace(GetControlValueText(c));
            }

            private void ApplyInvalidAppearance(Control c)
            {
                c.BackColor = Color.MistyRose;
            }

            private void RestoreOriginalAppearance(Control c, FieldMeta m)
            {
                c.BackColor = m.OriginalBackColor;
            }

            private void MarkInvalid(Control c, string message)
            {
                if (!TryGetMeta(c, out _)) return;

                _ep.SetError(c, message ?? "Invalid");
                ApplyInvalidAppearance(c);
            }

            private void ClearInvalid(Control c)
            {
                if (!TryGetMeta(c, out var meta)) return;

                _ep.SetError(c, string.Empty);
                RestoreOriginalAppearance(c, meta);
            }

            #endregion

            #region Rule Firing

            public bool FireRule(string table, string col)
            {
                if (OnInputChanged == null) return false;

                if (!_inputs.TryGetValue(K(table, col), out var c) || c == null)
                    return false;

                bool prev = SuppressChange;
                try
                {
                    SuppressChange = false;
                    NotifyChanged(c);
                    return true;
                }
                finally
                {
                    SuppressChange = prev;
                }
            }

            public void FireRulesInOrder(string table, params IEnumerable<string>[] candidateLists)
            {
                if (candidateLists == null || candidateLists.Length == 0) return;

                foreach (var list in candidateLists)
                {
                    if (list == null) continue;

                    foreach (var col in list)
                    {
                        if (string.IsNullOrWhiteSpace(col)) continue;

                        if (_inputs.ContainsKey(K(table, col)))
                        {
                            FireRule(table, col);
                            break;
                        }
                    }
                }
            }

            public void FireRulesForAllFilled(string table, bool onlyFilled = true)
            {
                if (OnInputChanged == null) return;

                bool prev = SuppressChange;
                try
                {
                    SuppressChange = false;

                    foreach (var kv in _inputs)
                    {
                        if (!kv.Key.StartsWith((table ?? string.Empty) + "::", StringComparison.OrdinalIgnoreCase))
                            continue;

                        var ctrl = kv.Value;
                        if (ctrl == null) continue;

                        string val = GetControlValueText(ctrl);
                        if (onlyFilled && string.IsNullOrWhiteSpace(val))
                            continue;

                        NotifyChanged(ctrl);
                    }
                }
                finally
                {
                    SuppressChange = prev;
                }
            }

            #endregion

            #region Get / Set Values

            public object GetValueObject(string table, string col)
            {
                if (_inputs.TryGetValue(K(table, col), out var c) == false || c == null)
                    return DBNull.Value;

                if (!TryGetMeta(c, out var meta) || meta.TypeInfo == null)
                {
                    string plainRaw = GetControlValueText(c);
                    return string.IsNullOrWhiteSpace(plainRaw) ? DBNull.Value : plainRaw;
                }

                string t = NormalizeType(meta.TypeInfo.BaseType);
                string raw = GetControlValueText(c);

                if (string.IsNullOrWhiteSpace(raw))
                    return DBNull.Value;

                // Only use unit conversion for ExtendedTextBox when it really has a valid unit context
                if (c is ExtendedTextBox etb && (IsIntegerType(t) || IsDecimalType(t) || t == "float" || t == "real"))
                {
                    bool hasUnitContext =
                        !string.IsNullOrWhiteSpace(etb.QuantityName) &&
                        !string.IsNullOrWhiteSpace(etb.DefaultUnit) &&
                        !string.IsNullOrWhiteSpace(etb.CurrentUnit);

                    if (hasUnitContext)
                    {
                        if (etb.TryGetBothValues(
                                out double currentValue,
                                out string currentUnit,
                                out double defaultValue,
                                out string defaultUnit,
                                out string? error))
                        {
                            raw = defaultValue.ToString(CultureInfo.InvariantCulture);
                        }
                        else
                        {
                            // Fallback to visible numeric text instead of returning NULL
                            raw = GetControlValueText(c);
                        }
                    }
                    else
                    {
                        // Plain numeric field with no unit system
                        raw = GetControlValueText(c);
                    }
                }

                if (string.IsNullOrWhiteSpace(raw))
                    return DBNull.Value;

                if (IsIntegerType(t))
                {
                    if (!long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out long lv))
                    {
                        if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal decLv))
                            lv = Convert.ToInt64(Math.Round(decLv, 0, MidpointRounding.AwayFromZero));
                        else
                            return DBNull.Value;
                    }

                    return t switch
                    {
                        "int" => lv >= int.MinValue && lv <= int.MaxValue ? (object)(int)lv : DBNull.Value,
                        "smallint" => lv >= short.MinValue && lv <= short.MaxValue ? (object)(short)lv : DBNull.Value,
                        "tinyint" => lv >= byte.MinValue && lv <= byte.MaxValue ? (object)(byte)lv : DBNull.Value,
                        _ => lv
                    };
                }

                if (t == "decimal" || t == "numeric" || t == "money" || t == "smallmoney")
                {
                    if (!decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal dv))
                        return DBNull.Value;

                    if (meta.TypeInfo.Scale.HasValue)
                        dv = Math.Round(dv, meta.TypeInfo.Scale.Value, MidpointRounding.AwayFromZero);

                    return dv;
                }

                if (t == "float")
                {
                    if (!double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out double d))
                        return DBNull.Value;

                    if (meta.TypeInfo.Scale.HasValue)
                        d = Math.Round(d, meta.TypeInfo.Scale.Value, MidpointRounding.AwayFromZero);

                    return d;
                }

                if (t == "real")
                {
                    if (!float.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out float f))
                        return DBNull.Value;

                    if (meta.TypeInfo.Scale.HasValue)
                        f = (float)Math.Round(f, meta.TypeInfo.Scale.Value, MidpointRounding.AwayFromZero);

                    return f;
                }

                if (t == "bit")
                {
                    return TryParseBit(raw, out bool b) ? b : DBNull.Value;
                }

                if (IsDateType(t))
                {
                    if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out DateTime dt) ||
                        DateTime.TryParse(raw, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out dt))
                        return dt;

                    return DBNull.Value;
                }

                if (t == "uniqueidentifier")
                {
                    return Guid.TryParse(raw, out Guid g) ? g : DBNull.Value;
                }

                return raw;
            }

            public bool SetTextBoxValue(string table, string col, string? value, bool triggerRules)
            {
                var ctrl = GetTextInput(table, col);
                if (ctrl == null) return false;

                string newVal = value ?? string.Empty;

                bool prev = SuppressChange;
                SuppressChange = true;
                try
                {
                    switch (ctrl)
                    {
                        case ExtendedTextBox etb:
                            {
                                bool numericMode = false;

                                if (TryGetMeta(etb, out var m) && m.TypeInfo != null)
                                {
                                    string sqlType = NormalizeType(m.TypeInfo.BaseType);
                                    numericMode = IsIntegerType(sqlType) || IsDecimalType(sqlType) || sqlType == "float" || sqlType == "real";
                                }

                                if (numericMode)
                                    etb.SetLeftTextRaw(newVal, numericMode: true, treatNumericAsDefaultUnit: true);
                                else
                                    etb.LeftText = newVal;

                                break;
                            }

                        case TextBox tb:
                            tb.Text = newVal;
                            tb.SelectionStart = tb.TextLength;
                            break;

                        default:
                            return false;
                    }
                }
                finally
                {
                    SuppressChange = prev;
                }

                if (TryGetMeta(ctrl, out var meta))
                {
                    if (ctrl is ExtendedTextBox x1) EnforceMaxLen(x1, meta);
                    if (ctrl is TextBox x2) EnforceMaxLen(x2, meta);
                }

                ValidateControl(ctrl);
                if (triggerRules) NotifyChanged(ctrl);
                return true;
            }

            public bool SetComboValue(string table, string col, string? value, bool triggerRules)
            {
                var ctrl = GetDropDownInput(table, col);
                if (ctrl == null) return false;

                string newVal = (value ?? string.Empty).Trim();

                bool prev = SuppressChange;
                SuppressChange = true;
                try
                {
                    switch (ctrl)
                    {
                        case ComboBox cb:
                            if (cb.DropDownStyle == ComboBoxStyle.DropDownList)
                            {
                                cb.SelectedIndex = FindItemIndex(cb, newVal);
                            }
                            else
                            {
                                int idx = FindItemIndex(cb, newVal);
                                if (idx >= 0)
                                    cb.SelectedIndex = idx;
                                else
                                    cb.Text = newVal;
                            }
                            break;

                        case ExtendedTextBox etb:
                            if (!etb.SetCurrentUnit(newVal, convertCurrentValue: true))
                                return false;
                            break;

                        default:
                            return false;
                    }
                }
                finally
                {
                    SuppressChange = prev;
                }

                ValidateControl(ctrl);
                if (triggerRules) NotifyChanged(ctrl);
                return true;
            }

            public object? GetAnyValue(string table, string col)
            {
                var ctrl = GetControl(table, col);
                if (ctrl == null)
                    return null;

                if (ctrl is ExtendedTextBox etb)
                    return etb.LeftText ?? string.Empty;

                if (ctrl is TextBox tb)
                    return tb.Text ?? string.Empty;

                if (ctrl is ComboBox cb)
                    return cb.SelectedValue ?? cb.SelectedItem?.ToString() ?? cb.Text ?? string.Empty;

                return ctrl.Text ?? string.Empty;
            }

            public bool SetAnyValue(string table, string col, object? value, bool triggerRules = false)
            {
                string s = value == null || value == DBNull.Value
                    ? string.Empty
                    : Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;

                var ctrl = GetControl(table, col);
                if (ctrl == null)
                    return false;

                if (ctrl is ComboBox)
                    return SetComboValue(table, col, s, triggerRules);

                if (ctrl is TextBox || ctrl is ExtendedTextBox)
                    return SetTextBoxValue(table, col, s, triggerRules);

                bool prev = SuppressChange;
                SuppressChange = true;
                try
                {
                    ctrl.Text = s;
                }
                finally
                {
                    SuppressChange = prev;
                }

                ValidateControl(ctrl);
                if (triggerRules) NotifyChanged(ctrl);
                return true;
            }

            #endregion

            #region Max Length

            private void EnforceMaxLen(ExtendedTextBox etb, FieldMeta meta)
            {
                int? max = meta.TypeInfo?.MaxLen;
                if (!max.HasValue || max.Value <= 0) return;

                string t = etb.LeftText ?? string.Empty;
                if (t.Length <= max.Value) return;

                bool prev = SuppressChange;
                SuppressChange = true;
                try
                {
                    etb.LeftText = t.Substring(0, max.Value);
                }
                finally
                {
                    SuppressChange = prev;
                }
            }

            private void EnforceMaxLen(TextBox tb, FieldMeta meta)
            {
                int? max = meta.TypeInfo?.MaxLen;
                if (!max.HasValue || max.Value <= 0) return;

                string t = tb.Text ?? string.Empty;
                if (t.Length <= max.Value) return;

                bool prev = SuppressChange;
                SuppressChange = true;
                try
                {
                    tb.Text = t.Substring(0, max.Value);
                    tb.SelectionStart = tb.Text.Length;
                }
                finally
                {
                    SuppressChange = prev;
                }
            }

            #endregion

            #region UI Generator

            public Panel Gen(
    string table,
    string colName,
    string inputName,
    string Unit,
    string DefaultUnit,
    string SelectedUnit,
    string Parameter,
    string Format,
    bool required,
    Color titleColor,
    string dataType = "varchar",
    int? precision = null,
    int? scale = null,
    string[]? opt = null,
    object? defaultValue = null)
            {
                Panel panelHolder = new Panel
                {
                    BackColor = Color.Transparent,
                    Padding = new Padding(8, 8, 18, 8),
                    Location = new Point(9, 9),
                    Name = $"Panel_{table}_{colName}",
                    Size = new Size(260, 70),
                    TabIndex = 0
                };

                Label labelInput = new Label
                {
                    BackColor = Color.Transparent,
                    Dock = DockStyle.Fill,
                    Font = new Font("Gadugi", 12F, FontStyle.Bold, GraphicsUnit.Point, 0),
                    ForeColor = titleColor,
                    Margin = new Padding(0),
                    Name = $"Label_{table}_{colName}",
                    Size = new Size(249, 35),
                    TabIndex = 2,
                    Text = inputName + (required ? "*" : string.Empty),
                    TextAlign = ContentAlignment.MiddleLeft,
                    AutoEllipsis = true
                };

                panelHolder.Controls.Add(labelInput);

                if (opt != null)
                {
                    ComboBox cb = new ComboBox
                    {
                        Dock = DockStyle.Bottom,
                        Font = new Font("Gadugi", 12F, FontStyle.Regular, GraphicsUnit.Point, 0),
                        Name = $"CB_{table}_{colName}",
                        Size = new Size(249, 35),
                        TabIndex = 0,
                        DropDownStyle = ComboBoxStyle.DropDownList,
                        FormattingEnabled = true
                    };

                    cb.Items.Clear();
                    foreach (var s in opt)
                        cb.Items.Add(s ?? string.Empty);

                    cb.SelectedIndex = -1;

                    if (!IsNullOrWhite(defaultValue))
                    {
                        string dv = SafeToText(defaultValue).Trim();
                        cb.SelectedIndex = FindItemIndex(cb, dv);
                    }

                    panelHolder.Controls.Add(cb);
                    RegisterExistingControl(table, colName, cb, required, null, Unit, Parameter, Format, precision, scale);
                }
                else
                {
                    if (TextMode == TextInputMode.TextBox)
                    {
                        TextBox tb = new TextBox
                        {
                            Dock = DockStyle.Bottom,
                            Font = new Font("Gadugi", 14.0F, FontStyle.Regular, GraphicsUnit.Point, 0),
                            Name = $"TB_{table}_{colName}",
                            Size = new Size(249, 35),
                            TabIndex = 0,
                            Text = !IsNullOrWhite(defaultValue) ? SafeToText(defaultValue) : string.Empty
                        };

                        panelHolder.Controls.Add(tb);
                        RegisterExistingControl(table, colName, tb, required, dataType, Unit, Parameter, Format, precision, scale);
                    }
                    else
                    {
                        List<string> etbUnits = ResolveUnits(Unit, Parameter);

                        ExtendedTextBox etb = new ExtendedTextBox
                        {
                            Dock = DockStyle.Bottom,
                            Font = new Font("Gadugi", 15.75F, FontStyle.Regular, GraphicsUnit.Point, 0),
                            Name = $"ETB_{table}_{colName}",
                            Size = new Size(249, 35),
                            TabIndex = 0,
                            UseRightWidth = true,
                            RightWidth = 100,
                            LeftEditable = true,
                            LeftText = string.Empty,
                            AutoConvertOnUnitChange = true,
                            AutoLoadUnitsFromParameter = false,
                            NumberFormat = string.IsNullOrWhiteSpace(Format) ? "0.###" : Format
                        };

                        etb.SetUnits(etbUnits);

                        string actualDefaultUnit = string.IsNullOrWhiteSpace(DefaultUnit)
                            ? etbUnits.FirstOrDefault() ?? string.Empty
                            : DefaultUnit.Trim();

                        string actualSelectedUnit = string.IsNullOrWhiteSpace(SelectedUnit)
                            ? actualDefaultUnit
                            : SelectedUnit.Trim();

                        etb.SetConversionContext(
                            ResolveQuantityName(colName, Parameter),
                            Parameter,
                            actualDefaultUnit,
                            string.IsNullOrWhiteSpace(Format) ? "0.###" : Format);

                        bool isNumericType = false;
                        string normalizedDataType = NormalizeType(dataType);
                        isNumericType = IsIntegerType(normalizedDataType) || IsDecimalType(normalizedDataType) || normalizedDataType == "float" || normalizedDataType == "real";

                        // Set selected/display unit WITHOUT converting current text yet
                        etb.SetCurrentUnit(actualSelectedUnit, false);

                        // Now load DB/default value and convert it for display
                        SetExtendedTextBoxValueFromDefaultUnit(etb, defaultValue, isNumericType);

                        panelHolder.Controls.Add(etb);
                        RegisterExistingControl(table, colName, etb, required, dataType, string.Join(",", etbUnits), Parameter, Format, precision, scale);

                        // Re-apply context because RegisterExistingControl configures ETB again
                        etb.SetConversionContext(
                            ResolveQuantityName(colName, Parameter),
                            Parameter,
                            actualDefaultUnit,
                            string.IsNullOrWhiteSpace(Format) ? "0.###" : Format);

                        etb.SetCurrentUnit(actualSelectedUnit, false);
                        SetExtendedTextBoxValueFromDefaultUnit(etb, defaultValue, isNumericType);
                    }
                }

                return panelHolder;
            }

            private void SetExtendedTextBoxValueFromDefaultUnit(ExtendedTextBox etb, object? value, bool numericMode)
            {
                if (etb == null)
                    return;

                if (value == null || value == DBNull.Value)
                {
                    etb.LeftText = string.Empty;
                    return;
                }

                string text = SafeToText(value).Trim();
                if (string.IsNullOrWhiteSpace(text))
                {
                    etb.LeftText = string.Empty;
                    return;
                }

                // Value coming from DB is assumed to be in DEFAULT UNIT for numeric fields.
                // For non-numeric fields we keep the raw text exactly as stored.
                etb.SetLeftTextRaw(text, numericMode: numericMode, treatNumericAsDefaultUnit: true);
            }

            private static bool IsNullOrWhite(object? v)
            {
                if (v == null || v == DBNull.Value)
                    return true;

                return v is string s && string.IsNullOrWhiteSpace(s);
            }

            public void BuildSection(
                string table,
                List<DynamicClass.ColumnInfo> cols,
                Control host,
                bool skipPumpModelCols = false,
                bool skipSetCols = false,
                Color titleColor = default)
            {
                if (cols == null || host == null) return;

                List<Control> controls = new List<Control>();

                foreach (var col in cols)
                {
                    if (col == null) continue;

                    if (string.Equals(col.Group?.Trim(), "Hidden", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (skipPumpModelCols)
                    {
                        if (col.Name.Equals("ModelId", StringComparison.OrdinalIgnoreCase) ||
                            col.Name.Equals("CreatedAt", StringComparison.OrdinalIgnoreCase) ||
                            col.Name.Equals("UpdatedAt", StringComparison.OrdinalIgnoreCase) ||
                            col.Name.Equals("IsActive", StringComparison.OrdinalIgnoreCase) ||
                            col.Name.Equals("ModelName", StringComparison.OrdinalIgnoreCase))
                            continue;
                    }

                    if (skipSetCols)
                    {
                        if (col.Name.Equals("Id", StringComparison.OrdinalIgnoreCase) ||
                            col.Name.Equals("Set_Name", StringComparison.OrdinalIgnoreCase))
                            continue;
                    }

                    string name = string.IsNullOrEmpty(col.DisplayName) ? col.Name : col.DisplayName;
                    string unit = string.IsNullOrEmpty(col.Unit) ? string.Empty : col.Unit;
                    string parameter = string.IsNullOrEmpty(col.Parameter) ? string.Empty : col.Parameter;
                    string format = string.IsNullOrEmpty(col.Format) ? string.Empty : col.Format;
                    string dt = string.IsNullOrWhiteSpace(col.DataType) ? "varchar" : col.DataType;
                    int? precision = col.Precision;
                    int? scale = col.Scale;
                    string resolvedUnit = string.Join(",", ResolveUnits(unit, parameter));

                    string defaultUnit = string.IsNullOrWhiteSpace(col.DefaultUnit)
                        ? ResolveUnits(unit, parameter).FirstOrDefault() ?? string.Empty
                        : col.DefaultUnit.Trim();

                    string selectedUnit = string.IsNullOrWhiteSpace(col.LastUsedUnit)
                        ? defaultUnit
                        : col.LastUsedUnit.Trim();

                    object? defaultValue = col.DefaultValue;

                    Control p = Gen(
                        table: table,
                        colName: col.Name,
                        inputName: name,
                        Unit: resolvedUnit,
                        DefaultUnit: defaultUnit,
                        SelectedUnit: selectedUnit,
                        Parameter: parameter,
                        Format: format,
                        required: !col.Nullable,
                        titleColor: titleColor,
                        dataType: dt,
                        precision: precision,
                        scale: scale,
                        opt: col.HasOptions ? col.Options : null,
                        defaultValue: defaultValue);

                    controls.Add(p);
                }

                host.SuspendLayout();
                try
                {
                    host.Controls.AddRange(controls.ToArray());
                }
                finally
                {
                    host.ResumeLayout();
                }
            }

            #endregion

            #region Last Used Unit Save
            public int SaveLastUsedUnits(DynamicClass dc, string? onlyTable = null)
            {
                if (dc == null)
                    throw new ArgumentNullException(nameof(dc));

                int updatedCount = 0;
                string targetTable = string.IsNullOrWhiteSpace(onlyTable) ? dc.Table : onlyTable;

                foreach (var kv in _metaByControl)
                {
                    if (kv.Key is not ExtendedTextBox etb)
                        continue;

                    FieldMeta meta = kv.Value;
                    if (meta == null)
                        continue;

                    if (string.IsNullOrWhiteSpace(meta.Column) || string.IsNullOrWhiteSpace(meta.Table))
                        continue;

                    if (!string.Equals(meta.Table, targetTable, StringComparison.OrdinalIgnoreCase))
                        continue;

                    string newLastUsedUnit = (etb.CurrentUnit ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(newLastUsedUnit))
                        continue;

                    dc.SetLastUsedUnit(meta.Column, newLastUsedUnit);
                    updatedCount++;
                }

                return updatedCount;
            }
            public bool SaveLastUsedUnit(string table, string col, DynamicClass dc)
            {
                if (dc == null)
                    throw new ArgumentNullException(nameof(dc));

                var ctrl = GetExtendedTextBox(table, col);
                if (ctrl == null)
                    return false;

                string newLastUsedUnit = (ctrl.CurrentUnit ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(newLastUsedUnit))
                    return false;

                dc.SetLastUsedUnit(col, newLastUsedUnit);
                return true;
            }

            #endregion

            #region Type Parsing Helpers

            private static InputTypeInfo ParseSqlType(string dataType)
            {
                string dt = NormalizeType(dataType);

                var mMax = Regex.Match(dt, @"^(n?varchar)\s*\(\s*max\s*\)$", RegexOptions.IgnoreCase);
                if (mMax.Success)
                {
                    return new InputTypeInfo
                    {
                        BaseType = mMax.Groups[1].Value.ToLowerInvariant(),
                        MaxLen = null
                    };
                }

                var mText = Regex.Match(dt, @"^(n?varchar|n?char|binary|varbinary)\s*\(\s*(\d+)\s*\)$", RegexOptions.IgnoreCase);
                if (mText.Success)
                {
                    return new InputTypeInfo
                    {
                        BaseType = mText.Groups[1].Value.ToLowerInvariant(),
                        MaxLen = int.Parse(mText.Groups[2].Value, CultureInfo.InvariantCulture)
                    };
                }

                var mDec = Regex.Match(dt, @"^(decimal|numeric)\s*\(\s*(\d+)\s*,\s*(\d+)\s*\)$", RegexOptions.IgnoreCase);
                if (mDec.Success)
                {
                    return new InputTypeInfo
                    {
                        BaseType = mDec.Groups[1].Value.ToLowerInvariant(),
                        Precision = int.Parse(mDec.Groups[2].Value, CultureInfo.InvariantCulture),
                        Scale = int.Parse(mDec.Groups[3].Value, CultureInfo.InvariantCulture)
                    };
                }

                return new InputTypeInfo { BaseType = dt };
            }

            private static string NormalizeType(string? dataType)
            {
                return (dataType ?? "varchar").Trim().ToLowerInvariant();
            }

            private static bool IsIntegerType(string t) =>
                t == "int" || t == "bigint" || t == "smallint" || t == "tinyint";

            private static bool IsDecimalType(string t) =>
                t == "decimal" || t == "numeric" || t == "float" || t == "real" || t == "money" || t == "smallmoney";

            private static bool IsTextType(string t) =>
                t == "varchar" || t == "nvarchar" || t == "char" || t == "nchar" || t == "text" || t == "ntext";

            private static bool IsDateType(string t) =>
                t == "date" || t == "datetime" || t == "datetime2" || t == "smalldatetime" || t == "time";

            private static bool TryParseBit(string raw, out bool value)
            {
                string x = (raw ?? string.Empty).Trim();

                if (x == "1" || x.Equals("true", StringComparison.OrdinalIgnoreCase) || x.Equals("yes", StringComparison.OrdinalIgnoreCase))
                {
                    value = true;
                    return true;
                }

                if (x == "0" || x.Equals("false", StringComparison.OrdinalIgnoreCase) || x.Equals("no", StringComparison.OrdinalIgnoreCase))
                {
                    value = false;
                    return true;
                }

                value = false;
                return false;
            }

            #endregion
        }

        public static Context CreateContext(
            ContainerControl container,
            TextInputMode textMode = TextInputMode.ExtendedTextBox)
        {
            return new Context(container, textMode);
        }
    }
}
