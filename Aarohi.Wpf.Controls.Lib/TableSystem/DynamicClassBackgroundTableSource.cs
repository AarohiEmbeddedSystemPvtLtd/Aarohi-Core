using System.Data;
using System.Text.RegularExpressions;
using Aarohi.Classes;
using Aarohi.Wpf.Controls.Lib.Controls.DataGridView;
using Microsoft.Data.SqlClient;

namespace Aarohi.Wpf.Controls.Lib.TableSystem;

public sealed class DynamicClassBackgroundTableSource : IDisposable
{
    private readonly Func<DynamicClass> dynamicClassFactory;
    private readonly int chunkSize;
    private readonly int backgroundChunkSize;
    private readonly SemaphoreSlim loadGate = new(1, 1);
    private CancellationTokenSource? cancellationTokenSource;
    private Task? workerTask;
    private DynamicClass? activeDynamicClass;
    private List<TableColumnMetadata> activeColumns = new();
    private DataTable? loadedTable;

    public DynamicClassBackgroundTableSource(
        Func<DynamicClass> dynamicClassFactory,
        int chunkSize = 1000,
        int? backgroundChunkSize = null,
        string? whereSql = null,
        IDictionary<string, object?>? parameters = null,
        string? orderBy = null)
    {
        this.dynamicClassFactory = dynamicClassFactory ?? throw new ArgumentNullException(nameof(dynamicClassFactory));
        if (chunkSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(chunkSize), "chunkSize must be greater than or equal to 1.");
        }

        int resolvedBackgroundChunkSize = backgroundChunkSize ?? chunkSize;
        if (resolvedBackgroundChunkSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(backgroundChunkSize), "backgroundChunkSize must be greater than or equal to 1.");
        }

        this.chunkSize = chunkSize;
        this.backgroundChunkSize = resolvedBackgroundChunkSize;
        WhereSql = whereSql;
        Parameters = parameters;
        OrderBy = orderBy;
    }

    public event EventHandler<BackgroundTableSnapshot>? SnapshotUpdated;

    public event EventHandler<Exception>? RefreshFailed;

    public BackgroundTableSnapshot? CurrentSnapshot { get; private set; }

    public string? WhereSql { get; set; }

    public IDictionary<string, object?>? Parameters { get; set; }

    public string? OrderBy { get; set; }

    public bool IsRunning
    {
        get { return workerTask is { IsCompleted: false }; }
    }

    public void Start()
    {
        if (IsRunning)
        {
            return;
        }

        cancellationTokenSource = new CancellationTokenSource();
        workerTask = Task.Run(() => RunAsync(cancellationTokenSource.Token));
    }

    public async Task StopAsync()
    {
        CancellationTokenSource? cts = cancellationTokenSource;
        Task? task = workerTask;

        if (cts is null || task is null)
        {
            return;
        }

        cts.Cancel();

        try
        {
            await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            cts.Dispose();
            cancellationTokenSource = null;
            workerTask = null;
        }
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await loadGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            activeDynamicClass?.Dispose();

            DynamicClass dynamicClass = dynamicClassFactory();
            activeDynamicClass = dynamicClass;

            activeColumns = BuildColumns(dynamicClass);
            DataTable firstChunk = await SelectChunkAsync(
                    dynamicClass,
                    offset: 0,
                    fetch: chunkSize,
                    cancellationToken)
                .ConfigureAwait(false);

            loadedTable = firstChunk.Copy();
            PublishSnapshot(loadedTable);

            if (firstChunk.Rows.Count < chunkSize)
            {
                return;
            }

            int chunkOffset = loadedTable.Rows.Count;

            while (!cancellationToken.IsCancellationRequested)
            {
                DataTable chunk = await SelectChunkAsync(
                        dynamicClass,
                        chunkOffset,
                        backgroundChunkSize,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (chunk.Rows.Count == 0)
                {
                    break;
                }

                AppendRows(loadedTable, chunk);
                PublishSnapshot(loadedTable);

                if (chunk.Rows.Count < backgroundChunkSize)
                {
                    break;
                }

                checked
                {
                    chunkOffset += chunk.Rows.Count;
                }
            }
        }
        finally
        {
            loadGate.Release();
        }
    }

    public void Dispose()
    {
        cancellationTokenSource?.Cancel();

        try
        {
            workerTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException ex) when (ex.InnerExceptions.All(inner => inner is OperationCanceledException or TaskCanceledException))
        {
        }

        cancellationTokenSource?.Dispose();
        cancellationTokenSource = null;
        workerTask = null;
        activeDynamicClass?.Dispose();
        activeDynamicClass = null;
        loadedTable = null;
        loadGate.Dispose();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            await LoadAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            RefreshFailed?.Invoke(this, ex);
        }
    }

    private void PublishSnapshot(DataTable table)
    {
        BackgroundTableSnapshot snapshot = new(table.Copy(), activeColumns, DateTime.Now);
        CurrentSnapshot = snapshot;
        SnapshotUpdated?.Invoke(this, snapshot);
    }

    private async Task<DataTable> SelectChunkAsync(
        DynamicClass dynamicClass,
        int offset,
        int fetch,
        CancellationToken cancellationToken)
    {
        Func<SqlConnection>? connectionFactory;
        try
        {
            if (DynamicClass.ConnectionFactory is null)
            {
                connectionFactory = null;
            }
            else
            {
                connectionFactory = () => (SqlConnection)DynamicClass.ConnectionFactory();
            }
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("ConnectionFactory", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("SQL connection is not configured.", ex);
        }

        if (connectionFactory is null)
        {
            throw new InvalidOperationException("SQL connection is not configured.");
        }

        await using SqlConnection connection = connectionFactory();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using SqlCommand command = connection.CreateCommand();
        command.CommandText = BuildChunkSql(dynamicClass);
        command.Parameters.AddWithValue("@__offset", offset);
        command.Parameters.AddWithValue("@__fetch", fetch);

        if (Parameters is not null)
        {
            foreach (KeyValuePair<string, object?> parameter in Parameters)
            {
                string parameterName = parameter.Key.StartsWith("@", StringComparison.Ordinal)
                    ? parameter.Key
                    : "@" + parameter.Key;
                command.Parameters.AddWithValue(parameterName, parameter.Value ?? DBNull.Value);
            }
        }

        await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        DataTable table = new(dynamicClass.Table);
        table.Load(reader);
        return table;
    }

    private string BuildChunkSql(DynamicClass dynamicClass)
    {
        string tableName = $"{QuoteIdentifier(dynamicClass.Schema)}.{QuoteIdentifier(dynamicClass.Table)}";
        string whereClause = string.IsNullOrWhiteSpace(WhereSql) ? string.Empty : $" WHERE {WhereSql}";
        string orderBy = ResolveOrderBy(dynamicClass);

        return $"SELECT * FROM {tableName}{whereClause} ORDER BY {orderBy} OFFSET @__offset ROWS FETCH NEXT @__fetch ROWS ONLY";
    }

    private string ResolveOrderBy(DynamicClass dynamicClass)
    {
        if (!string.IsNullOrWhiteSpace(OrderBy))
        {
            return Regex.Replace(OrderBy, "\\{([^}]+)\\}", match => QuoteIdentifier(match.Groups[1].Value));
        }

        if (!string.IsNullOrWhiteSpace(dynamicClass.KeyColumn))
        {
            return QuoteIdentifier(dynamicClass.KeyColumn);
        }

        string? firstColumn = activeColumns.FirstOrDefault()?.ColumnName;
        if (!string.IsNullOrWhiteSpace(firstColumn))
        {
            return QuoteIdentifier(firstColumn);
        }

        return "(SELECT NULL)";
    }

    private static string QuoteIdentifier(string identifier)
    {
        return "[" + identifier.Replace("]", "]]") + "]";
    }

    private static void AppendRows(DataTable target, DataTable source)
    {
        EnsureSchema(target, source);

        target.BeginLoadData();
        try
        {
            foreach (DataRow sourceRow in source.Rows)
            {
                DataRow targetRow = target.NewRow();

                foreach (DataColumn sourceColumn in source.Columns)
                {
                    if (target.Columns.Contains(sourceColumn.ColumnName))
                    {
                        targetRow[sourceColumn.ColumnName] = sourceRow[sourceColumn] ?? DBNull.Value;
                    }
                }

                target.Rows.Add(targetRow);
                targetRow.AcceptChanges();
            }
        }
        finally
        {
            target.EndLoadData();
        }
    }

    private static void EnsureSchema(DataTable target, DataTable source)
    {
        foreach (DataColumn sourceColumn in source.Columns)
        {
            if (target.Columns.Contains(sourceColumn.ColumnName))
            {
                continue;
            }

            DataColumn targetColumn = new(sourceColumn.ColumnName, sourceColumn.DataType)
            {
                AllowDBNull = sourceColumn.AllowDBNull,
                Caption = string.IsNullOrWhiteSpace(sourceColumn.Caption)
                    ? sourceColumn.ColumnName
                    : sourceColumn.Caption
            };

            if (sourceColumn.DataType == typeof(string))
            {
                targetColumn.MaxLength = sourceColumn.MaxLength;
            }

            foreach (object key in sourceColumn.ExtendedProperties.Keys)
            {
                targetColumn.ExtendedProperties[key] = sourceColumn.ExtendedProperties[key];
            }

            target.Columns.Add(targetColumn);
            targetColumn.SetOrdinal(sourceColumn.Ordinal);
        }
    }

    private static List<TableColumnMetadata> BuildColumns(DynamicClass dynamicClass)
    {
        return (dynamicClass.GetColumns() ?? new List<DynamicClass.ColumnInfo>())
            .OrderBy(column => column.Order ?? int.MaxValue)
            .ThenBy(column => ResolveDisplayName(column), StringComparer.OrdinalIgnoreCase)
            .Select(column => new TableColumnMetadata
            {
                ColumnName = column.Name,
                DisplayName = ResolveDisplayName(column),
                Unit = column.Unit ?? string.Empty,
                Parameter = column.Parameter ?? string.Empty,
                DefaultUnit = column.DefaultUnit ?? string.Empty,
                InputUnit = column.InputUnit ?? string.Empty,
                ShowUnit = column.ShowUnit ?? string.Empty,
                ReportUnit = column.ReportUnit ?? string.Empty,
                LastUsedUnit = column.LastUsedUnit ?? string.Empty,
                IsPrimaryKey = column.IsPrimaryKey
            })
            .ToList();
    }

    private static string ResolveDisplayName(DynamicClass.ColumnInfo column)
    {
        return FirstNotEmpty(column.DisplayName, column.Name);
    }

    private static string FirstNotEmpty(params string?[] values)
    {
        foreach (string? value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return string.Empty;
    }
}
