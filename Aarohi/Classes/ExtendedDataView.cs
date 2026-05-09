using Aarohi.Classes.Healper;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Aarohi.Classes
{
    public partial class ExtendedDataView : UserControl
    {
        #region ===== Fields =====
        private readonly Dictionary<string, TabPage> Pages = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ExtendedDataGridView> Datas = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DynamicClass> Tables = new(StringComparer.OrdinalIgnoreCase);

        // Remember whether a given table should use AutoJoin (used by RefreshData)
        private readonly Dictionary<string, bool> _autoJoinByTable = new(StringComparer.OrdinalIgnoreCase);

        // Filters
        private readonly Dictionary<string, DataInput> _filters = new(StringComparer.OrdinalIgnoreCase);
        private readonly DynamicClass _FilterClass = new("dbo", "Column_Permissions"); // currently unused, but kept
        private string[] _filterColumns = Array.Empty<string>();
        private string _filterTitle = "";

        // Mapped view: option -> list of (DynamicClass, useAutoJoin)
        private Dictionary<string, List<(DynamicClass useClass, bool useAutoJoin)>> _mapped_classes
            = new(StringComparer.OrdinalIgnoreCase);

        // Reusable constants
        private const string SelectToken = "--Select--";
        #endregion

        #region ===== Initialization =====
        private void Configure()
        {
            InitializeComponent();
            if (tabControl != null)
            {
                tabControl.SelectedIndexChanged += (_, __) =>
                {
                    var tab = tabControl.SelectedTab;
                    var grid = tab?.Controls.OfType<ExtendedDataGridView>().FirstOrDefault();
                    UpdateLocationLabel(grid);
                };
            }
        }


        public ExtendedDataView(DynamicClass[] classes, bool showPath = true)
        {
            Configure();
            run_inside(classes);
            LocationSpecifier.Visible = showPath;
        }

        public ExtendedDataView(
            string filter_Title,
            string[] filter_column,
            Dictionary<string, List<(DynamicClass, bool)>> mapped_classes)
        {
            Configure();

            _filterTitle = filter_Title ?? string.Empty;
            _filterColumns = (filter_column ?? Array.Empty<string>())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .ToArray();

            _mapped_classes = mapped_classes ?? new(StringComparer.OrdinalIgnoreCase);

            // Build filter dropdown values from mapping keys
            var values = new BindingList<string> { SelectToken };
            foreach (var key in _mapped_classes.Keys)
                values.Add(key);

            var di = new DataInput(_filterTitle, Array.Empty<string>());
            di.BindOptions(values, select: SelectToken);
            di.ValueChanged += filter_Changed_value;

            if (FillterPanel != null)
                FillterPanel.Controls.Add(di);

            _filters[_filterTitle] = di;
            EnsureFilterPanelVisible();
        }
        #endregion

        #region ===== Mapped-Filter Logic =====
        private void filter_Changed_value(object? sender, EventArgs e)
        {
            if (!_filters.TryGetValue(_filterTitle, out var di) || di == null)
                return;

            string? option = di.Value as string;
            option = string.IsNullOrWhiteSpace(option) ? null : option.Trim();

            if (tabControl != null)
                tabControl.TabPages.Clear();
            ClearState();

            if (string.IsNullOrEmpty(option) || string.Equals(option, SelectToken, StringComparison.OrdinalIgnoreCase))
                return;

            if (!_mapped_classes.TryGetValue(option, out var items) || items == null || items.Count == 0)
                return;

            run_inside(items.ToArray());
        }
        #endregion

        #region ===== Normal (Direct) Mode =====

        public void run_inside(DynamicClass[] classes)
        {
            if (classes == null || classes.Length == 0 || tabControl == null)
                return;

            string? selectedOption = null;
            if (_filters.TryGetValue(_filterTitle, out var di) && di != null)
            {
                var v = di.Value as string;
                selectedOption = string.IsNullOrWhiteSpace(v) || string.Equals(v, SelectToken, StringComparison.OrdinalIgnoreCase)
                                 ? null
                                 : v.Trim();
            }

            foreach (var c in classes)
            {
                if (c == null || string.IsNullOrWhiteSpace(c.Table))
                    continue;

                try
                {
                    Tables[c.Table] = c;
                    _autoJoinByTable[c.Table] = false;

                    var title = GetTableTitle(c);
                    var tabPage = NewOrReplaceTab(c.Table, title);

                    var grid = new ExtendedDataGridView(c)
                    {
                        CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                        BorderStyle = BorderStyle.FixedSingle,
                        Dock = DockStyle.Fill
                    };

                    Datas[c.Table] = grid;
                    tabPage.Controls.Add(grid);

                    WireGridLocationEvents(grid);

                    if (!string.IsNullOrWhiteSpace(selectedOption) &&
                        !string.Equals(selectedOption, SelectToken, StringComparison.OrdinalIgnoreCase))
                    {
                        var col = FirstExistingFilterColumnOnGrid(grid);
                        if (!string.IsNullOrWhiteSpace(col))
                        {
                            grid.SetFilterEquals(col!, selectedOption!);
                        }
                        else
                        {
                            var info = c.GetColumns() ?? new List<DynamicClass.ColumnInfo>();
                            var filterFkVals = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

                            foreach (var ci in info)
                            {
                                if (ci.IsForeignKey &&
                                    !string.IsNullOrWhiteSpace(ci.ReferencedTable) &&
                                    !string.IsNullOrWhiteSpace(ci.ReferencedColumn))
                                {
                                    // Pull the CURRENT (filtered) values shown in that referenced grid
                                    var vals = GetFilteredColumnValues(ci.ReferencedTable!, ci.ReferencedColumn!);
                                    if (vals.Length > 0)
                                        filterFkVals[ci.Name] = vals;
                                }
                            }

                            foreach (var kvp in filterFkVals)
                            {
                                var fkCol = kvp.Key;
                                var allowed = kvp.Value;
                                grid.SetFilterEquals(fkCol, allowed);
                            }

                            grid.DataBindingComplete += (_, __) =>
                            {
                                var col2 = FirstExistingFilterColumnOnGrid(grid);
                                if (!string.IsNullOrWhiteSpace(col2))
                                    grid.SetFilterEquals(col2!, selectedOption!);
                            };
                        }
                    }

                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error creating tab for '{c.Table}': {ex.Message}");
                }
            }
        }

        public void run_inside((DynamicClass dyn, bool useAutoJoin)[] classes)
        {
            if (classes == null || classes.Length == 0 || tabControl == null)
                return;

            // current selected option (filter value)
            string? selectedOption = null;
            if (_filters.TryGetValue(_filterTitle, out var di) && di != null)
            {
                var v = di.Value as string;
                selectedOption = string.IsNullOrWhiteSpace(v) || string.Equals(v, SelectToken, StringComparison.OrdinalIgnoreCase)
                                 ? null
                                 : v.Trim();
            }

            List<DynamicClass> normal_classes = new();

            foreach (var (c, useAutoJoin) in classes)
            {
                if (c == null || string.IsNullOrWhiteSpace(c.Table))
                    continue;

                try
                {
                    Tables[c.Table] = c;
                    _autoJoinByTable[c.Table] = useAutoJoin;

                    if (!useAutoJoin)
                    {
                        normal_classes.Add(c);
                        continue;
                    }

                    DataTable? data = c.AutoSelectWithJoins(includeRefKeyColumns: true);
                    if (data == null) data = new DataTable { TableName = c.Table };

                    var title = GetTableTitle(c);
                    var tabPage = NewOrReplaceTab(c.Table, title);

                    var grid = new ExtendedDataGridView(data)
                    {
                        CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                        BorderStyle = BorderStyle.FixedSingle,
                        Dock = DockStyle.Fill
                    };

                    Datas[c.Table] = grid;
                    tabPage.Controls.Add(grid);

                    WireGridLocationEvents(grid);

                    if (!string.IsNullOrWhiteSpace(selectedOption))
                    {
                        var col = FirstExistingFilterColumnOnGrid(grid);
                        if (!string.IsNullOrWhiteSpace(col))
                        {
                            grid.SetFilterEquals(col!, selectedOption!);
                        }
                        else
                        {
                            grid.DataBindingComplete += (_, __) =>
                            {
                                var col2 = FirstExistingFilterColumnOnGrid(grid);
                                if (!string.IsNullOrWhiteSpace(col2))
                                {
                                    grid.SetFilterEquals(col2!, selectedOption!);
                                }
                            };
                        }
                    }

                    foreach (var key in _filters.Keys.ToList())
                    {
                        if (data.Columns.Contains(key))
                            _filters[key].UpdateOptions(SqlHealper.GetBindingListFromTable(data, key));
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error creating tab for '{c.Table}': {ex.Message}");
                }
            }

            if (normal_classes.Count > 0)
            {
                run_inside(normal_classes.ToArray());
            }
        }

        #endregion

        #region ===== Events & Handlers =====
        private void HandleForeignKeyCellClicked(string fkTable, string fkColumn, object cellValue)
        {
            if (string.IsNullOrWhiteSpace(fkTable) || string.IsNullOrWhiteSpace(fkColumn))
                return;

            if (!Pages.ContainsKey(fkTable) || !Datas.ContainsKey(fkTable))
                return;

            var tabPage = Pages[fkTable];
            if (tabControl != null)
                tabControl.SelectedTab = tabPage;

            var grid = Datas[fkTable];
            SelectRowInDataGrid(grid, fkColumn, cellValue);
            UpdateLocationLabel(grid);
        }

        private void DataGrid_CellContentClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            if (sender is ExtendedDataGridView grid)
            {
                foreach (DataGridViewRow row in grid.Rows)
                    row.Selected = false;

                if (e.RowIndex < grid.Rows.Count)
                    grid.Rows[e.RowIndex].Selected = true;
            }
        }

        private void ExtendedDataView_Load(object sender, EventArgs e)
        {
            // Reserved for future use
        }

        public string[] GetFilteredColumnValues(string tableName, string columnName)
        {
            try
            {
                string? selectedOption = null;
                if (_filters.TryGetValue(_filterTitle, out var di) && di != null)
                {
                    var v = di.Value as string;
                    selectedOption = string.IsNullOrWhiteSpace(v) || string.Equals(v, SelectToken, StringComparison.OrdinalIgnoreCase)
                                     ? null
                                     : v.Trim();
                }

                using var dc = new DynamicClass("dbo", tableName);

                string? whereSql = null;
                Dictionary<string, object?>? pars = null;

                if (!string.IsNullOrWhiteSpace(selectedOption))
                {
                    // Try base table filter column first
                    var cols = dc.GetColumns() ?? new List<DynamicClass.ColumnInfo>();
                    var baseFilterCol = _filterColumns.FirstOrDefault(fc =>
                                        !string.IsNullOrWhiteSpace(fc) &&
                                        cols.Any(ci => ci.Name.Equals(fc, StringComparison.OrdinalIgnoreCase)));

                    if (!string.IsNullOrWhiteSpace(baseFilterCol))
                    {
                        whereSql = $"[{baseFilterCol}] = @opt";
                        pars = new Dictionary<string, object?> { ["@opt"] = selectedOption };
                    }
                    else
                    {
                        // No base column → build OR-of-EXISTS over FKs to referenced tables that HAVE a filter column
                        var existsClauses = new List<string>();

                        foreach (var fk in cols.Where(ci => ci.IsForeignKey
                                                            && !string.IsNullOrWhiteSpace(ci.ReferencedTable)
                                                            && !string.IsNullOrWhiteSpace(ci.ReferencedColumn)))
                        {
                            var refDyn = new DynamicClass(dc.Schema, fk.ReferencedTable!);
                            var refCols = refDyn.GetColumns() ?? new List<DynamicClass.ColumnInfo>();
                            var refFilterCol = _filterColumns.FirstOrDefault(fc =>
                                                 !string.IsNullOrWhiteSpace(fc) &&
                                                 refCols.Any(ci => ci.Name.Equals(fc, StringComparison.OrdinalIgnoreCase)));

                            if (string.IsNullOrWhiteSpace(refFilterCol))
                                continue;

                            // EXISTS (SELECT 1 FROM dbo.Ref r WHERE r.[<filter>] = @opt AND r.[<pk>] = dbo.Base.[<fk>])
                            string existsSql =
                                $"EXISTS (SELECT 1 FROM [{dc.Schema}].[{fk.ReferencedTable}] r " +
                                $"WHERE r.[{refFilterCol}] = @opt AND r.[{fk.ReferencedColumn}] = " +
                                $"[{dc.Schema}].[{tableName}].[{fk.Name}])";

                            existsClauses.Add(existsSql);
                        }

                        if (existsClauses.Count > 0)
                        {
                            whereSql = "(" + string.Join(" OR ", existsClauses) + ")";
                            pars = new Dictionary<string, object?> { ["@opt"] = selectedOption };
                        }
                    }
                }

                var data = (whereSql != null) ? dc.Select(whereSql, pars) : dc.Select();
                if (data == null || !data.Columns.Contains(columnName))
                    return Array.Empty<string>();

                return data.AsEnumerable()
                           .Select(r => r[columnName]?.ToString())
                           .Where(s => !string.IsNullOrWhiteSpace(s))
                           .Distinct(StringComparer.OrdinalIgnoreCase)
                           .ToArray();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        #endregion

        #region ===== Public API =====
        public void RefreshData(string? tableName = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(tableName))
                {
                    foreach (var key in Tables.Keys.ToList())
                    {
                        if (Datas.ContainsKey(key))
                            RefreshDataGrid(Datas[key], key);
                    }
                }
                else
                {
                    if (!Tables.ContainsKey(tableName))
                    {
                        MessageBox.Show($"Table '{tableName}' not found.");
                        return;
                    }

                    if (Datas.TryGetValue(tableName, out var grid))
                        RefreshDataGrid(grid, tableName);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error refreshing data: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves the currently active tab page's associated table name,
        /// the selected filter option (if any), and a placeholder for future extension.
        ///
        /// </summary>
        /// <remarks>
        /// **Return Tuple Order:**
        /// 1. <b>ActiveTableName</b> — The base name of the active TabPage, 
        ///    with the "_TabPage" suffix removed.  
        /// 2. <b>SelectedOption</b> — The selected filter value from `_filters`, 
        ///    or <c>null</c> if no valid option is selected.  
        /// 3. <b>ReservedField</b> — Currently always returns <see cref="string.Empty"/>,
        ///    reserved for future use (e.g., secondary filter or context data).
        ///
        /// This method helps in identifying which data grid or dataset is currently
        /// active in the UI along with its active filter context.
        /// </remarks>
        /// <returns>
        /// A tuple of three elements:
        /// <list type="number">
        /// <item><description><see cref="string"/> ActiveTableName</description></item>
        /// <item><description><see cref="string"/>? SelectedOption</description></item>
        /// <item><description><see cref="string"/> ReservedField (currently empty)</description></item>
        /// </list>
        /// </returns>
        public (string? ActiveTableName, string? SelectedOption, string? ReservedField) GetActivePageTableName()
        {
            var activeTab = tabControl?.SelectedTab;
            var activeTableName = activeTab?.Name.Replace("_TabPage", "", StringComparison.OrdinalIgnoreCase) ?? string.Empty;

            string? selectedOption = null;
            if (_filters.TryGetValue(_filterTitle, out var di) && di != null)
            {
                var v = di.Value as string;
                selectedOption = string.IsNullOrWhiteSpace(v) ||
                                 string.Equals(v, SelectToken, StringComparison.OrdinalIgnoreCase)
                                 ? null
                                 : v.Trim();
            }

            return (activeTableName, selectedOption, string.Empty);
        }


        public (string, Dictionary<string, object?>) GetActivePageAndSelectedRow()
        {
            var activeTab = tabControl?.SelectedTab;
            if (activeTab == null) return ("", new Dictionary<string, object?>());

            var activeTableName = activeTab.Name.Replace("_TabPage", "", StringComparison.OrdinalIgnoreCase);
            if (!Datas.ContainsKey(activeTableName)) return (activeTableName, new Dictionary<string, object?>());

            var grid = Datas[activeTableName];
            var selected = grid.SelectedRows.Cast<DataGridViewRow>().FirstOrDefault();
            if (selected == null) return (activeTableName, new Dictionary<string, object?>());

            var rowData = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (DataGridViewCell cell in selected.Cells)
            {
                var colName = cell?.OwningColumn?.Name ?? "";
                if (!string.IsNullOrWhiteSpace(colName))
                    rowData[colName] = cell.Value;
            }
            return (activeTableName, rowData);
        }
        #endregion

        #region ===== Helpers =====

        private static Dictionary<string, string> SelectedRowToDict(ExtendedDataGridView grid)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (grid == null) return dict;

            // Prefer an explicitly selected row; fall back to CurrentRow
            var row = grid.SelectedRows.Count > 0 ? grid.SelectedRows[0] : grid.CurrentRow;
            if (row == null) return dict;

            foreach (DataGridViewCell cell in row.Cells)
            {
                var colName = cell?.OwningColumn?.Name;
                if (string.IsNullOrWhiteSpace(colName)) continue;

                dict[colName] = cell?.Value?.ToString() ?? string.Empty;
            }

            return dict;
        }

        private void UpdateLocationLabel(ExtendedDataGridView? grid)
        {
            if (labelLocationSpecifier == null || grid == null) return;

            var tab = grid.Parent as TabPage;
            string tabText = tab?.Text ?? "";

            string tableName = grid.Tag as string ?? "";

            string colText = grid.CurrentCell?.OwningColumn?.HeaderText ?? "";
            object? rawVal = grid.CurrentCell?.Value;
            string valText = rawVal == null || rawVal == DBNull.Value ? "(null)" : Convert.ToString(rawVal) ?? "";

            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(tabText)) parts.Add(tabText);
            if (!string.IsNullOrWhiteSpace(tableName)) parts.Add(tableName);
            if (!string.IsNullOrWhiteSpace(colText)) parts.Add(colText);
            if (!string.IsNullOrWhiteSpace(valText)) parts.Add(valText);

            labelLocationSpecifier.Text = parts.Count == 0 ? "" : string.Join("  >  ", parts);
        }

        private void WireGridLocationEvents(ExtendedDataGridView grid)
        {
            void Refresh(object? s, EventArgs e) => UpdateLocationLabel(grid);

            grid.CurrentCellChanged += Refresh;
            grid.SelectionChanged += Refresh;
            grid.CellEnter += Refresh;
            grid.Sorted += Refresh;
            grid.DataBindingComplete += Refresh;
            grid.DataSourceChanged += Refresh;
            grid.ForeignKeyCellClicked += HandleForeignKeyCellClicked;
            grid.CellContentDoubleClick += DataGrid_CellContentClick;

            grid.CellMouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Right && e.RowIndex >= 0)
                {
                    grid.ClearSelection();
                    grid.Rows[e.RowIndex].Selected = true;
                    if (e.ColumnIndex >= 0)
                        grid.CurrentCell = grid[e.ColumnIndex, e.RowIndex];
                    Refresh(s, e);
                }
            };

            grid.RowCommandInvoked += (cmd, row) =>
            {
                switch (cmd)
                {
                    case ExtendedDataGridView.RowCommand.View:
                        {
                            var payload = SelectedRowToDict(grid);

                            string tabName = grid.FindForm()?.ActiveControl is TabControl tc && tc.SelectedTab != null
                                ? tc.SelectedTab.Text
                                : grid.Parent?.Name ?? "Unknown Tab";

                            var viewer = new CommonDataViewer(tabName.Replace("_TabPage", ""));
                            viewer.set_childs(payload);
                            viewer.Show();
                            break;
                        }

                    case ExtendedDataGridView.RowCommand.Edit:
                        // open edit UI
                        break;

                    case ExtendedDataGridView.RowCommand.Delete:
                        // confirm & delete by PK
                        break;
                }
            };
        }


        private void RefreshDataGrid(ExtendedDataGridView dataGrid, string tableName)
        {
            try
            {
                if (dataGrid == null || string.IsNullOrWhiteSpace(tableName) || !Tables.ContainsKey(tableName))
                    return;

                var dyn = Tables[tableName];
                var useAutoJoin = _autoJoinByTable.TryGetValue(tableName, out var flag) && flag;

                DataTable? data;
                if (useAutoJoin)
                {
                    data = dyn.AutoSelectWithJoins();
                    if (data != null)
                        dataGrid.RebindData(data, keepFilters: true);
                }
                else
                {
                    data = dataGrid.LoadDynamicClassData(keepFilters: true);
                }

                if (data == null)
                {
                    MessageBox.Show("No data returned for the table.");
                    return;
                }

                // If the page-level option is active, enforce it as a fixed filter
                string? selectedOption = null;
                if (_filters.TryGetValue(_filterTitle, out var di) && di != null)
                {
                    var v = di.Value as string;
                    selectedOption = string.IsNullOrWhiteSpace(v) ||
                                     string.Equals(v, SelectToken, StringComparison.OrdinalIgnoreCase)
                                     ? null
                                     : v.Trim();
                }

                if (!string.IsNullOrWhiteSpace(selectedOption))
                {
                    var col = FirstExistingFilterColumnOnGrid(dataGrid);
                    if (!string.IsNullOrWhiteSpace(col))
                    {
                        dataGrid.SetFilterEquals(col!, selectedOption!, asFixed: true);
                        dataGrid.ReapplyFilters();
                    }
                    else
                    {
                        void handler(object? s, EventArgs e)
                        {
                            dataGrid.DataBindingComplete -= handler;
                            var col2 = FirstExistingFilterColumnOnGrid(dataGrid);
                            if (!string.IsNullOrWhiteSpace(col2))
                            {
                                dataGrid.SetFilterEquals(col2!, selectedOption!, asFixed: true);
                                dataGrid.ReapplyFilters();
                            }
                        }
                        dataGrid.DataBindingComplete += handler;
                    }
                }
                else
                {
                    dataGrid.ClearFilter(includeFixed: false);
                    dataGrid.ReapplyFilters();
                }

                foreach (var key in _filters.Keys.ToList())
                    if (data.Columns.Contains(key))
                        _filters[key].UpdateOptions(SqlHealper.GetBindingListFromTable(data, key));

                dataGrid.Refresh();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error while refreshing the data grid: {ex.Message}");
            }
        }


        private string? FirstExistingFilterColumnOnGrid(ExtendedDataGridView grid)
        {
            if (grid == null || _filterColumns == null || _filterColumns.Length == 0)
                return null;

            foreach (var cand in _filterColumns)
            {
                if (!string.IsNullOrWhiteSpace(cand) && grid.Columns.Contains(cand))
                    return cand;
            }
            return null;
        }

        private void SelectRowInDataGrid(ExtendedDataGridView dataGrid, string fkColumn, object cellValue)
        {
            if (dataGrid == null || string.IsNullOrWhiteSpace(fkColumn))
                return;

            if (!dataGrid.Columns.Contains(fkColumn))
                return;

            foreach (DataGridViewRow row in dataGrid.Rows)
                row.Selected = false;

            foreach (DataGridViewRow row in dataGrid.Rows)
            {
                var v = row.Cells[fkColumn].Value;
                if (v != null && Equals(v, cellValue))
                {
                    row.Selected = true;
                    try
                    {
                        dataGrid.FirstDisplayedScrollingRowIndex = Math.Max(0, row.Index);
                    }
                    catch { /* ignore scrolling exceptions */ }
                    break;
                }
            }
        }

        private void EnsureFilterPanelVisible()
        {
            if (FillterPanel == null) return;

            FillterPanel.Visible = FillterPanel.Controls.Count > 0;
            FillterPanel.Enabled = FillterPanel.Controls.Count > 0;
        }

        private void CreateTabWithDataTable(string key, DataTable dt)
        {
            if (tabControl == null || dt == null)
                return;

            var safeKey = string.IsNullOrWhiteSpace(key) ? (dt.TableName ?? "Untitled") : key;

            if (Pages.TryGetValue(safeKey, out var existing))
            {
                tabControl.TabPages.Remove(existing);
                Pages.Remove(safeKey);
                Datas.Remove(safeKey);
            }

            var tabPage = new TabPage { Name = $"{safeKey}_TabPage", Text = (dt.TableName ?? safeKey).Trim() };
            Pages[safeKey] = tabPage;

            var grid = new ExtendedDataGridView(dt)
            {
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                BorderStyle = BorderStyle.FixedSingle,
                Dock = DockStyle.Fill
            };

            Datas[safeKey] = grid;

            tabControl.TabPages.Add(tabPage);
            tabPage.Controls.Add(grid);

            grid.ForeignKeyCellClicked += HandleForeignKeyCellClicked;
            grid.CellContentDoubleClick += DataGrid_CellContentClick;
        }

        private TabPage NewOrReplaceTab(string tableKey, string titleText)
        {
            // Create or replace tab (keeps Pages/Datas consistency)
            if (Pages.TryGetValue(tableKey, out var existing))
            {
                if (tabControl != null)
                    tabControl.TabPages.Remove(existing);
                Pages.Remove(tableKey);
                Datas.Remove(tableKey);
            }

            var tabPage = new TabPage { Name = $"{tableKey}_TabPage", Text = titleText };
            Pages[tableKey] = tabPage;
            tabControl?.TabPages.Add(tabPage);
            return tabPage;
        }

        private string GetTableTitle(DynamicClass tableClass)
        {
            var displayName = tableClass.GetTableDisplayName();
            return string.IsNullOrWhiteSpace(displayName) ? tableClass.Table : displayName;
        }

        private void ClearState()
        {
            // Remove all tab pages and clear dictionaries
            Pages.Clear();
            Datas.Clear();
            Tables.Clear();
            _autoJoinByTable.Clear();
        }
        #endregion
    }
}
