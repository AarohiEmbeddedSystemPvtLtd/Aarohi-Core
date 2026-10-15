using Aarohi.Core.Logger;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
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

            List<ColumnInfo> infos = GetColumns() ?? new List<ColumnInfo>();
            var physicalValues = ResolveValuesToPhysicalColumns(infos);
            var cols = physicalValues.Keys.ToArray();
            var qCols = cols.Select(QSafe).ToArray();
            var parNames = cols.Select((_, i) => $"@p{i}").ToArray();
            var physicalKeyColumn = ResolveKeyColumnName(infos);

            ValidateValuesAgainstTable();

            string identitySql;
            if (SqlType == SqlType.Sqlite)
            {
                identitySql = "; SELECT last_insert_rowid();";
            }
            else if (SqlType == SqlType.PostgreSql)
            {
                identitySql = !string.IsNullOrWhiteSpace(physicalKeyColumn)
                    ? $" RETURNING {QSafe(physicalKeyColumn)};"
                    : "";
            }
            else if (!string.IsNullOrWhiteSpace(physicalKeyColumn))
            {
                identitySql = $"; IF COLUMNPROPERTY(object_id('{Schema}.{physicalTable}'), '{physicalKeyColumn}', 'IsIdentity')=1 SELECT SCOPE_IDENTITY();";
            }
            else
            {
                identitySql = "";
            }

            var sql = $"INSERT INTO {FormatTableName()} ({string.Join(",", qCols)}) VALUES ({string.Join(",", parNames)}){identitySql}";

            using var cn = Open();
            using var cmd = CreateCommand(cn, sql);

            for (int i = 0; i < cols.Length; i++)
            {
                string colName = cols[i];
                object? val = physicalValues[colName];

                ColumnInfo? info = infos.FirstOrDefault(x => x.Name.Equals(colName, StringComparison.OrdinalIgnoreCase));

                if (info == null)
                    throw new Exception($"Column metadata not found for {colName}");

                if (val is string s && string.IsNullOrWhiteSpace(s) && info.Nullable)
                    val = DBNull.Value;

                if (cmd is SqlCommand sqlCmd)
                {
                    string dataType = info.DataType;
                    var param = sqlCmd.Parameters.Add(parNames[i], GetSqlDbType(dataType));

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
                else
                {
                    AddCommandParameter(cmd, parNames[i], val);
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
                if (id == null || id == DBNull.Value) return null;
                return id;
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

                if (!ValueEquals(oldVal, newVal))
                    changedCols.Add(c);
            }

            if (changedCols.Count == 0)
            {
                return 0;
            }
            ValidateValuesAgainstTable();
            var sets = string.Join(",", changedCols.Select(c => $"{QSafe(c)}=@{c}"));
            var sql = $"UPDATE {FormatTableName()} SET {sets} WHERE {QSafe(physicalKeyColumn)}=@__key";

            using var cmd = CreateCommand(cn, sql);

            // Bind only changed columns
            foreach (var c in changedCols)
                AddCommandParameter(cmd, "@" + c, physicalValues[c] ?? DBNull.Value);

            AddCommandParameter(cmd, "@__key", keyVal ?? DBNull.Value);

            int affected;

            try
            {
                affected = cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                var sb = new StringBuilder();
                sb.AppendLine(ex.Message);
                sb.AppendLine("---- SQL ----");
                sb.AppendLine(cmd.CommandText);
                sb.AppendLine("---- Params ----");

                foreach (DbParameter p in cmd.Parameters)
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
                    var was = existing[c];
                    var isNow = after != null && after.Table.Columns.Contains(c) ? after[c] : "<missing>";
                    deltas.Add($"{c}: had '{was}' ({was?.GetType().Name}), DB now has '{isNow}' ({isNow?.GetType().Name})");
                }

                var msg =
                    $"UPDATE affected 0 rows for [{Schema}].[{physicalTable}] ({physicalKeyColumn} = '{keyVal}').\n" +
                    $"Command: {cmd.CommandText}\n" +
                    $"Parameters:\n" +
                    string.Join("\n", cmd.Parameters.Cast<DbParameter>().Select(p => $"  {p.ParameterName} = {p.Value}")) + "\n" +
                    $"Deltas attempted:\n  " + string.Join("\n  ", deltas);

                WriteLog(LogLevel.Warn, msg, LogSource, null, new Dictionary<string, object>
                {
                    ["table"] = physicalTable,
                    ["schema"] = Schema,
                    ["key"] = keyVal?.ToString() ?? "",
                    ["changedCols"] = changedCols
                });
            }

            return affected;
        }

        public (int Inserted, int Updated) BulkUpsert(
            DataTable dt,
            string? keyColumn = null,
            int batchSize = 2000,
            bool useTransaction = true,
            bool autoTrimColumns = true,
            bool ignoreNullUpdates = false,
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

                var colsMeta = GetColumns() ?? new List<ColumnInfo>();
                key = ResolveColumnName(key, colsMeta);
                EnsureIdent(key);

                var destCols = new HashSet<string>(colsMeta.Select(c => c.Name), StringComparer.OrdinalIgnoreCase);
                var dtCols = dt.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToList();

                if (!dt.Columns.Contains(key))
                    throw new InvalidOperationException($"BulkUpsert DataTable must contain key column '{key}'.");

                var writeCols = dtCols.Where(c => destCols.Contains(c)).ToList();

                if (!autoTrimColumns)
                {
                    var unknown = dtCols.Where(c => !destCols.Contains(c)).ToList();
                    if (unknown.Count > 0)
                        throw new InvalidOperationException("Unknown columns in DataTable: " + string.Join(", ", unknown));
                }

                if (!writeCols.Contains(key, StringComparer.OrdinalIgnoreCase))
                    writeCols.Add(key);

                var updateCols = writeCols
                    .Where(c => !c.Equals(key, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (writeCols.Count == 0)
                    throw new InvalidOperationException("No matching columns between DataTable and destination table.");

                using var cn = Open();
                using var tx = useTransaction ? cn.BeginTransaction() : null;

                if (SqlType == SqlType.Sqlite || SqlType == SqlType.PostgreSql)
                {
                    try
                    {
                        var qKey = QSafe(key);
                        var qWriteCols = writeCols.Select(QSafe).ToList();
                        var parNames = writeCols.Select((_, idx) => $"@p{idx}").ToList();

                        var updateAssignments = updateCols.Select(c => $"{QSafe(c)} = EXCLUDED.{QSafe(c)}").ToList();
                        var conflictClause = updateAssignments.Count > 0
                            ? $"ON CONFLICT ({qKey}) DO UPDATE SET {string.Join(", ", updateAssignments)}"
                            : $"ON CONFLICT ({qKey}) DO NOTHING";

                        var upsertSql = $"INSERT INTO {FormatTableName()} ({string.Join(", ", qWriteCols)}) VALUES ({string.Join(", ", parNames)}) {conflictClause};";
                        using var cmd = CreateCommand(cn, upsertSql, tx);
                        for (int i = 0; i < writeCols.Count; i++)
                        {
                            var colName = writeCols[i];
                            var colType = dt.Columns.Contains(colName) ? dt.Columns[colName].DataType : typeof(string);
                            AddCommandParameter(cmd, parNames[i], null, TypeToDbType(colType));
                        }

                        int processed = 0;
                        foreach (DataRow row in dt.Rows)
                        {
                            for (int i = 0; i < writeCols.Count; i++)
                            {
                                cmd.Parameters[i].Value = row[writeCols[i]] ?? DBNull.Value;
                            }
                            cmd.ExecuteNonQuery();
                            processed++;
                        }

                        tx?.Commit();
                        return (processed, 0);
                    }
                    catch
                    {
                        tx?.Rollback();
                        throw;
                    }
                }
                else
                {
                    // SQL Server MERGE pattern via #tmp_upsert
                    string tmpName = "#tmp_upsert";
                    var metaByName = colsMeta.ToDictionary(c => c.Name, c => c, StringComparer.OrdinalIgnoreCase);

                    string BuildSqlType(ColumnInfo ci)
                    {
                        var t = (ci.DataType ?? "").ToLowerInvariant();

                        if (t is "varchar" or "nvarchar" or "char" or "nchar" or "varbinary" or "binary")
                        {
                            if (ci.MaxLength < 0) return $"{t}(max)";

                            int len = ci.MaxLength;
                            if (t is "nvarchar" or "nchar") len = len / 2;
                            if (len <= 0) len = 1;
                            return $"{t}({len})";
                        }

                        if (t is "decimal" or "numeric")
                        {
                            byte p = ci.Precision > 0 ? ci.Precision : (byte)18;
                            byte s = ci.Scale;
                            return $"{t}({p},{s})";
                        }

                        if (t is "datetime2" or "time")
                        {
                            byte s = ci.Scale;
                            return $"{t}({s})";
                        }

                        return t;
                    }

                    var tmpColDefs = writeCols.Select(c =>
                    {
                        var ci = metaByName[c];
                        var sqlType = BuildSqlType(ci);
                        return $"[{c}] {sqlType} NULL";
                    });

                    var createTmpSql = $"CREATE TABLE {tmpName} (\n  {string.Join(",\n  ", tmpColDefs)}\n);";

                    try
                    {
                        using (var cmdCreate = CreateCommand(cn, createTmpSql, tx))
                        {
                            cmdCreate.ExecuteNonQuery();
                        }

                        if (cn is SqlConnection sqlCn)
                        {
                            using (var bcp = new SqlBulkCopy(sqlCn, SqlBulkCopyOptions.Default, (SqlTransaction?)tx))
                            {
                                bcp.DestinationTableName = tmpName;
                                bcp.BatchSize = batchSize;
                                bcp.BulkCopyTimeout = ResolveBulkCopyTimeoutSeconds(bulkCopyTimeoutSeconds);

                                foreach (var c in writeCols)
                                    bcp.ColumnMappings.Add(c, c);

                                bcp.WriteToServer(dt);
                            }
                        }

                        var targetTbl = $"[{Schema}].[{physicalTable}]";
                        var updateSetParts = updateCols.Select(c =>
                        {
                            var qc = $"[{c}]";
                            if (!ignoreNullUpdates)
                                return $"{qc} = s.{qc}";
                            return $"{qc} = COALESCE(s.{qc}, t.{qc})";
                        });

                        var insertColList = string.Join(", ", writeCols.Select(c => $"[{c}]"));
                        var insertValList = string.Join(", ", writeCols.Select(c => $"s.[{c}]"));

                        var mergeSql = $@"
MERGE {targetTbl} AS t
USING {tmpName} AS s
ON t.[{key}] = s.[{key}]
{(updateCols.Count > 0 ? $@"WHEN MATCHED THEN
    UPDATE SET {string.Join(", ", updateSetParts)}" : "")}
WHEN NOT MATCHED BY TARGET THEN
    INSERT ({insertColList})
    VALUES ({insertValList})
OUTPUT $action AS ActionType;";

                        int inserted = 0;
                        int updated = 0;

                        using (var cmdMerge = CreateCommand(cn, mergeSql, tx))
                        using (var rdr = cmdMerge.ExecuteReader())
                        {
                            while (rdr.Read())
                            {
                                var act = rdr.GetString(0);
                                if (act.Equals("INSERT", StringComparison.OrdinalIgnoreCase)) inserted++;
                                else if (act.Equals("UPDATE", StringComparison.OrdinalIgnoreCase)) updated++;
                            }
                        }

                        using (var cmdDrop = CreateCommand(cn, $"DROP TABLE {tmpName};", tx))
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
                DbTransaction? tx = null;
                if (useTransaction) tx = cn.BeginTransaction();

                try
                {
                    if (SqlType == SqlType.SqlServer && cn is SqlConnection sqlCn)
                    {
                        var options = keepIdentity ? SqlBulkCopyOptions.KeepIdentity : SqlBulkCopyOptions.Default;
                        using var bcp = new SqlBulkCopy(sqlCn, options, (SqlTransaction?)tx)
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
                    else
                    {
                        var qCols = writeCols.Select(QSafe).ToList();
                        var paramNames = writeCols.Select((_, i) => $"@p{i}").ToList();
                        var insertSql = $"INSERT INTO {FormatTableName()} ({string.Join(", ", qCols)}) VALUES ({string.Join(", ", paramNames)});";

                        using var cmd = CreateCommand(cn, insertSql, tx);
                        for (int i = 0; i < writeCols.Count; i++)
                        {
                            var colName = writeCols[i];
                            var colType = dt.Columns.Contains(colName) ? dt.Columns[colName].DataType : typeof(string);
                            AddCommandParameter(cmd, paramNames[i], null, TypeToDbType(colType));
                        }

                        int count = 0;
                        foreach (DataRow row in dt.Rows)
                        {
                            for (int i = 0; i < writeCols.Count; i++)
                            {
                                cmd.Parameters[i].Value = row[writeCols[i]] ?? DBNull.Value;
                            }
                            cmd.ExecuteNonQuery();
                            count++;
                        }

                        tx?.Commit();
                        return count;
                    }
                }
                catch
                {
                    tx?.Rollback();
                    throw;
                }
            });
        }

        // Method: Save
        public object? Save(bool askBeforeOverwrite = true, bool Warningneeded = true) => SafeExecute("SAVE", extras =>
        {
            var columns = GetColumns() ?? new List<ColumnInfo>();
            var physicalTable = ResolveTableName();
            var physicalKeyColumn = ResolveKeyColumnName(columns);

            if (!string.IsNullOrWhiteSpace(physicalKeyColumn) &&
                (!TryGetValueForColumn(physicalKeyColumn, columns, out var keyCandidate) || IsEmptyKey(keyCandidate)))
            {
                var inferred = TryInferKeyFromDb();
                if (inferred != null && !IsEmptyKey(inferred))
                    Values[KeyColumn] = inferred;
            }

            if (string.IsNullOrWhiteSpace(physicalKeyColumn))
            {
                var id0 = Insert();
                AddCommonExtras(extras, ("mode", "insert(no-key)"), ("newId", id0));
                return id0;
            }

            if (!TryGetValueForColumn(physicalKeyColumn, columns, out var keyVal) || IsEmptyKey(keyVal))
            {
                var id1 = Insert();
                if (id1 != null) Values[KeyColumn] = id1;
                AddCommonExtras(extras, ("mode", "insert"), ("newId", id1));
                return id1;
            }

            bool exists;
            using (var cn = Open())
            using (var cmd = CreateCommand(cn, $"SELECT COUNT(*) FROM {FormatTableName()} WHERE {QSafe(physicalKeyColumn)} = @key"))
            {
                AddCommandParameter(cmd, "@key", keyVal ?? DBNull.Value);
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
                        $"Warning: No rows were updated in table {FormatTableName()}." + Environment.NewLine +
                        $"This may happen if:" + Environment.NewLine +
                        $"- The record with {KeyColumn} = '{keyVal}' does not exist," + Environment.NewLine +
                        $"- The data you're saving is identical to the current database values," + Environment.NewLine +
                        $"- Or there is a trigger preventing the update.",
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

        public object? SaveWithAudit(bool askBeforeOverwrite = true, bool Warningneeded = true) => SafeExecute("SAVE_WITH_AUDIT", extras =>
        {
            var columns = GetColumns() ?? new List<ColumnInfo>();
            var physicalTable = ResolveTableName();
            var physicalKeyColumn = ResolveKeyColumnName(columns);

            if (!string.IsNullOrWhiteSpace(physicalKeyColumn) &&
                (!TryGetValueForColumn(physicalKeyColumn, columns, out var keyCandidate) || IsEmptyKey(keyCandidate)))
            {
                var inferred = TryInferKeyFromDb();
                if (inferred != null && !IsEmptyKey(inferred))
                    Values[KeyColumn] = inferred;
            }

            if (string.IsNullOrWhiteSpace(physicalKeyColumn))
            {
                var id0 = Insert();
                AddCommonExtras(extras, ("mode", "insert(no-key)"), ("newId", id0));
                return id0;
            }

            if (!TryGetValueForColumn(physicalKeyColumn, columns, out var keyVal) || IsEmptyKey(keyVal))
            {
                var id1 = Insert();
                if (id1 != null) Values[KeyColumn] = id1;
                AddCommonExtras(extras, ("mode", "insert"), ("newId", id1));
                return id1;
            }

            bool exists;
            using (var cn = Open())
            using (var cmd = CreateCommand(cn, $"SELECT COUNT(*) FROM {FormatTableName()} WHERE {QSafe(physicalKeyColumn)} = @key"))
            {
                AddCommandParameter(cmd, "@key", keyVal ?? DBNull.Value);
                exists = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
            }

            if (exists)
            {
                using var cn2 = Open();
                var existingRow = GetRowByKey(cn2, keyVal);
                string changes = existingRow != null ? BuildChangeSummary(existingRow) : "(Could not read existing row)";

                if (askBeforeOverwrite)
                {
                    var prompt = $"Changes:\n{changes}\n\nDo you want to apply these changes?";
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
                        $"Warning: No rows were updated in table {FormatTableName()}.",
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
            for (int i = 0; i < cols.Count; i++)
            {
                whereParts.Add($"{QSafe(cols[i])}=@p{i}");
            }

            var where = string.Join(" AND ", whereParts);
            var sql = (SqlType == SqlType.Sqlite || SqlType == SqlType.PostgreSql)
                ? $"SELECT {QSafe(physicalKeyColumn)} FROM {FormatTableName()} WHERE {where} LIMIT 2;"
                : $"SELECT TOP (2) {QSafe(physicalKeyColumn)} FROM [{Schema}].[{physicalTable}] WHERE {where};";

            using var cn = Open();
            using var cmd = CreateCommand(cn, sql);
            for (int i = 0; i < cols.Count; i++)
            {
                AddCommandParameter(cmd, "@p" + i, physicalValues[cols[i]]!);
            }

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

        private static bool IsForeignKeyViolation(Exception ex, out string? constraint, out string? childTable, out string? childColumn)
        {
            constraint = null;
            childTable = null;
            childColumn = null;

            if (ex is SqlException sx && sx.Number == 547)
            {
                try
                {
                    var m1 = Regex.Match(sx.Message, "constraint \"(?<c>.+?)\"", RegexOptions.IgnoreCase);
                    if (m1.Success) constraint = m1.Groups["c"].Value;

                    var m2 = Regex.Match(sx.Message, "table \"(?<t>.+?)\"", RegexOptions.IgnoreCase);
                    if (m2.Success) childTable = m2.Groups["t"].Value;

                    var m3 = Regex.Match(sx.Message, "column '(?<col>.+?)'", RegexOptions.IgnoreCase);
                    if (m3.Success) childColumn = m3.Groups["col"].Value;
                }
                catch { }
                return true;
            }

            if (ex is SqliteException sqle && (sqle.SqliteErrorCode == 19 || sqle.Message.Contains("FOREIGN KEY", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            if (ex is Npgsql.PostgresException pgex && (pgex.SqlState == "23503" || ex.Message.Contains("foreign key", StringComparison.OrdinalIgnoreCase)))
            {
                constraint = pgex.ConstraintName;
                childTable = pgex.TableName;
                return true;
            }

            return false;
        }

        // Method: DeleteByKey
        public int DeleteByKey(object keyValue) => SafeExecute("DELETE", extras =>
        {
            EnsureIdent(Schema);
            var columns = GetColumns() ?? new List<ColumnInfo>();
            var physicalTable = ResolveTableName();
            var physicalKeyColumn = ResolveKeyColumnName(columns);
            EnsureIdent(physicalKeyColumn);

            if (SqlType == SqlType.SqlServer && ResolveFactory().Invoke() is SqlConnection)
            {
                var inspector = new DatabaseRelationshipInspector(() => (SqlConnection)Open());
                var usages = inspector.GetWhereRecordIsUsed(Schema, physicalTable, physicalKeyColumn, keyValue);
                if (usages.Count > 0)
                {
                    string message = inspector.BuildDeleteBlockedMessage(usages);
                    throw new ForeignKeyDeleteBlockedException(message, usages[0].ForeignKeyName, $"{usages[0].ChildSchema}.{usages[0].ChildTable}", usages[0].ChildColumn);
                }
            }

            var sql = $"DELETE FROM {FormatTableName()} WHERE {QSafe(physicalKeyColumn)}=@k";

            using var cn = Open();
            using var cmd = CreateCommand(cn, sql);
            AddCommandParameter(cmd, "@k", keyValue ?? DBNull.Value);

            AddCommonExtras(extras, ("sql", "DELETE ... WHERE key"), ("key", keyValue));

            try
            {
                return cmd.ExecuteNonQuery();
            }
            catch (Exception ex) when (IsForeignKeyViolation(ex, out var constraint, out var childTable, out var childColumn))
            {
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