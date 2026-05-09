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
            var physicalTable = ResolveTableName();

            if (Values.Count == 0)
                throw new InvalidOperationException("No Values to insert.");

            List<ColumnInfo> infos = GetColumns();
            var physicalValues = ResolveValuesToPhysicalColumns(infos);
            var cols = physicalValues.Keys.ToArray();
            var qCols = cols.Select(QSafe).ToArray();
            var parNames = cols.Select((_, i) => $"@p{i}").ToArray();
            var physicalKeyColumn = ResolveKeyColumnName(infos);

            ValidateValuesAgainstTable();

            var identitySql = string.IsNullOrWhiteSpace(physicalKeyColumn)
                ? ""
                : $"IF COLUMNPROPERTY(object_id('{Schema}.{physicalTable}'), '{physicalKeyColumn}', 'IsIdentity')=1 SELECT SCOPE_IDENTITY();";

            var sql = $"INSERT INTO [{Schema}].[{physicalTable}] ({string.Join(",", qCols)}) " +
                      $"VALUES ({string.Join(",", parNames)}); {identitySql}";

            using var cn = Open();
            using var cmd = new SqlCommand(sql, cn);

            for (int i = 0; i < cols.Length; i++)
            {
                string colName = cols[i];
                object? val = physicalValues[colName];

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

                if (!string.IsNullOrWhiteSpace(physicalKeyColumn) &&
                    TryGetValueForColumn(physicalKeyColumn, infos, out var providedKey) &&
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
            EnsureIdent(Schema);
            var physicalTable = ResolveTableName();
            var columns = GetColumns() ?? new List<ColumnInfo>();
            var physicalKeyColumn = ResolveKeyColumnName(columns);
            EnsureIdent(physicalKeyColumn);

            if (!TryGetValueForColumn(physicalKeyColumn, columns, out var keyVal))
                throw new InvalidOperationException($"Values must include '{KeyColumn}' for Update.");

            using var cn = Open();

            var existing = GetRowByKey(cn, keyVal);
            if (existing == null)
                return 0; // no such row

            var physicalValues = ResolveValuesToPhysicalColumns(columns);
            var changedCols = new List<string>();
            foreach (var c in physicalValues.Keys)
            {
                if (c.Equals(physicalKeyColumn, StringComparison.OrdinalIgnoreCase)) continue;
                if (!existing.Table.Columns.Contains(c)) continue;

                var oldVal = existing[c];
                var newVal = physicalValues[c] ?? DBNull.Value;

                if (!ValueEquals(oldVal, newVal)) // helper from earlier message
                    changedCols.Add(c);
            }

            if (changedCols.Count == 0)
            {
                return 0;
            }
            ValidateValuesAgainstTable();
            var sets = string.Join(",", changedCols.Select(c => $"{Q(c)}=@{c}"));
            var sql = $"UPDATE [{Schema}].[{physicalTable}] SET {sets} WHERE {Q(physicalKeyColumn)}=@__key";

            using var cmd = new SqlCommand(sql, cn);

            // Bind only changed columns
            foreach (var c in changedCols)
                cmd.Parameters.AddWithValue("@" + c, physicalValues[c] ?? DBNull.Value);

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
                    var newS = physicalValues[c] == null ? "NULL" : Convert.ToString(physicalValues[c]) ?? "";

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
            bool ignoreNullUpdates = false, // if true: NULL in source will NOT overwrite target
            int? bulkCopyTimeoutSeconds = null
        )
        {
            return SafeExecute("BULK_UPSERT_DT", extras =>
            {
                if (dt == null) throw new ArgumentNullException(nameof(dt));
                if (dt.Rows.Count == 0) return (0, 0);

                EnsureIdent(Schema);
                var physicalTable = ResolveTableName();

                var key = (keyColumn ?? KeyColumn)?.Trim();
                if (string.IsNullOrWhiteSpace(key))
                    throw new InvalidOperationException("BulkUpsert requires a key column. Provide keyColumn or set KeyColumn.");

                // Read destination metadata
                var colsMeta = GetColumns() ?? new List<ColumnInfo>();
                key = ResolveColumnName(key, colsMeta);
                EnsureIdent(key);

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

MERGE [{Schema}].[{physicalTable}] WITH (HOLDLOCK) AS {tgt}
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
                        bcp.BulkCopyTimeout = ResolveBulkCopyTimeoutSeconds(bulkCopyTimeoutSeconds);

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

MERGE [{Schema}].[{physicalTable}] WITH (HOLDLOCK) AS {tgt}
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
                      bool autoTrimColumns = true,
                      int? bulkCopyTimeoutSeconds = null)
        {
            return SafeExecute("BULK_INSERT_DT", extras =>
            {
                if (dt == null) throw new ArgumentNullException(nameof(dt));
                if (dt.Rows.Count == 0) return 0;

                EnsureIdent(Schema);
                var physicalTable = ResolveTableName();

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
                        DestinationTableName = $"[{Schema}].[{physicalTable}]",
                        BatchSize = batchSize,
                        BulkCopyTimeout = ResolveBulkCopyTimeoutSeconds(bulkCopyTimeoutSeconds)
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
            var columns = GetColumns() ?? new List<ColumnInfo>();
            var physicalTable = ResolveTableName();
            var physicalKeyColumn = ResolveKeyColumnName(columns);

            // Try to recover missing key from DB (based on non-key columns)
            if (!string.IsNullOrWhiteSpace(physicalKeyColumn) &&
                (!TryGetValueForColumn(physicalKeyColumn, columns, out var keyCandidate) || IsEmptyKey(keyCandidate)))
            {
                var inferred = TryInferKeyFromDb();
                if (inferred != null && !IsEmptyKey(inferred))
                    Values[KeyColumn] = inferred;
            }

            // No configured key? -> insert only
            if (string.IsNullOrWhiteSpace(physicalKeyColumn))
            {
                var id0 = Insert();
                AddCommonExtras(extras, ("mode", "insert(no-key)"), ("newId", id0));
                return id0;
            }

            // Re-check for key value
            if (!TryGetValueForColumn(physicalKeyColumn, columns, out var keyVal) || IsEmptyKey(keyVal))
            {
                var id1 = Insert();
                if (id1 != null) Values[KeyColumn] = id1;
                AddCommonExtras(extras, ("mode", "insert"), ("newId", id1));
                return id1;
            }
            // Does the row already exist?
            bool exists;
            using (var cn = Open())
            using (var cmd = new SqlCommand($"SELECT COUNT(*) FROM [{Schema}].[{physicalTable}] WHERE {Q(physicalKeyColumn)} = @key", cn))
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
                        $"⚠️ Warning: No rows were updated in table [{Schema}].[{physicalTable}]." + Environment.NewLine +
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

            var columns = GetColumns() ?? new List<ColumnInfo>();
            var physicalTable = ResolveTableName();
            var physicalKeyColumn = ResolveKeyColumnName(columns);
            var physicalValues = ResolveValuesToPhysicalColumns(columns);

            var cols = physicalValues.Keys
                .Where(k => !k.Equals(physicalKeyColumn, StringComparison.OrdinalIgnoreCase))
                .Where(k => physicalValues[k] != null && physicalValues[k] != DBNull.Value)
                .ToList();

            if (cols.Count == 0) return null;

            var whereParts = new List<string>();
            var ps = new List<SqlParameter>();
            int i = 0;

            foreach (var c in cols)
            {
                whereParts.Add($"{Q(c)}=@p{i}");
                ps.Add(new SqlParameter("@p" + i, physicalValues[c]!));
                i++;
            }

            var where = string.Join(" AND ", whereParts);
            var sql = $"SELECT TOP (2) {Q(physicalKeyColumn)} FROM [{Schema}].[{physicalTable}] WHERE {where};";

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
            EnsureIdent(Schema);
            var columns = GetColumns() ?? new List<ColumnInfo>();
            var physicalTable = ResolveTableName();
            var physicalKeyColumn = ResolveKeyColumnName(columns);
            EnsureIdent(physicalKeyColumn);

            var sql = $"DELETE FROM [{Schema}].[{physicalTable}] WHERE {Q(physicalKeyColumn)}=@k";

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

        #endregion
    }
}
