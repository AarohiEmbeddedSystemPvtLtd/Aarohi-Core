using Aarohi.Core.Logger;
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
            var physicalTable = ResolveTableName();
            var baseColumns = GetColumns() ?? new List<ColumnInfo>();

            relationFilters ??= new List<RelationFilter>();

            string baseAlias = "A";
            var joinClauses = new List<string>();
            var whereClauses = new List<string>();
            var sqlParams = new List<(string Name, object? Value)>();
            int paramIndex = 0;
            int joinIndex = 1;

            foreach (var rf in relationFilters)
            {
                if (rf == null) continue;

                var physicalBaseColumn = ResolveColumnName(rf.BaseColumn, baseColumns);
                EnsureIdent(physicalBaseColumn);
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
                        $"{joinType} {FormatTableName(rf.RefSchema, rf.RefTable, SqlType)} {refAlias} " +
                        $"ON {baseAlias}.{QSafe(physicalBaseColumn)} = {refAlias}.{QSafe(rf.RefColumn)}";
                }
                else
                {
                    joinClause =
                        $"{joinType} {FormatTableName(rf.RefSchema, rf.RefTable, SqlType)} {refAlias} " +
                        $"ON {refAlias}.{QSafe(rf.RefColumn)} = {baseAlias}.{QSafe(physicalBaseColumn)}";
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
                        sqlParams.Add((pName, val ?? DBNull.Value));
                    }

                    whereClauses.Add($"{refAlias}.{QSafe(rf.FilterColumn)} IN ({string.Join(", ", inParams)})");
                }
            }

            if (!string.IsNullOrWhiteSpace(whereSql))
                whereClauses.Add("(" + ResolveSqlNamePlaceholders(whereSql) + ")");

            string topClause = "";
            string limitClause = "";
            if (top.HasValue)
            {
                if (SqlType == SqlType.Sqlite || SqlType == SqlType.PostgreSql)
                    limitClause = $" LIMIT {top.Value}";
                else
                    topClause = $"TOP {top.Value} ";
            }

            string sql =
                $"SELECT {topClause}DISTINCT {baseAlias}.* " +
                $"FROM {FormatTableName()} {baseAlias} " +
                (joinClauses.Count > 0 ? string.Join(" ", joinClauses) + " " : "") +
                (whereClauses.Count > 0 ? "WHERE " + string.Join(" AND ", whereClauses) + " " : "") +
                (!string.IsNullOrWhiteSpace(orderBy) ? "ORDER BY " + ResolveSqlNamePlaceholders(orderBy) + " " : "") +
                limitClause;

            using var cn = Open();
            using var cmd = CreateCommand(cn, sql);

            foreach (var (pName, pVal) in sqlParams)
            {
                AddCommandParameter(cmd, pName, pVal);
            }

            if (parameters != null)
            {
                foreach (var kv in parameters)
                {
                    var name = kv.Key.StartsWith("@") ? kv.Key : "@" + kv.Key;
                    if (!cmd.Parameters.Contains(name))
                        AddCommandParameter(cmd, name, kv.Value);
                }
            }

            AddCommonExtras(extras,
                ("sql", sql),
                ("relationFilterCount", relationFilters.Count),
                ("joinCount", joinClauses.Count),
                ("where", whereSql ?? ""));

            var dt = ExecuteDataTable(cmd);

            dt = ReorderColumnsByMetadataOrder(dt);

            if (displayName)
                dt = ApplyDisplayNames(dt);

            return dt;
        });

    }
}
