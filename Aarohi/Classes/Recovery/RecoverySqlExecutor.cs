#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Aarohi.Classes;
using Microsoft.Data.SqlClient;

namespace Aarohi.Classes.Recovery
{
    // SQL commands are handled by transaction-aware DynamicClass methods.
    // The executor retains error classification used by the existing queue.
    internal static class RecoverySqlExecutor
    {
        internal static Task<Dictionary<string, string>> CommitAsync(
            Func<SqlConnection> connectionFactory, RecoveryBatch batch, int timeoutSeconds, CancellationToken ct) =>
            DynamicClass.CommitRecoveryBatchAsync(connectionFactory, batch, timeoutSeconds, ct);

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
