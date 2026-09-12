using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Aarohi.Classes
{
    public sealed class RelationshipUsageInfo
    {
        public string ForeignKeyName { get; set; } = "";
        public string ChildSchema { get; set; } = "";
        public string ChildTable { get; set; } = "";
        public string ChildColumn { get; set; } = "";
        public string ParentSchema { get; set; } = "";
        public string ParentTable { get; set; } = "";
        public string ParentColumn { get; set; } = "";
        public int UsedCount { get; set; }

        public string DisplayText()
        {
            return $"{ChildSchema}.{ChildTable} -> {ChildColumn} ({UsedCount} record(s))";
        }
    }

    public sealed class DatabaseRelationshipInspector
    {
        private readonly Func<SqlConnection> _connectionFactory;

        private static readonly Regex ValidIdent =
            new Regex(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

        public DatabaseRelationshipInspector(Func<SqlConnection> connectionFactory)
        {
            _connectionFactory = connectionFactory
                ?? throw new ArgumentNullException(nameof(connectionFactory));
        }

        public List<RelationshipUsageInfo> GetWhereRecordIsUsed(
            string parentSchema,
            string parentTable,
            string parentKeyColumn,
            object? keyValue)
        {
            EnsureIdent(parentSchema);
            EnsureIdent(parentTable);
            EnsureIdent(parentKeyColumn);

            var result = new List<RelationshipUsageInfo>();

            using var cn = _connectionFactory();

            if (cn.State != ConnectionState.Open)
                cn.Open();

            var foreignKeys = GetForeignKeysReferencingTable(
                cn,
                parentSchema,
                parentTable,
                parentKeyColumn);

            foreach (var fk in foreignKeys)
            {
                fk.UsedCount = CountChildRows(cn, fk, keyValue);

                if (fk.UsedCount > 0)
                    result.Add(fk);
            }

            return result;
        }

        private static List<RelationshipUsageInfo> GetForeignKeysReferencingTable(
            SqlConnection cn,
            string parentSchema,
            string parentTable,
            string parentKeyColumn)
        {
            var result = new List<RelationshipUsageInfo>();

            const string sql = @"
SELECT
    fk.name AS ForeignKeyName,
    SCHEMA_NAME(child.schema_id) AS ChildSchema,
    child.name AS ChildTable,
    childCol.name AS ChildColumn,
    SCHEMA_NAME(parent.schema_id) AS ParentSchema,
    parent.name AS ParentTable,
    parentCol.name AS ParentColumn
FROM sys.foreign_keys fk
INNER JOIN sys.foreign_key_columns fkc
    ON fk.object_id = fkc.constraint_object_id
INNER JOIN sys.tables child
    ON fkc.parent_object_id = child.object_id
INNER JOIN sys.columns childCol
    ON fkc.parent_object_id = childCol.object_id
   AND fkc.parent_column_id = childCol.column_id
INNER JOIN sys.tables parent
    ON fkc.referenced_object_id = parent.object_id
INNER JOIN sys.columns parentCol
    ON fkc.referenced_object_id = parentCol.object_id
   AND fkc.referenced_column_id = parentCol.column_id
WHERE SCHEMA_NAME(parent.schema_id) = @ParentSchema
  AND parent.name = @ParentTable
  AND parentCol.name = @ParentKeyColumn;";

            using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@ParentSchema", parentSchema);
            cmd.Parameters.AddWithValue("@ParentTable", parentTable);
            cmd.Parameters.AddWithValue("@ParentKeyColumn", parentKeyColumn);

            using var rd = cmd.ExecuteReader();

            while (rd.Read())
            {
                result.Add(new RelationshipUsageInfo
                {
                    ForeignKeyName = Convert.ToString(rd["ForeignKeyName"]) ?? "",
                    ChildSchema = Convert.ToString(rd["ChildSchema"]) ?? "",
                    ChildTable = Convert.ToString(rd["ChildTable"]) ?? "",
                    ChildColumn = Convert.ToString(rd["ChildColumn"]) ?? "",
                    ParentSchema = Convert.ToString(rd["ParentSchema"]) ?? "",
                    ParentTable = Convert.ToString(rd["ParentTable"]) ?? "",
                    ParentColumn = Convert.ToString(rd["ParentColumn"]) ?? ""
                });
            }

            return result;
        }

        private static int CountChildRows(
            SqlConnection cn,
            RelationshipUsageInfo fk,
            object? keyValue)
        {
            EnsureIdent(fk.ChildSchema);
            EnsureIdent(fk.ChildTable);
            EnsureIdent(fk.ChildColumn);

            string sql =
                $"SELECT COUNT(*) FROM [{fk.ChildSchema}].[{fk.ChildTable}] " +
                $"WHERE [{fk.ChildColumn}] = @KeyValue;";

            using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@KeyValue", keyValue ?? DBNull.Value);

            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        public string BuildDeleteBlockedMessage(List<RelationshipUsageInfo> usages)
        {
            var sb = new StringBuilder();

            sb.AppendLine("This record cannot be deleted because it is already used in other table(s).");
            sb.AppendLine();
            sb.AppendLine("Used in:");

            foreach (var item in usages)
            {
                sb.AppendLine($"- {item.DisplayText()}");
            }

            return sb.ToString();
        }

        private static void EnsureIdent(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || !ValidIdent.IsMatch(name))
                throw new ArgumentException($"Invalid SQL identifier: {name}");
        }
    }
}
