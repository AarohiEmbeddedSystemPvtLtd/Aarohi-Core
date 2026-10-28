#nullable enable
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Aarohi.Classes.Recovery;

namespace Aarohi.Classes
{
    // New APIs are deliberately separate; existing Insert/UpdateByKey/Save/Bulk methods are untouched.
    public sealed partial class DynamicClass
    {
        public Task<RecoveryReceipt> InsertRecoverableAsync(SqlRecoveryQueue queue, string streamKey,
            string? identityColumn = null, CancellationToken ct = default)
        {
            var batch = new RecoveryBatch(streamKey)
                .Insert(Schema, Table, new Dictionary<string, object?>(Values, StringComparer.OrdinalIgnoreCase), identityColumn);
            return queue.EnqueueAsync(batch, ct);
        }

        public Task<RecoveryReceipt> UpdateRecoverableAsync(SqlRecoveryQueue queue, string streamKey,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(KeyColumn) || !Values.TryGetValue(KeyColumn, out object? key) || key is null || key == DBNull.Value)
                throw new InvalidOperationException("Recoverable update requires an explicit KeyColumn and non-null key in Values.");
            var batch = new RecoveryBatch(streamKey)
                .Update(Schema, Table, KeyColumn, key, new Dictionary<string, object?>(Values, StringComparer.OrdinalIgnoreCase));
            return queue.EnqueueAsync(batch, ct);
        }

        public Task<RecoveryReceipt> SaveRecoverableAsync(SqlRecoveryQueue queue, string streamKey,
            string? identityColumn = null, CancellationToken ct = default)
        {
            var batch = new RecoveryBatch(streamKey);
            var snapshot = new Dictionary<string, object?>(Values, StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(KeyColumn) && snapshot.TryGetValue(KeyColumn, out object? key) &&
                key is not null && key != DBNull.Value)
                batch.Upsert(Schema, Table, KeyColumn, key, snapshot);
            else
                batch.Insert(Schema, Table, snapshot, identityColumn);
            // For SQL IDENTITY keys, an upsert of a nonexistent explicit identity is unsupported.
            return queue.EnqueueAsync(batch, ct);
        }

        public Task<RecoveryReceipt> BulkInsertRecoverableAsync(SqlRecoveryQueue queue, string streamKey,
            IEnumerable<IDictionary<string, object?>> rows, CancellationToken ct = default)
        {
            var batch = new RecoveryBatch(streamKey).InsertMany(Schema, Table, rows);
            return queue.EnqueueAsync(batch, ct);
        }

        public Task<RecoveryReceipt> BulkUpsertRecoverableAsync(SqlRecoveryQueue queue, string streamKey,
            IEnumerable<IDictionary<string, object?>> rows, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(KeyColumn)) throw new InvalidOperationException("Bulk upsert requires a KeyColumn.");
            var batch = new RecoveryBatch(streamKey).UpsertMany(Schema, Table, KeyColumn, rows);
            return queue.EnqueueAsync(batch, ct);
        }

        // Recovery-only transaction-aware CRUD. Existing public CRUD opens its own connection,
        // so it cannot be called safely inside the journal + ledger SQL transaction.
        internal static async Task<Dictionary<string, string>> CommitRecoveryBatchAsync(
            Func<SqlConnection> connectionFactory, RecoveryBatch batch, int timeoutSeconds, CancellationToken ct)
        {
            using SqlConnection cn = connectionFactory();
            await cn.OpenAsync(ct).ConfigureAwait(false);
            using SqlTransaction tx = (SqlTransaction)await cn.BeginTransactionAsync(IsolationLevel.Serializable, ct).ConfigureAwait(false);
            try
            {
                using (SqlCommand find = (SqlCommand)CreateCommand(cn,
                    "SELECT [ResultJson] FROM [dbo].[AarohiRecoveryOperations] WITH (UPDLOCK, HOLDLOCK) WHERE [OperationId]=@id", tx))
                {
                    find.CommandTimeout = timeoutSeconds;
                    AddCommandParameter(find, "@id", batch.OperationId, DbType.Guid);
                    object? result = await find.ExecuteScalarAsync(ct).ConfigureAwait(false);
                    if (result != null)
                    {
                        if (result is not string json)
                            throw new InvalidOperationException("Recovery ledger result is invalid; refusing duplicate execution.");
                        var existing = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                            ?? throw new InvalidOperationException("Recovery ledger result is invalid.");
                        await tx.CommitAsync(ct).ConfigureAwait(false);
                        return existing;
                    }
                }

                var identities = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (RecoveryStatement statement in batch.Statements)
                {
                    using var table = new DynamicClass
                    {
                        Schema = statement.Schema,
                        Table = statement.Table,
                        KeyColumn = statement.KeyColumn ?? string.Empty,
                        SqlType = SqlType.SqlServer
                    };
                    await table.ExecuteRecoveryStatementAsync(cn, tx, statement, identities, timeoutSeconds, ct)
                        .ConfigureAwait(false);
                }

                using (SqlCommand ack = (SqlCommand)CreateCommand(cn,
                    "INSERT INTO [dbo].[AarohiRecoveryOperations] ([OperationId],[ResultJson],[AppliedUtc]) VALUES (@id,@result,SYSUTCDATETIME())", tx))
                {
                    ack.CommandTimeout = timeoutSeconds;
                    AddCommandParameter(ack, "@id", batch.OperationId, DbType.Guid);
                    SqlParameter resultParameter = (SqlParameter)AddCommandParameter(ack, "@result",
                        System.Text.Json.JsonSerializer.Serialize(identities));
                    resultParameter.SqlDbType = SqlDbType.NVarChar;
                    resultParameter.Size = -1;
                    await ack.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                }
                await tx.CommitAsync(ct).ConfigureAwait(false);
                return identities;
            }
            catch
            {
                try { await tx.RollbackAsync(CancellationToken.None).ConfigureAwait(false); } catch { }
                throw;
            }
        }

        private async Task ExecuteRecoveryStatementAsync(SqlConnection cn, SqlTransaction tx,
            RecoveryStatement statement, Dictionary<string, string> identities, int timeout, CancellationToken ct)
        {
            string[] columns = statement.Values.Keys.ToArray();
            string[] quoted = columns.Select(QSafe).ToArray();
            string[] parameters = Enumerable.Range(0, columns.Length).Select(i => "@p" + i).ToArray();
            string target = FormatTableName();
            string sql;

            if (statement.Kind == RecoveryWriteKind.Insert)
            {
                string output = statement.IdentityColumn == null ? string.Empty
                    : " OUTPUT INSERTED." + QSafe(statement.IdentityColumn);
                sql = $"INSERT INTO {target} ({string.Join(",", quoted)}){output} VALUES ({string.Join(",", parameters)})";
            }
            else
            {
                string? keyColumn = statement.KeyColumn;
                if (string.IsNullOrWhiteSpace(keyColumn))
                    throw new InvalidOperationException("Recovery update/upsert requires a key column.");
                string predicate = QSafe(keyColumn) + "=@key";
                string assignments = string.Join(",", columns
                    .Where(c => !c.Equals(keyColumn, StringComparison.OrdinalIgnoreCase))
                    .Select(c => QSafe(c) + "=@p" + Array.IndexOf(columns, c)));
                if (string.IsNullOrWhiteSpace(assignments))
                    throw new InvalidOperationException("Recovery update/upsert requires non-key values.");

                if (statement.Kind == RecoveryWriteKind.Update)
                    sql = $"UPDATE {target} SET {assignments} WHERE {predicate}; " +
                          "IF @@ROWCOUNT=0 THROW 50011, 'Recovery update target missing', 1;";
                else if (statement.Kind == RecoveryWriteKind.AppendIdentityCsv)
                {
                    if (columns.Length != 1 || columns[0].Equals(keyColumn, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("CSV append requires one non-key column.");
                    string column = quoted[0];
                    sql = $"UPDATE {target} SET {column}=CONCAT(CASE WHEN COALESCE({column},'')='' THEN '' ELSE CONCAT({column},',') END,@p0) WHERE {predicate}; " +
                          "IF @@ROWCOUNT=0 THROW 50011, 'Recovery update target missing', 1;";
                }
                else if (statement.Kind == RecoveryWriteKind.Upsert)
                {
                    bool hasKey = columns.Any(c => c.Equals(keyColumn, StringComparison.OrdinalIgnoreCase));
                    string insertColumns = string.Join(",", quoted) + (hasKey ? "" : "," + QSafe(keyColumn));
                    string insertValues = string.Join(",", parameters) + (hasKey ? "" : ",@key");
                    // As in the previous recovery executor, don't use this for a missing SQL IDENTITY key.
                    sql = $"UPDATE {target} WITH (UPDLOCK, SERIALIZABLE) SET {assignments} WHERE {predicate}; " +
                          $"IF @@ROWCOUNT=0 INSERT INTO {target} ({insertColumns}) VALUES ({insertValues});";
                }
                else throw new NotSupportedException($"Unsupported recovery write: {statement.Kind}");
            }

            using SqlCommand cmd = (SqlCommand)CreateCommand(cn, sql, tx);
            cmd.CommandTimeout = timeout;
            for (int i = 0; i < columns.Length; i++)
                AddRecoveryParameter(cmd, parameters[i], statement.Values[columns[i]], identities);
            if (statement.Kind != RecoveryWriteKind.Insert)
                AddRecoveryParameter(cmd, "@key", statement.KeyValue!, identities);

            if (statement.Kind == RecoveryWriteKind.Insert && statement.IdentityColumn != null)
            {
                object? generated = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
                if (generated is null || generated == DBNull.Value)
                    throw new InvalidOperationException("SQL did not return an inserted identity.");
                if (statement.IdentityAlias != null)
                    identities[statement.IdentityAlias] = Convert.ToString(generated, CultureInfo.InvariantCulture)!;
            }
            else await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        private static void AddRecoveryParameter(SqlCommand cmd, string name, RecoveryValue value,
            Dictionary<string, string> identities)
        {
            SqlParameter parameter = (SqlParameter)AddCommandParameter(cmd, name, value.Resolve(identities));
            parameter.SqlDbType = value.SqlType;
            if (value.SqlType is SqlDbType.Decimal or SqlDbType.Money or SqlDbType.SmallMoney)
            {
                parameter.Precision = 38;
                parameter.Scale = 10;
            }
            if (value.SqlType is SqlDbType.VarChar or SqlDbType.NVarChar or SqlDbType.VarBinary)
                parameter.Size = -1;
        }
    }
}
