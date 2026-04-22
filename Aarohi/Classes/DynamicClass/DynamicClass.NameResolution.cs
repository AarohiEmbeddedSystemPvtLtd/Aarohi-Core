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
        public const string SoftUsedNamePropertyName = "Soft_Used_Name";

        private string ResolveTableName()
        {
            EnsureIdent(Schema);
            EnsureIdent(Table);

            using var cn = Open();
            return ResolveTableName(cn);
        }

        private string ResolveTableName(SqlConnection cn)
        {
            EnsureIdent(Schema);
            EnsureIdent(Table);

            const string sql = @"
SELECT t.name
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE s.name = @schema
  AND t.name = @table

UNION ALL

SELECT t.name
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
JOIN sys.extended_properties ep
  ON ep.major_id = t.object_id
 AND ep.minor_id = 0
 AND ep.name = @softNameProperty
WHERE s.name = @schema
  AND CONVERT(nvarchar(256), ep.value) = @table;";

            using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@schema", Schema);
            cmd.Parameters.AddWithValue("@table", Table);
            cmd.Parameters.AddWithValue("@softNameProperty", SoftUsedNamePropertyName);

            var matches = new List<string>();
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                var table = rd.GetString(0);
                if (!matches.Contains(table, StringComparer.OrdinalIgnoreCase))
                    matches.Add(table);
            }

            if (matches.Count == 1)
                return matches[0];

            if (matches.Count > 1)
                throw new InvalidOperationException(
                    $"Multiple tables in schema [{Schema}] use Soft_Used_Name '{Table}'. Keep software table names unique.");

            return Table;
        }

        private string ResolveColumnName(string columnName)
            => ResolveColumnName(columnName, GetColumns() ?? new List<ColumnInfo>());

        private string ResolveColumnName(string columnName, IEnumerable<ColumnInfo> columns)
        {
            EnsureIdent(columnName);

            var match = columns.FirstOrDefault(c =>
                c.Name.Equals(columnName, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(c.SoftUsedName) &&
                 c.SoftUsedName.Equals(columnName, StringComparison.OrdinalIgnoreCase)));

            return match?.Name ?? columnName;
        }

        private string ResolveKeyColumnName(IEnumerable<ColumnInfo> columns)
            => string.IsNullOrWhiteSpace(KeyColumn) ? "" : ResolveColumnName(KeyColumn, columns);

        private Dictionary<string, object?> ResolveValuesToPhysicalColumns(IEnumerable<ColumnInfo> columns)
        {
            var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            foreach (var kv in Values)
            {
                var physicalColumn = ResolveColumnName(kv.Key, columns);

                if (result.ContainsKey(physicalColumn))
                    throw new InvalidOperationException(
                        $"Duplicate values map to column '{physicalColumn}'. Use either the DB column name or Soft_Used_Name, not both.");

                result[physicalColumn] = kv.Value;
            }

            return result;
        }

        private bool TryGetValueForColumn(string columnName, IEnumerable<ColumnInfo> columns, out object? value)
        {
            value = null;

            if (Values.TryGetValue(columnName, out value))
                return true;

            var physicalColumn = ResolveColumnName(columnName, columns);
            if (Values.TryGetValue(physicalColumn, out value))
                return true;

            var softName = columns
                .FirstOrDefault(c => c.Name.Equals(physicalColumn, StringComparison.OrdinalIgnoreCase))
                ?.SoftUsedName;

            return !string.IsNullOrWhiteSpace(softName) && Values.TryGetValue(softName, out value);
        }

        private bool ValuesContainColumn(string columnName, IEnumerable<ColumnInfo> columns)
            => TryGetValueForColumn(columnName, columns, out _);

        private string? ResolveSqlNamePlaceholders(string? sqlFragment)
        {
            if (string.IsNullOrWhiteSpace(sqlFragment) || !sqlFragment.Contains("{"))
                return sqlFragment;

            var columns = GetColumns() ?? new List<ColumnInfo>();

            return Regex.Replace(sqlFragment, @"\{(?<name>[A-Za-z_][A-Za-z0-9_]*)\}", match =>
            {
                var requestedName = match.Groups["name"].Value;
                var physicalName = ResolveColumnName(requestedName, columns);
                return QSafe(physicalName);
            });
        }
    }
}
