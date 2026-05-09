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
        private string ResolveTableName()
        {
            EnsureIdent(Schema);
            EnsureIdent(Table);
            return Table;
        }

        private string ResolveTableName(SqlConnection cn)
        {
            EnsureIdent(Schema);
            EnsureIdent(Table);
            return Table;
        }

        private string ResolveColumnName(string columnName)
            => ResolveColumnName(columnName, columns: null);

        private string ResolveColumnName(string columnName, IEnumerable<ColumnInfo>? columns)
        {
            EnsureIdent(columnName);
            return columnName;
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
                    throw new InvalidOperationException($"Duplicate values map to column '{physicalColumn}'.");

                result[physicalColumn] = kv.Value;
            }

            return result;
        }

        private bool TryGetValueForColumn(string columnName, IEnumerable<ColumnInfo> columns, out object? value)
        {
            EnsureIdent(columnName);
            return Values.TryGetValue(columnName, out value);
        }

        private bool ValuesContainColumn(string columnName, IEnumerable<ColumnInfo> columns)
            => TryGetValueForColumn(columnName, columns, out _);

        private string? ResolveSqlNamePlaceholders(string? sqlFragment)
        {
            if (string.IsNullOrWhiteSpace(sqlFragment) || !sqlFragment.Contains("{"))
                return sqlFragment;

            return Regex.Replace(sqlFragment, @"\{(?<name>[A-Za-z_][A-Za-z0-9_]*)\}", match =>
            {
                var requestedName = match.Groups["name"].Value;
                return QSafe(ResolveColumnName(requestedName));
            });
        }
    }
}
