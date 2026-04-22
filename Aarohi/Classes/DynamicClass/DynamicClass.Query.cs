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
        /// Executes a parameterized <c>SELECT *</c> on the target table with optional WHERE/TOP/ORDER BY/pagination.
        /// </summary>
        /// <param name="whereSql">Optional WHERE clause (without the keyword "WHERE").</param>
        /// <param name="parameters">Optional parameter map (name → value). Names may omit the '@'.</param>
        /// <param name="top">Optional TOP N limit.</param>
        /// <param name="orderBy">Optional ORDER BY clause (without the keyword).</param>
        /// <param name="pageNumber">Optional 1-based page number. Requires <paramref name="pageSize"/>.</param>
        /// <param name="pageSize">Optional rows per page. Requires <paramref name="pageNumber"/>.</param>
        /// <returns>A <see cref="DataTable"/> with results; can be empty but not null.</returns>
        public DataTable? Select(
    string? whereSql = null,
    IDictionary<string, object?>? parameters = null,
    int? top = null,
    string? orderBy = null,
    bool DisplayName = false,
    bool WantFormatingInDefault = false,
    int? pageNumber = null,
    int? pageSize = null)
        => SafeExecute("SELECT", extras =>
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

            if (hasPagination)
            {
                da.SelectCommand!.Parameters.Add("@__dc_offset", SqlDbType.Int).Value = offsetRows;
                da.SelectCommand.Parameters.Add("@__dc_page_size", SqlDbType.Int).Value = fetchRows;
            }

            AddCommonExtras(extras,
                ("sql", sql),
                ("where", whereSql ?? ""),
                ("resolvedWhere", resolvedWhereSql ?? ""),
                ("orderBy", orderBy ?? ""),
                ("resolvedOrderBy", resolvedOrderBy ?? ""),
                ("pageNumber", pageNumber),
                ("pageSize", pageSize),
                ("offset", hasPagination ? offsetRows : null),
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

        private bool TryBuildPagination(
            int? pageNumber,
            int? pageSize,
            int? top,
            out int offsetRows,
            out int fetchRows)
        {
            offsetRows = 0;
            fetchRows = 0;

            if (!pageNumber.HasValue && !pageSize.HasValue)
                return false;

            if (!pageNumber.HasValue || !pageSize.HasValue)
                throw new ArgumentException("Both pageNumber and pageSize are required for pagination.");

            if (top.HasValue)
                throw new InvalidOperationException("Use either top or pagination, not both.");

            if (pageNumber.Value < 1)
                throw new ArgumentOutOfRangeException(nameof(pageNumber), "pageNumber must be greater than or equal to 1.");

            if (pageSize.Value < 1)
                throw new ArgumentOutOfRangeException(nameof(pageSize), "pageSize must be greater than or equal to 1.");

            checked
            {
                offsetRows = (pageNumber.Value - 1) * pageSize.Value;
            }

            fetchRows = pageSize.Value;
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
