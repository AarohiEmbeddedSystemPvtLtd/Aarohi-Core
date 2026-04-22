using Aarohi.Classes;
using Aarohi.Wpf.Controls.Lib.Controls.TextBox;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using static Aarohi.Globals.AGLobals;
using ExtendedTextBox = Aarohi.Wpf.Controls.Lib.Controls.TextBox.ExtendedTextBox;

namespace Aarohi.Wpf.Controls.Lib.Helper_Classes
{
    public class WpfInputGen
    {
        public enum TextInputMode { TextBox = 0, ExtendedTextBox = 1 }

        public sealed class InputTypeInfo
        {
            public string BaseType = "varchar";
            public int? MaxLen;
            public int? Precision;
            public int? Scale;

            public override string ToString()
            {
                if (MaxLen.HasValue) return $"{BaseType}({MaxLen.Value})";
                if (Precision.HasValue && Scale.HasValue) return $"{BaseType}({Precision.Value},{Scale.Value})";
                return BaseType ?? "varchar";
            }
        }

        public sealed class FieldMeta
        {
            public string Table = string.Empty;
            public string Column = string.Empty;
            public bool Required;
            public InputTypeInfo? TypeInfo;
            public Brush OriginalBackground;
            public object? OriginalTag;
            public string Parameter = string.Empty;
            public string Format = string.Empty;
            public string Unit = string.Empty;
        }

        public sealed class Context : IDisposable
        {
            private readonly Dictionary<string, FrameworkElement> _inputs = new Dictionary<string, FrameworkElement>(StringComparer.OrdinalIgnoreCase);
            private readonly Dictionary<FrameworkElement, FieldMeta> _metaByControl = new Dictionary<FrameworkElement, FieldMeta>();

            public bool SuppressChange { get; set; }
            public Action<string, string, string>? OnInputChanged { get; set; }
            public IReadOnlyDictionary<string, FrameworkElement> Inputs => _inputs;
            public TextInputMode TextMode { get; set; }

            internal Context(TextInputMode textMode = TextInputMode.ExtendedTextBox)
            {
                TextMode = textMode;
            }

            public void Dispose() { }

            private static string K(string table, string col) => $"{(table ?? string.Empty).Trim()}::{(col ?? string.Empty).Trim()}";

            private bool TryGetMeta(FrameworkElement c, out FieldMeta meta)
            {
                if (c != null && _metaByControl.TryGetValue(c, out meta!)) return true;
                meta = null!;
                return false;
            }

            private static string GetControlValueText(FrameworkElement c)
            {
                return c switch
                {
                    ExtendedTextBox etb => etb.LeftText?.Trim() ?? string.Empty,
                    ComboBox cb => (cb.SelectedValue?.ToString() ?? cb.Text ?? string.Empty).Trim(),
                    TextBox tb => tb.Text?.Trim() ?? string.Empty,
                    CheckBox cbx => cbx.IsChecked?.ToString() ?? "False",
                    _ => string.Empty // The base Control class in WPF has no .Text property
                };
            }

            #region Registration

            public void RegisterExistingControl(string table, string col, FrameworkElement ctrl, bool required, string? dataType = null, string? unit = null, string? parameter = null, string? format = null, int? precision = null, int? scale = null)
            {
                if (ctrl == null) return;
                string key = K(table, col);

                var meta = new FieldMeta
                {
                    Table = table ?? string.Empty,
                    Column = col ?? string.Empty,
                    Required = required,
                    TypeInfo = (ctrl is ExtendedTextBox || ctrl is TextBox) ? BuildTypeInfo(dataType, precision, scale) : null,
                    OriginalBackground = (ctrl as Control)?.Background ?? Brushes.Transparent,
                    Unit = unit ?? string.Empty,
                    Parameter = parameter ?? string.Empty,
                    Format = format ?? string.Empty
                };

                _metaByControl[ctrl] = meta;
                _inputs[key] = ctrl;

                if (ctrl is ExtendedTextBox etb)
                {
                    ConfigureExtendedTextBox(etb, meta);
                    // Attach to your custom events
                    etb.LeftTextChanged += (s, e) => { if (!SuppressChange) { ValidateControl(etb); NotifyChanged(etb); } };
                    etb.SelectedIndexChanged += (s, e) => { if (!SuppressChange) { ValidateControl(etb); NotifyChanged(etb); } };
                }
                else if (ctrl is TextBox tb)
                {
                    tb.TextChanged += (s, e) => { if (!SuppressChange) { ValidateControl(tb); NotifyChanged(tb); } };
                }
                else if (ctrl is ComboBox cb)
                {
                    cb.SelectionChanged += (s, e) => { if (!SuppressChange) { ValidateControl(cb); NotifyChanged(cb); } };
                }
            }

            private void ConfigureExtendedTextBox(ExtendedTextBox etb, FieldMeta meta)
            {
                etb.SetUnits(ResolveUnits(meta.Parameter));
                etb.QuantityName = ResolveQuantityName(meta.Column, meta.Parameter);
                etb.ParameterName = meta.Parameter;
                etb.DefaultUnit = meta.Unit;
                etb.NumberFormat = string.IsNullOrWhiteSpace(meta.Format) ? "0.###" : meta.Format;

                // Type specific constraints
                if (meta.TypeInfo != null)
                {
                    string t = NormalizeType(meta.TypeInfo.BaseType);
                    etb.LeftNumericOnly = IsIntegerType(t) || IsDecimalType(t);
                    etb.LeftAllowDecimal = IsDecimalType(t);
                }
            }

            #endregion

            #region Validation

            private void ValidateControl(FrameworkElement ctrl)
            {
                if (!TryGetMeta(ctrl, out var meta)) return;
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
                    // 1. Declare the variable once at the start of the block
                    string msg;
                    string t = NormalizeType(meta.TypeInfo.BaseType);

                    // 2. Use 'out msg' instead of 'out string msg'
                    if (IsIntegerType(t) && !TryValidateInteger(raw, t, out msg))
                    {
                        MarkInvalid(ctrl, msg);
                        return;
                    }

                    // 3. Reuse the same variable here
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
                if (c is Control ctrl)
                {
                    ctrl.Background = Brushes.MistyRose;
                    ctrl.ToolTip = msg; // In WPF, this is a simple property assignment
                }
            }

            private void ClearInvalid(FrameworkElement c)
            {
                if (TryGetMeta(c, out var meta) && c is Control ctrl)
                {
                    ctrl.Background = meta.OriginalBackground;
                    ctrl.ToolTip = null; // Clear the tooltip by setting it to null
                }
            }

            private void NotifyChanged(FrameworkElement ctrl)
            {
                if (TryGetMeta(ctrl, out var meta))
                    OnInputChanged?.Invoke(meta.Table, meta.Column, GetControlValueText(ctrl));
            }

            #endregion

            #region UI Generator


            //public void BuildSection(string table, List<DynamicClass.ColumnInfo> cols, Panel host, bool skipPumpModelCols = false, bool skipSetCols = false, Brush? titleColor = null)
            public void BuildSection(string table, List<DynamicClass.ColumnInfo> cols, Panel host, bool skipPumpModelCols = false, bool skipSetCols = false, string titleResourceKey = "TextBrush")
            {
                if (cols == null || host == null) return;

                //Brush actualColor = titleColor ?? Brushes.Black;
                List<FrameworkElement> controls = new List<FrameworkElement>();

                foreach (var col in cols)
                {
                    if (col == null) continue;

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

                    string resolvedUnit = string.Join(",", ResolveUnits(parameter));

                    string defaultUnit = string.IsNullOrWhiteSpace(col.DefaultUnit)
                        ? ResolveUnits(parameter).FirstOrDefault() ?? string.Empty
                        : col.DefaultUnit.Trim();

                    string selectedUnit = string.IsNullOrWhiteSpace(col.LastUsedUnit)
                        ? defaultUnit
                        : col.LastUsedUnit.Trim();

                    // ============================================================
                    // FIXED HERE: You must define defaultValue before using it
                    object? defaultValue = col.DefaultValue;
                    // ============================================================

                    FrameworkElement p = Gen(
                        table: table,
                        colName: col.Name,
                        inputName: name,
                        unit: resolvedUnit,
                        defUnit: defaultUnit,
                        selUnit: selectedUnit,
                        parameter: parameter,
                        format: format,
                        required: !col.Nullable,
                        titleResourceKey: titleResourceKey,
                        dataType: dt,
                        precision: precision,
                        scale: scale,
                        opt: col.HasOptions ? col.Options : null,
                        defaultValue: defaultValue);

                    controls.Add(p);
                }

                foreach (var ctrl in controls)
                {
                    host.Children.Add(ctrl);
                }
            }



            public string GetBrushName(Brush brush)
            {
                if (brush is SolidColorBrush solidBrush)
                {
                    // Search the Brushes class for a property with a matching color
                    var property = typeof(Brushes).GetProperties()
                        .FirstOrDefault(p => ((SolidColorBrush)p.GetValue(null)).Color == solidBrush.Color);

                    return property?.Name ?? "Custom Color";
                }
                return "Not a SolidColorBrush";
            }



            public FrameworkElement Gen(string table, string colName, string inputName, string unit, string defUnit, string selUnit, string parameter, string format, bool required, string titleResourceKey, string dataType = "varchar", int? precision = null, int? scale = null, string[]? opt = null, object? defaultValue = null)
            {
                // 1. Remove hardcoded Width (260) to allow the UniformGrid to control sizing.
                // Increased Margin for a "Card-like" breathing space.
                StackPanel panel = new StackPanel
                {
                    Margin = new Thickness(10, 8, 10, 8),
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };

                // 2. Modern Label Styling
                // Set FontWeight to SemiBold and use a slightly lower Opacity for the label 
                // to make the actual input data stand out more.
                TextBlock label = new TextBlock
                {
                    Text = inputName.Replace("_", " ") + (required ? "*" : ""), // Clean up DB underscores
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 13,
                    Margin = new Thickness(4, 0, 0, 6),
                    Opacity = 0.85
                };
                label.SetResourceReference(TextBlock.ForegroundProperty, titleResourceKey);
                panel.Children.Add(label);

                // Common Height for all controls to ensure baseline alignment
                double controlHeight = 38;

                if (opt != null)
                {
                    ComboBox cb = new ComboBox
                    {
                        Height = controlHeight,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        VerticalContentAlignment = VerticalAlignment.Center,
                        Padding = new Thickness(8, 0, 0, 0)
                    };
                    cb.ItemsSource = opt;
                    cb.SetResourceReference(ComboBox.BackgroundProperty, "PanelBgBrush");
                    cb.SetResourceReference(ComboBox.ForegroundProperty, "TextBrush");

                    if (defaultValue != null) cb.Text = defaultValue.ToString();
                    panel.Children.Add(cb);
                    RegisterExistingControl(table, colName, cb, required);
                }
                else if (TextMode == TextInputMode.TextBox)
                {
                    TextBox tb = new TextBox
                    {
                        Height = controlHeight,
                        Text = defaultValue?.ToString() ?? "",
                        VerticalContentAlignment = VerticalAlignment.Center,
                        Padding = new Thickness(8, 0, 5, 0),
                        HorizontalAlignment = HorizontalAlignment.Stretch
                    };
                    // Ensure standard TextBoxes match your theme
                    tb.SetResourceReference(TextBox.BackgroundProperty, "PanelBgBrush");
                    tb.SetResourceReference(TextBox.ForegroundProperty, "TextBrush");

                    panel.Children.Add(tb);
                    RegisterExistingControl(table, colName, tb, required, dataType, unit, parameter, format, precision, scale);
                }
                else
                {
                    // 3. ExtendedTextBox Modernization
                    ExtendedTextBox etb = new ExtendedTextBox
                    {
                        Height = controlHeight,
                        HorizontalAlignment = HorizontalAlignment.Stretch
                    };

                    etb.SetUnits(ResolveUnits(parameter));
                    etb.QuantityName = ResolveQuantityName(colName, parameter);
                    etb.ParameterName = parameter;
                    etb.DefaultUnit = defUnit;
                    etb.NumberFormat = string.IsNullOrWhiteSpace(format) ? "0.###" : format;
                    etb.Items = new System.Collections.ObjectModel.ObservableCollection<string>(ResolveUnits(parameter));
                    etb.LeftText = defaultValue?.ToString() ?? string.Empty;

                    panel.Children.Add(etb);
                    RegisterExistingControl(table, colName, etb, required, dataType, unit, parameter, format, precision, scale);
                }

                return panel;
            }

            #endregion

            #region Shared Logic

            private static InputTypeInfo BuildTypeInfo(string? dt, int? p, int? s)
            {
                if (string.IsNullOrWhiteSpace(dt)) return null!;
                var info = ParseSqlType(dt);
                info.Precision = p; info.Scale = s;
                return info;
            }

            private static string NormalizeType(string? t) => (t ?? "varchar").Trim().ToLowerInvariant();
            private static bool IsIntegerType(string t) => t == "int" || t == "bigint" || t == "smallint" || t == "tinyint";
            private static bool IsDecimalType(string t) => t == "decimal" || t == "numeric" || t == "float" || t == "real" || t == "money";

            private static List<string> ResolveUnits(string? parameter)
            {
                return UnitConverisonEngine.GetUnitsFromParameter(parameter); ;
            }

            private static string ResolveQuantityName(string col, string? param) => !string.IsNullOrWhiteSpace(param) ? param : col;

            private static InputTypeInfo ParseSqlType(string dataType)
            {
                string dt = NormalizeType(dataType);
                var mText = Regex.Match(dt, @"^(n?varchar|n?char)\s*\(\s*(\d+)\s*\)$", RegexOptions.IgnoreCase);
                if (mText.Success) return new InputTypeInfo { BaseType = mText.Groups[1].Value, MaxLen = int.Parse(mText.Groups[2].Value) };
                return new InputTypeInfo { BaseType = dt };
            }

            private static bool TryValidateInteger(string raw, string type, out string msg)
            {
                msg = "";
                if (!long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)) { msg = "Invalid Integer"; return false; }
                return true;
            }

            private static bool TryValidateDecimal(string raw, InputTypeInfo info, out string msg)
            {
                msg = "";
                if (!decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out _)) { msg = "Invalid Decimal"; return false; }
                return true;
            }

            #endregion
        }

        public static Context CreateContext(TextInputMode textMode = TextInputMode.ExtendedTextBox)
        {
            return new Context(textMode);
        }
    }
}
