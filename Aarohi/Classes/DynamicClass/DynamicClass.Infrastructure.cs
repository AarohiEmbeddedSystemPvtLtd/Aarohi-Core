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

        #region Helpers / infrastructure

        // Method: GetColumnNames
        /// <summary>
        /// Returns user-facing column names sorted by display name. If <paramref name="tablenameWant"/> is true,
        /// each entry is prefixed with the table display name (e.g., "Table → Column").
        /// </summary>
        /// <param name="tablenameWant">Whether to prefix names with the table display name.</param>
        /// <returns>Array of friendly names.</returns>
        public string[] GetColumnNames(bool tablenameWant = true)
        {
            var cols = GetColumns() ?? new List<ColumnInfo>();
            var tableVerbose = tablenameWant ? GetTableDisplayName() : string.Empty;

            var names = cols
                .Select(c => new
                {
                    Orig = c.Name,
                    Verbose = string.IsNullOrWhiteSpace(c.DisplayName) ? c.Name : c.DisplayName
                })
                .OrderBy(x => x.Verbose, StringComparer.OrdinalIgnoreCase)
                .Select(x => tablenameWant ? $"{tableVerbose} -> {x.Verbose}" : x.Verbose)
                .ToArray();

            return names;
        }

        // Method: GetColumnNamesOrignal
        /// <summary>
        /// Returns the original database column names in case-insensitive sorted order.
        /// </summary>
        /// <returns>Array of original column names.</returns>
        public string[] GetColumnNamesOrignal()
        {
            var columns = (GetColumns() ?? new List<ColumnInfo>())
                .Select(c => c.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .Select(n =>
                {
                    return n;
                });

            return columns.ToArray();
        }

        private static bool ValueEquals(object? a, object? b)
        {
            if (a == null || a == DBNull.Value) a = null;
            if (b == null || b == DBNull.Value) b = null;
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;

            // Normalize numbers & dates to strings for stable compare
            var ta = a.GetType();
            var tb = b.GetType();
            if (IsNumericType(ta.Name) && IsNumericType(tb.Name))
                return Convert.ToDecimal(a).Equals(Convert.ToDecimal(b));

            if (a is DateTime da && b is DateTime db)
                return da.ToString("O").Equals(db.ToString("O"), StringComparison.Ordinal);

            return string.Equals(Convert.ToString(a), Convert.ToString(b), StringComparison.Ordinal);
        }

        private DataRow? GetRowByKey(SqlConnection cn, object? keyVal)
        {
            var physicalTable = ResolveTableName(cn);
            var columns = GetColumns() ?? new List<ColumnInfo>();
            var physicalKeyColumn = ResolveKeyColumnName(columns);

            var sql = $"SELECT TOP (1) * FROM [{Schema}].[{physicalTable}] WHERE {Q(physicalKeyColumn)} = @k";
            using var da = new SqlDataAdapter(sql, cn);
            da.SelectCommand!.Parameters.AddWithValue("@k", keyVal ?? DBNull.Value);
            var dt = new DataTable();
            da.Fill(dt);
            return dt.Rows.Count > 0 ? dt.Rows[0] : null;
        }

        private string BuildChangeSummary(DataRow existing)
        {
            var columns = GetColumns() ?? new List<ColumnInfo>();
            var physicalValues = ResolveValuesToPhysicalColumns(columns);
            var physicalKeyColumn = ResolveKeyColumnName(columns);
            var lines = new List<string>();

            foreach (var kv in physicalValues)
            {
                var col = kv.Key;
                if (col.Equals(physicalKeyColumn, StringComparison.OrdinalIgnoreCase)) continue; // skip PK
                if (!existing.Table.Columns.Contains(col)) continue;

                var oldVal = existing[col];
                var newVal = kv.Value ?? DBNull.Value;

                if (!ValueEquals(oldVal, newVal))
                {
                    string oldS = oldVal == DBNull.Value ? "NULL" : Convert.ToString(oldVal) ?? "";
                    string newS = newVal == DBNull.Value ? "NULL" : Convert.ToString(newVal) ?? "";
                    lines.Add($"{col}: {oldS}  →  {newS}");
                }
            }

            if (lines.Count == 0) return "(No changes detected)";
            // keep the box reasonable
            const int maxLines = 15;
            if (lines.Count > maxLines)
            {
                var head = lines.Take(maxLines).ToList();
                head.Add($"… (+{lines.Count - maxLines} more)");
                return string.Join(Environment.NewLine, head);
            }
            return string.Join(Environment.NewLine, lines);
        }

        public sealed class ColumnDef
        {
            public string Name { get; set; } = "";
            public string SqlType { get; set; } = "nvarchar(max)";
            public bool Nullable { get; set; } = true;
            public bool Identity { get; set; } = false;
            public bool PrimaryKey { get; set; } = false;

            public void Validate()
            {
                if (string.IsNullOrWhiteSpace(Name)) throw new ArgumentException("ColumnDef.Name required.");
                if (string.IsNullOrWhiteSpace(SqlType)) throw new ArgumentException("ColumnDef.SqlType required.");
            }
        }

        public sealed class ColumnInfo
        {
            public string ParentTable { get; set; }
            public bool IsPrimaryKey { get; set; }
            public string Name { get; set; } = ""; // Want
            public string DataType { get; set; } = ""; // want
            public short MaxLength { get; set; }
            public byte Precision { get; set; } // want
            public byte Scale { get; set; } // want
            public bool Nullable { get; set; }
            public bool Identity { get; set; }
            public string? DefaultSql { get; set; }
            public object? DefaultValue { get; set; }
            public string? CheckDefinition { get; set; }
            public string? SoftUsedName { get; set; }
            public string? SoftName { get; set; }
            public string[]? Options { get; set; }
            public bool HasOptions { get; set; }

            // FK:
            public bool IsForeignKey { get; set; }
            public string? ForeignKeyName { get; set; }
            public string? ReferencedTable { get; set; }
            public string? ReferencedColumn { get; set; }

            // Verbose // want all
            public string? DisplayName { get; set; }
            public string? Description { get; set; }
            public string? Unit { get; set; }
            public string? DefaultUnit { get; set; }
            public string? InputUnit { get; set; }
            public string? LastUsedUnit { get; set; }
            public string? ShowUnit { get; set; }
            public string? ReportUnit { get; set; }
            public bool? DatagridShow { get; set; }
            public bool? HideInCrudForm { get; set; }
            public string? Format { get; set; }
            public string? Parameter { get; set; }
            public int? Order { get; set; }
            public bool? Visible { get; set; }

            // Meta
            public Dictionary<string, object?> CustomProperties { get; set; } = new();
        }

        public bool SetCustomColumnProperty(string column, string propertyName, object? value)
    => UpsertExtendedProperty(propertyName, value, column);

        public object? GetCustomColumnProperty(string column, string propertyName)
            => ReadExtendedProperty(propertyName, column);

        public string? GetCustomColumnPropertyString(string column, string propertyName)
            => Convert.ToString(ReadExtendedProperty(propertyName, column));

        public bool? GetCustomColumnPropertyBool(string column, string propertyName)
        {
            var v = ReadExtendedProperty(propertyName, column);
            if (v == null || v == DBNull.Value) return null;

            var s = Convert.ToString(v)?.Trim();
            if (string.IsNullOrWhiteSpace(s)) return null;

            if (bool.TryParse(s, out bool b))
                return b;

            if (s == "1") return true;
            if (s == "0") return false;

            return null;
        }

        public int? GetCustomColumnPropertyInt(string column, string propertyName)
        {
            var v = ReadExtendedProperty(propertyName, column);
            if (v == null || v == DBNull.Value) return null;

            if (int.TryParse(Convert.ToString(v), out int n))
                return n;

            return null;
        }

        public static bool ThrowOnError { get; set; } = true;

        private T SafeExecute<T>(string op, Func<Dictionary<string, object>, T> body)
        {
            var ctx = NewExtras(op);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                LastErrorMessage = null;
                LastException = null;

                var result = body(ctx);

                sw.Stop();
                ctx["durationMs"] = sw.ElapsedMilliseconds;
                return result;
            }
            catch (Exception ex)
            {
                sw.Stop();
                LastErrorMessage = ex.Message;
                LastException = ex;

                ctx["durationMs"] = sw.ElapsedMilliseconds;
                ctx["error"] = ex.Message;
                ctx["exception"] = ex.GetType().FullName ?? "";

                WriteLog(LogLevel.Error, $"{op} failed", LogSource, ex, ctx);

                if (ThrowOnError) throw;
                return default!;
            }
        }

        // ---------- Async infrastructure ----------
        public delegate Task<bool> ConfirmUpdateAsync(string changeSummary, CancellationToken ct);

        private async Task<SqlConnection> OpenAsync(CancellationToken ct = default)
        {
            var cn = ResolveFactory().Invoke();
            if (cn.State != ConnectionState.Open)
                await cn.OpenAsync(ct).ConfigureAwait(false);
            return cn;
        }

        private async Task<T> SafeExecuteAsync<T>(string op, Func<Dictionary<string, object>, Task<T>> bodyAsync)
        {
            var ctx = NewExtras(op);
            var sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                LastErrorMessage = null;
                LastException = null;

                var result = await bodyAsync(ctx).ConfigureAwait(false);

                sw.Stop();
                ctx["durationMs"] = sw.ElapsedMilliseconds;

                return result;
            }
            catch (Exception ex)
            {
                sw.Stop();
                LastErrorMessage = ex.Message;
                LastException = ex;

                ctx["durationMs"] = sw.ElapsedMilliseconds;
                ctx["error"] = ex.Message;
                ctx["exception"] = ex.GetType().FullName ?? "";

                WriteLog(LogLevel.Error, $"{op} failed", LogSource, ex, ctx);
                return default!;
            }
        }


        private Dictionary<string, object> NewExtras(string op) => new(StringComparer.OrdinalIgnoreCase)
        {
            ["operation"] = op,
            ["schema"] = Schema,
            ["table"] = Table,
            ["keyColumn"] = KeyColumn,
            ["timestamp"] = DateTimeOffset.Now.ToString("o")
        };

        private void AddCommonExtras(Dictionary<string, object> extras, params (string key, object? val)[] pairs)
        {
            foreach (var (k, v) in pairs) extras[k] = v ?? "";
        }

        private SqlConnection Open()
        {
            var cn = ResolveFactory().Invoke();

            if (cn.State != ConnectionState.Open)
                cn.Open();

            return cn;
        }

        private static readonly Regex ValidIdent = new(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);
        private static void EnsureIdent(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || !ValidIdent.IsMatch(name))
                throw new ArgumentException($"Invalid identifier: {name}");
        }
        private static string Q(string ident) { EnsureIdent(ident); return $"[{ident}]"; }

        private static bool TableExists(SqlConnection cn, SqlTransaction tx, string schema, string table)
        {
            using var cmd = new SqlCommand(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id=s.schema_id WHERE t.name=@t AND s.name=@s) THEN 1 ELSE 0 END",
                cn, tx);
            cmd.Parameters.AddWithValue("@t", table);
            cmd.Parameters.AddWithValue("@s", schema);
            return Convert.ToInt32(cmd.ExecuteScalar()) == 1;
        }

        private static void CreateTableInternal(SqlConnection cn, SqlTransaction tx, string schema, string table, Dictionary<string, ColumnDef> spec, string pkName)
        {
            foreach (var kv in spec) kv.Value.Validate();

            var defs = spec.Values.Select(c =>
                $"[{c.Name}] {c.SqlType}{(c.Identity ? " IDENTITY(1,1)" : "")} {(c.Nullable ? "NULL" : "NOT NULL")}");
            var pk = spec.Values.FirstOrDefault(x => x.PrimaryKey)
                     ?? (spec.TryGetValue(pkName, out var d) ? d : null);
            var pkSql = pk is null ? "" : $", CONSTRAINT PK_{table} PRIMARY KEY ([{pk.Name}])";

            var sql = $"CREATE TABLE [{schema}].[{table}] (\n  {string.Join(",\n  ", defs)}{pkSql}\n);";
            using var cmd = new SqlCommand(sql, cn, tx);
            cmd.ExecuteNonQuery();
        }

        private static void EnsureColumnsInternal(SqlConnection cn, SqlTransaction tx, string schema, string table, Dictionary<string, ColumnDef> spec)
        {
            foreach (var kv in spec)
            {
                var c = kv.Value; c.Validate();

                using var check = new SqlCommand("SELECT 1 WHERE COL_LENGTH(@f,@c) IS NOT NULL", cn, tx);
                check.Parameters.AddWithValue("@f", $"{schema}.{table}");
                check.Parameters.AddWithValue("@c", c.Name);
                var exists = false;
                using (var rd = check.ExecuteReader()) { exists = rd.Read(); }

                if (!exists)
                {
                    using var add = new SqlCommand(
                        $"ALTER TABLE [{schema}].[{table}] ADD [{c.Name}] {c.SqlType}{(c.Identity ? " IDENTITY(1,1)" : "")} {(c.Nullable ? "NULL" : "NOT NULL")};",
                        cn, tx);
                    add.ExecuteNonQuery();
                }
            }
        }

        private static bool IsEmptyKey(object? keyVal)
        {
            if (keyVal is null || keyVal == DBNull.Value) return true;
            if (keyVal is int i) return i == 0;
            if (keyVal is long l) return l == 0;
            if (keyVal is short s) return s == 0;
            if (keyVal is string str) return string.IsNullOrWhiteSpace(str);
            return false;
        }

        private static int ResolveBulkCopyTimeoutSeconds(int? bulkCopyTimeoutSeconds)
        {
            var timeout = bulkCopyTimeoutSeconds ?? BulkCopyTimeoutSeconds;
            if (timeout < 0)
                throw new ArgumentOutOfRangeException(nameof(bulkCopyTimeoutSeconds), "Bulk copy timeout must be 0 or greater.");

            return timeout;
        }

        private static void AddParam(SqlParameterCollection ps, string name, object? value)
        {
            var p = ps.AddWithValue(name, value ?? DBNull.Value);
            if (value is int or long or short or byte) p.SqlDbType = SqlDbType.Int;
            else if (value is decimal) p.SqlDbType = SqlDbType.Decimal;
            else if (value is double or float) p.SqlDbType = SqlDbType.Float;
            else if (value is bool) p.SqlDbType = SqlDbType.Bit;
            else if (value is DateTime) p.SqlDbType = SqlDbType.DateTime2;
            else p.SqlDbType = SqlDbType.NVarChar;
        }

        public static Type MapSqlTypeToCSharp(string sqlType, bool isNullable)
        {
            if (string.IsNullOrWhiteSpace(sqlType))
                return typeof(string);

            sqlType = sqlType.ToLowerInvariant();

            Type type = sqlType switch
            {
                "bigint" => typeof(long),
                "binary" => typeof(byte[]),
                "bit" => typeof(bool),
                "char" => typeof(string),
                "date" => typeof(DateTime),
                "datetime" => typeof(DateTime),
                "datetime2" => typeof(DateTime),
                "datetimeoffset" => typeof(DateTimeOffset),
                "decimal" => typeof(decimal),
                "float" => typeof(double),
                "image" => typeof(byte[]),
                "int" => typeof(int),
                "money" => typeof(decimal),
                "nchar" => typeof(string),
                "ntext" => typeof(string),
                "numeric" => typeof(decimal),
                "nvarchar" => typeof(string),
                "real" => typeof(float),
                "smalldatetime" => typeof(DateTime),
                "smallint" => typeof(short),
                "smallmoney" => typeof(decimal),
                "text" => typeof(string),
                "time" => typeof(TimeSpan),
                "timestamp" => typeof(byte[]),
                "tinyint" => typeof(byte),
                "uniqueidentifier" => typeof(Guid),
                "varbinary" => typeof(byte[]),
                "varchar" => typeof(string),
                "xml" => typeof(string),
                _ => typeof(string) // default fallback
            };

            // if nullable type is needed
            if (isNullable && type.IsValueType)
                return typeof(Nullable<>).MakeGenericType(type);

            return type;
        }

        public static string MapSqlTypeToCSharpString(string sqlType, bool isNullable)
        {
            if (string.IsNullOrWhiteSpace(sqlType))
                return "string";

            sqlType = sqlType.ToLowerInvariant();

            string type = sqlType switch
            {
                "bigint" => "long",
                "binary" => "byte[]",
                "bit" => "bool",
                "char" => "string",
                "date" => "DateTime",
                "datetime" => "DateTime",
                "datetime2" => "DateTime",
                "datetimeoffset" => "DateTimeOffset",
                "decimal" => "decimal",
                "float" => "double",
                "image" => "byte[]",
                "int" => "int",
                "money" => "decimal",
                "nchar" => "string",
                "ntext" => "string",
                "numeric" => "decimal",
                "nvarchar" => "string",
                "real" => "float",
                "smalldatetime" => "DateTime",
                "smallint" => "short",
                "smallmoney" => "decimal",
                "text" => "string",
                "time" => "TimeSpan",
                "timestamp" => "byte[]",
                "tinyint" => "byte",
                "uniqueidentifier" => "Guid",
                "varbinary" => "byte[]",
                "varchar" => "string",
                "xml" => "string",
                _ => "string" // default fallback
            };

            if (isNullable &&
                type != "string" &&
                type != "byte[]" &&
                type != "object")
            {
                return type + "?";
            }

            return type;
        }

        private void ValidateValuesAgainstTable()
        {
            var meta = GetColumns() ?? new List<ColumnInfo>();
            var allowed = new HashSet<string>(meta.Select(c => c.Name), StringComparer.OrdinalIgnoreCase);

            var unknown = Values.Keys.Where(k => !allowed.Contains(k)).ToList();
            if (unknown.Count > 0)
                throw new InvalidOperationException(
                    $"Unknown column(s) for [{Schema}].[{Table}]: {string.Join(", ", unknown)}");

            // Optional: NOT NULL without default/identity check
            var required = meta.Where(c => !c.Nullable && !c.Identity && c.DefaultSql == null)
                               .Select(c => c.Name)
                               .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var missingRequired = required
                .Where(r => !ValuesContainColumn(r, meta) || !TryGetValueForColumn(r, meta, out var value) || value is null)
                .ToList();
            if (missingRequired.Count > 0)
                throw new InvalidOperationException(
                    $"Missing required column(s) for [{Schema}].[{Table}]: {string.Join(", ", missingRequired)}");
        }

        private SqlDbType GetSqlDbType(string type)
        {
            switch (type.ToLower())
            {
                case "int": return SqlDbType.Int;
                case "bigint": return SqlDbType.BigInt;
                case "smallint": return SqlDbType.SmallInt;
                case "tinyint": return SqlDbType.TinyInt;

                case "bit": return SqlDbType.Bit;

                case "decimal":
                case "numeric": return SqlDbType.Decimal;

                case "float": return SqlDbType.Float;
                case "real": return SqlDbType.Real;

                case "date": return SqlDbType.Date;
                case "datetime": return SqlDbType.DateTime;
                case "datetime2": return SqlDbType.DateTime2;
                case "time": return SqlDbType.Time;

                case "uniqueidentifier": return SqlDbType.UniqueIdentifier;

                case "varchar": return SqlDbType.VarChar;
                case "nvarchar": return SqlDbType.NVarChar;
                case "char": return SqlDbType.Char;
                case "nchar": return SqlDbType.NChar;

                case "text": return SqlDbType.Text;
                case "ntext": return SqlDbType.NText;

                case "varbinary": return SqlDbType.VarBinary;

                default: return SqlDbType.Variant;
            }
        }

        public static bool FitsDecimal(decimal value, byte precision, byte scale)
        {
            value = Math.Abs(decimal.Round(value, scale, MidpointRounding.AwayFromZero));

            string s = value.ToString(System.Globalization.CultureInfo.InvariantCulture);

            if (s.Contains("."))
            {
                var parts = s.Split('.');
                int before = parts[0].TrimStart('0').Length;
                int after = parts[1].Length;

                if (parts[0] == "0") before = 0;

                return before <= (precision - scale) && after <= scale;
            }
            else
            {
                int before = s.TrimStart('0').Length;
                if (s == "0") before = 0;

                return before <= (precision - scale);
            }
        }
        #endregion

    }
}
