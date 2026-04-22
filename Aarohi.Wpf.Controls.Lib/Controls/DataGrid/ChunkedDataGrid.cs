using System.Data;
using System.Windows.Threading;

namespace Aarohi.Wpf.Controls.Lib.Controls.DataGrid;

public sealed class ChunkLoadedEventArgs : EventArgs
{
    public ChunkLoadedEventArgs(int totalRows, int chunkRows)
    {
        TotalRows = totalRows;
        ChunkRows = chunkRows;
    }

    public int TotalRows { get; }
    public int ChunkRows { get; }
}

public class ChunkedDataGrid : System.Windows.Controls.DataGrid
{
    public int ChunkSize { get; set; } = 50;
    public DataTable? Data { get; private set; }
    public bool IsChunkLoading { get; private set; }
    public int LoadedRowCount => Data?.Rows.Count ?? 0;

    public event EventHandler<ChunkLoadedEventArgs>? ChunkLoaded;
    public event EventHandler<Exception>? ChunkLoadFailed;

    public void BindTable(DataTable table)
    {
        if (table == null)
            throw new ArgumentNullException(nameof(table));

        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => BindTable(table));
            return;
        }

        Data = table;
        ItemsSource = Data.DefaultView;
        ChunkLoaded?.Invoke(this, new ChunkLoadedEventArgs(Data.Rows.Count, Data.Rows.Count));
    }

    public void AppendChunk(DataTable chunk)
    {
        if (chunk == null)
            throw new ArgumentNullException(nameof(chunk));

        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => AppendChunk(chunk));
            return;
        }

        if (Data == null)
        {
            BindTable(chunk);
            return;
        }

        EnsureCompatibleSchema(Data, chunk);

        Data.BeginLoadData();
        try
        {
            foreach (DataRow row in chunk.Rows)
                Data.ImportRow(row);
        }
        finally
        {
            Data.EndLoadData();
        }

        ChunkLoaded?.Invoke(this, new ChunkLoadedEventArgs(Data.Rows.Count, chunk.Rows.Count));
    }

    public async Task BindChunksAsync(IAsyncEnumerable<DataTable> chunks, CancellationToken ct = default)
    {
        if (chunks == null)
            throw new ArgumentNullException(nameof(chunks));

        await RunOnDispatcherAsync(() =>
        {
            IsChunkLoading = true;
            Data = null;
            ItemsSource = null;
        }, ct).ConfigureAwait(false);

        try
        {
            var first = true;

            await foreach (var chunk in chunks.WithCancellation(ct).ConfigureAwait(false))
            {
                ct.ThrowIfCancellationRequested();

                if (first)
                {
                    await RunOnDispatcherAsync(() => BindTable(chunk), ct).ConfigureAwait(false);
                    first = false;
                }
                else
                {
                    await RunOnDispatcherAsync(() => AppendChunk(chunk), ct).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Expected when the caller starts a new load or closes the view.
        }
        catch (Exception ex)
        {
            await RunOnDispatcherAsync(() => ChunkLoadFailed?.Invoke(this, ex), CancellationToken.None)
                .ConfigureAwait(false);
            throw;
        }
        finally
        {
            await RunOnDispatcherAsync(() => IsChunkLoading = false, CancellationToken.None)
                .ConfigureAwait(false);
        }
    }

    public async Task AppendChunksAsync(IAsyncEnumerable<DataTable> chunks, CancellationToken ct = default)
    {
        if (chunks == null)
            throw new ArgumentNullException(nameof(chunks));

        await RunOnDispatcherAsync(() => IsChunkLoading = true, ct).ConfigureAwait(false);

        try
        {
            await foreach (var chunk in chunks.WithCancellation(ct).ConfigureAwait(false))
            {
                ct.ThrowIfCancellationRequested();
                await RunOnDispatcherAsync(() => AppendChunk(chunk), ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Expected when the caller starts a new load or closes the view.
        }
        catch (Exception ex)
        {
            await RunOnDispatcherAsync(() => ChunkLoadFailed?.Invoke(this, ex), CancellationToken.None)
                .ConfigureAwait(false);
            throw;
        }
        finally
        {
            await RunOnDispatcherAsync(() => IsChunkLoading = false, CancellationToken.None)
                .ConfigureAwait(false);
        }
    }

    private Task RunOnDispatcherAsync(Action action, CancellationToken ct)
    {
        if (Dispatcher.CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }

        return Dispatcher.InvokeAsync(action, DispatcherPriority.Background, ct).Task;
    }

    private static void EnsureCompatibleSchema(DataTable target, DataTable chunk)
    {
        foreach (DataColumn col in chunk.Columns)
        {
            if (!target.Columns.Contains(col.ColumnName))
                throw new InvalidOperationException($"Chunk contains column '{col.ColumnName}' which does not exist in the bound table.");
        }
    }
}
