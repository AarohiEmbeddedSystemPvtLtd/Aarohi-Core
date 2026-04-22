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

                var identitySql = string.IsNullOrWhiteSpace(physicalKeyColumn)
            ? ""
            : $"IF COLUMNPROPERTY(object_id('{Schema}.{physicalTable}'), '{physicalKeyColumn}', 'IsIdentity')=1 SELECT SCOPE_IDENTITY();";

                var sql = $"INSERT INTO [{Schema}].[{physicalTable}] ({string.Join(",", qCols)}) " +
                  $"VALUES ({string.Join(",", parNames)}); {identitySql}";

                using var cn = await OpenAsync(ct).ConfigureAwait(false);
                using var cmd = new SqlCommand(sql, cn);

                for (int i = 0; i < cols.Length; i++)
                {
                    var val = physicalValues[cols[i]] ?? DBNull.Value;
                    cmd.Parameters.AddWithValue(parNames[i], val);
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

            var sets = string.Join(",", changedCols.Select(c => $"{Q(c)}=@{c}"));
            var sql = $"UPDATE [{Schema}].[{physicalTable}] SET {sets} WHERE {Q(physicalKeyColumn)}=@__key";

            using var cmd = new SqlCommand(sql, cn);

            foreach (var c in changedCols)
                cmd.Parameters.AddWithValue("@" + c, physicalValues[c] ?? DBNull.Value);

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
                        DestinationTableName = $"[{Schema}].[{physicalTable}]",
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

                using var cn = await OpenAsync(ct);
                SqlTransaction? tx = null;
                if (useTransaction) tx = cn.BeginTransaction();

                try
                {
                    var options = keepIdentity ? SqlBulkCopyOptions.KeepIdentity : SqlBulkCopyOptions.Default;
                    using var bcp = new SqlBulkCopy(cn, options, tx)
                    {
                        DestinationTableName = $"[{Schema}].[{physicalTable}]",
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
            var columns = GetColumns() ?? new List<ColumnInfo>();
            var physicalTable = ResolveTableName();
            var physicalKeyColumn = ResolveKeyColumnName(columns);

            // Try infer missing key
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
            using (var cmd = new SqlCommand($"SELECT COUNT(*) FROM [{Schema}].[{physicalTable}] WHERE {Q(physicalKeyColumn)} = @key", cn))
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
                EnsureIdent(Schema);
                var columns = GetColumns() ?? new List<ColumnInfo>();
                var physicalTable = ResolveTableName();
                var physicalKeyColumn = ResolveKeyColumnName(columns);
                EnsureIdent(physicalKeyColumn);

                var sql = $"DELETE FROM [{Schema}].[{physicalTable}] WHERE {Q(physicalKeyColumn)}=@k";

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
        /// Asynchronously executes a <c>SELECT *</c> on the target table with optional WHERE/TOP/ORDER BY/chunk limit.
        /// </summary>
        /// <param name="whereSql">Optional WHERE clause (without the keyword).</param>
        /// <param name="parameters">Optional parameters (name → value).</param>
        /// <param name="top">Optional TOP N.</param>
        /// <param name="orderBy">Optional ORDER BY (without the keyword).</param>
        /// <param name="ct">Cancellation token.</param>
        /// <param name="pageNumber">Optional 1-based page number for compatibility. If omitted with <paramref name="pageSize"/>, the first chunk is returned.</param>
        /// <param name="pageSize">Optional chunk size. When supplied alone, only the first chunk is returned.</param>
        /// <returns>A populated <see cref="DataTable"/>.</returns>
        public async Task<DataTable?> SelectAsync(
            string? whereSql = null,
            IDictionary<string, object?>? parameters = null,
            int? top = null,
            string? orderBy = null,
            CancellationToken ct = default,
            int? pageNumber = null,
            int? pageSize = null,
            bool DisplayName = false,
            bool WantFormatingInDefault = false)
        => await SafeExecuteAsync("SELECT_ASYNC", async extras =>
        {
            EnsureIdent(Schema);
            var physicalTable = ResolveTableName();
            var resolvedWhereSql = ResolveSqlNamePlaceholders(whereSql);
            var resolvedOrderBy = ResolveSqlNamePlaceholders(orderBy);
            var hasPagination = TryBuildPagination(pageNumber, pageSize, top, out var offsetRows, out var fetchRows);
            var orderClause = "";

            if (!string.IsNullOrWhiteSpace(resolvedOrderBy) || hasPagination)
            {
                var columns = hasPagination && string.IsNullOrWhiteSpace(resolvedOrderBy)
                    ? GetColumns() ?? new List<ColumnInfo>()
                    : new List<ColumnInfo>();

                orderClause = " ORDER BY " + ResolveOrderByForSelect(resolvedOrderBy, columns, hasPagination);
            }

            var sql = $"SELECT {(top.HasValue ? "TOP " + top.Value + " " : "")}* FROM [{Schema}].[{physicalTable}]"
                    + (string.IsNullOrWhiteSpace(resolvedWhereSql) ? "" : " WHERE " + resolvedWhereSql)
                    + orderClause
                    + (hasPagination ? " OFFSET @__dc_offset ROWS FETCH NEXT @__dc_page_size ROWS ONLY" : "");

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

            if (hasPagination)
            {
                cmd.Parameters.Add("@__dc_offset", SqlDbType.Int).Value = offsetRows;
                cmd.Parameters.Add("@__dc_page_size", SqlDbType.Int).Value = fetchRows;
            }

            AddCommonExtras(extras,
                ("sql", sql),
                ("where", whereSql ?? ""),
                ("resolvedWhere", resolvedWhereSql ?? ""),
                ("orderBy", orderBy ?? ""),
                ("resolvedOrderBy", resolvedOrderBy ?? ""),
                ("pageNumber", pageNumber ?? (hasPagination ? 1 : null)),
                ("pageSize", pageSize),
                ("offset", hasPagination ? offsetRows : null),
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

        private async Task<DataRow?> GetRowByKeyAsync(SqlConnection cn, object? keyVal, CancellationToken ct)
        {
            var physicalTable = ResolveTableName(cn);
            var columns = GetColumns() ?? new List<ColumnInfo>();
            var physicalKeyColumn = ResolveKeyColumnName(columns);

            var sql = $"SELECT TOP (1) * FROM [{Schema}].[{physicalTable}] WHERE {Q(physicalKeyColumn)} = @k";
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

    }
}
