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
    public sealed class DynamicClass : IDisposable
    {

        #region Fields
        // ---------- Global config ----------
        public static bool LogInfo { get; set; } = true;
        public static bool LogTrace { get; set; } = false;

        // ---------- Core properties ----------
        public string Schema { get; set; } = "dbo";
        public string Table { get; set; } = "";
        public string KeyColumn { get; set; } = "";
        public string LogSource { get; set; } = "DynamicClass";

        private Func<SqlConnection>? _instanceFactory;

        public Func<SqlConnection>? InstanceConnectionFactory
        {
            get => Volatile.Read(ref _instanceFactory);
            set
            {
                if (value is null) throw new ArgumentNullException(nameof(InstanceConnectionFactory));
                if (Interlocked.CompareExchange(ref _instanceFactory, value, null) != null)
                    throw new InvalidOperationException("InstanceConnectionFactory already set for this instance.");
            }
        }

        private static Func<SqlConnection>? _factory;
        public static Func<SqlConnection>? ConnectionFactory
        {
            get => Volatile.Read(ref _factory) ?? throw new InvalidOperationException("DefaultConnectionFactory is not set.");
            set
            {
                if (value is null) throw new ArgumentNullException(nameof(ConnectionFactory));
                if (Interlocked.CompareExchange(ref _factory, value, null) != null)
                    throw new InvalidOperationException("InstanceConnectionFactory already set for this instance.");
            }
        }

        private Func<SqlConnection> ResolveFactory()
        {
            return _instanceFactory
                ?? ConnectionFactory
                ?? throw new InvalidOperationException(
                    "DynamicClass.ConnectionFactory is not set. Set default at startup or set InstanceConnectionFactory for this instance.");
        }

        public Dictionary<string, object?> Values { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, ColumnDef> SchemaSpec { get; } = new(StringComparer.OrdinalIgnoreCase);

        public string? LastErrorMessage { get; private set; }
        public Exception? LastException { get; private set; }

        #endregion

        #region Constructors and Disposers

        /// <summary>
        /// Initializes a new <see cref="DynamicClass"/> with default settings.
        /// </summary>
        public DynamicClass() { }

        /// <summary>
        /// Initializes a new <see cref="DynamicClass"/> targeting a specific table.
        /// If <paramref name="keyColumn"/> is null or empty, attempts to auto-detect
        /// the key column (PK preferred; falls back to identity if enabled).
        /// </summary>
        /// <param name="schema">Database schema name (e.g., "dbo").</param>
        /// <param name="table">Database table name.</param>
        /// <param name="keyColumn">Optional key column name.</param>
        public DynamicClass(string schema, string table, string? keyColumn = null)
        {
            Schema = schema;
            Table = table;
            KeyColumn = keyColumn?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(KeyColumn))
            {
                DetectAndSetKeyColumn(preferIdentityFallback: true, throwOnComposite: false);
            }
        }

        /// <summary>
        /// Releases transient state (last error, values, schema spec) and suppresses finalization.
        /// </summary>
        public void Dispose()
        {
            if (LastException != null)
            {
                LastException = null;
            }

            Values.Clear();
            SchemaSpec.Clear();

            GC.SuppressFinalize(this);
        }
        #endregion

        #region DML (ROW CRUD)

        /// <summary>
        /// Detects the appropriate key column and assigns it to <see cref="KeyColumn"/>.
        /// Prefers primary key; if none and <paramref name="preferIdentityFallback"/> is true,
        /// falls back to a single identity column. Logs the result.
        /// </summary>
        /// <param name="preferIdentityFallback">When true, use a single identity column if no PK is found.</param>
        /// <param name="throwOnComposite">When true, throws if a composite PK exists.</param>
        /// <returns><c>true</c> if a single key column was set; otherwise <c>false</c>.</returns>
        /// <exception cref="InvalidOperationException">Thrown when a composite PK is detected and <paramref name="throwOnComposite"/> is true.</exception>
        public bool DetectAndSetKeyColumn(bool preferIdentityFallback = true, bool throwOnComposite = false)
        {
            var pkCols = GetPrimaryKeyColumns();

            if (pkCols.Length == 1)
            {
                KeyColumn = pkCols[0];
                _logger.Debug("Auto-detected primary key column", LogSource, new Dictionary<string, object>
                {
                    ["table"] = Table,
                    ["schema"] = Schema,
                    ["keyColumn"] = KeyColumn
                });
                return true;
            }

            if (pkCols.Length > 1)
            {
                if (throwOnComposite)
                    throw new InvalidOperationException($"Composite primary key detected on [{Schema}].[{Table}] ({string.Join(",", pkCols)}). DynamicClass supports a single KeyColumn only.");
                KeyColumn = "";
                _logger.Debug("Composite primary key detected; KeyColumn left empty", LogSource, new Dictionary<string, object>
                {
                    ["table"] = Table,
                    ["schema"] = Schema,
                    ["pkColumns"] = string.Join(",", pkCols)
                });
                return false;
            }

            if (preferIdentityFallback)
            {
                var cols = GetColumns() ?? new List<ColumnInfo>();
                var idCols = cols.Where(c => c.Identity)
                                 .Select(c => c.Name)
                                 .Distinct(StringComparer.OrdinalIgnoreCase)
                                 .ToArray();

                if (idCols.Length == 1)
                {
                    KeyColumn = idCols[0];
                    _logger.Info("No PK found; fell back to identity column", LogSource, new Dictionary<string, object>
                    {
                        ["table"] = Table,
                        ["schema"] = Schema,
                        ["keyColumn"] = KeyColumn
                    });
                    return true;
                }
            }

            KeyColumn = "";
            _logger.Info("No PK/identity found; KeyColumn left empty", LogSource, new Dictionary<string, object>
            {
                ["table"] = Table,
                ["schema"] = Schema
            });
            return false;
        }

        // Method: GetPrimaryKeyColumns
        /// <summary>
        /// Returns the names of primary key columns defined on the target table.
        /// </summary>
        /// <returns>Array of PK column names (can be empty or contain multiple for composite keys).</returns>

        public string[] GetPrimaryKeyColumns()
        {
            var cols = GetColumns() ?? new List<ColumnInfo>();
            return cols.Where(c => c.IsPrimaryKey)
                       .Select(c => c.Name)
                       .Distinct(StringComparer.OrdinalIgnoreCase)
                       .ToArray();
        }

        // Method: Insert
        /// <summary>
        /// Inserts a new row using <see cref="Values"/> as column/value pairs.
        /// If the configured <see cref="KeyColumn"/> is an identity, returns the new identity value.
        /// Otherwise returns the provided key (if supplied) or <c>null</c>.
        /// </summary>
        /// <returns>New identity value, provided key, or <c>null</c>.</returns>
        /// <exception cref="InvalidOperationException">Thrown if <see cref="Values"/> is empty.</exception>
        public object? Insert() => SafeExecute("INSERT", extras =>
        {
            EnsureIdent(Schema);
            EnsureIdent(Table);

            if (Values.Count == 0)
                throw new InvalidOperationException("No Values to insert.");

            var cols = Values.Keys.ToArray();
            var qCols = cols.Select(QSafe).ToArray();
            var parNames = cols.Select((_, i) => $"@p{i}").ToArray();

            ValidateValuesAgainstTable();

            var identitySql = string.IsNullOrWhiteSpace(KeyColumn)
                ? ""
                : $"IF COLUMNPROPERTY(object_id('{Schema}.{Table}'), '{KeyColumn}', 'IsIdentity')=1 SELECT SCOPE_IDENTITY();";

            var sql = $"INSERT INTO [{Schema}].[{Table}] ({string.Join(",", qCols)}) " +
                      $"VALUES ({string.Join(",", parNames)}); {identitySql}";

            using var cn = Open();
            using var cmd = new SqlCommand(sql, cn);

            List<ColumnInfo> infos = GetColumns();

            for (int i = 0; i < cols.Length; i++)
            {
                string colName = cols[i];
                object? val = Values[colName];

                if (val is string s && string.IsNullOrWhiteSpace(s))
                    val = DBNull.Value;

                ColumnInfo info = infos.FirstOrDefault(x => x.Name.Equals(colName, StringComparison.OrdinalIgnoreCase));

                if (info == null)
                    throw new Exception($"Column metadata not found for {colName}");

                string dataType = info.DataType;
                var param = cmd.Parameters.Add(parNames[i], GetSqlDbType(dataType));

                if (dataType.Equals("decimal", StringComparison.OrdinalIgnoreCase) ||
    dataType.Equals("numeric", StringComparison.OrdinalIgnoreCase))
                {
                    param.Precision = Convert.ToByte(info.Precision);
                    param.Scale = Convert.ToByte(info.Scale);

                    if (val == null || val == DBNull.Value)
                    {
                        param.Value = DBNull.Value;
                    }
                    else
                    {
                        decimal decVal = Convert.ToDecimal(val);

                        if (!FitsDecimal(decVal, param.Precision, param.Scale))
                        {
                            throw new Exception(
                                $"Value '{decVal}' does not fit column '{colName}' decimal({param.Precision},{param.Scale}).");
                        }

                        param.Value = decVal;
                    }
                }
                else
                {
                    param.Value = val ?? DBNull.Value;
                }
            }

            AddCommonExtras(extras,
                ("sql", sql),
                ("columns", string.Join(",", cols)),
                ("valuesCount", cols.Length));

            if (string.IsNullOrWhiteSpace(identitySql))
            {
                cmd.ExecuteNonQuery();

                if (!string.IsNullOrWhiteSpace(KeyColumn) &&
                    Values.TryGetValue(KeyColumn, out var providedKey) &&
                    !IsEmptyKey(providedKey))
                {
                    return providedKey;
                }

                return null;
            }
            else
            {
                var id = cmd.ExecuteScalar();
                return id == DBNull.Value ? null : id;
            }
        });

        // Method: UpdateByKey
        /// <summary>
        /// Updates an existing row identified by <see cref="KeyColumn"/> in <see cref="Values"/>.
        /// Only changed columns are updated (diff performed against the database row).
        /// Shows a diagnostic message if no rows are affected.
        /// </summary>
        /// <returns>Number of affected rows (0 when nothing changed or row does not exist).</returns>
        /// <exception cref="InvalidOperationException">Thrown when key value is missing from <see cref="Values"/>.</exception>

        public int UpdateByKey()
        {
            EnsureIdent(Schema); EnsureIdent(Table); EnsureIdent(KeyColumn);

            if (!Values.TryGetValue(KeyColumn, out var keyVal))
                throw new InvalidOperationException($"Values must include '{KeyColumn}' for Update.");

            using var cn = Open();

            var existing = GetRowByKey(cn, keyVal);
            if (existing == null)
                return 0; // no such row

            var changedCols = new List<string>();
            foreach (var c in Values.Keys)
            {
                if (c.Equals(KeyColumn, StringComparison.OrdinalIgnoreCase)) continue;
                if (!existing.Table.Columns.Contains(c)) continue;

                var oldVal = existing[c];
                var newVal = Values[c] ?? DBNull.Value;

                if (!ValueEquals(oldVal, newVal)) // helper from earlier message
                    changedCols.Add(c);
            }

            if (changedCols.Count == 0)
            {
                return 0;
            }
            ValidateValuesAgainstTable();
            var sets = string.Join(",", changedCols.Select(c => $"{Q(c)}=@{c}"));
            var sql = $"UPDATE [{Schema}].[{Table}] SET {sets} WHERE {Q(KeyColumn)}=@__key";

            using var cmd = new SqlCommand(sql, cn);

            // Bind only changed columns
            foreach (var c in changedCols)
                cmd.Parameters.AddWithValue("@" + c, Values[c] ?? DBNull.Value);

            cmd.Parameters.AddWithValue("@__key", keyVal ?? DBNull.Value);

            int affected;

            try
            {
                affected = cmd.ExecuteNonQuery();
            }
            catch (SqlException ex)
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine(ex.Message);
                sb.AppendLine("---- SQL ----");
                sb.AppendLine(cmd.CommandText);
                sb.AppendLine("---- Params ----");

                foreach (SqlParameter p in cmd.Parameters)
                {
                    string val = (p.Value == null || p.Value == DBNull.Value) ? "NULL" : p.Value.ToString();
                    string type = (p.Value == null) ? "null" : p.Value.GetType().FullName;

                    sb.AppendLine($"{p.ParameterName} = {val}   ({type})");
                }

                MessageBox.Show(sb.ToString(), "Update Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);

                throw;
            }

            return affected;


            if (affected == 0)
            {
                var after = GetRowByKey(cn, keyVal);
                var deltas = new List<string>();
                foreach (var c in changedCols)
                {
                    var beforeS = existing[c] == DBNull.Value ? "NULL" : Convert.ToString(existing[c]) ?? "";
                    var afterS = after != null && after.Table.Columns.Contains(c)
                                    ? after[c] == DBNull.Value ? "NULL" : Convert.ToString(after[c]) ?? ""
                                    : "(missing)";
                    var newS = Values[c] == null ? "NULL" : Convert.ToString(Values[c]) ?? "";

                    deltas.Add($"{c}: before={beforeS}, after={afterS}, attempted={newS}");
                }

                MessageBox.Show(
                    "Update executed but reported 0 rows affected.\n\n" +
                    string.Join(Environment.NewLine, deltas) +
                    "\n\nPossible causes: INSTEAD OF trigger, equal values after normalization, or constraints.",
                    "Diagnostics",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }

            return affected;
        }

        public (int Inserted, int Updated) BulkUpsert(
            DataTable dt,
            string? keyColumn = null,
            int batchSize = 2000,
            bool useTransaction = true,
            bool autoTrimColumns = true,
            bool ignoreNullUpdates = false // if true: NULL in source will NOT overwrite target
        )
        {
            return SafeExecute("BULK_UPSERT_DT", extras =>
            {
                if (dt == null) throw new ArgumentNullException(nameof(dt));
                if (dt.Rows.Count == 0) return (0, 0);

                EnsureIdent(Schema);
                EnsureIdent(Table);

                var key = (keyColumn ?? KeyColumn)?.Trim();
                if (string.IsNullOrWhiteSpace(key))
                    throw new InvalidOperationException("BulkUpsert requires a key column. Provide keyColumn or set KeyColumn.");

                EnsureIdent(key);

                // Read destination metadata
                var colsMeta = GetColumns() ?? new List<ColumnInfo>();
                var destCols = new HashSet<string>(colsMeta.Select(c => c.Name), StringComparer.OrdinalIgnoreCase);

                // DT columns
                var dtCols = dt.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToList();

                // Validate key exists in dt
                if (!dt.Columns.Contains(key))
                    throw new InvalidOperationException($"BulkUpsert DataTable must contain key column '{key}'.");

                // Determine which columns we will write (intersection)
                var writeCols = dtCols.Where(c => destCols.Contains(c)).ToList();

                if (!autoTrimColumns)
                {
                    var unknown = dtCols.Where(c => !destCols.Contains(c)).ToList();
                    if (unknown.Count > 0)
                        throw new InvalidOperationException("Unknown columns in DataTable: " + string.Join(", ", unknown));
                }

                // Must include key
                if (!writeCols.Contains(key, StringComparer.OrdinalIgnoreCase))
                    writeCols.Add(key);

                // Columns to UPDATE (exclude key)
                var updateCols = writeCols
                    .Where(c => !c.Equals(key, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (writeCols.Count == 0)
                    throw new InvalidOperationException("No matching columns between DataTable and destination table.");

                // Build CREATE TABLE #tmp with correct SQL types from destination metadata
                string tmpName = "#tmp_upsert";
                var metaByName = colsMeta.ToDictionary(c => c.Name, c => c, StringComparer.OrdinalIgnoreCase);

                string BuildSqlType(ColumnInfo ci)
                {
                    // Use sys.types name + length/precision/scale, similar to your column reader.
                    // max_length is bytes; nvarchar/nchar are 2 bytes per char
                    var t = (ci.DataType ?? "").ToLowerInvariant();

                    // types with (len)
                    if (t is "varchar" or "nvarchar" or "char" or "nchar" or "varbinary" or "binary")
                    {
                        if (ci.MaxLength < 0) return $"{t}(max)";

                        int len = ci.MaxLength;
                        if (t is "nvarchar" or "nchar") len = len / 2; // bytes -> chars
                        if (len <= 0) len = 1;
                        return $"{t}({len})";
                    }

                    // types with (precision,scale)
                    if (t is "decimal" or "numeric")
                    {
                        int p = ci.Precision <= 0 ? 18 : ci.Precision;
                        int s = ci.Scale < 0 ? 0 : ci.Scale;
                        return $"{t}({p},{s})";
                    }

                    // datetime2/time/datetimeoffset scale
                    if (t is "datetime2" or "time" or "datetimeoffset")
                    {
                        int sc = ci.Scale;
                        if (sc < 0 || sc > 7) return t;
                        return $"{t}({sc})";
                    }

                    return t;
                }

                var tmpColsSql = new List<string>();
                foreach (var c in writeCols)
                {
                    if (!metaByName.TryGetValue(c, out var ci))
                        continue;

                    // temp table columns nullable (safe for bulk load)
                    tmpColsSql.Add($"{QSafe(c)} {BuildSqlType(ci)} NULL");
                }

                if (!tmpColsSql.Any())
                    throw new InvalidOperationException("Could not build temp table schema from metadata. Check table/columns.");

                // Build MERGE parts
                string tgt = "T";
                string src = "S";

                string onClause = $"{tgt}.{QSafe(key)} = {src}.{QSafe(key)}";

                string updateSet;
                if (updateCols.Count == 0)
                {
                    // nothing to update, only insert new
                    updateSet = "";
                }
                else
                {
                    if (ignoreNullUpdates)
                    {
                        // do not overwrite target with NULL from source
                        updateSet = string.Join(", ",
                            updateCols.Select(c =>
                                $"{tgt}.{QSafe(c)} = COALESCE({src}.{QSafe(c)}, {tgt}.{QSafe(c)})"));
                    }
                    else
                    {
                        updateSet = string.Join(", ",
                            updateCols.Select(c => $"{tgt}.{QSafe(c)} = {src}.{QSafe(c)}"));
                    }
                }

                string insertColsSql = string.Join(", ", writeCols.Select(QSafe));
                string insertValsSql = string.Join(", ", writeCols.Select(c => $"{src}.{QSafe(c)}"));

                // Optional: update only when something differs (reduces unnecessary writes)
                // NOTE: This is “best effort” generic compare using ISNULL + CONVERT.
                // If you want ultra strict compare per datatype, we can enhance it later.
                string whenMatchedAndDiff = "";
                if (updateCols.Count > 0)
                {
                    var diffs = updateCols.Select(c =>
                        $"ISNULL(CONVERT(nvarchar(max), {tgt}.{QSafe(c)}), N'') <> ISNULL(CONVERT(nvarchar(max), {src}.{QSafe(c)}), N'')");
                    whenMatchedAndDiff = "AND (" + string.Join(" OR ", diffs) + ")";
                }

                var sql = $@"
SET NOCOUNT ON;

IF OBJECT_ID('tempdb..{tmpName}') IS NOT NULL DROP TABLE {tmpName};

CREATE TABLE {tmpName} (
    {string.Join(",\n    ", tmpColsSql)}
);

-- Bulk copy loads into temp table, then MERGE into target
DECLARE @Actions TABLE([Action] nvarchar(10));

MERGE [{Schema}].[{Table}] WITH (HOLDLOCK) AS {tgt}
USING {tmpName} AS {src}
ON {onClause}
{(updateCols.Count == 0 ? "" : $@"
WHEN MATCHED {whenMatchedAndDiff} THEN
    UPDATE SET {updateSet}")}
WHEN NOT MATCHED BY TARGET THEN
    INSERT ({insertColsSql})
    VALUES ({insertValsSql})
OUTPUT $action INTO @Actions;

SELECT
    SUM(CASE WHEN [Action] = 'INSERT' THEN 1 ELSE 0 END) AS Inserted,
    SUM(CASE WHEN [Action] = 'UPDATE' THEN 1 ELSE 0 END) AS Updated
FROM @Actions;
";

                extras["rowCount"] = dt.Rows.Count;
                extras["writeCols"] = string.Join(",", writeCols);
                extras["keyColumn"] = key;
                extras["ignoreNullUpdates"] = ignoreNullUpdates;

                using var cn = Open();
                SqlTransaction? tx = null;
                if (useTransaction) tx = cn.BeginTransaction();

                try
                {
                    // 1) Create temp + prepare merge command
                    using (var cmdPrep = new SqlCommand(sql, cn, tx))
                    {
                        // We will execute AFTER bulk copy, but temp table must exist first
                        // So: split into create + merge OR execute create first.
                        // Easiest: execute CREATE TABLE only, then bulk copy, then MERGE.
                    }

                    // Execute CREATE TABLE part first
                    var createSql = $@"
SET NOCOUNT ON;
IF OBJECT_ID('tempdb..{tmpName}') IS NOT NULL DROP TABLE {tmpName};
CREATE TABLE {tmpName} (
    {string.Join(",\n    ", tmpColsSql)}
);";
                    using (var cmdCreate = new SqlCommand(createSql, cn, tx))
                        cmdCreate.ExecuteNonQuery();

                    // 2) Bulk copy into temp table
                    using (var bcp = new SqlBulkCopy(cn, SqlBulkCopyOptions.Default, tx))
                    {
                        bcp.DestinationTableName = tmpName;
                        bcp.BatchSize = batchSize;
                        bcp.BulkCopyTimeout = 0;

                        // Map only writeCols
                        foreach (var c in writeCols)
                            bcp.ColumnMappings.Add(c, c);

                        // Create a trimmed view table containing only writeCols (in same order)
                        var view = dt.DefaultView.ToTable(false, writeCols.ToArray());
                        bcp.WriteToServer(view);
                    }

                    // 3) MERGE + get counts
                    var mergeSql = $@"
SET NOCOUNT ON;
DECLARE @Actions TABLE([Action] nvarchar(10));

MERGE [{Schema}].[{Table}] WITH (HOLDLOCK) AS {tgt}
USING {tmpName} AS {src}
ON {onClause}
{(updateCols.Count == 0 ? "" : $@"
WHEN MATCHED {whenMatchedAndDiff} THEN
    UPDATE SET {updateSet}")}
WHEN NOT MATCHED BY TARGET THEN
    INSERT ({insertColsSql})
    VALUES ({insertValsSql})
OUTPUT $action INTO @Actions;

SELECT
    SUM(CASE WHEN [Action] = 'INSERT' THEN 1 ELSE 0 END) AS Inserted,
    SUM(CASE WHEN [Action] = 'UPDATE' THEN 1 ELSE 0 END) AS Updated
FROM @Actions;";
                    int inserted = 0, updated = 0;

                    using (var cmdMerge = new SqlCommand(mergeSql, cn, tx))
                    {
                        using (var rd = cmdMerge.ExecuteReader())
                        {
                            if (rd.Read())
                            {
                                inserted = rd["Inserted"] == DBNull.Value ? 0 : Convert.ToInt32(rd["Inserted"]);
                                updated = rd["Updated"] == DBNull.Value ? 0 : Convert.ToInt32(rd["Updated"]);
                            }
                        } // ✅ reader closed here
                    }

                    using (var cmdDrop = new SqlCommand(
                        $"IF OBJECT_ID('tempdb..{tmpName}') IS NOT NULL DROP TABLE {tmpName};", cn, tx))
                    {
                        cmdDrop.ExecuteNonQuery();
                    }

                    tx?.Commit();
                    return (inserted, updated);
                }
                catch
                {
                    tx?.Rollback();
                    throw;
                }
            });
        }
        public int BulkInsert(DataTable dt,
                      int batchSize = 1000,
                      bool keepIdentity = false,
                      bool useTransaction = true,
                      bool autoTrimColumns = true)
        {
            return SafeExecute("BULK_INSERT_DT", extras =>
            {
                if (dt == null) throw new ArgumentNullException(nameof(dt));
                if (dt.Rows.Count == 0) return 0;

                EnsureIdent(Schema);
                EnsureIdent(Table);

                var cols = GetColumns() ?? new List<ColumnInfo>();
                var tableCols = new HashSet<string>(cols
                    .Where(c => keepIdentity || !c.Identity)
                    .Select(c => c.Name),
                    StringComparer.OrdinalIgnoreCase);

                var dtCols = dt.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToList();
                var writeCols = dtCols.Where(c => tableCols.Contains(c)).ToList();

                if (!autoTrimColumns)
                {
                    var unknown = dtCols.Where(c => !tableCols.Contains(c)).ToList();
                    if (unknown.Count > 0)
                        throw new InvalidOperationException("Unknown columns in DataTable: " + string.Join(", ", unknown));
                }

                if (writeCols.Count == 0)
                    throw new InvalidOperationException("No matching columns between DataTable and destination table.");

                extras["rowCount"] = dt.Rows.Count;
                extras["colCount"] = writeCols.Count;

                using var cn = Open();
                SqlTransaction? tx = null;
                if (useTransaction) tx = cn.BeginTransaction();

                try
                {
                    var options = keepIdentity ? SqlBulkCopyOptions.KeepIdentity : SqlBulkCopyOptions.Default;
                    using var bcp = new SqlBulkCopy(cn, options, tx)
                    {
                        DestinationTableName = $"[{Schema}].[{Table}]",
                        BatchSize = batchSize,
                        BulkCopyTimeout = 0
                    };

                    foreach (var name in writeCols)
                        bcp.ColumnMappings.Add(name, name);

                    bcp.WriteToServer(dt);

                    tx?.Commit();
                    return dt.Rows.Count;
                }
                catch
                {
                    tx?.Rollback();
                    throw;
                }
            });
        }


        // Method: Save
        /// <summary>
        /// Upserts the current <see cref="Values"/>:
        /// - If <see cref="KeyColumn"/> is unset or missing in values, inserts.
        /// - If a row exists for the key, optionally asks for confirmation and updates only changed columns.
        /// - If no row exists, inserts.
        /// Returns the key/identity when available.
        /// </summary>
        /// <param name="askBeforeOverwrite">If true, shows a diff prompt before updating.</param>
        /// <returns>The row key (existing or newly created) or <c>null</c> if user cancels.</returns>
        public object? Save(bool askBeforeOverwrite = true, bool Warningneeded = true) => SafeExecute("SAVE", extras =>
        {
            // Try to recover missing key from DB (based on non-key columns)
            if (!string.IsNullOrWhiteSpace(KeyColumn) &&
                (!Values.TryGetValue(KeyColumn, out var keyCandidate) || IsEmptyKey(keyCandidate)))
            {
                var inferred = TryInferKeyFromDb();
                if (inferred != null && !IsEmptyKey(inferred))
                    Values[KeyColumn] = inferred;
            }

            // No configured key? -> insert only
            if (string.IsNullOrWhiteSpace(KeyColumn))
            {
                var id0 = Insert();
                AddCommonExtras(extras, ("mode", "insert(no-key)"), ("newId", id0));
                return id0;
            }

            // Re-check for key value
            if (!Values.TryGetValue(KeyColumn, out var keyVal) || IsEmptyKey(keyVal))
            {
                var id1 = Insert();
                if (id1 != null) Values[KeyColumn] = id1;
                AddCommonExtras(extras, ("mode", "insert"), ("newId", id1));
                return id1;
            }
            // Does the row already exist?
            bool exists;
            using (var cn = Open())
            using (var cmd = new SqlCommand($"SELECT COUNT(*) FROM [{Schema}].[{Table}] WHERE {Q(KeyColumn)} = @key", cn))
            {
                cmd.Parameters.AddWithValue("@key", keyVal ?? DBNull.Value);
                exists = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
            }

            if (exists)
            {
                if (askBeforeOverwrite)
                {
                    using var cn2 = Open();
                    var existingRow = GetRowByKey(cn2, keyVal);
                    string changes = existingRow != null ? BuildChangeSummary(existingRow)
                                                         : "(Could not read existing row)";

                    var prompt =
                        $"Changes:\n{changes}\n\n" +
                        "Do you want to apply these changes?";

                    var result = MessageBox.Show(prompt, "Confirm Update",
                                                 MessageBoxButtons.YesNoCancel,
                                                 MessageBoxIcon.Question,
                                                 MessageBoxDefaultButton.Button1);

                    if (result == DialogResult.Cancel || result == DialogResult.No)
                    {
                        AddCommonExtras(extras, ("mode", "discarded"), ("key", keyVal));
                        return null;
                    }
                }

                var affected = UpdateByKey();

                AddCommonExtras(extras, ("mode", "update"), ("key", keyVal), ("affected", affected));

                if (affected == 0 && Warningneeded)
                {
                    MessageBox.Show(
                        $"⚠️ Warning: No rows were updated in table [{Schema}].[{Table}]." + Environment.NewLine +
                        $"This may happen if:" + Environment.NewLine +
                        $"- The record with {KeyColumn} = '{keyVal}' does not exist," + Environment.NewLine +
                        $"- The data you're saving is identical to the current database values," + Environment.NewLine +
                        $"- Or there’s an INSTEAD OF trigger preventing the update.",
                        "No Changes Detected",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );
                }

                return keyVal;
            }
            else
            {
                var id2 = Insert();
                if (id2 != null) Values[KeyColumn] = id2;
                AddCommonExtras(extras, ("mode", "insert(new)"), ("newId", id2));
                return id2;
            }
        });

        private object? TryInferKeyFromDb()
        {
            if (string.IsNullOrWhiteSpace(KeyColumn)) return null;

            var cols = Values.Keys
                .Where(k => !k.Equals(KeyColumn, StringComparison.OrdinalIgnoreCase))
                .Where(k => Values[k] != null && Values[k] != DBNull.Value)
                .ToList();

            if (cols.Count == 0) return null;

            var whereParts = new List<string>();
            var ps = new List<SqlParameter>();
            int i = 0;

            foreach (var c in cols)
            {
                whereParts.Add($"{Q(c)}=@p{i}");
                ps.Add(new SqlParameter("@p" + i, Values[c]!));
                i++;
            }

            var where = string.Join(" AND ", whereParts);
            var sql = $"SELECT TOP (2) {Q(KeyColumn)} FROM [{Schema}].[{Table}] WHERE {where};";

            using var cn = Open();
            using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddRange(ps.ToArray());

            using var rd = cmd.ExecuteReader();
            object? found = null;
            int count = 0;
            while (rd.Read())
            {
                found = rd.GetValue(0);
                count++;
                if (count > 1) return null;
            }
            return count == 1 && found != DBNull.Value ? found : null;
        }

        // Method: DeleteByKey
        /// <summary>
        /// Deletes a row by key from the target table.
        /// </summary>
        /// <param name="keyValue">Key value to delete.</param>
        /// <returns>Number of rows deleted (0 if not found).</returns>
        /// <exception cref="ForeignKeyDeleteBlockedException">
        /// Thrown when the delete is blocked by a foreign key constraint (SQL error 547).
        /// </exception>
        public int DeleteByKey(object keyValue) => SafeExecute("DELETE", extras =>
        {
            EnsureIdent(Schema); EnsureIdent(Table); EnsureIdent(KeyColumn);

            var sql = $"DELETE FROM [{Schema}].[{Table}] WHERE {Q(KeyColumn)}=@k";

            using var cn = Open();
            using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@k", keyValue ?? DBNull.Value);

            AddCommonExtras(extras, ("sql", "DELETE ... WHERE key"), ("key", keyValue));

            try
            {
                return cmd.ExecuteNonQuery();
            }
            catch (SqlException ex) when (ex.Number == 547) // FK constraint violation
            {
                string? constraint = null, childTable = null, childColumn = null;

                try
                {
                    var m1 = Regex.Match(ex.Message, "constraint \"(?<c>.+?)\"", RegexOptions.IgnoreCase);
                    if (m1.Success) constraint = m1.Groups["c"].Value;

                    var m2 = Regex.Match(ex.Message, "table \"(?<t>.+?)\"", RegexOptions.IgnoreCase);
                    if (m2.Success) childTable = m2.Groups["t"].Value;

                    var m3 = Regex.Match(ex.Message, "column '(?<col>.+?)'", RegexOptions.IgnoreCase);
                    if (m3.Success) childColumn = m3.Groups["col"].Value;
                }
                catch { /* ignore parsing errors */ }

                AddCommonExtras(extras,
                    ("error", "FK violation on delete"),
                    ("sqlerr", ex.Message),
                    ("constraint", constraint ?? ""),
                    ("refTable", childTable ?? ""),
                    ("refColumn", childColumn ?? "")
                );

                throw new ForeignKeyDeleteBlockedException(
       "Delete blocked by a foreign key reference.",
       constraint, childTable, childColumn);
            }
        });

        // Method: Select
        /// <summary>
        /// Executes a parameterized <c>SELECT *</c> on the target table with optional WHERE/TOP/ORDER BY.
        /// </summary>
        /// <param name="whereSql">Optional WHERE clause (without the keyword "WHERE").</param>
        /// <param name="parameters">Optional parameter map (name → value). Names may omit the '@'.</param>
        /// <param name="top">Optional TOP N limit.</param>
        /// <param name="orderBy">Optional ORDER BY clause (without the keyword).</param>
        /// <returns>A <see cref="DataTable"/> with results; can be empty but not null.</returns>
        public DataTable? Select(
    string? whereSql = null,
    IDictionary<string, object?>? parameters = null,
    int? top = null,
    string? orderBy = null,
    bool DisplayName = true,
    bool WantFormatingInDefault = true)
=> SafeExecute("SELECT", extras =>
{
    EnsureIdent(Schema);
    EnsureIdent(Table);

    var sql = $"SELECT {(top.HasValue ? "TOP " + top.Value + " " : "")}* FROM [{Schema}].[{Table}]"
            + (string.IsNullOrWhiteSpace(whereSql) ? "" : " WHERE " + whereSql)
            + (string.IsNullOrWhiteSpace(orderBy) ? "" : " ORDER BY " + orderBy);

    using var cn = Open();
    using var da = new SqlDataAdapter(sql, cn);

    if (parameters != null)
    {
        da.SelectCommand!.Parameters.Clear();

        foreach (var kv in parameters)
        {
            var name = kv.Key.StartsWith("@") ? kv.Key : "@" + kv.Key;
            da.SelectCommand.Parameters.AddWithValue(name, kv.Value ?? DBNull.Value);
        }
    }

    AddCommonExtras(extras,
        ("sql", sql),
        ("where", whereSql ?? ""),
        ("params", parameters ?? new Dictionary<string, object?>()));

    var dt = new DataTable();
    da.Fill(dt);

    dt = ReorderColumnsByMetadataOrder(dt);

    if (WantFormatingInDefault)
    {
        var formats = GetFormatsFromMetadata();
        dt = ApplyFormats(dt, formats);
    }

    if (DisplayName)
        dt = ApplyDisplayNames(dt);

    return dt;
});

        private Dictionary<string, string> GetFormatsFromMetadata()
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var col in GetColumns())
            {
                var format = GetColumnProperty("Format", col.Name)?.ToString();

                if (!string.IsNullOrWhiteSpace(format))
                    dict[col.Name] = format;
            }

            return dict;
        }

        private object FormatValue(object value, string? format)
        {
            if (value == null || value == DBNull.Value || string.IsNullOrWhiteSpace(format))
                return value;

            try
            {
                return string.Format(CultureInfo.InvariantCulture, "{0:" + format + "}", value);
            }
            catch
            {
                return value;
            }
        }

        private DataTable ApplyFormats(DataTable source, Dictionary<string, string> columnFormats)
        {
            var result = new DataTable();

            foreach (DataColumn col in source.Columns)
            {
                if (columnFormats.ContainsKey(col.ColumnName))
                    result.Columns.Add(col.ColumnName, typeof(string));
                else
                    result.Columns.Add(col.ColumnName, col.DataType);
            }

            foreach (DataRow row in source.Rows)
            {
                var newRow = result.NewRow();

                foreach (DataColumn col in source.Columns)
                {
                    if (columnFormats.TryGetValue(col.ColumnName, out var format))
                        newRow[col.ColumnName] = FormatValue(row[col], format) ?? DBNull.Value;
                    else
                        newRow[col.ColumnName] = row[col];
                }

                result.Rows.Add(newRow);
            }

            return result;
        }
        public string[] GetColumnValues(
    string columnName,
    string? whereSql = null,
    IDictionary<string, object?>? parameters = null,
    bool distinct = true)
        {
            var dt = Select(whereSql, parameters);
            var values = GetColumnValuesFromDataTable(dt!, columnName);

            if (distinct)
                values = values.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

            return values;
        }

        // Method: GetColumnValuesFromDataTable (static)
        /// <summary>
        /// Extracts a column as a string array from an existing <see cref="DataTable"/>.
        /// </summary>
        /// <param name="dt">Source data table.</param>
        /// <param name="columnName">Column to read.</param>
        /// <returns>Array of strings for the given column.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="dt"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the column does not exist.</exception>
        public static string[] GetColumnValuesFromDataTable(DataTable dt, string columnName)
        {
            if (dt == null)
                throw new ArgumentNullException(nameof(dt));

            if (!dt.Columns.Contains(columnName))
                throw new ArgumentException($"Column '{columnName}' not found in DataTable", nameof(columnName));

            return dt.AsEnumerable()
                     .Select(r => r[columnName]?.ToString() ?? string.Empty)
                     .ToArray();
        }

        public Dictionary<string, object?>? GetRowAsDictionary(
    string columnName,
    object? value)
        {
            EnsureIdent(Schema);
            EnsureIdent(Table);
            EnsureIdent(columnName);

            var dt = Select($"{Q(columnName)} = @v", new Dictionary<string, object?> { ["v"] = value }, top: 1);
            if (dt == null || dt.Rows.Count == 0) return null;

            var row = dt.Rows[0];
            var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            foreach (DataColumn col in dt.Columns)
            {
                var v = row[col];
                dict[col.ColumnName] = v == DBNull.Value ? null : v;
            }

            return dict;
        }

        #endregion

        #region Async DML (ROW CRUD)
        // Method: InsertAsync
        /// <summary>
        /// Asynchronously inserts a new row using <see cref="Values"/>; returns identity/provided key or <c>null</c>.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Identity value, provided key, or <c>null</c>.</returns>
        public async Task<object?> InsertAsync(CancellationToken ct = default) =>
            await SafeExecuteAsync("INSERT_ASYNC", async extras =>
            {
                EnsureIdent(Schema);
                EnsureIdent(Table);

                if (Values.Count == 0)
                    throw new InvalidOperationException("No Values to insert.");

                var cols = Values.Keys.ToArray();
                var qCols = cols.Select(QSafe).ToArray();
                var parNames = cols.Select((_, i) => $"@p{i}").ToArray();

                var identitySql = string.IsNullOrWhiteSpace(KeyColumn)
            ? ""
            : $"IF COLUMNPROPERTY(object_id('{Schema}.{Table}'), '{KeyColumn}', 'IsIdentity')=1 SELECT SCOPE_IDENTITY();";

                var sql = $"INSERT INTO [{Schema}].[{Table}] ({string.Join(",", qCols)}) " +
                  $"VALUES ({string.Join(",", parNames)}); {identitySql}";

                using var cn = await OpenAsync(ct).ConfigureAwait(false);
                using var cmd = new SqlCommand(sql, cn);

                for (int i = 0; i < cols.Length; i++)
                {
                    var val = Values[cols[i]] ?? DBNull.Value;
                    cmd.Parameters.AddWithValue(parNames[i], val);
                }

                AddCommonExtras(extras,
            ("sql", sql),
            ("columns", string.Join(",", cols)),
            ("valuesCount", cols.Length));

                if (string.IsNullOrWhiteSpace(identitySql))
                {
                    await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

                    if (!string.IsNullOrWhiteSpace(KeyColumn) &&
                Values.TryGetValue(KeyColumn, out var providedKey) &&
                !IsEmptyKey(providedKey))
                    {
                        return providedKey;
                    }

                    return null;
                }
                else
                {
                    var id = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
                    return id == DBNull.Value ? null : id;
                }
            }).ConfigureAwait(false);

        // Method: UpdateByKeyAsync
        /// <summary>
        /// Asynchronously updates a row by key, changing only modified columns.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Number of affected rows.</returns>
        /// <exception cref="InvalidOperationException">Thrown when key value is missing from <see cref="Values"/>.</exception>
        public async Task<int> UpdateByKeyAsync(CancellationToken ct = default)
        {
            EnsureIdent(Schema); EnsureIdent(Table); EnsureIdent(KeyColumn);

            if (!Values.TryGetValue(KeyColumn, out var keyVal))
                throw new InvalidOperationException($"Values must include '{KeyColumn}' for Update.");

            using var cn = await OpenAsync(ct).ConfigureAwait(false);

            var existing = await GetRowByKeyAsync(cn, keyVal, ct).ConfigureAwait(false);
            if (existing == null)
                return 0;

            var changedCols = new List<string>();
            foreach (var c in Values.Keys)
            {
                if (c.Equals(KeyColumn, StringComparison.OrdinalIgnoreCase)) continue;
                if (!existing.Table.Columns.Contains(c)) continue;

                var oldVal = existing[c];
                var newVal = Values[c] ?? DBNull.Value;

                if (!ValueEquals(oldVal, newVal))
                    changedCols.Add(c);
            }

            if (changedCols.Count == 0)
                return 0;

            var sets = string.Join(",", changedCols.Select(c => $"{Q(c)}=@{c}"));
            var sql = $"UPDATE [{Schema}].[{Table}] SET {sets} WHERE {Q(KeyColumn)}=@__key";

            using var cmd = new SqlCommand(sql, cn);

            foreach (var c in changedCols)
                cmd.Parameters.AddWithValue("@" + c, Values[c] ?? DBNull.Value);

            cmd.Parameters.AddWithValue("@__key", keyVal ?? DBNull.Value);

            var affected = await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            return affected;
        }

        public async Task<int> BulkInsertAsync(IEnumerable<IDictionary<string, object?>> rows,
                                       int batchSize = 1000,
                                       bool keepIdentity = false,
                                       bool useTransaction = true,
                                       CancellationToken ct = default)
        {
            return await SafeExecuteAsync("BULK_INSERT_ASYNC", async extras =>
            {
                var list = rows as IList<IDictionary<string, object?>> ?? rows.ToList();
                if (list.Count == 0) return 0;

                EnsureIdent(Schema);
                EnsureIdent(Table);

                var cols = GetColumns() ?? new List<ColumnInfo>();
                var insertCols = cols
                    .Where(c => !c.Identity || keepIdentity)
                    .Select(c => c.Name)
                    .ToList();

                var dt = new DataTable();
                foreach (var c in insertCols) dt.Columns.Add(c, typeof(object));
                foreach (var r in list)
                {
                    var dr = dt.NewRow();
                    foreach (var c in insertCols) { r.TryGetValue(c, out var v); dr[c] = v ?? DBNull.Value; }
                    dt.Rows.Add(dr);
                }

                extras["rowCount"] = dt.Rows.Count;
                extras["colCount"] = dt.Columns.Count;

                using var cn = await OpenAsync(ct);
                SqlTransaction? tx = null;
                if (useTransaction) tx = cn.BeginTransaction();

                try
                {
                    var options = keepIdentity ? SqlBulkCopyOptions.KeepIdentity : SqlBulkCopyOptions.Default;
                    using var bcp = new SqlBulkCopy(cn, options, tx)
                    {
                        DestinationTableName = $"[{Schema}].[{Table}]",
                        BatchSize = batchSize,
                        BulkCopyTimeout = 0
                    };
                    foreach (DataColumn dc in dt.Columns)
                        bcp.ColumnMappings.Add(dc.ColumnName, dc.ColumnName);

                    await bcp.WriteToServerAsync(dt, ct);
                    tx?.Commit();
                    return dt.Rows.Count;
                }
                catch
                {
                    tx?.Rollback();
                    throw;
                }
            });
        }

        public async Task<int> BulkInsertAsync(DataTable dt,
                                       int batchSize = 1000,
                                       bool keepIdentity = false,
                                       bool useTransaction = true,
                                       bool autoTrimColumns = true,
                                       CancellationToken ct = default)
        {
            return await SafeExecuteAsync("BULK_INSERT_DT_ASYNC", async extras =>
            {
                if (dt == null) throw new ArgumentNullException(nameof(dt));
                if (dt.Rows.Count == 0) return 0;

                EnsureIdent(Schema);
                EnsureIdent(Table);

                var cols = GetColumns() ?? new List<ColumnInfo>();
                var tableCols = new HashSet<string>(cols
                    .Where(c => keepIdentity || !c.Identity)
                    .Select(c => c.Name),
                    StringComparer.OrdinalIgnoreCase);

                var dtCols = dt.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToList();
                var writeCols = dtCols.Where(c => tableCols.Contains(c)).ToList();

                if (!autoTrimColumns)
                {
                    var unknown = dtCols.Where(c => !tableCols.Contains(c)).ToList();
                    if (unknown.Count > 0)
                        throw new InvalidOperationException("Unknown columns in DataTable: " + string.Join(", ", unknown));
                }

                if (writeCols.Count == 0)
                    throw new InvalidOperationException("No matching columns between DataTable and destination table.");

                extras["rowCount"] = dt.Rows.Count;
                extras["colCount"] = writeCols.Count;

                using var cn = await OpenAsync(ct);
                SqlTransaction? tx = null;
                if (useTransaction) tx = cn.BeginTransaction();

                try
                {
                    var options = keepIdentity ? SqlBulkCopyOptions.KeepIdentity : SqlBulkCopyOptions.Default;
                    using var bcp = new SqlBulkCopy(cn, options, tx)
                    {
                        DestinationTableName = $"[{Schema}].[{Table}]",
                        BatchSize = batchSize,
                        BulkCopyTimeout = 0
                    };

                    foreach (var name in writeCols)
                        bcp.ColumnMappings.Add(name, name);

                    await bcp.WriteToServerAsync(dt, ct);

                    tx?.Commit();
                    return dt.Rows.Count;
                }
                catch
                {
                    tx?.Rollback();
                    throw;
                }
            });
        }

        // Method: SaveAsync
        /// <summary>
        /// Asynchronous upsert for <see cref="Values"/>.
        /// Uses <paramref name="confirmAsync"/> to request user confirmation before updating when <paramref name="askBeforeOverwrite"/> is true.
        /// </summary>
        /// <param name="askBeforeOverwrite">If true, invokes <paramref name="confirmAsync"/> with a change summary.</param>
        /// <param name="confirmAsync">Async delegate to confirm update; return false to cancel.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Key/identity value, or <c>null</c> if cancelled.</returns>
        public async Task<object?> SaveAsync(
            bool askBeforeOverwrite = true,
            ConfirmUpdateAsync? confirmAsync = null,
            CancellationToken ct = default)
        => await SafeExecuteAsync("SAVE_ASYNC", async extras =>
        {
            // Try infer missing key
            if (!string.IsNullOrWhiteSpace(KeyColumn) &&
                (!Values.TryGetValue(KeyColumn, out var keyCandidate) || IsEmptyKey(keyCandidate)))
            {
                var inferred = await TryInferKeyFromDbAsync(ct).ConfigureAwait(false);
                if (inferred != null && !IsEmptyKey(inferred))
                    Values[KeyColumn] = inferred;
            }

            if (string.IsNullOrWhiteSpace(KeyColumn))
            {
                var id0 = await InsertAsync(ct).ConfigureAwait(false);
                AddCommonExtras(extras, ("mode", "insert(no-key)"), ("newId", id0));
                return id0;
            }

            if (!Values.TryGetValue(KeyColumn, out var keyVal) || IsEmptyKey(keyVal))
            {
                var id1 = await InsertAsync(ct).ConfigureAwait(false);
                if (id1 != null) Values[KeyColumn] = id1;
                AddCommonExtras(extras, ("mode", "insert"), ("newId", id1));
                return id1;
            }

            bool exists;
            using (var cn = await OpenAsync(ct).ConfigureAwait(false))
            using (var cmd = new SqlCommand($"SELECT COUNT(*) FROM [{Schema}].[{Table}] WHERE {Q(KeyColumn)} = @key", cn))
            {
                cmd.Parameters.AddWithValue("@key", keyVal ?? DBNull.Value);
                exists = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false)) > 0;
            }

            if (exists)
            {
                if (askBeforeOverwrite && confirmAsync != null)
                {
                    using var cn2 = await OpenAsync(ct).ConfigureAwait(false);
                    var existingRow = await GetRowByKeyAsync(cn2, keyVal, ct).ConfigureAwait(false);
                    string changes = existingRow != null ? BuildChangeSummary(existingRow) : "(Could not read existing row)";
                    var accept = await confirmAsync(changes, ct).ConfigureAwait(false);
                    if (!accept)
                    {
                        AddCommonExtras(extras, ("mode", "discarded"), ("key", keyVal));
                        return null;
                    }
                }

                var affected = await UpdateByKeyAsync(ct).ConfigureAwait(false);
                AddCommonExtras(extras, ("mode", "update"), ("key", keyVal), ("affected", affected));
                return keyVal;
            }
            else
            {
                var id2 = await InsertAsync(ct).ConfigureAwait(false);
                if (id2 != null) Values[KeyColumn] = id2;
                AddCommonExtras(extras, ("mode", "insert(new)"), ("newId", id2));
                return id2;
            }
        }).ConfigureAwait(false);

        // Method: DeleteByKeyAsync
        /// <summary>
        /// Asynchronously deletes a row by key.
        /// </summary>
        /// <param name="keyValue">Key value to delete.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Number of rows deleted.</returns>
        /// <exception cref="ForeignKeyDeleteBlockedException">
        /// Thrown when deletion is blocked by an FK constraint.
        /// </exception>
        public async Task<int> DeleteByKeyAsync(object keyValue, CancellationToken ct = default) =>
            await SafeExecuteAsync("DELETE_ASYNC", async extras =>
            {
                EnsureIdent(Schema); EnsureIdent(Table); EnsureIdent(KeyColumn);

                var sql = $"DELETE FROM [{Schema}].[{Table}] WHERE {Q(KeyColumn)}=@k";

                using var cn = await OpenAsync(ct).ConfigureAwait(false);
                using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@k", keyValue ?? DBNull.Value);

                AddCommonExtras(extras, ("sql", "DELETE ... WHERE key"), ("key", keyValue));

                try
                {
                    return await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                }
                catch (SqlException ex) when (ex.Number == 547) // FK violation
                {
                    string? constraint = null, childTable = null, childColumn = null;
                    try
                    {
                        var m1 = Regex.Match(ex.Message, "constraint \"(?<c>.+?)\"", RegexOptions.IgnoreCase);
                        if (m1.Success) constraint = m1.Groups["c"].Value;

                        var m2 = Regex.Match(ex.Message, "table \"(?<t>.+?)\"", RegexOptions.IgnoreCase);
                        if (m2.Success) childTable = m2.Groups["t"].Value;

                        var m3 = Regex.Match(ex.Message, "column '(?<col>.+?)'", RegexOptions.IgnoreCase);
                        if (m3.Success) childColumn = m3.Groups["col"].Value;
                    }
                    catch { /* ignore */ }

                    AddCommonExtras(extras,
                ("error", "FK violation on delete"),
                ("sqlerr", ex.Message),
                ("constraint", constraint ?? ""),
                ("refTable", childTable ?? ""),
                ("refColumn", childColumn ?? "")
            );

                    throw new ForeignKeyDeleteBlockedException(
                "Delete blocked by a foreign key reference.",
                constraint, childTable, childColumn);
                }
            }).ConfigureAwait(false);

        // Method: SelectAsync
        /// <summary>
        /// Asynchronously executes a <c>SELECT *</c> on the target table with optional WHERE/TOP/ORDER BY.
        /// </summary>
        /// <param name="whereSql">Optional WHERE clause (without the keyword).</param>
        /// <param name="parameters">Optional parameters (name → value).</param>
        /// <param name="top">Optional TOP N.</param>
        /// <param name="orderBy">Optional ORDER BY (without the keyword).</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A populated <see cref="DataTable"/>.</returns>
        public async Task<DataTable?> SelectAsync(
            string? whereSql = null,
            IDictionary<string, object?>? parameters = null,
            int? top = null,
            string? orderBy = null,
            CancellationToken ct = default)
        => await SafeExecuteAsync("SELECT_ASYNC", async extras =>
        {
            EnsureIdent(Schema); EnsureIdent(Table);
            var sql = $"SELECT {(top.HasValue ? "TOP " + top.Value + " " : "")}* FROM [{Schema}].[{Table}]"
                    + (string.IsNullOrWhiteSpace(whereSql) ? "" : " WHERE " + whereSql)
                    + (string.IsNullOrWhiteSpace(orderBy) ? "" : " ORDER BY " + orderBy);

            using var cn = await OpenAsync(ct).ConfigureAwait(false);
            using var cmd = new SqlCommand(sql, cn);

            if (parameters != null)
            {
                foreach (var kv in parameters)
                {
                    var name = kv.Key.StartsWith("@") ? kv.Key : "@" + kv.Key;
                    var p = cmd.Parameters.Add(name, SqlDbType.NVarChar);
                    p.Value = kv.Value ?? DBNull.Value;
                }
            }

            AddCommonExtras(extras, ("sql", sql), ("where", whereSql ?? ""), ("params", parameters ?? new Dictionary<string, object?>()));

            using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            var dt = new DataTable();
            dt.Load(reader);
            return dt;
        }).ConfigureAwait(false);

        private async Task<DataRow?> GetRowByKeyAsync(SqlConnection cn, object? keyVal, CancellationToken ct)
        {
            var sql = $"SELECT TOP (1) * FROM [{Schema}].[{Table}] WHERE {Q(KeyColumn)} = @k";
            using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@k", keyVal ?? DBNull.Value);
            using var rd = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);

            var dt = new DataTable();
            dt.Load(rd);
            return dt.Rows.Count > 0 ? dt.Rows[0] : null;
        }

        private async Task<object?> TryInferKeyFromDbAsync(CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(KeyColumn)) return null;

            var cols = Values.Keys
                .Where(k => !k.Equals(KeyColumn, StringComparison.OrdinalIgnoreCase))
                .Where(k => Values[k] != null && Values[k] != DBNull.Value)
                .ToList();

            if (cols.Count == 0) return null;

            var whereParts = new List<string>();
            var ps = new List<SqlParameter>();
            int i = 0;

            foreach (var c in cols)
            {
                whereParts.Add($"{Q(c)}=@p{i}");
                ps.Add(new SqlParameter("@p" + i, Values[c]!));
                i++;
            }

            var where = string.Join(" AND ", whereParts);
            var sql = $"SELECT TOP (2) {Q(KeyColumn)} FROM [{Schema}].[{Table}] WHERE {where};";

            using var cn = await OpenAsync(ct).ConfigureAwait(false);
            using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddRange(ps.ToArray());

            using var rd = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);

            object? found = null;
            int count = 0;
            while (await rd.ReadAsync(ct).ConfigureAwait(false))
            {
                found = rd.GetValue(0);
                count++;
                if (count > 1) return null;
            }
            return count == 1 && found != DBNull.Value ? found : null;
        }

        #endregion

        #region DDL (TABLE/COLUMN CRUD)

        #region Table Manager

        // Method: CreateTable
        /// <summary>
        /// Creates the table defined by <see cref="Schema"/> and <see cref="Table"/> using the column definitions in <see cref="SchemaSpec"/>.
        /// Optionally sets a primary key column.
        /// </summary>
        /// <param name="pkName">Optional column name to use as PK when no <see cref="ColumnDef.PrimaryKey"/> is marked.</param>
        /// <returns><c>true</c> on success.</returns>
        /// <exception cref="InvalidOperationException">Thrown when <see cref="SchemaSpec"/> is empty.</exception>
        public bool CreateTable(string? pkName = null) => SafeExecute("DDL_CREATE_TABLE", extras =>
        {
            EnsureIdent(Schema); EnsureIdent(Table);
            if (SchemaSpec.Count == 0) throw new InvalidOperationException("SchemaSpec empty. Add columns before CreateTable.");

            using var cn = Open();
            using var tx = cn.BeginTransaction();
            try
            {
                foreach (var kv in SchemaSpec) kv.Value.Validate();

                var defs = SchemaSpec.Values.Select(c =>
                    $"[{c.Name}] {c.SqlType}{(c.Identity ? " IDENTITY(1,1)" : "")} {(c.Nullable ? "NULL" : "NOT NULL")}");
                var pk = SchemaSpec.Values.FirstOrDefault(x => x.PrimaryKey)
                         ?? (pkName != null && SchemaSpec.TryGetValue(pkName, out var d) ? d : null);
                var pkSql = pk is null ? "" : $", CONSTRAINT PK_{Table} PRIMARY KEY ([{pk.Name}])";

                var sql = $"CREATE TABLE [{Schema}].[{Table}] (\n  {string.Join(",\n  ", defs)}{pkSql}\n);";
                extras["sql"] = "CREATE TABLE ...";
                using var cmd = new SqlCommand(sql, cn, tx);
                cmd.ExecuteNonQuery();

                tx.Commit();
                return true;
            }
            catch { tx.Rollback(); throw; }
        });

        // Method: DropTable
        /// <summary>
        /// Drops the target table if it exists (idempotent).
        /// </summary>
        /// <returns><c>true</c> on success.</returns>
        public bool DropTable() => SafeExecute("DDL_DROP_TABLE", extras =>
        {
            EnsureIdent(Schema); EnsureIdent(Table);
            using var cn = Open();
            using var cmd = new SqlCommand(
                $"IF OBJECT_ID(@full,'U') IS NOT NULL DROP TABLE [{Schema}].[{Table}];", cn);
            cmd.Parameters.AddWithValue("@full", $"{Schema}.{Table}");
            extras["sql"] = "DROP TABLE IF EXISTS";
            cmd.ExecuteNonQuery();
            return true;
        });

        // Method: EnsureTable
        /// <summary>
        /// Ensures the target table exists. If missing, creates it from <see cref="SchemaSpec"/>.
        /// If present and <see cref="SchemaSpec"/> has entries, adds any missing columns.
        /// </summary>
        /// <param name="pkName">Optional column name to consider as PK when creating.</param>
        /// <returns><c>true</c> when the operation completes successfully.</returns>
        public bool EnsureTable(string? pkName = null) => SafeExecute("DDL_ENSURE_TABLE", extras =>
        {
            EnsureIdent(Schema); EnsureIdent(Table);
            using var cn = Open();
            using var tx = cn.BeginTransaction();
            try
            {
                var exists = TableExists(cn, tx, Schema, Table);
                extras["existsBefore"] = exists;

                if (!exists)
                {
                    if (SchemaSpec.Count == 0)
                        throw new InvalidOperationException("SchemaSpec empty. Provide definitions to create table.");
                    CreateTableInternal(cn, tx, Schema, Table, SchemaSpec, pkName ?? KeyColumn);
                }
                else if (SchemaSpec.Count > 0)
                {
                    EnsureColumnsInternal(cn, tx, Schema, Table, SchemaSpec);
                }

                tx.Commit();
                return true;
            }
            catch { tx.Rollback(); throw; }
        });

        #endregion

        #region Column Manager

        // Method: AddColumn
        /// <summary>
        /// Adds a column to the target table if it does not already exist.
        /// </summary>
        /// <param name="column">Column name.</param>
        /// <param name="sqlType">SQL type definition (e.g., "int", "nvarchar(200)").</param>
        /// <param name="nullable">Whether the column permits NULLs.</param>
        /// <param name="identity">Whether the column is an identity.</param>
        /// <returns><c>true</c> on success.</returns>
        public sealed class ColumnAddOptions
        {
            public required string Name { get; init; }                 // Column name
            public required string Type { get; init; }                 // Base type e.g. "varchar", "decimal", "int"

            public int? Length { get; init; }                          // For varchar/nvarchar/char/nchar/varbinary/binary; use -1 for MAX
            public int? Precision { get; init; }                       // For decimal/numeric
            public int? Scale { get; init; }                           // For decimal/numeric

            public bool Nullable { get; init; } = true;                // NOT NULL if false
            public bool Identity { get; init; } = false;               // IDENTITY(Seed,Inc)
            public int IdentitySeed { get; init; } = 1;
            public int IdentityIncrement { get; init; } = 1;

            public string? DefaultSql { get; init; }

            public string? ComputedExpressionSql { get; init; }
            public bool Persisted { get; init; } = false;
            public string? DefaultConstraintName { get; init; }
        }

        private const string EpName_AddedFromSoftware = "AddedFromSoftware";
        private static string EpValue_AddedFromSoftware = string.Empty;
        public static string Soft_Name
        {
            get => EpValue_AddedFromSoftware;
            set => EpValue_AddedFromSoftware = value ?? string.Empty;
        }

        private string BuildUpsertColumnExtendedPropertySql(string columnName)
        {
            string epName = EpName_AddedFromSoftware.Replace("'", "''");
            string epValue = EpValue_AddedFromSoftware.Replace("'", "''");

            return $@"
IF EXISTS (
    SELECT 1
    FROM sys.fn_listextendedproperty
    (N'{epName}', N'SCHEMA', N'{Schema}', N'TABLE', N'{Table}', N'COLUMN', N'{columnName}')
)
    EXEC sys.sp_updateextendedproperty
        @name = N'{epName}',
        @value = N'{epValue}',
        @level0type = N'SCHEMA', @level0name = N'{Schema}',
        @level1type = N'TABLE',  @level1name = N'{Table}',
        @level2type = N'COLUMN', @level2name = N'{columnName}';
ELSE
    EXEC sys.sp_addextendedproperty
        @name = N'{epName}',
        @value = N'{epValue}',
        @level0type = N'SCHEMA', @level0name = N'{Schema}',
        @level1type = N'TABLE',  @level1name = N'{Table}',
        @level2type = N'COLUMN', @level2name = N'{columnName}';
";
        }


        public bool AddColumn(string column, string sqlType, bool nullable = true, bool identity = false)
            => AddColumn(new ColumnAddOptions
            {
                Name = column,
                Type = sqlType,          // if you pass raw like "varchar(100)" it will be used as-is
                Nullable = nullable,
                Identity = identity
            });

        public bool AddColumn(ColumnAddOptions opt)
            => SafeExecute("DDL_ADD_COLUMN", extras =>
            {
                EnsureIdent(Schema);
                EnsureIdent(Table);
                EnsureIdent(opt.Name);

                using var cn = Open();

                // If the column already exists => do nothing
                using var cmd = new SqlCommand(
                    $"IF COL_LENGTH(@f,@c) IS NULL BEGIN {BuildAddColumnSql(opt, cn, extras)} END",
                    cn);

                cmd.Parameters.AddWithValue("@f", $"{Schema}.{Table}");
                cmd.Parameters.AddWithValue("@c", opt.Name);

                extras["sql"] = "ALTER TABLE ADD COLUMN (rich)";
                cmd.ExecuteNonQuery();
                return true;
            });

        private string BuildAddColumnSql(ColumnAddOptions opt, SqlConnection cn, IDictionary<string, object> extras)
        {
            // Computed column rules
            bool isComputed = !string.IsNullOrWhiteSpace(opt.ComputedExpressionSql);

            if (opt.Persisted && !isComputed)
                throw new InvalidOperationException("Persisted can be used only with a computed column (ComputedExpressionSql).");

            if (isComputed)
            {
                if (opt.Identity)
                    throw new InvalidOperationException("Identity cannot be used on a computed column.");
                if (!string.IsNullOrWhiteSpace(opt.DefaultSql))
                    throw new InvalidOperationException("Default cannot be used on a computed column.");
            }

            if (!isComputed && !opt.Nullable && string.IsNullOrWhiteSpace(opt.DefaultSql))
            {
                using var check = new SqlCommand($"SELECT TOP(1) 1 FROM [{Schema}].[{Table}];", cn);
                var hasRows = check.ExecuteScalar() != null;
                if (hasRows)
                    throw new InvalidOperationException(
                        $"Cannot add NOT NULL column [{opt.Name}] without Default on a table that already has rows. " +
                        $"Either provide DefaultSql, or add as NULL first and then backfill + ALTER to NOT NULL.");
            }

            string typeSql = isComputed ? "" : ComposeTypeSql(opt);
            string nullSql = isComputed ? "" : (opt.Nullable ? "NULL" : "NOT NULL");
            string identitySql = (!isComputed && opt.Identity) ? $" IDENTITY({opt.IdentitySeed},{opt.IdentityIncrement})" : "";

            string defaultSql = "";
            if (!isComputed && !string.IsNullOrWhiteSpace(opt.DefaultSql))
            {
                var dfName = MakeSafeConstraintName(opt.DefaultConstraintName ?? $"DF_{Table}_{opt.Name}");
                EnsureIdent(dfName);
                defaultSql = $" CONSTRAINT [{dfName}] DEFAULT ({opt.DefaultSql})";
            }

            string colSql;
            if (isComputed)
            {
                var persistedSql = opt.Persisted ? " PERSISTED" : "";
                colSql = $"ALTER TABLE [{Schema}].[{Table}] ADD [{opt.Name}] AS ({opt.ComputedExpressionSql}){persistedSql};";
            }
            else
            {
                colSql = $"ALTER TABLE [{Schema}].[{Table}] ADD [{opt.Name}] {typeSql}{identitySql}{defaultSql} {nullSql};";
            }

            extras["column"] = opt.Name;
            extras["type"] = isComputed ? "COMPUTED" : typeSql;
            extras["nullable"] = opt.Nullable;
            extras["identity"] = opt.Identity;
            extras["default"] = opt.DefaultSql ?? "";
            extras["persisted"] = opt.Persisted;

            colSql += "\n" + BuildUpsertColumnExtendedPropertySql(opt.Name);

            extras["extended_property"] = $"{EpName_AddedFromSoftware}={EpValue_AddedFromSoftware}";

            return colSql;
        }

        private string ComposeTypeSql(ColumnAddOptions opt)
        {
            // If caller passed already-formed type like "varchar(100)" or "decimal(18,2)", use it as-is.
            // (This keeps your old behavior working.)
            string t = (opt.Type ?? "").Trim();
            if (t.Contains("(") || t.Contains(" "))
                return t;

            t = t.ToLowerInvariant();

            switch (t)
            {
                // Length types
                case "varchar":
                case "nvarchar":
                case "char":
                case "nchar":
                case "varbinary":
                case "binary":
                    if (opt.Length is null)
                        throw new InvalidOperationException($"{t} requires Length (use -1 for MAX where applicable).");

                    if (opt.Length == -1)
                    {
                        if (t is "char" or "nchar" or "binary")
                            throw new InvalidOperationException($"{t} does not support MAX.");
                        return $"{t}(MAX)";
                    }

                    if (opt.Length <= 0)
                        throw new InvalidOperationException($"{t} Length must be > 0 (or -1 for MAX).");

                    return $"{t}({opt.Length})";

                case "decimal":
                case "numeric":
                    int p = opt.Precision ?? 18;
                    int s = opt.Scale ?? 0;
                    if (p < 1 || p > 38) throw new InvalidOperationException($"{t} Precision must be 1..38.");
                    if (s < 0 || s > p) throw new InvalidOperationException($"{t} Scale must be 0..Precision.");
                    return $"{t}({p},{s})";

                case "datetime2":
                case "time":
                case "datetimeoffset":
                    if (opt.Scale is null) return t;
                    if (opt.Scale < 0 || opt.Scale > 7) throw new InvalidOperationException($"{t} Scale must be 0..7.");
                    return $"{t}({opt.Scale})";

                // Common fixed types
                default:
                    return t; // int, bigint, bit, float, real, date, datetime, uniqueidentifier, etc.
            }
        }

        private static string MakeSafeConstraintName(string name)
        {
            // SQL identifier rules are bigger than this, but this makes stable safe names.
            // Keep letters/digits/_ only. Trim to 128 chars.
            var sb = new System.Text.StringBuilder(name.Length);
            foreach (var ch in name)
                sb.Append(char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_');

            var s = sb.ToString().Trim('_');
            if (string.IsNullOrWhiteSpace(s)) s = "DF_" + Guid.NewGuid().ToString("N");

            return s.Length <= 128 ? s : s.Substring(0, 128);
        }


        // Method: AlterColumn
        /// <summary>
        /// Alters a column's type and nullability.
        /// </summary>
        /// <param name="column">Column name.</param>
        /// <param name="sqlType">New SQL type.</param>
        /// <param name="nullable">New nullability.</param>
        /// <returns><c>true</c> on success.</returns>
        public sealed class ColumnAlterOptions
        {
            public required string Name { get; init; }
            public required string Type { get; init; }

            public int? Length { get; init; }      // -1 for MAX where allowed
            public int? Precision { get; init; }   // decimal/numeric
            public int? Scale { get; init; }       // decimal/numeric / datetime2 scale
            public bool Nullable { get; init; } = true;
        }

        public bool AlterColumn(ColumnAlterOptions opt)
            => SafeExecute("DDL_ALTER_COLUMN", extras =>
            {
                EnsureIdent(Schema); EnsureIdent(Table); EnsureIdent(opt.Name);

                using var cn = Open();

                // Build the type string the same way you do in AddColumn
                var sqlType = ComposeTypeSql(new ColumnAddOptions
                {
                    Name = opt.Name,
                    Type = opt.Type,
                    Length = opt.Length,
                    Precision = opt.Precision,
                    Scale = opt.Scale,
                    Nullable = opt.Nullable
                });

                using var cmd = new SqlCommand(
                    $"ALTER TABLE [{Schema}].[{Table}] ALTER COLUMN [{opt.Name}] {sqlType} {(opt.Nullable ? "NULL" : "NOT NULL")};",
                    cn);

                extras["sql"] = "ALTER TABLE ALTER COLUMN (rich)";
                cmd.ExecuteNonQuery();
                return true;
            });

        // Method: RenameColumn
        /// <summary>
        /// Renames a column using <c>sp_rename</c>.
        /// </summary>
        /// <param name="oldName">Existing column name.</param>
        /// <param name="newName">New column name.</param>
        /// <returns><c>true</c> on success.</returns>
        public bool RenameColumn(string oldName, string newName)
            => SafeExecute("DDL_RENAME_COLUMN", extras =>
            {
                EnsureIdent(Schema); EnsureIdent(Table); EnsureIdent(oldName); EnsureIdent(newName);
                using var cn = Open();
                using var cmd = new SqlCommand("EXEC sp_rename @full, @new, 'COLUMN';", cn);
                cmd.Parameters.AddWithValue("@full", $"{Schema}.{Table}.{oldName}");
                cmd.Parameters.AddWithValue("@new", newName);
                extras["sql"] = "sp_rename COLUMN";
                cmd.ExecuteNonQuery();
                return true;
            });

        // Method: DropColumn
        /// <summary>
        /// Drops a column if it exists (idempotent).
        /// </summary>
        /// <param name="column">Column name.</param>
        /// <returns><c>true</c> on success.</returns>
        public bool DropColumn(string column)
            => SafeExecute("DDL_DROP_COLUMN", extras =>
            {
                EnsureIdent(Schema); EnsureIdent(Table); EnsureIdent(column);
                using var cn = Open();
                using var cmd = new SqlCommand(
                    $"IF COL_LENGTH(@f,@c) IS NOT NULL ALTER TABLE [{Schema}].[{Table}] DROP COLUMN [{column}];", cn);
                cmd.Parameters.AddWithValue("@f", $"{Schema}.{Table}");
                cmd.Parameters.AddWithValue("@c", column);
                extras["sql"] = "ALTER TABLE DROP COLUMN";
                cmd.ExecuteNonQuery();
                return true;
            });

        #endregion

        #region Column Options
        // Method: GetOptions
        /// <summary>
        /// Returns the current set of allowed values (options) parsed from the column's CHECK constraint (if any).
        /// </summary>
        /// <param name="column">Column name.</param>
        /// <returns>Array of options; empty if none detected.</returns>
        public string[] GetOptions(string column)
        {
            var cols = GetColumns() ?? new List<ColumnInfo>();
            var ci = cols.FirstOrDefault(c => c.Name.Equals(column, StringComparison.OrdinalIgnoreCase));
            return (ci?.Options ?? Array.Empty<string>())
                   .Where(s => !string.IsNullOrWhiteSpace(s))
                   .Distinct(StringComparer.OrdinalIgnoreCase)
                   .ToArray();
        }

        // Method: AddOption
        /// <summary>
        /// Adds a single option to a column's allowed set by recreating the CHECK constraint safely.
        /// </summary>
        /// <param name="column">Column name.</param>
        /// <param name="option">Option value to add.</param>
        /// <returns><c>true</c> on success.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="option"/> is null/empty.</exception>
        public bool AddOption(string column, string option)
        {
            if (string.IsNullOrWhiteSpace(option))
                throw new ArgumentException("Option cannot be empty.", nameof(option));

            var current = GetOptions(column).ToList();
            if (!current.Contains(option, StringComparer.OrdinalIgnoreCase))
                current.Add(option);

            return SetOptions(column, current);
        }

        // Method: RemoveOption
        /// <summary>
        /// Removes an option from a column's allowed set. If no options remain, drops the CHECK constraint.
        /// </summary>
        /// <param name="column">Column name.</param>
        /// <param name="option">Option value to remove.</param>
        /// <returns><c>true</c> on success.</returns>
        public bool RemoveOption(string column, string option)
        {
            var list = GetOptions(column).ToList();
            var removed = list.RemoveAll(s => s.Equals(option, StringComparison.OrdinalIgnoreCase));
            if (removed == 0) return true; // nothing to do

            if (list.Count == 0)
                return DropOptionsConstraint(column);

            return SetOptions(column, list);
        }

        // Method: SetOptions
        /// <summary>
        /// Replaces the allowed options for a column by creating a new CHECK constraint (idempotent).
        /// Handles string vs numeric quoting automatically and respects column nullability.
        /// </summary>
        /// <param name="column">Column name.</param>
        /// <param name="options">New set of options (must contain at least one non-empty value).</param>
        /// <returns><c>true</c> on success.</returns>
        /// <exception cref="InvalidOperationException">Thrown when no options are provided or column is not found.</exception>
        public bool SetOptions(string column, IEnumerable<string> options)
            => SafeExecute("DDL_SET_OPTIONS", extras =>
            {
                EnsureIdent(Schema); EnsureIdent(Table);

                var opts = (options ?? Enumerable.Empty<string>())
                   .Where(s => !string.IsNullOrWhiteSpace(s))
                   .Distinct(StringComparer.OrdinalIgnoreCase)
                   .ToArray();

                if (opts.Length == 0)
                    throw new InvalidOperationException($"No options provided for column '{column}'.");

                // Get column metadata to know nullability and SQL type (for quoting)
                var colInfo = (GetColumns() ?? new List<ColumnInfo>())
                      .FirstOrDefault(c => c.Name.Equals(column, StringComparison.OrdinalIgnoreCase));
                if (colInfo is null)
                    throw new InvalidOperationException($"Column '{column}' not found in [{Schema}].[{Table}].");

                // Build IN(...) list with correct quoting for the column type
                var inList = BuildInList(opts, colInfo.DataType);

                var qCol = QSafe(column);

                var constraintName = MakeConstraintName(Table, column);
                var allowNull = colInfo.Nullable;

                using var cn = Open();
                using var tx = cn.BeginTransaction();
                try
                {
                    // Drop any prior "our" constraint (idempotent)
                    DropConstraintInternal(cn, tx, Schema, Table, constraintName);

                    // Build the CHECK predicate
                    var checkPredicate = allowNull
                        ? $"{qCol} IS NULL OR {qCol} IN ({inList})"
                        : $"{qCol} IN ({inList})";
                    checkPredicate = "(" + checkPredicate + ")";

                    // Create the constraint
                    var createSql = $@"
ALTER TABLE [{Schema}].[{Table}] 
ADD CONSTRAINT [{constraintName}] 
CHECK ({checkPredicate});";

                    using (var create = new SqlCommand(createSql, cn, tx))
                        create.ExecuteNonQuery();

                    AddCommonExtras(extras,
                ("constraint", constraintName),
                ("check", checkPredicate),
                ("optionsCount", opts.Length));

                    tx.Commit();
                    return true;
                }
                catch
                {
                    tx.Rollback();
                    throw;
                }
            });

        // Method: DropOptionsConstraint
        /// <summary>
        /// Drops the generated options CHECK constraint for a column (idempotent).
        /// </summary>
        /// <param name="column">Column name.</param>
        /// <returns><c>true</c> on success.</returns>
        public bool DropOptionsConstraint(string column)
         => SafeExecute("DDL_DROP_OPTIONS_CONSTRAINT", extras =>
         {
             EnsureIdent(Schema); EnsureIdent(Table);

             using var cn = Open();
             using var tx = cn.BeginTransaction();
             try
             {
                 var constraintName = MakeConstraintName(Table, column);
                 var dropped = DropConstraintInternal(cn, tx, Schema, Table, constraintName);
                 AddCommonExtras(extras, ("constraint", constraintName), ("dropped", dropped));
                 tx.Commit();
                 return true;
             }
             catch
             {
                 tx.Rollback();
                 throw;
             }
         });

        private static string MakeConstraintName(string table, string column)
        {
            string raw = $"CK_{table}_{column}_OPTIONS";
            return SanitizeForObjectName(raw, 128);
        }

        private static bool DropConstraintInternal(SqlConnection cn, SqlTransaction tx, string schema, string table, string constraintName)
        {
            const string sql = @"
IF EXISTS (
    SELECT 1
    FROM sys.check_constraints cc
    JOIN sys.tables t  ON t.object_id = cc.parent_object_id
    JOIN sys.schemas s ON s.schema_id = t.schema_id
    WHERE cc.name = @n AND t.name = @t AND s.name = @s
)
BEGIN
    DECLARE @sql nvarchar(max) = N'ALTER TABLE [' + @s + N'].[' + @t + N'] DROP CONSTRAINT [' + @n + N'];';
    EXEC (@sql);
END";
            using var cmd = new SqlCommand(sql, cn, tx);
            cmd.Parameters.AddWithValue("@n", constraintName);
            cmd.Parameters.AddWithValue("@t", table);
            cmd.Parameters.AddWithValue("@s", schema);
            return cmd.ExecuteNonQuery() >= 0; // if existed, it's dropped; idempotent
        }

        private static string BuildInList(IEnumerable<string> options, string sqlDataType)
        {
            // Decide quoting by SQL type; treat non-numeric as string
            bool isNumeric = IsNumericType(sqlDataType);

            return string.Join(",",
                options.Select(o => isNumeric
                    ? NormalizeNumericLiteral(o)
                    : QuoteSqlLiteral(o)));
        }

        private static bool IsNumericType(string sqlType)
        {
            if (string.IsNullOrWhiteSpace(sqlType)) return false;
            var t = sqlType.Trim().ToLowerInvariant();

            return t.StartsWith("int") || t.StartsWith("bigint") || t.StartsWith("smallint") ||
                   t.StartsWith("tinyint") || t.StartsWith("decimal") || t.StartsWith("numeric") ||
                   t.StartsWith("float") || t.StartsWith("real") || t.StartsWith("money") ||
                   t.StartsWith("smallmoney");
        }

        private static string QuoteSqlLiteral(string value)
        {
            // N'...' with escaped single quotes
            var escaped = (value ?? string.Empty).Replace("'", "''");
            return $"N'{escaped}'";
        }

        private static string NormalizeNumericLiteral(string raw)
        {
            // Allow simple normalization; will throw if not a number
            if (decimal.TryParse(raw, System.Globalization.NumberStyles.Any,
                                 System.Globalization.CultureInfo.InvariantCulture, out var d))
            {
                // Keep as invariant string (no quotes)
                return d.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            // If parsing fails, fallback to 0 to avoid SQL errors? Better: throw.
            throw new ArgumentException($"Option '{raw}' is not a valid numeric literal for numeric column.");
        }

        private static string QSafe(string ident)
        {
            if (string.IsNullOrWhiteSpace(ident))
                throw new ArgumentException("Identifier cannot be null/empty.");
            // Escape closing bracket inside names: a]b -> a]]b
            var safe = ident.Replace("]", "]]");
            return "[" + safe + "]";
        }

        private static string SanitizeForObjectName(string raw, int maxLen = 128)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "_";
            var chars = raw.Select(ch =>
                ch >= 'A' && ch <= 'Z' ||
                ch >= 'a' && ch <= 'z' ||
                ch >= '0' && ch <= '9' || ch == '_' ? ch : '_').ToArray();
            var s = new string(chars);
            return s.Length <= maxLen ? s : s.Substring(0, maxLen);
        }

        #endregion

        #region Extended Properties Manager

        private bool UpsertExtendedProperty(string propName, object value, string? column = null)
        {
            EnsureIdent(Schema); EnsureIdent(Table);
            if (!string.IsNullOrWhiteSpace(column)) EnsureIdent(column);

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
                    chk.Parameters.AddWithValue("@t", Table);
                    chk.Parameters.AddWithValue("@col", (object?)column ?? DBNull.Value);
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
                    cmd.Parameters.AddWithValue("@t", Table);
                    if (!string.IsNullOrWhiteSpace(column))
                        cmd.Parameters.AddWithValue("@c", column);

                    cmd.ExecuteNonQuery();
                }

                tx.Commit();
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
            EnsureIdent(Schema); EnsureIdent(Table);
            if (!string.IsNullOrWhiteSpace(column)) EnsureIdent(column);

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
            cmd.Parameters.AddWithValue("@t", Table);
            if (!string.IsNullOrWhiteSpace(column))
                cmd.Parameters.AddWithValue("@c", column);
            cmd.ExecuteNonQuery();
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
            EnsureIdent(Table);
            if (!string.IsNullOrWhiteSpace(column)) EnsureIdent(column);

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
            cmd.Parameters.Add("@table", System.Data.SqlDbType.NVarChar, 128).Value = Table;
            cmd.Parameters.Add("@column", System.Data.SqlDbType.NVarChar, 128).Value =
                (object?)column ?? DBNull.Value;

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
    => ReadExtendedProperty(propertyName, column);

        // Method: GetColumnDisplayName
        /// <summary>
        /// Gets a column's display name from extended properties; falls back to the original column name.
        /// </summary>
        /// <param name="column">Column name.</param>
        /// <returns>Display name string.</returns>
        public string GetColumnDisplayName(string column)
        {
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
            => Convert.ToString(ReadExtendedProperty("DefaultUnit", column));

        #endregion

        #region Parameter Setters/Getters
        public bool SetColumnParameter(string column, string unit)
    => UpsertExtendedProperty("Parameter", unit, column);
        public string? GetColumnParameter(string column)
            => Convert.ToString(ReadExtendedProperty("Parameter", column));

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
            => Convert.ToInt16(ReadExtendedProperty("Order", column));

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
            => Convert.ToString(ReadExtendedProperty("Format", column));

        #endregion

        #region DataGridShow Setters/Getters
        public bool SetShowInDataGrid(string column, bool value)
            => UpsertExtendedProperty("DatagridShow", value, column);

        public bool? GetShowInDataGrid(string column)
            => Convert.ToBoolean(ReadExtendedProperty("DatagridShow", column));

        #endregion

        #region DefaultUnit Setters/Getters

        public bool SetDefaultUnit(string column, string? value)
            => UpsertExtendedProperty("DefaultUnit", value ?? string.Empty, column);

        public string GetDefaultUnit(string column)
            => Convert.ToString(ReadExtendedProperty("DefaultUnit", column)) ?? string.Empty;

        #endregion

        #region InputUnit Setters/Getters

        public bool SetInputUnit(string column, string? value)
            => UpsertExtendedProperty("InputUnit", value ?? string.Empty, column);

        public string GetInputUnit(string column)
            => Convert.ToString(ReadExtendedProperty("InputUnit", column)) ?? string.Empty;

        #endregion

        #region LastUsedUnit Setters/Getters

        public bool SetLastUsedUnit(string column, string? value)
            => UpsertExtendedProperty("LastUsedUnit", value ?? string.Empty, column);

        public string GetLastUsedUnit(string column)
            => Convert.ToString(ReadExtendedProperty("LastUsedUnit", column)) ?? string.Empty;

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
            var v = ReadExtendedProperty("HideInCrudForm", column);
            if (v == null || v == DBNull.Value) return null;

            var s = Convert.ToString(v)?.Trim();
            if (string.IsNullOrWhiteSpace(s)) return null;

            return s.Equals("1", StringComparison.OrdinalIgnoreCase)
                || s.Equals("true", StringComparison.OrdinalIgnoreCase)
                || s.Equals("yes", StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region Internal DDL Helpers

        // Method: GetColumns
        /// <summary>
        /// Returns detailed column metadata for the target table, including PK/FK flags, identity,
        /// defaults, CHECK constraints, parsed option lists, and selected extended properties
        /// (DisplayName, Description, Unit, Format, Order, Visible).
        /// </summary>
        /// <returns>List of <see cref="ColumnInfo"/> items (can be empty).</returns>
        public List<ColumnInfo>? GetColumns()
    => SafeExecute("DDL_GET_COLUMNS", extras =>
    {
        EnsureIdent(Schema);
        EnsureIdent(Table);

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

    -- CHECK constraints aggregation
    ca.CheckDefinition,

    -- ===== Extended Properties =====
    xp.DisplayName,
    xp.[Description],
    xp.[DefaultUnit],
    xp.[InputUnit],
    xp.[LastUsedUnit],
    xp.[Format],
    xp.[Parameter],
    xp.[Order],
    xp.[DatagridShow],
    xp.[HideInCrudForm],
    xp.[Visible],
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

-- CHECK
OUTER APPLY (
    SELECT STRING_AGG(cc.definition, ' AND ') AS CheckDefinition
    FROM sys.check_constraints cc
    WHERE cc.parent_object_id = c.object_id
      AND cc.definition LIKE '%[' + c.name + ']%'
) ca

-- Extended Properties (normalized)
OUTER APPLY (
    SELECT
        MAX(CASE WHEN ep.name='DisplayName' THEN CAST(ep.value AS nvarchar(256)) END) AS DisplayName,
        MAX(CASE WHEN ep.name='Description' THEN CAST(ep.value AS nvarchar(max)) END) AS [Description],
        MAX(CASE WHEN ep.name='DefaultUnit' THEN CAST(ep.value AS nvarchar(64)) END) AS [DefaultUnit],
        MAX(CASE WHEN ep.name='InputUnit' THEN CAST(ep.value AS nvarchar(64)) END) AS [InputUnit],
        MAX(CASE WHEN ep.name='LastUsedUnit' THEN CAST(ep.value AS nvarchar(64)) END) AS [LastUsedUnit],
        MAX(CASE WHEN ep.name='Format' THEN CAST(ep.value AS nvarchar(64)) END) AS [Format],
        MAX(CASE WHEN ep.name='Parameter' THEN CAST(ep.value AS nvarchar(256)) END) AS [Parameter],
        MAX(TRY_CAST(CASE WHEN ep.name='Order' THEN ep.value END AS int)) AS [Order],

        -- Boolean normalization
                MAX(CASE WHEN ep.name='DatagridShow'
                 THEN CASE WHEN LOWER(CAST(ep.value AS nvarchar(10))) IN ('1','true','yes') THEN 1 ELSE 0 END
            END) AS DatagridShow,

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
        cmd.Parameters.AddWithValue("@t", Table);
        cmd.Parameters.AddWithValue("@s", Schema);

        var list = new List<ColumnInfo>();

        using var rd = cmd.ExecuteReader();

        while (rd.Read())
        {
            var ci = new ColumnInfo
            {
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
                CheckDefinition = rd["CheckDefinition"] as string,

                DisplayName = rd["DisplayName"] as string,
                Description = rd["Description"] as string,
                DefaultUnit = rd["DefaultUnit"] as string,
                InputUnit = rd["InputUnit"] as string,
                LastUsedUnit = rd["LastUsedUnit"] as string,
                Format = rd["Format"] as string,
                Parameter = rd["Parameter"] as string,
                Order = rd["Order"] as int?,

                DatagridShow = rd["DatagridShow"] as int? == 1,
                HideInCrudForm = rd["HideInCrudForm"] as int? == 1,   // <-- ADD THIS
                Visible = rd["Visible"] as int? == 1,
                SoftName = rd["SoftName"] as string
            };

            ci.DefaultValue = TryParseDefaultValue(ci.DefaultSql);
            ci.Options = TryParseOptionsFromCheck(ci.CheckDefinition, ci.Name);
            ci.HasOptions = (ci.Options?.Length ?? 0) >= 2;
            ci.Unit = "";

            list.Add(ci);
        }

        extras["count"] = list.Count;
        return list
    .OrderBy(c => c.Order ?? int.MaxValue)
    .ToList();
    });

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
                    col.Caption = disp;
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

        private static string[]? TryParseOptionsFromCheck(string? checkDefinition, string columnName)
        {
            if (string.IsNullOrWhiteSpace(checkDefinition) || string.IsNullOrWhiteSpace(columnName))
                return null;

            var colEsc = Regex.Escape(columnName);
            var colPattern = $@"(?:\[\s*{colEsc}\s*\]|\b{colEsc}\b)";

            var def = checkDefinition;

            var inMatch = Regex.Match(def, $@"{colPattern}\s+IN\s*\(\s*(?<list>[^)]+)\)",
                                      RegexOptions.IgnoreCase);
            if (inMatch.Success)
            {
                var list = inMatch.Groups["list"].Value;
                var tokens = new List<string>();
                int i = 0;
                while (i < list.Length)
                {
                    while (i < list.Length && (char.IsWhiteSpace(list[i]) || list[i] == ',')) i++;
                    if (i >= list.Length) break;

                    if (list[i] == '\'')
                    {
                        int start = ++i;
                        var sb = new System.Text.StringBuilder();
                        while (i < list.Length)
                        {
                            if (list[i] == '\'' && i + 1 < list.Length && list[i + 1] == '\'')
                            { sb.Append('\''); i += 2; continue; }
                            if (list[i] == '\'') { i++; break; }
                            sb.Append(list[i]); i++;
                        }
                        tokens.Add(sb.ToString());
                    }
                    else
                    {
                        int start = i;
                        while (i < list.Length && list[i] != ',' && list[i] != ')') i++;
                        tokens.Add(list.Substring(start, i - start).Trim());
                    }
                }

                var optsIn = tokens.Where(t => !string.IsNullOrWhiteSpace(t))
                                   .Distinct(StringComparer.OrdinalIgnoreCase)
                                   .ToArray();
                return optsIn.Length >= 2 ? optsIn : null;
            }

            var strMatches = Regex.Matches(checkDefinition,
                    $@"{colPattern}\s*=\s*\(?\s*N?'((?:''|[^'])*)'\s*\)?",
                    RegexOptions.IgnoreCase)
                .Cast<Match>()
                .Select(m => m.Groups[1].Value.Replace("''", "'"))
                .Where(s => !string.IsNullOrWhiteSpace(s));

            var numMatches = Regex.Matches(checkDefinition,
                    $@"{colPattern}\s*=\s*\(?\s*([-+]?\d+(?:\.\d+)?)\s*\)?",
                    RegexOptions.IgnoreCase)
                .Cast<Match>()
                .Select(m => m.Groups[1].Value.Trim());


            var options = strMatches.Concat(numMatches)
                                    .Distinct(StringComparer.OrdinalIgnoreCase)
                                    .ToArray();

            return options.Length >= 2 ? options : null;
        }

        #endregion

        #region Auto-Join (zero-config)

        // Method: AutoSelectWithJoins
        /// <summary>
        /// Builds and executes a SELECT that automatically joins all detected foreign keys
        /// from the base table to their referenced tables, optionally as LEFT JOINs.
        /// Includes all base table columns plus referenced table columns (prefixed as Table__Column).
        /// </summary>
        /// <param name="whereSql">Optional WHERE clause.</param>
        /// <param name="parameters">Optional parameters for WHERE.</param>
        /// <param name="leftJoin">When true, uses LEFT JOIN; otherwise INNER JOIN.</param>
        /// <param name="orderBy">Optional ORDER BY clause. Defaults to key column if available.</param>
        /// <param name="includeRefKeyColumns">When true, includes referenced key columns in the projection.</param>
        /// <param name="defaultRefSchema">Optional schema to assume for referenced tables when not specified.</param>
        /// <returns>A <see cref="DataTable"/> with the joined results.</returns>
        public DataTable? AutoSelectWithJoins(
            string? whereSql = null,
            IDictionary<string, object?>? parameters = null,
            bool leftJoin = true,
            string? orderBy = null,
            bool includeRefKeyColumns = false,
            string? defaultRefSchema = null
        )
        => SafeExecute("SELECT_AUTO_JOIN_ALL_FK", extras =>
        {
            EnsureIdent(Schema); EnsureIdent(Table);

            var baseCols = GetColumns() ?? new List<ColumnInfo>();
            var fkCols = baseCols.Where(c =>
                    c.IsForeignKey &&
                    !string.IsNullOrWhiteSpace(c.ReferencedTable) &&
                    !string.IsNullOrWhiteSpace(c.ReferencedColumn))
                .ToList();

            var tBase = "b";
            var selectParts = new List<string>();

            // Base table columns
            foreach (var c in baseCols)
                selectParts.Add($"{tBase}.{QSafe(c.Name)}");

            string QTbl(string s, string t) => $"{QSafe(s)}.{QSafe(t)}";
            string QC(string alias, string col) { EnsureIdent(col); return $"{alias}.{QSafe(col)}"; }

            var joins = new List<string>();
            var aliasIndex = 1;

            foreach (var fk in fkCols)
            {
                var refSchema = string.IsNullOrWhiteSpace(defaultRefSchema) ? Schema : defaultRefSchema!;
                EnsureIdent(refSchema);
                var refTable = fk.ReferencedTable!;
                var refColumn = fk.ReferencedColumn!;
                EnsureIdent(refTable); EnsureIdent(refColumn);

                var alias = "r" + aliasIndex++;
                var joinKind = leftJoin ? "LEFT JOIN" : "INNER JOIN";

                joins.Add($"{joinKind} {QTbl(refSchema, refTable)} {alias} ON {QC(tBase, fk.Name)} = {QC(alias, refColumn)}");

                // Get referenced table columns
                var refCols = GetTableColumnNames(refSchema, refTable);

                IEnumerable<string> colsToInclude = includeRefKeyColumns
                    ? refCols
                    : refCols.Where(cn => !cn.Equals(refColumn, StringComparison.OrdinalIgnoreCase));

                foreach (var rc in colsToInclude)
                    selectParts.Add($"{alias}.{QSafe(rc)} AS {QSafe($"{rc}")}");

            }

            string orderClause;
            if (!string.IsNullOrWhiteSpace(orderBy))
                orderClause = $"ORDER BY {orderBy}";
            else if (!string.IsNullOrWhiteSpace(KeyColumn))
                orderClause = $"ORDER BY {tBase}.{QSafe(KeyColumn)}";
            else
                orderClause = "";

            var sql =
                $"SELECT {string.Join(", ", selectParts)} " +
                $"FROM {QTbl(Schema, Table)} {tBase} " +
                string.Join(" ", joins) + " " +
                (string.IsNullOrWhiteSpace(whereSql) ? "" : $"WHERE {whereSql} ") +
                orderClause;

            using var cn = Open();
            using var da = new SqlDataAdapter(sql, cn);

            if (parameters != null)
            {
                da.SelectCommand!.Parameters.Clear();
                foreach (var kv in parameters)
                {
                    var name = kv.Key.StartsWith("@") ? kv.Key : "@" + kv.Key;
                    var p = da.SelectCommand.Parameters.Add(name, SqlDbType.NVarChar);
                    p.Value = kv.Value ?? DBNull.Value;
                }
            }

            AddCommonExtras(extras,
                ("sql", "SELECT auto-join all FKs"),
                ("fkCount", fkCols.Count),
                ("leftJoin", leftJoin));

            var dt = new DataTable();
            da.Fill(dt);
            return dt;
        });

        private List<string> GetTableColumnNames(string schema, string table)
        {
            EnsureIdent(schema);
            EnsureIdent(table);

            using var cn = Open();
            using var cmd = new SqlCommand(@"
SELECT c.name
FROM sys.columns c
JOIN sys.tables  t ON t.object_id = c.object_id
JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE s.name = @s AND t.name = @t
ORDER BY c.column_id;", cn);

            cmd.Parameters.AddWithValue("@s", schema);
            cmd.Parameters.AddWithValue("@t", table);

            var list = new List<string>();
            using var rd = cmd.ExecuteReader();
            while (rd.Read()) list.Add(rd.GetString(0));
            return list;
        }

        #endregion

        #endregion

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
            var tableVerbose = GetTableDisplayName();

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
            var sql = $"SELECT TOP (1) * FROM [{Schema}].[{Table}] WHERE {Q(KeyColumn)} = @k";
            using var da = new SqlDataAdapter(sql, cn);
            da.SelectCommand!.Parameters.AddWithValue("@k", keyVal ?? DBNull.Value);
            var dt = new DataTable();
            da.Fill(dt);
            return dt.Rows.Count > 0 ? dt.Rows[0] : null;
        }

        private string BuildChangeSummary(DataRow existing)
        {
            var lines = new List<string>();
            foreach (var kv in Values)
            {
                var col = kv.Key;
                if (col.Equals(KeyColumn, StringComparison.OrdinalIgnoreCase)) continue; // skip PK
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
            public bool IsPrimaryKey { get; set; }
            public string Name { get; set; } = "";
            public string DataType { get; set; } = "";
            public short MaxLength { get; set; }
            public byte Precision { get; set; }
            public byte Scale { get; set; }
            public bool Nullable { get; set; }
            public bool Identity { get; set; }
            public string? DefaultSql { get; set; }
            public object? DefaultValue { get; set; }
            public string? CheckDefinition { get; set; }
            public string? SoftName { get; set; }
            public string[]? Options { get; set; }
            public bool HasOptions { get; set; }

            // FK:
            public bool IsForeignKey { get; set; }
            public string? ForeignKeyName { get; set; }
            public string? ReferencedTable { get; set; }
            public string? ReferencedColumn { get; set; }

            // Verbose
            public string? DisplayName { get; set; }
            public string? Description { get; set; }
            public string? Unit { get; set; }
            public string? DefaultUnit { get; set; }
            public string? InputUnit { get; set; }
            public string? LastUsedUnit { get; set; }
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

            var missingRequired = required.Where(r => !Values.ContainsKey(r) || Values[r] is null).ToList();
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

        #region logger 

        // ---------- Logger injection (set once at startup) ----------
        private static Action<LogLevel, string, string, Exception?, Dictionary<string, object>?>? _logSink;

        public static Action<LogLevel, string, string, Exception?, Dictionary<string, object>?>? LogSink
        {
            get => Volatile.Read(ref _logSink);
            set
            {
                if (value is null) throw new ArgumentNullException(nameof(LogSink));
                if (Interlocked.CompareExchange(ref _logSink, value, null) != null)
                    throw new InvalidOperationException("LogSink already set. Set it once at startup.");
            }
        }

        private static void WriteLog(LogLevel level, string message, string source, Exception? ex, Dictionary<string, object> extras)
        {
            var sink = Volatile.Read(ref _logSink);
            if (sink != null)
            {
                sink(level, message, source, ex, extras);
                return;
            }

            // fallback (still works even if LogSink is not set)
            Aarohi.Core.Logger._logger.Log(level, message, source, ex, extras);
        }

        #endregion

        public enum ForeignFilterDirection
        {
            BaseToReference,
            ReferenceToBase
        }

        public sealed class RelationFilter
        {
            public ForeignFilterDirection Direction { get; set; }

            // Base table column involved in relation
            public string BaseColumn { get; set; } = string.Empty;

            // Related table info
            public string RefSchema { get; set; } = "dbo";
            public string RefTable { get; set; } = string.Empty;
            public string RefColumn { get; set; } = string.Empty;

            // Column in related table on which filtering is needed
            public string FilterColumn { get; set; } = string.Empty;

            // Multiple values support
            public List<object?> Values { get; set; } = new();
        }
        public DataTable? SelectWithRelationFilters(
     List<RelationFilter> relationFilters,
     string? whereSql = null,
     IDictionary<string, object?>? parameters = null,
     int? top = null,
     string? orderBy = null,
     bool leftJoin = true,
     bool displayName = true)
 => SafeExecute("SELECT_WITH_RELATION_FILTERS", extras =>
 {
     EnsureIdent(Schema);
     EnsureIdent(Table);

     relationFilters ??= new List<RelationFilter>();

     string baseAlias = "A";
     var joinClauses = new List<string>();
     var whereClauses = new List<string>();
     var sqlParams = new List<SqlParameter>();
     int paramIndex = 0;
     int joinIndex = 1;

     foreach (var rf in relationFilters)
     {
         if (rf == null) continue;

         EnsureIdent(rf.BaseColumn);
         EnsureIdent(rf.RefSchema);
         EnsureIdent(rf.RefTable);
         EnsureIdent(rf.RefColumn);
         EnsureIdent(rf.FilterColumn);

         string refAlias = "J" + joinIndex++;
         string joinType = leftJoin ? "LEFT JOIN" : "INNER JOIN";

         string joinClause;

         if (rf.Direction == ForeignFilterDirection.BaseToReference)
         {
             joinClause =
                 $"{joinType} [{rf.RefSchema}].[{rf.RefTable}] {refAlias} " +
                 $"ON {baseAlias}.[{rf.BaseColumn}] = {refAlias}.[{rf.RefColumn}]";
         }
         else
         {
             joinClause =
                 $"{joinType} [{rf.RefSchema}].[{rf.RefTable}] {refAlias} " +
                 $"ON {refAlias}.[{rf.RefColumn}] = {baseAlias}.[{rf.BaseColumn}]";
         }

         joinClauses.Add(joinClause);

         var validValues = (rf.Values ?? new List<object?>())
             .Where(v => v != null && v != DBNull.Value && !string.IsNullOrWhiteSpace(Convert.ToString(v)))
             .ToList();

         if (validValues.Count > 0)
         {


             var inParams = new List<string>();

             foreach (var val in validValues)
             {
                 string pName = "@rf" + paramIndex++;
                 inParams.Add(pName);
                 sqlParams.Add(new SqlParameter(pName, val ?? DBNull.Value));
             }

             whereClauses.Add($"{refAlias}.[{rf.FilterColumn}] IN ({string.Join(", ", inParams)})");
         }
     }

     if (!string.IsNullOrWhiteSpace(whereSql))
         whereClauses.Add("(" + whereSql + ")");

     string sql =
         $"SELECT {(top.HasValue ? "TOP " + top.Value + " " : "")}DISTINCT {baseAlias}.* " +
         $"FROM [{Schema}].[{Table}] {baseAlias} " +
         (joinClauses.Count > 0 ? string.Join(" ", joinClauses) + " " : "") +
         (whereClauses.Count > 0 ? "WHERE " + string.Join(" AND ", whereClauses) + " " : "") +
         (!string.IsNullOrWhiteSpace(orderBy) ? "ORDER BY " + orderBy : "");

     using var cn = Open();
     using var da = new SqlDataAdapter(sql, cn);

     da.SelectCommand!.Parameters.Clear();

     foreach (var p in sqlParams)
         da.SelectCommand.Parameters.Add(p);

     if (parameters != null)
     {
         foreach (var kv in parameters)
         {
             var name = kv.Key.StartsWith("@") ? kv.Key : "@" + kv.Key;
             if (!da.SelectCommand.Parameters.Contains(name))
                 da.SelectCommand.Parameters.AddWithValue(name, kv.Value ?? DBNull.Value);
         }
     }

     AddCommonExtras(extras,
         ("sql", sql),
         ("relationFilterCount", relationFilters.Count),
         ("joinCount", joinClauses.Count),
         ("where", whereSql ?? ""));

     var dt = new DataTable();
     da.Fill(dt);

     dt = ReorderColumnsByMetadataOrder(dt);

     if (displayName)
         dt = ApplyDisplayNames(dt);

     return dt;
 });



    }
}