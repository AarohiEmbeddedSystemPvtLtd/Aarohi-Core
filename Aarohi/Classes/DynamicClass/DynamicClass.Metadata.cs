using Aarohi.Core.Logger;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using static Aarohi.Core.Exceptions;

namespace Aarohi.Classes
{
    public sealed partial class DynamicClass
    {

        #region Schema metadata and extended properties
        #region Extended Properties Manager

        private bool UpsertExtendedProperty(string propName, object value, string? column = null)
        {
            EnsureIdent(Schema);
            var physicalTable = ResolveTableName();
            var physicalColumn = string.IsNullOrWhiteSpace(column) ? null : ResolveColumnName(column);

            using var cn = Open();
            using var tx = cn.BeginTransaction();
            try
            {
                var existsSql = @"
DECLARE @colId int = NULL;
IF @col IS NOT NULL
    SELECT @colId = c.column_id
    FROM sys.columns c
    JOIN sys.tables t  ON t.object_id = c.object_id
    JOIN sys.schemas s ON s.schema_id = t.schema_id
    WHERE s.name=@s AND t.name=@t AND c.name=@col;

SELECT CASE WHEN EXISTS (
  SELECT 1
  FROM sys.extended_properties ep
  JOIN sys.tables t  ON t.object_id = ep.major_id
  JOIN sys.schemas s ON s.schema_id = t.schema_id
  WHERE ep.name=@n AND s.name=@s AND t.name=@t
    AND ((@col IS NULL AND ep.minor_id = 0) OR (@col IS NOT NULL AND ep.minor_id = @colId))
) THEN 1 ELSE 0 END;";

                using (var chk = new SqlCommand(existsSql, cn, tx))
                {
                    chk.Parameters.AddWithValue("@n", propName);
                    chk.Parameters.AddWithValue("@s", Schema);
                    chk.Parameters.AddWithValue("@t", physicalTable);
                    chk.Parameters.AddWithValue("@col", (object?)physicalColumn ?? DBNull.Value);
                    var exists = Convert.ToInt32(chk.ExecuteScalar()) == 1;

                    var proc = exists ? "sys.sp_updateextendedproperty" : "sys.sp_addextendedproperty";
                    var sql = $@"
EXEC {proc}
  @name=@n, @value=@v,
  @level0type=N'SCHEMA', @level0name=@s,
  @level1type=N'TABLE',  @level1name=@t
{(string.IsNullOrWhiteSpace(column) ? "" : ",  @level2type=N'COLUMN', @level2name=@c")};";

                    using var cmd = new SqlCommand(sql, cn, tx);
                    cmd.Parameters.AddWithValue("@n", propName);
                    cmd.Parameters.AddWithValue("@v", value ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@s", Schema);
                    cmd.Parameters.AddWithValue("@t", physicalTable);
                    if (!string.IsNullOrWhiteSpace(physicalColumn))
                        cmd.Parameters.AddWithValue("@c", physicalColumn);

                    cmd.ExecuteNonQuery();
                }

                tx.Commit();
                InvalidateColumnMetadataCache();
                return true;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        private bool DropExtendedProperty(string propName, string? column = null)
        {
            EnsureIdent(Schema);
            var physicalTable = ResolveTableName();
            var physicalColumn = string.IsNullOrWhiteSpace(column) ? null : ResolveColumnName(column);

            using var cn = Open();
            var sql = $@"
BEGIN TRY
  EXEC sys.sp_dropextendedproperty
    @name=@n,
    @level0type=N'SCHEMA', @level0name=@s,
    @level1type=N'TABLE',  @level1name=@t
    {(string.IsNullOrWhiteSpace(column) ? "" : ", @level2type=N'COLUMN', @level2name=@c")};
  RETURN 1;
END TRY
BEGIN CATCH
  -- Property might not exist; treat as success for idempotency
  RETURN 1;
END CATCH";
            using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@n", propName);
            cmd.Parameters.AddWithValue("@s", Schema);
            cmd.Parameters.AddWithValue("@t", physicalTable);
            if (!string.IsNullOrWhiteSpace(physicalColumn))
                cmd.Parameters.AddWithValue("@c", physicalColumn);
            cmd.ExecuteNonQuery();
            InvalidateColumnMetadataCache();
            return true;
        }

        //        private object? ReadExtendedProperty(string propName, string? column = null)
        //        {
        //            EnsureIdent(Schema); EnsureIdent(Table);
        //            if (!string.IsNullOrWhiteSpace(column)) EnsureIdent(column);

        //            const string sql = @"
        //SELECT CAST(ep.value AS sql_variant) AS v
        //FROM sys.extended_properties ep
        //JOIN sys.tables t  ON t.object_id = ep.major_id
        //JOIN sys.schemas s ON s.schema_id = t.schema_id
        //LEFT JOIN sys.columns c ON c.object_id = t.object_id AND c.column_id = ep.minor_id
        //WHERE ep.name=@n AND s.name=@s AND t.name=@t
        //  AND ((@c IS NULL AND ep.minor_id = 0) OR (@c IS NOT NULL AND c.name=@c));";

        //            using var cn = Open();
        //            using var cmd = new SqlCommand(sql, cn);
        //            cmd.Parameters.AddWithValue("@n", propName);
        //            cmd.Parameters.AddWithValue("@s", Schema);
        //            cmd.Parameters.AddWithValue("@t", Table);
        //            cmd.Parameters.AddWithValue("@c", (object?)column ?? DBNull.Value);

        //            var obj = cmd.ExecuteScalar();
        //            return obj == null || obj == DBNull.Value ? null : obj;
        //        }
        public object? ReadExtendedProperty(string propName, string? column = null)
        {
            EnsureIdent(Schema);
            var physicalTable = ResolveTableName();
            var physicalColumn = string.IsNullOrWhiteSpace(column) ? null : ResolveColumnName(column);

            // NOTE:
            // - If column == null => table-level EP
            // - If column != null => column-level EP
            const string sql = @"
SELECT TOP (1) [value]
FROM sys.fn_listextendedproperty
(
    @propName,
    N'SCHEMA', @schema,
    N'TABLE',  @table,
    CASE WHEN @column IS NULL THEN NULL ELSE N'COLUMN' END,
    @column
);";

            using var cn = Open();
            using var cmd = new SqlCommand(sql, cn);

            cmd.Parameters.Add("@propName", System.Data.SqlDbType.NVarChar, 128).Value = propName;
            cmd.Parameters.Add("@schema", System.Data.SqlDbType.NVarChar, 128).Value = Schema;
            cmd.Parameters.Add("@table", System.Data.SqlDbType.NVarChar, 128).Value = physicalTable;
            cmd.Parameters.Add("@column", System.Data.SqlDbType.NVarChar, 128).Value =
                (object?)physicalColumn ?? DBNull.Value;

            var obj = cmd.ExecuteScalar();
            return obj == null || obj == DBNull.Value ? null : obj;
        }

        #endregion

        #region Display Name Helpers

        // TABLE-LEVEL

        // Method: SetTableDisplayName
        /// <summary>
        /// Sets a human-friendly display name for the table (extended property "DisplayName").
        /// </summary>
        /// <param name="displayName">Display name to set.</param>
        /// <returns><c>true</c> on success.</returns>
        public bool SetTableDisplayName(string displayName)
            => UpsertExtendedProperty("DisplayName", displayName, column: null);

        // Method: GetTableDisplayName
        public string GetTableDisplayName()
            => Convert.ToString(ReadExtendedProperty("DisplayName", column: null)) ?? Table;

        public bool RemoveTableDisplayName()
            => DropExtendedProperty("DisplayName", column: null);

        // COLUMN-LEVEL

        // Method: SetColumnDisplayName
        /// <summary>
        /// Sets a human-friendly display name for a column (extended property "DisplayName").
        /// </summary>
        /// <param name="column">Column name.</param>
        /// <param name="displayName">Display name to set.</param>
        /// <returns><c>true</c> on success.</returns>
        public bool SetColumnDisplayName(string column, string displayName)
            => UpsertExtendedProperty("DisplayName", displayName, column);


        // Method: SetColumnDisplayName
        /// <summary>
        /// Sets a human-friendly display name for a column (extended property "DisplayName").
        /// </summary>
        /// <param name="column">Column name.</param>
        /// <param name="displayName">Display name to set.</param>
        /// <returns><c>true</c> on success.</returns>
        public bool SetColumnProperty(string propertyName, string column, string displayName)
            => UpsertExtendedProperty(propertyName, displayName, column);

        public object? GetColumnProperty(string propertyName, string column)
        {
            var meta = GetColumnMetadataEntry(column);
            if (meta != null)
            {
                return propertyName switch
                {
                    "DisplayName" => meta.DisplayName,
                    "Description" => meta.Description,
                    "DefaultUnit" => meta.DefaultUnit,
                    "InputUnit" => meta.InputUnit,
                    "LastUsedUnit" => meta.LastUsedUnit,
                    "ShowUnit" => meta.ShowUnit,
                    "ReportUnit" => meta.ReportUnit,
                    "Format" => meta.Format,
                    "Parameter" => meta.Parameter,
                    "Group" => meta.Group,
                    "Order" => meta.Order,
                    "DatagridShow" => meta.DatagridShow,
                    "ShowInOnePhase" => meta.ShowInOnePhase,
                    "ShowInThreePhase" => meta.ShowInThreePhase,
                    "HideInCrudForm" => meta.HideInCrudForm,
                    "Visible" => meta.Visible,
                    "AddedFromSoftware" => meta.SoftName,
                    _ => ReadExtendedProperty(propertyName, column)
                };
            }

            return ReadExtendedProperty(propertyName, column);
        }

        // Method: GetColumnDisplayName
        /// <summary>
        /// Gets a column's display name from extended properties; falls back to the original column name.
        /// </summary>
        /// <param name="column">Column name.</param>
        /// <returns>Display name string.</returns>
        public string GetColumnDisplayName(string column)
        {
            var meta = GetColumnMetadataEntry(column);
            if (!string.IsNullOrWhiteSpace(meta?.DisplayName))
                return meta.DisplayName!;

            var v = ReadExtendedProperty("DisplayName", column);
            return Convert.ToString(v) ?? column;
        }

        // Method: GetColumnDisplayName
        /// <summary>
        /// Checks column is added by software if it is added by software then return true else false
        /// </summary>
        /// <param name="column">Column name.</param>
        /// <returns>Display name string.</returns>
        public bool CheckColumnIsAddedBySoftware(string column)
        {
            bool isAdded = false;
            string prop = Convert.ToString(ReadExtendedProperty("isAdded", column));
            if (prop != null)
            {
                if (prop == "False")
                {
                    isAdded = false;
                }
                else if (prop == "")
                {
                    isAdded = false;
                }
                else if (prop == "True")
                {
                    isAdded = true;
                }
            }
            return isAdded;
        }

        // Method: RemoveColumnDisplayName
        /// <summary>
        /// Removes a column-level "DisplayName" extended property (no-op if absent).
        /// </summary>
        /// <param name="column">Column name.</param>
        /// <returns><c>true</c> on success.</returns>
        public bool RemoveColumnDisplayName(string column)
            => DropExtendedProperty("DisplayName", column);

        #endregion

        //    #region Unit Setters/Getters
        //    // Method: SetColumnUnit
        //    /// <summary>
        //    /// Sets a column's engineering unit (extended property "Unit").
        //    /// </summary>
        //    /// <param name="column">Column name.</param>
        //    /// <param name="unit">Unit text (e.g., "V", "A", "°C").</param>
        //    /// <returns><c>true</c> on success.</returns>
        //    public bool SetColumnUnit(string column, string unit)
        //=> UpsertExtendedProperty("Unit", unit, column);

        //    // Method: GetColumnUnit
        //    /// <summary>
        //    /// Gets a column's engineering unit (extended property "Unit").
        //    /// </summary>
        //    /// <param name="column">Column name.</param>
        //    /// <returns>Unit string or <c>null</c> if not set.</returns>
        //    public string? GetColumnUnit(string column)
        //        => Convert.ToString(ReadExtendedProperty("Unit", column));

        //    #endregion

        #region Default unit Setters/Getters
        // Method: SetColumnUnit
        /// <summary>
        /// Sets a column's engineering unit (extended property "Unit").
        /// </summary>
        /// <param name="column">Column name.</param>
        /// <param name="unit">Unit text (e.g., "V", "A", "°C").</param>
        /// <returns><c>true</c> on success.</returns>
        public bool SetColumnDefaultUnit(string column, string unit)
    => UpsertExtendedProperty("DefaultUnit", unit, column);

        // Method: GetColumnUnit
        /// <summary>
        /// Gets a column's engineering unit (extended property "Unit").
        /// </summary>
        /// <param name="column">Column name.</param>
        /// <returns>Unit string or <c>null</c> if not set.</returns>
        public string? GetColumnDefaultUnit(string column)
        {
            var meta = GetColumnMetadataEntry(column);
            return !string.IsNullOrWhiteSpace(meta?.DefaultUnit)
                ? meta.DefaultUnit
                : Convert.ToString(ReadExtendedProperty("DefaultUnit", column));
        }

        #endregion

        #region Parameter Setters/Getters
        public bool SetColumnParameter(string column, string unit)
    => UpsertExtendedProperty("Parameter", unit, column);
        public string? GetColumnParameter(string column)
        {
            var meta = GetColumnMetadataEntry(column);
            return !string.IsNullOrWhiteSpace(meta?.Parameter)
                ? meta.Parameter
                : Convert.ToString(ReadExtendedProperty("Parameter", column));
        }

        #endregion

        #region Order Setters/Getters
        // Method: SetColumnFormat
        /// <summary>
        /// Sets a column's display format hint (extended property "Format").
        /// </summary>
        /// <param name="column">Column name.</param>
        /// <param name="format">Format string (e.g., "N2", "P1", "yyyy-MM-dd HH:mm").</param>
        /// <returns><c>true</c> on success.</returns>
        public bool SetOrder(string column, int order)
            => UpsertExtendedProperty("Order", order, column);

        // Method: GetColumnFormat
        /// <summary>
        /// Gets a column's display format hint (extended property "Format").
        /// </summary>
        /// <param name="column">Column name.</param>
        /// <returns>Format string or <c>null</c> if not set.</returns>
        public int? GetOrder(string column)
        {
            var meta = GetColumnMetadataEntry(column);
            if (meta?.Order != null)
                return meta.Order;

            var value = ReadExtendedProperty("Order", column);
            return value == null ? null : Convert.ToInt16(value);
        }

        #endregion

        #region Visibility Setters/Getters
        // Method: SetColumnFormat
        /// <summary>
        /// Sets a column's display format hint (extended property "Format").
        /// </summary>
        /// <param name="column">Column name.</param>
        /// <param name="format">Format string (e.g., "N2", "P1", "yyyy-MM-dd HH:mm").</param>
        /// <returns><c>true</c> on success.</returns>
        public bool SetColumnFormat(string column, string format)
            => UpsertExtendedProperty("Format", format, column);

        // Method: GetColumnFormat
        /// <summary>
        /// Gets a column's display format hint (extended property "Format").
        /// </summary>
        /// <param name="column">Column name.</param>
        /// <returns>Format string or <c>null</c> if not set.</returns>
        public string? GetColumnFormat(string column)
        {
            var meta = GetColumnMetadataEntry(column);
            return !string.IsNullOrWhiteSpace(meta?.Format)
                ? meta.Format
                : Convert.ToString(ReadExtendedProperty("Format", column));
        }

        #endregion

        #region DataGridShow Setters/Getters
        public bool SetShowInDataGrid(string column, bool value)
            => UpsertExtendedProperty("DatagridShow", value, column);

        public bool? GetShowInDataGrid(string column)
        {
            var meta = GetColumnMetadataEntry(column);
            if (meta?.DatagridShow != null)
                return meta.DatagridShow;

            var value = ReadExtendedProperty("DatagridShow", column);
            return value == null ? null : Convert.ToBoolean(value);
        }

        #endregion

        #region DefaultUnit Setters/Getters

        public bool SetDefaultUnit(string column, string? value)
            => UpsertExtendedProperty("DefaultUnit", value ?? string.Empty, column);

        public string GetDefaultUnit(string column)
        {
            var meta = GetColumnMetadataEntry(column);
            if (!string.IsNullOrWhiteSpace(meta?.DefaultUnit))
                return meta.DefaultUnit!;

            return Convert.ToString(ReadExtendedProperty("DefaultUnit", column)) ?? string.Empty;
        }

        #endregion

        #region InputUnit Setters/Getters

        public bool SetInputUnit(string column, string? value)
            => UpsertExtendedProperty("InputUnit", value ?? string.Empty, column);

        public string GetInputUnit(string column)
        {
            var meta = GetColumnMetadataEntry(column);
            if (!string.IsNullOrWhiteSpace(meta?.InputUnit))
                return meta.InputUnit!;

            return Convert.ToString(ReadExtendedProperty("InputUnit", column)) ?? string.Empty;
        }

        #endregion

        #region LastUsedUnit Setters/Getters

        public bool SetLastUsedUnit(string column, string? value)
            => UpsertExtendedProperty("LastUsedUnit", value ?? string.Empty, column);

        public string GetLastUsedUnit(string column)
        {
            var meta = GetColumnMetadataEntry(column);
            if (!string.IsNullOrWhiteSpace(meta?.LastUsedUnit))
                return meta.LastUsedUnit!;

            return Convert.ToString(ReadExtendedProperty("LastUsedUnit", column)) ?? string.Empty;
        }

        #endregion

        #region HideInCrudForm Setters/Getters

        /// <summary>
        /// Sets whether a column should appear in CRUD form.
        /// </summary>
        /// <param name="column">Column name.</param>
        /// <param name="value">True to show in CRUD form; otherwise false.</param>
        /// <returns><c>true</c> on success.</returns>
        public bool SetHideInCrudForm(string column, bool value)
            => UpsertExtendedProperty("HideInCrudForm", value, column);

        /// <summary>
        /// Gets whether a column should appear in CRUD form.
        /// </summary>
        /// <param name="column">Column name.</param>
        /// <returns>True/False if set; otherwise null.</returns>
        public bool? GetHideInCrudForm(string column)
        {
            var meta = GetColumnMetadataEntry(column);
            if (meta?.HideInCrudForm != null)
                return meta.HideInCrudForm;

            var v = ReadExtendedProperty("HideInCrudForm", column);
            if (v == null || v == DBNull.Value) return null;

            var s = Convert.ToString(v)?.Trim();
            if (string.IsNullOrWhiteSpace(s)) return null;

            return s.Equals("1", StringComparison.OrdinalIgnoreCase)
                || s.Equals("true", StringComparison.OrdinalIgnoreCase)
                || s.Equals("yes", StringComparison.OrdinalIgnoreCase);
        }

        private ColumnInfo? GetColumnMetadataEntry(string column)
        {
            var physicalColumn = ResolveColumnName(column);
            return (GetColumns() ?? new List<ColumnInfo>())
                .FirstOrDefault(c => c.Name.Equals(physicalColumn, StringComparison.OrdinalIgnoreCase));
        }

        #endregion

        #region Internal DDL Helpers

        // Method: GetColumns
        /// <summary>
        /// Returns detailed column metadata for the target table, including PK/FK flags, identity,
        /// defaults and selected extended properties, including option lists
        /// (DisplayName, Description, Unit, Format, Order, Visible, Options).
        /// </summary>
        /// <returns>List of <see cref="ColumnInfo"/> items (can be empty).</returns>
        public List<ColumnInfo>? GetColumns()
    => SafeExecute("DDL_GET_COLUMNS", extras =>
    {
        var cacheKey = GetColumnMetadataCacheKey();
        if (ColumnMetadataCache.TryGetValue(cacheKey, out var cached))
        {
            extras["count"] = cached.Count;
            extras["cache"] = "hit";
            return cached;
        }

        EnsureIdent(Schema);
        var physicalTable = ResolveTableName();

        const string sql = @"
SELECT 
    c.name AS ColumnName,
    t.name AS DataType,
    c.max_length AS MaxLength,
    c.precision  AS [Precision],
    c.scale      AS Scale,
    c.is_nullable AS IsNullable,
    COLUMNPROPERTY(c.object_id, c.name, 'IsIdentity') AS IsIdentity,

    -- FK info
    CASE WHEN fkcol.parent_column_id IS NOT NULL THEN 1 ELSE 0 END AS IsForeignKey,
    fk.name AS ForeignKeyName,
    reft.name AS ReferencedTable,
    refc.name AS ReferencedColumn,

    -- Defaults
    dc.definition AS DefaultDefinition,

    -- PK
    CASE WHEN i.is_primary_key = 1 THEN 1 ELSE 0 END AS IsPrimaryKey,
    i.name AS PrimaryKeyName,

    -- ===== Extended Properties =====
    xp.DisplayName,
    xp.[Description],
    xp.[DefaultUnit],
    xp.[InputUnit],
    xp.[LastUsedUnit],
    xp.[ShowUnit],
    xp.[ReportUnit],
    xp.[Format],
    xp.[Parameter],
    xp.[Group],
    xp.[Order],
    xp.[DatagridShow],
    xp.[ShowInOnePhase],
    xp.[ShowInThreePhase],
    xp.[HideInCrudForm],
    xp.[Visible],
    xp.[Options],
    xp.SoftName

FROM sys.columns c
JOIN sys.tables tb ON tb.object_id = c.object_id
JOIN sys.schemas s ON s.schema_id = tb.schema_id
JOIN sys.types t ON t.user_type_id = c.user_type_id

-- FK
LEFT JOIN sys.foreign_key_columns fkcol 
       ON fkcol.parent_object_id = c.object_id 
      AND fkcol.parent_column_id = c.column_id
LEFT JOIN sys.foreign_keys fk 
       ON fk.object_id = fkcol.constraint_object_id
LEFT JOIN sys.tables reft 
       ON reft.object_id = fk.referenced_object_id
LEFT JOIN sys.columns refc 
       ON refc.object_id = fk.referenced_object_id 
      AND refc.column_id = fkcol.referenced_column_id

-- Defaults
LEFT JOIN sys.default_constraints dc
       ON dc.parent_object_id = c.object_id
      AND dc.parent_column_id = c.column_id

-- PK
LEFT JOIN sys.index_columns ic
       ON ic.object_id = c.object_id
      AND ic.column_id = c.column_id
LEFT JOIN sys.indexes i
       ON i.object_id = ic.object_id
      AND i.index_id   = ic.index_id
      AND i.is_primary_key = 1

-- Extended Properties (normalized)
OUTER APPLY (
    SELECT
        MAX(CASE WHEN ep.name='DisplayName' THEN CAST(ep.value AS nvarchar(256)) END) AS DisplayName,
        MAX(CASE WHEN ep.name='Description' THEN CAST(ep.value AS nvarchar(max)) END) AS [Description],
        MAX(CASE WHEN ep.name='DefaultUnit' THEN CAST(ep.value AS nvarchar(64)) END) AS [DefaultUnit],
        MAX(CASE WHEN ep.name='InputUnit' THEN CAST(ep.value AS nvarchar(64)) END) AS [InputUnit],
        MAX(CASE WHEN ep.name='LastUsedUnit' THEN CAST(ep.value AS nvarchar(64)) END) AS [LastUsedUnit],
        MAX(CASE WHEN ep.name='ShowUnit' THEN CAST(ep.value AS nvarchar(64)) END) AS [ShowUnit],
        MAX(CASE WHEN ep.name='ReportUnit' THEN CAST(ep.value AS nvarchar(64)) END) AS [ReportUnit],
        MAX(CASE WHEN ep.name='Format' THEN CAST(ep.value AS nvarchar(64)) END) AS [Format],
        MAX(CASE WHEN ep.name='Parameter' THEN CAST(ep.value AS nvarchar(256)) END) AS [Parameter],
        MAX(CASE WHEN ep.name='Group' THEN CAST(ep.value AS nvarchar(256)) END) AS [Group],
        MAX(CASE WHEN ep.name='Options' THEN CAST(ep.value AS nvarchar(max)) END) AS [Options],
        MAX(TRY_CAST(CASE WHEN ep.name='Order' THEN ep.value END AS int)) AS [Order],

        -- Boolean normalization
        MAX(CASE WHEN ep.name='DatagridShow'
                 THEN CASE WHEN LOWER(CAST(ep.value AS nvarchar(10))) IN ('1','true','yes') THEN 1 ELSE 0 END
            END) AS DatagridShow,

        MAX(CASE WHEN ep.name='ShowInOnePhase'
                 THEN CASE WHEN LOWER(CAST(ep.value AS nvarchar(10))) IN ('1','true','yes') THEN 1 ELSE 0 END
            END) AS ShowInOnePhase,

        MAX(CASE WHEN ep.name='ShowInThreePhase'
                 THEN CASE WHEN LOWER(CAST(ep.value AS nvarchar(10))) IN ('1','true','yes') THEN 1 ELSE 0 END
            END) AS ShowInThreePhase,

        MAX(CASE WHEN ep.name='HideInCrudForm'
                 THEN CASE WHEN LOWER(CAST(ep.value AS nvarchar(10))) IN ('1','true','yes') THEN 1 ELSE 0 END
            END) AS HideInCrudForm,

        MAX(CASE WHEN ep.name='Visible'
                 THEN CASE WHEN LOWER(CAST(ep.value AS nvarchar(10))) IN ('1','true','yes') THEN 1 ELSE 0 END
            END) AS Visible,

        MAX(CASE WHEN ep.name='AddedFromSoftware'
                 THEN CAST(ep.value AS nvarchar(256)) END) AS SoftName

    FROM sys.extended_properties ep
    WHERE ep.major_id = c.object_id AND ep.minor_id = c.column_id
) xp

WHERE tb.name = @t AND s.name = @s
ORDER BY c.column_id;";

        using var cn = Open();
        using var cmd = new SqlCommand(sql, cn);
        cmd.Parameters.AddWithValue("@t", physicalTable);
        cmd.Parameters.AddWithValue("@s", Schema);

        var list = new List<ColumnInfo>();

        using var rd = cmd.ExecuteReader();

        while (rd.Read())
        {
            var ci = new ColumnInfo
            {
                ParentTable = physicalTable,
                Name = rd["ColumnName"].ToString(),
                DataType = rd["DataType"].ToString(),

                MaxLength = rd["MaxLength"] as short? ?? 0,
                Precision = rd["Precision"] as byte? ?? 0,
                Scale = rd["Scale"] as byte? ?? 0,

                Nullable = rd["IsNullable"] as bool? ?? false,
                Identity = Convert.ToInt32(rd["IsIdentity"]) == 1,

                IsPrimaryKey = Convert.ToInt32(rd["IsPrimaryKey"]) == 1,
                IsForeignKey = Convert.ToInt32(rd["IsForeignKey"]) == 1,

                ForeignKeyName = rd["ForeignKeyName"] as string,
                ReferencedTable = rd["ReferencedTable"] as string,
                ReferencedColumn = rd["ReferencedColumn"] as string,

                DefaultSql = rd["DefaultDefinition"] as string,

                DisplayName = rd["DisplayName"] as string,
                Description = rd["Description"] as string,
                DefaultUnit = rd["DefaultUnit"] as string,
                InputUnit = rd["InputUnit"] as string,
                ReportUnit = rd["ReportUnit"] as string,
                ShowUnit = rd["ShowUnit"] as string,
                LastUsedUnit = rd["LastUsedUnit"] as string,
                Format = rd["Format"] as string,
                Parameter = rd["Parameter"] as string,
                Group = rd["Group"] as string,
                Order = rd["Order"] as int?,

                DatagridShow = rd["DatagridShow"] as int? == 1,
                ShowInOnePhase = rd["ShowInOnePhase"] as int? == 1,
                ShowInThreePhase = rd["ShowInThreePhase"] as int? == 1,
                HideInCrudForm = rd["HideInCrudForm"] as int? == 1,   // <-- ADD THIS
                Visible = rd["Visible"] as int? == 1,
                SoftName = rd["SoftName"] as string
            };

            ci.DefaultValue = TryParseDefaultValue(ci.DefaultSql);
            // Resolve column options using Extended Properties if available, otherwise fall back to Check Constraints.
            ResolveColumnOptions(ci);
            ci.Options = ParseOptionsExtendedProperty(rd["Options"] as string);
            ci.HasOptions = (ci.Options?.Length ?? 0) >= 2;
            ci.Unit = "";

            list.Add(ci);
        }

        var ordered = list
            .OrderBy(c => c.Order ?? int.MaxValue)
            .ToList();

        ColumnMetadataCache[cacheKey] = ordered;
        extras["count"] = ordered.Count;
        extras["cache"] = "miss";
        return ordered;
    });

        /// <summary>
        /// Invalidates this table's cached column metadata and reloads it from
        /// SQL Server in one metadata query.
        /// </summary>
        public List<ColumnInfo>? RefreshColumns()
        {
            InvalidateColumnMetadataCache();
            return GetColumns();
        }

        private void ResolveColumnOptions(ColumnInfo ci)
        {
            // 1. Try to load options from the database Extended Properties first
            ci.Options = GetOptionsExtended(ci.Name);

            // 2. Fall back to parsing options from Check Constraints if no Extended Property exists
            if (ci.Options == null || ci.Options.Length < 2)
            {
                ci.Options = TryParseOptionsFromCheck(ci.CheckDefinition, ci.Name);
            }

            ci.HasOptions = (ci.Options?.Length ?? 0) >= 2;
        }

        public DataTable ApplyDisplayNames(DataTable dt)
        {
            if (dt == null) return dt;

            var cols = GetColumns();
            if (cols == null || cols.Count == 0) return dt;

            var displayNameMap = cols
                .Where(c => !string.IsNullOrWhiteSpace(c.DisplayName))
                .ToDictionary(c => c.Name,
                              c => c.DisplayName!,
                              StringComparer.OrdinalIgnoreCase);

            foreach (DataColumn col in dt.Columns)
            {
                if (displayNameMap.TryGetValue(col.ColumnName, out var disp))
                {
                    col.Caption = col.ColumnName;
                    col.ColumnName = disp;
                }
            }

            return dt;
        }

        private DataTable ReorderColumnsByMetadataOrder(DataTable dt)
        {
            var cols = GetColumns();
            if (cols == null || cols.Count == 0 || dt.Columns.Count == 0)
                return dt;

            // Take only columns which exist in DataTable
            var orderedCols = cols
                .Where(c => dt.Columns.Contains(c.Name))
                .OrderBy(c => c.Order ?? int.MaxValue)
                .ThenBy(c => c.Name)
                .ToList();

            int ordinal = 0;

            foreach (var col in orderedCols)
            {
                dt.Columns[col.Name].SetOrdinal(ordinal);
                ordinal++;
            }

            return dt;
        }

        private static object? TryParseDefaultValue(string? defaultSql)
        {
            if (string.IsNullOrWhiteSpace(defaultSql)) return null;

            string s = defaultSql.Trim();
            while (s.StartsWith("(") && s.EndsWith(")") && s.Length >= 2)
                s = s.Substring(1, s.Length - 2).Trim();

            if (s.StartsWith("N'") && s.EndsWith("'") && s.Length >= 3)
                return s.Substring(2, s.Length - 3).Replace("''", "'");

            if (s.StartsWith("'") && s.EndsWith("'") && s.Length >= 2)
                return s.Substring(1, s.Length - 2).Replace("''", "'");

            if (int.TryParse(s, out var i)) return i;
            if (long.TryParse(s, out var l)) return l;
            if (decimal.TryParse(s, out var d)) return d;
            if (bool.TryParse(s, out var b)) return b;

            return s;
        }

        private static string[]? ParseOptionsExtendedProperty(string? optionsText)
        {
            if (string.IsNullOrWhiteSpace(optionsText))
                return null;

            var options = optionsText
                .Split(new[] { ',', ';', '|', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(option => option.Trim())
                .Where(option => !string.IsNullOrWhiteSpace(option))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return options.Length == 0 ? null : options;
        }

        #endregion

        #endregion
    }
}
