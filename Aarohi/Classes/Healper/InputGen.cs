using Aarohi.Classes;
using Aarohi.Core.Logger;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace Aarohi.Classes.Healper
{
    public static class InputGen
    {
        public sealed class InputTypeInfo
        {
            public string BaseType = "varchar";
            public int? MaxLen;
            public int? Precision;
            public int? Scale;
        }

        public sealed class FieldMeta
        {
            public string Table = "";
            public string Column = "";
            public bool Required;
            public InputTypeInfo? TypeInfo;

            public Color OriginalBackColor;
        }

        public sealed class Context
        {
            private readonly Dictionary<string, Control> _inputs =
                new Dictionary<string, Control>(StringComparer.OrdinalIgnoreCase);

            private readonly ErrorProvider _ep;

            /// <summary>
            /// If true, change events will NOT trigger validation/rules.
            /// Use this while programmatically filling values (edit mode / rule updates).
            /// </summary>
            public bool SuppressChange { get; set; }

            /// <summary>
            /// Form can set this to apply rules on any input change.
            /// </summary>
            public Action<string, string, string>? OnInputChanged { get; set; }

            public IReadOnlyDictionary<string, Control> Inputs => _inputs;

            internal Context(ContainerControl container)
            {
                _ep = new ErrorProvider
                {
                    ContainerControl = container,
                    BlinkStyle = ErrorBlinkStyle.NeverBlink
                };
            }

            #region Key/Meta Helpers
            private static string K(string table, string col) => $"{table}::{col}";
            private static FieldMeta? GetMeta(Control c) => c.Tag as FieldMeta;
            #endregion

            #region Register / Get Controls

            public void RegisterExistingControl(string table, string col, Control ctrl, bool required, string? dataType = null)
            {
                if (ctrl == null) return;

                var meta = new FieldMeta
                {
                    Table = table ?? "",
                    Column = col ?? "",
                    Required = required,
                    TypeInfo = (ctrl is TextBox && !string.IsNullOrWhiteSpace(dataType)) ? ParseSqlType(dataType) : null,
                    OriginalBackColor = ctrl.BackColor
                };

                ctrl.Tag = meta;
                _inputs[K(table, col)] = ctrl;

                if (ctrl is TextBox tb)
                {
                    if (meta.TypeInfo != null)
                        ConfigureTextBoxForType(tb, meta.TypeInfo);

                    AttachTextBoxNotify(tb);
                    ValidateRequiredLive(tb);
                }
                else if (ctrl is ComboBox cb)
                {
                    AttachComboNotify(cb);
                    ValidateRequiredLive(cb);
                }
                else
                {
                    ctrl.TextChanged += (s, e) =>
                    {
                        if (SuppressChange) return;

                        ValidateRequiredLive(ctrl);
                        var m = GetMeta(ctrl);
                        if (m != null) OnInputChanged?.Invoke(m.Table, m.Column, ctrl.Text ?? "");
                    };

                    ValidateRequiredLive(ctrl);
                }
            }

            public TextBox? GetTextBox(string table, string col) =>
                _inputs.TryGetValue(K(table, col), out var c) ? c as TextBox : null;

            public ComboBox? GetComboBox(string table, string col) =>
                _inputs.TryGetValue(K(table, col), out var c) ? c as ComboBox : null;

            #endregion

            public object GetValueObject(string table, string col)
            {
                if (_inputs.TryGetValue($"{table}::{col}", out var c) == false || c == null)
                    return DBNull.Value;

                var meta = c.Tag as FieldMeta;

                string raw;
                if (c is ComboBox cb)
                    raw = (cb.SelectedItem?.ToString() ?? cb.Text ?? "").Trim();
                else
                    raw = (c.Text ?? "").Trim();

                if (string.IsNullOrWhiteSpace(raw))
                    return DBNull.Value;

                var info = meta?.TypeInfo;
                if (info == null) return raw;

                string t = (info.BaseType ?? "").ToLowerInvariant();

                // integers
                if (t == "int" || t == "bigint" || t == "smallint" || t == "tinyint")
                {
                    long lv;
                    if (!long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out lv))
                        return DBNull.Value;

                    if (t == "int") return (int)lv;
                    if (t == "smallint") return (short)lv;
                    if (t == "tinyint") return (byte)lv;
                    return lv;
                }

                // decimals
                if (t == "decimal" || t == "numeric" || t == "float" || t == "real" || t == "money" || t == "smallmoney")
                {
                    if (raw.Equals("Infinity", StringComparison.OrdinalIgnoreCase) ||
                        raw.Equals("+Infinity", StringComparison.OrdinalIgnoreCase) ||
                        raw.Equals("-Infinity", StringComparison.OrdinalIgnoreCase) ||
                        raw.Equals("NaN", StringComparison.OrdinalIgnoreCase))
                        return DBNull.Value;

                    decimal dv;
                    if (!decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out dv))
                        return DBNull.Value;

                    if (info.Scale.HasValue) dv = Math.Round(dv, info.Scale.Value);
                    return dv;
                }

                // bit
                if (t == "bit")
                {
                    if (raw == "1" || raw.Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
                    if (raw == "0" || raw.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;
                    return DBNull.Value;
                }

                return raw; // varchar/char/etc
            }

            #region Rule Firing (IMPORTANT FOR LOAD)

            public bool FireRule(string table, string col)
            {
                if (OnInputChanged == null) return false;

                if (!_inputs.TryGetValue($"{table}::{col}", out var c) || c == null)
                    return false;

                bool prev = SuppressChange;
                try
                {
                    // Force allow rule evaluation
                    SuppressChange = false;

                    string val;
                    if (c is ComboBox cb)
                        val = (cb.SelectedItem?.ToString() ?? cb.Text ?? "").Trim();
                    else
                        val = (c.Text ?? "").Trim();

                    OnInputChanged?.Invoke(table, col, val);
                    return true;
                }
                finally
                {
                    SuppressChange = prev;
                }
            }

            /// <summary>
            /// For each candidate list, it finds the first existing column and fires rule for it.
            /// Use to apply rules once on load in correct order.
            /// </summary>
            public void FireRulesInOrder(string table, params IEnumerable<string>[] candidateLists)
            {
                if (candidateLists == null || candidateLists.Length == 0) return;

                foreach (var list in candidateLists)
                {
                    if (list == null) continue;

                    foreach (var col in list)
                    {
                        if (string.IsNullOrWhiteSpace(col)) continue;

                        if (_inputs.ContainsKey($"{table}::{col}"))
                        {
                            FireRule(table, col);
                            break;
                        }
                    }
                }
            }

            /// <summary>
            /// Fires rules for all filled inputs in that table (optional).
            /// Helpful when you add more rules later.
            /// </summary>
            public void FireRulesForAllFilled(string table, bool onlyFilled = true)
            {
                if (OnInputChanged == null) return;

                bool prev = SuppressChange;
                try
                {
                    SuppressChange = false;

                    foreach (var kv in _inputs)
                    {
                        var key = kv.Key; // Table::Col
                        if (!key.StartsWith(table + "::", StringComparison.OrdinalIgnoreCase))
                            continue;

                        var ctrl = kv.Value;
                        if (ctrl == null) continue;

                        string col = key.Substring((table + "::").Length);

                        string val;
                        if (ctrl is ComboBox cb)
                            val = (cb.SelectedItem?.ToString() ?? cb.Text ?? "").Trim();
                        else
                            val = (ctrl.Text ?? "").Trim();

                        if (onlyFilled && string.IsNullOrWhiteSpace(val))
                            continue;

                        OnInputChanged?.Invoke(table, col, val);
                    }
                }
                finally
                {
                    SuppressChange = prev;
                }
            }

            #endregion

            #region Required Validation

            private static bool IsEmpty(Control c)
            {
                if (c is ComboBox cb) return string.IsNullOrWhiteSpace(cb.Text);
                return string.IsNullOrWhiteSpace(c.Text);
            }

            private void MarkInvalid(Control c, string message)
            {
                var m = GetMeta(c);
                if (m == null) return;

                _ep.SetError(c, message ?? "Invalid");
                c.BackColor = Color.MistyRose;
            }

            private void ClearInvalid(Control c)
            {
                var m = GetMeta(c);
                if (m == null) return;

                _ep.SetError(c, "");
                c.BackColor = m.OriginalBackColor;
            }

            public void ValidateRequiredLive(Control c)
            {
                var m = GetMeta(c);
                if (m == null || !m.Required) return;

                if (IsEmpty(c)) MarkInvalid(c, "Required");
                else ClearInvalid(c);
            }

            public bool ValidateAllRequired()
            {
                bool ok = true;

                foreach (var kv in _inputs)
                {
                    var ctrl = kv.Value;
                    var meta = GetMeta(ctrl);
                    if (meta == null || !meta.Required) continue;

                    if (IsEmpty(ctrl))
                    {
                        MarkInvalid(ctrl, "Required");
                        ok = false;
                    }
                    else
                    {
                        ClearInvalid(ctrl);
                    }
                }

                return ok;
            }

            #endregion

            #region Notify Wiring

            private void AttachTextBoxNotify(TextBox tb)
            {
                tb.GotFocus += (s, e) => tb.SelectAll();

                tb.Enter += (s, e) =>
                {
                    tb.BeginInvoke((Action)(() => tb.SelectAll()));
                };

                tb.MouseClick += (s, e) =>
                {
                    tb.BeginInvoke((Action)(() => tb.SelectAll()));
                };

                tb.TextChanged += (s, e) =>
                {
                    if (SuppressChange) return;

                    var meta = GetMeta(tb);
                    if (meta == null) return;

                    EnforceMaxLen(tb, meta);
                    ValidateRequiredLive(tb);

                    OnInputChanged?.Invoke(meta.Table, meta.Column, tb.Text ?? "");
                };

                tb.Validating += Tb_Validating_ByType;
            }

            private void AttachComboNotify(ComboBox cb)
            {
                cb.SelectedIndexChanged += (s, e) =>
                {
                    if (SuppressChange) return;

                    var meta = GetMeta(cb);
                    if (meta == null) return;

                    ValidateRequiredLive(cb);
                    OnInputChanged?.Invoke(meta.Table, meta.Column, cb.Text ?? "");
                };

                cb.TextChanged += (s, e) =>
                {
                    if (SuppressChange) return;

                    var meta = GetMeta(cb);
                    if (meta == null) return;

                    ValidateRequiredLive(cb);
                    OnInputChanged?.Invoke(meta.Table, meta.Column, cb.Text ?? "");
                };
            }

            private void EnforceMaxLen(TextBox tb, FieldMeta meta)
            {
                var max = meta.TypeInfo?.MaxLen;
                if (!max.HasValue || max.Value <= 0) return;

                string t = tb.Text ?? "";
                if (t.Length <= max.Value) return;

                var prev = SuppressChange;
                SuppressChange = true;
                tb.Text = t.Substring(0, max.Value);
                tb.SelectionStart = tb.Text.Length;
                SuppressChange = prev;
            }

            #endregion

            #region Type Parsing / Validation (TextBox)

            private static InputTypeInfo ParseSqlType(string dataType)
            {
                var dt = (dataType ?? "varchar").Trim().ToLowerInvariant();

                var mMax = Regex.Match(dt, @"^(n?varchar)\s*\(\s*max\s*\)$");
                if (mMax.Success)
                    return new InputTypeInfo { BaseType = mMax.Groups[1].Value, MaxLen = null };

                var mText = Regex.Match(dt, @"^(n?varchar|n?char)\s*\(\s*(\d+)\s*\)$");
                if (mText.Success)
                {
                    return new InputTypeInfo
                    {
                        BaseType = mText.Groups[1].Value,
                        MaxLen = int.Parse(mText.Groups[2].Value)
                    };
                }

                var mDec = Regex.Match(dt, @"^(decimal|numeric)\s*\(\s*(\d+)\s*,\s*(\d+)\s*\)$");
                if (mDec.Success)
                {
                    return new InputTypeInfo
                    {
                        BaseType = mDec.Groups[1].Value,
                        Precision = int.Parse(mDec.Groups[2].Value),
                        Scale = int.Parse(mDec.Groups[3].Value)
                    };
                }

                return new InputTypeInfo { BaseType = dt };
            }

            private static bool IsIntegerType(string t) =>
                t == "int" || t == "bigint" || t == "smallint" || t == "tinyint";

            private static bool IsDecimalType(string t) =>
                t == "decimal" || t == "numeric" || t == "float" || t == "real" || t == "money" || t == "smallmoney";

            private void ConfigureTextBoxForType(TextBox tb, InputTypeInfo info)
            {
                if (IsIntegerType(info.BaseType))
                {
                    tb.KeyPress += (s, e) =>
                    {
                        if (char.IsControl(e.KeyChar)) return;
                        if (char.IsDigit(e.KeyChar)) return;
                        if (e.KeyChar == '-' && tb.SelectionStart == 0 && !tb.Text.Contains("-")) return;
                        e.Handled = true;
                    };
                }
                else if (IsDecimalType(info.BaseType))
                {
                    tb.KeyPress += (s, e) =>
                    {
                        if (char.IsControl(e.KeyChar)) return;
                        if (char.IsDigit(e.KeyChar)) return;

                        if (e.KeyChar == '-' && tb.SelectionStart == 0 && !tb.Text.Contains("-")) return;

                        if (e.KeyChar == '.')
                        {
                            if (tb.Text.Contains(".")) { e.Handled = true; return; }
                            return;
                        }

                        e.Handled = true;
                    };
                }
            }

            private void Tb_Validating_ByType(object? sender, CancelEventArgs e)
            {
                var tb = sender as TextBox;
                if (tb == null) return;

                var meta = GetMeta(tb);
                var info = meta?.TypeInfo;
                if (info == null) return;

                EnforceMaxLen(tb, meta!);

                var txt = (tb.Text ?? "").Trim();
                if (txt.Length == 0)
                {
                    ValidateRequiredLive(tb);
                    return;
                }

                var t = info.BaseType.ToLowerInvariant();

                if (IsIntegerType(t))
                {
                    if (!long.TryParse(txt, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                        MarkInvalid(tb, "Enter a valid integer");
                    else
                        ClearInvalid(tb);

                    return;
                }

                if (IsDecimalType(t))
                {
                    if (!decimal.TryParse(txt, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
                    {
                        MarkInvalid(tb, "Enter a valid decimal");
                        return;
                    }

                    if (info.Scale.HasValue)
                    {
                        int scale = info.Scale.Value;
                        var parts = txt.Split('.');
                        int after = parts.Length == 2 ? parts[1].Length : 0;

                        if (after > scale)
                        {
                            MarkInvalid(tb, $"Only {scale} digits allowed after decimal");
                            return;
                        }
                    }

                    ClearInvalid(tb);
                }
            }

            #endregion

            #region Value Setters (Edit + Rules)

            public bool SetTextBoxValue(string table, string col, string? value, bool triggerRules)
            {
                var tb = GetTextBox(table, col);
                if (tb == null) return false;

                var prev = SuppressChange;
                SuppressChange = true;

                tb.Text = value ?? "";

                SuppressChange = prev;

                var meta = GetMeta(tb);
                if (meta != null) EnforceMaxLen(tb, meta);

                ValidateRequiredLive(tb);
                if (triggerRules) OnInputChanged?.Invoke(table, col, tb.Text ?? "");
                return true;
            }

            public bool SetComboValue(string table, string col, string? value, bool triggerRules)
            {
                var cb = GetComboBox(table, col);
                if (cb == null) return false;

                var newVal = (value ?? "").Trim();

                var prev = SuppressChange;
                SuppressChange = true;

                if (cb.DropDownStyle == ComboBoxStyle.DropDownList)
                {
                    int idx = -1;
                    for (int i = 0; i < cb.Items.Count; i++)
                    {
                        if (string.Equals(cb.Items[i]?.ToString()?.Trim(), newVal, StringComparison.OrdinalIgnoreCase))
                        {
                            idx = i;
                            break;
                        }
                    }
                    cb.SelectedIndex = idx;
                }
                else
                {
                    cb.Text = newVal;
                }

                SuppressChange = prev;

                ValidateRequiredLive(cb);
                if (triggerRules) OnInputChanged?.Invoke(table, col, cb.Text ?? "");
                return true;
            }

            public void SetAnyValue(string table, string col, object? value, bool triggerRules)
            {
                string s = value == null || value == DBNull.Value
                    ? ""
                    : Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";

                if (SetComboValue(table, col, s, triggerRules)) return;
                SetTextBoxValue(table, col, s, triggerRules);
            }

            #endregion

            #region UI Generator (TextBox + ComboBox) + Default Values

            public Panel Gen(
                string table,
                string colName,
                string inputName,
                string Unit,
                string Parameter,
                string Format,
                bool required,
                Color titleColor,
                string dataType = "varchar",
                string[]? opt = null,
                object? defaultValue = null)
            {
                Panel panelHolder = new Panel();
                Label labelInput = new Label();

                panelHolder.BackColor = Color.Transparent;
                panelHolder.Padding = new Padding(8, 8, 18, 8);
                panelHolder.Location = new Point(9, 9);
                panelHolder.Name = $"Panel_{table}_{colName}";
                panelHolder.Size = new Size(249, 70);
                panelHolder.TabIndex = 0;

                labelInput.BackColor = Color.Transparent;
                labelInput.Dock = DockStyle.Fill;
                labelInput.Font = new Font("Gadugi", 12F, FontStyle.Bold, GraphicsUnit.Point, 0);
                labelInput.ForeColor = titleColor;
                labelInput.Margin = new Padding(0);
                labelInput.Name = $"Label_{table}_{colName}";
                labelInput.Size = new Size(249, 35);
                labelInput.TabIndex = 2;
                labelInput.Text = inputName + (required ? "*" : "");
                labelInput.TextAlign = ContentAlignment.MiddleLeft;

                panelHolder.Controls.Add(labelInput);

                if (opt != null)
                {
                    ComboBox cb = new ComboBox();
                    panelHolder.Controls.Add(cb);

                    cb.Dock = DockStyle.Bottom;
                    cb.Font = new Font("Gadugi", 14.0F, FontStyle.Regular, GraphicsUnit.Point, 0);
                    cb.Name = $"CB_{table}_{colName}";
                    cb.Size = new Size(249, 35);
                    cb.TabIndex = 0;

                    cb.DropDownStyle = ComboBoxStyle.DropDownList;

                    cb.Items.Clear();
                    foreach (var s in opt) cb.Items.Add(s ?? "");
                    cb.SelectedIndex = -1;

                    // Default value set (case-insensitive safe)
                    if (!IsNullOrWhite(defaultValue))
                    {
                        string dv = ToText(defaultValue).Trim();
                        int idx = -1;
                        for (int i = 0; i < cb.Items.Count; i++)
                        {
                            if (string.Equals(cb.Items[i]?.ToString()?.Trim(), dv, StringComparison.OrdinalIgnoreCase))
                            {
                                idx = i;
                                break;
                            }
                        }
                        cb.SelectedIndex = idx;
                    }

                    RegisterExistingControl(table, colName, cb, required, null);
                }
                else
                {
                    TextBox tb = new TextBox();
                    panelHolder.Controls.Add(tb);

                    tb.Dock = DockStyle.Bottom;
                    tb.Font = new Font("Gadugi", 14.0F, FontStyle.Regular, GraphicsUnit.Point, 0);
                    tb.Name = $"TB_{table}_{colName}";
                    tb.Size = new Size(249, 35);
                    tb.TabIndex = 0;

                    if (!IsNullOrWhite(defaultValue))
                        tb.Text = ToText(defaultValue);

                    RegisterExistingControl(table, colName, tb, required, dataType);
                }

                return panelHolder;
            }

            private static bool IsNullOrWhite(object? v)
            {
                if (v == null || v == DBNull.Value) return true;
                return v is string s && string.IsNullOrWhiteSpace(s);
            }

            private static string ToText(object? v)
            {
                if (v == null || v == DBNull.Value) return "";
                return v.ToString() ?? "";
            }

            public void BuildSection(
                string table,
                List<DynamicClass.ColumnInfo> cols,
                Control host,
                bool skipPumpModelCols = false,
                bool skipSetCols = false,
                Color titleColor = default)
            {
                if (cols == null) return;

                List<Control> controls = new List<Control>();

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

                    object? defaultValue = col.DefaultValue;

                    var p = Gen(
                        table,
                        col.Name,
                        name,
                        unit,
                        parameter,
                        format,
                        required: !col.Nullable,
                        titleColor: titleColor,
                        dataType: dt,
                        opt: col.HasOptions ? col.Options : null,
                        defaultValue: defaultValue
                    );

                    controls.Add(p);
                }

                host.Controls.AddRange(controls.ToArray());
            }

            #endregion
        }

        public static Context CreateContext(ContainerControl container) => new Context(container);
    }
}