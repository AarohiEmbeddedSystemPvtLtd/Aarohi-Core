#nullable enable
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace Aarohi.Classes
{
    public sealed partial class DynamicClass
    {
        public ChunkedTable SelectChunkedTable(
            string? whereSql = null,
            IDictionary<string, object?>? parameters = null,
            string? orderBy = null,
            int chunkSize = 150,
            int? backgroundChunkSize = null,
            bool loadRemainingInBackground = true,
            ChunkedTable? target = null,
            bool WantFormatingInDefault = false,
            CancellationToken ct = default)
        {
            if (chunkSize < 1)
                throw new ArgumentOutOfRangeException(nameof(chunkSize), "chunkSize must be greater than or equal to 1.");

            var effectiveBackgroundChunkSize = backgroundChunkSize ?? chunkSize;
            if (effectiveBackgroundChunkSize < 1)
                throw new ArgumentOutOfRangeException(nameof(backgroundChunkSize), "backgroundChunkSize must be greater than or equal to 1.");

            ct.ThrowIfCancellationRequested();

            var table = PrepareChunkedTable(target);
            var firstChunk = Select(
                whereSql: whereSql,
                parameters: parameters,
                orderBy: orderBy,
                WantFormatingInDefault: WantFormatingInDefault,
                chunkNumber: 1,
                chunkSize: chunkSize) ?? new DataTable(Table);

            var firstChunkIsFinal = firstChunk.Rows.Count < chunkSize;
            table.ReplaceWith(firstChunk, chunkNumber: 1, isFinalChunk: firstChunkIsFinal);

            if (loadRemainingInBackground && !firstChunkIsFinal)
            {
                StartRemainingChunkLoad(
                    table,
                    whereSql,
                    parameters,
                    orderBy,
                    firstChunk.Rows.Count,
                    effectiveBackgroundChunkSize,
                    WantFormatingInDefault,
                    ct);
            }

            return table;
        }

        public async Task<ChunkedTable> SelectChunkedTableAsync(
            string? whereSql = null,
            IDictionary<string, object?>? parameters = null,
            string? orderBy = null,
            int chunkSize = 150,
            int? backgroundChunkSize = null,
            bool loadRemainingInBackground = true,
            ChunkedTable? target = null,
            bool WantFormatingInDefault = false,
            CancellationToken ct = default)
        {
            if (chunkSize < 1)
                throw new ArgumentOutOfRangeException(nameof(chunkSize), "chunkSize must be greater than or equal to 1.");

            var effectiveBackgroundChunkSize = backgroundChunkSize ?? chunkSize;
            if (effectiveBackgroundChunkSize < 1)
                throw new ArgumentOutOfRangeException(nameof(backgroundChunkSize), "backgroundChunkSize must be greater than or equal to 1.");

            ct.ThrowIfCancellationRequested();

            var table = CreateChunkedTableShell(target);
            var firstChunk = await Task.Run(async () =>
                await SelectAsync(
                    whereSql: whereSql,
                    parameters: parameters,
                    orderBy: orderBy,
                    ct: ct,
                    WantFormatingInDefault: WantFormatingInDefault,
                    chunkNumber: 1,
                    chunkSize: chunkSize).ConfigureAwait(false), ct).ConfigureAwait(false) ?? new DataTable(Table);

            var firstChunkIsFinal = firstChunk.Rows.Count < chunkSize;
            table.ApplyColumnMetadata(GetColumns());
            table.ReplaceWith(firstChunk, chunkNumber: 1, isFinalChunk: firstChunkIsFinal);

            if (loadRemainingInBackground && !firstChunkIsFinal)
            {
                StartRemainingChunkLoad(
                    table,
                    whereSql,
                    parameters,
                    orderBy,
                    firstChunk.Rows.Count,
                    effectiveBackgroundChunkSize,
                    WantFormatingInDefault,
                    ct);
            }

            return table;
        }

        private ChunkedTable PrepareChunkedTable(ChunkedTable? target)
        {
            var table = CreateChunkedTableShell(target);
            table.ApplyColumnMetadata(GetColumns());
            return table;
        }

        private ChunkedTable CreateChunkedTableShell(ChunkedTable? target)
        {
            var table = target ?? new ChunkedTable(Table);
            if (table.UpdateContext == null)
                table.UpdateContext = SynchronizationContext.Current;
            return table;
        }

        private void StartRemainingChunkLoad(
            ChunkedTable table,
            string? whereSql,
            IDictionary<string, object?>? parameters,
            string? orderBy,
            int initialOffset,
            int chunkSize,
            bool WantFormatingInDefault,
            CancellationToken ct)
        {
            table.StartBackgroundLoad(async loadCt =>
            {
                var chunkNumber = 2;
                var chunkOffset = initialOffset;

                while (true)
                {
                    loadCt.ThrowIfCancellationRequested();

                    var chunk = await SelectAsync(
                        whereSql: whereSql,
                        parameters: parameters,
                        orderBy: orderBy,
                        ct: loadCt,
                        WantFormatingInDefault: WantFormatingInDefault,
                        chunkSize: chunkSize,
                        chunkOffset: chunkOffset).ConfigureAwait(false);

                    if (chunk == null || chunk.Rows.Count == 0)
                        break;

                    var isFinalChunk = chunk.Rows.Count < chunkSize;
                    table.AppendChunk(
                        chunk,
                        chunkNumber: chunkNumber,
                        isFinalChunk: isFinalChunk,
                        isBackgroundUpdate: true);

                    if (isFinalChunk)
                        break;

                    checked
                    {
                        chunkOffset += chunk.Rows.Count;
                    }

                    chunkNumber++;
                }
            }, ct);
        }
    }
}
