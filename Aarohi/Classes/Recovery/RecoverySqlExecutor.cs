#nullable enable
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Aarohi.Classes.Recovery
{
    internal static class RecoverySqlExecutor
    {
        internal static async Task<Dictionary<string, string>> CommitAsync(Func<SqlConnection> connectionFactory,
            RecoveryBatch batch, int timeoutSeconds, CancellationToken ct)
        {
            using SqlConnection cn = connectionFactory();
            await cn.OpenAsync(ct).ConfigureAwait(false);
            using SqlTransaction tx = (SqlTransaction)await cn.BeginTransactionAsync(IsolationLevel.Serializable, ct).ConfigureAwait(false);
            try
            {
                using (var existing = Command(cn, tx, "SELECT [ResultJson] FROM [dbo].[AarohiRecoveryOperations] WITH (UPDLOCK, HOLDLOCK) WHERE [OperationId]=@id", timeoutSeconds))
                {
                    existing.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = batch.OperationId;
                    object? found = await existing.ExecuteScalarAsync(ct).ConfigureAwait(false);
                    if (found != null && found != DBNull.Value)
                    {
                        await tx.CommitAsync(ct).ConfigureAwait(false);
                        return JsonSerializer.Deserialize<Dictionary<string, string>>((string)found) ?? new();
                    }
                }

                var identities = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (RecoveryStatement stmt in batch.Statements)
                    await ExecuteStatementAsync(cn, tx, stmt, identities, timeoutSeconds, ct).ConfigureAwait(false);

                using (var acknowledgement = Command(cn, tx,
                    "INSERT INTO [dbo].[AarohiRecoveryOperations] ([OperationId],[ResultJson],[AppliedUtc]) VALUES (@id,@result,SYSUTCDATETIME())", timeoutSeconds))
                {
                    acknowledgement.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = batch.OperationId;
                    acknowledgement.Parameters.Add("@result", SqlDbType.NVarChar, -1).Value = JsonSerializer.Serialize(identities);
                    await acknowledgement.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
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

        private static async Task ExecuteStatementAsync(SqlConnection cn, SqlTransaction tx, RecoveryStatement st,
            Dictionary<string, string> identities, int timeout, CancellationToken ct)
        {
            string target = RecoveryBatch.Quoted(st.Schema) + "." + RecoveryBatch.Quoted(st.Table);
            string[] keys = st.Values.Keys.ToArray();
            string[] names = keys.Select(RecoveryBatch.Quoted).ToArray();
            string[] parameters = Enumerable.Range(0, keys.Length).Select(i => "@p" + i).ToArray();
            string sql;
            if (st.Kind == RecoveryWriteKind.Insert)
            {
                string output = st.IdentityColumn == null ? "" : " OUTPUT INSERTED." + RecoveryBatch.Quoted(st.IdentityColumn);
                sql = $"INSERT INTO {target} ({string.Join(",", names)}){output} VALUES ({string.Join(",", parameters)})";
            }
            else
            {
                string assignments = string.Join(",", keys.Where(k => !k.Equals(st.KeyColumn, StringComparison.OrdinalIgnoreCase))
                    .Select(k => RecoveryBatch.Quoted(k) + "=@p" + Array.IndexOf(keys, k)));
                if (string.IsNullOrWhiteSpace(assignments))
                    throw new InvalidOperationException("Update requires non-key values.");
                string predicate = RecoveryBatch.Quoted(st.KeyColumn!) + "=@key";
                if (st.Kind == RecoveryWriteKind.Update)
                    sql = $"UPDATE {target} SET {assignments} WHERE {predicate}; IF @@ROWCOUNT=0 THROW 50011, 'Recovery update target missing', 1;";
                else if (st.Kind == RecoveryWriteKind.AppendIdentityCsv)
                {
                    if (keys.Length != 1 || keys[0].Equals(st.KeyColumn, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("CSV append requires exactly one non-key column.");
                    string column = names[0];
                    sql = $"UPDATE {target} SET {column}=CONCAT(CASE WHEN COALESCE({column},'')='' THEN '' ELSE CONCAT({column},',') END,@p0) WHERE {predicate}; IF @@ROWCOUNT=0 THROW 50011, 'Recovery update target missing', 1;";
                }
                else
                {
                    bool hasKey = keys.Any(k => k.Equals(st.KeyColumn, StringComparison.OrdinalIgnoreCase));
                    string insertColumns = string.Join(",", names) + (hasKey ? "" : "," + RecoveryBatch.Quoted(st.KeyColumn!));
                    string insertValues = string.Join(",", parameters) + (hasKey ? "" : ",@key");
                    // Key must be insertable on a missing row. Do not use Upsert for SQL IDENTITY keys.
                    sql = $"UPDATE {target} WITH (UPDLOCK, SERIALIZABLE) SET {assignments} WHERE {predicate}; IF @@ROWCOUNT=0 INSERT INTO {target} ({insertColumns}) VALUES ({insertValues});";
                }
            }

            using var cmd = Command(cn, tx, sql, timeout);
            for (int i = 0; i < keys.Length; i++)
                AddParameter(cmd, parameters[i], st.Values[keys[i]], identities);
            if (st.Kind != RecoveryWriteKind.Insert)
                AddParameter(cmd, "@key", st.KeyValue!, identities);

            if (st.Kind == RecoveryWriteKind.Insert && st.IdentityColumn != null)
            {
                object? id = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
                if (id is null || id == DBNull.Value) throw new InvalidOperationException("SQL did not return an inserted identity.");
                if (st.IdentityAlias != null) identities[st.IdentityAlias] = Convert.ToString(id, CultureInfo.InvariantCulture)!;
            }
            else
            {
                await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }
        }

        private static SqlCommand Command(SqlConnection cn, SqlTransaction tx, string sql, int timeout) =>
            new(sql, cn, tx) { CommandTimeout = timeout };

        private static void AddParameter(SqlCommand cmd, string name, RecoveryValue v, Dictionary<string, string> ids)
        {
            SqlParameter p = cmd.Parameters.Add(name, v.SqlType);
            if (v.SqlType is SqlDbType.Decimal or SqlDbType.Money or SqlDbType.SmallMoney)
            { p.Precision = 38; p.Scale = 10; }
            if (v.SqlType is SqlDbType.VarChar or SqlDbType.NVarChar or SqlDbType.VarBinary)
                p.Size = -1;
            p.Value = v.Resolve(ids);
        }

        internal static bool IsTransient(Exception ex)
        {
            if (ex is TimeoutException or System.IO.IOException or System.Net.Sockets.SocketException) return true;
            if (ex is not SqlException sql) return ex.InnerException != null && IsTransient(ex.InnerException);
            foreach (SqlError e in sql.Errors)
                if (e.Number is -2 or 0 or 2 or 20 or 26 or 40 or 53 or 64 or 233 or 4060 or 1205 or 1222 or 3960 or 10053 or 10054 or 10060 or 10061 or 40197 or 40501 or 40613 or 10928 or 10929)
                    return true;
            return false;
        }
    }
}
