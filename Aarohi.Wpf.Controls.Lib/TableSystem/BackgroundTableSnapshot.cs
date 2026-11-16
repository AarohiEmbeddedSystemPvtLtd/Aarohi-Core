using System.Data;
using Aarohi.Wpf.Controls.Lib.Controls.DataGridView;

namespace Aarohi.Wpf.Controls.Lib.TableSystem;

public sealed class BackgroundTableSnapshot
{
    public BackgroundTableSnapshot(DataTable data, IReadOnlyList<TableColumnMetadata> columns, DateTime updatedAt)
    {
        Data = data;
        Columns = columns;
        UpdatedAt = updatedAt;
    }

    public DataTable Data { get; }

    public DataView View
    {
        get { return Data.DefaultView; }
    }

    public IReadOnlyList<TableColumnMetadata> Columns { get; }

    public DateTime UpdatedAt { get; }

    public int RowCount
    {
        get { return Data.Rows.Count; }
    }
}
