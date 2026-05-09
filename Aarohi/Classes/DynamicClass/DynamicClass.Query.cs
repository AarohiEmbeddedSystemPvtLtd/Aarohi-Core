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

        #region Query / result shaping
        // Method: Select
        /// <summary>
        /// Executes a parameterized <c>SELECT *</c> on the target table with optional WHERE/TOP/ORDER BY/chunk limit.
        /// </summary>
        /// <param name="whereSql">Optional WHERE clause (without the keyword "WHERE").</param>
        /// <param name="parameters">Optional parameter map (name → value). Names may omit the '@'.</param>
        /// <param name="top">Optional TOP N limit.</param>
        /// <param name="orderBy">Optional ORDER BY clause (without the keyword).</param>
        /// <param name="pageNumber">Optional 1-based page number kept for compatibility. Prefer <paramref name="chunkNumber"/>.</param>
        /// <param name="pageSize">Optional chunk size kept for compatibility. Prefer <paramref name="chunkSize"/>.</param>
        /// <param name="chunkNumber">Optional 1-based chunk number. If omitted with <paramref name="chunkSize"/>, the first chunk is returned.</param>
        /// <param name="chunkSize">Optional chunk size. When supplied alone, only the first chunk is returned.</param>
        /// <param name="chunkOffset">Optional zero-based row offset for variable-size chunk loading. Use with <paramref name="chunkSize"/>.</param>
        /// <returns>A <see cref="DataTable"/> with results; can be empty but not null.</returns>
        public DataTable? Select(
    string? whereSql = null,
    IDictionary<string, object?>? parameters = null,
    int? top = null,
    string? orderBy = null,
    bool DisplayName = false,
    bool WantFormatingInDefault = false,
    int? pageNumber = null,
    int? pageSize = null,
    int? chunkNumber = null,
    int? chunkSize = null,
    int? chunkOffset = null)
        => SafeExecute("SELECT", extras =>
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

            var sql = $"SELECT {(top.HasValue ? "TOP " + top.Value + " " : "")}* FROM [{Schema}].[{physicalTable}]"
                    + (string.IsNullOrWhiteSpace(resolvedWhereSql) ? "" : " WHERE " + resolvedWhereSql)
                    + orderClause
                    + (hasChunkWindow ? " OFFSET @__dc_offset ROWS FETCH NEXT @__dc_chunk_size ROWS ONLY" : "");

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

            if (hasChunkWindow)
            {
                da.SelectCommand!.Parameters.Add("@__dc_offset", SqlDbType.Int).Value = offsetRows;
                da.SelectCommand.Parameters.Add("@__dc_chunk_size", SqlDbType.Int).Value = fetchRows;
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

        private bool TryBuildChunkWindow(
            int? pageNumber,
            int? pageSize,
            int? chunkNumber,
            int? chunkSize,
            int? chunkOffset,
            int? top,
            out int offsetRows,
            out int fetchRows,
            out int effectiveChunkNumber,
            out int effectiveChunkSize,
            out int effectiveChunkOffset)
        {
            offsetRows = 0;
            fetchRows = 0;
            effectiveChunkNumber = 0;
            effectiveChunkSize = 0;
            effectiveChunkOffset = 0;

            if (pageNumber.HasValue && chunkNumber.HasValue && pageNumber.Value != chunkNumber.Value)
                throw new ArgumentException("Use either pageNumber or chunkNumber, not both with different values.");

            if (pageSize.HasValue && chunkSize.HasValue && pageSize.Value != chunkSize.Value)
                throw new ArgumentException("Use either pageSize or chunkSize, not both with different values.");

            chunkNumber ??= pageNumber;
            chunkSize ??= pageSize;

            if (chunkOffset.HasValue && (pageNumber.HasValue || chunkNumber.HasValue))
                throw new ArgumentException("Use either chunkOffset or chunkNumber, not both.");

            if (!chunkNumber.HasValue && !chunkSize.HasValue && !chunkOffset.HasValue)
                return false;

            if (!chunkSize.HasValue)
                throw new ArgumentException("chunkSize is required when chunkNumber or chunkOffset is used.");

            if (top.HasValue)
                throw new InvalidOperationException("Use either top or chunk loading, not both.");

            effectiveChunkNumber = chunkNumber ?? (chunkOffset.HasValue ? 0 : 1);

            if (!chunkOffset.HasValue && effectiveChunkNumber < 1)
                throw new ArgumentOutOfRangeException(nameof(chunkNumber), "chunkNumber must be greater than or equal to 1.");

            if (chunkSize.Value < 1)
                throw new ArgumentOutOfRangeException(nameof(chunkSize), "chunkSize must be greater than or equal to 1.");

            if (chunkOffset.HasValue)
            {
                if (chunkOffset.Value < 0)
                    throw new ArgumentOutOfRangeException(nameof(chunkOffset), "chunkOffset must be greater than or equal to 0.");

                offsetRows = chunkOffset.Value;
            }
            else
            {
                checked
                {
                    offsetRows = (effectiveChunkNumber - 1) * chunkSize.Value;
                }
            }

            effectiveChunkSize = chunkSize.Value;
            effectiveChunkOffset = offsetRows;
            fetchRows = effectiveChunkSize;
            return true;
        }

        private string ResolveOrderByForSelect(string? resolvedOrderBy, IReadOnlyList<ColumnInfo> columns, bool requireOrderBy)
        {
            if (!string.IsNullOrWhiteSpace(resolvedOrderBy))
                return resolvedOrderBy;

            if (!requireOrderBy)
                return "";

            var physicalKeyColumn = ResolveKeyColumnName(columns);
            if (!string.IsNullOrWhiteSpace(physicalKeyColumn))
                return QSafe(physicalKeyColumn);

            var firstColumn = columns.FirstOrDefault()?.Name;
            if (!string.IsNullOrWhiteSpace(firstColumn))
                return QSafe(firstColumn);

            return "(SELECT 1)";
        }

        /// <summary>
        /// Streams the result set in fixed-size chunks. The caller can bind the first chunk immediately
        /// and append later chunks on the UI thread.
        /// </summary>
        public async IAsyncEnumerable<DataTable> SelectChunksAsync(
            string? whereSql = null,
            IDictionary<string, object?>? parameters = null,
            string? orderBy = null,
            int chunkSize = 50,
            bool skipFirstChunk = false,
            bool DisplayName = false,
            bool WantFormatingInDefault = false,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            if (chunkSize < 1)
                throw new ArgumentOutOfRangeException(nameof(chunkSize), "chunkSize must be greater than or equal to 1.");

            var chunkNumber = skipFirstChunk ? 2 : 1;

            while (true)
            {
                ct.ThrowIfCancellationRequested();

                var chunk = await SelectAsync(
                    whereSql: whereSql,
                    parameters: parameters,
                    top: null,
                    orderBy: orderBy,
                    ct: ct,
                    chunkNumber: chunkNumber,
                    chunkSize: chunkSize,
                    DisplayName: DisplayName,
                    WantFormatingInDefault: WantFormatingInDefault).ConfigureAwait(false);

                if (chunk == null || chunk.Rows.Count == 0)
                    yield break;

                yield return chunk;

                if (chunk.Rows.Count < chunkSize)
                    yield break;

                checked
                {
                    chunkNumber++;
                }
            }
        }

        private Dictionary<string, string> GetFormatsFromMetadata()
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var col in GetColumns() ?? new List<ColumnInfo>())
            {
                if (!string.IsNullOrWhiteSpace(col.Format))
                    dict[col.Name] = col.Format!;
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
            var dt = Select(whereSql, parameters, DisplayName: false);
            var values = GetColumnValuesFromDataTable(dt!, ResolveColumnName(columnName));

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
            var physicalColumn = ResolveColumnName(columnName);

            var dt = Select($"{Q(physicalColumn)} = @v", new Dictionary<string, object?> { ["v"] = value }, top: 1, DisplayName:false);
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

    }
}
