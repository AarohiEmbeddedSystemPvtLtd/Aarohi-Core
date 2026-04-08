using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Linq.Expressions;
using System.Windows.Forms;
using Aarohi.Classes.Utils;
using Newtonsoft.Json.Linq;

#region Public API

public sealed class SectionDesign
{
    public Dictionary<string, SectionStyle> Legend { get; } =
        new Dictionary<string, SectionStyle>(StringComparer.OrdinalIgnoreCase);

    public HashSet<string>? IncludeSections { get; set; }
    public bool UseSystemSelectionHighlight { get; set; } = true;
    public List<string>? LegendOrder { get; set; }
}

public sealed class SectionStyle
{
    public string Symbol { get; set; } = "";
    public Color Foreground { get; set; } = Color.Empty;
    public Color Background { get; set; } = Color.Empty;
}

#endregion

public sealed class ExtendedTreeViewBuilderForPLCTags : IDisposable
{
    private readonly TreeView _tree;
    private readonly SectionDesign? _design;
    private readonly float _fontSize;

    private readonly ContextMenuStrip _ctx;
    private readonly ToolStripMenuItem _miView;
    private readonly ToolStripMenuItem _miWrite;
    private readonly ToolStripMenuItem _miRead;

    public event Action<TreeNode>? ViewRequested;
    public event Action<(NodeMeta?, string?)>? WriteRequested;
    public event Action<NodeMeta>? ReadRequested;

    public ExtendedTreeViewBuilderForPLCTags(TreeView treeView, SectionDesign? design = null, float fontSize = 9.0f, bool Read_Write = false)
    {
        _tree = treeView ?? throw new ArgumentNullException(nameof(treeView));
        _design = design;
        _fontSize = fontSize;

        _tree.ShowLines = true;
        _tree.ShowRootLines = true;
        _tree.ShowPlusMinus = true;
        _tree.ShowNodeToolTips = true;

        if (_design != null)
        {
            _tree.DrawMode = TreeViewDrawMode.OwnerDrawText;
            _tree.HideSelection = true;
            _tree.FullRowSelect = true;

            _tree.DrawNode -= Tree_DrawNode;
            _tree.DrawNode += Tree_DrawNode;
        }
        else
        {
            _tree.DrawMode = TreeViewDrawMode.Normal;
            _tree.DrawNode -= Tree_DrawNode;
        }

        _ctx = new ContextMenuStrip { ShowImageMargin = false, AutoClose = true };
        _miView = new ToolStripMenuItem("View");
        _miView.Click += OnViewClicked;
        _ctx.Items.Add(_miView);

        if (Read_Write)
        {
            _miWrite = new ToolStripMenuItem("Write");
            _miRead = new ToolStripMenuItem("Read");
            var sap = new ToolStripSeparator();

            _miRead.Click += onReadClicked;
            _miWrite.Click += onWriteClicked;

            _ctx.Items.Add(sap);
            _ctx.Items.Add(_miWrite);
            _ctx.Items.Add(_miRead);
        }

        _tree.NodeMouseClick -= Tree_NodeMouseClick_ShowMenu;
        _tree.NodeMouseClick += Tree_NodeMouseClick_ShowMenu;
    }

    private void onWriteClicked(object? sender, EventArgs e)
    {
        (bool ok, string? val) = Inliners.InlineTextInput.ShowWithOk();
        if (!ok || _tree.SelectedNode is null) return;

        if (_tree.SelectedNode.Tag is NodeMeta meta)
        {
            WriteRequested?.Invoke((meta,val));
        }
    }


    private void onReadClicked(object? sender, EventArgs e)
    {
        if(_tree.SelectedNode is null) return;
        if(_tree.SelectedNode.Tag is NodeMeta meta)
        {
            ReadRequested?.Invoke(meta);
        }
    }

    // =========================
    // Context Menu plumbing
    // =========================
    private void Tree_NodeMouseClick_ShowMenu(object? sender, TreeNodeMouseClickEventArgs e)
    {
        if (e.Button != MouseButtons.Right) return;

        _tree.SelectedNode = e.Node;

        bool canView = CanViewNode(e.Node);
        _miView.Enabled = canView;

        _ctx.Show(_tree, e.Location);
    }

    private bool CanViewNode(TreeNode node)
    {
        if (node?.Tag is NodeMeta meta)
        {
            if (meta.IsLegendBranch) return false;
            if (meta.IsStructuralOnly) return false;
        }
        return node != null;
    }

    private void OnViewClicked(object? sender, EventArgs e)
    {
        var node = _tree.SelectedNode;
        if (node == null) return;

        ViewRequested?.Invoke(node);
        try
        {
            string raw = GetRawPathWithoutLegend(node, ".");
            string pretty = GetDisplayArrowPathWithoutLegend(node);
            string dtype = (node.Tag as NodeMeta)?.DataType ?? "";
            string info = string.IsNullOrWhiteSpace(dtype)
                ? $"{pretty}\nRaw: {raw}"
                : $"{pretty}\nType: {dtype}\nRaw: {raw}";
            MessageBox.Show(info, "View", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch
        {

        }
    }


    public void Build(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("JSON is empty.", nameof(json));

        _tree.BeginUpdate();
        try
        {
            _tree.Nodes.Clear();
            _tree.Font = new Font(_tree.Font.FontFamily, _fontSize, FontStyle.Regular);

            var root = JToken.Parse(json);
            var datablocks = root["datablocks"] as JArray;
            if (datablocks == null || datablocks.Count == 0)
            {
                _tree.Nodes.Add(new TreeNode("No datablocks found") { Name = "No datablocks found" });
                return;
            }

            if (_design != null && _design.Legend.Count > 0)
            {
                _tree.Nodes.Add(BuildLegendNode(_tree.Font, _design));
                _tree.Nodes[0].Expand();
            }

            foreach (var db in datablocks.OfType<JObject>())
            {
                int? dbNum = (int?)db["DB_number"];
                string dbName = (string?)db["DB_name"] ?? "(UnnamedDB)";
                bool optimized = (bool?)(db["Optimized_Access"]) ?? false;
                string Address = (string?)(db["Logical_Address"]) ?? string.Empty;
                string dbHeader = dbNum.HasValue ? $"DB{dbNum}: {dbName}" : dbName;

                var dbNode = new TreeNode(dbHeader)
                {
                    // IMPORTANT: Name is the RAW key you want to read in code
                    Name = dbName,
                    Tag = new NodeMeta(section: "(top)", dataType: "", name: dbName, addr: Address, design: _design),
                    NodeFont = new Font(_tree.Font.FontFamily, _fontSize, FontStyle.Bold),
                    ToolTipText = dbHeader
                };

                ApplyNodeVisualsIfAny(dbNode);
                _tree.Nodes.Add(dbNode);

                var vars = db["variables"] as JArray;
                if (vars != null)
                {
                    foreach (var v in vars.OfType<JObject>())
                        AddVariableNode(dbNode, v, parentSection: "(top)");
                }

                dbNode.Expand();
            }
        }
        finally
        {
            _tree.EndUpdate();
        }
    }

    public void Dispose()
    {
        // Clean up event handlers when you’re done with the builder
        _tree.DrawNode -= Tree_DrawNode;
    }

    #region Owner-Draw

    private void Tree_DrawNode(object? sender, DrawTreeNodeEventArgs e)
    {
        try
        {
            if (_design == null) { e.DrawDefault = true; return; }

            var tv = (TreeView)sender!;
            var meta = e.Node.Tag as NodeMeta;
            var (fore, back) = ResolvePalette(meta?.Section, tv, _design, e.State.HasFlag(TreeNodeStates.Selected));

            // Fill only from label start to the right, so +/- and lines remain visible
            var fill = new Rectangle(e.Bounds.X, e.Bounds.Y, tv.Width - e.Bounds.X, e.Bounds.Height);
            using (var bg = new SolidBrush(back))
                e.Graphics.FillRectangle(bg, fill);

            TextRenderer.DrawText(
                e.Graphics,
                e.Node.Text,
                e.Node.NodeFont ?? tv.Font,
                e.Bounds,
                fore,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            if (e.State.HasFlag(TreeNodeStates.Focused))
                ControlPaint.DrawFocusRectangle(e.Graphics, e.Bounds);

            e.DrawDefault = false;
        }
        catch
        {
            e.DrawDefault = true;
        }
    }

    private static (Color Fore, Color Back) ResolvePalette(string? section, TreeView tv, SectionDesign design, bool isSelected)
    {
        SectionStyle? style = null;
        if (!string.IsNullOrEmpty(section) && design.Legend.TryGetValue(section!, out var s))
            style = s;

        var fore = (style?.Foreground.IsEmpty == false) ? style!.Foreground : tv.ForeColor;
        var back = (style?.Background.IsEmpty == false) ? style!.Background : tv.BackColor;

        if (!isSelected) return (fore, back);

        if (design.UseSystemSelectionHighlight)
            return (SystemColors.HighlightText, SystemColors.Highlight);

        var selBack = (style?.Foreground.IsEmpty == false) ? style!.Foreground : SystemColors.Highlight;
        var selFore = GetContrastBW(selBack);
        return (selFore, selBack);
    }

    private static Color GetContrastBW(Color c)
    {
        double r = (c.R / 255.0), g = (c.G / 255.0), b = (c.B / 255.0);
        r = (r <= 0.03928) ? r / 12.92 : Math.Pow((r + 0.055) / 1.055, 2.4);
        g = (g <= 0.03928) ? g / 12.92 : Math.Pow((g + 0.055) / 1.055, 2.4);
        b = (b <= 0.03928) ? b / 12.92 : Math.Pow((b + 0.055) / 1.055, 2.4);
        double L = 0.2126 * r + 0.7152 * g + 0.0722 * b;
        return (L > 0.179) ? Color.Black : Color.White;
    }

    #endregion

    #region Node building

    private TreeNode BuildLegendNode(Font baseFont, SectionDesign design)
    {
        var legend = new TreeNode("Legend")
        {
            Name = "Legend",
            NodeFont = new Font(baseFont.FontFamily, baseFont.Size + 1, FontStyle.Bold),
            Tag = new NodeMeta(section: "(legend)", dataType: "", name: "Legend", addr: string.Empty, design: design, isLegendBranch: true) // ← mark
        };

        IEnumerable<KeyValuePair<string, SectionStyle>> items =
            (design.LegendOrder != null && design.LegendOrder.Count > 0)
            ? design.LegendOrder.Where(k => design.Legend.ContainsKey(k))
                                .Select(k => new KeyValuePair<string, SectionStyle>(k, design.Legend[k]))
            : design.Legend;

        foreach (var kv in items)
        {
            var text = string.IsNullOrWhiteSpace(kv.Value.Symbol) ? kv.Key : $"{kv.Value.Symbol}  {kv.Key}";

            var n = new TreeNode(text)
            {
                Name = kv.Key,
                Tag = new NodeMeta(section: kv.Key, dataType: "", name: kv.Key, addr: string.Empty, design: design, isLegendBranch: true), // ← mark
                NodeFont = new Font(baseFont.FontFamily, baseFont.Size, FontStyle.Regular),
                ToolTipText = kv.Key
            };
            ApplyNodeVisualsIfAny(n);
            legend.Nodes.Add(n);
        }
        return legend;
    }


    private void ApplyNodeVisualsIfAny(TreeNode node)
    {
        var meta = node.Tag as NodeMeta;
        var design = meta?.Design;
        if (design == null) return;

        if (!string.IsNullOrEmpty(meta!.Section) &&
            design.Legend.TryGetValue(meta.Section, out var style))
        {
            if (!style.Foreground.IsEmpty) node.ForeColor = style.Foreground;
            if (!style.Background.IsEmpty) node.BackColor = style.Background;
        }
    }

    private void AddVariableNode(
        TreeNode parent,
        JObject varObj,
        string parentSection)
    {
        string name = (string?)varObj["Variable_Name"] ?? "(Unnamed)";
        string dt = (string?)varObj["Data_Type"] ?? "(Unknown)";
        string la = (string?)varObj["Logical_Address"] ?? "";

        // What user sees:
        string body = string.IsNullOrWhiteSpace(la) ? $"{name} -> {dt}" : $"{name} -> {dt} ( {la} )";

        // Optional section prefix symbol
        string prefix = "";
        if (_design != null &&
            !string.IsNullOrEmpty(parentSection) &&
            _design.Legend.TryGetValue(parentSection, out var style) &&
            !string.IsNullOrWhiteSpace(style.Symbol))
        {
            prefix = style.Symbol + "  ";
        }

        var node = new TreeNode(prefix + body)
        {
            // RAW name only here:
            Name = name,
            Tag = new NodeMeta(section: parentSection, dataType: dt, addr: la, name: name, design: _design),
            ToolTipText = body
        };
        ApplyNodeVisualsIfAny(node);
        parent.Nodes.Add(node);

        var dtChildren = varObj["Datatype_Children"] as JObject;
        if (dtChildren == null) return;

        IEnumerable<(string key, JArray arr)> childSections =
            dtChildren.Properties()
                      .Where(p => p.Value is JArray ja && ja.Count > 0)
                      .Select(p => (p.Name, (JArray)p.Value));

        if (_design?.IncludeSections != null && _design.IncludeSections.Count > 0)
            childSections = childSections.Where(t => _design.IncludeSections.Contains(t.key));

        foreach (var (key, arr) in childSections)
        {
            TreeNode sectionFolder = node;

            if (_design != null && _design.Legend.TryGetValue(key, out var sectStyle))
            {
                var sectionText = string.IsNullOrWhiteSpace(sectStyle.Symbol)
                    ? key
                    : $"{sectStyle.Symbol}  {key}";

                sectionFolder = new TreeNode(sectionText)
                {
                    Name = key,
                    Tag = new NodeMeta(section: key, dataType: "", name: key,
                    addr: la,
                       design: _design,
                       isLegendBranch: false,
                       isStructuralOnly: true),
                    NodeFont = new Font(node.TreeView.Font, FontStyle.Bold),
                    ToolTipText = key
                };

                ApplyNodeVisualsIfAny(sectionFolder);
                node.Nodes.Add(sectionFolder);
            }

            foreach (var child in arr.OfType<JObject>())
                AddVariableNode(sectionFolder, child, parentSection: key);
        }
    }

    #endregion

    #region Internals

    public sealed class NodeMeta
    {
        public string Section { get; }
        public string DataType { get; }
        public string Name { get; }
        public string Address { get; }
        public SectionDesign? Design { get; }
        public bool IsLegendBranch { get; }
        public bool IsStructuralOnly { get; }

        public NodeMeta(string section, string dataType, string name, string addr,
                        SectionDesign? design,
                        bool isLegendBranch = false,
                        bool isStructuralOnly = false)
        {
            Section = section;
            DataType = dataType;
            Name = name;
            Design = design;
            IsLegendBranch = isLegendBranch;
            IsStructuralOnly = isStructuralOnly;
            Address = addr;
        }
    }


    public string GetRawPathWithoutLegend(TreeNode? node, string separator = ".")
    {
        if (node == null) return string.Empty;
        if (IsLegendOrUnderLegend(node)) return string.Empty;

        var parts = new Stack<string>();
        for (var cur = node; cur != null; cur = cur.Parent)
        {
            if (IsLegend(cur)) break;
            var token = CleanRawName(cur);
            if (!string.IsNullOrWhiteSpace(token)) parts.Push(token);
        }
        return string.Join(separator, parts);
    }

    public string GetRawPath(TreeNode? node, string separator = ".")
    {
        if (node == null) return string.Empty;
        var parts = new Stack<string>();
        for (var cur = node; cur != null; cur = cur.Parent)
        {
            if (IsLegend(cur)) break;
            var token = CleanRawName(cur);
            if (!string.IsNullOrWhiteSpace(token)) parts.Push(token);
        }
        return string.Join(separator, parts);
    }

    public string GetAddress(TreeNode? node)
    {

        if (node?.Tag is NodeMeta meta)
        {
            if (string.IsNullOrEmpty(meta.Address)) return string.Empty;
            return meta.Address;
        }

        return string.Empty;
    }

    public string GetDataType(TreeNode? node)
    {

        if (node?.Tag is NodeMeta meta)
        {
            if (string.IsNullOrEmpty(meta.DataType)) return string.Empty;
            return meta.DataType;
        }

        return string.Empty;
    }

    private static bool IsLegend(TreeNode n)
    {
        var meta = n.Tag as NodeMeta;
        return meta?.IsLegendBranch == true && string.Equals(n.Text, "Legend", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLegendOrUnderLegend(TreeNode n)
    {
        for (var cur = n; cur != null; cur = cur.Parent)
        {
            var meta = cur.Tag as NodeMeta;
            if (meta?.IsLegendBranch == true) return true;
        }
        return false;
    }

    private static string CleanRawName(TreeNode n)
    {
        if (n?.Tag is NodeMeta meta)
        {
            if (meta.IsLegendBranch) return string.Empty;      // drop legend
            if (meta.IsStructuralOnly) return string.Empty;    // drop section folders (Input/Members/etc.)
        }

        var raw = n?.Name;
        if (string.IsNullOrWhiteSpace(raw)) raw = n?.Text ?? "";

        var idx = raw.LastIndexOf('.');
        if (idx >= 0 && idx < raw.Length - 1)
            raw = raw.Substring(idx + 1);

        return raw.Trim();
    }

    public string GetDisplayArrowPathWithoutLegend(TreeNode? node)
    {
        var dot = GetRawPathWithoutLegend(node, ".");
        return string.IsNullOrEmpty(dot) ? "" : dot.Replace(".", " > ");
    }


    #endregion
}

public sealed class ExtendedTreeView : IDisposable
{
    private readonly TreeView _tree;
    private readonly float _fontSize;
    private SectionDesign _design; // always non-null after ctor (we ensure default)
    private bool _ownsHandlers;

    // ---- Generic section keys used in Legend ----
    private const string Section_Object = "Object";
    private const string Section_Array = "Array";
    private const string Section_Property = "Property";
    private const string Section_String = "String";
    private const string Section_Number = "Number";
    private const string Section_Bool = "Bool";
    private const string Section_Null = "Null";

    public ExtendedTreeView(TreeView treeView, SectionDesign? design = null, float fontSize = 9.0f)
    {
        _tree = treeView ?? throw new ArgumentNullException(nameof(treeView));
        _fontSize = fontSize;
        _design = design ?? EnsureDefaultLegend();

        _tree.ShowLines = true;
        _tree.ShowRootLines = true;
        _tree.ShowPlusMinus = true;
        _tree.ShowNodeToolTips = true;
        _tree.HideSelection = true;
        _tree.FullRowSelect = true;
        _tree.Font = new Font(_tree.Font.FontFamily, _fontSize, FontStyle.Regular);

        _tree.DrawMode = TreeViewDrawMode.OwnerDrawText;
        _tree.DrawNode += Tree_DrawNode;
        _tree.KeyDown += Tree_KeyDown;
        _ownsHandlers = true;
    }

    public void Build(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("JSON is empty.", nameof(json));

        _tree.BeginUpdate();
        try
        {
            _tree.Nodes.Clear();

            var rootToken = JToken.Parse(json);

            if (_design.Legend.Count > 0)
            {
                _tree.Nodes.Add(BuildLegendNode(_tree.Font, _design));
                _tree.Nodes[0].Expand();
            }

            var rootName = rootToken.Type switch
            {
                JTokenType.Object => "(root object)",
                JTokenType.Array => "(root array)",
                _ => "(root)"
            };

            var rootNode = new TreeNode(rootName)
            {
                Name = "$",
                Tag = new NodeMeta(section: Section_Object, dataType: rootToken.Type.ToString(), name: "$",
                                    design: _design),
                NodeFont = new Font(_tree.Font.FontFamily, _fontSize, FontStyle.Bold),
                ToolTipText = $"Root: {rootToken.Type}"
            };
            ApplyNodeVisualsIfAny(rootNode);
            _tree.Nodes.Add(rootNode);

            AddTokenNode(rootNode, rootToken, pathSoFar: "$");

            rootNode.Expand();
        }
        finally
        {
            _tree.EndUpdate();
        }
    }

    public void Dispose()
    {
        if (_ownsHandlers)
        {
            _tree.DrawNode -= Tree_DrawNode;
            _tree.KeyDown -= Tree_KeyDown;
            _ownsHandlers = false;
        }
    }

    private void Tree_DrawNode(object? sender, DrawTreeNodeEventArgs e)
    {
        try
        {
            var tv = (TreeView)sender!;
            var meta = e.Node.Tag as NodeMeta;
            var (fore, back) = ResolvePalette(meta?.Section, tv, _design, e.State.HasFlag(TreeNodeStates.Selected));

            var fill = new Rectangle(e.Bounds.X, e.Bounds.Y, tv.Width - e.Bounds.X, e.Bounds.Height);
            using (var bg = new SolidBrush(back))
                e.Graphics.FillRectangle(bg, fill);

            TextRenderer.DrawText(
                e.Graphics,
                e.Node.Text,
                e.Node.NodeFont ?? tv.Font,
                e.Bounds,
                fore,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            if (e.State.HasFlag(TreeNodeStates.Focused))
                ControlPaint.DrawFocusRectangle(e.Graphics, e.Bounds);

            e.DrawDefault = false;
        }
        catch
        {
            e.DrawDefault = true;
        }
    }

    private void Tree_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter && _tree.SelectedNode != null)
        {
            var n = _tree.SelectedNode;
            if (n.IsExpanded) n.Collapse();
            else n.Expand();
            e.Handled = true;
        }
    }

    private static (Color Fore, Color Back) ResolvePalette(string? section, TreeView tv, SectionDesign design, bool isSelected)
    {
        SectionStyle? style = null;
        if (!string.IsNullOrEmpty(section) && design.Legend.TryGetValue(section!, out var s))
            style = s;

        var fore = (style?.Foreground.IsEmpty == false) ? style!.Foreground : tv.ForeColor;
        var back = (style?.Background.IsEmpty == false) ? style!.Background : tv.BackColor;

        if (!isSelected) return (fore, back);

        if (design.UseSystemSelectionHighlight)
            return (SystemColors.HighlightText, SystemColors.Highlight);

        var selBack = (style?.Foreground.IsEmpty == false) ? style!.Foreground : SystemColors.Highlight;
        var selFore = GetContrastBW(selBack);
        return (selFore, selBack);
    }

    private static Color GetContrastBW(Color c)
    {
        double r = (c.R / 255.0), g = (c.G / 255.0), b = (c.B / 255.0);
        r = (r <= 0.03928) ? r / 12.92 : Math.Pow((r + 0.055) / 1.055, 2.4);
        g = (g <= 0.03928) ? g / 12.92 : Math.Pow((g + 0.055) / 1.055, 2.4);
        b = (b <= 0.03928) ? b / 12.92 : Math.Pow((b + 0.055) / 1.055, 2.4);
        double L = 0.2126 * r + 0.7152 * g + 0.0722 * b;
        return (L > 0.179) ? Color.Black : Color.White;
    }

    private TreeNode BuildLegendNode(Font baseFont, SectionDesign design)
    {
        var legend = new TreeNode("Legend")
        {
            Name = "Legend",
            NodeFont = new Font(baseFont.FontFamily, baseFont.Size + 1, FontStyle.Bold),
            Tag = new NodeMeta(section: "(legend)", dataType: "", name: "Legend", design: design, isLegendBranch: true)
        };

        IEnumerable<KeyValuePair<string, SectionStyle>> items =
            (design.LegendOrder != null && design.LegendOrder.Count > 0)
            ? design.LegendOrder.Where(k => design.Legend.ContainsKey(k))
                                .Select(k => new KeyValuePair<string, SectionStyle>(k, design.Legend[k]))
            : design.Legend;

        foreach (var kv in items)
        {
            var text = string.IsNullOrWhiteSpace(kv.Value.Symbol) ? kv.Key : $"{kv.Value.Symbol}  {kv.Key}";
            var n = new TreeNode(text)
            {
                Name = kv.Key,
                Tag = new NodeMeta(section: kv.Key, dataType: "", name: kv.Key, design: design, isLegendBranch: true),
                NodeFont = new Font(baseFont.FontFamily, baseFont.Size, FontStyle.Regular),
                ToolTipText = kv.Key
            };
            ApplyNodeVisualsIfAny(n);
            legend.Nodes.Add(n);
        }
        return legend;
    }

    private void ApplyNodeVisualsIfAny(TreeNode node)
    {
        if (node?.Tag is not NodeMeta meta) return;
        var design = meta.Design;
        if (design == null) return;

        if (!string.IsNullOrEmpty(meta.Section) &&
            design.Legend.TryGetValue(meta.Section, out var style))
        {
            if (!style.Foreground.IsEmpty) node.ForeColor = style.Foreground;
            if (!style.Background.IsEmpty) node.BackColor = style.Background;
        }
    }

    private SectionDesign EnsureDefaultLegend()
    {
        var d = new SectionDesign
        {
            UseSystemSelectionHighlight = true,
            LegendOrder = new List<string>
            {
                Section_Object, Section_Array, Section_Property,
                Section_String, Section_Number, Section_Bool, Section_Null
            }
        };

        d.Legend[Section_Object] = new SectionStyle { Symbol = "🗂️", Foreground = Color.FromArgb(30, 30, 30), Background = Color.FromArgb(235, 243, 255) };
        d.Legend[Section_Array] = new SectionStyle { Symbol = "📚", Foreground = Color.FromArgb(30, 30, 30), Background = Color.FromArgb(232, 245, 233) };
        d.Legend[Section_Property] = new SectionStyle { Symbol = "🏷️", Foreground = Color.FromArgb(30, 30, 30), Background = Color.FromArgb(245, 245, 245) };
        d.Legend[Section_String] = new SectionStyle { Symbol = "🔤", Foreground = Color.DarkSlateBlue, Background = Color.Empty };
        d.Legend[Section_Number] = new SectionStyle { Symbol = "🔢", Foreground = Color.DarkGreen, Background = Color.Empty };
        d.Legend[Section_Bool] = new SectionStyle { Symbol = "🔘", Foreground = Color.Firebrick, Background = Color.Empty };
        d.Legend[Section_Null] = new SectionStyle { Symbol = "∅", Foreground = Color.DimGray, Background = Color.Empty };

        return d;
    }

    private void AddTokenNode(TreeNode parent, JToken token, string pathSoFar, string? overrideName = null)
    {
        switch (token.Type)
        {
            case JTokenType.Object:
                {
                    var obj = (JObject)token;
                    var name = overrideName ?? GetLastPathToken(pathSoFar);
                    var text = $"{name} {{...}}";
                    var node = new TreeNode(DecorateWithSymbol(Section_Object, text))
                    {
                        Name = name,
                        Tag = new NodeMeta(section: Section_Object, dataType: "Object", name: name,
                                            design: _design),
                        ToolTipText = $"{pathSoFar}  (Object with {obj.Properties().Count()} properties)"
                    };
                    ApplyNodeVisualsIfAny(node);
                    parent.Nodes.Add(node);

                    foreach (var p in obj.Properties())
                    {
                        var childPath = pathSoFar + "." + EscapeIfNeeded(p.Name);

                        // Property as a structural folder (excluded from raw path)
                        var propFolder = new TreeNode(DecorateWithSymbol(Section_Property, p.Name))
                        {
                            Name = p.Name,
                            Tag = new NodeMeta(section: Section_Property, dataType: "Property", name: p.Name,
                                                design: _design, isStructuralOnly: true),
                            NodeFont = new Font(parent.TreeView.Font, FontStyle.Bold),
                            ToolTipText = $"{childPath} (Property)"
                        };
                        ApplyNodeVisualsIfAny(propFolder);
                        node.Nodes.Add(propFolder);

                        AddTokenNode(propFolder, p.Value, childPath, overrideName: p.Name);
                    }
                    break;
                }

            case JTokenType.Array:
                {
                    var arr = (JArray)token;
                    var name = overrideName ?? GetLastPathToken(pathSoFar);
                    var text = $"{name} [{arr.Count}]";
                    var node = new TreeNode(DecorateWithSymbol(Section_Array, text))
                    {
                        Name = name,
                        Tag = new NodeMeta(section: Section_Array, dataType: "Array", name: name,
                                            design: _design),
                        ToolTipText = $"{pathSoFar}  (Array length {arr.Count})"
                    };
                    ApplyNodeVisualsIfAny(node);
                    parent.Nodes.Add(node);

                    for (int i = 0; i < arr.Count; i++)
                    {
                        var idxName = $"[{i}]";
                        var childPath = $"{pathSoFar}[{i}]";

                        // Index as a structural folder (excluded from raw path)
                        var idxFolder = new TreeNode(idxName)
                        {
                            Name = idxName,
                            Tag = new NodeMeta(section: Section_Array, dataType: "Index", name: idxName,
                                                design: _design, isStructuralOnly: true),
                            ToolTipText = $"{childPath} (Index)"
                        };
                        ApplyNodeVisualsIfAny(idxFolder);
                        node.Nodes.Add(idxFolder);

                        AddTokenNode(idxFolder, arr[i], childPath, overrideName: idxName);
                    }
                    break;
                }

            case JTokenType.Integer:
            case JTokenType.Float:
                {
                    EmitValueLeaf(parent, token, pathSoFar, Section_Number, ((JValue)token).Value);
                    break;
                }
            case JTokenType.Boolean:
                {
                    EmitValueLeaf(parent, token, pathSoFar, Section_Bool, ((JValue)token).Value);
                    break;
                }
            case JTokenType.String:
                {
                    var val = ((JValue)token).Value?.ToString() ?? "";
                    var preview = val.Length > 64 ? val.Substring(0, 61) + "..." : val;
                    EmitValueLeaf(parent, token, pathSoFar, Section_String, $"\"{preview}\"");
                    break;
                }
            case JTokenType.Null:
            case JTokenType.Undefined:
                {
                    EmitValueLeaf(parent, token, pathSoFar, Section_Null, "null");
                    break;
                }
            default:
                {
                    var v = (token as JValue)?.Value;
                    var dt = token.Type.ToString();
                    EmitValueLeaf(parent, token, pathSoFar, Section_String, v, explicitType: dt);
                    break;
                }
        }
    }

    private void EmitValueLeaf(TreeNode parent, JToken token, string path, string section, object? value, string? explicitType = null)
    {
        var name = GetLastPathToken(path);
        string typeLabel = explicitType ?? token.Type.ToString();
        string leafText = $"{name} -> {typeLabel} ( {value} )";

        var node = new TreeNode(DecorateWithSymbol(section, leafText))
        {
            Name = name,
            Tag = new NodeMeta(section: section, dataType: typeLabel, name: name, design: _design),
            ToolTipText = $"{path}\r\nType: {typeLabel}\r\nValue: {value}"
        };
        ApplyNodeVisualsIfAny(node);
        parent.Nodes.Add(node);
    }

    private string DecorateWithSymbol(string section, string text)
    {
        if (_design.Legend.TryGetValue(section, out var style) && !string.IsNullOrWhiteSpace(style.Symbol))
            return $"{style.Symbol}  {text}";
        return text;
    }

    private static string GetLastPathToken(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "";
        int lastDot = path.LastIndexOf('.');
        int lastBracket = path.LastIndexOf('[');
        int cut = Math.Max(lastDot, lastBracket);
        if (cut >= 0 && cut < path.Length - 1)
            return path.Substring(cut + 1).Trim();
        return path.Trim();
    }

    private static string EscapeIfNeeded(string name)
    {
        return name.Contains(".") ? $"['{name}']" : name;
    }

    private sealed class NodeMeta
    {
        public string Section { get; }
        public string DataType { get; }
        public string Name { get; }
        public SectionDesign? Design { get; }
        public bool IsLegendBranch { get; }
        public bool IsStructuralOnly { get; } // property folders / array indices

        public NodeMeta(string section, string dataType, string name,
                        SectionDesign? design,
                        bool isLegendBranch = false,
                        bool isStructuralOnly = false)
        {
            Section = section;
            DataType = dataType;
            Name = name;
            Design = design;
            IsLegendBranch = isLegendBranch;
            IsStructuralOnly = isStructuralOnly;
        }
    }
}