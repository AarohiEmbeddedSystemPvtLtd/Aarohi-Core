using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace Aarohi.Wpf.Controls.Lib.Controls.DataGridView
{
    public partial class ExtendedDataGrid : UserControl
    {
        #region Fields & Properties

        private DataTable _sourceData;
        private DataView _filteredView;
        private List<DataRowView> _allRows;
        private int _currentPage = 1;
        private int _pageSize = 50;
        private int _totalPages = 1;
        private DataGridColumnHeader _currentFilterColumn;

        private readonly Dictionary<string, string> _columnFilters = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _fixedFilters = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _originalHeaderText = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _primaryKeyColumns = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Dictionary<string, string>> _foreignKeyColumns = new(StringComparer.OrdinalIgnoreCase);

        private DataRowView _contextMenuRow;

        public event Action<string, string, object>? ForeignKeyCellClicked;
        public event Action<RowCommand, DataRowView>? RowCommandInvoked;
        public event Action<RowCommand, IDictionary<string, object?>>? RowCommandDataInvoked;

        // DataGrid Events
        public event SelectionChangedEventHandler? GridSelectionChanged;
        public event MouseButtonEventHandler? GridMouseDoubleClick;
        public event MouseButtonEventHandler? GridMouseRightButtonUp;
        public event EventHandler<DataGridCellEditEndingEventArgs>? GridCellEditEnding;
        public event EventHandler<DataGridRowEventArgs>? GridLoadingRow;
        public event EventHandler<DataGridRowEventArgs>? GridUnloadingRow;
        public event EventHandler<DataGridBeginningEditEventArgs>? GridBeginningEdit;
        public event KeyEventHandler? GridPreviewKeyDown;

        public enum RowCommand { View, Edit, Delete }

        #endregion

        #region Dependency Properties

        public static readonly DependencyProperty PageSizeProperty =
            DependencyProperty.Register(nameof(PageSize), typeof(int), typeof(ExtendedDataGrid),
                new PropertyMetadata(50, (d, e) => ((ExtendedDataGrid)d).OnPageSizeChanged((int)e.NewValue)));

        public int PageSize
        {
            get => (int)GetValue(PageSizeProperty);
            set => SetValue(PageSizeProperty, value);
        }

        public static readonly DependencyProperty EnablePaginationProperty =
            DependencyProperty.Register(nameof(EnablePagination), typeof(bool), typeof(ExtendedDataGrid),
                new PropertyMetadata(true));

        public bool EnablePagination
        {
            get => (bool)GetValue(EnablePaginationProperty);
            set => SetValue(EnablePaginationProperty, value);
        }

        public static readonly DependencyProperty AllowMultiSelectProperty =
            DependencyProperty.Register(nameof(AllowMultiSelect), typeof(bool), typeof(ExtendedDataGrid),
                new PropertyMetadata(true, (d, e) => ((ExtendedDataGrid)d).PART_Grid.SelectionMode =
                    (bool)e.NewValue ? DataGridSelectionMode.Extended : DataGridSelectionMode.Single));

        public bool AllowMultiSelect
        {
            get => (bool)GetValue(AllowMultiSelectProperty);
            set => SetValue(AllowMultiSelectProperty, value);
        }

        #endregion

        #region Constructor

        public ExtendedDataGrid()
        {
            _sourceData = new DataTable();
            _filteredView = new DataView(_sourceData);
            _allRows = new List<DataRowView>();
            _currentPage = 1;
            _pageSize = 50;
            _totalPages = 1;
            _currentFilterColumn = null;
            _contextMenuRow = null;

            InitializeComponent();
            InitializePageSizeCombo();
            UpdatePaginationUI();
        }

        #endregion

        #region Public Methods

        public void SetDataSource(DataTable data)
        {
            _sourceData = data ?? new DataTable();
            _allRows = new List<DataRowView>();
            _columnFilters.Clear();
            _fixedFilters.Clear();
            _currentPage = 1;
            _filteredView = new DataView(_sourceData);
            _allRows = _filteredView.Cast<DataRowView>().ToList();
            
            // Create explicit columns from DataTable
            if (PART_Grid != null)
            {
                PART_Grid.Columns.Clear();
                foreach (DataColumn col in _sourceData.Columns)
                {
                    var textCol = new DataGridTextColumn
                    {
                        Header = col.ColumnName,
                        Binding = new Binding($"[{col.ColumnName}]"),
                        Width = new DataGridLength(150, DataGridLengthUnitType.Pixel),
                        MinWidth = 100
                    };
                    PART_Grid.Columns.Add(textCol);
                }
                PART_Grid.UpdateLayout();
            }
            RefreshGrid();
        }

        public bool TryGetSelectedRowData(out IDictionary<string, object?> data)
        {
            data = null;
            var row = PART_Grid.SelectedItem as DataRowView;

            if (row == null && PART_Grid.SelectedItems.Count > 0)
                row = PART_Grid.SelectedItems[0] as DataRowView;

            if (row == null) return false;

            data = GetRowData(row);
            return true;
        }

        public IList<IDictionary<string, object?>> GetAllSelectedRowsData()
        {
            var list = new List<IDictionary<string, object?>>();
            foreach (DataRowView row in PART_Grid.SelectedItems)
                list.Add(GetRowData(row));
            return list;
        }

        public void SetFilterEquals(string column, string value, bool asFixed = true)
        {
            if (string.IsNullOrWhiteSpace(column)) return;

            var expr = BuildFilterExpr(column, "Equals", value ?? string.Empty);
            if (asFixed)
                _fixedFilters[column] = expr;
            else
                _columnFilters[column] = expr;

            _currentPage = 1;
            ApplyCombinedFilter();
        }

        public void SetFilterContains(string column, string value, bool asFixed = true)
        {
            if (string.IsNullOrWhiteSpace(column)) return;

            var expr = $"CONVERT([{column}], 'System.String') LIKE '%{EscapeLike(value ?? string.Empty)}%'";
            if (asFixed)
                _fixedFilters[column] = expr;
            else
                _columnFilters[column] = expr;

            _currentPage = 1;
            ApplyCombinedFilter();
        }

        public void ClearFilter(string column = null, bool includeFixed = false)
        {
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

            _currentPage = 1;
            ApplyCombinedFilter();
        }

        public void SetPrimaryKeyColumns(params string[] columns)
        {
            _primaryKeyColumns.Clear();
            foreach (var col in columns ?? Array.Empty<string>())
                _primaryKeyColumns.Add(col);
        }

        public void SetForeignKeyColumn(string column, string refTable, string refColumn)
        {
            if (!_foreignKeyColumns.ContainsKey(column))
                _foreignKeyColumns[column] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            _foreignKeyColumns[column][refTable] = refColumn;
        }

        public DataGrid GetInternalGrid() => PART_Grid;

        #endregion

        #region Pagination

        private void OnPageSizeChanged(int newSize)
        {
            _pageSize = newSize;
            _currentPage = 1;
            RefreshGrid();
        }

        private void InitializePageSizeCombo()
        {
            if (PART_PageSizeCombo != null)
                PART_PageSizeCombo.SelectedIndex = 1;
        }

        private void PageSizeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PART_PageSizeCombo.SelectedItem is ComboBoxItem item)
            {
                string content = item.Content.ToString();
                if (content == "All")
                {
                    _pageSize = int.MaxValue;
                }
                else if (int.TryParse(content, out int size))
                {
                    _pageSize = size;
                }

                _currentPage = 1;
                RefreshGrid();
            }
        }

        private void FirstPage_Click(object sender, RoutedEventArgs e)
        {
            _currentPage = 1;
            RefreshGrid();
        }

        private void PrevPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage > 1)
            {
                _currentPage--;
                RefreshGrid();
            }
        }

        private void NextPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage < _totalPages)
            {
                _currentPage++;
                RefreshGrid();
            }
        }

        private void LastPage_Click(object sender, RoutedEventArgs e)
        {
            _currentPage = _totalPages;
            RefreshGrid();
        }

        private void PageInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                if (int.TryParse(PART_PageInput.Text, out int page) && page > 0 && page <= _totalPages)
                {
                    _currentPage = page;
                    RefreshGrid();
                }
                e.Handled = true;
            }
        }

        private void PageInput_LostFocus(object sender, RoutedEventArgs e)
        {
            UpdatePaginationUI();
        }

        private void UpdatePaginationUI()
        {
            if (PART_PageInput == null || PART_PageTotal == null) return;

            int totalRows = _allRows?.Count ?? 0;
            _totalPages = _pageSize == int.MaxValue ? 1 : Math.Max(1, (totalRows + _pageSize - 1) / _pageSize);

            if (_currentPage > _totalPages)
                _currentPage = _totalPages;

            PART_PageInput.Text = _currentPage.ToString();
            PART_PageTotal.Text = $" of {_totalPages}";
            if (PART_RowInfo != null) PART_RowInfo.Text = $"{totalRows} rows";

            if (PART_FirstPage != null) PART_FirstPage.IsEnabled = _currentPage > 1;
            if (PART_PrevPage != null) PART_PrevPage.IsEnabled = _currentPage > 1;
            if (PART_NextPage != null) PART_NextPage.IsEnabled = _currentPage < _totalPages;
            if (PART_LastPage != null) PART_LastPage.IsEnabled = _currentPage < _totalPages;

            bool hasFilters = _columnFilters.Count > 0 || _fixedFilters.Count > 0;
            if (PART_FilterBadge != null) PART_FilterBadge.Visibility = hasFilters ? Visibility.Visible : Visibility.Collapsed;
            if (PART_ClearAllFiltersBtn != null) PART_ClearAllFiltersBtn.Visibility = hasFilters ? Visibility.Visible : Visibility.Collapsed;
        }

        #endregion

        #region Filtering & Sorting

        private void ApplyCombinedFilter()
        {
            if (_sourceData == null || _sourceData.Rows.Count == 0)
            {
                _allRows = new List<DataRowView>();
                RefreshGrid();
                return;
            }

            var parts = new List<string>();
            parts.AddRange(_fixedFilters.Values.Where(s => !string.IsNullOrWhiteSpace(s)));
            parts.AddRange(_columnFilters.Values.Where(s => !string.IsNullOrWhiteSpace(s)));

            try
            {
                _filteredView.RowFilter = string.Join(" AND ", parts);
                _allRows = _filteredView.Cast<DataRowView>().ToList();
            }
            catch
            {
                _allRows = _filteredView.Cast<DataRowView>().ToList();
            }

            RefreshGrid();
            RefreshHeaderFilterBadges();
        }

        private void RefreshGrid()
        {
            if (PART_Grid == null) return;

            UpdatePaginationUI();

            if (_allRows == null || _allRows.Count == 0)
            {
                PART_Grid.ItemsSource = null;
                return;
            }

            int skip = (_currentPage - 1) * _pageSize;
            var pageData = _allRows.Skip(skip).Take(_pageSize).ToList();

            PART_Grid.ItemsSource = pageData;

            // ADD THIS: Forces the ScrollViewer to re-evaluate the new column widths
            PART_Grid.UpdateLayout();

            // OPTIONAL: If the bar still won't show, force the internal scrollviewer to update
            var scrollViewer = GetVisualChild<ScrollViewer>(PART_Grid);
            scrollViewer?.InvalidateMeasure();
        }

        // Helper to find the internal ScrollViewer if needed
        private T GetVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T t) return t;
                var result = GetVisualChild<T>(child);
                if (result != null) return result;
            }
            return null;
        }

        private string BuildFilterExpr(string columnName, string op, string rawValue)
        {
            bool isNum = ColumnIsNumeric(columnName);

            return op switch
            {
                "Equals" => isNum
                    ? $"CONVERT([{columnName}], 'System.String') = '{EscapeLike(rawValue)}'"
                    : $"[{columnName}] = '{EscapeLike(rawValue)}'",

                "Contains" or "Search" => $"CONVERT([{columnName}], 'System.String') LIKE '%{EscapeLike(rawValue)}%'",

                "StartsWith" => $"CONVERT([{columnName}], 'System.String') LIKE '{EscapeLike(rawValue)}%'",

                "EndsWith" => $"CONVERT([{columnName}], 'System.String') LIKE '%{EscapeLike(rawValue)}'",

                "GreaterThan" => isNum
                    ? $"[{columnName}] > {rawValue}"
                    : $"CONVERT([{columnName}], 'System.String') > '{EscapeLike(rawValue)}'",

                "LessThan" => isNum
                    ? $"[{columnName}] < {rawValue}"
                    : $"CONVERT([{columnName}], 'System.String') < '{EscapeLike(rawValue)}'",

                "Clear" => string.Empty,

                _ => $"CONVERT([{columnName}], 'System.String') LIKE '%{EscapeLike(rawValue)}%'"
            };
        }

        private bool ColumnIsNumeric(string columnName)
        {
            if (_sourceData?.Columns.Contains(columnName) != true) return false;

            var colType = _sourceData.Columns[columnName].DataType;
            var underlyingType = Nullable.GetUnderlyingType(colType) ?? colType;

            return underlyingType == typeof(byte) || underlyingType == typeof(short) ||
                   underlyingType == typeof(int) || underlyingType == typeof(long) ||
                   underlyingType == typeof(float) || underlyingType == typeof(double) ||
                   underlyingType == typeof(decimal);
        }

        private static string EscapeLike(string input)
        {
            return input?.Replace("'", "''") ?? string.Empty;
        }

        private void RefreshHeaderFilterBadges()
        {
            if (PART_Grid == null) return;

            foreach (var col in PART_Grid.Columns)
            {
                string colName = col.Header?.ToString() ?? "";
                if (string.IsNullOrWhiteSpace(colName)) continue;

                if (!_originalHeaderText.TryGetValue(colName, out var baseText))
                {
                    baseText = colName;
                    _originalHeaderText[colName] = baseText;
                }

                bool hasUser = _columnFilters.ContainsKey(colName);
                bool hasFixed = _fixedFilters.ContainsKey(colName);

                string suffix = (hasFixed && hasUser) ? " ⧉🔒"
                              : hasFixed ? " 🔒"
                              : hasUser ? " ⧉"
                              : string.Empty;

                col.Header = baseText + suffix;
            }
        }

        #endregion

        #region Header Filter Popup

        private void FilterButton_Click(object sender, RoutedEventArgs e)
        {
            if (PART_SearchBox == null || PART_FilterPopup == null) return;

            if (sender is ToggleButton btn && btn.Tag is DataGridColumnHeader header)
            {
                _currentFilterColumn = header;
                PART_SearchBox.Clear();
                if (PART_ColumnCheckList != null)
                    PART_ColumnCheckList.ItemsSource = GetColumnVisibilityItems();

                PART_FilterPopup.PlacementTarget = btn;
                PART_FilterPopup.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                PART_FilterPopup.HorizontalOffset = 0;
                PART_FilterPopup.VerticalOffset = 2;
                PART_FilterPopup.IsOpen = true;
                PART_SearchBox.Focus();
            }
        }

        private void Popup_Close_Click(object sender, RoutedEventArgs e)
        {
            if (PART_FilterPopup != null)
                PART_FilterPopup.IsOpen = false;
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return && PART_SearchBox != null && PART_FilterPopup != null)
            {
                string colName = _currentFilterColumn?.Column?.Header?.ToString() ?? "";
                if (!string.IsNullOrWhiteSpace(colName))
                {
                    ApplyFilter(colName, "Search", PART_SearchBox.Text);
                    PART_FilterPopup.IsOpen = false;
                }
                e.Handled = true;
            }
        }

        private void ClearSearch_Click(object sender, RoutedEventArgs e)
        {
            if (PART_SearchBox != null)
                PART_SearchBox.Clear();
        }

        private void Sort_Asc_Click(object sender, RoutedEventArgs e)
        {
            if (PART_FilterPopup == null) return;

            string colName = _currentFilterColumn?.Column?.Header?.ToString() ?? "";
            if (!string.IsNullOrWhiteSpace(colName) && _filteredView != null)
            {
                _filteredView.Sort = $"[{colName}] ASC";
                _allRows = _filteredView.Cast<DataRowView>().ToList();
                RefreshGrid();
                PART_FilterPopup.IsOpen = false;
            }
        }

        private void Sort_Desc_Click(object sender, RoutedEventArgs e)
        {
            if (PART_FilterPopup == null) return;

            string colName = _currentFilterColumn?.Column?.Header?.ToString() ?? "";
            if (!string.IsNullOrWhiteSpace(colName) && _filteredView != null)
            {
                _filteredView.Sort = $"[{colName}] DESC";
                _allRows = _filteredView.Cast<DataRowView>().ToList();
                RefreshGrid();
                PART_FilterPopup.IsOpen = false;
            }
        }

        private void Filter_Equals_Click(object sender, RoutedEventArgs e) => PromptAndApplyFilter("Equals");
        private void Filter_Contains_Click(object sender, RoutedEventArgs e) => PromptAndApplyFilter("Contains");
        private void Filter_StartsWith_Click(object sender, RoutedEventArgs e) => PromptAndApplyFilter("StartsWith");
        private void Filter_EndsWith_Click(object sender, RoutedEventArgs e) => PromptAndApplyFilter("EndsWith");
        private void Filter_Greater_Click(object sender, RoutedEventArgs e) => PromptAndApplyFilter("GreaterThan");
        private void Filter_Less_Click(object sender, RoutedEventArgs e) => PromptAndApplyFilter("LessThan");

        private void PromptAndApplyFilter(string op)
        {
            var dlg = new InputDialog($"Enter value for {op}");
            if (dlg.ShowDialog() == true)
            {
                string colName = _currentFilterColumn?.Column?.Header?.ToString() ?? "";
                ApplyFilter(colName, op, dlg.InputValue);
                PART_FilterPopup.IsOpen = false;
            }
        }

        private void ApplyFilter(string column, string op, string value)
        {
            if (string.IsNullOrWhiteSpace(column)) return;

            if (string.IsNullOrWhiteSpace(value))
            {
                _columnFilters.Remove(column);
            }
            else
            {
                string expr = BuildFilterExpr(column, op, value);
                _columnFilters[column] = expr;
            }

            _currentPage = 1;
            ApplyCombinedFilter();
        }

        private void Filter_ClearThis_Click(object sender, RoutedEventArgs e)
        {
            if (PART_FilterPopup == null) return;

            string colName = _currentFilterColumn?.Column?.Header?.ToString() ?? "";
            if (!string.IsNullOrWhiteSpace(colName))
            {
                _columnFilters.Remove(colName);
                _currentPage = 1;
                ApplyCombinedFilter();
                PART_FilterPopup.IsOpen = false;
            }
        }

        private void Filter_ClearAll_Click(object sender, RoutedEventArgs e)
        {
            _columnFilters.Clear();
            _currentPage = 1;
            ApplyCombinedFilter();
            if (PART_FilterPopup != null)
                PART_FilterPopup.IsOpen = false;
        }

        private void Columns_ShowAll_Click(object sender, RoutedEventArgs e)
        {
            if (PART_Grid == null || PART_FilterPopup == null) return;

            foreach (var col in PART_Grid.Columns)
                col.Visibility = Visibility.Visible;
            PART_FilterPopup.IsOpen = false;
        }

        private void Columns_HideAll_Click(object sender, RoutedEventArgs e)
        {
            if (PART_Grid == null || PART_FilterPopup == null) return;

            foreach (var col in PART_Grid.Columns)
            {
                string colName = col.Header?.ToString() ?? "";
                if (!_primaryKeyColumns.Contains(colName))
                    col.Visibility = Visibility.Collapsed;
            }
            PART_FilterPopup.IsOpen = false;
        }

        private void ColumnVisibility_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && cb.DataContext is ColumnVisibilityItem item)
                item.Column.Visibility = item.IsVisible ? Visibility.Visible : Visibility.Collapsed;
        }

        private void View_AutosizeColumn_Click(object sender, RoutedEventArgs e)
        {
            if (PART_FilterPopup == null) return;

            if (_currentFilterColumn?.Column is DataGridColumn col)
            {
                col.Width = new DataGridLength(1, DataGridLengthUnitType.Auto);
            }
            PART_FilterPopup.IsOpen = false;
        }

        private void View_AutosizeAll_Click(object sender, RoutedEventArgs e)
        {
            if (PART_Grid == null || PART_FilterPopup == null) return;

            foreach (var col in PART_Grid.Columns)
                col.Width = new DataGridLength(1, DataGridLengthUnitType.Auto);
            PART_FilterPopup.IsOpen = false;
        }

        private ObservableCollection<ColumnVisibilityItem> GetColumnVisibilityItems()
        {
            var items = new ObservableCollection<ColumnVisibilityItem>();
            if (PART_Grid == null) return items;

            foreach (var col in PART_Grid.Columns)
            {
                string colName = col.Header?.ToString() ?? "";
                bool isPk = _primaryKeyColumns.Contains(colName);
                items.Add(new ColumnVisibilityItem
                {
                    Header = isPk ? $"{colName} (PK)" : colName,
                    IsVisible = col.Visibility == Visibility.Visible,
                    IsEnabled = !isPk,
                    Column = col
                });
            }
            return items;
        }

        private void ColumnSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (PART_ColumnSearchBox == null || PART_ColumnCheckList == null || PART_Grid == null) return;

            string searchText = PART_ColumnSearchBox.Text?.ToLower() ?? "";
            var allItems = new ObservableCollection<ColumnVisibilityItem>();

            foreach (var col in PART_Grid.Columns)
            {
                string colName = col.Header?.ToString() ?? "";
                bool isPk = _primaryKeyColumns.Contains(colName);

                if (string.IsNullOrWhiteSpace(searchText) || colName.ToLower().Contains(searchText))
                {
                    allItems.Add(new ColumnVisibilityItem
                    {
                        Header = isPk ? $"{colName} (PK)" : colName,
                        IsVisible = col.Visibility == Visibility.Visible,
                        IsEnabled = !isPk,
                        Column = col
                    });
                }
            }

            PART_ColumnCheckList.ItemsSource = allItems;
        }

        #endregion

        #region Row Context Menu

        private void PART_Grid_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (PART_Grid == null || RowContextMenu == null) return;

            var row = FindVisualParent<DataGridRow>(e.OriginalSource as DependencyObject);
            if (row != null)
            {
                _contextMenuRow = row.Item as DataRowView;
                PART_Grid.SelectedItem = _contextMenuRow;
                RowContextMenu.Visibility = Visibility.Visible;
                RowContextMenu.IsOpen = true;
            }
        }

        private void RowMenu_View_Click(object sender, RoutedEventArgs e)
        {
            if (_contextMenuRow != null)
            {
                RowCommandInvoked?.Invoke(RowCommand.View, _contextMenuRow);
                RowCommandDataInvoked?.Invoke(RowCommand.View, GetRowData(_contextMenuRow));
            }
        }

        private void RowMenu_Edit_Click(object sender, RoutedEventArgs e)
        {
            if (_contextMenuRow != null)
            {
                RowCommandInvoked?.Invoke(RowCommand.Edit, _contextMenuRow);
                RowCommandDataInvoked?.Invoke(RowCommand.Edit, GetRowData(_contextMenuRow));
            }
        }

        private void RowMenu_Delete_Click(object sender, RoutedEventArgs e)
        {
            if (_contextMenuRow != null)
            {
                RowCommandInvoked?.Invoke(RowCommand.Delete, _contextMenuRow);
                RowCommandDataInvoked?.Invoke(RowCommand.Delete, GetRowData(_contextMenuRow));
            }
        }

        private void PART_Grid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            GridMouseDoubleClick?.Invoke(this, e);

            var row = FindVisualParent<DataGridRow>(e.OriginalSource as DependencyObject);
            if (row?.Item is DataRowView drv)
            {
                RowCommandInvoked?.Invoke(RowCommand.View, drv);
                RowCommandDataInvoked?.Invoke(RowCommand.View, GetRowData(drv));
            }
        }

        #endregion

        #region DataGrid Events



        private void PART_Grid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            if (PART_Grid == null) return;

            e.Handled = true;
            string colName = e.Column.Header?.ToString() ?? "";
            if (!string.IsNullOrWhiteSpace(colName) && _filteredView != null)
            {
                var direction = e.Column.SortDirection == ListSortDirection.Ascending
                    ? ListSortDirection.Descending
                    : ListSortDirection.Ascending;

                _filteredView.Sort = $"[{colName}] {(direction == ListSortDirection.Ascending ? "ASC" : "DESC")}";
                _allRows = _filteredView.Cast<DataRowView>().ToList();
                RefreshGrid();

                e.Column.SortDirection = direction;
            }
        }

        private void PART_Grid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (PART_Grid == null) return;

            if (e.Row.Item is DataRowView row)
            {
                for (int i = 0; i < PART_Grid.Columns.Count; i++)
                {
                    string colName = PART_Grid.Columns[i].Header?.ToString() ?? "";
                    if (_foreignKeyColumns.ContainsKey(colName))
                    {
                        var cell = PART_Grid.Columns[i].GetCellContent(e.Row) as FrameworkElement;
                        if (cell != null)
                        {
                            cell.Cursor = Cursors.Hand;
                        }
                    }
                }
            }

            GridLoadingRow?.Invoke(this, e);
        }

        private void PART_Grid_UnloadingRow(object sender, DataGridRowEventArgs e)
        {
            GridUnloadingRow?.Invoke(this, e);
        }

        private void PART_Grid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            GridSelectionChanged?.Invoke(this, e);
        }

        private void PART_Grid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            GridCellEditEnding?.Invoke(this, e);
        }

        private void PART_Grid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            GridBeginningEdit?.Invoke(this, e);
        }

        private void PART_Grid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            GridPreviewKeyDown?.Invoke(this, e);
        }

        #endregion

        #region Helpers

        private IDictionary<string, object?> GetRowData(DataRowView row)
        {
            var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (DataColumn col in row.Row.Table.Columns)
            {
                var val = row[col.ColumnName];
                dict[col.ColumnName] = val is DBNull ? null : val;
            }
            return dict;
        }

        private static T FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T parent) return parent;
                child = VisualTreeHelper.GetParent(child);
            }
            return null;
        }

        #endregion
    }

    #region Supporting Classes

    public class ColumnVisibilityItem
    {
        public string Header { get; set; }
        public bool IsVisible { get; set; }
        public bool IsEnabled { get; set; }
        public DataGridColumn Column { get; set; }
    }

    public class InputDialog : Window
    {
        private System.Windows.Controls.TextBox _textBox;
        public string InputValue { get; set; }

        public InputDialog(string title)
        {
            Title = title;
            Width = 350;
            Height = 140;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new SolidColorBrush(Color.FromRgb(245, 248, 255));
            BorderBrush = new SolidColorBrush(Color.FromRgb(200, 216, 240));

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var label = new TextBlock
            {
                Text = title,
                Margin = new Thickness(12, 12, 12, 12),
                FontSize = 12,
                FontFamily = new FontFamily("Segoe UI")
            };

            _textBox = new System.Windows.Controls.TextBox
            {
                Margin = new Thickness(12, 0, 12, 8),
                Padding = new Thickness(4, 2, 4, 2),
                FontSize = 12,
                Height = 26
            };

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(12, 12, 12, 12),
                Height = 32
            };

            var okBtn = new Button
            {
                Content = "OK",
                Width = 70,
                Height = 26,
                Margin = new Thickness(0, 0, 4, 0),
                IsDefault = true
            };
            okBtn.Click += (s, e) => { InputValue = _textBox.Text; DialogResult = true; Close(); };

            var cancelBtn = new Button
            {
                Content = "Cancel",
                Width = 70,
                Height = 26,
                IsCancel = true
            };
            cancelBtn.Click += (s, e) => { DialogResult = false; Close(); };

            buttonPanel.Children.Add(okBtn);
            buttonPanel.Children.Add(cancelBtn);

            var stackPanel = new StackPanel();
            stackPanel.Children.Add(label);
            stackPanel.Children.Add(_textBox);

            Grid.SetRow(stackPanel, 0);
            Grid.SetRow(buttonPanel, 1);

            grid.Children.Add(stackPanel);
            grid.Children.Add(buttonPanel);

            Content = grid;
        }

        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);
            _textBox.Focus();
        }
    }

    #endregion
}
