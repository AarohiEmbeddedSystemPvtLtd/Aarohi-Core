using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;

namespace Aarohi.Classes
{
    public class ExtendedDataGridView : DataGridView
    {
        #region ==== Nested: FilterHeaderCell ====
        public sealed class FilterHeaderCell : DataGridViewColumnHeaderCell
        {
            private const int ButtonWidth = 18;
            private const int ButtonHeight = 16;

            private bool _hover;
            private bool _pressed;
            private Rectangle _buttonBounds;

            public event EventHandler? DropDownClicked;

            public Rectangle ButtonBounds => _buttonBounds;

            internal Rectangle GetButtonBoundsFromGrid()
            {
                if (DataGridView == null || OwningColumn == null) return Rectangle.Empty;

                // Header cell rectangle in client coordinates
                Rectangle cellRect = DataGridView.GetCellDisplayRectangle(OwningColumn.Index, -1, true);

                // Button in client coordinates (right-align inside header cell)
                int bx = cellRect.Right - ButtonWidth - 4;
                int by = cellRect.Top + (cellRect.Height - ButtonHeight) / 2;
                return new Rectangle(bx, by, ButtonWidth, ButtonHeight);
            }

            internal void SetHoverPressed(bool hover, bool pressed)
            {
                if (_hover != hover || _pressed != pressed)
                {
                    _hover = hover;
                    _pressed = pressed;
                    DataGridView?.InvalidateCell(this);
                }
            }

            protected override Size GetPreferredSize(
                Graphics graphics,
                DataGridViewCellStyle cellStyle,
                int rowIndex,
                Size constraintSize)
            {
                var hdr = ResolveHeaderStyle(cellStyle);
                Size sz = base.GetPreferredSize(graphics, hdr, rowIndex, constraintSize);

                const int paddingAroundButton = 8; // ~4px left & right “breathing room”
                sz.Width += ButtonWidth + paddingAroundButton;
                return sz;
            }

            protected override void Paint(
                Graphics g,
                Rectangle clipBounds,
                Rectangle cellBounds,
                int rowIndex,
                DataGridViewElementStates dataGridViewElementState,
                object? value,
                object? formattedValue,
                string? errorText,
                DataGridViewCellStyle cellStyle,
                DataGridViewAdvancedBorderStyle advancedBorderStyle,
                DataGridViewPaintParts paintParts)
            {
                var hdr = ResolveHeaderStyle(cellStyle);

                // Paint background & borders but skip default text so we can layout with our button
                base.Paint(g, clipBounds, cellBounds, rowIndex,
                    dataGridViewElementState, value, formattedValue, errorText,
                    hdr, advancedBorderStyle, paintParts & ~DataGridViewPaintParts.ContentForeground);

                // Compute and cache the button rect (cell-relative)
                int bx = cellBounds.Right - ButtonWidth - 4;
                int by = cellBounds.Top + (cellBounds.Height - ButtonHeight) / 2;
                _buttonBounds = new Rectangle(bx, by, ButtonWidth, ButtonHeight);

                // Text rect (avoid the button area)
                var textRect = new Rectangle(
                    cellBounds.X + 4,
                    cellBounds.Y,
                    Math.Max(0, cellBounds.Width - ButtonWidth - 8),
                    cellBounds.Height);

                TextRenderer.DrawText(
                    g,
                    Convert.ToString(formattedValue) ?? string.Empty,
                    hdr.Font!,
                    textRect,
                    hdr.ForeColor,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);

                if (Application.RenderWithVisualStyles)
                {
                    var state = _pressed ? ComboBoxState.Pressed : (_hover ? ComboBoxState.Hot : ComboBoxState.Normal);
                    ComboBoxRenderer.DrawDropDownButton(g, _buttonBounds, state);
                }
                else
                {
                    var btnState = _pressed ? ButtonState.Pushed : ButtonState.Normal;
                    ControlPaint.DrawComboButton(g, _buttonBounds, btnState);
                }
            }

            protected override void OnMouseLeave(int rowIndex)
            {
                if (_hover || _pressed)
                {
                    _hover = false;
                    _pressed = false;
                    DataGridView?.InvalidateCell(this);
                }
                base.OnMouseLeave(rowIndex);
            }

            protected override void OnMouseMove(DataGridViewCellMouseEventArgs e)
            {
                var btn = GetButtonBoundsRelative();
                if (_buttonBounds != btn) _buttonBounds = btn;

                bool newHover = btn.Contains(e.Location);
                if (newHover != _hover)
                {
                    _hover = newHover;
                    DataGridView?.InvalidateCell(this);
                }
                base.OnMouseMove(e);
            }

            protected override void OnMouseDown(DataGridViewCellMouseEventArgs e)
            {
                var btn = GetButtonBoundsRelative();
                if (_buttonBounds != btn) _buttonBounds = btn;

                if (e.Button == MouseButtons.Left && btn.Contains(e.Location))
                {
                    _pressed = true;
                    DataGridView?.InvalidateCell(this);
                }
                base.OnMouseDown(e);
            }

            protected override void OnMouseUp(DataGridViewCellMouseEventArgs e)
            {
                var btn = GetButtonBoundsRelative();
                if (_buttonBounds != btn) _buttonBounds = btn;

                bool wasPressed = _pressed;
                _pressed = false;
                DataGridView?.InvalidateCell(this);

                if (e.Button == MouseButtons.Left && wasPressed && btn.Contains(e.Location))
                {
                    if (DataGridView is ExtendedDataGridView egv && OwningColumn != null)
                    {
                        egv.ShowHeaderMenuAt(this, OwningColumn);
                        egv._suppressNextHeaderSort = true;
                    }
                    DropDownClicked?.Invoke(this, EventArgs.Empty);
                }
                base.OnMouseUp(e);
            }

            private Rectangle GetButtonBoundsRelative()
            {
                // If already computed in Paint, reuse it
                if (_buttonBounds.Width > 0 && _buttonBounds.Height > 0)
                    return _buttonBounds;

                if (DataGridView == null || OwningColumn == null)
                    return _buttonBounds;

                // Use the header cell size (relative to the cell)
                Rectangle cellRect = DataGridView.GetCellDisplayRectangle(OwningColumn.Index, -1, true);
                int bx = cellRect.Width - ButtonWidth - 4;
                int by = (cellRect.Height - ButtonHeight) / 2;
                return new Rectangle(bx, by, ButtonWidth, ButtonHeight);
            }

            private DataGridViewCellStyle ResolveHeaderStyle(DataGridViewCellStyle cellStyle)
            {
                var style = new DataGridViewCellStyle(cellStyle);

                // 1) Per-cell overrides
                if (Style?.Font != null) style.Font = Style.Font;
                if (Style?.ForeColor != Color.Empty) style.ForeColor = Style.ForeColor;
                if (Style?.BackColor != Color.Empty) style.BackColor = Style.BackColor;

                // 2) Grid-level defaults
                var grid = DataGridView;
                if (grid != null)
                {
                    var ch = grid.ColumnHeadersDefaultCellStyle;
                    if (style.Font == null && ch?.Font != null) style.Font = ch.Font;
                    if (style.ForeColor == Color.Empty && ch?.ForeColor != Color.Empty) style.ForeColor = ch.ForeColor;
                    if (style.BackColor == Color.Empty && ch?.BackColor != Color.Empty) style.BackColor = ch.BackColor;
                }

                // 3) Fallbacks
                style.Font ??= SystemFonts.DefaultFont;
                if (style.ForeColor == Color.Empty) style.ForeColor = SystemColors.ControlText;
                if (style.BackColor == Color.Empty) style.BackColor = SystemColors.Control;

                return style;
            }
        }
        #endregion

        #region ==== Public API: Properties & Events ====
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Dictionary<string, Dictionary<string, string>> ForeignKeyColumns { get; set; }

        public event Action<string, string, object>? ForeignKeyCellClicked;

        public event Action<string>? HeaderDropDownOpened;

        public event Action<string /*column*/, string /*section*/, string /*action*/, string? /*value*/>? HeaderCommand;

        public bool TryGetSelectedRowData(out IDictionary<string, object?> data)
        {
            data = null!;
            DataGridViewRow? row = CurrentRow;

            // fallback to first selected row if CurrentRow is null
            if (row is null && SelectedRows.Count > 0)
                row = SelectedRows[0];

            if (row is null)
                return false;

            data = GetRowData(row); // uses your existing private static helper
            return true;
        }

        public IList<IDictionary<string, object?>> GetAllSelectedRowsData()
        {
            var list = new List<IDictionary<string, object?>>();
            foreach (DataGridViewRow r in SelectedRows)
                list.Add(GetRowData(r));
            return list;
        }

        #endregion

        #region ==== Private fields (menu, search, state) ====

        private readonly ContextMenuStrip _headerMenu = new ContextMenuStrip();
        private DataGridViewColumn? _menuForColumn;
        private TextBox? _searchTextBox;

        internal bool _suppressNextHeaderSort;

        private int _hotHeaderCol = -1;
        private int _pressedHeaderCol = -1;

        private readonly HashSet<string> _primaryKeyColumns = new(StringComparer.OrdinalIgnoreCase);

        private BindingSource? _bs;
        private DataView? _view;
        private readonly Dictionary<string, string> _columnFilters = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _originalHeaderText =
            new(StringComparer.OrdinalIgnoreCase);

        // Add next to _columnFilters, _originalHeaderText, etc.
        private readonly Dictionary<string, string> _fixedFilters =
            new(StringComparer.OrdinalIgnoreCase);

        public enum RowCommand { View, Edit, Delete }

        public event Action<RowCommand, DataGridViewRow>? RowCommandInvoked;
        // Optional convenience: same event but with the row turned into a dictionary
        public event Action<RowCommand, IDictionary<string, object?>>? RowCommandDataInvoked;

        private readonly ContextMenuStrip _rowMenu = new ContextMenuStrip();
        private int _rowMenuTargetIndex = -1;

        private DynamicClass _dynClass = new DynamicClass();
        private CancellationTokenSource? _dynamicLoadCts;

        [DefaultValue(50)]
        public int DynamicSelectChunkSize { get; set; } = 50;

        [DefaultValue(true)]
        public bool DynamicSelectLoadInChunks { get; set; } = true;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool IsDynamicSelectLoading { get; private set; }
        public event Action<int, int>? DynamicSelectChunkLoaded;
        public event Action<Exception>? DynamicSelectChunkLoadFailed;

        #endregion

        #region ==== Constructor ====
        public ExtendedDataGridView(DynamicClass dynamicClass)
        {
            _dynClass = dynamicClass;

            // Visuals
            Dock = DockStyle.Fill;
            BackgroundColor = Color.White;
            RowHeadersVisible = false;
            SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            AllowUserToAddRows = false;
            ReadOnly = true;
            AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            EnableHeadersVisualStyles = false;

            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            BorderStyle = BorderStyle.FixedSingle;
            Dock = DockStyle.Fill;

            AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(235, 240, 250),
                ForeColor = Color.Black,
            };

            DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.White,
                ForeColor = Color.Black,
                SelectionBackColor = Color.FromArgb(0, 123, 255),
                SelectionForeColor = Color.White,
                Font = new Font("Segoe UI", 12f, FontStyle.Regular),
            };

            ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.AliceBlue,
                ForeColor = Color.Black,
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                Alignment = DataGridViewContentAlignment.MiddleCenter,
            };

            Invalidate(); // ensure header repaints with new font

            // Foreign key extraction & data bind
            ForeignKeyColumns = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            ExtractForeignKeyColumns(dynamicClass);
            ExtractPrimaryKeyColumns(dynamicClass);

            // listen to header actions centrally
            this.HeaderCommand += OnHeaderCommand;

            // Cell/UI events
            CellMouseEnter += ExtendedDataGridView_CellMouseEnter;
            CellFormatting += ExtendedDataGridView_CellFormatting;
            CellDoubleClick += DataGridView_CellClick; // intentionally maps to same FK handler

            // Header button hit-testing via grid-level mouse events
            CellMouseMove += ExtendedDataGridView_CellMouseMove_HeaderButtons;
            CellMouseDown += ExtendedDataGridView_CellMouseDown_HeaderButtons;
            CellMouseUp += ExtendedDataGridView_CellMouseUp_HeaderButtons;

            CellMouseDown += ExtendedDataGridView_CellMouseDown_RowMenu;
            CellContextMenuStripNeeded += ExtendedDataGridView_CellContextMenuStripNeeded;

            // Header menu base items (will be rebuilt dynamically)
            _headerMenu.Items.Add("Open… (placeholder)", null, (_, __) =>
            {
                if (_menuForColumn != null)
                    HeaderDropDownOpened?.Invoke(_menuForColumn.Name);
            });
            _headerMenu.Items.Add(new ToolStripSeparator());
            _headerMenu.Items.Add("Close", null, (_, __) => _headerMenu.Close());

            // Ensure custom header cells are applied whenever columns exist/change
            DataBindingComplete += (_, __) =>
            {
                ApplyDisplayNameAndUnitFromMetadata();
                ApplyFilterHeaderCells();
                ApplyColumnVisibilityFromMetadata();
            };

            HandleCreated += (_, __) => ApplyFilterHeaderCells();
            ColumnAdded += (_, __) => ApplyFilterHeaderCells();
            ColumnHeaderMouseClick += ExtendedDataGridView_ColumnHeaderMouseClick;

            // Smoother scrolling/painting
            DoubleBuffered = true;

            BuildRowMenu();
            LoadDynamicClassData(DynamicSelectChunkSize, keepFilters: false);

        }

        public ExtendedDataGridView(DataTable data)
        {
            // Visuals
            Dock = DockStyle.Fill;
            BackgroundColor = Color.White;
            RowHeadersVisible = false;
            SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            AllowUserToAddRows = false;
            ReadOnly = true;
            AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            EnableHeadersVisualStyles = false;

            AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(235, 240, 250),
                ForeColor = Color.Black,
            };

            DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.White,
                ForeColor = Color.Black,
                SelectionBackColor = Color.FromArgb(0, 123, 255),
                SelectionForeColor = Color.White,
                Font = new Font("Segoe UI", 12f, FontStyle.Regular),
            };

            ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.AliceBlue,
                ForeColor = Color.Black,
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                Alignment = DataGridViewContentAlignment.MiddleCenter,
            };

            Invalidate(); // ensure header repaints with new font

            DataSource = data;
            EnsureBindingLayer();

            // listen to header actions centrally
            this.HeaderCommand += OnHeaderCommand;

            // Cell/UI events
            CellMouseEnter += ExtendedDataGridView_CellMouseEnter;
            CellFormatting += ExtendedDataGridView_CellFormatting;
            CellDoubleClick += DataGridView_CellClick; // intentionally maps to same FK handler

            // Header button hit-testing via grid-level mouse events
            CellMouseMove += ExtendedDataGridView_CellMouseMove_HeaderButtons;
            CellMouseDown += ExtendedDataGridView_CellMouseDown_HeaderButtons;
            CellMouseUp += ExtendedDataGridView_CellMouseUp_HeaderButtons;

            CellMouseDown += ExtendedDataGridView_CellMouseDown_RowMenu;
            CellContextMenuStripNeeded += ExtendedDataGridView_CellContextMenuStripNeeded;

            // Header menu base items (will be rebuilt dynamically)
            _headerMenu.Items.Add("Open… (placeholder)", null, (_, __) =>
            {
                if (_menuForColumn != null)
                    HeaderDropDownOpened?.Invoke(_menuForColumn.Name);
            });
            _headerMenu.Items.Add(new ToolStripSeparator());
            _headerMenu.Items.Add("Close", null, (_, __) => _headerMenu.Close());

            // Ensure custom header cells are applied whenever columns exist/change
            DataBindingComplete += (_, __) => ApplyFilterHeaderCells();
            HandleCreated += (_, __) => ApplyFilterHeaderCells();
            ColumnAdded += (_, __) => ApplyFilterHeaderCells();
            ColumnHeaderMouseClick += ExtendedDataGridView_ColumnHeaderMouseClick;

            // Smoother scrolling/painting
            DoubleBuffered = true;

            BuildRowMenu();

        }

        private void ExtractPrimaryKeyColumns(DynamicClass dynamicClass)
        {
            _primaryKeyColumns.Clear();
            foreach (var pk in dynamicClass.GetPrimaryKeyColumns() ?? new string[] { })
            {
                _primaryKeyColumns.Add(pk);
            }

        }

        #endregion

        #region ==== Header wiring & menu presentation ====

        #region Filter Cell Apply
        private void ApplyFilterHeaderCells()
        {
            if (Columns.Count == 0) return;

            foreach (DataGridViewColumn col in Columns)
            {
                // Always programmatic to avoid accidental sorts when clicking header area
                col.SortMode = DataGridViewColumnSortMode.Programmatic;

                if (col.HeaderCell is FilterHeaderCell)
                    continue;

                var text = col.HeaderCell?.Value;

                var custom = new FilterHeaderCell
                {
                    Value = text
                };

                // When the drop-down is clicked from inside the cell
                custom.DropDownClicked += (s, e) =>
                {
                    _suppressNextHeaderSort = true; // prevent immediate sort by header click
                    _menuForColumn = col;
                    BuildHeaderMenu(col);

                    if (s is FilterHeaderCell fhc)
                    {
                        Rectangle cellRect = GetCellDisplayRectangle(col.Index, -1, true);
                        var screenButtonRect = new Rectangle(
                            PointToScreen(new Point(
                                cellRect.Right - fhc.ButtonBounds.Width - 4,
                                cellRect.Top + (cellRect.Height - fhc.ButtonBounds.Height) / 2)),
                            fhc.ButtonBounds.Size);

                        _headerMenu.Show(screenButtonRect.Location.X, screenButtonRect.Bottom);
                    }
                    else
                    {
                        _headerMenu.Show(Cursor.Position);
                    }

                    BeginInvoke(new Action(() => _searchTextBox?.Focus()));
                };

                col.HeaderCell = custom;
                if (!_originalHeaderText.ContainsKey(col.Name))
                {
                    var baseVal = (col.HeaderCell?.Value as string);
                    _originalHeaderText[col.Name] = string.IsNullOrEmpty(baseVal) ? col.HeaderText : baseVal!;
                }

            }

            AutoResizeColumnHeadersHeight();
            AutoResizeColumns(AutoSizeColumnsMode);
            Invalidate(); // repaint headers with buttons

        }
        public void CloseHeaderMenu() => _headerMenu.Close();
        #endregion

        #region Event handling
        private void ExtendedDataGridView_ColumnHeaderMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
        {
            if (_suppressNextHeaderSort)
            {
                _suppressNextHeaderSort = false;
                return;
            }

            if (e.ColumnIndex < 0 || e.ColumnIndex >= Columns.Count) return;
            var col = Columns[e.ColumnIndex];

            var dir = (col.HeaderCell.SortGlyphDirection == SortOrder.Ascending)
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;

            try
            {
                Sort(col, dir);
                col.HeaderCell.SortGlyphDirection =
                    (dir == ListSortDirection.Ascending) ? SortOrder.Ascending : SortOrder.Descending;
            }
            catch
            {
                // Swallow sort exceptions to keep UX smooth (e.g., non-sortable data source)
            }
        }

        private void ExtendedDataGridView_CellMouseMove_HeaderButtons(object? sender, DataGridViewCellMouseEventArgs e)
        {
            // Only headers
            if (e.RowIndex != -1 || e.ColumnIndex < 0) return;

            if (Columns[e.ColumnIndex].HeaderCell is not FilterHeaderCell cell) return;

            Rectangle btn = cell.GetButtonBoundsFromGrid();

            // Convert current mouse to client coordinates reliably
            Point mouseClient = PointToClient(Control.MousePosition);
            bool over = btn.Contains(mouseClient);

            if (over)
            {
                _hotHeaderCol = e.ColumnIndex;
                cell.SetHoverPressed(true, _pressedHeaderCol == e.ColumnIndex);
                Cursor = Cursors.Arrow;
            }
            else
            {
                if (_hotHeaderCol == e.ColumnIndex) _hotHeaderCol = -1;
                cell.SetHoverPressed(false, false);
            }
        }

        private void ExtendedDataGridView_CellMouseDown_HeaderButtons(object? sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex != -1 || e.ColumnIndex < 0 || e.Button != MouseButtons.Left) return;
            if (Columns[e.ColumnIndex].HeaderCell is not FilterHeaderCell cell) return;

            Rectangle btn = cell.GetButtonBoundsFromGrid();
            Point mouseClient = PointToClient(Control.MousePosition);

            if (btn.Contains(mouseClient))
            {
                _pressedHeaderCol = e.ColumnIndex;
                _suppressNextHeaderSort = true;
                cell.SetHoverPressed(true, true);
            }
        }

        private void ExtendedDataGridView_CellMouseUp_HeaderButtons(object? sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex != -1 || e.ColumnIndex < 0 || e.Button != MouseButtons.Left) return;
            if (Columns[e.ColumnIndex].HeaderCell is not FilterHeaderCell cell) return;

            Rectangle btn = cell.GetButtonBoundsFromGrid();
            Point mouseClient = PointToClient(Control.MousePosition);

            bool wasPressedHere = (_pressedHeaderCol == e.ColumnIndex);
            _pressedHeaderCol = -1;

            // Clear pressed visual; keep hover state if still inside
            cell.SetHoverPressed(btn.Contains(mouseClient), false);

            if (wasPressedHere && btn.Contains(mouseClient))
            {
                ShowHeaderMenuAt(cell, Columns[e.ColumnIndex]);
                _suppressNextHeaderSort = true;
            }
        }

        #endregion

        #region Healper
        internal void ShowHeaderMenuAt(FilterHeaderCell fhc, DataGridViewColumn col)
        {
            _menuForColumn = col;
            BuildHeaderMenu(col);

            Rectangle cellRect = GetCellDisplayRectangle(col.Index, -1, true);
            var anchorClient = new Point(
                cellRect.Right - fhc.ButtonBounds.Width - 4,
                cellRect.Top + (cellRect.Height - fhc.ButtonBounds.Height) / 2 + fhc.ButtonBounds.Height);

            Point screenPoint = PointToScreen(anchorClient);
            _headerMenu.Show(screenPoint);

            BeginInvoke(new Action(() => _searchTextBox?.Focus()));
        }

        private void EnsureBindingLayer()
        {
            // If already wrapped, skip
            if (_bs != null && _view != null) return;

            if (DataSource is DataTable dt)
            {
                _view = new DataView(dt);
                _bs = new BindingSource { DataSource = _view };
                DataSource = _bs; // rebind to a filterable/sortable source
            }
            else if (DataSource is DataView dv)
            {
                _view = dv;
                _bs = new BindingSource { DataSource = dv };
                DataSource = _bs;
            }
            else if (DataSource is BindingSource bs)
            {
                _bs = bs;
                if (bs.List is DataView dv2) _view = dv2;
                else if (bs.DataSource is DataTable dt2) _view = dt2.DefaultView;
            }
            // Note: if you have other custom sources, you can adapt here.
        }

        private void ApplyCombinedFilter()
        {
            if (_view == null) return;

            // fixed filters (sticky) + user filters (clearable)
            var parts = new List<string>();
            parts.AddRange(_fixedFilters.Values.Where(s => !string.IsNullOrWhiteSpace(s)));
            parts.AddRange(_columnFilters.Values.Where(s => !string.IsNullOrWhiteSpace(s)));

            _view.RowFilter = string.Join(" AND ", parts);
            RefreshHeaderFilterBadges();
            Refresh();
        }


        private static string EscapeLike(string input)
        {
            // Escape single quotes for DataView RowFilter
            return input.Replace("'", "''");
        }

        private bool ColumnIsNumeric(string columnName)
        {
            // Try DataGridView column type first
            if (Columns.Contains(columnName))
            {
                var t = Columns[columnName].ValueType;
                if (t != null)
                {
                    var nt = Nullable.GetUnderlyingType(t) ?? t;
                    return nt == typeof(byte) || nt == typeof(short) || nt == typeof(int) ||
                           nt == typeof(long) || nt == typeof(float) || nt == typeof(double) ||
                           nt == typeof(decimal);
                }
            }

            // Fallback via DataView schema (if available)
            if (_view?.Table?.Columns.Contains(columnName) == true)
            {
                var t = _view.Table.Columns[columnName].DataType;
                var nt = Nullable.GetUnderlyingType(t) ?? t;
                return nt == typeof(byte) || nt == typeof(short) || nt == typeof(int) ||
                       nt == typeof(long) || nt == typeof(float) || nt == typeof(double) ||
                       nt == typeof(decimal);
            }
            return false;
        }

        private string BuildFilterExpr(string columnName, string op, string rawValue)
        {
            bool isNum = ColumnIsNumeric(columnName);

            switch (op)
            {
                case "Equals":
                    return isNum
                        ? $"CONVERT([{columnName}], 'System.String') = '{EscapeLike(rawValue)}'"
                        : $"[{columnName}] = '{EscapeLike(rawValue)}'";

                case "Contains":
                case "Search":
                    return $"CONVERT([{columnName}], 'System.String') LIKE '%{EscapeLike(rawValue)}%'";

                case "StartsWith":
                    return $"CONVERT([{columnName}], 'System.String') LIKE '{EscapeLike(rawValue)}%'";

                case "EndsWith":
                    return $"CONVERT([{columnName}], 'System.String') LIKE '%{EscapeLike(rawValue)}'";

                case "GreaterThan":
                    return isNum
                        ? $"[{columnName}] > {rawValue}"
                        : $"CONVERT([{columnName}], 'System.String') > '{EscapeLike(rawValue)}'";

                case "LessThan":
                    return isNum
                        ? $"[{columnName}] < {rawValue}"
                        : $"CONVERT([{columnName}], 'System.String') < '{EscapeLike(rawValue)}'";

                case "Clear":
                    return string.Empty;

                default:
                    // fallback to string contains
                    return $"CONVERT([{columnName}], 'System.String') LIKE '%{EscapeLike(rawValue)}%'";
            }
        }

        private void OnHeaderCommand(string column, string section, string action, string? value)
        {
            EnsureBindingLayer();

            // ---- SORT ----
            if (section == "Sort")
            {
                if (_bs != null)
                {
                    _bs.Sort = action == "Ascending" ? $"[{column}] ASC" : $"[{column}] DESC";
                }
                else
                {
                    // fallback: grid sort
                    var col = Columns[column];
                    var dir = (action == "Ascending") ? ListSortDirection.Ascending : ListSortDirection.Descending;
                    try
                    {
                        Sort(col, dir);
                        col.HeaderCell.SortGlyphDirection =
                            (dir == ListSortDirection.Ascending) ? SortOrder.Ascending : SortOrder.Descending;
                    }
                    catch { /* ignore */ }
                }
                return;
            }

            // ---- FILTER / SEARCH ----
            if (section == "Filter" || section == "Search")
            {
                if (action == "Clear" || string.IsNullOrWhiteSpace(value))
                {
                    _columnFilters.Remove(column);
                }
                else
                {
                    string op = (section == "Search" && action == "Apply") ? "Search" : action;
                    string expr = BuildFilterExpr(column, op, value ?? string.Empty);
                    _columnFilters[column] = expr;
                }

                ApplyCombinedFilter();
                return;
            }

            // ---- COLUMNS ----
            if (section == "Columns")
            {
                // ---- FILTER / SEARCH ----
                if (section == "Filter" || section == "Search")
                {
                    // NEW: global clear (all columns)
                    if (action == "ClearAll")
                    {
                        _columnFilters.Clear();
                        ApplyCombinedFilter();         // also refreshes badges
                        return;
                    }

                    // NEW: clear only this column
                    if (action == "ClearThis")
                    {
                        _columnFilters.Remove(column);
                        ApplyCombinedFilter();         // also refreshes badges
                        return;
                    }

                    // existing behavior
                    if (action == "Clear" || string.IsNullOrWhiteSpace(value))
                    {
                        _columnFilters.Remove(column);
                    }
                    else
                    {
                        string op = (section == "Search" && action == "Apply") ? "Search" : action;
                        string expr = BuildFilterExpr(column, op, value ?? string.Empty);
                        _columnFilters[column] = expr;
                    }

                    ApplyCombinedFilter();
                    return;
                }

                if (action == "ShowAll")
                {
                    foreach (DataGridViewColumn c in Columns) c.Visible = true;
                }
                else if (action == "HideAll")
                {
                    foreach (DataGridViewColumn c in Columns)
                    {
                        if (_primaryKeyColumns.Contains(c.Name)) c.Visible = true;
                        else c.Visible = false;
                    }
                }
                else if (action == "Show" && !string.IsNullOrEmpty(value))
                {
                    if (Columns.Contains(value)) Columns[value].Visible = true;
                }
                else if (action == "Hide" && !string.IsNullOrEmpty(value))
                {
                    if (Columns.Contains(value) && !_primaryKeyColumns.Contains(value))
                        Columns[value].Visible = false;
                }
                return;
            }

            // ---- VIEW (no data change needed) ----
            if (section == "View")
            {
                // already handled where invoked (autosize); nothing else required here
                return;
            }
        }

        private void RefreshHeaderFilterBadges()
        {
            foreach (DataGridViewColumn c in Columns)
            {
                if (!_originalHeaderText.TryGetValue(c.Name, out var baseText) || baseText == null)
                {
                    baseText = c.HeaderText;
                    _originalHeaderText[c.Name] = baseText;
                }

                bool hasUser = _columnFilters.ContainsKey(c.Name);
                bool hasFixed = _fixedFilters.ContainsKey(c.Name);

                string suffix = hasFixed && hasUser ? " ⧉🔒"
                             : hasFixed ? " 🔒"
                             : hasUser ? " ⧉"
                             : string.Empty;

                c.HeaderCell.Value = baseText + suffix;
                c.HeaderCell.ToolTipText =
                    hasFixed && hasUser ? "Fixed & user filter applied"
                  : hasFixed ? "Fixed filter applied"
                  : hasUser ? "Filter applied"
                  : string.Empty;
            }

            Invalidate();
        }


        #endregion

        #endregion

        #region ==== Header menu builder (Search, Sort, Filter, Columns, View) ====
        private void BuildHeaderMenu(DataGridViewColumn col)
        {
            _headerMenu.SuspendLayout();
            _headerMenu.Items.Clear();
            _headerMenu.ShowImageMargin = false;

            ToolStripItem Section(string title)
            {
                var it = new ToolStripMenuItem(title) { Enabled = false };
                it.Font = new Font(it.Font, FontStyle.Bold);
                return it;
            }

            // ---- Search ----
            _headerMenu.Items.Add(Section("Search"));
            _headerMenu.Items.Add(CreateSearchHost(col.Name));
            _headerMenu.Items.Add(new ToolStripSeparator());

            // ---- Sort ----
            _headerMenu.Items.Add(Section("Sort"));
            _headerMenu.Items.Add(new ToolStripMenuItem("Ascending", null, (_, __) =>
                HeaderCommand?.Invoke(col.Name, "Sort", "Ascending", null)));
            _headerMenu.Items.Add(new ToolStripMenuItem("Descending", null, (_, __) =>
                HeaderCommand?.Invoke(col.Name, "Sort", "Descending", null)));
            _headerMenu.Items.Add(new ToolStripSeparator());

            // ---- Filter ----
            _headerMenu.Items.Add(Section("Filter"));

            var equals = new ToolStripMenuItem("Equals…");
            equals.Click += (_, __) => PromptAndFire(col.Name, "Filter", "Equals");

            var contains = new ToolStripMenuItem("Contains…");
            contains.Click += (_, __) => PromptAndFire(col.Name, "Filter", "Contains");

            _headerMenu.Items.Add(equals);
            _headerMenu.Items.Add(contains);

            // --- Clear Filters (new global clear option) ---
            var clearFilterMenu = new ToolStripMenuItem("Clear Filter");
            clearFilterMenu.DropDownItems.Add("This Column", null, (_, __) =>
            {
                HeaderCommand?.Invoke(col.Name, "Filter", "ClearThis", null);
            });
            clearFilterMenu.DropDownItems.Add("All Columns", null, (_, __) =>
            {
                HeaderCommand?.Invoke(col.Name, "Filter", "ClearAll", null);
            });
            _headerMenu.Items.Add(clearFilterMenu);

            var operators = new ToolStripMenuItem("By Operator");
            operators.DropDownItems.Add("Starts With…", null, (_, __) => PromptAndFire(col.Name, "Filter", "StartsWith"));
            operators.DropDownItems.Add("Ends With…", null, (_, __) => PromptAndFire(col.Name, "Filter", "EndsWith"));
            operators.DropDownItems.Add("Greater Than…", null, (_, __) => PromptAndFire(col.Name, "Filter", "GreaterThan"));
            operators.DropDownItems.Add("Less Than…", null, (_, __) => PromptAndFire(col.Name, "Filter", "LessThan"));
            _headerMenu.Items.Add(operators);

            var distinct = new ToolStripMenuItem("Distinct Values");
            distinct.DropDownItems.Add("— All —", null, (_, __) => HeaderCommand?.Invoke(col.Name, "Filter", "Clear", null));
            // Note: you can populate real distinct values externally using HeaderCommand and your data source
            _headerMenu.Items.Add(distinct);

            _headerMenu.Items.Add(new ToolStripSeparator());

            // ---------- Section: Columns (Show/Hide) ----------
            _headerMenu.Items.Add(Section("Columns"));

            // Make it a submenu like "Distinct Values"
            var columnsMenu = new ToolStripMenuItem("Columns");

            // --- Show All ---
            var showAll = new ToolStripMenuItem("— Show All —", image: null, onClick: (_, __) =>
            {
                foreach (DataGridViewColumn c in this.Columns)
                {
                    c.Visible = true;
                    if(_dynClass != null)
                    {
                        _dynClass.SetShowInDataGrid(c.Name,true);
                    }
                }

                HeaderCommand?.Invoke(col.Name, "Columns", "ShowAll", null);
            });
            columnsMenu.DropDownItems.Add(showAll);

            // --- Hide All (non-PK only) ---
            var hideAll = new ToolStripMenuItem("— Hide All —", image: null, onClick: (_, __) =>
            {
                foreach (DataGridViewColumn c in this.Columns)
                {
                    // Keep primary key columns always visible
                    if (_primaryKeyColumns.Contains(c.Name))
                    {
                        c.Visible = true;
                        _dynClass.SetShowInDataGrid(c.Name, true);
                    }
                    else
                    {
                        c.Visible = false;
                        _dynClass.SetShowInDataGrid(c.Name, false);
                    }
                }
                HeaderCommand?.Invoke(col.Name, "Columns", "HideAll", null);
            });
            columnsMenu.DropDownItems.Add(hideAll);

            columnsMenu.DropDownItems.Add(new ToolStripSeparator());

            foreach (DataGridViewColumn c in this.Columns
                     .Cast<DataGridViewColumn>()
                     .OrderByDescending(cc => _primaryKeyColumns.Contains(cc.Name))
                     .ThenBy(cc => cc.HeaderText))
            {
                bool isPk = _primaryKeyColumns.Contains(c.Name);
                string label = isPk ? $"{c.HeaderText} (PK)" : c.HeaderText;

                var chk = new ToolStripMenuItem(label)
                {
                    Checked = c.Visible,
                    CheckOnClick = !isPk,
                    Enabled = !isPk,
                    Tag = c
                };

                chk.Click += (_, __) =>
                {
                    if (chk.Tag is not DataGridViewColumn target)
                        return;

                    if (_primaryKeyColumns.Contains(target.Name))
                    {
                        target.Visible = true;
                        chk.Checked = true;
                        return;
                    }

                    bool newState = chk.Checked;

                    target.Visible = newState;

                    _dynClass?.SetShowInDataGrid(target.Name, newState);

                    // 3️⃣ Raise header event
                    HeaderCommand?.Invoke(
                        target.Name,
                        "Columns",
                        newState ? "Show" : "Hide",
                        target.Name
                    );
                };

                columnsMenu.DropDownItems.Add(chk);
            }

            _headerMenu.Items.Add(columnsMenu);


            _headerMenu.Items.Add(new ToolStripSeparator());

            // ---- View ----
            _headerMenu.Items.Add(Section("View"));
            _headerMenu.Items.Add(new ToolStripMenuItem("Auto-size This Column", null, (_, __) =>
            {
                AutoResizeColumn(col.Index, (DataGridViewAutoSizeColumnMode)AutoSizeColumnsMode);
                HeaderCommand?.Invoke(col.Name, "View", "AutosizeColumn", null);
            }));
            _headerMenu.Items.Add(new ToolStripMenuItem("Auto-size All Columns", null, (_, __) =>
            {
                AutoResizeColumns(AutoSizeColumnsMode);
                HeaderCommand?.Invoke(col.Name, "View", "AutosizeAll", null);
            }));

            _headerMenu.Items.Add(new ToolStripSeparator());
            _headerMenu.Items.Add(new ToolStripMenuItem("Close", null, (_, __) => _headerMenu.Close()));

            _headerMenu.ResumeLayout();
        }

        private void PromptAndFire(string column, string section, string action)
        {
            using var f = new Form
            {
                StartPosition = FormStartPosition.Manual,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MinimizeBox = false,
                MaximizeBox = false,
                ShowInTaskbar = false,
                Width = 360,
                Height = 140,
                Text = $"{section}: {action}"
            };

            var tb = new TextBox { Left = 12, Top = 12, Width = 320 };
            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Left = 252, Top = 48, Width = 80 };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Left = 166, Top = 48, Width = 80 };

            f.Controls.Add(tb);
            f.Controls.Add(ok);
            f.Controls.Add(cancel);

            f.AcceptButton = ok;
            f.CancelButton = cancel;

            // Try to show near mouse pointer
            var pt = Control.MousePosition;
            f.Location = new Point(pt.X - f.Width / 2, pt.Y - f.Height / 2);

            if (f.ShowDialog(FindForm()) == DialogResult.OK)
            {
                HeaderCommand?.Invoke(column, section, action, tb.Text);
            }
        }

        private ToolStripControlHost CreateSearchHost(string columnName)
        {
            var panel = new Panel
            {
                BackColor = SystemColors.Window,
                Margin = new Padding(6),
                Padding = new Padding(6),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };

            var lbl = new Label
            {
                AutoSize = true,
                Text = "Search",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 4)
            };

            var tb = new TextBox
            {
                Width = 220,
                Margin = new Padding(0),
                PlaceholderText = "type and press Enter"
            };

            tb.KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    HeaderCommand?.Invoke(columnName, "Search", "Apply", tb.Text);
                    _headerMenu.Close();
                    e.SuppressKeyPress = true;
                }
            };

            // Remember last search box so we can focus it when menu opens
            _searchTextBox = tb;

            panel.Controls.Add(lbl);
            panel.Controls.Add(tb);
            tb.Top = lbl.Bottom + 4;

            return new ToolStripControlHost(panel) { AutoSize = true, Margin = new Padding(2) };
        }

        #endregion

        #region ==== Filtering API ====

        private static string EscapeRowFilterLike(string input)
        {
            if (input == null) return string.Empty;
            // DataView.RowFilter uses SQL-like syntax with %, _, and [ as specials.
            return input
                .Replace("[", "[[]")
                .Replace("%", "[%]")
                .Replace("_", "[_]")
                .Replace("'", "''"); // quotes still need escaping
        }

        public void SetFilterEquals(string column, string value, bool asFixed = true)
        {
            EnsureBindingLayer();
            if (string.IsNullOrWhiteSpace(column)) return;

            var expr = BuildFilterExpr(column, "Equals", value ?? string.Empty);
            if (asFixed)
                _fixedFilters[column] = expr;
            else
                _columnFilters[column] = expr;

            ApplyCombinedFilter();
        }


        public void SetFilterEquals(string column, IEnumerable<string> values, bool asFixed = true)
        {
            EnsureBindingLayer();
            if (string.IsNullOrWhiteSpace(column) || values == null) return;

            var terms = values
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (terms.Length == 0)
            {
                if (asFixed) _fixedFilters.Remove(column);
                else _columnFilters.Remove(column);
                ApplyCombinedFilter();
                return;
            }

            var parts = new List<string>(terms.Length);
            foreach (var t in terms)
            {
                var expr = BuildFilterExpr(column, "Equals", t);
                if (!string.IsNullOrWhiteSpace(expr)) parts.Add(expr);
            }

            if (parts.Count == 0)
            {
                if (asFixed) _fixedFilters.Remove(column);
                else _columnFilters.Remove(column);
                ApplyCombinedFilter();
                return;
            }

            var combined = (parts.Count == 1) ? parts[0] : "(" + string.Join(" OR ", parts) + ")";
            if (asFixed)
                _fixedFilters[column] = combined;
            else
                _columnFilters[column] = combined;

            ApplyCombinedFilter();
        }

        public void SetFilterContains(string column, string value, bool asFixed = true)
        {
            EnsureBindingLayer();
            if (string.IsNullOrWhiteSpace(column)) return;

            var expr = $"CONVERT([{column}], 'System.String') LIKE '%{EscapeRowFilterLike(value ?? string.Empty)}%'";
            if (asFixed)
                _fixedFilters[column] = expr;
            else
                _columnFilters[column] = expr;

            ApplyCombinedFilter();
        }


        public void ClearFilter(string? column = null, bool includeFixed = false)
        {
            EnsureBindingLayer();

            if (string.IsNullOrWhiteSpace(column))
            {
                _columnFilters.Clear();
                if (includeFixed) _fixedFilters.Clear();
            }
            else
            {
                _columnFilters.Remove(column);
                if (includeFixed) _fixedFilters.Remove(column);
            }

            ApplyCombinedFilter();
        }

        public string CurrentFilter => _view?.RowFilter ?? string.Empty;

        public void ReapplyFilters()
        {
            EnsureBindingLayer();
            ApplyCombinedFilter();
        }

        protected override void OnDataSourceChanged(EventArgs e)
        {
            base.OnDataSourceChanged(e);

            // Always ensure we’re bound to a DataView/BindingSource for filtering
            EnsureBindingLayer();

            // Re-apply fixed + user filters on the new DataView
            try
            {
                ApplyCombinedFilter();
            }
            catch
            {
                // If schema changed and some columns disappeared, drop bad filters
                if (_view?.Table != null)
                {
                    var cols = _view.Table.Columns;
                    foreach (var kv in _fixedFilters.Keys.ToList())
                        if (!cols.Contains(kv)) _fixedFilters.Remove(kv);

                    foreach (var kv in _columnFilters.Keys.ToList())
                        if (!cols.Contains(kv)) _columnFilters.Remove(kv);
                }
                ApplyCombinedFilter();
            }
        }


        public void RebindData(DataTable data, bool keepFilters = true)
        {
            // Snapshot filters
            Dictionary<string, string>? savedFixed = null, savedUser = null;
            if (keepFilters)
            {
                savedFixed = new Dictionary<string, string>(_fixedFilters, StringComparer.OrdinalIgnoreCase);
                savedUser = new Dictionary<string, string>(_columnFilters, StringComparer.OrdinalIgnoreCase);
            }

            // Hard rebind
            _view = null; _bs = null;
            DataSource = data;         // OnDataSourceChanged will EnsureBindingLayer + ApplyCombinedFilter

            // Restore
            if (keepFilters && savedFixed != null && savedUser != null)
            {
                _fixedFilters.Clear();
                foreach (var kv in savedFixed) _fixedFilters[kv.Key] = kv.Value;

                _columnFilters.Clear();
                foreach (var kv in savedUser) _columnFilters[kv.Key] = kv.Value;

                ReapplyFilters();
            }
        }

        public DataTable LoadDynamicClassData(
            int? chunkSize = null,
            string? whereSql = null,
            IDictionary<string, object?>? parameters = null,
            string? orderBy = null,
            bool keepFilters = true)
        {
            _dynamicLoadCts?.Cancel();
            _dynamicLoadCts?.Dispose();
            _dynamicLoadCts = null;
            IsDynamicSelectLoading = false;

            var effectiveChunkSize = chunkSize ?? DynamicSelectChunkSize;
            if (effectiveChunkSize < 1)
                throw new ArgumentOutOfRangeException(nameof(chunkSize), "chunkSize must be greater than or equal to 1.");

            DataTable? firstChunk = DynamicSelectLoadInChunks
                ? _dynClass.Select(whereSql, parameters, orderBy: orderBy, chunkSize: effectiveChunkSize)
                : _dynClass.Select(whereSql, parameters, orderBy: orderBy);

            firstChunk ??= new DataTable(_dynClass.Table);
            RebindData(firstChunk, keepFilters);

            if (!DynamicSelectLoadInChunks || firstChunk.Rows.Count < effectiveChunkSize)
                return firstChunk;

            _dynamicLoadCts = new CancellationTokenSource();
            IsDynamicSelectLoading = true;

            _ = LoadRemainingDynamicClassChunksAsync(
                whereSql,
                parameters,
                orderBy,
                effectiveChunkSize,
                _dynamicLoadCts.Token);

            return firstChunk;
        }

        private async Task LoadRemainingDynamicClassChunksAsync(
            string? whereSql,
            IDictionary<string, object?>? parameters,
            string? orderBy,
            int chunkSize,
            CancellationToken ct)
        {
            try
            {
                await foreach (var chunk in _dynClass.SelectChunksAsync(
                    whereSql: whereSql,
                    parameters: parameters,
                    orderBy: orderBy,
                    chunkSize: chunkSize,
                    skipFirstChunk: true,
                    ct: ct).ConfigureAwait(false))
                {
                    ct.ThrowIfCancellationRequested();
                    await AppendChunkToBoundTableAsync(chunk, ct).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when refresh/dispose starts a new load.
            }
            catch (Exception ex)
            {
                DynamicSelectChunkLoadFailed?.Invoke(ex);
            }
            finally
            {
                IsDynamicSelectLoading = false;
            }
        }

        private Task AppendChunkToBoundTableAsync(DataTable chunk, CancellationToken ct)
        {
            if (ct.IsCancellationRequested || IsDisposed || chunk.Rows.Count == 0)
                return Task.CompletedTask;

            var tcs = new TaskCompletionSource<object?>();

            void AppendOnUi()
            {
                try
                {
                    if (ct.IsCancellationRequested || IsDisposed)
                    {
                        tcs.TrySetResult(null);
                        return;
                    }

                    var target = _view?.Table;
                    if (target == null)
                    {
                        tcs.TrySetResult(null);
                        return;
                    }

                    target.BeginLoadData();
                    try
                    {
                        foreach (DataRow row in chunk.Rows)
                            target.ImportRow(row);
                    }
                    finally
                    {
                        target.EndLoadData();
                    }

                    ReapplyFilters();
                    DynamicSelectChunkLoaded?.Invoke(target.Rows.Count, chunk.Rows.Count);
                    tcs.TrySetResult(null);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            }

            if (InvokeRequired || !IsHandleCreated)
            {
                if (IsHandleCreated)
                    BeginInvoke((Action)AppendOnUi);
                else
                {
                    void Handler(object? sender, EventArgs e)
                    {
                        HandleCreated -= Handler;
                        if (!IsDisposed)
                            BeginInvoke((Action)AppendOnUi);
                        else
                            tcs.TrySetResult(null);
                    }

                    HandleCreated += Handler;
                }
            }
            else
            {
                AppendOnUi();
            }

            return tcs.Task;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _dynamicLoadCts?.Cancel();
                _dynamicLoadCts?.Dispose();
                _dynamicLoadCts = null;
            }

            base.Dispose(disposing);
        }

        private void ApplyColumnVisibilityFromMetadata()
        {
            if (_dynClass == null) return;

            var cols = _dynClass.GetColumns();
            if (cols == null) return;

            foreach (var ci in cols)
            {
                if (!Columns.Contains(ci.Name))
                    continue;

                var gridCol = Columns[ci.Name];

                if (_primaryKeyColumns.Contains(ci.Name))
                {
                    gridCol.Visible = true;
                    continue;
                }

                if (ci.DatagridShow.HasValue)
                {
                    gridCol.Visible = ci.DatagridShow.Value;
                }
                else
                {
                    gridCol.Visible = true;
                }
            }
        }

        private void ApplyDisplayNameAndUnitFromMetadata()
        {
            if (_dynClass == null) return;

            var cols = _dynClass.GetColumns();
            if (cols == null) return;

            foreach (var ci in cols)
            {
                if (!Columns.Contains(ci.Name))
                    continue;

                var gridCol = Columns[ci.Name];

                string headerText;

                if (!string.IsNullOrWhiteSpace(ci.DisplayName))
                {
                    headerText = ci.DisplayName;

                    //if (!string.IsNullOrWhiteSpace(ci.DefaultUnit))
                    //{
                    //    headerText += $" ({ci.DefaultUnit})";
                    //}
                }
                else
                {
                    headerText = ci.Name;
                }

                gridCol.HeaderText = headerText;

                // Keep original header text tracking updated
                _originalHeaderText[ci.Name] = headerText;
            }
        }


        #endregion

        #region ==== Foreign Key Support ====
        private void ExtractForeignKeyColumns(DynamicClass dynamicClass)
        {
            var columns = dynamicClass.GetColumns() ?? new List<DynamicClass.ColumnInfo>();

            foreach (var column in columns)
            {
                if (!column.IsForeignKey) continue;

                if (!ForeignKeyColumns.ContainsKey(column.Name))
                {
                    ForeignKeyColumns[column.Name] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }
                ForeignKeyColumns[column.Name][column.ReferencedTable ?? string.Empty] = column.ReferencedColumn ?? string.Empty;
            }
        }

        private void DataGridView_CellClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return; // ignore headers
            if (!IsForeignKey(e.ColumnIndex)) return;

            string columnName = Columns[e.ColumnIndex].Name;
            var fkDetails = ForeignKeyColumns[columnName];

            var cellValue = Rows[e.RowIndex].Cells[e.ColumnIndex].Value;
            foreach (var kv in fkDetails)
            {
                OnForeignKeyCellClicked(kv.Key, kv.Value, cellValue!);
            }
        }

        protected virtual void OnForeignKeyCellClicked(string fkTable, string fkColumn, object cellValue)
            => ForeignKeyCellClicked?.Invoke(fkTable, fkColumn, cellValue);

        private void ExtendedDataGridView_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (IsForeignKey(e.ColumnIndex))
            {
                e.CellStyle.BackColor = Color.LightYellow;
                e.CellStyle.Font = new Font("Arial", 9, FontStyle.Bold);
            }
        }

        private void ExtendedDataGridView_CellMouseEnter(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) { Cursor = Cursors.Default; return; }
            Cursor = IsForeignKey(e.ColumnIndex) ? Cursors.Hand : Cursors.Default;
        }

        private bool IsForeignKey(int columnIndex)
        {
            if (columnIndex < 0 || columnIndex >= Columns.Count) return false;
            string columnName = Columns[columnIndex].Name;
            if (ForeignKeyColumns == null)
            {
                return false;
            }
            return ForeignKeyColumns.ContainsKey(columnName);
        }

        private bool IsForeignKey(string columnName) => ForeignKeyColumns.ContainsKey(columnName);

        public void DisplayForeignKeyInfo(string columnName)
        {
            if (!IsForeignKey(columnName))
            {
                Console.WriteLine($"No foreign key mapping found for column: {columnName}");
                return;
            }

            var details = ForeignKeyColumns[columnName];
            foreach (var fk in details)
            {
                Console.WriteLine($"Column: {columnName}, Foreign Key Table: {fk.Key}, Foreign Key Column: {fk.Value}");
            }
        }
        #endregion

        #region ==== View, Edit, Delete ====

        private void BuildRowMenu()
        {
            _rowMenu.SuspendLayout();
            _rowMenu.Items.Clear();
            _rowMenu.ShowImageMargin = false;

            ToolStripMenuItem Make(string text, RowCommand cmd)
            {
                var it = new ToolStripMenuItem(text) { Tag = cmd };
                it.Click += (_, __) =>
                {
                    if (_rowMenuTargetIndex >= 0 && _rowMenuTargetIndex < Rows.Count)
                    {
                        var row = Rows[_rowMenuTargetIndex];
                        RowCommandInvoked?.Invoke(cmd, row);
                        RowCommandDataInvoked?.Invoke(cmd, GetRowData(row));
                    }
                };
                return it;
            }

            _rowMenu.Items.Add(Make("View", RowCommand.View));
            _rowMenu.Items.Add(Make("Edit", RowCommand.Edit));
            _rowMenu.Items.Add(new ToolStripSeparator());
            _rowMenu.Items.Add(Make("Delete", RowCommand.Delete));

            _rowMenu.ResumeLayout();
        }

        private static IDictionary<string, object?> GetRowData(DataGridViewRow row)
        {
            var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (DataGridViewCell c in row.Cells)
            {
                var name = c.OwningColumn?.Name ?? "";
                if (!string.IsNullOrWhiteSpace(name))
                    dict[name] = c.Value is DBNull ? null : c.Value;
            }
            return dict;
        }

        private void ExtendedDataGridView_CellMouseDown_RowMenu(object? sender, DataGridViewCellMouseEventArgs e)
        {
            // Right-click on a data row (not header)
            if (e.Button == MouseButtons.Right && e.RowIndex >= 0)
            {
                // Select the row under the mouse (single select UX)
                ClearSelection();
                if (e.RowIndex < Rows.Count)
                {
                    Rows[e.RowIndex].Selected = true;
                    CurrentCell = this[e.ColumnIndex >= 0 ? e.ColumnIndex : 0, e.RowIndex];
                }

                _rowMenuTargetIndex = e.RowIndex;

                // If you prefer automatic positioning via CellContextMenuStripNeeded, you can skip Show() here.
                // But for immediate show under cursor:
                var pt = PointToScreen(new Point(e.Location.X + GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, true).Left,
                                                 e.Location.Y + GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, true).Top));
                _rowMenu.Show(pt);
            }
        }

        private void ExtendedDataGridView_CellContextMenuStripNeeded(object? sender, DataGridViewCellContextMenuStripNeededEventArgs e)
        {
            // Fallback path so OS can show it if you don’t call Show() manually.
            if (e.RowIndex >= 0)
            {
                _rowMenuTargetIndex = e.RowIndex;
                e.ContextMenuStrip = _rowMenu;
            }
        }


        #endregion
    }
}
