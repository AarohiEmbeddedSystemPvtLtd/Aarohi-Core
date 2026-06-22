using Aarohi.Classes;
using AarohiWpfControls.Controls.DropDown;
using AarohiWpfControls.Controls.TextBox;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using static Aarohi.Globals.AGLobals;
using ExtendedTextBox = AarohiWpfControls.Controls.TextBox.ExtendedTextBox;

namespace AarohiWpfControls.Helper_Classes
{
    public class WpfInputGen
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
            public Brush OriginalBackground = Brushes.Transparent;
            public Brush? OriginalChromeBackground;
            public object? OriginalTag;

            public string Parameter = string.Empty;
            public string Unit = string.Empty;

            // Format is intentionally removed from here.
            // NumberFormat is now resolved from UnitConverisonEngine.
            public string DefaultUnit = string.Empty;
            public string SelectedUnit = string.Empty;
        }

        public sealed class Context : IDisposable
        {
            private readonly Dictionary<string, FrameworkElement> _inputs =
                new Dictionary<string, FrameworkElement>(StringComparer.OrdinalIgnoreCase);

            private readonly Dictionary<FrameworkElement, FieldMeta> _metaByControl =
                new Dictionary<FrameworkElement, FieldMeta>();

            public bool SuppressChange { get; set; }
            public Action<string, string, string>? OnInputChanged { get; set; }
            public IReadOnlyDictionary<string, FrameworkElement> Inputs => _inputs;
            public TextInputMode TextMode { get; set; }

            internal Context(TextInputMode textMode = TextInputMode.ExtendedTextBox)
            {
                TextMode = textMode;
            }

            public void Dispose()
            {
            }

            private static string K(string table, string col)
            {
                return $"{(table ?? string.Empty).Trim()}::{(col ?? string.Empty).Trim()}";
            }

            private bool TryGetMeta(FrameworkElement c, out FieldMeta meta)
            {
                if (c != null && _metaByControl.TryGetValue(c, out meta!))
                    return true;

                meta = null!;
                return false;
            }

            private static string GetControlValueText(FrameworkElement c)
            {
                return c switch
                {
                    ExtendedTextBox etb => etb.LeftText?.Trim() ?? string.Empty,
                    GeneratedOptionDropDown gdd => gdd.ValueText,
                    GeneratedOptionComboBox gcb => gcb.ValueText,
                    ComboBox cb => (cb.SelectedValue?.ToString() ?? cb.Text ?? string.Empty).Trim(),
                    TextBox tb => tb.Text?.Trim() ?? string.Empty,
                    CheckBox cbx => cbx.IsChecked?.ToString() ?? "False",
                    _ => string.Empty
                };
            }

            #region Registration

            public void RegisterExistingControl(
                string table,
                string col,
                FrameworkElement ctrl,
                bool required,
                string? dataType = null,
                string? unit = null,
                string? parameter = null,
                string? defaultUnit = null,
                string? selectedUnit = null,
                int? precision = null,
                int? scale = null)
            {
                if (ctrl == null)
                    return;

                string key = K(table, col);

                var meta = new FieldMeta
                {
                    Table = table ?? string.Empty,
                    Column = col ?? string.Empty,
                    Required = required,
                    TypeInfo = (ctrl is ExtendedTextBox || ctrl is TextBox)
                        ? BuildTypeInfo(dataType, precision, scale)
                        : null,
                    OriginalBackground = (ctrl as Control)?.Background ?? Brushes.Transparent,
                    OriginalChromeBackground = FindNamedBorder(ctrl, "ControlChrome")?.Background,
                    Unit = unit ?? string.Empty,
                    Parameter = parameter ?? string.Empty,
                    DefaultUnit = defaultUnit ?? string.Empty,
                    SelectedUnit = string.IsNullOrWhiteSpace(selectedUnit)
                        ? (defaultUnit ?? string.Empty)
                        : selectedUnit.Trim()
                };

                _metaByControl[ctrl] = meta;
                _inputs[key] = ctrl;

                if (ctrl is ExtendedTextBox etb)
                {
                    ConfigureExtendedTextBox(etb, meta);

                    etb.LeftTextChanged += (s, e) =>
                    {
                        if (SuppressChange)
                            return;

                        ValidateControl(etb);
                        NotifyChanged(etb);
                    };

                    etb.SelectedIndexChanged += (s, e) =>
                    {
                        if (SuppressChange)
                            return;

                        meta.SelectedUnit = GetSelectedUnitSafe(etb, meta.SelectedUnit);
                        ApplyEngineNumberFormat(etb, meta);

                        ValidateControl(etb);
                        NotifyChanged(etb);
                    };
                }
                else if (ctrl is GeneratedOptionDropDown gdd)
                {
                    gdd.ValueChanged += (s, e) =>
                    {
                        if (SuppressChange)
                            return;

                        ValidateControl(gdd);
                        NotifyChanged(gdd);
                    };
                }
                else if (ctrl is TextBox tb)
                {
                    tb.TextChanged += (s, e) =>
                    {
                        if (SuppressChange)
                            return;

                        ValidateControl(tb);
                        NotifyChanged(tb);
                    };
                }
                else if (ctrl is ComboBox cb)
                {
                    cb.SelectionChanged += (s, e) =>
                    {
                        if (SuppressChange)
                            return;

                        ValidateControl(cb);
                        NotifyChanged(cb);
                    };

                    cb.AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler((s, e) =>
                    {
                        if (SuppressChange)
                            return;

                        ValidateControl(cb);
                        NotifyChanged(cb);
                    }));
                }
            }

            private void ConfigureExtendedTextBox(ExtendedTextBox etb, FieldMeta meta)
            {
                List<string> units = ResolveUnits(meta.Parameter);

                etb.SetUnits(units);
                etb.Items = new System.Collections.ObjectModel.ObservableCollection<string>(units);

                etb.QuantityName = ResolveQuantityName(meta.Column, meta.Parameter);
                etb.ParameterName = meta.Parameter;
                etb.DefaultUnit = meta.DefaultUnit;

                // Main change:
                // NumberFormat is not taken from col.Format anymore.
                // It is resolved from UnitConverisonEngine conversion table.
                ApplyEngineNumberFormat(etb, meta);

                if (meta.TypeInfo != null)
                {
                    string t = NormalizeType(meta.TypeInfo.BaseType);
                    etb.LeftNumericOnly = IsIntegerType(t) || IsDecimalType(t);
                    etb.LeftAllowDecimal = IsDecimalType(t);
                }
            }

            private static void ApplyEngineNumberFormat(ExtendedTextBox etb, FieldMeta meta)
            {
                string fromUnit = meta.DefaultUnit?.Trim() ?? string.Empty;

                string toUnit = string.IsNullOrWhiteSpace(meta.SelectedUnit)
                    ? fromUnit
                    : meta.SelectedUnit.Trim();

                etb.NumberFormat = UnitConverisonEngine.GetNumberFormat(
                    meta.Parameter,
                    fromUnit,
                    toUnit);
            }

            private static string GetSelectedUnitSafe(ExtendedTextBox etb, string fallback)
            {
                if (etb == null)
                    return fallback ?? string.Empty;

                string[] possiblePropertyNames =
                {
                    "SelectedUnit",
                    "SelectedUnitText",
                    "SelectedValue",
                    "SelectedText",
                    "SelectedItem",
                    "Unit",
                    "UnitText",
                    "RightText",
                    "ValueUnit"
                };

                foreach (string propertyName in possiblePropertyNames)
                {
                    var prop = etb.GetType().GetProperty(propertyName);

                    if (prop == null)
                        continue;

                    object? value = prop.GetValue(etb);
                    string text = Convert.ToString(value)?.Trim() ?? string.Empty;

                    if (!string.IsNullOrWhiteSpace(text))
                        return text;
                }

                return fallback ?? string.Empty;
            }

            #endregion

            #region Validation

            private void ValidateControl(FrameworkElement ctrl)
            {
                if (!TryGetMeta(ctrl, out var meta))
                    return;

                string raw = GetControlValueText(ctrl);

                if (meta.Required && string.IsNullOrWhiteSpace(raw))
                {
                    MarkInvalid(ctrl, "Required");
                    return;
                }

                if (!meta.Required && string.IsNullOrWhiteSpace(raw))
                {
                    ClearInvalid(ctrl);
                    return;
                }

                if (meta.TypeInfo != null)
                {
                    string msg;
                    string t = NormalizeType(meta.TypeInfo.BaseType);

                    if (IsIntegerType(t) && !TryValidateInteger(raw, t, out msg))
                    {
                        MarkInvalid(ctrl, msg);
                        return;
                    }

                    if (IsDecimalType(t) && !TryValidateDecimal(raw, meta.TypeInfo, out msg))
                    {
                        MarkInvalid(ctrl, msg);
                        return;
                    }
                }

                ClearInvalid(ctrl);
            }

            private void MarkInvalid(FrameworkElement c, string msg)
            {
                Border? chrome = FindNamedBorder(c, "ControlChrome");
                if (chrome != null)
                {
                    chrome.Background = Brushes.MistyRose;
                    chrome.BorderBrush = Brushes.IndianRed;
                    c.ToolTip = msg;
                    return;
                }

                if (c is Control ctrl)
                {
                    ctrl.Background = Brushes.MistyRose;
                    ctrl.BorderBrush = Brushes.IndianRed;
                    ctrl.ToolTip = msg;
                }
            }

            private void ClearInvalid(FrameworkElement c)
            {
                if (!TryGetMeta(c, out var meta))
                    return;

                Border? chrome = FindNamedBorder(c, "ControlChrome");
                if (chrome != null)
                {
                    if (meta.OriginalChromeBackground != null)
                    {
                        chrome.Background = meta.OriginalChromeBackground;
                    }
                    else
                    {
                        chrome.SetResourceReference(Border.BackgroundProperty, "PanelBgBrush");
                    }

                    chrome.SetResourceReference(Border.BorderBrushProperty, "AppBrush.Border");
                    c.ToolTip = null;
                    return;
                }

                if (c is Control ctrl)
                {
                    ctrl.Background = meta.OriginalBackground;
                    ctrl.ClearValue(Control.BorderBrushProperty);
                    ctrl.ToolTip = null;
                }
            }

            private static Border? FindNamedBorder(DependencyObject root, string name)
            {
                if (root == null)
                    return null;

                if (root is Border border && string.Equals(border.Name, name, StringComparison.Ordinal))
                    return border;

                int childCount = VisualTreeHelper.GetChildrenCount(root);

                for (int i = 0; i < childCount; i++)
                {
                    Border? match = FindNamedBorder(VisualTreeHelper.GetChild(root, i), name);

                    if (match != null)
                        return match;
                }

                return null;
            }

            private void NotifyChanged(FrameworkElement ctrl)
            {
                if (TryGetMeta(ctrl, out var meta))
                    OnInputChanged?.Invoke(meta.Table, meta.Column, GetControlValueText(ctrl));
            }

            public bool ValidateAllRequired()
            {
                bool ok = true;

                foreach (FrameworkElement ctrl in _inputs.Values)
                {
                    ValidateControl(ctrl);

                    if (TryGetMeta(ctrl, out var meta) &&
                        meta.Required &&
                        string.IsNullOrWhiteSpace(GetControlValueText(ctrl)))
                    {
                        ok = false;
                    }
                }

                return ok;
            }

            #endregion

            #region UI Generator

            public void BuildSection(
                string table,
                List<DynamicClass.ColumnInfo> cols,
                Panel host,
                bool skipPumpModelCols = false,
                bool skipSetCols = false,
                string titleResourceKey = "TextBrush")
            {
                if (cols == null || host == null)
                    return;

                List<FrameworkElement> controls = new List<FrameworkElement>();

                foreach (var col in cols)
                {
                    if (col == null)
                        continue;

                    if (string.Equals(col.Group?.Trim(), "Hidden", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (skipPumpModelCols)
                    {
                        if (col.Name.Equals("ModelId", StringComparison.OrdinalIgnoreCase) ||
                            col.Name.Equals("CreatedAt", StringComparison.OrdinalIgnoreCase) ||
                            col.Name.Equals("UpdatedAt", StringComparison.OrdinalIgnoreCase) ||
                            col.Name.Equals("IsActive", StringComparison.OrdinalIgnoreCase) ||
                            col.Name.Equals("ModelName", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }
                    }

                    if (skipSetCols)
                    {
                        if (col.Name.Equals("Id", StringComparison.OrdinalIgnoreCase) ||
                            col.Name.Equals("Set_Name", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }
                    }

                    string name = string.IsNullOrEmpty(col.DisplayName) ? col.Name : col.DisplayName;
                    string parameter = string.IsNullOrEmpty(col.Parameter) ? string.Empty : col.Parameter;
                    string dt = string.IsNullOrWhiteSpace(col.DataType) ? "varchar" : col.DataType;

                    int? precision = col.Precision;
                    int? scale = col.Scale;

                    List<string> unitList = ResolveUnits(parameter);
                    string resolvedUnit = string.Join(",", unitList);

                    string defaultUnit = string.IsNullOrWhiteSpace(col.DefaultUnit)
                        ? unitList.FirstOrDefault() ?? string.Empty
                        : col.DefaultUnit.Trim();

                    string selectedUnit = string.IsNullOrWhiteSpace(col.LastUsedUnit)
                        ? defaultUnit
                        : col.LastUsedUnit.Trim();

                    object? defaultValue = col.DefaultValue;
                    bool required = col.IsRequired == true || !col.Nullable;

                    FrameworkElement p = Gen(
                        table: table,
                        colName: col.Name,
                        inputName: name,
                        unit: resolvedUnit,
                        defUnit: defaultUnit,
                        selUnit: selectedUnit,
                        parameter: parameter,
                        required: required,
                        titleResourceKey: titleResourceKey,
                        dataType: dt,
                        precision: precision,
                        scale: scale,
                        opt: col.HasOptions ? col.Options : null,
                        defaultValue: defaultValue);

                    controls.Add(p);
                }

                foreach (var ctrl in controls)
                    host.Children.Add(ctrl);
            }

            public string GetBrushName(Brush brush)
            {
                if (brush is SolidColorBrush solidBrush)
                {
                    var property = typeof(Brushes).GetProperties()
                        .FirstOrDefault(p => ((SolidColorBrush)p.GetValue(null)).Color == solidBrush.Color);

                    return property?.Name ?? "Custom Color";
                }

                return "Not a SolidColorBrush";
            }

            public FrameworkElement Gen(
                string table,
                string colName,
                string inputName,
                string unit,
                string defUnit,
                string selUnit,
                string parameter,
                bool required,
                string titleResourceKey,
                string dataType = "varchar",
                int? precision = null,
                int? scale = null,
                string[]? opt = null,
                object? defaultValue = null)
            {
                StackPanel panel = new StackPanel
                {
                    Margin = new Thickness(10, 8, 10, 8),
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };

                TextBlock label = new TextBlock
                {
                    Text = inputName.Replace("_", " ") + (required ? "*" : ""),
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 13,
                    Margin = new Thickness(4, 0, 0, 6),
                    Opacity = 0.85
                };

                label.SetResourceReference(TextBlock.ForegroundProperty, titleResourceKey);
                panel.Children.Add(label);

                double controlHeight = 38;

                if (opt != null)
                {
                    GeneratedOptionDropDown cb = new GeneratedOptionDropDown
                    {
                        Height = controlHeight,
                        HorizontalAlignment = HorizontalAlignment.Stretch
                    };

                    cb.SetOptions(opt);
                    cb.SetInitialValue(defaultValue);

                    panel.Children.Add(cb);
                    RegisterExistingControl(table, colName, cb, required);
                }
                else if (TextMode == TextInputMode.TextBox)
                {
                    TextBox tb = new TextBox
                    {
                        Height = controlHeight,
                        Text = defaultValue?.ToString() ?? string.Empty,
                        VerticalContentAlignment = VerticalAlignment.Center,
                        Padding = new Thickness(8, 0, 5, 0),
                        HorizontalAlignment = HorizontalAlignment.Stretch
                    };

                    tb.SetResourceReference(TextBox.BackgroundProperty, "PanelBgBrush");
                    tb.SetResourceReference(TextBox.ForegroundProperty, "TextBrush");

                    panel.Children.Add(tb);

                    RegisterExistingControl(
                        table,
                        colName,
                        tb,
                        required,
                        dataType,
                        unit,
                        parameter,
                        defUnit,
                        selUnit,
                        precision,
                        scale);
                }
                else
                {
                    ExtendedTextBox etb = new ExtendedTextBox
                    {
                        Height = controlHeight,
                        HorizontalAlignment = HorizontalAlignment.Stretch
                    };

                    List<string> units = ResolveUnits(parameter);

                    etb.SetUnits(units);
                    etb.Items = new System.Collections.ObjectModel.ObservableCollection<string>(units);
                    etb.QuantityName = ResolveQuantityName(colName, parameter);
                    etb.ParameterName = parameter;
                    etb.DefaultUnit = defUnit;

                    // Format comes from UnitConverisonEngine.
                    etb.NumberFormat = UnitConverisonEngine.GetNumberFormat(
                        parameter,
                        defUnit,
                        string.IsNullOrWhiteSpace(selUnit) ? defUnit : selUnit);

                    etb.LeftText = string.Empty;

                    panel.Children.Add(etb);

                    RegisterExistingControl(
                        table,
                        colName,
                        etb,
                        required,
                        dataType,
                        unit,
                        parameter,
                        defUnit,
                        selUnit,
                        precision,
                        scale);

                    if (defaultValue != null && defaultValue != DBNull.Value)
                    {
                        SetValue(
                            table,
                            colName,
                            defaultValue,
                            validate: false,
                            notify: false);
                    }
                }

                return panel;
            }

            #endregion

            #region Shared Logic

            private static InputTypeInfo BuildTypeInfo(string? dt, int? p, int? s)
            {
                if (string.IsNullOrWhiteSpace(dt))
                    return null!;

                var info = ParseSqlType(dt);
                info.Precision = p;
                info.Scale = s;

                return info;
            }

            private static string NormalizeType(string? t)
            {
                return (t ?? "varchar").Trim().ToLowerInvariant();
            }

            private static bool IsIntegerType(string t)
            {
                return t == "int" ||
                       t == "bigint" ||
                       t == "smallint" ||
                       t == "tinyint";
            }

            private static bool IsDecimalType(string t)
            {
                return t == "decimal" ||
                       t == "numeric" ||
                       t == "float" ||
                       t == "real" ||
                       t == "money";
            }

            private static List<string> ResolveUnits(string? parameter)
            {
                if (string.IsNullOrWhiteSpace(parameter))
                    return new List<string>();

                return UnitConverisonEngine.GetUnitsFromParameter(parameter);
            }

            private static string ResolveQuantityName(string col, string? param)
            {
                return !string.IsNullOrWhiteSpace(param) ? param : col;
            }

            private static InputTypeInfo ParseSqlType(string dataType)
            {
                string dt = NormalizeType(dataType);

                var mText = Regex.Match(
                    dt,
                    @"^(n?varchar|n?char)\s*\(\s*(\d+)\s*\)$",
                    RegexOptions.IgnoreCase);

                if (mText.Success)
                {
                    return new InputTypeInfo
                    {
                        BaseType = mText.Groups[1].Value,
                        MaxLen = int.Parse(mText.Groups[2].Value)
                    };
                }

                return new InputTypeInfo
                {
                    BaseType = dt
                };
            }

            private static bool TryValidateInteger(string raw, string type, out string msg)
            {
                msg = string.Empty;

                if (!long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                {
                    msg = "Invalid Integer";
                    return false;
                }

                return true;
            }

            private static bool TryValidateDecimal(string raw, InputTypeInfo info, out string msg)
            {
                msg = string.Empty;

                if (!decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
                {
                    msg = "Invalid Decimal";
                    return false;
                }

                return true;
            }

            public string GetValue(string table, string column)
            {
                string key = K(table, column);

                if (_inputs.TryGetValue(key, out FrameworkElement ctrl))
                {
                    return GetValueTextInDefaultUnit(ctrl);
                }

                return string.Empty;
            }

            public bool TryGetValue(string table, string column, out string value)
            {
                value = string.Empty;

                string key = K(table, column);

                if (!_inputs.TryGetValue(key, out FrameworkElement ctrl))
                    return false;

                value = GetValueTextInDefaultUnit(ctrl);
                return true;
            }

            public FrameworkElement? GetControl(string table, string column)
            {
                string key = K(table, column);

                if (_inputs.TryGetValue(key, out FrameworkElement ctrl))
                    return ctrl;

                return null;
            }

            public Dictionary<string, string> GetAllValues()
            {
                Dictionary<string, string> values = new Dictionary<string, string>();

                foreach (var item in _inputs)
                {
                    FrameworkElement ctrl = item.Value;

                    if (TryGetMeta(ctrl, out FieldMeta meta))
                    {
                        string key = $"{meta.Table}.{meta.Column}";
                        values[key] = GetValueTextInDefaultUnit(ctrl);
                    }
                    else
                    {
                        values[item.Key] = GetValueTextInDefaultUnit(ctrl);
                    }
                }

                return values;
            }
            public bool SetValue(
    string table,
    string column,
    object? value,
    bool validate = true,
    bool notify = true)
            {
                string key = K(table, column);

                if (!_inputs.TryGetValue(key, out FrameworkElement ctrl))
                    return false;

                bool oldSuppress = SuppressChange;

                try
                {
                    SuppressChange = true;

                    FieldMeta? meta = null;
                    TryGetMeta(ctrl, out meta!);

                    SetControlValueText(ctrl, value, meta);

                    if (validate)
                        ValidateControl(ctrl);

                    if (notify)
                        NotifyChanged(ctrl);

                    return true;
                }
                finally
                {
                    SuppressChange = oldSuppress;
                }
            }

            private static void SetControlValueText(
                FrameworkElement ctrl,
                object? value,
                FieldMeta? meta = null)
            {
                string text = value?.ToString() ?? string.Empty;

                switch (ctrl)
                {
                    case ExtendedTextBox etb:
                        if (meta != null)
                        {
                            text = ConvertDefaultUnitValueToSelectedUnitText(etb, meta, text);
                        }

                        etb.LeftText = text;
                        break;

                    case TextBox tb:
                        tb.Text = text;
                        break;

                    case GeneratedOptionComboBox gcb:
                        SetCustomDropDownValue(gcb, text);
                        break;

                    case ComboBox cb:
                        SetComboBoxValue(cb, text);
                        break;

                    case CheckBox cbx:
                        cbx.IsChecked = ToBool(text);
                        break;

                    case GeneratedOptionDropDown gdd:
                        SetCustomDropDownValue(gdd, text);
                        break;
                }
            }

            private static string ConvertDefaultUnitValueToSelectedUnitText(
    ExtendedTextBox etb,
    FieldMeta meta,
    string rawText)
            {
                if (string.IsNullOrWhiteSpace(rawText))
                    return string.Empty;

                if (!double.TryParse(
                        rawText,
                        NumberStyles.Any,
                        CultureInfo.InvariantCulture,
                        out double baseValue))
                {
                    return rawText;
                }

                string fromUnit = meta.DefaultUnit?.Trim() ?? string.Empty;

                string toUnit = GetSelectedUnitSafe(etb, meta.SelectedUnit);

                if (string.IsNullOrWhiteSpace(toUnit))
                    toUnit = fromUnit;

                meta.SelectedUnit = toUnit;

                ApplyEngineNumberFormat(etb, meta);

                if (string.IsNullOrWhiteSpace(fromUnit) ||
                    string.IsNullOrWhiteSpace(toUnit) ||
                    string.Equals(fromUnit, toUnit, StringComparison.OrdinalIgnoreCase))
                {
                    return FormatNumberForTextBox(baseValue, etb.NumberFormat);
                }

                double convertedValue = UnitConverisonEngine.ConvertValue(
                    meta.Parameter,
                    baseValue,
                    fromUnit,
                    toUnit);

                return FormatNumberForTextBox(convertedValue, etb.NumberFormat);
            }

            private string GetValueTextInDefaultUnit(FrameworkElement ctrl)
            {
                string displayedText = GetControlValueText(ctrl);

                if (ctrl is not ExtendedTextBox etb ||
                    !TryGetMeta(ctrl, out FieldMeta meta) ||
                    string.IsNullOrWhiteSpace(displayedText))
                {
                    return displayedText;
                }

                if (!double.TryParse(
                        displayedText,
                        NumberStyles.Any,
                        CultureInfo.InvariantCulture,
                        out double displayedValue))
                {
                    return displayedText;
                }

                string defaultUnit = meta.DefaultUnit?.Trim() ?? string.Empty;
                string selectedUnit = GetSelectedUnitSafe(etb, meta.SelectedUnit);

                if (string.IsNullOrWhiteSpace(selectedUnit))
                    selectedUnit = defaultUnit;

                meta.SelectedUnit = selectedUnit;

                if (string.IsNullOrWhiteSpace(defaultUnit) ||
                    string.IsNullOrWhiteSpace(selectedUnit) ||
                    string.Equals(selectedUnit, defaultUnit, StringComparison.OrdinalIgnoreCase))
                {
                    return displayedText;
                }

                double defaultValue = UnitConverisonEngine.ConvertValue(
                    meta.Parameter,
                    displayedValue,
                    selectedUnit,
                    defaultUnit);

                return defaultValue.ToString("0.##########", CultureInfo.InvariantCulture);
            }

            private static string FormatNumberForTextBox(double value, string? numberFormat)
            {
                if (double.IsNaN(value) || double.IsInfinity(value))
                    return string.Empty;

                if (string.IsNullOrWhiteSpace(numberFormat))
                    return value.ToString("0.##########", CultureInfo.InvariantCulture);

                try
                {
                    return value.ToString(numberFormat, CultureInfo.InvariantCulture);
                }
                catch
                {
                    return value.ToString("0.##########", CultureInfo.InvariantCulture);
                }
            }

            private static void SetComboBoxValue(ComboBox cb, string text)
            {
                if (cb == null)
                    return;

                foreach (object item in cb.Items)
                {
                    if (string.Equals(item?.ToString(), text, StringComparison.OrdinalIgnoreCase))
                    {
                        cb.SelectedItem = item;
                        return;
                    }
                }

                cb.Text = text;
            }

            private static bool ToBool(string text)
            {
                if (bool.TryParse(text, out bool result))
                    return result;

                if (text == "1" || text.Equals("yes", StringComparison.OrdinalIgnoreCase) || text.Equals("true", StringComparison.OrdinalIgnoreCase))
                    return true;

                return false;
            }

            private static void SetCustomDropDownValue(object control, string text)
            {
                if (control == null)
                    return;

                // First try SetInitialValue(value)
                var method = control.GetType().GetMethod("SetInitialValue");
                if (method != null)
                {
                    method.Invoke(control, new object?[] { text });
                    return;
                }

                // Then try ValueText property
                var prop = control.GetType().GetProperty("ValueText");
                if (prop != null && prop.CanWrite)
                {
                    prop.SetValue(control, text);
                    return;
                }

                // Then try Text property
                var textProp = control.GetType().GetProperty("Text");
                if (textProp != null && textProp.CanWrite)
                {
                    textProp.SetValue(control, text);
                }
            }
            #endregion
        }

        public static Context CreateContext(TextInputMode textMode = TextInputMode.ExtendedTextBox)
        {
            return new Context(textMode);
        }
    }
}
