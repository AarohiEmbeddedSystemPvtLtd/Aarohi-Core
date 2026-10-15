using Aarohi.Core.Logger;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
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
                var physicalTable = ResolveTableName();

                if (Values.Count == 0)
                    throw new InvalidOperationException("No Values to insert.");

                var columns = GetColumns() ?? new List<ColumnInfo>();
                var physicalValues = ResolveValuesToPhysicalColumns(columns);
                var cols = physicalValues.Keys.ToArray();
                var qCols = cols.Select(QSafe).ToArray();
                var parNames = cols.Select((_, i) => $"@p{i}").ToArray();
                var physicalKeyColumn = ResolveKeyColumnName(columns);

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

                using var cn = await OpenAsync(ct).ConfigureAwait(false);
                using var cmd = CreateCommand(cn, sql);

                for (int i = 0; i < cols.Length; i++)
                {
                    string colName = cols[i];
                    var val = physicalValues[colName];

                    ColumnInfo? info = columns.FirstOrDefault(x => x.Name.Equals(colName, StringComparison.OrdinalIgnoreCase));
                    if (info == null)
                        throw new Exception($"Column metadata not found for {colName}");

                    if (val is string s && string.IsNullOrWhiteSpace(s) && info.Nullable)
                        val = DBNull.Value;

                    AddCommandParameter(cmd, parNames[i], val ?? DBNull.Value);
                }

                AddCommonExtras(extras,
                    ("sql", sql),
                    ("columns", string.Join(",", cols)),
                    ("valuesCount", cols.Length));

                if (string.IsNullOrWhiteSpace(identitySql))
                {
                    await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

                    if (!string.IsNullOrWhiteSpace(physicalKeyColumn) &&
                        TryGetValueForColumn(physicalKeyColumn, columns, out var providedKey) &&
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
            EnsureIdent(Schema);
            var physicalTable = ResolveTableName();
            var columns = GetColumns() ?? new List<ColumnInfo>();
            var physicalKeyColumn = ResolveKeyColumnName(columns);
            EnsureIdent(physicalKeyColumn);

            if (!TryGetValueForColumn(physicalKeyColumn, columns, out var keyVal))
                throw new InvalidOperationException($"Values must include '{KeyColumn}' for Update.");

            using var cn = await OpenAsync(ct).ConfigureAwait(false);

            var existing = await GetRowByKeyAsync(cn, keyVal, ct).ConfigureAwait(false);
            if (existing == null)
                return 0;

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
                return 0;

            var sets = string.Join(",", changedCols.Select(c => $"{QSafe(c)}=@{c}"));
            var sql = $"UPDATE {FormatTableName()} SET {sets} WHERE {QSafe(physicalKeyColumn)}=@__key";

            using var cmd = CreateCommand(cn, sql);

            foreach (var c in changedCols)
                AddCommandParameter(cmd, "@" + c, physicalValues[c] ?? DBNull.Value);

            AddCommandParameter(cmd, "@__key", keyVal ?? DBNull.Value);

            var affected = await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            return affected;
        }

        public async Task<int> BulkInsertAsync(IEnumerable<IDictionary<string, object?>> rows,
                                       int batchSize = 1000,
                                       bool keepIdentity = false,
                                       bool useTransaction = true,
                                       int? bulkCopyTimeoutSeconds = null,
                                       CancellationToken ct = default)
        {
            return await SafeExecuteAsync("BULK_INSERT_ASYNC", async extras =>
            {
                var list = rows as IList<IDictionary<string, object?>> ?? rows.ToList();
                if (list.Count == 0) return 0;

                EnsureIdent(Schema);
                var physicalTable = ResolveTableName();

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

                return await BulkInsertAsync(dt, batchSize, keepIdentity, useTransaction, true, bulkCopyTimeoutSeconds, ct).ConfigureAwait(false);
            });
        }

        public async Task<int> BulkInsertAsync(DataTable dt,
                                       int batchSize = 1000,
                                       bool keepIdentity = false,
                                       bool useTransaction = true,
                                       bool autoTrimColumns = true,
                                       int? bulkCopyTimeoutSeconds = null,
                                       CancellationToken ct = default)
        {
            return await SafeExecuteAsync("BULK_INSERT_DT_ASYNC", async extras =>
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

                using var cn = await OpenAsync(ct).ConfigureAwait(false);
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

                        await bcp.WriteToServerAsync(dt, ct).ConfigureAwait(false);
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
                            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
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

        // Method: SaveAsync
        /// <summary>
        /// Asynchronous upsert for <see cref="Values"/>.
        /// Uses <paramref name="confirmAsync"/> to request user confirmation before updating when <paramref name="askBeforeOverwrite"/> is true.
        /// </summary>
        public async Task<object?> SaveAsync(
            bool askBeforeOverwrite = true,
            ConfirmUpdateAsync? confirmAsync = null,
            CancellationToken ct = default)
        => await SafeExecuteAsync("SAVE_ASYNC", async extras =>
        {
            var columns = GetColumns() ?? new List<ColumnInfo>();
            var physicalTable = ResolveTableName();
            var physicalKeyColumn = ResolveKeyColumnName(columns);

            if (!string.IsNullOrWhiteSpace(physicalKeyColumn) &&
                (!TryGetValueForColumn(physicalKeyColumn, columns, out var keyCandidate) || IsEmptyKey(keyCandidate)))
            {
                var inferred = await TryInferKeyFromDbAsync(ct).ConfigureAwait(false);
                if (inferred != null && !IsEmptyKey(inferred))
                    Values[KeyColumn] = inferred;
            }

            if (string.IsNullOrWhiteSpace(physicalKeyColumn))
            {
                var id0 = await InsertAsync(ct).ConfigureAwait(false);
                AddCommonExtras(extras, ("mode", "insert(no-key)"), ("newId", id0));
                return id0;
            }

            if (!TryGetValueForColumn(physicalKeyColumn, columns, out var keyVal) || IsEmptyKey(keyVal))
            {
                var id1 = await InsertAsync(ct).ConfigureAwait(false);
                if (id1 != null) Values[KeyColumn] = id1;
                AddCommonExtras(extras, ("mode", "insert"), ("newId", id1));
                return id1;
            }

            bool exists;
            using (var cn = await OpenAsync(ct).ConfigureAwait(false))
            using (var cmd = CreateCommand(cn, $"SELECT COUNT(*) FROM {FormatTableName()} WHERE {QSafe(physicalKeyColumn)} = @key"))
            {
                AddCommandParameter(cmd, "@key", keyVal ?? DBNull.Value);
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
        public async Task<int> DeleteByKeyAsync(object keyValue, CancellationToken ct = default) =>
            await SafeExecuteAsync("DELETE_ASYNC", async extras =>
            {
                EnsureIdent(Schema);
                var columns = GetColumns() ?? new List<ColumnInfo>();
                var physicalTable = ResolveTableName();
                var physicalKeyColumn = ResolveKeyColumnName(columns);
                EnsureIdent(physicalKeyColumn);

                var sql = $"DELETE FROM {FormatTableName()} WHERE {QSafe(physicalKeyColumn)}=@k";

                using var cn = await OpenAsync(ct).ConfigureAwait(false);
                using var cmd = CreateCommand(cn, sql);
                AddCommandParameter(cmd, "@k", keyValue ?? DBNull.Value);

                AddCommonExtras(extras, ("sql", "DELETE ... WHERE key"), ("key", keyValue));

                try
                {
                    return await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
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
            }).ConfigureAwait(false);

        // Method: SelectAsync
        public async Task<DataTable?> SelectAsync(
            string? whereSql = null,
            IDictionary<string, object?>? parameters = null,
            int? top = null,
            string? orderBy = null,
            CancellationToken ct = default,
            int? pageNumber = null,
            int? pageSize = null,
            bool DisplayName = false,
            bool WantFormatingInDefault = false,
            int? chunkNumber = null,
            int? chunkSize = null,
            int? chunkOffset = null)
        => await SafeExecuteAsync("SELECT_ASYNC", async extras =>
        {
            EnsureIdent(Schema);
            var physicalTable = ResolveTableName();
            var resolvedWhereSql = ResolveSqlNamePlaceholders(whereSql);
            var resolvedOrderBy = ResolveSqlNamePlaceholders(orderBy);
            var hasChunkWindow = TryBuildChunkWindow(
                pageNumber,
                pageSize,
                chunkNumber,
                chunkSize,
                chunkOffset,
                top,
                out var offsetRows,
                out var fetchRows,
                out var effectiveChunkNumber,
                out var effectiveChunkSize,
                out var effectiveChunkOffset);
            var orderClause = "";

            if (!string.IsNullOrWhiteSpace(resolvedOrderBy) || hasChunkWindow)
            {
                var columns = hasChunkWindow && string.IsNullOrWhiteSpace(resolvedOrderBy)
                    ? GetColumns() ?? new List<ColumnInfo>()
                    : new List<ColumnInfo>();

                orderClause = " ORDER BY " + ResolveOrderByForSelect(resolvedOrderBy, columns, hasChunkWindow);
            }

            string sql;
            if (SqlType == SqlType.SqlServer)
            {
                var topClause = (top.HasValue && !hasChunkWindow) ? $"TOP {top.Value} " : "";
                sql = $"SELECT {topClause}* FROM [{Schema}].[{physicalTable}]"
                    + (string.IsNullOrWhiteSpace(resolvedWhereSql) ? "" : " WHERE " + resolvedWhereSql)
                    + orderClause
                    + (hasChunkWindow ? " OFFSET @__dc_offset ROWS FETCH NEXT @__dc_chunk_size ROWS ONLY" : "");
            }
            else
            {
                var limitOffset = "";
                if (hasChunkWindow)
                    limitOffset = " LIMIT @__dc_chunk_size OFFSET @__dc_offset";
                else if (top.HasValue)
                    limitOffset = $" LIMIT {top.Value}";

                sql = $"SELECT * FROM {FormatTableName()}"
                    + (string.IsNullOrWhiteSpace(resolvedWhereSql) ? "" : " WHERE " + resolvedWhereSql)
                    + orderClause
                    + limitOffset;
            }

            using var cn = await OpenAsync(ct).ConfigureAwait(false);
            using var cmd = CreateCommand(cn, sql);

            if (parameters != null)
            {
                foreach (var kv in parameters)
                {
                    AddCommandParameter(cmd, kv.Key, kv.Value);
                }
            }

            if (hasChunkWindow)
            {
                AddCommandParameter(cmd, "@__dc_offset", offsetRows);
                AddCommandParameter(cmd, "@__dc_chunk_size", fetchRows);
            }

            AddCommonExtras(extras,
                ("sql", sql),
                ("where", whereSql ?? ""),
                ("resolvedWhere", resolvedWhereSql ?? ""),
                ("orderBy", orderBy ?? ""),
                ("resolvedOrderBy", resolvedOrderBy ?? ""),
                ("pageNumber", pageNumber),
                ("pageSize", pageSize),
                ("chunkNumber", hasChunkWindow ? effectiveChunkNumber : null),
                ("chunkSize", hasChunkWindow ? effectiveChunkSize : null),
                ("chunkOffset", hasChunkWindow ? effectiveChunkOffset : null),
                ("offset", hasChunkWindow ? offsetRows : null),
                ("params", parameters ?? new Dictionary<string, object?>()));

            using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            var dt = new DataTable();
            dt.Load(reader);

            dt = ReorderColumnsByMetadataOrder(dt);

            if (WantFormatingInDefault)
            {
                var formats = GetFormatsFromMetadata();
                dt = ApplyFormats(dt, formats);
            }

            if (DisplayName)
                dt = ApplyDisplayNames(dt);

            return dt;
        }).ConfigureAwait(false);

        private async Task<DataRow?> GetRowByKeyAsync(DbConnection cn, object? keyVal, CancellationToken ct)
        {
            var physicalTable = ResolveTableName(cn);
            var columns = GetColumns() ?? new List<ColumnInfo>();
            var physicalKeyColumn = ResolveKeyColumnName(columns);

            var sql = (SqlType == SqlType.Sqlite || SqlType == SqlType.PostgreSql)
                ? $"SELECT * FROM {FormatTableName()} WHERE {QSafe(physicalKeyColumn)} = @k LIMIT 1"
                : $"SELECT TOP (1) * FROM [{Schema}].[{physicalTable}] WHERE {Q(physicalKeyColumn)} = @k";

            using var cmd = CreateCommand(cn, sql);
            AddCommandParameter(cmd, "@k", keyVal ?? DBNull.Value);
            using var rd = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);

            var dt = new DataTable();
            dt.Load(rd);
            return dt.Rows.Count > 0 ? dt.Rows[0] : null;
        }

        private async Task<object?> TryInferKeyFromDbAsync(CancellationToken ct)
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
                : $"SELECT TOP (2) {Q(physicalKeyColumn)} FROM [{Schema}].[{physicalTable}] WHERE {where};";

            using var cn = await OpenAsync(ct).ConfigureAwait(false);
            using var cmd = CreateCommand(cn, sql);
            for (int i = 0; i < cols.Count; i++)
            {
                AddCommandParameter(cmd, "@p" + i, physicalValues[cols[i]]!);
            }

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
    }
}
