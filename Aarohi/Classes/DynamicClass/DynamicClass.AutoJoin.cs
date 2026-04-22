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
            EnsureIdent(Schema);
            var physicalTable = ResolveTableName();

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
                orderClause = $"ORDER BY {ResolveSqlNamePlaceholders(orderBy)}";
            else if (!string.IsNullOrWhiteSpace(KeyColumn))
                orderClause = $"ORDER BY {tBase}.{QSafe(ResolveKeyColumnName(baseCols))}";
            else
                orderClause = "";

            var sql =
                $"SELECT {string.Join(", ", selectParts)} " +
                $"FROM {QTbl(Schema, physicalTable)} {tBase} " +
                string.Join(" ", joins) + " " +
                (string.IsNullOrWhiteSpace(whereSql) ? "" : $"WHERE {ResolveSqlNamePlaceholders(whereSql)} ") +
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

    }
}
