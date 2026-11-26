using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;
using Aarohi.Classes;
using IOPath = System.IO.Path;

namespace Aarohi.Wpf.Controls.Lib.Controls.DataGridView;

[ContentProperty(nameof(Columns))]
public partial class ExtendedDataGridView : UserControl
{
    private const string RowActionsColumnName = "__RowActions";
    private static readonly object UnitTraceLock = new();
    private static readonly string UnitTraceFilePath = IOPath.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs", "ExtendedDataGridViewUnitDebug.txt");

    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(IEnumerable),
            typeof(ExtendedDataGridView),
            new PropertyMetadata(null, OnItemsSourceChanged));

    public static readonly DependencyProperty ColumnMetadataProperty =
        DependencyProperty.Register(
            nameof(ColumnMetadata),
            typeof(IEnumerable<TableColumnMetadata>),
            typeof(ExtendedDataGridView),
            new PropertyMetadata(null, OnColumnMetadataChanged));

    public static readonly DependencyProperty UnitDisplayModeProperty =
        DependencyProperty.Register(
            nameof(UnitDisplayMode),
            typeof(ColumnUnitDisplayMode),
            typeof(ExtendedDataGridView),
            new PropertyMetadata(ColumnUnitDisplayMode.Default, OnUnitDisplayModeChanged));

    public static readonly DependencyProperty SearchTextProperty =
        DependencyProperty.Register(
            nameof(SearchText),
            typeof(string),
            typeof(ExtendedDataGridView),
            new PropertyMetadata(string.Empty, OnSearchTextChanged));

    public static readonly DependencyProperty SelectedItemProperty =
        DependencyProperty.Register(
            nameof(SelectedItem),
            typeof(object),
            typeof(ExtendedDataGridView),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty ShowPaginationProperty =
        DependencyProperty.Register(
            nameof(ShowPagination),
            typeof(bool),
            typeof(ExtendedDataGridView),
            new PropertyMetadata(true, OnShowPaginationChanged));

    public static readonly DependencyProperty IsPaginationVisibleProperty =
        DependencyProperty.Register(
            nameof(IsPaginationVisible),
            typeof(bool),
            typeof(ExtendedDataGridView),
            new PropertyMetadata(true, OnIsPaginationVisibleChanged));

    public static readonly DependencyProperty PageSizeProperty =
        DependencyProperty.Register(
            nameof(PageSize),
            typeof(int),
            typeof(ExtendedDataGridView),
            new PropertyMetadata(25, OnPageSizeChanged));

    public static readonly DependencyProperty IsSearchBoxVisibleProperty =
        DependencyProperty.Register(
            nameof(IsSearchBoxVisible),
            typeof(bool),
            typeof(ExtendedDataGridView),
            new PropertyMetadata(true, OnIsSearchBoxVisibleChanged));

    public static readonly DependencyProperty AutoGenerateColumnsProperty =
        DependencyProperty.Register(
            nameof(AutoGenerateColumns),
            typeof(bool),
            typeof(ExtendedDataGridView),
            new PropertyMetadata(true));

    public static readonly DependencyProperty IsReadOnlyProperty =
        DependencyProperty.Register(
            nameof(IsReadOnly),
            typeof(bool),
            typeof(ExtendedDataGridView),
            new PropertyMetadata(true));

    public static readonly DependencyProperty CanUserAddRowsProperty =
        DependencyProperty.Register(
            nameof(CanUserAddRows),
            typeof(bool),
            typeof(ExtendedDataGridView),
            new PropertyMetadata(false));

    public static readonly DependencyProperty CanUserDeleteRowsProperty =
        DependencyProperty.Register(
            nameof(CanUserDeleteRows),
            typeof(bool),
            typeof(ExtendedDataGridView),
            new PropertyMetadata(false));

    public static readonly DependencyProperty ShowEditActionColumnProperty =
        DependencyProperty.Register(
            nameof(ShowEditActionColumn),
            typeof(bool),
            typeof(ExtendedDataGridView),
            new PropertyMetadata(false, OnActionColumnOptionsChanged));
    public static readonly DependencyProperty ShowViewActionColumnProperty =
        DependencyProperty.Register(
            nameof(ShowViewActionColumn),
            typeof(bool),
            typeof(ExtendedDataGridView),
            new PropertyMetadata(false, OnActionColumnOptionsChanged));

    public static readonly DependencyProperty ShowDeleteActionColumnProperty =
        DependencyProperty.Register(
            nameof(ShowDeleteActionColumn),
            typeof(bool),
            typeof(ExtendedDataGridView),
            new PropertyMetadata(false, OnActionColumnOptionsChanged));

    public static readonly DependencyProperty EditActionPermissionProperty =
        DependencyProperty.Register(
            nameof(EditActionPermission),
            typeof(string),
            typeof(ExtendedDataGridView),
            new PropertyMetadata(null, OnActionColumnOptionsChanged));

    public static readonly DependencyProperty DeleteActionPermissionProperty =
        DependencyProperty.Register(
            nameof(DeleteActionPermission),
            typeof(string),
            typeof(ExtendedDataGridView),
            new PropertyMetadata(null, OnActionColumnOptionsChanged));

    public static readonly DependencyProperty FrozenColumnCountProperty =
        DependencyProperty.Register(
            nameof(FrozenColumnCount),
            typeof(int),
            typeof(ExtendedDataGridView),
            new PropertyMetadata(0));

    public static readonly DependencyProperty ScrollUnitProperty =
        DependencyProperty.Register(
            nameof(ScrollUnit),
            typeof(ScrollUnit),
            typeof(ExtendedDataGridView),
            new PropertyMetadata(ScrollUnit.Item));

    public static readonly DependencyProperty SelectionModeProperty =
        DependencyProperty.Register(
            nameof(SelectionMode),
            typeof(DataGridSelectionMode),
            typeof(ExtendedDataGridView),
            new PropertyMetadata(DataGridSelectionMode.Single));

    public static readonly DependencyProperty SelectionUnitProperty =
        DependencyProperty.Register(
            nameof(SelectionUnit),
            typeof(DataGridSelectionUnit),
            typeof(ExtendedDataGridView),
            new PropertyMetadata(DataGridSelectionUnit.FullRow));

    public static readonly DependencyProperty DefaultFilterIconVisibilityProperty =
        DependencyProperty.RegisterAttached(
            "DefaultFilterIconVisibility",
            typeof(Visibility),
            typeof(ExtendedDataGridView),
            new PropertyMetadata(Visibility.Visible));

    public static readonly DependencyProperty UserFilterIconVisibilityProperty =
        DependencyProperty.RegisterAttached(
            "UserFilterIconVisibility",
            typeof(Visibility),
            typeof(ExtendedDataGridView),
            new PropertyMetadata(Visibility.Collapsed));

    public static readonly DependencyProperty LockedFilterIconVisibilityProperty =
        DependencyProperty.RegisterAttached(
            "LockedFilterIconVisibility",
            typeof(Visibility),
            typeof(ExtendedDataGridView),
            new PropertyMetadata(Visibility.Collapsed));

    private string? pendingFilterPropertyName;
    private ColumnFilter? userFilter;
    private readonly List<object> sourceItems = new();
    private readonly List<object> filteredItems = new();
    private readonly Dictionary<string, TableColumnMetadata> columnMetadataByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ColumnFilter> lockedFilters = new(StringComparer.OrdinalIgnoreCase);
    private INotifyCollectionChanged? sourceCollection;
    private int currentPage = 1;
    private int pageSize = 25;

    public ObservableCollection<ColumnOption> ColumnOptions { get; } = new();

    public ObservableCollection<object> PagedItems { get; } = new();

    public ObservableCollection<DataGridColumn> Columns
    {
        get { return PART_DataGrid.Columns; }
    }

    public IEnumerable? ItemsSource
    {
        get { return (IEnumerable?)GetValue(ItemsSourceProperty); }
        set { SetValue(ItemsSourceProperty, value); }
    }

    public IEnumerable<TableColumnMetadata>? ColumnMetadata
    {
        get { return (IEnumerable<TableColumnMetadata>?)GetValue(ColumnMetadataProperty); }
        set { SetValue(ColumnMetadataProperty, value); }
    }

    public ColumnUnitDisplayMode UnitDisplayMode
    {
        get { return (ColumnUnitDisplayMode)GetValue(UnitDisplayModeProperty); }
        set { SetValue(UnitDisplayModeProperty, value); }
    }

    public string SearchText
    {
        get { return (string)GetValue(SearchTextProperty); }
        set { SetValue(SearchTextProperty, value); }
    }

    public object? SelectedItem
    {
        get { return GetValue(SelectedItemProperty); }
        set { SetValue(SelectedItemProperty, value); }
    }

    public bool ShowPagination
    {
        get { return (bool)GetValue(ShowPaginationProperty); }
        set { SetValue(ShowPaginationProperty, value); }
    }

    public bool IsPaginationVisible
    {
        get { return (bool)GetValue(IsPaginationVisibleProperty); }
        set { SetValue(IsPaginationVisibleProperty, value); }
    }

    public int PageSize
    {
        get { return (int)GetValue(PageSizeProperty); }
        set { SetValue(PageSizeProperty, value); }
    }

    public bool IsSearchBoxVisible
    {
        get { return (bool)GetValue(IsSearchBoxVisibleProperty); }
        set { SetValue(IsSearchBoxVisibleProperty, value); }
    }

    public bool AutoGenerateColumns
    {
        get { return (bool)GetValue(AutoGenerateColumnsProperty); }
        set { SetValue(AutoGenerateColumnsProperty, value); }
    }

    public bool IsReadOnly
    {
        get { return (bool)GetValue(IsReadOnlyProperty); }
        set { SetValue(IsReadOnlyProperty, value); }
    }

    public bool CanUserAddRows
    {
        get { return (bool)GetValue(CanUserAddRowsProperty); }
        set { SetValue(CanUserAddRowsProperty, value); }
    }

    public bool CanUserDeleteRows
    {
        get { return (bool)GetValue(CanUserDeleteRowsProperty); }
        set { SetValue(CanUserDeleteRowsProperty, value); }
    }

    public bool ShowEditActionColumn
    {
        get { return (bool)GetValue(ShowEditActionColumnProperty); }
        set { SetValue(ShowEditActionColumnProperty, value); }
    }
    public bool ShowViewActionColumn
    {
        get { return (bool)GetValue(ShowViewActionColumnProperty); }
        set { SetValue(ShowViewActionColumnProperty, value); }
    }

    public bool ShowDeleteActionColumn
    {
        get { return (bool)GetValue(ShowDeleteActionColumnProperty); }
        set { SetValue(ShowDeleteActionColumnProperty, value); }
    }

    /// <summary>
    /// Global delegate to verify whether a permission string is granted.
    /// If null or not set, actions default to allowed (true).
    /// </summary>
    public static Func<string, bool>? HasPermissionCallback { get; set; }

    /// <summary>
    /// Global event to notify all active ExtendedDataGridView instances that user permissions have changed and columns should refresh.
    /// </summary>
    public static event EventHandler? PermissionsChanged;

    /// <summary>
    /// Invokes the PermissionsChanged event to refresh all active ExtendedDataGridView instances.
    /// </summary>
    public static void NotifyPermissionsChanged()
    {
        PermissionsChanged?.Invoke(null, EventArgs.Empty);
    }

    public string? EditActionPermission
    {
        get { return (string?)GetValue(EditActionPermissionProperty); }
        set { SetValue(EditActionPermissionProperty, value); }
    }

    public string? DeleteActionPermission
    {
        get { return (string?)GetValue(DeleteActionPermissionProperty); }
        set { SetValue(DeleteActionPermissionProperty, value); }
    }

    public int FrozenColumnCount
    {
        get { return (int)GetValue(FrozenColumnCountProperty); }
        set { SetValue(FrozenColumnCountProperty, value); }
    }

    public ScrollUnit ScrollUnit
    {
        get { return (ScrollUnit)GetValue(ScrollUnitProperty); }
        set { SetValue(ScrollUnitProperty, value); }
    }

    public DataGridSelectionMode SelectionMode
    {
        get { return (DataGridSelectionMode)GetValue(SelectionModeProperty); }
        set { SetValue(SelectionModeProperty, value); }
    }

    public DataGridSelectionUnit SelectionUnit
    {
        get { return (DataGridSelectionUnit)GetValue(SelectionUnitProperty); }
        set { SetValue(SelectionUnitProperty, value); }
    }

    public ExtendedDataGridView()
    {
        InitializeComponent();
        Loaded += ExtendedDataGridView_Loaded;
        PermissionsChanged += OnGlobalPermissionsChanged;
    }

    public event EventHandler<DataGridRowActionEventArgs>? EditActionRequested;
    public event SelectionChangedEventHandler? SelectionChanged;

    private void PART_DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SelectionChanged?.Invoke(this, e);
    }

    public void ScrollIntoView(object item)
    {
        PART_DataGrid?.ScrollIntoView(item);
    }

    public event EventHandler<DataGridRowActionEventArgs>? ViewActionRequested;
    public event EventHandler<DataGridRowActionEventArgs>? DeleteActionRequested;

    public static Visibility GetDefaultFilterIconVisibility(DependencyObject dependencyObject)
    {
        return (Visibility)dependencyObject.GetValue(DefaultFilterIconVisibilityProperty);
    }

    public static void SetDefaultFilterIconVisibility(DependencyObject dependencyObject, Visibility value)
    {
        dependencyObject.SetValue(DefaultFilterIconVisibilityProperty, value);
    }

    public static Visibility GetUserFilterIconVisibility(DependencyObject dependencyObject)
    {
        return (Visibility)dependencyObject.GetValue(UserFilterIconVisibilityProperty);
    }

    public static void SetUserFilterIconVisibility(DependencyObject dependencyObject, Visibility value)
    {
        dependencyObject.SetValue(UserFilterIconVisibilityProperty, value);
    }

    public static Visibility GetLockedFilterIconVisibility(DependencyObject dependencyObject)
    {
        return (Visibility)dependencyObject.GetValue(LockedFilterIconVisibilityProperty);
    }

    public static void SetLockedFilterIconVisibility(DependencyObject dependencyObject, Visibility value)
    {
        dependencyObject.SetValue(LockedFilterIconVisibilityProperty, value);
    }

    public void SetColumnFilter(string columnName, string? value, string filterMode = "Contains Filter")
    {
        if (string.IsNullOrWhiteSpace(columnName))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            ClearColumnFilter(columnName);
            return;
        }

        lockedFilters[columnName] = new ColumnFilter(columnName, filterMode, value.Trim());

        if (string.Equals(userFilter?.PropertyName, columnName, StringComparison.OrdinalIgnoreCase))
        {
            userFilter = null;
        }

        ApplyCurrentFilter();
        UpdateColumnFilterIndicators();
    }

    public void SetFilter(string columnName, string? value, string filterMode = "Contains Filter")
    {
        SetColumnFilter(columnName, value, filterMode);
    }

    public void ClearColumnFilter(string columnName)
    {
        if (string.IsNullOrWhiteSpace(columnName))
        {
            return;
        }

        lockedFilters.Remove(columnName);

        if (string.Equals(userFilter?.PropertyName, columnName, StringComparison.OrdinalIgnoreCase))
        {
            userFilter = null;
        }

        ApplyCurrentFilter();
        UpdateColumnFilterIndicators();
    }

    public void ClearFilter(string columnName)
    {
        ClearColumnFilter(columnName);
    }

    public void ClearColumnFilters()
    {
        lockedFilters.Clear();
        userFilter = null;
        ApplyCurrentFilter();
        UpdateColumnFilterIndicators();
    }

    public bool CommitEdit(DataGridEditingUnit editingUnit, bool exitEditingMode)
    {
        return PART_DataGrid.CommitEdit(editingUnit, exitEditingMode);
    }

    public void RefreshItems()
    {
        ResetSourceItems(ItemsSource);
    }

    public void RefreshColumnLayout()
    {
        RefreshColumnHeaders();
        RefreshPinnedColumns();
        RefreshPagedItems();
    }

    private static void OnItemsSourceChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        ((ExtendedDataGridView)dependencyObject).ResetSourceItems(e.NewValue as IEnumerable);
    }

    private static void OnColumnMetadataChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        ExtendedDataGridView gridView = (ExtendedDataGridView)dependencyObject;
        gridView.TraceUnit($"ColumnMetadata changed: newCount={CountOf(e.NewValue as IEnumerable<TableColumnMetadata>)} unitMode={gridView.UnitDisplayMode}");
        gridView.RebuildColumnMetadataMap(e.NewValue as IEnumerable<TableColumnMetadata>);
        gridView.RefreshColumnHeaders();
    }

    private static void OnUnitDisplayModeChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        ExtendedDataGridView gridView = (ExtendedDataGridView)dependencyObject;
        gridView.TraceUnit($"UnitDisplayMode changed: {e.OldValue} -> {e.NewValue}");
        gridView.RefreshColumnHeaders();
        gridView.PART_DataGrid?.Items.Refresh();
    }

    private static void OnSearchTextChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        ExtendedDataGridView gridView = (ExtendedDataGridView)dependencyObject;
        gridView.currentPage = 1;
        gridView.RebuildFilteredItems();
    }

    private static void OnShowPaginationChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        ExtendedDataGridView gridView = (ExtendedDataGridView)dependencyObject;
        if (e.NewValue is bool isVisible && gridView.IsPaginationVisible != isVisible)
        {
            gridView.IsPaginationVisible = isVisible;
        }
        gridView.currentPage = 1;
        gridView.UpdatePaginationVisibility();
        gridView.RefreshPagedItems();
    }

    private static void OnIsPaginationVisibleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ExtendedDataGridView gridView && e.NewValue is bool isVisible)
        {
            if (gridView.ShowPagination != isVisible)
            {
                gridView.ShowPagination = isVisible;
            }
            gridView.UpdatePaginationVisibility();
        }
    }

    private static void OnPageSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ExtendedDataGridView gridView && e.NewValue is int newSize && newSize > 0)
        {
            gridView.pageSize = newSize;
            gridView.currentPage = 1;
            gridView.SyncPageSizeComboBox(newSize);
            gridView.ClampCurrentPage();
            gridView.RefreshPagedItems();
        }
    }

    private static void OnIsSearchBoxVisibleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ExtendedDataGridView gridView)
        {
            gridView.UpdateSearchBoxVisibility();
        }
    }

    private static void OnActionColumnOptionsChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        ((ExtendedDataGridView)dependencyObject).RefreshPinnedColumns();
    }

    private void ResetSourceItems(IEnumerable? items)
    {
        if (sourceCollection is not null)
        {
            sourceCollection.CollectionChanged -= SourceCollection_OnCollectionChanged;
            sourceCollection = null;
        }

        sourceItems.Clear();

        if (items is not null)
        {
            foreach (object item in items)
            {
                sourceItems.Add(item);
            }
        }

        if (items is INotifyCollectionChanged notifyCollectionChanged)
        {
            sourceCollection = notifyCollectionChanged;
            sourceCollection.CollectionChanged += SourceCollection_OnCollectionChanged;
        }

        currentPage = 1;
        RebuildFilteredItems();
    }

    private void SourceCollection_OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshItems();
    }

    private void DataGrid_OnAutoGeneratingColumn(object? sender, DataGridAutoGeneratingColumnEventArgs e)
    {
        e.Column.Header = ResolveColumnHeader(e.PropertyName);
        TraceUnit($"AutoGeneratingColumn: property={e.PropertyName}; columnType={e.Column.GetType().Name}; header={e.Column.Header}; unitMode={UnitDisplayMode}");

        if (e.Column is DataGridBoundColumn boundColumn)
        {
            ApplyUnitValueBinding(boundColumn, e.PropertyName);
        }
    }

    private void DataGrid_OnAutoGeneratedColumns(object sender, EventArgs e)
    {
        TraceUnit($"AutoGeneratedColumns: columnCount={PART_DataGrid?.Columns.Count ?? 0}; unitMode={UnitDisplayMode}");
        RefreshPinnedColumns();
        ColumnOptions.Clear();
        string primaryKeyColumnName = GetPrimaryKeyColumnName();

        foreach (DataGridColumn column in PART_DataGrid.Columns)
        {
            string propertyName = GetColumnPropertyName(column);

            if (string.IsNullOrWhiteSpace(propertyName))
            {
                continue;
            }

            if (IsRowActionsColumn(column) ||
                string.Equals(propertyName, primaryKeyColumnName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            ColumnOptions.Add(new ColumnOption(column, column.Header?.ToString() ?? propertyName));
        }

        UpdateColumnFilterIndicators();
    }

    private void RefreshPinnedColumns()
    {
        if (PART_DataGrid is null)
        {
            return;
        }

        EnsureRowActionsColumn();

        string primaryKeyColumnName = GetPrimaryKeyColumnName();
        DataGridColumn? primaryKeyColumn = string.IsNullOrWhiteSpace(primaryKeyColumnName)
            ? null
            : PART_DataGrid.Columns.FirstOrDefault(column =>
                string.Equals(GetColumnPropertyName(column), primaryKeyColumnName, StringComparison.OrdinalIgnoreCase));

        int frozenCount = 0;

        if (primaryKeyColumn is not null)
        {
            primaryKeyColumn.Header = ResolveColumnHeader(primaryKeyColumnName, primaryKeyColumn.Header?.ToString());
            primaryKeyColumn.Visibility = Visibility.Visible;
            primaryKeyColumn.DisplayIndex = 0;
            frozenCount = 1;
        }

        DataGridColumn? actionsColumn = PART_DataGrid.Columns.FirstOrDefault(IsRowActionsColumn);

        if (actionsColumn is not null)
        {
            actionsColumn.DisplayIndex = primaryKeyColumn is null ? 0 : 1;
            frozenCount = primaryKeyColumn is null ? 1 : 2;
        }

        PART_DataGrid.FrozenColumnCount = Math.Max(FrozenColumnCount, frozenCount);
    }

    private void EnsureRowActionsColumn()
    {
        DataGridColumn? existingColumn = PART_DataGrid.Columns.FirstOrDefault(IsRowActionsColumn);

        if (!ShowEditActionColumn && !ShowDeleteActionColumn && !ShowViewActionColumn)
        {
            if (existingColumn is not null)
            {
                PART_DataGrid.Columns.Remove(existingColumn);
            }
            return;
        }

        if (existingColumn is DataGridTemplateColumn existingTemplateColumn)
        {
            //existingTemplateColumn.MinWidth = ShowEditActionColumn && ShowDeleteActionColumn && ShowViewActionColumn ? 112 : 64;
            existingTemplateColumn.MinWidth = GetActionColumnWidth();
            existingTemplateColumn.CellTemplate = CreateRowActionsTemplate();
            return;
        }

        var column = new DataGridTemplateColumn
        {
            Header = "Actions",
            SortMemberPath = RowActionsColumnName,
            IsReadOnly = true,
            CanUserSort = false,
            Width = DataGridLength.Auto,
            //MinWidth = ShowEditActionColumn && ShowDeleteActionColumn && ShowViewActionColumn ? 112 : 64,
            MinWidth = GetActionColumnWidth(),
            CellTemplate = CreateRowActionsTemplate()
        };

        SetDefaultFilterIconVisibility(column, Visibility.Collapsed);
        SetUserFilterIconVisibility(column, Visibility.Collapsed);
        SetLockedFilterIconVisibility(column, Visibility.Collapsed);
        PART_DataGrid.Columns.Insert(0, column);
    }
    private double GetActionColumnWidth()
    {
        int count =
            (ShowViewActionColumn ? 1 : 0) +
            (ShowEditActionColumn ? 1 : 0) +
            (ShowDeleteActionColumn ? 1 : 0);

        return count switch
        {
            3 => 112,
            2 => 82,
            1 => 42,
            _ => 0
        };
    }
    private DataTemplate CreateRowActionsTemplate()
    {
        var panelFactory = new FrameworkElementFactory(typeof(StackPanel));
        panelFactory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
        panelFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        panelFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(4, 0, 4, 0));

        if (ShowEditActionColumn)
        {
            panelFactory.AppendChild(CreateActionButtonFactory("Edit", "Edit"));
        }

        if (ShowDeleteActionColumn)
        {
            panelFactory.AppendChild(CreateActionButtonFactory("Delete", "Delete"));
        }
        if (ShowViewActionColumn)
        {
            panelFactory.AppendChild(CreateActionButtonFactory("View", "View"));
        }
        return new DataTemplate
        {
            VisualTree = panelFactory
        };
    }

    
    private FrameworkElementFactory CreateActionButtonFactory(string text, string action)
    {
        var buttonFactory = new FrameworkElementFactory(typeof(Button));
        bool isAllowed = IsActionAllowed(action);

        buttonFactory.SetValue(FrameworkElement.TagProperty, action);
        buttonFactory.SetValue(FrameworkElement.WidthProperty, 30.0);
        buttonFactory.SetValue(FrameworkElement.HeightProperty, 30.0);
        buttonFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(3, 0, 3, 0));
        buttonFactory.SetValue(Control.PaddingProperty, new Thickness(0));
        buttonFactory.SetValue(Control.CursorProperty, Cursors.Hand);
        buttonFactory.SetValue(Control.FocusableProperty, false);
        buttonFactory.SetValue(FrameworkElement.ToolTipProperty, text);
        buttonFactory.SetValue(Control.BorderThicknessProperty, new Thickness(1));
        buttonFactory.SetValue(UIElement.IsEnabledProperty, isAllowed);

        if (string.Equals(action, "Delete", StringComparison.OrdinalIgnoreCase))
        {
            buttonFactory.SetResourceReference(Control.BackgroundProperty, "AppBrush.DangerSubtle");
            buttonFactory.SetResourceReference(Control.BorderBrushProperty, "AppBrush.Danger");
            buttonFactory.SetResourceReference(Control.ForegroundProperty, "AppBrush.Danger");
        }
        else if (string.Equals(action, "View", StringComparison.OrdinalIgnoreCase))
        {
            buttonFactory.SetResourceReference(Control.BackgroundProperty, "AppBrush.PrimarySubtle");
            buttonFactory.SetResourceReference(Control.BorderBrushProperty, "AppBrush.Primary");
            buttonFactory.SetResourceReference(Control.ForegroundProperty, "AppBrush.Primary");
        }
        else
        {
            buttonFactory.SetResourceReference(Control.BackgroundProperty, "AppBrush.Control");
            buttonFactory.SetResourceReference(Control.BorderBrushProperty, "AppBrush.Border");
            buttonFactory.SetResourceReference(Control.ForegroundProperty, "AppBrush.TextSecondary");
        }

        buttonFactory.SetValue(Control.TemplateProperty, CreateActionButtonTemplate(
            CreateActionPathData(action),
            string.Equals(action, "Delete", StringComparison.OrdinalIgnoreCase)));

        buttonFactory.AddHandler(Button.ClickEvent, new RoutedEventHandler(RowActionButton_OnClick));

        return buttonFactory;
    }
    private static ControlTemplate CreateActionButtonTemplate(string pathData, bool isDangerAction)
    {
        var borderFactory = new FrameworkElementFactory(typeof(Border));
        borderFactory.Name = "ButtonChrome";
        borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
        borderFactory.SetValue(Border.SnapsToDevicePixelsProperty, true);
        borderFactory.SetBinding(Border.BackgroundProperty, new Binding(nameof(Button.Background))
        {
            RelativeSource = RelativeSource.TemplatedParent
        });
        borderFactory.SetBinding(Border.BorderBrushProperty, new Binding(nameof(Button.BorderBrush))
        {
            RelativeSource = RelativeSource.TemplatedParent
        });
        borderFactory.SetBinding(Border.BorderThicknessProperty, new Binding(nameof(Button.BorderThickness))
        {
            RelativeSource = RelativeSource.TemplatedParent
        });

        var gridFactory = new FrameworkElementFactory(typeof(Grid));
        var pathFactory = new FrameworkElementFactory(typeof(System.Windows.Shapes.Path));
        pathFactory.SetValue(FrameworkElement.WidthProperty, 14.0);
        pathFactory.SetValue(FrameworkElement.HeightProperty, 14.0);
        pathFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        pathFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        pathFactory.SetValue(System.Windows.Shapes.Path.DataProperty, Geometry.Parse(pathData));
        pathFactory.SetValue(Shape.StretchProperty, Stretch.Uniform);
        pathFactory.SetValue(Shape.StrokeThicknessProperty, 1.7);
        pathFactory.SetValue(Shape.StrokeStartLineCapProperty, PenLineCap.Round);
        pathFactory.SetValue(Shape.StrokeEndLineCapProperty, PenLineCap.Round);
        pathFactory.SetValue(Shape.StrokeLineJoinProperty, PenLineJoin.Round);
        pathFactory.SetBinding(Shape.StrokeProperty, new Binding(nameof(Button.Foreground))
        {
            RelativeSource = RelativeSource.TemplatedParent
        });

        gridFactory.AppendChild(pathFactory);
        borderFactory.AppendChild(gridFactory);

        var template = new ControlTemplate(typeof(Button))
        {
            VisualTree = borderFactory
        };

        Trigger hoverTrigger = new()
        {
            Property = UIElement.IsMouseOverProperty,
            Value = true
        };
        hoverTrigger.Setters.Add(new Setter(Control.BackgroundProperty, new DynamicResourceExtension(isDangerAction ? "AppBrush.DangerSubtle" : "AppBrush.PrimarySubtle")));
        hoverTrigger.Setters.Add(new Setter(Control.BorderBrushProperty, new DynamicResourceExtension(isDangerAction ? "AppBrush.Danger" : "AppBrush.Primary")));
        hoverTrigger.Setters.Add(new Setter(Control.ForegroundProperty, new DynamicResourceExtension(isDangerAction ? "AppBrush.Danger" : "AppBrush.Primary")));
        template.Triggers.Add(hoverTrigger);

        Trigger pressedTrigger = new()
        {
            Property = ButtonBase.IsPressedProperty,
            Value = true
        };
        pressedTrigger.Setters.Add(new Setter(Control.BackgroundProperty, new DynamicResourceExtension(isDangerAction ? "AppBrush.Danger" : "AppBrush.Primary")));
        pressedTrigger.Setters.Add(new Setter(Control.BorderBrushProperty, new DynamicResourceExtension(isDangerAction ? "AppBrush.Danger" : "AppBrush.Primary")));
        pressedTrigger.Setters.Add(new Setter(Control.ForegroundProperty, new DynamicResourceExtension("AppBrush.TextOnAccent")));
        template.Triggers.Add(pressedTrigger);

        Trigger disabledTrigger = new()
        {
            Property = UIElement.IsEnabledProperty,
            Value = false
        };
        disabledTrigger.Setters.Add(new Setter(UIElement.OpacityProperty, 0.42));
        disabledTrigger.Setters.Add(new Setter(Control.BackgroundProperty, new DynamicResourceExtension("AppBrush.Control")));
        disabledTrigger.Setters.Add(new Setter(Control.BorderBrushProperty, new DynamicResourceExtension("AppBrush.Border")));
        disabledTrigger.Setters.Add(new Setter(Control.ForegroundProperty, new DynamicResourceExtension("AppBrush.TextMuted")));
        template.Triggers.Add(disabledTrigger);

        return template;
    }

    private bool IsActionAllowed(string action)
    {
        if (string.Equals(action, "Delete", StringComparison.OrdinalIgnoreCase))
        {
            return string.IsNullOrWhiteSpace(DeleteActionPermission) || (HasPermissionCallback?.Invoke(DeleteActionPermission) ?? true);
        }

        if (string.Equals(action, "Edit", StringComparison.OrdinalIgnoreCase))
        {
            return string.IsNullOrWhiteSpace(EditActionPermission) || (HasPermissionCallback?.Invoke(EditActionPermission) ?? true);
        }

        return true;
    }

    private static string CreateActionPathData(string action)
    {
        if (string.Equals(action, "Delete", StringComparison.OrdinalIgnoreCase))
        {
            return ("M6,7 L18,7 M9,7 L9,18 M15,7 L15,18 M8,7 L8.8,20 L17.2,20 L18,7 M10,5 L14,5 L15,7 L9,7 Z");
        }

        if (string.Equals(action, "View", StringComparison.OrdinalIgnoreCase))
        {
            return ("M2,12 C5,6 9,4 12,4 C15,4 19,6 22,12 C19,18 15,20 12,20 C9,20 5,18 2,12 Z M12,8 A4,4 0 1 1 12,16 A4,4 0 1 1 12,8");
        }

        // Edit icon
        return ("M5,17.5 L5,21 L8.5,21 L18.8,10.7 L15.3,7.2 L5,17.5 Z M17,5.5 L18.5,4 C19.3,3.2 20.8,3.2 21.6,4 L22,4.4 C22.8,5.2 22.8,6.7 22,7.5 L20.5,9 L17,5.5 Z");
    }
    private void RowActionButton_OnClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;

        if (sender is not Button button || button.DataContext is null)
        {
            return;
        }

        string action = button.Tag?.ToString() ?? string.Empty;
        var args = new DataGridRowActionEventArgs(button.DataContext);

        if (string.Equals(action, "Edit", StringComparison.OrdinalIgnoreCase))
        {
            EditActionRequested?.Invoke(this, args);
        }
        else if (string.Equals(action, "Delete", StringComparison.OrdinalIgnoreCase))
        {
            DeleteActionRequested?.Invoke(this, args);
        }
        else if (string.Equals(action, "View", StringComparison.OrdinalIgnoreCase))
        {
            ViewActionRequested?.Invoke(this, args);
        }
    }

    private string GetPrimaryKeyColumnName()
    {
        return ColumnMetadata?
            .FirstOrDefault(column => column.IsPrimaryKey && !string.IsNullOrWhiteSpace(column.ColumnName))
            ?.ColumnName ?? string.Empty;
    }

    private static bool IsRowActionsColumn(DataGridColumn column)
    {
        return string.Equals(column.SortMemberPath, RowActionsColumnName, StringComparison.Ordinal);
    }

    private void ExtendedDataGridView_Loaded(object sender, RoutedEventArgs e)
    {
        pageSize = PageSize > 0 ? PageSize : 25;
        RefreshColumnHeaders();
        UpdateEmptyStateVisibility();
        UpdatePaginationVisibility();
        UpdateSearchBoxVisibility();
        SyncPageSizeComboBox(pageSize);
    }

    private void SearchBoxTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        bool hasText = !string.IsNullOrEmpty(SearchBoxTextBox?.Text);
        if (SearchPlaceholderText != null)
        {
            SearchPlaceholderText.Visibility = hasText ? Visibility.Collapsed : Visibility.Visible;
        }
        if (ClearSearchButton != null)
        {
            ClearSearchButton.Visibility = hasText ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void ClearSearchButton_OnClick(object sender, RoutedEventArgs e)
    {
        SearchText = string.Empty;
        if (SearchBoxTextBox != null)
        {
            SearchBoxTextBox.Text = string.Empty;
            SearchBoxTextBox.Focus();
        }
    }

    private void UpdateSearchBoxVisibility()
    {
        if (TopSearchBar is null)
        {
            return;
        }

        TopSearchBar.Visibility = IsSearchBoxVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SyncPageSizeComboBox(int size)
    {
        if (PageSizeComboBox is null)
        {
            return;
        }

        foreach (object item in PageSizeComboBox.Items)
        {
            if (item is ComboBoxItem cbi && int.TryParse(cbi.Content?.ToString(), out int val) && val == size)
            {
                PageSizeComboBox.SelectedItem = cbi;
                return;
            }
        }

        var customItem = new ComboBoxItem { Content = size.ToString() };
        PageSizeComboBox.Items.Insert(0, customItem);
        PageSizeComboBox.SelectedItem = customItem;
    }

    private void OnGlobalPermissionsChanged(object? sender, EventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        DataGrid_OnAutoGeneratedColumns(this, EventArgs.Empty);
    }

    private void FilterButton_OnClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;

        if (sender is not Button button || button.Tag is not DataGridColumn column)
        {
            return;
        }

        pendingFilterPropertyName = GetColumnPropertyName(column);
        FilterColumnText.Text = $"Filter {column.Header}";
        FilterPopup.PlacementTarget = button;
        FilterPopup.IsOpen = true;
        FilterValueTextBox.Focus();
        FilterValueTextBox.SelectAll();
    }

    private void ApplyFilterButton_OnClick(object sender, RoutedEventArgs e)
    {
        string? filterValue = FilterValueTextBox.Text?.Trim();
        userFilter = string.IsNullOrWhiteSpace(pendingFilterPropertyName) || string.IsNullOrWhiteSpace(filterValue)
            ? null
            : new ColumnFilter(pendingFilterPropertyName, FindSelectedFilterMode(), filterValue);
        ApplyCurrentFilter();
        UpdateColumnFilterIndicators();
        FilterPopup.IsOpen = false;
    }

    private void ClearFilterButton_OnClick(object sender, RoutedEventArgs e)
    {
        userFilter = null;
        FilterValueTextBox.Clear();
        ApplyCurrentFilter();
        UpdateColumnFilterIndicators();
        FilterPopup.IsOpen = false;
    }

    private void ApplyCurrentFilter()
    {
        currentPage = 1;
        RebuildFilteredItems();
    }

    private bool MatchesFilter(object item, ColumnFilter filter)
    {
        if (string.IsNullOrWhiteSpace(filter.PropertyName) || string.IsNullOrWhiteSpace(filter.Value))
        {
            return true;
        }

        object? rawValue = GetValue(item, filter.PropertyName);
        string cellValue = rawValue?.ToString() ?? string.Empty;
        string filterValue = filter.Value;

        return filter.Mode switch
        {
            "Equal Filter" => string.Equals(cellValue, filterValue, StringComparison.OrdinalIgnoreCase),
            "Not Equal Filter" => !string.Equals(cellValue, filterValue, StringComparison.OrdinalIgnoreCase),
            "Starts With Filter" => cellValue.StartsWith(filterValue, StringComparison.OrdinalIgnoreCase),
            "Ends With Filter" => cellValue.EndsWith(filterValue, StringComparison.OrdinalIgnoreCase),
            "Greater Than Filter" => CompareValues(cellValue, filterValue) > 0,
            "Less Than Filter" => CompareValues(cellValue, filterValue) < 0,
            _ => cellValue.Contains(filterValue, StringComparison.OrdinalIgnoreCase)
        };
    }

    private static int CompareValues(string left, string right)
    {
        if (double.TryParse(Regex.Match(left, @"-?\d+(\.\d+)?").Value, out double leftNumber) &&
            double.TryParse(Regex.Match(right, @"-?\d+(\.\d+)?").Value, out double rightNumber))
        {
            return leftNumber.CompareTo(rightNumber);
        }

        return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private void RebuildFilteredItems()
    {
        filteredItems.Clear();

        IEnumerable<object> items = sourceItems;

        foreach (ColumnFilter filter in lockedFilters.Values)
        {
            items = items.Where(item => MatchesFilter(item, filter));
        }

        if (userFilter is not null)
        {
            items = items.Where(item => MatchesFilter(item, userFilter));
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            items = items.Where(MatchesSearchText);
        }

        filteredItems.AddRange(items);
        ClampCurrentPage();
        RefreshPagedItems();
    }

    private void RefreshPagedItems()
    {
        PagedItems.Clear();

        IEnumerable<object> visibleItems = ShowPagination
            ? filteredItems.Skip((currentPage - 1) * pageSize).Take(pageSize)
            : filteredItems;

        foreach (object item in visibleItems)
        {
            PagedItems.Add(item);
        }

        UpdatePaginationText();
        UpdateEmptyStateVisibility();
    }

    private void UpdateEmptyStateVisibility()
    {
        if (EmptyStateOverlay == null)
        {
            return;
        }

        bool isEmpty = ItemsSource == null || sourceItems.Count == 0;
        EmptyStateOverlay.Visibility = isEmpty ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ClampCurrentPage()
    {
        currentPage = Math.Max(1, Math.Min(currentPage, GetTotalPages()));
    }

    private int GetTotalPages()
    {
        return Math.Max(1, (int)Math.Ceiling(filteredItems.Count / (double)pageSize));
    }

    private void UpdatePaginationText()
    {
        UpdatePaginationVisibility();

        if (!ShowPagination)
        {
            return;
        }

        int totalItems = filteredItems.Count;
        int firstVisible = totalItems == 0 ? 0 : ((currentPage - 1) * pageSize) + 1;
        int lastVisible = Math.Min(currentPage * pageSize, totalItems);
        int totalPages = GetTotalPages();

        PaginationSummaryText.Text = totalItems == 0
            ? "Showing 0 of 0"
            : $"Showing {firstVisible}-{lastVisible} of {totalItems}";
        PageNumberTextBox.Text = currentPage.ToString();
        PaginationTotalPageText.Text = $"/ {totalPages}";

        bool canMoveBack = currentPage > 1;
        bool canMoveNext = currentPage < totalPages;
        FirstPageButton.IsEnabled = canMoveBack;
        PreviousPageButton.IsEnabled = canMoveBack;
        NextPageButton.IsEnabled = canMoveNext;
        LastPageButton.IsEnabled = canMoveNext;
    }

    private void UpdatePaginationVisibility()
    {
        if (PaginationBar is null)
        {
            return;
        }

        PaginationBar.Visibility = ShowPagination ? Visibility.Visible : Visibility.Collapsed;
    }

    private void PageSizeComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || PageSizeComboBox.SelectedItem is not ComboBoxItem item)
        {
            return;
        }

        if (int.TryParse(item.Content?.ToString(), out int selectedPageSize) && selectedPageSize > 0)
        {
            if (PageSize != selectedPageSize)
            {
                PageSize = selectedPageSize;
            }
            else
            {
                pageSize = selectedPageSize;
                currentPage = 1;
                ClampCurrentPage();
                RefreshPagedItems();
            }
        }
    }

    private void FirstPageButton_OnClick(object sender, RoutedEventArgs e)
    {
        currentPage = 1;
        RefreshPagedItems();
    }

    private void PreviousPageButton_OnClick(object sender, RoutedEventArgs e)
    {
        currentPage--;
        ClampCurrentPage();
        RefreshPagedItems();
    }

    private void NextPageButton_OnClick(object sender, RoutedEventArgs e)
    {
        currentPage++;
        ClampCurrentPage();
        RefreshPagedItems();
    }

    private void LastPageButton_OnClick(object sender, RoutedEventArgs e)
    {
        currentPage = GetTotalPages();
        RefreshPagedItems();
    }

    private void PageNumberTextBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        ApplyTypedPageNumber();
        e.Handled = true;
    }

    private void PageNumberTextBox_OnLostFocus(object sender, RoutedEventArgs e)
    {
        ApplyTypedPageNumber();
    }

    private void ApplyTypedPageNumber()
    {
        if (!int.TryParse(PageNumberTextBox.Text, out int requestedPage))
        {
            PageNumberTextBox.Text = currentPage.ToString();
            return;
        }

        currentPage = Math.Max(1, Math.Min(requestedPage, GetTotalPages()));
        RefreshPagedItems();
    }

    private static string GetColumnPropertyName(DataGridColumn column)
    {
        if (!string.IsNullOrWhiteSpace(column.SortMemberPath))
        {
            return column.SortMemberPath;
        }

        if (column is DataGridBoundColumn boundColumn && boundColumn.Binding is System.Windows.Data.Binding binding)
        {
            return binding.Path?.Path ?? string.Empty;
        }

        return column.Header?.ToString()?.Replace(" ", string.Empty) ?? string.Empty;
    }

    private void RebuildColumnMetadataMap(IEnumerable<TableColumnMetadata>? columns)
    {
        columnMetadataByName.Clear();

        if (columns is null)
        {
            return;
        }

        foreach (TableColumnMetadata column in columns)
        {
            if (string.IsNullOrWhiteSpace(column.ColumnName))
            {
                continue;
            }

            columnMetadataByName[column.ColumnName] = column;
        }
    }

    private void RefreshColumnHeaders()
    {
        if (PART_DataGrid is null)
        {
            return;
        }

        TraceUnit($"RefreshColumnHeaders start: columnCount={PART_DataGrid.Columns.Count}; metadataCount={columnMetadataByName.Count}; unitMode={UnitDisplayMode}");

        foreach (DataGridColumn column in PART_DataGrid.Columns)
        {
            string propertyName = GetColumnPropertyName(column);
            TraceUnit($"RefreshColumnHeaders column: property={propertyName}; columnType={column.GetType().Name}; currentHeader={column.Header}; sortMemberPath={column.SortMemberPath}");
            column.Header = ResolveColumnHeader(propertyName, column.Header?.ToString());
            if (column is DataGridBoundColumn boundColumn)
            {
                ApplyUnitValueBinding(boundColumn, propertyName);
            }
        }

        DataGrid_OnAutoGeneratedColumns(this, EventArgs.Empty);
        PART_DataGrid.Items.Refresh();
        TraceUnit("RefreshColumnHeaders done");
    }

    private void ApplyUnitValueBinding(DataGridBoundColumn boundColumn, string propertyName)
    {
        if (boundColumn.Binding is not Binding binding)
        {
            TraceUnit($"ApplyUnitValueBinding skipped: property={propertyName}; bindingType={boundColumn.Binding?.GetType().Name ?? "null"}");
            return;
        }

        if (!columnMetadataByName.TryGetValue(propertyName, out TableColumnMetadata? metadata))
        {
            TraceUnit($"ApplyUnitValueBinding no metadata: property={propertyName}; existingBindingPath={binding.Path?.Path}; unitMode={UnitDisplayMode}");
            return;
        }

        Binding convertedBinding = new(propertyName);
        CopyBindingOptions(binding, convertedBinding);
        convertedBinding.Converter = UnitValueConverter.Instance;
        convertedBinding.ConverterParameter = new UnitValueContext(this, propertyName, metadata);
        boundColumn.Binding = convertedBinding;

        TraceUnit(
            $"ApplyUnitValueBinding applied: property={propertyName}; existingPath={binding.Path?.Path}; " +
            $"sourceUnit={DescribeSourceUnit(metadata)}; targetUnit={metadata.GetUnitText(UnitDisplayMode)}; " +
            $"header={metadata.GetHeaderText(UnitDisplayMode)}; bindingMode={binding.Mode}; stringFormat={binding.StringFormat}");
    }

    private static void CopyBindingOptions(Binding source, Binding target)
    {
        target.Mode = source.Mode;
        target.UpdateSourceTrigger = source.UpdateSourceTrigger;
        target.Converter = source.Converter;
        target.ConverterCulture = source.ConverterCulture;
        target.ConverterParameter = source.ConverterParameter;
        target.FallbackValue = source.FallbackValue;
        target.StringFormat = source.StringFormat;
        target.TargetNullValue = source.TargetNullValue;
        target.BindsDirectlyToSource = source.BindsDirectlyToSource;
        target.NotifyOnSourceUpdated = source.NotifyOnSourceUpdated;
        target.NotifyOnTargetUpdated = source.NotifyOnTargetUpdated;
        target.NotifyOnValidationError = source.NotifyOnValidationError;
        target.ValidatesOnDataErrors = source.ValidatesOnDataErrors;
        target.ValidatesOnExceptions = source.ValidatesOnExceptions;
        target.IsAsync = source.IsAsync;
    }

    private void UpdateColumnFilterIndicators()
    {
        if (PART_DataGrid is null)
        {
            return;
        }

        foreach (DataGridColumn column in PART_DataGrid.Columns)
        {
            string propertyName = GetColumnPropertyName(column);
            if (IsRowActionsColumn(column))
            {
                SetDefaultFilterIconVisibility(column, Visibility.Collapsed);
                SetUserFilterIconVisibility(column, Visibility.Collapsed);
                SetLockedFilterIconVisibility(column, Visibility.Collapsed);
                continue;
            }

            bool hasLockedFilter = lockedFilters.ContainsKey(propertyName);
            bool hasUserFilter = string.Equals(userFilter?.PropertyName, propertyName, StringComparison.OrdinalIgnoreCase);

            SetDefaultFilterIconVisibility(column, hasLockedFilter || hasUserFilter ? Visibility.Collapsed : Visibility.Visible);
            SetUserFilterIconVisibility(column, !hasLockedFilter && hasUserFilter ? Visibility.Visible : Visibility.Collapsed);
            SetLockedFilterIconVisibility(column, hasLockedFilter ? Visibility.Visible : Visibility.Collapsed);
        }
    }

    private string ResolveColumnHeader(string propertyName)
    {
        return ResolveColumnHeader(propertyName, null);
    }

    private string ResolveColumnHeader(string propertyName, string? fallbackHeader)
    {
        if (columnMetadataByName.TryGetValue(propertyName, out TableColumnMetadata? metadata))
        {
            return metadata.GetHeaderText(UnitDisplayMode);
        }

        if (!string.IsNullOrWhiteSpace(fallbackHeader))
        {
            return fallbackHeader;
        }

        return SplitPascalCase(propertyName);
    }

    private bool MatchesSearchText(object item)
    {
        string text = SearchText.Trim();

        foreach (string propertyName in GetSearchPropertyNames(item))
        {
            object? value = GetValue(item, propertyName);

            if ((value?.ToString() ?? string.Empty).Contains(text, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private IEnumerable<string> GetSearchPropertyNames(object item)
    {
        if (columnMetadataByName.Count > 0)
        {
            return columnMetadataByName.Keys;
        }

        if (item is DataRowView dataRowView)
        {
            return dataRowView.Row.Table.Columns.Cast<DataColumn>().Select(column => column.ColumnName);
        }

        if (item is DataRow dataRow)
        {
            return dataRow.Table.Columns.Cast<DataColumn>().Select(column => column.ColumnName);
        }

        if (item is IDictionary<string, object?> dictionary)
        {
            return dictionary.Keys;
        }

        if (item is IDictionary legacyDictionary)
        {
            return legacyDictionary.Keys.Cast<object>().Select(key => key?.ToString() ?? string.Empty);
        }

        return TypeDescriptor.GetProperties(item).Cast<PropertyDescriptor>().Select(property => property.Name);
    }

    private static object? GetValue(object item, string propertyName)
    {
        if (item is DataRowView dataRowView && dataRowView.Row.Table.Columns.Contains(propertyName))
        {
            return dataRowView[propertyName];
        }

        if (item is DataRow dataRow && dataRow.Table.Columns.Contains(propertyName))
        {
            return dataRow[propertyName];
        }

        if (item is IDictionary<string, object?> dictionary && dictionary.TryGetValue(propertyName, out object? dictionaryValue))
        {
            return dictionaryValue;
        }

        if (item is IDictionary legacyDictionary && legacyDictionary.Contains(propertyName))
        {
            return legacyDictionary[propertyName];
        }

        return TypeDescriptor.GetProperties(item)[propertyName]?.GetValue(item);
    }

    private string FindSelectedFilterMode()
    {
        foreach (RadioButton radioButton in FindVisualChildren<RadioButton>(FilterPopup.Child))
        {
            if (radioButton.GroupName == "FilterModes" && radioButton.IsChecked == true)
            {
                return radioButton.Tag?.ToString() ?? "Contains Filter";
            }
        }

        return "Contains Filter";
    }
    public string GetAvailableColumnNamesForFilter()
    {
        if (sourceItems.Count == 0)
            return "No source items loaded.";

        return string.Join(Environment.NewLine, GetSearchPropertyNames(sourceItems[0]));
    }
    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject? root)
        where T : DependencyObject
    {
        if (root is null)
        {
            yield break;
        }

        for (int index = 0; index < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); index++)
        {
            DependencyObject child = System.Windows.Media.VisualTreeHelper.GetChild(root, index);

            if (child is T match)
            {
                yield return match;
            }

            foreach (T descendant in FindVisualChildren<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private static string SplitPascalCase(string value)
    {
        return Regex.Replace(value, "([a-z0-9])([A-Z])", "$1 $2");
    }

    private sealed class UnitValueContext
    {
        public UnitValueContext(ExtendedDataGridView owner, string propertyName, TableColumnMetadata metadata)
        {
            Owner = owner;
            PropertyName = propertyName;
            Metadata = metadata;
        }

        public ExtendedDataGridView Owner { get; }

        public string PropertyName { get; }

        public TableColumnMetadata Metadata { get; }
    }

    private sealed class UnitValueConverter : System.Windows.Data.IValueConverter
    {
        public static readonly UnitValueConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is null || value == DBNull.Value)
            {
                return string.Empty;
            }

            if (parameter is not UnitValueContext context)
            {
                return value?.ToString() ?? string.Empty;
            }

            return ConvertValue(value, context);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }

        private static string ConvertValue(object rawValue, UnitValueContext context)
        {
            ColumnUnitDisplayMode mode = context.Owner.UnitDisplayMode;
            TableColumnMetadata metadata = context.Metadata;

            string sourceUnit = FirstNotEmpty(
                metadata.DefaultUnit,
                metadata.Unit,
                metadata.InputUnit,
                metadata.LastUsedUnit,
                metadata.ReportUnit,
                metadata.ShowUnit);
            string targetUnit = metadata.GetUnitText(mode);

            context.Owner.TraceUnit(
                $"Convert start: property={context.PropertyName}; raw={rawValue}; rawType={rawValue.GetType().Name}; " +
                $"mode={mode}; sourceUnit={sourceUnit}; targetUnit={targetUnit}; parameter={metadata.Parameter}; defaultUnit={metadata.DefaultUnit}; unit={metadata.Unit}; " +
                $"inputUnit={metadata.InputUnit}; showUnit={metadata.ShowUnit}; reportUnit={metadata.ReportUnit}; lastUsedUnit={metadata.LastUsedUnit}");

            if (string.IsNullOrWhiteSpace(targetUnit) ||
                string.IsNullOrWhiteSpace(sourceUnit) ||
                mode == ColumnUnitDisplayMode.Default)
            {
                context.Owner.TraceUnit(
                    $"Convert bypassed: property={context.PropertyName}; reason={GetBypassReason(mode, sourceUnit, targetUnit)}; raw={rawValue}");
                return rawValue.ToString() ?? string.Empty;
            }

            if (!TryToDouble(rawValue, out double numericValue))
            {
                context.Owner.TraceUnit($"Convert parse failed: property={context.PropertyName}; raw={rawValue}; rawType={rawValue.GetType().Name}");
                return rawValue.ToString() ?? string.Empty;
            }

            string parameter = FirstNotEmpty(metadata.Parameter, metadata.DisplayName, metadata.ColumnName);

            try
            {
                if (string.Equals(sourceUnit, targetUnit, StringComparison.OrdinalIgnoreCase))
                {
                    string sameUnitFormat = UnitConverisonEngine.GetNumberFormat(parameter, sourceUnit, targetUnit);
                    string sameUnitFormatted = UnitConverisonEngine.FormatValue(numericValue, sameUnitFormat);
                    context.Owner.TraceUnit(
                        $"Convert same-unit formatted: property={context.PropertyName}; parameter={parameter}; input={numericValue}; sourceUnit={sourceUnit}; targetUnit={targetUnit}; format={sameUnitFormat}; formatted={sameUnitFormatted}");
                    return sameUnitFormatted;
                }

                return TryConvertWithParameterCandidates(
                    context,
                    numericValue,
                    sourceUnit,
                    targetUnit,
                    out string convertedText)
                    ? convertedText
                    : rawValue.ToString() ?? string.Empty;
            }
            catch (Exception ex)
            {
                context.Owner.TraceUnit($"Convert failed: property={context.PropertyName}; parameter={parameter}; sourceUnit={sourceUnit}; targetUnit={targetUnit}; error={ex.Message}");
                return rawValue.ToString() ?? string.Empty;
            }
        }

        private static bool TryToDouble(object rawValue, out double result)
        {
            string text = rawValue.ToString() ?? string.Empty;
            return double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out result)
                || double.TryParse(text, NumberStyles.Any, CultureInfo.CurrentCulture, out result);
        }

        private static string FirstNotEmpty(params string?[] values)
        {
            foreach (string? value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return string.Empty;
        }

        private static bool TryConvertWithParameterCandidates(
            UnitValueContext context,
            double numericValue,
            string sourceUnit,
            string targetUnit,
            out string convertedText)
        {
            convertedText = string.Empty;

            string[] candidates =
            {
                context.Metadata.Parameter,
                FirstNotEmpty(context.Metadata.DisplayName, context.Metadata.ColumnName),
                context.Metadata.DisplayName,
                context.Metadata.ColumnName,
                NormalizeParameterName(context.Metadata.Parameter),
                NormalizeParameterName(context.Metadata.ColumnName),
                NormalizeParameterName(context.Metadata.DisplayName)
            };

            foreach (string parameter in candidates.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    double converted = UnitConverisonEngine.ConvertValue(parameter, numericValue, sourceUnit, targetUnit);
                    string format = UnitConverisonEngine.GetNumberFormat(parameter, sourceUnit, targetUnit);
                    convertedText = UnitConverisonEngine.FormatValue(converted, format);
                    context.Owner.TraceUnit(
                        $"Convert ok: property={context.PropertyName}; parameter={parameter}; input={numericValue}; converted={converted}; format={format}; formatted={convertedText}");
                    return true;
                }
                catch (Exception ex)
                {
                    context.Owner.TraceUnit(
                        $"Convert candidate failed: property={context.PropertyName}; parameter={parameter}; sourceUnit={sourceUnit}; targetUnit={targetUnit}; error={ex.Message}");
                }
            }

            return false;
        }

        private static string NormalizeParameterName(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string normalized = Regex.Replace(value.Trim(), @"[_\s]+", " ");
            normalized = Regex.Replace(normalized, @"(?<!^)([A-Z])", " $1");
            return Regex.Replace(normalized, @"\s+", " ").Trim();
        }

        private static string GetBypassReason(ColumnUnitDisplayMode mode, string sourceUnit, string targetUnit)
        {
            if (mode == ColumnUnitDisplayMode.Default)
            {
                return "mode=Default";
            }

            if (string.IsNullOrWhiteSpace(sourceUnit))
            {
                return "missing source unit";
            }

            if (string.IsNullOrWhiteSpace(targetUnit))
            {
                return "missing target unit";
            }

            return "unknown";
        }
    }

    private void TraceUnit(string message)
    {
        try
        {
            lock (UnitTraceLock)
            {
                string? directory = IOPath.GetDirectoryName(UnitTraceFilePath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.AppendAllText(
                    UnitTraceFilePath,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Debug tracing must never break the UI.
        }
    }

    private static int CountOf(IEnumerable<TableColumnMetadata>? columns)
    {
        return columns?.Count() ?? 0;
    }

    private static string DescribeSourceUnit(TableColumnMetadata metadata)
    {
        if (!string.IsNullOrWhiteSpace(metadata.DefaultUnit))
        {
            return metadata.DefaultUnit;
        }

        if (!string.IsNullOrWhiteSpace(metadata.Unit))
        {
            return metadata.Unit;
        }

        if (!string.IsNullOrWhiteSpace(metadata.InputUnit))
        {
            return metadata.InputUnit;
        }

        if (!string.IsNullOrWhiteSpace(metadata.LastUsedUnit))
        {
            return metadata.LastUsedUnit;
        }

        if (!string.IsNullOrWhiteSpace(metadata.ReportUnit))
        {
            return metadata.ReportUnit;
        }

        if (!string.IsNullOrWhiteSpace(metadata.ShowUnit))
        {
            return metadata.ShowUnit;
        }

        return string.Empty;
    }
}

public sealed class DataGridRowActionEventArgs : EventArgs
{
    public DataGridRowActionEventArgs(object item)
    {
        Item = item;
    }

    public object Item { get; }
}

public sealed class ColumnOption : INotifyPropertyChanged
{
    private readonly DataGridColumn column;
    private bool isVisible;

    public ColumnOption(DataGridColumn column, string displayName)
    {
        this.column = column;
        DisplayName = displayName;
        isVisible = column.Visibility == Visibility.Visible;
    }

    public string DisplayName { get; }

    public bool IsVisible
    {
        get { return isVisible; }
        set
        {
            if (isVisible == value)
            {
                return;
            }

            isVisible = value;
            column.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsVisible)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

internal sealed record ColumnFilter(string PropertyName, string Mode, string Value);
