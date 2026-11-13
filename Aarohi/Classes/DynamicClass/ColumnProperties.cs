using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aarohi.Classes;

#region Column Extended Properties
/// <summary>
/// Defines metadata and database column properties for dynamic table provisioning and UI mapping.
/// Works across all database engines supported by DynamicClass (SqlServer, Sqlite, PostgreSql, MySql).
/// </summary>
public class Column
{
    public string ParentTable { get; private set; }

    public bool IsPrimaryKey { get; private set; }
    
    public string Name { get; private set; }

    public string DataType { get; private set; }

    public int MaxLength { get; private set; }

    public byte Precision { get; private set; }

    public byte Scale { get; private set; }

    public bool Nullable { get; private set; }

    public bool Identity { get; private set; }

    public string? DefaultSql { get; private set; }

    public object? DefaultValue { get; private set; }

    public string? CheckDefinition { get; private set; }

    public string? SoftUsedName { get; private set; }

    public string? SoftName { get; private set; }

    public string[]? Options { get; private set; }

    public bool HasOptions { get; private set; }

    public bool IsForeignKey { get; private set; }

    public string? ForeignKeyName { get; private set; }

    public string? ReferencedTable { get; private set; }

    public string? ReferencedColumn { get; private set; }

    public string? DisplayName { get; private set; }

    public string? Description { get; private set; }

    public string? Unit { get; private set; }

    public string? DefaultUnit { get; private set; }

    public string? InputUnit { get; private set; }

    public string? LastUsedUnit { get; private set; }

    public string? ShowUnit { get; private set; }

    public string? ReportUnit { get; private set; }

    public bool? DatagridShow { get; private set; }

    public bool? ShowInOnePhase { get; private set; }

    public bool? ShowInThreePhase { get; private set; }

    public bool? HideInCrudForm { get; private set; }

    public string? Format { get; private set; }

    public string? Parameter { get; private set; }

    public string? Group { get; private set; }

    public bool? IsRequired { get; private set; }

    public int? Order { get; private set; }

    public bool? Visible { get; private set; }
    public bool foreignKeyTable { get; private set; }

    public static implicit operator string(Column col) => col?.Name ?? string.Empty;
    public override string ToString() => Name;

    public Column(
        string parentTable,
        string name,
        bool isPrimaryKey,
        string dataType,
        short maxLength,
        byte precision,
        byte scale,
        bool nullable,
        bool identity,
        string? defaultSql = null,
        object? defaultValue = null,
        string? checkDefinition = null,
        string? softUsedName = null,
        string? softName = null,
        string[]? options = null,
        bool hasOptions = false,
        bool isForeignKey = false,
        string? foreignKeyName = null,
        string? referencedTable = null,
        string? referencedColumn = null,
        string? displayName = null,
        string? description = null,
        string? unit = null,
        string? defaultUnit = null,
        string? inputUnit = null,
        string? lastUsedUnit = null,
        string? showUnit = null,
        string? reportUnit = null,
        bool? datagridShow = null,
        bool? showInOnePhase = null,
        bool? showInThreePhase = null,
        bool? hideInCrudForm = null,
        string? format = null,
        string? parameter = null,
        string? group = null,
        bool? isRequired = null,
        int? order = null,
        bool? visible = null)
    {
        ParentTable = parentTable;
        Name = name ?? throw new ArgumentNullException(nameof(name));
        DataType = dataType ?? throw new ArgumentNullException(nameof(dataType));
        MaxLength = maxLength;
        IsPrimaryKey = isPrimaryKey;
        Precision = precision;
        Scale = scale;
        Nullable = nullable;
        Identity = identity;
        DefaultSql = defaultSql;
        DefaultValue = defaultValue;
        CheckDefinition = checkDefinition;
        SoftUsedName = softUsedName;
        SoftName = softName;
        Options = options;
        HasOptions = hasOptions;
        IsForeignKey = isForeignKey;
        ForeignKeyName = foreignKeyName;
        ReferencedTable = referencedTable;
        ReferencedColumn = referencedColumn;
        DisplayName = displayName;
        Description = description;
        Unit = unit;
        DefaultUnit = defaultUnit;
        InputUnit = inputUnit;
        LastUsedUnit = lastUsedUnit;
        ShowUnit = showUnit;
        ReportUnit = reportUnit;
        DatagridShow = datagridShow;
        ShowInOnePhase = showInOnePhase;
        ShowInThreePhase = showInThreePhase;
        HideInCrudForm = hideInCrudForm;
        Format = format;
        Parameter = parameter;
        Group = group;
        IsRequired = isRequired;
        Order = order;
        Visible = visible;
    }
}
#endregion
