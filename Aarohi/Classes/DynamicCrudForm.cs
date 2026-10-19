using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static Aarohi.Core.Exceptions;

namespace Aarohi.Classes
{
    public partial class DynamicCrudForm : Form
    {
        DynamicClass _dc;
        DynamicAdderForm _adder;
        ExtendedDataGridView _edgv;

        string _tableName;
        string _schemaName;
        string _suffix;

        Dictionary<string, string[]> _mapOfCombobox;

        private bool _applyingResponsiveLayout;

        public DynamicCrudForm(string schema, string table, string suffix, Dictionary<string, string[]> mapOfCombobox = null)
        {
            _tableName = table;
            _schemaName = schema;
            _suffix = suffix;
            _mapOfCombobox = mapOfCombobox;

            InitializeComponent();

            LabelHeading.Text = $"{_suffix} Manager";
            ButtonAdd.Text = $"Add {_suffix}";
            ButtonEdit.Text = $"Edit {_suffix}";
            ButtonDelete.Text = $"Delete {_suffix}";

            _dc = new DynamicClass(_schemaName, _tableName);
            _adder = new DynamicAdderForm(
                $"Add {_suffix}",
                new DynamicClass[] { _dc },
                MapOfCombobox: _mapOfCombobox);

            _edgv = new ExtendedDataGridView(_dc);

            Resize -= DynamicCrudForm_Resize;
            Resize += DynamicCrudForm_Resize;

            Shown -= DynamicCrudForm_Shown;
            Shown += DynamicCrudForm_Shown;
        }

        private void DynamicCrudForm_Resize(object? sender, EventArgs e)
        {
            ApplyResponsiveLayout();
        }

        private void ApplyWorkingAreaBounds()
        {
            Screen screen = Screen.FromControl(this);
            Rectangle workingArea = screen.WorkingArea;

            bool isPortrait = workingArea.Height > workingArea.Width;

            // Preserve current landscape behavior.
            if (!isPortrait)
                return;

            WindowState = FormWindowState.Normal;
            StartPosition = FormStartPosition.Manual;

            Bounds = workingArea;
        }

        private void DynamicCrudForm_Shown(object? sender, EventArgs e)
        {
            ApplyWorkingAreaBounds();
            ApplyResponsiveLayout();
        }

        private void ApplyResponsiveLayout()
        {
            if (_applyingResponsiveLayout ||
                IsDisposed ||
                Disposing ||
                ClientSize.Width <= 0 ||
                ClientSize.Height <= 0)
            {
                return;
            }

            try
            {
                _applyingResponsiveLayout = true;

                bool isPortrait =
                    ClientSize.Height > ClientSize.Width;

                if (isPortrait)
                {
                    ApplyPortraitLayout();
                }
                else
                {
                    ApplyLandscapeLayout();
                }
            }
            finally
            {
                _applyingResponsiveLayout = false;
            }
        }

        private void ApplyPortraitLayout()
        {
            SuspendLayout();

            try
            {
                PanelFooter.Height = 91;

                extendedPanel3.Dock = DockStyle.Left;
                extendedPanel2.Dock = DockStyle.Right;

                int footerWidth =
                    Math.Max(
                        0,
                        PanelFooter.ClientSize.Width -
                        PanelFooter.Padding.Horizontal);

                if (footerWidth <= 0)
                    return;

                const int groupGap = 10;

                /*
                 * Give Edit/Delete about two-thirds of the footer,
                 * because it contains two buttons.
                 *
                 * Give Add about one-third.
                 */
                int addGroupWidth =
                    Math.Max(
                        150,
                        (footerWidth - groupGap) / 3);

                int editDeleteGroupWidth =
                    Math.Max(
                        300,
                        footerWidth -
                        addGroupWidth -
                        groupGap);

                extendedPanel3.Width =
                    editDeleteGroupWidth;

                extendedPanel2.Width =
                    addGroupWidth;

                const int buttonGap = 10;

                int editDeleteUsable =
                    extendedPanel3.ClientSize.Width -
                    extendedPanel3.Padding.Horizontal;

                int editDeleteButtonWidth =
                    Math.Max(
                        100,
                        (editDeleteUsable - buttonGap) / 2);

                ButtonEdit.Width =
                    editDeleteButtonWidth;

                ButtonDelete.Width =
                    editDeleteButtonWidth;

                int addUsable =
                    extendedPanel2.ClientSize.Width -
                    extendedPanel2.Padding.Horizontal;

                ButtonAdd.Width =
                    Math.Max(100, addUsable);

                Padding portraitPadding =
                    new Padding(4, 8, 4, 8);

                ButtonEdit.Padding = portraitPadding;
                ButtonDelete.Padding = portraitPadding;
                ButtonAdd.Padding = portraitPadding;

                extendedPanel3.PerformLayout();
                extendedPanel2.PerformLayout();
                PanelFooter.PerformLayout();
            }
            finally
            {
                ResumeLayout(true);
            }
        }

        private void ApplyLandscapeLayout()
        {
            SuspendLayout();

            try
            {
                PanelFooter.Height = 91;

                extendedPanel3.Dock = DockStyle.Left;
                extendedPanel3.Width = 420;

                extendedPanel2.Dock = DockStyle.Right;
                extendedPanel2.Width = 248;

                ButtonEdit.Width = 195;
                ButtonDelete.Width = 195;
                ButtonAdd.Width = 228;

                Padding originalPadding =
                    new Padding(14, 8, 14, 8);

                ButtonEdit.Padding = originalPadding;
                ButtonDelete.Padding = originalPadding;
                ButtonAdd.Padding = originalPadding;

                extendedPanel3.PerformLayout();
                extendedPanel2.PerformLayout();
                PanelFooter.PerformLayout();
            }
            finally
            {
                ResumeLayout(true);
            }
        }

        private void ButtonAdd_Click(object sender, EventArgs e)
        {
            if (_adder.IsDisposed)
                _adder = new DynamicAdderForm($"Add {_suffix}", new DynamicClass[] { _dc }, MapOfCombobox: _mapOfCombobox);
            _adder.Save_Success += (_, __) => refresh();
            _adder.Show();
        }

        private void DynamicCrudForm_Load(object sender, EventArgs e)
        {
            _edgv.Dock = DockStyle.Fill;

            PanelTestUCHolder.AutoScroll = false;
            PanelTestUCHolder.EnableAutoScrollY = false;

            PanelTestUCHolder.Controls.Add(_edgv);

            ApplyResponsiveLayout();
        }

        private void ButtonEdit_Click(object sender, EventArgs e)
        {
            _edgv.TryGetSelectedRowData(out IDictionary<string, object?> rowData);
            if (rowData == null) return;
            _adder = new DynamicAdderForm($"Edit {_suffix}", new DynamicClass[] { _dc },MapOfCombobox:_mapOfCombobox, InitVal: (Dictionary<string, object?>?)rowData);
            _adder.Save_Success += (_, __) => refresh();
            _adder.Show();
        }

        private void ButtonDelete_Click(object sender, EventArgs e)
        {
            try
            {
                _edgv.TryGetSelectedRowData(out IDictionary<string, object?> rowData);
                if (rowData == null) return;
                var result = MessageBox.Show($"Are you sure you want to delete {_suffix} '{rowData[_dc.GetPrimaryKeyColumns().FirstOrDefault()]}'?", "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (result == DialogResult.Yes)
                {
                    object? GrpPrimaryKey = _dc.GetPrimaryKeyColumns().FirstOrDefault();
                    object val = rowData[GrpPrimaryKey.ToString()];
                    if (GrpPrimaryKey != null && val != null)
                    {
                        _dc.DeleteByKey(val);
                        refresh();
                    }
                }
            }
            catch (ForeignKeyDeleteBlockedException Fkex)
            {
                MessageBox.Show(Fkex.Message, "Delete Blocked", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Delete Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void refresh()
        {
            _edgv.LoadDynamicClassData(keepFilters: true);
        }

    }
}
