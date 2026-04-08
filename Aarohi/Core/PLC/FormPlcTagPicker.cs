using Aarohi.Core.PLC;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

public sealed class FormPlcTagPicker : Form
{
    private readonly List<PlcTagInfo> _allTags;
    private bool _loading;
    private bool _editMode;

    private ComboBox cboDb = new ComboBox();
    private ComboBox cboTag = new ComboBox();

    private TextBox txtDbName = new TextBox();
    private NumericUpDown nudDbNumber = new NumericUpDown();

    private TextBox txtTagName = new TextBox();
    private ComboBox cboDataType = new ComboBox();
    private TextBox txtOffset = new TextBox();

    private TextBox txtAddress = new TextBox();
    private TextBox txtSheet = new TextBox();
    private TextBox txtByteLen = new TextBox();
    private TextBox txtWarning = new TextBox();

    private Button btnSelect = new Button();
    private Button btnEdit = new Button();
    private Button btnSave = new Button();
    private Button btnCancel = new Button();

    private Label lblEditHint = new Label();

    public PlcTagInfo? SelectedTagInfo { get; private set; }

    private PlcTagInfo? _currentTag; // current selected
    private sealed class DbItem
    {
        public string DbName { get; init; } = "";
        public int DbNumber { get; init; }
        public override string ToString()
            => string.IsNullOrWhiteSpace(DbName) ? $"DB {DbNumber}" : $"{DbName} (DB {DbNumber})";
    }

    public FormPlcTagPicker(IEnumerable<PlcTagInfo> tags)
    {
        _allTags = (tags ?? Enumerable.Empty<PlcTagInfo>()).ToList();

        Text = "PLC Tag Picker";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Width = 820;
        Height = 600;

        BuildUi();
        WireEvents();
        LoadDbList();
        SetEditMode(false);
    }

    // ---------------- UI ----------------

    private void BuildUi()
    {
        // Form base
        SuspendLayout();
        BackColor = Color.White;
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 9f);

        // Root layout: Header / Selection / Details / Buttons
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = Color.White,
            Padding = new Padding(12),
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        // row 0 = header (Auto)
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        // row 1 = selection (Auto)
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        // row 2 = details (Fill)
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        // row 3 = buttons (Auto)
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        Controls.Add(root);

        // ---------------- Header ----------------
        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 56,
            BackColor = Color.FromArgb(245, 247, 250),
            Padding = new Padding(14, 10, 14, 10),
            Margin = new Padding(0, 0, 0, 10)
        };

        var title = new Label
        {
            Text = "PLC Tag Picker",
            AutoSize = true,
            Font = new Font("Segoe UI", 14f, FontStyle.Bold),
            ForeColor = Color.FromArgb(35, 35, 35),
            Location = new Point(0, 0)
        };

        lblEditHint = new Label
        {
            Text = "Read-only mode",
            AutoSize = true,
            Font = new Font("Segoe UI", 9f, FontStyle.Regular),
            ForeColor = Color.DimGray,
            Location = new Point(2, 30)
        };

        header.Controls.Add(title);
        header.Controls.Add(lblEditHint);
        root.Controls.Add(header, 0, 0);

        // ---------------- Selection Group ----------------
        var grpSelect = new GroupBox
        {
            Text = "Selection",
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(12),
            Margin = new Padding(0, 0, 0, 10)
        };

        cboDb.DropDownStyle = ComboBoxStyle.DropDownList;
        cboTag.DropDownStyle = ComboBoxStyle.DropDownList;

        cboDb.Dock = DockStyle.Fill;
        cboTag.Dock = DockStyle.Fill;

        var selectGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 2
        };
        selectGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        selectGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        selectGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        selectGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        selectGrid.Controls.Add(MakeLabel("Select DB:"), 0, 0);
        selectGrid.Controls.Add(cboDb, 1, 0);

        selectGrid.Controls.Add(MakeLabel("Select Tag:"), 0, 1);
        selectGrid.Controls.Add(cboTag, 1, 1);

        grpSelect.Controls.Add(selectGrid);
        root.Controls.Add(grpSelect, 0, 1);

        // ---------------- Details Group ----------------
        var grpDetails = new GroupBox
        {
            Text = "Details",
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            Margin = new Padding(0, 0, 0, 10)
        };

        // Inputs style
        txtDbName.Font = txtTagName.Font = txtOffset.Font = new Font("Segoe UI", 9f);
        txtDbName.Dock = DockStyle.Fill;
        txtTagName.Dock = DockStyle.Fill;
        txtOffset.Dock = DockStyle.Fill;

        nudDbNumber.Dock = DockStyle.Fill;
        nudDbNumber.Minimum = 0;
        nudDbNumber.Maximum = 99999;

        cboDataType.Dock = DockStyle.Fill;
        cboDataType.DropDownStyle = ComboBoxStyle.DropDownList;
        cboDataType.Font = new Font("Segoe UI", 9f);
        cboDataType.Items.Clear();
        cboDataType.Items.AddRange(new object[]
        {
        "BOOL","BYTE","CHAR","INT","UINT","DINT","UDINT","REAL","LREAL","WORD","DWORD","TIME"
        });

        // Readonly boxes
        StyleReadOnly(txtAddress);
        StyleReadOnly(txtSheet);
        StyleReadOnly(txtByteLen);

        txtAddress.Dock = DockStyle.Fill;
        txtSheet.Dock = DockStyle.Fill;
        txtByteLen.Dock = DockStyle.Fill;

        txtWarning.ReadOnly = true;
        txtWarning.Multiline = true;
        txtWarning.ScrollBars = ScrollBars.Vertical;
        txtWarning.Dock = DockStyle.Fill;
        txtWarning.BackColor = Color.FromArgb(252, 252, 252);

        // Details grid
        var details = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 6
        };

        details.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        details.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        details.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        details.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        // First 5 rows are fixed height, last row (Warning) fills remaining space
        details.RowStyles.Add(new RowStyle(SizeType.Absolute, 34)); // DB name/number
        details.RowStyles.Add(new RowStyle(SizeType.Absolute, 34)); // tag/type
        details.RowStyles.Add(new RowStyle(SizeType.Absolute, 34)); // offset/byteLen
        details.RowStyles.Add(new RowStyle(SizeType.Absolute, 34)); // address
        details.RowStyles.Add(new RowStyle(SizeType.Absolute, 34)); // sheet
        details.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // warning fill

        // Row 0
        details.Controls.Add(MakeLabel("DB Name:"), 0, 0);
        details.Controls.Add(txtDbName, 1, 0);
        details.Controls.Add(MakeLabel("DB Number:"), 2, 0);
        details.Controls.Add(nudDbNumber, 3, 0);

        // Row 1
        details.Controls.Add(MakeLabel("Tag Name:"), 0, 1);
        details.Controls.Add(txtTagName, 1, 1);
        details.Controls.Add(MakeLabel("DataType:"), 2, 1);
        details.Controls.Add(cboDataType, 3, 1);

        // Row 2
        details.Controls.Add(MakeLabel("Offset:"), 0, 2);
        details.Controls.Add(txtOffset, 1, 2);
        details.Controls.Add(MakeLabel("Byte Length:"), 2, 2);
        details.Controls.Add(txtByteLen, 3, 2);

        // Row 3 (Address spans 3 columns)
        details.Controls.Add(MakeLabel("Full Address:"), 0, 3);
        details.Controls.Add(txtAddress, 1, 3);
        details.SetColumnSpan(txtAddress, 3);

        // Row 4 (Sheet spans 3 columns)
        details.Controls.Add(MakeLabel("Sheet Name:"), 0, 4);
        details.Controls.Add(txtSheet, 1, 4);
        details.SetColumnSpan(txtSheet, 3);

        // Row 5 (Warning spans 3 columns and fills)
        details.Controls.Add(MakeLabel("Warning:"), 0, 5);
        details.Controls.Add(txtWarning, 1, 5);
        details.SetColumnSpan(txtWarning, 3);

        grpDetails.Controls.Add(details);
        root.Controls.Add(grpDetails, 0, 2);

        // ---------------- Bottom Buttons ----------------
        // ---------------- Bottom Buttons (RIGHT aligned) ----------------
        var bottom = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 68,
            BackColor = Color.FromArgb(245, 247, 250),
            Padding = new Padding(12),
            Margin = new Padding(0)
        };

        btnSelect.Text = "Select Tag";
        btnEdit.Text = "Edit";
        btnSave.Text = "Save Changes";
        btnCancel.Text = "Cancel";
        btnCancel.DialogResult = DialogResult.Cancel;

        StyleButtonPrimary(btnSelect);
        StyleButtonSecondary(btnEdit);
        StyleButtonSecondary(btnSave);
        StyleButtonSecondary(btnCancel);

        // ✅ Right-aligned row with perfect spacing
        var buttons = new TableLayoutPanel
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            ColumnCount = 4,
            RowCount = 1,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        buttons.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        btnEdit.Margin = new Padding(0, 8, 10, 8);
        btnSave.Margin = new Padding(0, 8, 10, 8);
        btnSelect.Margin = new Padding(0, 8, 10, 8);
        btnCancel.Margin = new Padding(0, 8, 0, 8);

        buttons.Controls.Add(btnEdit, 0, 0);
        buttons.Controls.Add(btnSave, 1, 0);
        buttons.Controls.Add(btnSelect, 2, 0);
        buttons.Controls.Add(btnCancel, 3, 0);

        bottom.Controls.Add(buttons);
        root.Controls.Add(bottom, 0, 3);

        AcceptButton = btnSelect;
        CancelButton = btnCancel;

        ResumeLayout(true);
    }

    private static Label MakeLabel(string text)
        => new Label
        {
            Text = text,
            AutoSize = true,
            Font = new Font("Segoe UI", 9f),
            ForeColor = Color.FromArgb(50, 50, 50),
            Padding = new Padding(0, 7, 0, 0)
        };

    private static void StyleReadOnly(TextBox tb)
    {
        tb.ReadOnly = true;
        tb.Font = new Font("Segoe UI", 9f);
        tb.BackColor = Color.FromArgb(252, 252, 252);
    }

    private static void StyleButtonPrimary(Button b)
    {
        b.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
        b.BackColor = Color.FromArgb(52, 120, 246);
        b.ForeColor = Color.White;
        b.FlatStyle = FlatStyle.Flat;
        b.FlatAppearance.BorderSize = 0;
        b.Height = 34;
        b.Width = 120;
        b.Margin = new Padding(10, 8, 0, 8);
    }

    private static void StyleButtonSecondary(Button b)
    {
        b.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
        b.BackColor = Color.White;
        b.ForeColor = Color.FromArgb(60, 60, 60);
        b.FlatStyle = FlatStyle.Flat;
        b.FlatAppearance.BorderColor = Color.FromArgb(210, 210, 210);
        b.FlatAppearance.BorderSize = 1;
        b.Height = 34;
        b.Width = 120;
        b.Margin = new Padding(10, 8, 0, 8);
    }

    // ---------------- Events ----------------

    private void WireEvents()
    {
        cboDb.SelectedIndexChanged += (_, __) =>
        {
            if (_loading) return;
            LoadTagsForSelectedDb();
        };

        cboTag.SelectedIndexChanged += (_, __) =>
        {
            if (_loading) return;
            ShowSelectedTagDetails();
        };

        btnEdit.Click += (_, __) =>
        {
            if (_currentTag == null)
            {
                MessageBox.Show(this, "Select a tag first.", "Edit", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            SetEditMode(true);
        };

        btnSave.Click += (_, __) =>
        {
            if (_currentTag == null) return;

            var updated = BuildUpdatedTagFromUi(_currentTag);
            if (updated == null) return; // validation failed

            // Update UI to show recomputed address + byte length + warning
            _currentTag = updated;
            ShowTagInUi(_currentTag);

            SetEditMode(false);
        };

        btnSelect.Click += (_, __) =>
        {
            // If user is editing, save before selecting
            if (_editMode)
            {
                btnSave.PerformClick();
                if (_editMode) return; // still in edit mode due to validation errors
            }

            if (_currentTag == null)
            {
                MessageBox.Show(this, "Please select a tag.", "Select Tag",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SelectedTagInfo = _currentTag;
            DialogResult = DialogResult.OK;
            Close();
        };
    }

    private void SetEditMode(bool on)
    {
        _editMode = on;

        // editable fields only in edit mode
        txtDbName.ReadOnly = !on;
        nudDbNumber.Enabled = on;
        txtTagName.ReadOnly = !on;
        cboDataType.Enabled = on;
        txtOffset.ReadOnly = !on;

        // selection should not change while editing
        cboDb.Enabled = !on;
        cboTag.Enabled = !on;

        btnSave.Enabled = on;
        btnEdit.Enabled = !on;

        lblEditHint.Text = on ? "Edit mode ON — you can modify fields, then click Save Changes." : "Read-only mode";
        lblEditHint.ForeColor = on ? Color.FromArgb(180, 110, 0) : Color.DimGray;
    }

    // ---------------- Data Load ----------------

    private void LoadDbList()
    {
        _loading = true;
        try
        {
            var dbs = _allTags
                .GroupBy(t => new { t.DbNumber, t.DbName })
                .Select(g => new DbItem { DbNumber = g.Key.DbNumber, DbName = g.Key.DbName ?? "" })
                .OrderBy(d => d.DbNumber)
                .ThenBy(d => d.DbName)
                .ToList();

            cboDb.DataSource = dbs;

            if (dbs.Count > 0) cboDb.SelectedIndex = 0;
            LoadTagsForSelectedDb();
        }
        finally { _loading = false; }
    }

    private void LoadTagsForSelectedDb()
    {
        _loading = true;
        try
        {
            var db = cboDb.SelectedItem as DbItem;
            if (db == null)
            {
                cboTag.DataSource = null;
                ClearDetails();
                return;
            }

            var tags = _allTags
                .Where(t => t.DbNumber == db.DbNumber &&
                            string.Equals(t.DbName ?? "", db.DbName ?? "", StringComparison.OrdinalIgnoreCase))
                .OrderBy(t => t.Name)
                .ToList();

            cboTag.DataSource = tags;
            cboTag.DisplayMember = nameof(PlcTagInfo.Name);

            if (tags.Count > 0) cboTag.SelectedIndex = 0;

            ShowSelectedTagDetails();
        }
        finally { _loading = false; }
    }

    private void ShowSelectedTagDetails()
    {
        var tag = cboTag.SelectedItem as PlcTagInfo;
        _currentTag = tag;
        ShowTagInUi(tag);
        SetEditMode(false);
    }

    private void ShowTagInUi(PlcTagInfo? tag)
    {
        if (tag == null)
        {
            ClearDetails();
            return;
        }

        txtDbName.Text = tag.DbName ?? "";
        nudDbNumber.Value = tag.DbNumber >= (int)nudDbNumber.Minimum && tag.DbNumber <= (int)nudDbNumber.Maximum ? tag.DbNumber : 0;

        txtTagName.Text = tag.Name ?? "";
        cboDataType.SelectedItem = (tag.DataType ?? "").Trim().ToUpperInvariant();
        if (cboDataType.SelectedIndex < 0 && cboDataType.Items.Count > 0)
            cboDataType.SelectedIndex = 0;

        txtOffset.Text = tag.OffsetRaw ?? "";
        txtAddress.Text = tag.FullAddress ?? "";
        txtSheet.Text = tag.SheetName ?? "";
        txtByteLen.Text = tag.ByteLength.ToString();
        txtWarning.Text = tag.Warning ?? "";
    }

    private void ClearDetails()
    {
        txtDbName.Text = "";
        nudDbNumber.Value = 0;
        txtTagName.Text = "";
        cboDataType.SelectedIndex = -1;
        txtOffset.Text = "";
        txtAddress.Text = "";
        txtSheet.Text = "";
        txtByteLen.Text = "";
        txtWarning.Text = "";
    }

    // ---------------- Build updated tag ----------------

    private PlcTagInfo? BuildUpdatedTagFromUi(PlcTagInfo baseTag)
    {
        string dbName = (txtDbName.Text ?? "").Trim();
        int dbNum = (int)nudDbNumber.Value;

        string tagName = (txtTagName.Text ?? "").Trim();
        string dt = (cboDataType.SelectedItem?.ToString() ?? "").Trim();
        string off = (txtOffset.Text ?? "").Trim();

        if (string.IsNullOrWhiteSpace(tagName))
        {
            MessageBox.Show(this, "Tag Name cannot be empty.", "Validation",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return null;
        }

        if (dbNum <= 0)
        {
            MessageBox.Show(this, "DB Number must be > 0.", "Validation",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return null;
        }

        // Recompute Address + Length + Warning using your builder
        var rebuilt = PlcTagExcelLoader.BuildAddress(
            dbName,
            dbNum,
            baseTag.SheetName ?? "",
            tagName,
            dt,
            off
        );

        return rebuilt;
    }
}
