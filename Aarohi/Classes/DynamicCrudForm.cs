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
            _adder = new DynamicAdderForm($"Add {_suffix}", new DynamicClass[] { _dc }, MapOfCombobox: _mapOfCombobox);
            _edgv = new ExtendedDataGridView(_dc);
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
            PanelTestUCHolder.Controls.Add(_edgv);
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
