using Aarohi.Classes.Healper;
using Aarohi.ExtendedUI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static Aarohi.Classes.Healper.SqlHealper;

namespace Aarohi.Classes
{

    /// <summary>
    /// Dynamic form builder that generates input controls for one or more <see cref="DynamicClass"/> entities.
    /// Supports:
    /// - Grid-based layout: decimals/bools, text fields, and dropdowns.
    /// - Dependency rules: enable/disable, required, option population, data rewrite.
    /// - Foreign key filters and dynamic WHERE/parameter providers.
    /// - Validation via <see cref="ErrorProvider"/> and saving into <see cref="DynamicClass.Values"/>.
    /// </summary>
    public partial class DynamicAdderForm : Form
    {
        #region Global Declarations

        // ====== Switched to DynamicClass ======
        private DynamicClass[] _entities = Array.Empty<DynamicClass>();
        private readonly Dictionary<string, DynamicClass[]> _selectionMap = new();
        Dictionary<string, string[]>? _mapOfCombobox = new Dictionary<string, string[]>();
        Dictionary<string, object>? _InitVal = new Dictionary<string, object>();

        /// <summary>
        /// Key convention: "Table.Column" (e.g., "PumpParameters.Model_Name").
        /// Registry of generated inputs for quick access.
        /// </summary>
        private readonly Dictionary<string, DataInput> _inputsByKey =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// ChildKey -> ParentKey mapping used for option dependencies.
        /// </summary>
        private readonly Dictionary<string, string> _parentOf =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// ChildKey -> provider(parentValue) that returns options for the child.
        /// </summary>
        private readonly Dictionary<string, Func<string?, IEnumerable<string>>> _optionProvider =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Builds a stable key for the internal registries: "Table.Column".
        /// </summary>
        private static string KeyOf(string table, string baseName) => $"{table}.{baseName}";

        private readonly ErrorProvider _errors = new ErrorProvider();

        /// <summary>
        /// Helper to fetch an input by table/column.
        /// </summary>
        private bool TryGetInput(string table, string column, out DataInput? di)
            => _inputsByKey.TryGetValue(KeyOf(table, column), out di);

        /// <summary>
        /// Enable rules registry: controllerKey -> (targetKey, predicate, clearOnDisable).
        /// </summary>
        private readonly Dictionary<string, List<(string targetKey, Func<string?, bool> enableWhen, bool clearOnDisable)>> _enableRules
            = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Cached options for dropdowns by key.
        /// </summary>
        private readonly Dictionary<string, string[]> _optionsCache = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Set of keys which are dropdown controls (used to decide how to clear).
        /// </summary>
        private readonly HashSet<string> _dropDownKeys = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Require rules registry: controllerKey -> (targetKey, predicate).
        /// </summary>
        private readonly Dictionary<string, List<(string targetKey, Func<string?, bool> requireWhen)>> _requireRules
            = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Dynamic required set updated by rules at runtime.
        /// </summary>
        private readonly HashSet<string> _dynamicRequired
            = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Per-control FK WHERE provider registry (key = "Table.Column").
        /// </summary>
        private readonly Dictionary<string, Func<string?>> _foreignKeyWhere =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Per-control FK parameters provider registry (key = "Table.Column").
        /// </summary>
        private readonly Dictionary<string, Func<Dictionary<string, object?>>> _foreignKeyParams =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Enumerates all input keys that belong to a given table.
        /// </summary>
        public IEnumerable<string> KeysOfTable(string table)
            => _inputsByKey.Keys.Where(k => k.StartsWith(table + ".", StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// Raised after successful save (legacy field-style delegate).
        /// Prefer converting this to a C# event if you change signatures later.
        /// </summary>
        public EventHandler<EventArgs> Save_Success;

        // Shared dynamic classes used for UI configuration data.
        DynamicClass ComboBoxValues = new DynamicClass("dbo", "ComboBoxValues");
        DynamicClass Column_Permissions = new DynamicClass("dbo", "Column_Permissions");

        #endregion

        #region ====== Constructors ======

        /// <summary>
        /// Constructs a form that immediately builds inputs for the provided entities.
        /// </summary>
        /// <param name="title">Form title suffix appended to header label.</param>
        /// <param name="entities">One or more <see cref="DynamicClass"/> instances to render.</param>
        /// <param name="MapOfCombobox">Static dropdown options by column name.</param>
        /// <param name="InitVal">Initial values by column name to override defaults.</param>
        /// <param name="autoBuild">If true, builds inputs during construction.</param>
        public DynamicAdderForm(string title, DynamicClass[] entities, Dictionary<string, string[]>? MapOfCombobox = null, Dictionary<string, object?>? InitVal = null, bool autoBuild = true)
        {
            InitializeComponent();
            InitCommon(title);
            EnableDoubleBuffer(this, true);
            EnableDoubleBuffer(PanelHolder, true);
            EnableDoubleBuffer(PanelSelection, true);

            if (entities == null || entities.Length == 0)
            {
                MessageBox.Show("No items to show!");
                Close();
                return;
            }

            _mapOfCombobox = MapOfCombobox;
            _InitVal = InitVal;
            PanelSelection.Visible = false;
            _entities = entities;

            if (autoBuild)
                BuildInputs(_entities, Get_mapOfCombobox());
        }

        /// <summary>
        /// Explicitly (re)builds the UI for the last provided entities and combobox map.
        /// </summary>
        public void BuildNow() => BuildInputs(_entities, Get_mapOfCombobox());

        /// <summary>
        /// Constructs a form with a top selection combo that switches between groups of entities.
        /// </summary>
        /// <param name="title">Form title suffix appended to header label.</param>
        /// <param name="selectionTitle">Title shown above the selection combobox.</param>
        /// <param name="selectionMap">Map of selection name to entity array.</param>
        public DynamicAdderForm(string title, string selectionTitle, Dictionary<string, DynamicClass[]> selectionMap)
        {
            InitializeComponent();
            this.DoubleBuffered = true;
            InitCommon(title);

            LabelSelection.Text = selectionTitle;

            if (selectionMap == null || selectionMap.Count == 0)
            {
                MessageBox.Show("No items to show!");
                Close();
                return;
            }

            PanelSelection.Visible = true;

            comboBoxSelection.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxSelection.SelectedIndexChanged -= comboBoxSelection_SelectedIndexChanged;
            comboBoxSelection.Items.Clear();
            comboBoxSelection.Items.Add("--Select--");

            _selectionMap.Clear();
            foreach (var kv in selectionMap)
            {
                _selectionMap[kv.Key] = kv.Value ?? Array.Empty<DynamicClass>();
                comboBoxSelection.Items.Add(kv.Key);
            }

            comboBoxSelection.SelectedIndex = 0;
            comboBoxSelection.SelectedIndexChanged += comboBoxSelection_SelectedIndexChanged;
        }

        #region ---------- flicker-free ----------

        /// <summary>
        /// Enables whole-window double buffering via WS_EX_COMPOSITED to reduce flicker.
        /// </summary>
        protected override CreateParams CreateParams
        {
            get
            {
                const int WS_EX_COMPOSITED = 0x02000000;
                var cp = base.CreateParams;
                cp.ExStyle |= WS_EX_COMPOSITED;
                return cp;
            }
        }

        /// <summary>
        /// Turns on private DoubleBuffered flag on controls using reflection.
        /// </summary>
        private void EnableDoubleBuffer(Control c, bool enable = true)
        {
            var prop = typeof(Control).GetProperty("DoubleBuffered",
                BindingFlags.Instance | BindingFlags.NonPublic);
            prop?.SetValue(c, enable, null);
        }

        /// <summary>
        /// Common visual init for header/selection panels and title binding.
        /// </summary>
        private void InitCommon(string title)
        {
            LabelHeading.Text += title ?? string.Empty;
            EnableDoubleBuffer(this, true);
            EnableDoubleBuffer(PanelHolder, true);
            EnableDoubleBuffer(PanelSelection, true);
            PanelSelection.Visible = false; // constructors decide
        }
        #endregion

        #endregion

        #region ---------- Event Handler (RegisterDependencyFunctions) ----------

        /// <summary>
        /// Handles top selection change; rebuilds entity inputs for the chosen group.
        /// </summary>
        private void comboBoxSelection_SelectedIndexChanged(object? sender, EventArgs e)
        {
            var selection = comboBoxSelection.SelectedItem as string;

            using (new RedrawScope(PanelHolder))
            {
                PanelHolder.SuspendLayout();
                try
                {
                    PanelHolder.Controls.Clear();

                    if (!string.Equals(selection, "--Select--", StringComparison.Ordinal)
                        && selection != null
                        && _selectionMap.TryGetValue(selection, out var entities))
                    {
                        BuildInputs(entities ?? Array.Empty<DynamicClass>(), Get_mapOfCombobox());
                    }
                }
                finally
                {
                    PanelHolder.ResumeLayout(true);
                }
            }
        }

        /// <summary>
        /// Returns the current static map of combobox options.
        /// </summary>
        private Dictionary<string, string[]>? Get_mapOfCombobox()
        {
            return _mapOfCombobox;
        }

        /// <summary>
        /// Registers a parent?child options dependency. When parent changes, child options are repopulated.
        /// </summary>
        public void RegisterDependency(
            string parentKey,
            string childKey,
            Func<string?, IEnumerable<string>> optionsProvider)
        {
            if (string.IsNullOrWhiteSpace(parentKey) ||
                string.IsNullOrWhiteSpace(childKey) ||
                optionsProvider == null) return;

            _parentOf[childKey] = parentKey;
            _optionProvider[childKey] = optionsProvider;

            // If both controls exist, populate immediately.
            if (_inputsByKey.TryGetValue(parentKey, out var parentDi) &&
                _inputsByKey.TryGetValue(childKey, out var childDi))
            {
                var parentVal = parentDi.Value?.ToString();
                var opts = optionsProvider(parentVal);
                childDi.SetOptions(opts, select: null);
            }
        }

        /// <summary>
        /// Registers required-status rules: a controller makes one or more targets required based on a predicate.
        /// </summary>
        public void RegisterRequireRule(
            string controllerKey,
            IEnumerable<string> targetKeys,
            Func<string?, bool> requireWhen)
        {
            if (!_requireRules.TryGetValue(controllerKey, out var list))
                _requireRules[controllerKey] = list = new();

            foreach (var tk in targetKeys)
                list.Add((tk, requireWhen));

            // If controller & targets exist already, apply immediately.
            if (_inputsByKey.TryGetValue(controllerKey, out var ctrl))
            {
                var v = ctrl.Value?.ToString();
                foreach (var (targetKey, rw) in list)
                    ApplyRequire(targetKey, rw(v));
            }
        }

        /// <summary>
        /// Applies (un)required status to a target input and tracks it in <see cref="_dynamicRequired"/>.
        /// </summary>
        private void ApplyRequire(string targetKey, bool makeRequired)
        {
            if (makeRequired)
                _dynamicRequired.Add(targetKey);
            else
                _dynamicRequired.Remove(targetKey);

            if (_inputsByKey.TryGetValue(targetKey, out var di))
            {
                if (makeRequired)
                    di.set_Required();
                else
                    di.unset_Required();
            }
        }

        /// <summary>
        /// Registers a static FK filter (WHERE and parameters) for a dropdown bound to a foreign key.
        /// </summary>
        public void RegisterForeignKeyFilter(string table, string column,
            string whereSql,
            Dictionary<string, object?> parameters)
        {
            string key = KeyOf(table, column);
            _foreignKeyWhere[key] = () => { return whereSql; };
            _foreignKeyParams[key] = () => { return parameters; };
        }

        /// <summary>
        /// Registers a dynamic FK filter (providers invoked at population time).
        /// </summary>
        public void RegisterForeignKeyFilterDynamic(string table, string column,
            Func<string?> whereSqlProvider,
            Func<Dictionary<string, object?>>? parametersProvider = null)
        {
            var key = KeyOf(table, column);
            _foreignKeyWhere[key] = whereSqlProvider;
            if (parametersProvider != null)
                _foreignKeyParams[key] = parametersProvider;
        }

        /// <summary>
        /// Registers N paired parent?child rewrite rules using a common transform.
        /// </summary>
        public void RegisterDataRewriteDependencies(
            IReadOnlyList<string> parentKeys,
            IReadOnlyList<string> childKeys,
            Func<string?, string?> transform)
        {
            if (parentKeys == null || childKeys == null) return;
            if (parentKeys.Count != childKeys.Count)
                throw new ArgumentException("parentKeys and childKeys must be same length.");

            for (int i = 0; i < parentKeys.Count; i++)
            {
                var pKey = parentKeys[i];
                var cKey = childKeys[i];
                RegisterDataRewriteDependency(pKey, cKey, transform);
            }
        }

        /// <summary>
        /// Registers N paired parent?child rewrite rules using an index-aware transform.
        /// </summary>
        public void RegisterDataRewriteDependencies(
            IReadOnlyList<string> parentKeys,
            IReadOnlyList<string> childKeys,
            Func<string?, int, string?> transformByIndex)
        {
            if (parentKeys == null || childKeys == null) return;
            if (parentKeys.Count != childKeys.Count)
                throw new ArgumentException("parentKeys and childKeys must be same length.");

            for (int i = 0; i < parentKeys.Count; i++)
            {
                int idx = i;
                var pKey = parentKeys[idx];
                var cKey = childKeys[idx];

                RegisterDataRewriteDependency(
                    pKey,
                    cKey,
                    parentValue => transformByIndex(parentValue, idx));
            }
        }

        /// <summary>
        /// Registers N paired parent?child rewrite rules using a key-aware transform.
        /// </summary>
        public void RegisterDataRewriteDependencies(
            IReadOnlyList<string> parentKeys,
            IReadOnlyList<string> childKeys,
            Func<string?, string /*parentKey*/, string /*childKey*/, string?> transformByKey)
        {
            if (parentKeys == null || childKeys == null) return;
            if (parentKeys.Count != childKeys.Count)
                throw new ArgumentException("parentKeys and childKeys must be same length.");

            for (int i = 0; i < parentKeys.Count; i++)
            {
                var pKey = parentKeys[i];
                var cKey = childKeys[i];

                RegisterDataRewriteDependency(
                    pKey,
                    cKey,
                    parentValue => transformByKey(parentValue, pKey, cKey));
            }
        }

        /// <summary>
        /// Registers aggregate rewrite rules where each child is rewritten from a snapshot of multiple parent values.
        /// Optionally disables children to make them read-only mirrors.
        /// </summary>
        public void RegisterAggregatedRewriteDependenciesForEachChild(
            IReadOnlyList<string> parentKeys,
            IReadOnlyList<string> childKeys,
            Func<IReadOnlyDictionary<string, string?>, string /*childKey*/, string?> transform,
            bool disableChildren = true)
        {
            if (parentKeys == null || parentKeys.Count == 0) return;
            if (childKeys == null || childKeys.Count == 0) return;

            IReadOnlyDictionary<string, string?> Snapshot()
            {
                var map = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                foreach (var pk in parentKeys)
                {
                    if (_inputsByKey.TryGetValue(pk, out var di))
                        map[pk] = di?.Value?.ToString();
                    else
                        map[pk] = null;
                }
                return map;
            }

            if (disableChildren)
            {
                foreach (var pk in parentKeys)
                {
                    foreach (var ck in childKeys)
                        RegisterEnableRule(controllerKey: pk, targetKey: ck, enableWhen: _ => false);
                }
            }

            var current = Snapshot();
            foreach (var ck in childKeys)
            {
                if (_inputsByKey.TryGetValue(ck, out var childDi))
                    ApplyRewrite(childDi, transform(current, ck));
            }

            foreach (var pk in parentKeys)
            {
                if (_inputsByKey.TryGetValue(pk, out var parentDi))
                {
                    parentDi.ValueChanged += (_, __) =>
                    {
                        var snap = Snapshot();
                        foreach (var ck in childKeys)
                        {
                            if (_inputsByKey.TryGetValue(ck, out var childDi))
                                ApplyRewrite(childDi, transform(snap, ck) ?? "null here");
                        }
                    };
                }
            }
        }

        /// <summary>
        /// Registers a single parent?child data rewrite rule using a transform for the child value.
        /// </summary>
        public void RegisterDataRewriteDependency(
            string parentKey,
            string childKey,
            Func<string?, string?> dataRewriteFunction)
        {
            if (string.IsNullOrWhiteSpace(parentKey) ||
                string.IsNullOrWhiteSpace(childKey) ||
                dataRewriteFunction == null) return;

            if (_inputsByKey.TryGetValue(parentKey, out var parentDi) &&
                _inputsByKey.TryGetValue(childKey, out var childDi))
            {
                ApplyRewrite(childDi, dataRewriteFunction(parentDi.Value?.ToString()));
            }

            if (_inputsByKey.TryGetValue(parentKey, out var parentControl))
            {
                parentControl.ValueChanged += (_, __) =>
                {
                    if (_inputsByKey.TryGetValue(childKey, out var childControl))
                        ApplyRewrite(childControl, dataRewriteFunction(parentControl.Value?.ToString()));
                };
            }
        }

        /// <summary>
        /// Applies a rewritten value to a target input, respecting the editor type for correct coercion.
        /// </summary>
        private void ApplyRewrite(DataInput target, string? rewritten)
        {
            switch (target.GetEditorType())
            {
                case "ComboBox":
                    {
                        var opts = string.IsNullOrWhiteSpace(rewritten)
                            ? new[] { "--Select--" }
                            : new[] { "--Select--", rewritten };

                        target.SetOptions(opts, select: rewritten);
                    }
                    break;

                case "TextBox":
                case "MaskedTextBox":
                    target.Value = rewritten ?? string.Empty;
                    break;

                case "NumericUpDown":
                    {
                        if (decimal.TryParse(rewritten, out var d))
                            target.Value = d;
                        else
                            target.Value = null;
                    }
                    break;

                default:
                    // Fallback: let DataInput handle conversion
                    target.Value = rewritten;
                    break;
            }
        }

        /// <summary>
        /// Resolves the FK WHERE and parameters providers (if any) for a given child key.
        /// </summary>
        private (string? where, Dictionary<string, object?>? parms) GetFkFilter(string childKey)
        {
            string? where = null;
            Dictionary<string, object?>? parms = null;

            if (_foreignKeyWhere.Keys.Contains(childKey) || _foreignKeyParams.Keys.Contains(childKey))
            {
                if (_foreignKeyWhere.TryGetValue(childKey, out var wfn))
                    where = wfn?.Invoke();

                if (_foreignKeyParams.TryGetValue(childKey, out var pfn))
                    parms = pfn?.Invoke();
            }

            return (where, parms);
        }

        #endregion

        #region ---------- Core: Build Inputs from DynamicClass columns ----------

        /// <summary>
        /// Creates a header panel container for a table section.
        /// </summary>
        private Panel MakeHeaderPanel()
        {
            return new Panel
            {
                Padding = new Padding(20),
                Dock = DockStyle.Top,
                BackColor = System.Drawing.Color.Transparent
            };
        }

        /// <summary>
        /// Creates a header label showing the verbose table name (fallback to table name).
        /// </summary>
        private Label MakeHeaderLabel(DynamicClass dyn)
        {
            return new Label
            {
                Text =  dyn.GetTableDisplayName(),
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                Font = LabelHeading.Font,
                BackColor = System.Drawing.Color.Transparent
            };
        }

        /// <summary>
        /// Creates a grid panel with the given column count for compact layout.
        /// </summary>
        private ExtendedPanel MakeDecimalGrid(int I)
        {
            return new ExtendedPanel
            {
                Dock = DockStyle.Top,
                Padding = new Padding(0),
                Margin = new Padding(0),
                DisplayMode = DisplayMode.Grid,
                GridAutoColumnWidth = false,
                GridAutoRowHeight = true,
                GridColumnCount = I,
                GridColumnGap = 10,
                GridRowCount = 0,
                GridRowGap = 10,
                CornerRadius = 0,
                BackColor = System.Drawing.Color.White
            };
        }

        /// <summary>
        /// Calculates a panel height based on number of children and grid geometry.
        /// </summary>
        private int CalculatePanelHeight(ExtendedPanel GridPanel)
        {
            int count = GridPanel.Controls.Count;
            int rows = (int)Math.Ceiling((decimal)count / GridPanel.GridColumnCount);
            int height = rows > 0 ? rows * 100 + (rows - 1) * GridPanel.GridRowGap : 0;

            return height;
        }

        /// <summary>
        /// Picks a suitable grid column count based on item count for good packing.
        /// </summary>
        public int GetColumnCount(int I)
        {
            int columnCount = 0;

            if (I > 0)
            {
                if (I < 10)
                {
                    columnCount = I;
                }
                else
                {
                    int smallestRemainder = int.MaxValue;
                    int selectedDivisor = -1;

                    for (int divisor = 5; divisor <= 10; divisor++)
                    {
                        int remainder = I % divisor;

                        if (remainder < smallestRemainder || (remainder == smallestRemainder && divisor > selectedDivisor))
                        {
                            smallestRemainder = remainder;
                            selectedDivisor = divisor;
                        }
                    }

                    columnCount = selectedDivisor;
                }
            }

            return columnCount;
        }

        /// <summary>
        /// Builds the full set of inputs for all provided entities, grouping by editor type.
        /// Includes validation flags, options, defaults, dependencies and events.
        /// </summary>
        private void BuildInputs(DynamicClass[] entities, Dictionary<string, string[]>? _mapOfCombobox)
        {
            using (new RedrawScope(PanelHolder))
            {
                PanelHolder.SuspendLayout();
                try
                {
                    PanelHolder.Controls.Clear();

                    var ControlArray = new List<Control>();

                    foreach (var dyn in entities)
                    {
                        if (dyn == null) continue;

                        #region header
                        var headerPanel = MakeHeaderPanel();
                        var headerLabel = MakeHeaderLabel(dyn);
                        headerPanel.SuspendLayout();
                        headerPanel.Controls.Add(headerLabel);
                        headerPanel.ResumeLayout(false);
                        #endregion

                        #region grids
                        var DropDownPanel = MakeDecimalGrid(6);
                        var decimalPanel = MakeDecimalGrid(10);
                        var TextPanel = MakeDecimalGrid(6);

                        EnableDoubleBuffer(decimalPanel, true);
                        decimalPanel.SuspendLayout();
                        DropDownPanel.SuspendLayout();
                        TextPanel.SuspendLayout();
                        #endregion

                        List<Control> nonDecimalStack = new List<Control>();
                        List<DynamicClass.ColumnInfo> cols = dyn.GetColumns() ?? new List<DynamicClass.ColumnInfo>();

                        foreach (var c in cols)
                        {
                            bool _isDrop = false;
                            if (SqlHealper.ShouldSkipColumn(dyn, c, Column_Permissions)) continue;

                            // Column metadata and flags
                            var baseName = c.Name;
                            var flags = SqlHealper.GetFlags(dyn.Table, baseName, Column_Permissions);
                            var effType = SqlHealper.MapSqlToType(c);
                            DataInput di;

                            _InitVal ??= new Dictionary<string, object>();

                            if (_InitVal.TryGetValue(baseName, out object? value) && value is not null)
                            {
                                c.DefaultValue = value;
                            }

                            // Dropdown building
                            var items_dd = new List<string> { "--Select--" };

                            if (flags.HasFlag(PropertyUiFlags.Dropdown) || c.IsForeignKey || c.HasOptions || (_mapOfCombobox?.ContainsKey(c.Name) ?? false))
                            {
                                string type = string.Empty;
                                if (flags.HasFlag(PropertyUiFlags.Dropdown))
                                {
                                    type = SqlHealper.GetDropDownType(dyn.Table, baseName, Column_Permissions);
                                }

                                if (c.IsForeignKey)
                                {
                                    var childKey = KeyOf(dyn.Table, baseName);
                                    var (where, parms) = GetFkFilter(childKey);
                                    var values = SqlHealper.Evalute_Foreign_Key_Values(
                                        c.ReferencedTable!,
                                        c.ReferencedColumn!,
                                        where,
                                        parms
                                    );

                                    items_dd.AddRange(values);
                                }
                                else if (c.HasOptions)
                                {
                                    items_dd.AddRange(c.Options);
                                }
                                else if (!string.IsNullOrEmpty(type))
                                {
                                    if (type == "Database_Stored_Values")
                                    {
                                        var parameters = new Dictionary<string, object?>
                                        {
                                            { "@Coloum", baseName },
                                            { "@Table", dyn.Table }
                                        };

                                        var dt = ComboBoxValues.Select(
                                            "Coloum_Name=@Coloum AND Table_Name=@Table",
                                            parameters,
                                            orderBy: "Iteam_Name"
                                        );

                                        if (dt != null)
                                        {
                                            foreach (DataRow row in dt.Rows)
                                            {
                                                if (row["Iteam_Name"] != DBNull.Value)
                                                    items_dd.Add(row["Iteam_Name"].ToString()!);
                                            }
                                        }
                                    }
                                    else if (type == "Table->Column_Values")
                                    {
                                        items_dd.AddRange(SqlHealper.Evalute_Coloum_Values(dyn.Table, baseName, Column_Permissions));
                                    }
                                    else if (type == "Table->Columns")
                                    {
                                        items_dd.AddRange(SqlHealper.Evalute_Coloum_Names(dyn.Table, baseName, Column_Permissions));
                                    }
                                }
                                else if (_mapOfCombobox != null && _mapOfCombobox.TryGetValue(baseName, out var mappedValues))
                                {
                                    items_dd.AddRange(mappedValues);
                                }

                                di = new DataInput(baseName, items_dd.ToArray(), initialValue: c.DefaultValue?.ToString(), c.DisplayName);
                                _isDrop = true;
                            }
                            else
                            {
                                di = new DataInput(baseName, effType, initialValue: c.DefaultValue, c.DisplayName)
                                {
                                    Margin = new Padding(0)
                                };
                            }

                            var key = KeyOf(dyn.Table, baseName);

                            _inputsByKey[key] = di;

                            if (_isDrop)
                            {
                                _dropDownKeys.Add(key);
                                _optionsCache[key] = items_dd.ToArray();
                            }

                            if (flags.HasFlag(PropertyUiFlags.Required) || c.IsForeignKey || !c.Nullable)
                                di.set_Required();

                            if (flags.HasFlag(PropertyUiFlags.ReadOnly) || flags.HasFlag(PropertyUiFlags.Disabled) || (c.IsPrimaryKey && _InitVal.ContainsKey(baseName)))
                                di.Enabled = false;

                            di.ValueChanged += (_, __) =>
                            {
                                var changedKey = KeyOf(dyn.Table, baseName);

                                // Pump child options when parent changes
                                foreach (var kv in _parentOf.Where(p => p.Value.Equals(changedKey, StringComparison.OrdinalIgnoreCase)).ToList())
                                {
                                    var childKey = kv.Key;

                                    if (_inputsByKey.TryGetValue(childKey, out var childDi)
                                        && _optionProvider.TryGetValue(childKey, out var provider))
                                    {
                                        var parentValue = di.Value?.ToString();
                                        var newOptions = provider(parentValue) ?? Enumerable.Empty<string>();
                                        var arr = newOptions.ToArray();
                                        childDi.SetOptions(arr, select: null);
                                        _optionsCache[childKey] = arr;
                                    }
                                }

                                OnControlValueChanged(changedKey, di.Value);
                            };

                            // Layout target panels
                            if (effType == typeof(decimal) || effType == typeof(decimal?) || effType == typeof(bool?))
                            {
                                di.Dock = DockStyle.None;
                                di.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
                                decimalPanel.Controls.Add(di);
                            }
                            else if (effType == typeof(string) && _isDrop)
                            {
                                di.Dock = DockStyle.None;
                                di.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
                                DropDownPanel.Controls.Add(di);
                            }
                            else if (effType == typeof(string))
                            {
                                di.Dock = DockStyle.None;
                                di.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
                                TextPanel.Controls.Add(di);
                            }
                            else
                            {
                                di.Dock = DockStyle.Top;
                                nonDecimalStack.Add(di);
                            }
                        }

                        // Compute columns & heights
                        decimalPanel.GridColumnCount = GetColumnCount(decimalPanel.Controls.Count);
                        DropDownPanel.GridColumnCount = GetColumnCount(DropDownPanel.Controls.Count);
                        TextPanel.GridColumnCount = GetColumnCount(TextPanel.Controls.Count);

                        decimalPanel.Height = CalculatePanelHeight(decimalPanel);
                        DropDownPanel.Height = CalculatePanelHeight(DropDownPanel);
                        TextPanel.Height = CalculatePanelHeight(TextPanel);

                        DropDownPanel.ResumeLayout(true);
                        decimalPanel.ResumeLayout(true);
                        TextPanel.ResumeLayout(true);

                        ControlArray.Add(headerPanel);
                        ControlArray.AddRange(nonDecimalStack);
                        ControlArray.Add(TextPanel);
                        ControlArray.Add(DropDownPanel);
                        ControlArray.Add(decimalPanel);
                    }

                    for (int i = 1; i <= ControlArray.Count; i++)
                        PanelHolder.Controls.Add(ControlArray[i - 1]);
                }
                finally
                {
                    PanelHolder.ResumeLayout(true);
                }
            }

            // Optional: trigger any post-build initial logic
            if (_inputsByKey.TryGetValue("Column_Permissions.Dropdown_Type", out var ddType))
            {
                OnControlValueChanged("Column_Permissions.Dropdown_Type", ddType.Value);
            }
        }
        #endregion

        #region ---------- Helpers: name flag parsing & skip rules ----------

        /// <summary>
        /// Registers an enable rule: a controller toggles a target enabled/disabled with optional clearing when disabled.
        /// </summary>
        public void RegisterEnableRule(
            string controllerKey,
            string targetKey,
            Func<string?, bool> enableWhen,
            bool clearOnDisable = true)
        {
            if (!_enableRules.TryGetValue(controllerKey, out var list))
                _enableRules[controllerKey] = list = new();

            list.Add((targetKey, enableWhen, clearOnDisable));

            if (_inputsByKey.TryGetValue(controllerKey, out var ctrl)
                && _inputsByKey.TryGetValue(targetKey, out var tgt))
            {
                var val = ctrl.Value?.ToString();
                var on = enableWhen(val);
                ApplyEnable(targetKey, on, clearOnDisable);
            }
        }

        /// <summary>
        /// Applies enable/disable to a target control and clears its value if configured to do so.
        /// </summary>
        private void ApplyEnable(string targetKey, bool enable, bool clearOnDisable)
        {
            if (_inputsByKey.TryGetValue(targetKey, out var di))
            {
                di.Enabled = enable;

                if (!enable && clearOnDisable)
                {
                    if (_dropDownKeys.Contains(targetKey))
                    {
                        di.Value = "--Select--";
                    }
                    else
                    {
                        di.Value = null;
                    }
                }
            }
        }

        #endregion

        #region --- Hook: whenever any control changes, apply enable/require rules for its dependents ---

        /// <summary>
        /// Central change hook: when any input changes, this updates children options, enable rules, and require rules.
        /// </summary>
        private void OnControlValueChanged(string changedKey, object? newValue)
        {
            // Options dependency
            foreach (var kv in _parentOf.Where(p => p.Value.Equals(changedKey, StringComparison.OrdinalIgnoreCase)).ToList())
            {
                var childKey = kv.Key;
                if (_inputsByKey.TryGetValue(childKey, out var childDi)
                    && _optionProvider.TryGetValue(childKey, out var provider))
                {
                    var parentValue = newValue?.ToString();
                    var newOptions = provider(parentValue) ?? Enumerable.Empty<string>();
                    childDi.SetOptions(newOptions, select: null);
                }
            }

            // Enable rules
            if (_enableRules.TryGetValue(changedKey, out var rules))
            {
                var v = newValue?.ToString();
                foreach (var rule in rules)
                {
                    var on = rule.enableWhen(v);
                    ApplyEnable(rule.targetKey, on, rule.clearOnDisable);
                }
            }

            // Require rules
            if (_requireRules.TryGetValue(changedKey, out var rr))
            {
                var v = newValue?.ToString();
                foreach (var (targetKey, requireWhen) in rr)
                    ApplyRequire(targetKey, requireWhen(v));
            }
        }

        #endregion

        #region ---------- Redraw scope helper ----------

        /// <summary>
        /// Disables/enables redraw for a control scope using WM_SETREDRAW to reduce flicker during large updates.
        /// </summary>
        private sealed class RedrawScope : IDisposable
        {
            private const int WM_SETREDRAW = 0x000B;
            private readonly Control _c;

            [DllImport("user32.dll")]
            private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

            public RedrawScope(Control c)
            {
                _c = c;
                if (c.IsHandleCreated)
                {
                    SendMessage(c.Handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
                }
            }

            public void Dispose()
            {
                if (_c.IsHandleCreated)
                {
                    SendMessage(_c.Handle, WM_SETREDRAW, new IntPtr(1), IntPtr.Zero);
                    _c.Invalidate(true);
                    _c.Update();
                }
            }
        }

        #endregion

        #region --- Save ---

        /// <summary>
        /// Validates all entities, gathers values, and persists them via <see cref="DynamicClass.Save"/>.
        /// Shows a summary of validation errors if any and closes on success.
        /// </summary>
        private void ButtonSave_Click(object sender, EventArgs e)
        {
            ButtonSave.Enabled = false;
            Cursor = Cursors.WaitCursor;
            try
            {
                var allErrors = new List<string>();
                var perEntityValues = new List<(DynamicClass dyn, Dictionary<string, object?> vals)>();

                foreach (var dyn in _entities)
                {
                    if (dyn == null) continue;

                    if (!TryCollectEntityValues(dyn, out var vals, out var errs))
                    {
                        allErrors.AddRange(errs);
                        continue;
                    }
                    perEntityValues.Add((dyn, vals));
                }

                if (allErrors.Count > 0)
                {
                    var msg = "Please fix the following:\n• " + string.Join("\n• ", allErrors);
                    MessageBox.Show(this, msg, "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                int saved = 0;

                foreach (var (dyn, vals) in perEntityValues)
                {
                    dyn.Values.Clear();

                    foreach (var kv in vals)
                    {
                        dyn.Values[kv.Key] = kv.Value;
                    }
                    var id = dyn.Save();
                    saved++;
                }

                MessageBox.Show(this, $"Saved record(s) successfully.", "Success",
                                MessageBoxButtons.OK, MessageBoxIcon.Information);

                Save_Success?.Invoke(sender, e);
                this.Close();

            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Save failed: " + ex.Message, "Error",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
                ButtonSave.Enabled = true;
            }
        }

        /// <summary>
        /// Attempts to collect and convert values for the given entity from the current UI.
        /// Skips hidden fields and the "Id" column. Applies required rules and type conversion.
        /// </summary>
        /// <param name="dyn">The target <see cref="DynamicClass"/>.</param>
        /// <param name="values">Output dictionary of converted values.</param>
        /// <param name="errors">Validation error messages, if any.</param>
        /// <returns>True when no validation errors were found.</returns>
        private bool TryCollectEntityValues(DynamicClass dyn, out Dictionary<string, object?> values, out List<string> errors)
        {
            values = new(StringComparer.OrdinalIgnoreCase);
            errors = new();

            var cols = dyn.GetColumns() ?? new List<DynamicClass.ColumnInfo>();

            foreach (var c in cols)
            {
                var column = c.Name;

                if (string.Equals(column, "Id", StringComparison.OrdinalIgnoreCase))
                    continue;

                var flags = GetFlags(dyn.Table, column, Column_Permissions);

                if (flags.HasFlag(PropertyUiFlags.Hidden))
                    continue;

                if (!TryGetInput(dyn.Table, column, out var di))
                    continue;

                var raw = di?.Value;

                if (raw is string s && s.Trim() == "--Select--")
                    raw = null;

                var key = KeyOf(dyn.Table, column);
                bool isRequired = flags.HasFlag(PropertyUiFlags.Required) || _dynamicRequired.Contains(key);

                if (isRequired)
                {
                    var isEmpty = raw is null || (raw is string str && string.IsNullOrWhiteSpace(str));
                    if (isEmpty)
                    {
                        errors.Add($"{(string.IsNullOrWhiteSpace(c.DisplayName) ? c.Name : c.DisplayName)} is required.");
                        _errors.SetError(di, "Required");
                        continue;
                    }
                }

                _errors.SetError(di, "");

                var targetType = MapSqlToType(c);
                object? converted = null;

                try
                {
                    converted = ConvertTo(targetType, raw);
                }
                catch (Exception ex)
                {
                    errors.Add($"Invalid value for {column}: {ex.Message}");
                    _errors.SetError(di, ex.Message);
                    continue;
                }

                if (converted is null) continue;
                values[column] = converted;
            }
            return errors.Count == 0;
        }
        #endregion

        /// <summary>
        /// Closes the dialog without saving.
        /// </summary>
        private void buttonCancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}

