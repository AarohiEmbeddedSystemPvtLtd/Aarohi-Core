using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Drawing.Design;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace Aarohi.Classes
{
    /// <summary>
    /// ExtendedTextBox:
    /// - Left side: editable numeric/text input (painted, caret, scroll)
    /// - Right side: dropdown items (optional; hidden if no items)
    /// - Auto conversion: when unit (right side) changes, convert LeftText value using UnitConverisonEngine
    /// - Supports DefaultUnit + methods to get both current unit value and default unit value.
    /// </summary>
    public class ExtendedTextBox : Control
    {
        // ---------- Designer collection editor (fix for string items in designer) ----------
        public sealed class StringListEditor : CollectionEditor
        {
            public StringListEditor(Type type) : base(type) { }
            protected override Type CreateCollectionItemType() => typeof(string);
            protected override object CreateInstance(Type itemType) => string.Empty; // string has no ctor
        }

        // ---------- Items collection that auto refreshes control ----------
        public sealed class StringItemCollection : Collection<string>
        {
            private readonly ExtendedTextBox _owner;
            public StringItemCollection(ExtendedTextBox owner) => _owner = owner;

            protected override void InsertItem(int index, string item)
            {
                base.InsertItem(index, item ?? "");
                _owner.OnItemsChanged();
            }

            protected override void SetItem(int index, string item)
            {
                base.SetItem(index, item ?? "");
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

        private readonly StringItemCollection _items;

        private int _selectedIndex = -1;

        private string _leftText = "0";
        private int _leftWidth = 34;
        private int _gap = 0;
        private int _cornerRadius = 6;

        private Color _borderColor = Color.FromArgb(210, 210, 210);
        private Color _hoverBorderColor = Color.FromArgb(170, 170, 170);
        private Color _fillColor = Color.White;
        private Color _textColor = Color.FromArgb(40, 40, 40);
        private Color _arrowColor = Color.FromArgb(60, 60, 60);
        private Color _pressedOverlay = Color.FromArgb(20, 0, 0, 0);

        private bool _hover;
        private bool _pressed;

        private DropPopup? _popup;

        // Left editing (paint-only)
        private bool _leftEditing = false;
        private int _caretIndex = 0;
        private bool _caretVisible = true;
        private readonly Timer _caretTimer = new Timer();

        // caret overlap fix + textbox-like scroll
        private int _leftScrollX = 0;
        private const int _leftPad = 6;

        // ====== Unit conversion extras ======
        private bool _autoConverting = false;
        private string? _quantityName;      // e.g. "Length", "Torque"
        private string? _defaultUnit;       // e.g. "meter"
        private string _numberFormat = "0.###";

        public event EventHandler? LeftTextChanged;
        public event EventHandler? SelectedIndexChanged;

        /// <summary>
        /// Fires when either current value or unit changes OR when default value changes (due to current changes).
        /// Use this if you want to refresh labels / push both values to DB.
        /// </summary>
        public event EventHandler? ValuePairChanged;

        private bool HasRightPart => _items != null && _items.Count > 0;

        private int _lastGoodSelectedIndex = -1;

        // Optional: control whether to show MessageBox on conversion failure
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public bool ShowConversionErrorMessageBox { get; set; } = true;

        // Optional: customize title
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public string ConversionErrorTitle { get; set; } = "Unit Conversion Error";

        // ✅ Keep Visible as you asked
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public bool LeftEditable { get; set; } = true;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public bool LeftNumericOnly { get; set; } = false;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public bool LeftAllowDecimal { get; set; } = true;

        private int _rightWidth = 70;
        private bool _useRightWidth = true;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public bool UseRightWidth
        {
            get => _useRightWidth;
            set { _useRightWidth = value; Invalidate(); }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public int RightWidth
        {
            get => _rightWidth;
            set { _rightWidth = Math.Max(40, value); Invalidate(); }
        }

        // ====== Conversion settings ======
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public string? QuantityName
        {
            get => _quantityName;
            set => _quantityName = (value ?? "").Trim();
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public string? DefaultUnit
        {
            get => _defaultUnit;
            set => _defaultUnit = (value ?? "").Trim();
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public bool AutoConvertOnUnitChange { get; set; } = true;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public string NumberFormat
        {
            get => _numberFormat;
            set => _numberFormat = string.IsNullOrWhiteSpace(value) ? "0.###" : value;
        }

        public ExtendedTextBox()
        {
            _items = new StringItemCollection(this);

            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.UserPaint |
                     ControlStyles.Selectable |
                     ControlStyles.SupportsTransparentBackColor, true);

            TabStop = true;
            Cursor = Cursors.Hand;
            Size = new Size(130, 28);

            Font = new Font("Segoe UI", 9f);
            BackColor = Color.Transparent;

            if (LicenseManager.UsageMode != LicenseUsageMode.Designtime)
            {
                _caretTimer.Interval = 500;
                _caretTimer.Tick += (s, e) =>
                {
                    if (_leftEditing && Focused)
                    {
                        _caretVisible = !_caretVisible;
                        Invalidate();
                    }
                };
            }

            UpdateRoundedRegion();
        }

        // ---------- Public API ----------

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        [Editor(typeof(StringListEditor), typeof(UITypeEditor))]
        public StringItemCollection Items => _items;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public string LeftText
        {
            get => _leftText;
            set
            {
                _leftText = value ?? "";
                _caretIndex = Math.Min(_caretIndex, _leftText.Length);
                _leftScrollX = 0;
                Invalidate();

                // NOTE: Setter raises change events for consistency (improvement)
                LeftTextChanged?.Invoke(this, EventArgs.Empty);
                ValuePairChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public int LeftWidth
        {
            get => _leftWidth;
            set { _leftWidth = Math.Max(20, value); Invalidate(); }
        }

        /// <summary>Current selected unit text (right side). Null if no right part.</summary>
        public string? CurrentUnit => SelectedItem;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                int newVal = value;

                if (_items.Count == 0) newVal = -1;
                else newVal = Math.Max(0, Math.Min(_items.Count - 1, newVal));

                if (_selectedIndex == newVal) return;

                // store old state
                int oldIndex = _selectedIndex;
                string? oldUnit = SelectedItem; // old unit text

                // switch to new index (optimistic)
                _selectedIndex = newVal;
                Invalidate();

                // raise change event (selection changed)
                SelectedIndexChanged?.Invoke(this, EventArgs.Empty);

                // if auto convert enabled, try conversion; if fails -> revert index
                if (AutoConvertOnUnitChange)
                {
                    string? newUnit = SelectedItem;
                    bool ok = TryConvertLeftTextOnUnitChange_WithResult(oldUnit, newUnit, out string? error);

                    if (!ok)
                    {
                        // revert selection back to last good
                        _selectedIndex = (_lastGoodSelectedIndex >= 0) ? _lastGoodSelectedIndex : oldIndex;
                        Invalidate();
                        SelectedIndexChanged?.Invoke(this, EventArgs.Empty);

                        if (ShowConversionErrorMessageBox && !string.IsNullOrWhiteSpace(error))
                        {
                            var owner = (IWin32Window)(this.FindForm() ?? (Form?)null) ?? this;
                            MessageBox.Show(owner, error, ConversionErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);

                        }

                        // do NOT fire ValuePairChanged for failed conversion (keeps consistent)
                        return;
                    }

                    // conversion success => this index is now last good
                    _lastGoodSelectedIndex = _selectedIndex;
                }
                else
                {
                    // no conversion requested, just accept it as good
                    _lastGoodSelectedIndex = _selectedIndex;
                }

                ValuePairChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public bool SetValueAndUnit(double value, string unit, bool convertFromDefaultUnit = false)
        {
            if (string.IsNullOrWhiteSpace(unit)) return false;

            // Ensure the unit exists in Items
            int idx = -1;
            for (int i = 0; i < Items.Count; i++)
            {
                if (string.Equals(Items[i], unit, StringComparison.OrdinalIgnoreCase))
                {
                    idx = i;
                    break;
                }
            }
            if (idx < 0) return false;

            // If user says "value is in DefaultUnit", but you want to display it in "unit"
            double finalValue = value;

            if (convertFromDefaultUnit)
            {
                if (string.IsNullOrWhiteSpace(QuantityName)) return false;
                if (string.IsNullOrWhiteSpace(DefaultUnit)) return false;

                // value is given in DefaultUnit, convert to the target unit
                if (!unit.Equals(DefaultUnit, StringComparison.OrdinalIgnoreCase))
                {
                    var (cv, _) = UnitConverisonEngine.convert(
                        QuantityName!,
                        value,
                        DefaultUnit!,
                        unit
                    );
                    finalValue = cv;
                }
            }

            // IMPORTANT: prevent auto conversion firing when we programmatically set unit+text
            try
            {
                _autoConverting = true;

                // 1) set unit
                SelectedIndex = idx;

                // 2) set value text
                LeftText = finalValue.ToString(NumberFormat, CultureInfo.CurrentCulture);

                // keep caret sane
                _caretIndex = Math.Min(_caretIndex, _leftText.Length);
                _leftScrollX = 0;

                Invalidate();
                return true;
            }
            finally
            {
                _autoConverting = false;
            }
        }

        // Replace your SetLeftTextRaw with this upgraded version.
        //
        // Behavior:
        // - Always sets LeftText for non-numeric text
        // - If text is numeric AND you ask numericMode=true:
        //    -> it treats the input as a DEFAULT UNIT value
        //    -> converts it into the currently selected unit (if needed)
        //    -> sets LeftText using SetDefaultValue()
        // - If you want numeric input treated as CURRENT UNIT value (not default),
        //   pass treatNumericAsDefaultUnit:false

        public void SetLeftTextRaw(string? text, bool numericMode = false, bool treatNumericAsDefaultUnit = true)
        {
            text = (text ?? "").Trim();

            // quick empty
            if (text.Length == 0)
            {
                try
                {
                    _autoConverting = true;
                    LeftText = "";
                    _caretIndex = 0;
                    _leftScrollX = 0;
                    Invalidate();
                }
                finally { _autoConverting = false; }
                return;
            }

            // If numericMode is enabled -> try parse numeric first
            if (numericMode && TryParseDoubleAnyCulture(text, out double num))
            {
                if (treatNumericAsDefaultUnit)
                {
                    // treat input as DEFAULT UNIT value, and show in currently selected unit
                    // (your SetDefaultValue already does conversion DefaultUnit -> CurrentUnit)
                    SetDefaultValue(num);
                }
                else
                {
                    // treat input as current unit numeric value (no conversion)
                    try
                    {
                        _autoConverting = true;
                        LeftText = num.ToString(NumberFormat, CultureInfo.CurrentCulture);
                        _caretIndex = Math.Min(_caretIndex, (_leftText ?? "").Length);
                        _leftScrollX = 0;
                        Invalidate();
                    }
                    finally { _autoConverting = false; }
                }

                return;
            }

            // Not numeric (or numericMode off) -> just set raw text
            try
            {
                _autoConverting = true; // prevent any conversion side-effects
                LeftText = text;
                _caretIndex = Math.Min(_caretIndex, (_leftText ?? "").Length);
                _leftScrollX = 0;
                Invalidate();
            }
            finally
            {
                _autoConverting = false;
            }
        }

        // Helper: supports CurrentCulture + InvariantCulture
        private static bool TryParseDoubleAnyCulture(string s, out double value)
        {
            value = 0;

            if (double.TryParse(s, NumberStyles.Any, CultureInfo.CurrentCulture, out value))
                return true;

            if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out value))
                return true;

            // optional: handle comma/decimal mismatch like "1,23" vs "1.23"
            // (best-effort; comment out if you don't want it)
            var swapped = s.Replace(',', '.');
            if (!ReferenceEquals(swapped, s) &&
                double.TryParse(swapped, NumberStyles.Any, CultureInfo.InvariantCulture, out value))
                return true;

            return false;
        }


        // Convenience: set by default unit only (will keep current selected unit and convert)
        public bool SetDefaultValue(double defaultValue)
        {
            if (string.IsNullOrWhiteSpace(QuantityName)) return false;
            if (string.IsNullOrWhiteSpace(DefaultUnit)) return false;

            string? unitNow = CurrentUnit;
            if (string.IsNullOrWhiteSpace(unitNow)) return false;

            double finalValue = defaultValue;

            if (!unitNow!.Equals(DefaultUnit!, StringComparison.OrdinalIgnoreCase))
            {
                var (cv, _) = UnitConverisonEngine.convert(
                    QuantityName!,
                    defaultValue,
                    DefaultUnit!,
                    unitNow!
                );
                finalValue = cv;
            }

            try
            {
                _autoConverting = true;
                LeftText = finalValue.ToString(NumberFormat, CultureInfo.CurrentCulture);
                Invalidate();
                return true;
            }
            finally
            {
                _autoConverting = false;
            }
        }


        private bool TryConvertLeftTextOnUnitChange_WithResult(string? oldUnit, string? newUnit, out string? error)
        {
            error = null;

            if (_autoConverting) return true; // avoid re-entrancy, treat as ok
            if (string.IsNullOrWhiteSpace(_quantityName)) return true; // nothing to do
            if (string.IsNullOrWhiteSpace(oldUnit)) return true;
            if (string.IsNullOrWhiteSpace(newUnit)) return true;

            if (oldUnit.Equals(newUnit, StringComparison.OrdinalIgnoreCase))
                return true;

            // if left is non-numeric, we skip conversion (not an error)
            if (!TryGetLeftValue(out double oldValue))
                return true;

            try
            {
                _autoConverting = true;

                var (newValue, _) = UnitConverisonEngine.convert(
                    _quantityName!,
                    oldValue,
                    oldUnit!,
                    newUnit!
                );

                _leftText = newValue.ToString(_numberFormat, CultureInfo.CurrentCulture);
                _caretIndex = Math.Min(_caretIndex, _leftText.Length);
                _leftScrollX = 0;

                LeftTextChanged?.Invoke(this, EventArgs.Empty);
                Invalidate();

                return true;
            }
            catch (Exception ex)
            {
                // Build a useful message
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

        public string? SelectedItem =>
            (_selectedIndex >= 0 && _selectedIndex < _items.Count) ? _items[_selectedIndex] : null;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public Color BorderColor
        {
            get => _borderColor;
            set { _borderColor = value; Invalidate(); }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public Color HoverBorderColor
        {
            get => _hoverBorderColor;
            set { _hoverBorderColor = value; Invalidate(); }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public Color FillColor
        {
            get => _fillColor;
            set { _fillColor = value; Invalidate(); }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public int CornerRadius
        {
            get => _cornerRadius;
            set
            {
                _cornerRadius = Math.Max(0, value);
                UpdateRoundedRegion();
                Invalidate();
            }
        }

        // ---------- Items changed handler ----------
        private void OnItemsChanged()
        {
            if (!HasRightPart)
            {
                HidePopup();
                if (_selectedIndex != -1) _selectedIndex = -1;
                _lastGoodSelectedIndex = -1;
            }
            else
            {
                if (_selectedIndex < 0) _selectedIndex = 0;
                if (_selectedIndex >= _items.Count) _selectedIndex = _items.Count - 1;

                // When items exist, treat current as "good" by default
                _lastGoodSelectedIndex = _selectedIndex;
            }

            Invalidate();
            ValuePairChanged?.Invoke(this, EventArgs.Empty);
        }

        // ---------- Region / Transparency ----------
        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            UpdateRoundedRegion();
        }

        private void UpdateRoundedRegion()
        {
            if (Width <= 1 || Height <= 1) return;
            using var path = RoundedRectPath(new Rectangle(0, 0, Width - 1, Height - 1), _cornerRadius);
            Region = new Region(path);
        }

        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
            // keep empty (we draw parent in OnPaint)
        }

        // ---------- Caret measure helpers ----------
        private int MeasureTextWidthTypographic(Graphics g, string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;

            using var sf = (StringFormat)StringFormat.GenericTypographic.Clone();
            sf.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;

            var size = g.MeasureString(text, Font, int.MaxValue, sf);
            return (int)Math.Ceiling(size.Width);
        }

        private void EnsureCaretVisible(Graphics g, Rectangle editRect)
        {
            string t = _leftText ?? "";
            int idx = Math.Max(0, Math.Min(_caretIndex, t.Length));
            string before = t.Substring(0, idx);

            int caretX = MeasureTextWidthTypographic(g, before);
            int visibleX = caretX - _leftScrollX;

            int rightLimit = Math.Max(0, editRect.Width - 2);
            if (visibleX > rightLimit) _leftScrollX = caretX - rightLimit;
            if (visibleX < 0) _leftScrollX = caretX;

            if (_leftScrollX < 0) _leftScrollX = 0;
        }

        // ---------- Split rects ----------
        private void GetSplitRects(out Rectangle leftRect, out Rectangle rightRect)
        {
            int w = Width - 1;
            int h = Height - 1;

            // ✅ when no items => full left, NO right
            if (!HasRightPart)
            {
                leftRect = new Rectangle(0, 0, w, h);
                rightRect = Rectangle.Empty;
                return;
            }

            if (_useRightWidth)
            {
                int rw = Math.Min(_rightWidth, w - 30);
                int lw = Math.Max(20, w - rw - _gap);

                leftRect = new Rectangle(0, 0, lw, h);
                rightRect = new Rectangle(leftRect.Right + _gap, 0, w - lw - _gap, h);
            }
            else
            {
                int lw = Math.Min(_leftWidth, w - 30);

                leftRect = new Rectangle(0, 0, lw, h);
                rightRect = new Rectangle(leftRect.Right + _gap, 0, w - lw - _gap, h);
            }
        }

        // ---------- Paint ----------
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            PaintParentBackgroundSafe(g);

            int w = Width;
            int h = Height;

            // if items removed while popup open -> close immediately
            if (!HasRightPart && IsPopupOpen())
                HidePopup();

            GetSplitRects(out var leftRect, out var rightRect);

            // Outer rounded body
            using (var path = RoundedRectPath(new Rectangle(0, 0, w - 1, h - 1), _cornerRadius))
            using (var fill = new SolidBrush(_fillColor))
            using (var pen = new Pen(_hover ? _hoverBorderColor : _borderColor, 1f))
            {
                g.FillPath(fill, path);
                g.DrawPath(pen, path);
            }

            // ✅ Divider + Right section only when HasRightPart
            if (HasRightPart)
            {
                using (var penDiv = new Pen(_borderColor, 1f))
                {
                    int x = leftRect.Right + _gap;
                    g.DrawLine(penDiv, x, 3, x, Height - 4);
                }
            }

            // Left text + caret (typographic draw+measure, clipped, scrolled)
            using (var txtBrush = new SolidBrush(_textColor))
            {
                if (_leftEditing)
                {
                    var editRect = new Rectangle(
                        leftRect.X + _leftPad,
                        leftRect.Y,
                        Math.Max(1, leftRect.Width - (_leftPad * 2)),
                        leftRect.Height
                    );

                    EnsureCaretVisible(g, editRect);

                    string t = _leftText ?? "";

                    using var sfEdit = (StringFormat)StringFormat.GenericTypographic.Clone();
                    sfEdit.Alignment = StringAlignment.Near;
                    sfEdit.LineAlignment = StringAlignment.Center;
                    sfEdit.Trimming = StringTrimming.None;
                    sfEdit.FormatFlags |= StringFormatFlags.NoClip;
                    sfEdit.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;

                    var st = g.Save();
                    g.SetClip(editRect);

                    // scroll
                    g.TranslateTransform(-_leftScrollX, 0);

                    g.DrawString(t, Font, txtBrush, editRect, sfEdit);

                    // Caret
                    if (Focused && _caretVisible)
                    {
                        int idx = Math.Max(0, Math.Min(_caretIndex, t.Length));
                        string before = t.Substring(0, idx);

                        int caretLocalX = MeasureTextWidthTypographic(g, before);

                        int xCaret = editRect.X + caretLocalX + 1;

                        int yTop = editRect.Y + 6;
                        int yBot = editRect.Bottom - 6;

                        using var penCaret = new Pen(_textColor, 1f);
                        g.DrawLine(penCaret, xCaret, yTop, xCaret, yBot);
                    }

                    g.Restore(st);
                }
                else
                {
                    var showRect = new Rectangle(
                        leftRect.X + _leftPad,
                        leftRect.Y,
                        Math.Max(1, leftRect.Width - (_leftPad * 2)),
                        leftRect.Height
                    );

                    using var sf = new StringFormat(StringFormat.GenericTypographic)
                    {
                        Alignment = StringAlignment.Near,   // left aligned
                        LineAlignment = StringAlignment.Center
                    };
                    sf.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;

                    g.DrawString(_leftText ?? "", Font, txtBrush, showRect, sf);
                }
            }

            // ✅ Right section only when HasRightPart
            if (HasRightPart)
            {
                string rightText = SelectedItem ?? "";
                var textRect = new Rectangle(rightRect.X + 10, rightRect.Y, rightRect.Width - 30, rightRect.Height);

                using (var sf2 = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center })
                using (var txtBrush2 = new SolidBrush(_textColor))
                {
                    g.DrawString(rightText, Font, txtBrush2, textRect, sf2);
                }

                DrawChevron(g, new Rectangle(rightRect.Right - 22, 0, 22, h), _arrowColor);

                if (_pressed)
                {
                    using var overlay = new SolidBrush(_pressedOverlay);
                    g.FillRectangle(overlay, rightRect);
                }
            }

            // Focus cue (rounded + clean)
            if (Focused)
            {
                int inset = 2;
                var r = new Rectangle(inset, inset, w - 1 - inset * 2, h - 1 - inset * 2);
                int rr = Math.Max(0, _cornerRadius - inset);

                using var focusPath = RoundedRectPath(r, rr);

                using var focusPen = new Pen(Color.FromArgb(140, 0, 120, 215), 1.3f)
                {
                    Alignment = PenAlignment.Inset,
                    LineJoin = LineJoin.Round
                };
                g.DrawPath(focusPen, focusPath);

                using var glowPen = new Pen(Color.FromArgb(45, 0, 120, 215), 3.0f)
                {
                    Alignment = PenAlignment.Inset,
                    LineJoin = LineJoin.Round
                };
                g.DrawPath(glowPen, focusPath);
            }
        }

        private void PaintParentBackgroundSafe(Graphics g)
        {
            if (Parent == null)
            {
                using var br = new SolidBrush(SystemColors.Control);
                g.FillRectangle(br, ClientRectangle);
                return;
            }

            if (LicenseManager.UsageMode == LicenseUsageMode.Designtime)
            {
                using var br = new SolidBrush(Parent.BackColor);
                g.FillRectangle(br, ClientRectangle);
                return;
            }

            var state = g.Save();
            try
            {
                g.TranslateTransform(-Left, -Top);
                var pe = new PaintEventArgs(g, Parent.ClientRectangle);
                InvokePaintBackground(Parent, pe);
                InvokePaint(Parent, pe);
            }
            catch
            {
                using var br = new SolidBrush(Parent.BackColor);
                g.FillRectangle(br, ClientRectangle);
            }
            finally
            {
                g.Restore(state);
            }
        }

        // ---------- Input ----------
        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = false; _pressed = false; Invalidate(); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();

            GetSplitRects(out var leftRect, out var rightRect);

            if (e.Button == MouseButtons.Left)
            {
                // Left click: edit mode
                if (LeftEditable && leftRect.Contains(e.Location))
                {
                    StartLeftEdit();
                    SetCaretFromMouseX(e.X, leftRect);
                    _pressed = false;
                    Invalidate();
                    return;
                }

                if (!HasRightPart)
                    return;

                // Right side click
                if (_leftEditing)
                    StopLeftEdit();

                if (rightRect.Contains(e.Location))
                {
                    _pressed = true;
                    Invalidate();
                }
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left) return;

            GetSplitRects(out var leftRect, out var rightRect);

            if (_pressed)
            {
                _pressed = false;
                Invalidate();
            }

            if (!HasRightPart) return;

            if (rightRect.Contains(e.Location))
                TogglePopup();
        }

        private void SetCaretFromMouseX(int mouseX, Rectangle leftRect)
        {
            if (!_leftEditing) return;

            var editRect = new Rectangle(
                leftRect.X + _leftPad,
                leftRect.Y,
                Math.Max(1, leftRect.Width - (_leftPad * 2)),
                leftRect.Height
            );

            string t = _leftText ?? "";
            int xLocal = mouseX - editRect.X + _leftScrollX;

            using var bmp = new Bitmap(1, 1);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int best = 0;
            int bestDist = int.MaxValue;

            for (int i = 0; i <= t.Length; i++)
            {
                int w = MeasureTextWidthTypographic(g, (i == 0) ? "" : t.Substring(0, i));
                int dist = Math.Abs(w - xLocal);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = i;
                }
            }

            _caretIndex = best;
            _caretVisible = true;
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            if (_leftEditing) StopLeftEdit();
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            base.OnKeyPress(e);

            if (!_leftEditing || !LeftEditable) return;

            char ch = e.KeyChar;

            if (ch == (char)Keys.Enter)
            {
                StopLeftEdit();
                e.Handled = true;
                return;
            }

            if (ch == (char)Keys.Back) return;
            if (char.IsControl(ch)) return;

            if (LeftNumericOnly)
            {
                char dec = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator[0];

                bool isDigit = char.IsDigit(ch);
                bool isDecimal = LeftAllowDecimal && (ch == '.' || ch == dec);

                if (!isDigit && !isDecimal)
                {
                    e.Handled = true;
                    return;
                }

                if (isDecimal && ch != dec)
                    ch = dec;

                string t0 = _leftText ?? "";

                if (isDecimal && t0.Contains(dec))
                {
                    e.Handled = true;
                    return;
                }

                if (isDecimal && _caretIndex == 0 && t0.Length == 0)
                {
                    _leftText = "0";
                    _caretIndex = 1;
                    t0 = _leftText;
                }
            }

            string t = _leftText ?? "";
            _caretIndex = Math.Max(0, Math.Min(_caretIndex, t.Length));

            t = t.Insert(_caretIndex, ch.ToString());
            _caretIndex++;

            _leftText = t;

            LeftTextChanged?.Invoke(this, EventArgs.Empty);
            ValuePairChanged?.Invoke(this, EventArgs.Empty);

            _caretVisible = true;
            Invalidate();
            e.Handled = true;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            // Left editing keys
            if (_leftEditing && LeftEditable)
            {
                string t = _leftText ?? "";

                if (e.KeyCode == Keys.Escape)
                {
                    StopLeftEdit();
                    e.Handled = true;
                    return;
                }

                if (e.KeyCode == Keys.Left)
                {
                    _caretIndex = Math.Max(0, _caretIndex - 1);
                    _caretVisible = true;
                    Invalidate();
                    e.Handled = true;
                    return;
                }

                if (e.KeyCode == Keys.Right)
                {
                    _caretIndex = Math.Min(t.Length, _caretIndex + 1);
                    _caretVisible = true;
                    Invalidate();
                    e.Handled = true;
                    return;
                }

                if (e.KeyCode == Keys.Home)
                {
                    _caretIndex = 0;
                    _caretVisible = true;
                    Invalidate();
                    e.Handled = true;
                    return;
                }

                if (e.KeyCode == Keys.End)
                {
                    _caretIndex = t.Length;
                    _caretVisible = true;
                    Invalidate();
                    e.Handled = true;
                    return;
                }

                if (e.KeyCode == Keys.Back)
                {
                    if (_caretIndex > 0 && t.Length > 0)
                    {
                        t = t.Remove(_caretIndex - 1, 1);
                        _caretIndex--;
                        _leftText = t;

                        LeftTextChanged?.Invoke(this, EventArgs.Empty);
                        ValuePairChanged?.Invoke(this, EventArgs.Empty);

                        _caretVisible = true;
                        Invalidate();
                    }
                    e.Handled = true;
                    return;
                }

                if (e.KeyCode == Keys.Delete)
                {
                    if (_caretIndex < t.Length)
                    {
                        t = t.Remove(_caretIndex, 1);
                        _leftText = t;

                        LeftTextChanged?.Invoke(this, EventArgs.Empty);
                        ValuePairChanged?.Invoke(this, EventArgs.Empty);

                        _caretVisible = true;
                        Invalidate();
                    }
                    e.Handled = true;
                    return;
                }

                if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Down || e.KeyCode == Keys.Up)
                {
                    e.Handled = true;
                    return;
                }
            }

            base.OnKeyDown(e);

            if (_leftEditing) return;

            // ✅ if no items -> ignore dropdown keys completely
            if (!HasRightPart) return;

            if (e.Alt && e.KeyCode == Keys.Down)
            {
                TogglePopup();
                e.Handled = true;
                return;
            }

            if (e.KeyCode == Keys.Down)
            {
                if (IsPopupOpen()) _popup!.MoveHover(1);
                else SelectedIndex = Math.Min(_items.Count - 1, _selectedIndex + 1);
                e.Handled = true;
                return;
            }

            if (e.KeyCode == Keys.Up)
            {
                if (IsPopupOpen()) _popup!.MoveHover(-1);
                else SelectedIndex = Math.Max(0, _selectedIndex - 1);
                e.Handled = true;
                return;
            }

            if (e.KeyCode == Keys.Enter)
            {
                if (!IsPopupOpen()) ShowPopup();
                else _popup!.CommitHover();
                e.Handled = true;
                return;
            }

            if (e.KeyCode == Keys.Escape)
            {
                HidePopup();
                e.Handled = true;
                return;
            }
        }

        private void StartLeftEdit()
        {
            _leftEditing = true;
            _caretIndex = (_leftText ?? "").Length;
            _caretVisible = true;

            if (LicenseManager.UsageMode != LicenseUsageMode.Designtime)
                _caretTimer.Start();

            Invalidate();
        }

        private void StopLeftEdit()
        {
            _leftEditing = false;
            _caretVisible = true;

            if (LicenseManager.UsageMode != LicenseUsageMode.Designtime)
                _caretTimer.Stop();

            Invalidate();
        }

        // ---------- Popup ----------
        private bool IsPopupOpen() => _popup != null && !_popup.IsDisposed && _popup.Visible;

        private void TogglePopup()
        {
            if (!HasRightPart) return;
            if (IsPopupOpen()) HidePopup();
            else ShowPopup();
        }

        private void ShowPopup()
        {
            if (!HasRightPart) return;

            HidePopup();

            _popup = new DropPopup(this);
            _popup.SetItems(_items.ToList(), _selectedIndex);

            _popup.ItemPicked += (s, idx) =>
            {
                SelectedIndex = idx;
                HidePopup();
            };
            _popup.Canceled += (s, e) => HidePopup();

            var screen = RectangleToScreen(ClientRectangle);
            _popup.ShowBelow(screen.Left, screen.Bottom, Width);
        }

        private void HidePopup()
        {
            if (_popup == null) return;
            try { if (!_popup.IsDisposed) _popup.Close(); } catch { }
            _popup = null;
        }

        // ---------- Conversion core ----------
        private bool TryGetLeftValue(out double val)
        {
            val = 0;

            string s = (_leftText ?? "").Trim();
            if (string.IsNullOrWhiteSpace(s)) return false;

            if (double.TryParse(s, NumberStyles.Any, CultureInfo.CurrentCulture, out val))
                return true;

            if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out val))
                return true;

            return false;
        }

        private void TryConvertLeftTextOnUnitChange(string? oldUnit, string? newUnit)
        {
            if (_autoConverting) return;
            if (string.IsNullOrWhiteSpace(_quantityName)) return;
            if (string.IsNullOrWhiteSpace(oldUnit)) return;
            if (string.IsNullOrWhiteSpace(newUnit)) return;

            if (oldUnit.Equals(newUnit, StringComparison.OrdinalIgnoreCase))
                return;

            if (!TryGetLeftValue(out double oldValue))
                return;

            try
            {
                _autoConverting = true;

                // IMPORTANT:
                // Your UnitConverisonEngine MUST have overload:
                // Convert(quantity, inputValue, fromUnit, toUnit)
                var (newValue, _) = UnitConverisonEngine.convert(
                    _quantityName!,
                    oldValue,
                    oldUnit!,
                    newUnit!
                );

                _leftText = newValue.ToString(_numberFormat, CultureInfo.CurrentCulture);
                _caretIndex = Math.Min(_caretIndex, _leftText.Length);
                _leftScrollX = 0;

                LeftTextChanged?.Invoke(this, EventArgs.Empty);
                ValuePairChanged?.Invoke(this, EventArgs.Empty);
                Invalidate();
            }
            catch
            {
                // Safe fail: do not break UI
            }
            finally
            {
                _autoConverting = false;
            }
        }

        /// <summary>
        /// Returns current numeric value from LeftText (in CurrentUnit).
        /// </summary>
        public bool TryGetCurrentValue(out double value)
        {
            return TryGetLeftValue(out value);
        }

        /// <summary>
        /// Returns:
        /// - currentValue in CurrentUnit
        /// - defaultValue in DefaultUnit (converted)
        /// </summary>
        public bool TryGetBothValues(
    out double currentValue, out string currentUnit,
    out double defaultValue, out string defaultUnit,
    out string? error)
        {
            currentValue = 0;
            defaultValue = 0;
            currentUnit = (CurrentUnit ?? "").Trim();
            defaultUnit = (DefaultUnit ?? "").Trim();
            error = null;

            try
            {
                if (string.IsNullOrWhiteSpace(_quantityName))
                {
                    error = "QuantityName is empty.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(currentUnit))
                {
                    error = "CurrentUnit is empty (SelectedIndex/Items not set).";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(defaultUnit))
                {
                    error = "DefaultUnit is empty.";
                    return false;
                }

                if (!TryGetLeftValue(out currentValue))
                {
                    error = $"LeftText is not numeric: '{(_leftText ?? "")}'";
                    return false;
                }

                if (currentUnit.Equals(defaultUnit, StringComparison.OrdinalIgnoreCase))
                {
                    defaultValue = currentValue;
                    return true;
                }

                var (dv, _) = UnitConverisonEngine.convert(
                    _quantityName!,
                    currentValue,
                    currentUnit,
                    defaultUnit
                );

                defaultValue = dv;
                return true;
            }
            catch (Exception ex)
            {
                error =
                    "TryGetBothValues failed.\n\n" +
                    $"Quantity: '{_quantityName}'\n" +
                    $"CurrentUnit: '{currentUnit}'\n" +
                    $"DefaultUnit: '{defaultUnit}'\n" +
                    $"LeftText: '{(_leftText ?? "")}'\n\n" +
                    ex.Message;

                return false;
            }
        }


        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                HidePopup();
                try
                {
                    _caretTimer.Stop();
                    _caretTimer.Dispose();
                }
                catch { }
            }
            base.Dispose(disposing);
        }

        // ---------- Drawing helpers ----------
        private static void DrawChevron(Graphics g, Rectangle r, Color color)
        {
            int cx = r.Left + r.Width / 2;
            int cy = r.Top + r.Height / 2;

            Point p1 = new Point(cx - 5, cy - 2);
            Point p2 = new Point(cx, cy + 3);
            Point p3 = new Point(cx + 5, cy - 2);

            using var pen = new Pen(color, 1.6f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            };
            g.DrawLines(pen, new[] { p1, p2, p3 });
        }

        private static GraphicsPath RoundedRectPath(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            if (radius <= 0)
            {
                path.AddRectangle(r);
                path.CloseFigure();
                return path;
            }

            int d = radius * 2;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        // ---------- Popup form ----------
        private sealed class DropPopup : Form
        {
            private readonly ExtendedTextBox _owner;
            private List<string> _items = new();
            private int _selectedIndex;
            private int _hoverIndex;

            private readonly int _itemHeight = 26;
            private readonly int _maxVisible = 8;

            private readonly Color _border = Color.FromArgb(210, 210, 210);
            private readonly Color _bg = Color.White;
            private readonly Color _hoverBg = Color.FromArgb(235, 242, 252);
            private readonly Color _text = Color.FromArgb(40, 40, 40);

            public event EventHandler<int>? ItemPicked;
            public event EventHandler? Canceled;

            public DropPopup(ExtendedTextBox owner)
            {
                _owner = owner;

                FormBorderStyle = FormBorderStyle.None;
                StartPosition = FormStartPosition.Manual;
                ShowInTaskbar = false;
                TopMost = true;
                DoubleBuffered = true;
                KeyPreview = true;

                Deactivate += (s, e) => Canceled?.Invoke(this, EventArgs.Empty);
            }

            public void SetItems(List<string> items, int selectedIndex)
            {
                _items = items ?? new List<string>();
                _selectedIndex = selectedIndex;
                _hoverIndex = (_selectedIndex >= 0) ? _selectedIndex : 0;

                int visible = Math.Min(_maxVisible, Math.Max(1, _items.Count));
                Height = visible * _itemHeight + 2;
            }

            public void ShowBelow(int x, int y, int width)
            {
                Width = Math.Max(60, width);
                Location = new Point(x, y);
                Show();
                BringToFront();
            }

            public void MoveHover(int delta)
            {
                if (_items.Count == 0) return;
                _hoverIndex = Math.Max(0, Math.Min(_items.Count - 1, _hoverIndex + delta));
                Invalidate();
            }

            public void CommitHover()
            {
                if (_items.Count == 0) return;
                ItemPicked?.Invoke(this, _hoverIndex);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);

                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                using (var br = new SolidBrush(_bg))
                    g.FillRectangle(br, ClientRectangle);

                using (var pen = new Pen(_border, 1f))
                    g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);

                if (_items == null || _items.Count == 0) return;

                int visibleCount = Math.Max(1, (Height - 2) / _itemHeight);
                int count = Math.Min(_items.Count, visibleCount);

                var rowsClip = new Rectangle(1, 1, Width - 2, Height - 2);
                var state = g.Save();
                g.SetClip(rowsClip);

                try
                {
                    for (int i = 0; i < count; i++)
                    {
                        var itemRect = new Rectangle(1, 1 + i * _itemHeight, Width - 2, _itemHeight);

                        bool isHover = (i == _hoverIndex);
                        bool isSelected = (i == _selectedIndex);

                        if (isSelected)
                        {
                            using var selBr = new SolidBrush(Color.FromArgb(18, 0, 120, 215));
                            g.FillRectangle(selBr, itemRect);
                        }

                        if (isHover)
                        {
                            using var hbr = new SolidBrush(_hoverBg);
                            g.FillRectangle(hbr, itemRect);
                        }

                        var textRect = new Rectangle(itemRect.X + 10, itemRect.Y, itemRect.Width - 40, itemRect.Height);

                        string s = _items[i] ?? "";
                        TextRenderer.DrawText(
                            g,
                            s,
                            _owner.Font,
                            textRect,
                            _text,
                            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding
                        );

                        if (isSelected)
                            DrawTick(g, new Rectangle(itemRect.Right - 22, itemRect.Y, 18, itemRect.Height), _text);

                        if (i < count - 1)
                        {
                            using var rowPen = new Pen(Color.FromArgb(240, 240, 240), 1f);
                            int y = itemRect.Bottom - 1;
                            g.DrawLine(rowPen, itemRect.Left + 6, y, itemRect.Right - 6, y);
                        }
                    }
                }
                finally
                {
                    g.Restore(state);
                }
            }

            private static void DrawTick(Graphics g, Rectangle r, Color color)
            {
                int cx = r.Left + r.Width / 2;
                int cy = r.Top + r.Height / 2;

                Point a = new Point(cx - 5, cy);
                Point b = new Point(cx - 1, cy + 4);
                Point c = new Point(cx + 6, cy - 4);

                using var pen = new Pen(color, 1.6f)
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round,
                    LineJoin = LineJoin.Round
                };
                g.DrawLines(pen, new[] { a, b, c });
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                int idx = (e.Y - 1) / _itemHeight;
                idx = Math.Max(0, Math.Min(_items.Count - 1, idx));

                if (_hoverIndex != idx)
                {
                    _hoverIndex = idx;
                    Invalidate();
                }
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                base.OnMouseDown(e);
                if (e.Button == MouseButtons.Left)
                {
                    int idx = (e.Y - 1) / _itemHeight;
                    if (idx >= 0 && idx < _items.Count)
                        ItemPicked?.Invoke(this, idx);
                }
            }

            protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
            {
                if (keyData == Keys.Escape) { Canceled?.Invoke(this, EventArgs.Empty); return true; }
                if (keyData == Keys.Enter) { CommitHover(); return true; }
                if (keyData == Keys.Down) { MoveHover(1); return true; }
                if (keyData == Keys.Up) { MoveHover(-1); return true; }

                return base.ProcessCmdKey(ref msg, keyData);
            }


        }
    }
}
