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
     var sqlParams = new List<SqlParameter>();
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
                 $"{joinType} [{rf.RefSchema}].[{rf.RefTable}] {refAlias} " +
                 $"ON {baseAlias}.[{physicalBaseColumn}] = {refAlias}.[{rf.RefColumn}]";
         }
         else
         {
             joinClause =
                 $"{joinType} [{rf.RefSchema}].[{rf.RefTable}] {refAlias} " +
                 $"ON {refAlias}.[{rf.RefColumn}] = {baseAlias}.[{physicalBaseColumn}]";
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
         whereClauses.Add("(" + ResolveSqlNamePlaceholders(whereSql) + ")");

     string sql =
         $"SELECT {(top.HasValue ? "TOP " + top.Value + " " : "")}DISTINCT {baseAlias}.* " +
         $"FROM [{Schema}].[{physicalTable}] {baseAlias} " +
         (joinClauses.Count > 0 ? string.Join(" ", joinClauses) + " " : "") +
         (whereClauses.Count > 0 ? "WHERE " + string.Join(" AND ", whereClauses) + " " : "") +
         (!string.IsNullOrWhiteSpace(orderBy) ? "ORDER BY " + ResolveSqlNamePlaceholders(orderBy) : "");

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
