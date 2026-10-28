using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Aarohi.ExtendedUI;
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

        #region Public Properties & Customization Hooks

        /// <summary>
        /// Direct access to the internal data grid (avoids manual control tree scanning).
        /// </summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ExtendedDataGridView Grid => _edgv;

        /// <summary>
        /// Direct access to the underlying DynamicClass metadata &amp; schema definition.
        /// </summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public DynamicClass DynamicClass => _dc;

        /// <summary>
        /// Direct access to the action buttons for custom styling, icons, or visibility toggles.
        /// </summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ExtendedUI.ExtendedButton AddButton => ButtonAdd;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ExtendedUI.ExtendedButton EditButton => ButtonEdit;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ExtendedUI.ExtendedButton DeleteButton => ButtonDelete;

        /// <summary>
        /// Direct access to the header title label.
        /// </summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Label TitleLabel => LabelHeading;

        /// <summary>
        /// If true, automatically snaps to screen working area bounds in portrait screens.
        /// Set to false if the caller application defines its own Bounds or Size. Default is true.
        /// </summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool AutoEnforceWorkingAreaBounds { get; set; } = true;

        /// <summary>
        /// If true, uses styled ExtendedMessageBox; if false, falls back to standard WinForms MessageBox.
        /// Defaults to true.
        /// </summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool UseExtendedMessageBox { get; set; } = true;

        /// <summary>
        /// If true, displays a success message box after saving a record in the child Adder form.
        /// Set to false if the caller application handles notifications independently. Defaults to true.
        /// </summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool ShowSaveSuccessMessage { get; set; } = true;

        /// <summary>
        /// Optional explicit size for the child DynamicAdderForm popup. If null, uses Adder form's default size.
        /// </summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Size? AdderFormSize { get; set; }

        /// <summary>
        /// Startup position for the child DynamicAdderForm. Defaults to CenterParent.
        /// </summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public FormStartPosition AdderFormStartPosition { get; set; } = FormStartPosition.CenterParent;

        /// <summary>
        /// If true, opens DynamicAdderForm as a modal dialog (ShowDialog(this)).
        /// If false, opens it modelessly with this form as owner (Show(this)).
        /// Defaults to false for backward compatibility.
        /// </summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool ShowAdderAsDialog { get; set; } = false;

        /// <summary>
        /// Hook invoked right after DynamicAdderForm is instantiated and before it is shown.
        /// Parameters: (DynamicAdderForm adderForm, bool isEditMode).
        /// Allows any calling application to configure validation, dependencies, sizes, or rules.
        /// </summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Action<DynamicAdderForm, bool>? ConfigureAdderForm { get; set; }

        /// <summary>
        /// Optional predicate to validate whether a record can be deleted.
        /// Return null to allow deletion, or a non-null string containing the reason deletion is blocked.
        /// </summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Func<IDictionary<string, object?>, string?>? CanDeleteRecord { get; set; }

        /// <summary>
        /// Fires when a record is successfully saved in the Adder form.
        /// </summary>
        public event EventHandler? RecordSaved;

        /// <summary>
        /// Fires when a record is successfully deleted.
        /// </summary>
        public event EventHandler? RecordDeleted;

        #endregion

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
            if (!AutoEnforceWorkingAreaBounds)
                return;

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

        private void ShowAdderForm(bool isEditMode, Dictionary<string, object?>? initVal = null)
        {
            string title = isEditMode ? $"Edit {_suffix}" : $"Add {_suffix}";

            _adder = new DynamicAdderForm(
                title,
                new DynamicClass[] { _dc },
                MapOfCombobox: _mapOfCombobox,
                InitVal: initVal);

            _adder.UseExtendedMessageBox = UseExtendedMessageBox;
            _adder.ShowSaveSuccessMessage = ShowSaveSuccessMessage;

            if (AdderFormSize.HasValue)
            {
                _adder.AutoEnforceWorkingAreaBounds = false;
                _adder.WindowState = FormWindowState.Normal;
                _adder.Size = AdderFormSize.Value;
            }
            _adder.StartPosition = AdderFormStartPosition;

            ConfigureAdderForm?.Invoke(_adder, isEditMode);

            _adder.Save_Success += (_, __) =>
            {
                refresh();
                RecordSaved?.Invoke(this, EventArgs.Empty);
            };

            if (ShowAdderAsDialog)
            {
                _adder.ShowDialog(this);
            }
            else
            {
                _adder.Show(this);
            }
        }

        private DialogResult ShowUserMessage(string message, string title, MessageType type, MessageButtons buttons = MessageButtons.OK)
        {
            if (UseExtendedMessageBox)
            {
                return ExtendedMessageBox.Show(this, message, title, type, buttons);
            }
            else
            {
                MessageBoxIcon icon = type switch
                {
                    MessageType.Error => MessageBoxIcon.Error,
                    MessageType.Warning => MessageBoxIcon.Warning,
                    _ => MessageBoxIcon.Information
                };
                MessageBoxButtons winButtons = buttons switch
                {
                    MessageButtons.YesNo => MessageBoxButtons.YesNo,
                    MessageButtons.OKCancel => MessageBoxButtons.OKCancel,
                    MessageButtons.YesNoCancel => MessageBoxButtons.YesNoCancel,
                    MessageButtons.RetryCancel => MessageBoxButtons.RetryCancel,
                    _ => MessageBoxButtons.OK
                };
                return MessageBox.Show(this, message, title, winButtons, icon);
            }
        }

        private void ButtonAdd_Click(object sender, EventArgs e)
        {
            ShowAdderForm(isEditMode: false);
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
            ShowAdderForm(isEditMode: true, initVal: (Dictionary<string, object?>?)rowData);
        }

        private void ButtonDelete_Click(object sender, EventArgs e)
        {
            try
            {
                _edgv.TryGetSelectedRowData(out IDictionary<string, object?> rowData);
                if (rowData == null) return;

                if (CanDeleteRecord != null)
                {
                    string? blockReason = CanDeleteRecord(rowData);
                    if (!string.IsNullOrWhiteSpace(blockReason))
                    {
                        ShowUserMessage(blockReason, "Delete Blocked", MessageType.Warning, MessageButtons.OK);
                        return;
                    }
                }

                var result = ShowUserMessage(
                    $"Are you sure you want to delete {_suffix} '{rowData[_dc.GetPrimaryKeyColumns().FirstOrDefault()]}'?",
                    "Confirm Delete",
                    MessageType.Warning,
                    MessageButtons.YesNo);
                if (result == DialogResult.Yes)
                {
                    object? GrpPrimaryKey = _dc.GetPrimaryKeyColumns().FirstOrDefault();
                    object val = rowData[GrpPrimaryKey.ToString()];
                    if (GrpPrimaryKey != null && val != null)
                    {
                        _dc.DeleteByKey(val);
                        refresh();
                        RecordDeleted?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
            catch (ForeignKeyDeleteBlockedException Fkex)
            {
                ShowUserMessage(Fkex.Message, "Delete Blocked", MessageType.Warning, MessageButtons.OK);
            }
            catch (Exception ex)
            {
                ShowUserMessage(ex.Message, "Delete Failed", MessageType.Error, MessageButtons.OK);
            }
        }

        private void refresh()
        {
            _edgv.LoadDynamicClassData(keepFilters: true);
        }

    }
}
