using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Drawing;
using System.Drawing.Design;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace Aarohi.Classes
{
    public class ExtendedDropDownList : Control
    {
        // ---------- Designer editor for string collection ----------
        public sealed class StringListEditor : CollectionEditor
        {
            public StringListEditor(Type type) : base(type) { }
            protected override Type CreateCollectionItemType() => typeof(string);
            protected override object CreateInstance(Type itemType) => string.Empty;
        }

        // ---------- Items collection that auto-refreshes ----------
        public sealed class StringItemCollection : Collection<string>
        {
            private readonly ExtendedDropDownList _owner;
            public StringItemCollection(ExtendedDropDownList owner) => _owner = owner;

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
        private DropPopup? _popup;

        private bool _hover;
        private bool _pressed;

        // ---- “Other” custom typing (paint-only) ----
        private bool _editing = false;
        private int _caretIndex = 0;
        private bool _caretVisible = true;
        private readonly Timer _caretTimer = new Timer();
        private int _scrollX = 0;

        private const int PAD_X = 10;
        private const int ARROW_W = 28;

        private string _customText = "";

        // ---- style ----
        private int _cornerRadius = 8;
        private Color _borderColor = Color.FromArgb(210, 210, 210);
        private Color _hoverBorderColor = Color.FromArgb(170, 170, 170);
        private Color _fillColor = Color.White;
        private Color _textColor = Color.FromArgb(40, 40, 40);
        private Color _arrowColor = Color.FromArgb(60, 60, 60);
        private Color _pressedOverlay = Color.FromArgb(18, 0, 0, 0);

        // ---- events ----
        public event EventHandler? SelectedIndexChanged;
        public event EventHandler? CustomTextChanged;

        private bool HasItems => _items != null && _items.Count > 0;

        // ===================== PUBLIC PROPERTIES =====================

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        [Editor(typeof(StringListEditor), typeof(UITypeEditor))]
        public StringItemCollection Items => _items;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                int newVal = value;
                if (_items.Count == 0) newVal = -1;
                else newVal = Math.Max(0, Math.Min(_items.Count - 1, newVal));

                if (_selectedIndex != newVal)
                {
                    _selectedIndex = newVal;

                    // Auto-enter edit when selecting Other
                    if (IsOtherSelected && AllowCustomEntryWhenOther)
                        BeginEdit();
                    else
                        EndEdit();

                    Invalidate();
                    SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public string? SelectedItem =>
            (_selectedIndex >= 0 && _selectedIndex < _items.Count) ? _items[_selectedIndex] : null;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string? SelectedItemText
        {
            get => SelectedItem;
            set
            {
                if (_items.Count == 0)
                {
                    SelectedIndex = -1;
                    return;
                }

                if (value == null)
                {
                    SelectedIndex = -1;
                    return;
                }

                int idx = -1;
                for (int i = 0; i < _items.Count; i++)
                {
                    if (string.Equals(_items[i], value, StringComparison.OrdinalIgnoreCase))
                    {
                        idx = i;
                        break;
                    }
                }

                if (idx >= 0)
                {
                    SelectedIndex = idx; // will trigger edit if "Other"
                }
                else
                {
                    // if you want: auto-select Other and put custom text
                    if (AllowCustomEntryWhenOther)
                    {
                        int otherIdx = -1;
                        for (int i = 0; i < _items.Count; i++)
                        {
                            if (string.Equals(_items[i], OtherItemText, StringComparison.OrdinalIgnoreCase))
                            {
                                otherIdx = i;
                                break;
                            }
                        }

                        if (otherIdx >= 0)
                        {
                            SelectedIndex = otherIdx;
                            CustomText = value; // typed value goes here
                            CustomTextChanged?.Invoke(this, EventArgs.Empty);
                            Invalidate();
                        }
                        else
                        {
                            // no Other item exists, just select first
                            SelectedIndex = 0;
                        }
                    }
                    else
                    {
                        SelectedIndex = 0;
                    }
                }
            }
        }


        // The label that triggers editable mode
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public string OtherItemText { get; set; } = "Other";

        // Enable/disable Other typing feature
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public bool AllowCustomEntryWhenOther { get; set; } = true;

        // Custom text typed when “Other” selected
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public string CustomText
        {
            get => _customText;
            set
            {
                _customText = value ?? "";
                _caretIndex = Math.Min(_caretIndex, _customText.Length);
                _scrollX = 0;
                Invalidate();
            }
        }

        // The final value your form should use:
        // - if Other selected => CustomText
        // - else => SelectedItem
        [Browsable(false)]
        public string? Value => (IsOtherSelected && AllowCustomEntryWhenOther)
            ? (CustomText ?? "")
            : (SelectedItem ?? "");

        [Browsable(false)]
        private bool IsOtherSelected =>
            (SelectedItem ?? "").Equals(OtherItemText ?? "Other", StringComparison.OrdinalIgnoreCase);

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public int CornerRadius
        {
            get => _cornerRadius;
            set { _cornerRadius = Math.Max(0, value); UpdateRoundedRegion(); Invalidate(); }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public Color BorderColor { get => _borderColor; set { _borderColor = value; Invalidate(); } }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public Color HoverBorderColor { get => _hoverBorderColor; set { _hoverBorderColor = value; Invalidate(); } }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public Color FillColor { get => _fillColor; set { _fillColor = value; Invalidate(); } }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public Color TextColor { get => _textColor; set { _textColor = value; Invalidate(); } }

        // ===================== CTOR =====================

        public ExtendedDropDownList()
        {
            _items = new StringItemCollection(this);

            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.UserPaint |
                     ControlStyles.Selectable, true);

            TabStop = true;
            Size = new Size(180, 32);
            Font = new Font("Segoe UI", 9f);

            _caretTimer.Interval = 500;
            _caretTimer.Tick += (s, e) =>
            {
                if (_editing && Focused)
                {
                    _caretVisible = !_caretVisible;
                    Invalidate();
                }
            };
        }

        // ===================== ITEMS CHANGED =====================

        private void OnItemsChanged()
        {
            if (!HasItems)
            {
                HidePopup();
                _selectedIndex = -1;
                EndEdit();
            }
            else
            {
                if (_selectedIndex < 0) _selectedIndex = 0;
                if (_selectedIndex >= _items.Count) _selectedIndex = _items.Count - 1;

                if (IsOtherSelected && AllowCustomEntryWhenOther)
                    BeginEdit();
                else
                    EndEdit();
            }
            Invalidate();
        }

        // ===================== REGION =====================

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

        protected override void OnPaintBackground(PaintEventArgs pevent) { /* keep empty */ }

        // ===================== PAINT =====================

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (var path = RoundedRectPath(new Rectangle(0, 0, Width - 1, Height - 1), _cornerRadius))
            using (var fill = new SolidBrush(_fillColor))
            using (var pen = new Pen(_hover ? _hoverBorderColor : _borderColor, 1f))
            {
                g.FillPath(fill, path);
                g.DrawPath(pen, path);
            }

            var textRect = new Rectangle(PAD_X, 0, Width - PAD_X - ARROW_W, Height - 1);
            var arrowRect = new Rectangle(Width - ARROW_W, 0, ARROW_W, Height);

            // Text to display
            string displayText;
            if (IsOtherSelected && AllowCustomEntryWhenOther)
                displayText = CustomText ?? "";
            else
                displayText = SelectedItem ?? "";

            // draw text (editable = custom paint + caret)
            if (_editing && IsOtherSelected && AllowCustomEntryWhenOther)
            {
                DrawEditableText(g, textRect, displayText);
            }
            else
            {
                TextRenderer.DrawText(
                    g, displayText, Font, textRect, _textColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            }

            // Arrow only if items exist
            if (HasItems)
                DrawChevron(g, arrowRect, _arrowColor);

            // press overlay on arrow click
            if (_pressed && HasItems)
            {
                using var overlay = new SolidBrush(_pressedOverlay);
                g.FillRectangle(overlay, arrowRect);
            }

            // Focus ring
            if (Focused)
            {
                int inset = 2;
                var r = new Rectangle(inset, inset, Width - 1 - inset * 2, Height - 1 - inset * 2);
                int rr = Math.Max(0, _cornerRadius - inset);

                using var focusPath = RoundedRectPath(r, rr);
                using var focusPen = new Pen(Color.FromArgb(140, 0, 120, 215), 1.3f)
                {
                    Alignment = PenAlignment.Inset,
                    LineJoin = LineJoin.Round
                };
                g.DrawPath(focusPen, focusPath);
            }
        }

        private void DrawEditableText(Graphics g, Rectangle rect, string text)
        {
            // ensure caret visible by scrolling
            EnsureCaretVisible(g, rect, text);

            var st = g.Save();
            g.SetClip(rect);

            // shift for scroll
            g.TranslateTransform(-_scrollX, 0);

            TextRenderer.DrawText(
                g, text, Font, rect, _textColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            // caret
            if (Focused && _caretVisible)
            {
                int xCaret = rect.X + MeasureTextWidth(g, text.Substring(0, Math.Min(_caretIndex, text.Length))) + 1;
                int y1 = rect.Y + 6;
                int y2 = rect.Bottom - 6;
                using var pen = new Pen(_textColor, 1f);
                g.DrawLine(pen, xCaret, y1, xCaret, y2);
            }

            g.Restore(st);
        }

        private int MeasureTextWidth(Graphics g, string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            var size = TextRenderer.MeasureText(g, text, Font, new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.NoPadding | TextFormatFlags.NoClipping);
            return size.Width;
        }

        private void EnsureCaretVisible(Graphics g, Rectangle rect, string text)
        {
            int idx = Math.Max(0, Math.Min(_caretIndex, text.Length));
            int caretX = MeasureTextWidth(g, text.Substring(0, idx));
            int visibleX = caretX - _scrollX;

            int rightLimit = Math.Max(0, rect.Width - 2);
            if (visibleX > rightLimit) _scrollX = caretX - rightLimit;
            if (visibleX < 0) _scrollX = caretX;

            if (_scrollX < 0) _scrollX = 0;
        }

        // ===================== MOUSE / KEYBOARD =====================

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = false; _pressed = false; Invalidate(); }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            var arrowRect = new Rectangle(Width - ARROW_W, 0, ARROW_W, Height);
            var textRect = new Rectangle(0, 0, Width - ARROW_W, Height);

            if (IsOtherSelected && AllowCustomEntryWhenOther && textRect.Contains(e.Location))
                Cursor = Cursors.IBeam;
            else if (HasItems)
                Cursor = Cursors.Hand;
            else
                Cursor = Cursors.Default;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();

            if (!HasItems) return;

            var arrowRect = new Rectangle(Width - ARROW_W, 0, ARROW_W, Height);
            var textRect = new Rectangle(0, 0, Width - ARROW_W, Height);

            if (e.Button == MouseButtons.Left)
            {
                // Click arrow -> dropdown always
                if (arrowRect.Contains(e.Location))
                {
                    _pressed = true;
                    Invalidate();
                    return;
                }

                // If Other selected -> click text area enters edit
                if (IsOtherSelected && AllowCustomEntryWhenOther && textRect.Contains(e.Location))
                {
                    BeginEdit();
                    // move caret to end (simple). If you want click-position caret, tell me.
                    _caretIndex = (CustomText ?? "").Length;
                    _caretVisible = true;
                    Invalidate();
                    return;
                }

                // otherwise click body toggles dropdown
                EndEdit();
                TogglePopup();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left) return;

            if (_pressed)
            {
                _pressed = false;
                Invalidate();

                var arrowRect = new Rectangle(Width - ARROW_W, 0, ARROW_W, Height);
                if (arrowRect.Contains(e.Location))
                {
                    EndEdit();
                    TogglePopup();
                }
            }
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            if (_editing) EndEdit();
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            base.OnKeyPress(e);

            if (!_editing) return;
            if (!(IsOtherSelected && AllowCustomEntryWhenOther)) return;

            char ch = e.KeyChar;

            if (ch == (char)Keys.Enter)
            {
                EndEdit();
                e.Handled = true;
                return;
            }

            if (ch == (char)Keys.Back) return; // handled in KeyDown
            if (char.IsControl(ch)) return;

            var t = CustomText ?? "";
            _caretIndex = Math.Max(0, Math.Min(_caretIndex, t.Length));
            t = t.Insert(_caretIndex, ch.ToString());
            _caretIndex++;

            CustomText = t;
            CustomTextChanged?.Invoke(this, EventArgs.Empty);

            _caretVisible = true;
            Invalidate();
            e.Handled = true;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (!HasItems) return;

            // Editing keys
            if (_editing && IsOtherSelected && AllowCustomEntryWhenOther)
            {
                var t = CustomText ?? "";

                if (e.KeyCode == Keys.Escape)
                {
                    EndEdit();
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
                        CustomText = t;
                        CustomTextChanged?.Invoke(this, EventArgs.Empty);
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
                        CustomText = t;
                        CustomTextChanged?.Invoke(this, EventArgs.Empty);
                        _caretVisible = true;
                        Invalidate();
                    }
                    e.Handled = true;
                    return;
                }

                // Allow Alt+Down to open dropdown even in edit
                if (e.Alt && e.KeyCode == Keys.Down)
                {
                    EndEdit();
                    TogglePopup();
                    e.Handled = true;
                    return;
                }

                return;
            }

            // Normal dropdown navigation when not editing
            if (e.Alt && e.KeyCode == Keys.Down)
            {
                TogglePopup();
                e.Handled = true;
                return;
            }

            if (e.KeyCode == Keys.Down)
            {
                if (!IsPopupOpen())
                    SelectedIndex = Math.Min(_items.Count - 1, _selectedIndex + 1);
                else
                    _popup!.MoveHover(1);
                e.Handled = true;
                return;
            }

            if (e.KeyCode == Keys.Up)
            {
                if (!IsPopupOpen())
                    SelectedIndex = Math.Max(0, _selectedIndex - 1);
                else
                    _popup!.MoveHover(-1);
                e.Handled = true;
                return;
            }

            if (e.KeyCode == Keys.Enter)
            {
                TogglePopup();
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

        // ===================== EDIT MODE =====================

        private void BeginEdit()
        {
            if (!_editing)
            {
                _editing = true;
                _caretVisible = true;
                _caretIndex = Math.Min(_caretIndex, (CustomText ?? "").Length);
                _caretTimer.Start();
                Invalidate();
            }
        }

        private void EndEdit()
        {
            if (_editing)
            {
                _editing = false;
                _caretTimer.Stop();
                _caretVisible = true;
                _scrollX = 0;
                Invalidate();
            }
        }

        // ===================== POPUP =====================

        private bool IsPopupOpen() => _popup != null && !_popup.IsDisposed && _popup.Visible;

        private void TogglePopup()
        {
            if (!HasItems) return;
            if (IsPopupOpen()) HidePopup();
            else ShowPopup();
        }

        private void ShowPopup()
        {
            if (!HasItems) return;

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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                HidePopup();
                try { _caretTimer.Stop(); _caretTimer.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }

        // ===================== HELPERS =====================

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
            if (radius <= 0) { path.AddRectangle(r); path.CloseFigure(); return path; }

            int d = radius * 2;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        // ===================== POPUP FORM (paint-only) =====================

        private sealed class DropPopup : Form
        {
            private readonly ExtendedDropDownList _owner;
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

            public DropPopup(ExtendedDropDownList owner)
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

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                using (var br = new SolidBrush(_bg))
                    g.FillRectangle(br, ClientRectangle);

                using (var pen = new Pen(_border, 1f))
                    g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);

                if (_items.Count == 0) return;

                int visibleCount = Math.Max(1, (Height - 2) / _itemHeight);
                int count = Math.Min(_items.Count, visibleCount);

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

                    var textRect = new Rectangle(itemRect.X + 10, itemRect.Y, itemRect.Width - 20, itemRect.Height);
                    TextRenderer.DrawText(
                        g,
                        _items[i] ?? "",
                        _owner.Font,
                        textRect,
                        _text,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding
                    );

                    if (i < count - 1)
                    {
                        using var rowPen = new Pen(Color.FromArgb(240, 240, 240), 1f);
                        g.DrawLine(rowPen, itemRect.Left + 6, itemRect.Bottom - 1, itemRect.Right - 6, itemRect.Bottom - 1);
                    }
                }
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                if (_items.Count == 0) return;

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
                if (keyData == Keys.Enter) { ItemPicked?.Invoke(this, _hoverIndex); return true; }
                if (keyData == Keys.Down) { MoveHover(1); return true; }
                if (keyData == Keys.Up) { MoveHover(-1); return true; }

                return base.ProcessCmdKey(ref msg, keyData);
            }
        }
    }
}
