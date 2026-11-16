namespace Aarohi.Wpf.Controls.Lib.Controls.DataGridView;

public enum ColumnUnitDisplayMode
{
    Default,
    Input,
    Show,
    Report,
    LastUsed
}

public sealed class TableColumnMetadata
{
    public string ColumnName { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Unit { get; set; } = string.Empty;

    public string Parameter { get; set; } = string.Empty;

    public string DefaultUnit { get; set; } = string.Empty;

    public string InputUnit { get; set; } = string.Empty;

    public string ShowUnit { get; set; } = string.Empty;

    public string ReportUnit { get; set; } = string.Empty;

    public string LastUsedUnit { get; set; } = string.Empty;

    public bool IsPrimaryKey { get; set; }

    public ColumnUnitDisplayMode UnitDisplayMode { get; set; } = ColumnUnitDisplayMode.Default;

    public string HeaderText => GetHeaderText(UnitDisplayMode);

    public string GetHeaderText(ColumnUnitDisplayMode unitDisplayMode)
    {
        string name = string.IsNullOrWhiteSpace(DisplayName) ? ColumnName : DisplayName;
        string unit = GetUnitText(unitDisplayMode);
        return string.IsNullOrWhiteSpace(unit) ? name : $"{name} ({unit})";
    }

    public string GetUnitText(ColumnUnitDisplayMode unitDisplayMode)
    {
        return unitDisplayMode switch
        {
            ColumnUnitDisplayMode.Show => FirstNotEmpty(ShowUnit, DefaultUnit, Unit, InputUnit, ReportUnit, LastUsedUnit),
            ColumnUnitDisplayMode.Input => FirstNotEmpty(InputUnit, DefaultUnit, Unit, ShowUnit, ReportUnit, LastUsedUnit),
            ColumnUnitDisplayMode.Report => FirstNotEmpty(ReportUnit, DefaultUnit, Unit, ShowUnit, InputUnit, LastUsedUnit),
            ColumnUnitDisplayMode.LastUsed => FirstNotEmpty(LastUsedUnit, DefaultUnit, Unit, ShowUnit, InputUnit, ReportUnit),
            _ => FirstNotEmpty(DefaultUnit, Unit, ShowUnit, InputUnit, ReportUnit, LastUsedUnit)
        };
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
}
