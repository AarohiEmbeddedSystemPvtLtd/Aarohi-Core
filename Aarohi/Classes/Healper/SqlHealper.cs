using Aarohi.Classes;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Aarohi.Classes.Healper
{

    public static class SqlHealper
    {
        [Flags]
        public enum PropertyUiFlags
        {
            None = 0,
            Hidden = 1 << 0,   // DONTSHOW
            Required = 1 << 1, // REQUIRED
            ReadOnly = 1 << 2, // READONLY
            Disabled = 1 << 3, // DISABLED
            Optional = 1 << 4, // OPTIONAL
            Dropdown = 1 << 5, // DROPDOWN
        }

        public static List<DataInput> BuildInputs(
            DynamicClass cp,
            DynamicClass CV,
            DynamicClass[] entities,
            Dictionary<string, string[]>? mapOfCombobox)
        {
            var built = new List<DataInput>();

            foreach (var dyn in entities ?? Array.Empty<DynamicClass>())
            {
                if (dyn == null) continue;

                var cols = dyn.GetColumns() ?? new List<DynamicClass.ColumnInfo>();
                foreach (var c in cols)
                {
                    if (ShouldSkipColumn(dyn, c, cp)) continue;

                    var baseName = c.Name;
                    var flags = GetFlags(dyn.Table, baseName, cp);
                    var effType = MapSqlToType(c);

                    DataInput di;
                    var items_dd = new List<string> { "--Select--" };

                    if (flags.HasFlag(PropertyUiFlags.Dropdown) || c.IsForeignKey)
                    {
                        if (mapOfCombobox != null && mapOfCombobox.TryGetValue(baseName, out var mappedValues))
                        {
                            items_dd.AddRange(mappedValues);
                        }
                        else
                        {
                            string type = GetDropDownType(dyn.Table, baseName, cp);

                            if (c.IsForeignKey)
                            {
                                items_dd.AddRange(Evalute_Foreign_Key_Values(c.ReferencedTable!, c.ReferencedColumn!));
                            }
                            else if (type == "Database_Stored_Values")
                            {
                                items_dd.AddRange(Evalute_Database_Stored_Value(dyn.Table, baseName, CV));
                            }
                            else if (type == "Table->Column_Values")
                            {
                                items_dd.AddRange(Evalute_Coloum_Values(dyn.Table, baseName, cp));
                            }
                            else if (type == "Table->Columns")
                            {
                                items_dd.AddRange(Evalute_Coloum_Names(dyn.Table, baseName, cp));
                            }
                        }

                        di = new DataInput(baseName, items_dd.ToArray());
                    }
                    else
                    {
                        di = new DataInput(baseName, effType)
                        {
                            Margin = new Padding(0)
                        };
                    }

                    if (flags.HasFlag(PropertyUiFlags.Required) || c.IsForeignKey || !c.Nullable)
                        di.set_Required();

                    if (flags.HasFlag(PropertyUiFlags.ReadOnly) || flags.HasFlag(PropertyUiFlags.Disabled))
                        di.Enabled = false;

                    built.Add(di);

                }
            }

            return built;
        }

        public static PropertyUiFlags GetFlags(string table, string column, DynamicClass ColumnPermissions)
        {
            var parameters = new Dictionary<string, object?>
            {
                ["@Table"] = table,
                ["@Column"] = column
            };

            var dt = ColumnPermissions.Select(
                "Table_Name=@Table AND Column_Name=@Column",
                parameters
            );

            if (dt == null || dt.Rows.Count == 0) return PropertyUiFlags.None;

            var row = dt.Rows[0];
            var flags = PropertyUiFlags.None;

            if (row.Table.Columns.Contains("Hidden") && row["Hidden"] is bool hidden && hidden) flags |= PropertyUiFlags.Hidden;
            if (row.Table.Columns.Contains("Read_Only") && row["Read_Only"] is bool ro && ro) flags |= PropertyUiFlags.ReadOnly;
            if (row.Table.Columns.Contains("Required") && row["Required"] is bool req && req) flags |= PropertyUiFlags.Required;
            if (row.Table.Columns.Contains("Disabled") && row["Disabled"] is bool dis && dis) flags |= PropertyUiFlags.Disabled;
            if (row.Table.Columns.Contains("Optional") && row["Optional"] is bool opt && opt) flags |= PropertyUiFlags.Optional;
            if (row.Table.Columns.Contains("Dropdown") && row["Dropdown"] is bool dd && dd) flags |= PropertyUiFlags.Dropdown;

            return flags;
        }

        public static string[] Evalute_Coloum_Names(string table, string column, DynamicClass ColumnPermissions)
        {
            var parameters = new Dictionary<string, object?>
            {
                ["@Table"] = table,
                ["@Column"] = column
            };

            var tables = ColumnPermissions.GetColumnValues(
                "Table_Name_DD",
                "Table_Name=@Table AND Column_Name=@Column",
                parameters
            );

            var result = new List<string>();
            foreach (var t in tables)
            {
                var dc = new DynamicClass("dbo", t);
                result.AddRange(dc.GetColumnNames());
            }
            return result.ToArray();
        }

        public static string[] Evalute_Foreign_Key_Values(string table, string column, string? Where = null, Dictionary<string, object?>? Pera = null)
        {
            var dc = new DynamicClass("dbo", table);
            return dc.GetColumnValues(column, Where, Pera);
        }

        public static string[] Evalute_Coloum_Values(string table, string column, DynamicClass Column_Permissions)
        {
            var parameters = new Dictionary<string, object?>
            {
                ["@Table"] = table,
                ["@Column"] = column
            };

            var tables = Column_Permissions.GetColumnValues(
                "Table_Name_DD",
                "Table_Name=@Table AND Column_Name=@Column",
                parameters
            );

            var columns = Column_Permissions.GetColumnValues(
                "Column_Name_DD",
                "Table_Name=@Table AND Column_Name=@Column",
                parameters
            );

            var result = new List<string>();
            foreach (var t in tables)
            {
                var dc = new DynamicClass("dbo", t);
                foreach (var col in columns)
                    result.AddRange(dc.GetColumnValues(col));
            }
            return result.ToArray();
        }

        public static string[] Evalute_Database_Stored_Value(string table, string column, DynamicClass ComboBoxValues)
        {
            List<string> values = new List<string>();

            var parameters = new Dictionary<string, object?>
                                        {
                                            { "@Coloum", column },
                                            { "@Table", table }
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
                        values.Add(row["Iteam_Name"].ToString()!);
                }
            }
            return values.ToArray();
        }

        public static string GetDropDownType(string table, string column, DynamicClass Column_Permissions)
        {
            var parameters = new Dictionary<string, object?>
            {
                ["@Table"] = table,
                ["@Column"] = column
            };

            try
            {
                var arr = Column_Permissions.GetColumnValues(
                    "Dropdown_Type",
                    "Table_Name=@Table AND Column_Name=@Column",
                    parameters
                );
                return arr.Length > 0 ? arr[0] : "";
            }
            catch
            {
                return "";
            }
        }

        public static bool ShouldSkipColumn(DynamicClass dyn, DynamicClass.ColumnInfo c, DynamicClass Column_Permissions)
        {
            var baseName = c.Name;
            var flags = GetFlags(dyn.Table, baseName, Column_Permissions);

            if (string.Equals(baseName, "Id", StringComparison.OrdinalIgnoreCase))
                return true;                    // always skip surrogate Id

            if (flags.HasFlag(PropertyUiFlags.Hidden))
                return true;                    // hide via permissions

            return false;
        }

        public static Type MapSqlToType(DynamicClass.ColumnInfo c)
        {
            var t = c.DataType?.ToLowerInvariant() ?? "";
            bool nullable = c.Nullable;
            Type Wrap(Type core) => nullable && core.IsValueType ? typeof(Nullable<>).MakeGenericType(core) : core;

            return t switch
            {
                "int" => Wrap(typeof(int)),
                "bigint" => Wrap(typeof(long)),
                "smallint" => Wrap(typeof(short)),
                "bit" => Wrap(typeof(bool)),
                "decimal" or "numeric" or "money" or "smallmoney" => Wrap(typeof(decimal)),
                "float" => Wrap(typeof(double)),
                "real" => Wrap(typeof(float)),
                "date" or "datetime" or "datetime2" or "smalldatetime" => Wrap(typeof(DateTime)),
                "uniqueidentifier" => Wrap(typeof(Guid)),
                "varbinary" or "binary" or "image" => typeof(byte[]),
                _ => typeof(string)
            };
        }

        public static Type GetTypeFromString(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
                return typeof(string);

            switch (typeName.Trim().ToLower())
            {
                case "bool":
                case "boolean":
                    return typeof(bool);

                case "int":
                case "integer":
                    return typeof(int);

                case "decimal":
                    return typeof(decimal);

                case "double":
                    return typeof(double);

                case "float":
                    return typeof(float);

                case "string":
                    return typeof(string);

                default:
                    return typeof(string);
            }
        }

        public static object? ConvertTo(Type targetType, object? raw)
        {
            if (raw is null) return null;

            // Unwrap Nullable<T>
            var isNullable = targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Nullable<>);
            var coreType = isNullable ? Nullable.GetUnderlyingType(targetType)! : targetType;

            // Handle strings
            if (raw is string s)
            {
                s = s.Trim();
                if (s.Length == 0 || s == "--Select--") return null;

                if (coreType == typeof(string)) return s;
                if (coreType == typeof(int)) return int.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
                if (coreType == typeof(long)) return long.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
                if (coreType == typeof(short)) return short.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
                if (coreType == typeof(decimal)) return decimal.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
                if (coreType == typeof(double)) return double.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
                if (coreType == typeof(float)) return float.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
                if (coreType == typeof(bool))
                {
                    // accept common truthy forms
                    if (bool.TryParse(s, out var b)) return b;
                    if (s is "1" or "y" or "Y" or "yes" or "Yes" or "true" or "True") return true;
                    if (s is "0" or "n" or "N" or "no" or "No" or "false" or "False") return false;
                    throw new FormatException($"Invalid boolean: {s}");
                }
                if (coreType == typeof(DateTime)) return DateTime.Parse(s, System.Globalization.CultureInfo.CurrentCulture);
                if (coreType == typeof(Guid)) return Guid.Parse(s);
                return s;
            }

            if (coreType.IsInstanceOfType(raw)) return raw;

            try
            {
                return Convert.ChangeType(raw, coreType, System.Globalization.CultureInfo.InvariantCulture);
            }
            catch
            {
                return raw;
            }
        }

        public static BindingList<string> GetBindingListFromTable(DataTable? table, string columnName, bool distinct = false)
        {
            var result = new BindingList<string>();

            if (table == null || string.IsNullOrWhiteSpace(columnName))
                return result;

            if (!table.Columns.Contains(columnName))
                return result;

            IEnumerable<string> values = table.AsEnumerable()
                .Select(r => r[columnName]?.ToString() ?? string.Empty)
                .Where(s => !string.IsNullOrWhiteSpace(s));

            if (distinct)
                values = values.Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var val in values)
                result.Add(val);

            return result;
        }

        public static DataTable MergeTablesSideBySide(params DataTable[] tables)
        {
            if (tables == null || tables.Length == 0)
                throw new ArgumentException("No tables provided.");

            DataTable result = new DataTable();

            foreach (DataTable t in tables)
            {
                foreach (DataColumn col in t.Columns)
                {
                    string colName = col.ColumnName;
                    if (result.Columns.Contains(colName))
                        colName = $"{t.TableName}_{colName}";
                    result.Columns.Add(colName, col.DataType);
                }
            }

            // --- Step 3: Determine max row count ---
            int maxRows = tables.Max(t => t.Rows.Count);

            // --- Step 4: Merge rows side-by-side ---
            for (int i = 0; i < maxRows; i++)
            {
                DataRow newRow = result.NewRow();

                int colOffset = 0;
                foreach (DataTable t in tables)
                {
                    for (int c = 0; c < t.Columns.Count; c++)
                    {
                        string colName = t.Columns[c].ColumnName;
                        if (!result.Columns.Contains(colName))
                            colName = $"{colName}";

                        if (i < t.Rows.Count)
                            newRow[colName] = t.Rows[i][c];
                    }
                    colOffset += t.Columns.Count;
                }

                result.Rows.Add(newRow);
            }

            return result;
        }

        public static DataTable FilterDataTable(DataTable source, string columnName, object value)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (columnName == "")
                return source;

            if (!source.Columns.Contains(columnName))
                throw new ArgumentException($"Column '{columnName}' not found in table '{source.TableName}'.");

            // Create filtered rows using LINQ
            var filteredRows = source.AsEnumerable()
                                     .Where(row => row[columnName]?.ToString() == value?.ToString());

            // Copy matching rows into a new DataTable (preserve schema)
            return filteredRows.Any() ? filteredRows.CopyToDataTable() : source.Clone();
        }

        public static List<string> GetForeignKeyNames(List<DynamicClass.ColumnInfo> columns)
        {
            if (columns == null)
                throw new ArgumentNullException(nameof(columns));

            // Extract names of columns where IsForeignKey == true
            return columns
                .Where(c => c.IsForeignKey)
                .Select(c => c.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

    }
}

