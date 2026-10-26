#nullable enable
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.Data.SqlClient;

namespace Aarohi.Classes.Recovery
{
    public enum RecoveryWriteKind { Insert, Update, Upsert, AppendIdentityCsv }
    public enum RecoveryWriteStatus { QueuedDurable, QueuedVolatile, Rejected }

    public sealed class RecoveryReceipt
    {
        public Guid OperationId { get; init; }
        public RecoveryWriteStatus Status { get; init; }
        public string Message { get; init; } = "";
    }

    public sealed class RecoveryOptions
    {
        public required string ApplicationId { get; init; }
        public string? StorageRoot { get; init; }
        public int SqlCommandTimeoutSeconds { get; init; } = 5;
        public long MemoryLimitBytes { get; init; } = 50L * 1024 * 1024;
        public long SegmentLimitBytes { get; init; } = 8L * 1024 * 1024;
        public int MaxStatementsPerGroup { get; init; } = 1000;
        public bool RecoveryEnabled { get; init; } = false;
    }

    // Values are snapshotted before journal append: never serialize mutable DynamicClass.Values directly.
    public sealed class RecoveryValue
    {
        public SqlDbType SqlType { get; set; }
        public string? Data { get; set; }
        public bool IsNull { get; set; }
        public string[]? IdentityReferences { get; set; }

        public static RecoveryValue Identity(string alias) => new() { SqlType = SqlDbType.BigInt, IdentityReferences = new[] { alias } };
        public static RecoveryValue IdentityCsv(params string[] aliases) => new() { SqlType = SqlDbType.VarChar, IdentityReferences = aliases };

        public static RecoveryValue From(object? source)
        {
            if (source is null || source == DBNull.Value) return new() { SqlType = SqlDbType.NVarChar, IsNull = true };
            SqlDbType type = source switch
            {
                string => SqlDbType.NVarChar,
                bool => SqlDbType.Bit,
                byte => SqlDbType.TinyInt,
                short => SqlDbType.SmallInt,
                int => SqlDbType.Int,
                long => SqlDbType.BigInt,
                decimal => SqlDbType.Decimal,
                float => SqlDbType.Real,
                double => SqlDbType.Float,
                Guid => SqlDbType.UniqueIdentifier,
                DateOnly => SqlDbType.Date,
                TimeOnly or TimeSpan => SqlDbType.Time,
                DateTime => SqlDbType.DateTime2,
                DateTimeOffset => SqlDbType.DateTimeOffset,
                byte[] => SqlDbType.VarBinary,
                _ => throw new NotSupportedException($"Recovery value type '{source.GetType().FullName}' is unsupported.")
            };
            string str = source switch
            {
                byte[] bytes => Convert.ToBase64String(bytes),
                DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                TimeOnly time => time.ToString("HH:mm:ss.fffffff", CultureInfo.InvariantCulture),
                TimeSpan span => span.ToString("c", CultureInfo.InvariantCulture),
                DateTime dt => dt.ToString("O", CultureInfo.InvariantCulture),
                DateTimeOffset dto => dto.ToString("O", CultureInfo.InvariantCulture),
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                _ => source.ToString() ?? ""
            };
            return new() { SqlType = type, Data = str };
        }

        internal object Resolve(Dictionary<string, string> aliases)
        {
            if (IsNull) return DBNull.Value;
            string text = IdentityReferences is { Length: > 0 } refs
                ? SqlType == SqlDbType.VarChar
                    ? string.Join(",", refs.Select(r => aliases.TryGetValue(r, out string? v) ? v : throw new InvalidOperationException($"Unknown identity reference: {r}")))
                    : aliases.TryGetValue(refs[0], out string? val) ? val : throw new InvalidOperationException($"Unknown identity reference: {refs[0]}")
                : Data ?? "";
            return SqlType switch
            {
                SqlDbType.Bit => bool.Parse(text),
                SqlDbType.TinyInt => byte.Parse(text, CultureInfo.InvariantCulture),
                SqlDbType.SmallInt => short.Parse(text, CultureInfo.InvariantCulture),
                SqlDbType.Int => int.Parse(text, CultureInfo.InvariantCulture),
                SqlDbType.BigInt => long.Parse(text, CultureInfo.InvariantCulture),
                SqlDbType.Decimal or SqlDbType.Money or SqlDbType.SmallMoney => decimal.Parse(text, CultureInfo.InvariantCulture),
                SqlDbType.Real => float.Parse(text, CultureInfo.InvariantCulture),
                SqlDbType.Float => double.Parse(text, CultureInfo.InvariantCulture),
                SqlDbType.UniqueIdentifier => Guid.Parse(text),
                SqlDbType.Date => DateOnly.ParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture).ToDateTime(TimeOnly.MinValue),
                SqlDbType.Time => TimeSpan.Parse(text, CultureInfo.InvariantCulture),
                SqlDbType.DateTime2 => DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                SqlDbType.DateTimeOffset => DateTimeOffset.ParseExact(text, "O", CultureInfo.InvariantCulture, DateTimeStyles.None),
                SqlDbType.VarBinary => Convert.FromBase64String(text),
                _ => text
            };
        }
    }

    public sealed class RecoveryStatement
    {
        public RecoveryWriteKind Kind { get; set; }
        public string Schema { get; set; } = "dbo";
        public string Table { get; set; } = "";
        public string? KeyColumn { get; set; }
        public RecoveryValue? KeyValue { get; set; }
        public string? IdentityColumn { get; set; }
        public string? IdentityAlias { get; set; }
        public Dictionary<string, RecoveryValue> Values { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public sealed class RecoveryBatch
    {
        public Guid OperationId { get; set; } = Guid.NewGuid();
        public string StreamKey { get; set; } = "";
        public DateTimeOffset CapturedUtc { get; set; } = DateTimeOffset.UtcNow;
        public long Sequence { get; set; }
        public List<RecoveryStatement> Statements { get; set; } = new();
        private static readonly Regex Identifier = new(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public RecoveryBatch() { }
        public RecoveryBatch(string streamKey, string? stableOperationKey = null)
        {
            StreamKey = streamKey;
            if (!string.IsNullOrWhiteSpace(stableOperationKey))
            {
                // A stable app-provided logical ID prevents a resumed test from re-adding its earlier writes.
                byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(streamKey + "|" + stableOperationKey));
                OperationId = new Guid(hash.AsSpan(0, 16));
            }
        }

        public RecoveryBatch Insert(string schema, string table, IDictionary<string, object?> values,
            string? identityColumn = null, string? identityAlias = null)
        {
            Statements.Add(new RecoveryStatement { Kind = RecoveryWriteKind.Insert, Schema = schema, Table = table,
                IdentityColumn = identityColumn, IdentityAlias = identityAlias, Values = Copy(values) });
            return this;
        }
        public RecoveryBatch Update(string schema, string table, string keyColumn, object keyValue, IDictionary<string, object?> values)
        {
            Statements.Add(new RecoveryStatement { Kind = RecoveryWriteKind.Update, Schema = schema, Table = table,
                KeyColumn = keyColumn, KeyValue = RecoveryValue.From(keyValue), Values = Copy(values) });
            return this;
        }
        public RecoveryBatch Upsert(string schema, string table, string keyColumn, object keyValue, IDictionary<string, object?> values)
        {
            Statements.Add(new RecoveryStatement { Kind = RecoveryWriteKind.Upsert, Schema = schema, Table = table,
                KeyColumn = keyColumn, KeyValue = RecoveryValue.From(keyValue), Values = Copy(values) });
            return this;
        }
        public RecoveryBatch InsertMany(string schema, string table, IEnumerable<IDictionary<string, object?>> rows)
        {
            foreach (var row in rows) Insert(schema, table, row);
            return this;
        }
        public RecoveryBatch UpsertMany(string schema, string table, string keyColumn, IEnumerable<IDictionary<string, object?>> rows)
        {
            foreach (var row in rows)
            {
                object? key = row.FirstOrDefault(kv => kv.Key.Equals(keyColumn, StringComparison.OrdinalIgnoreCase)).Value;
                if (key is null)
                    throw new ArgumentException($"Upsert row must contain key '{keyColumn}'.");
                Upsert(schema, table, keyColumn, key, row);
            }
            return this;
        }
        public RecoveryBatch AppendIdentityCsv(string schema, string table, string keyColumn, object keyValue,
            string targetColumn, params string[] identityAliases)
        {
            Statements.Add(new RecoveryStatement { Kind = RecoveryWriteKind.AppendIdentityCsv, Schema = schema, Table = table,
                KeyColumn = keyColumn, KeyValue = RecoveryValue.From(keyValue),
                Values = new(StringComparer.OrdinalIgnoreCase) { [targetColumn] = RecoveryValue.IdentityCsv(identityAliases) } });
            return this;
        }
        public RecoveryBatch UpdateWithIdentityCsv(string schema, string table, string keyColumn, object keyValue,
            string targetColumn, params string[] identityAliases)
        {
            Statements.Add(new RecoveryStatement { Kind = RecoveryWriteKind.Update, Schema = schema, Table = table,
                KeyColumn = keyColumn, KeyValue = RecoveryValue.From(keyValue),
                Values = new(StringComparer.OrdinalIgnoreCase) { [targetColumn] = RecoveryValue.IdentityCsv(identityAliases) } });
            return this;
        }
        private static Dictionary<string, RecoveryValue> Copy(IDictionary<string, object?> source) =>
            source.ToDictionary(k => k.Key, v => RecoveryValue.From(v.Value), StringComparer.OrdinalIgnoreCase);

        internal void Validate(int maxStatements)
        {
            if (OperationId == Guid.Empty || string.IsNullOrWhiteSpace(StreamKey) || StreamKey.Length > 256)
                throw new ArgumentException("Recovery batch requires a nonempty operation ID and stream key.");
            if (Statements.Count == 0 || Statements.Count > maxStatements)
                throw new ArgumentException($"Recovery batch must contain 1..{maxStatements} statements.");
            var aliases = new HashSet<string>(StringComparer.Ordinal);
            foreach (RecoveryStatement stmt in Statements)
            {
                Check(stmt.Schema); Check(stmt.Table);
                if (stmt.KeyColumn != null) Check(stmt.KeyColumn);
                if (stmt.IdentityColumn != null) Check(stmt.IdentityColumn);
                if (stmt.Values.Count == 0) throw new ArgumentException("Empty recovery statement.");
                foreach (var (name, value) in stmt.Values)
                {
                    Check(name);
                    if (value.IdentityReferences is { Length: > 0 } refs &&
                        (refs.Any(r => !aliases.Contains(r)) || (value.SqlType != SqlDbType.VarChar && refs.Length != 1)))
                        throw new ArgumentException("An identity reference must refer to an earlier insert in the batch.");
                }
                if (stmt.Kind != RecoveryWriteKind.Insert && (stmt.KeyColumn == null || stmt.KeyValue == null))
                    throw new ArgumentException("Update/upsert requires a key column and key value.");
                if (stmt.KeyValue?.IdentityReferences is { Length: > 0 } keyRefs && keyRefs.Any(r => !aliases.Contains(r)))
                    throw new ArgumentException("Unknown key reference.");
                if (stmt.IdentityAlias != null && (stmt.Kind != RecoveryWriteKind.Insert || stmt.IdentityColumn == null || !aliases.Add(stmt.IdentityAlias)))
                    throw new ArgumentException("Identity alias requires a unique insert with an identity column.");
                if (stmt.Kind == RecoveryWriteKind.Insert && stmt.IdentityColumn != null && stmt.Values.ContainsKey(stmt.IdentityColumn))
                    throw new ArgumentException("Do not insert a value into the generated identity column.");
            }
        }
        internal static string Quoted(string name)
        {
            Check(name);
            return "[" + name + "]";
        }
        private static void Check(string name)
        {
            if (!Identifier.IsMatch(name)) throw new ArgumentException($"Invalid SQL identifier: '{name}'");
        }
    }
}
