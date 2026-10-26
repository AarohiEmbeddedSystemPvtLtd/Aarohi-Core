#nullable enable
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
    }
}
