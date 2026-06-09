#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Aarohi.Classes
{
    public enum ChunkedTableUpdateMode
    {
        Replace,
        Append
    }

    public sealed class ChunkedTableColumnInfo
    {
        public string ColumnName { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public string Unit { get; init; } = string.Empty;
        public string Parameter { get; init; } = string.Empty;
    }

    public class ChunkedTableUpdateEventArgs : EventArgs
    {
        public ChunkedTableUpdateEventArgs(
            ChunkedTable table,
            DataTable source,
            ChunkedTableUpdateMode mode,
            int rowsBefore,
            int incomingRows,
            int? chunkNumber,
            bool isFinalChunk,
            bool isBackgroundUpdate)
        {
            Table = table;
            Source = source;
            Mode = mode;
            RowsBefore = rowsBefore;
            IncomingRows = incomingRows;
            ChunkNumber = chunkNumber;
            IsFinalChunk = isFinalChunk;
            IsBackgroundUpdate = isBackgroundUpdate;
        }

        public ChunkedTable Table { get; }
        public DataTable Source { get; }
        public ChunkedTableUpdateMode Mode { get; }
        public int RowsBefore { get; }
        public int IncomingRows { get; }
        public int? ChunkNumber { get; }
        public bool IsFinalChunk { get; }
        public bool IsBackgroundUpdate { get; }
    }

    public sealed class ChunkedTableUpdatedEventArgs : ChunkedTableUpdateEventArgs
    {
        public ChunkedTableUpdatedEventArgs(
            ChunkedTable table,
            DataTable source,
            ChunkedTableUpdateMode mode,
            int rowsBefore,
            int rowsAfter,
            int incomingRows,
            int? chunkNumber,
            bool isFinalChunk,
            bool isBackgroundUpdate,
            bool success,
            Exception? error)
            : base(table, source, mode, rowsBefore, incomingRows, chunkNumber, isFinalChunk, isBackgroundUpdate)
        {
            RowsAfter = rowsAfter;
            Success = success;
            Error = error;
        }

        public int RowsAfter { get; }
        public bool Success { get; }
        public Exception? Error { get; }
    }

    public class ChunkedTable : DataTable
    {
        private readonly object _updateSync = new();
        private readonly DataTable _firstChunkData;
        private readonly DataTable _afterFirstChunkData;
        private readonly Dictionary<string, ChunkedTableColumnInfo> _columnMetadata =
            new(StringComparer.OrdinalIgnoreCase);
        private CancellationTokenSource? _backgroundLoadCts;

        public ChunkedTable()
            : this("ChunkedTable")
        {
        }

        public ChunkedTable(string tableName)
            : base(string.IsNullOrWhiteSpace(tableName) ? "ChunkedTable" : tableName)
        {
            _firstChunkData = new DataTable($"{TableName}_FirstChunk");
            _afterFirstChunkData = new DataTable($"{TableName}_AfterFirstChunk");
            UpdateContext = SynchronizationContext.Current;
        }

        public event EventHandler<ChunkedTableUpdateEventArgs>? OnUpdate;
        public event EventHandler<ChunkedTableUpdateEventArgs>? Updating;
        public event EventHandler<ChunkedTableUpdatedEventArgs>? Updated;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IReadOnlyDictionary<string, ChunkedTableColumnInfo> ColumnMetadata => _columnMetadata;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public SynchronizationContext? UpdateContext { get; set; }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool MarshalUpdatesToContext { get; set; } = true;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool AcceptChangesAfterUpdate { get; set; } = true;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool IsUpdating { get; private set; }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool IsBackgroundLoading { get; private set; }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool IsComplete { get; private set; }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int UpdateCount { get; private set; }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Exception? LastUpdateException { get; private set; }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Exception? BackgroundLoadException { get; private set; }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Task? BackgroundLoadTask { get; private set; }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public DataTable FirstChunkData => _firstChunkData;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public DataTable AfterFirstChunkData => _afterFirstChunkData;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public DataTable AllRowsData => this;

        public void ReplaceWith(
            DataTable source,
            int? chunkNumber = null,
            bool isFinalChunk = false,
            bool isBackgroundUpdate = false)
            => Update(source, ChunkedTableUpdateMode.Replace, chunkNumber, isFinalChunk, isBackgroundUpdate);

        public void AppendChunk(
            DataTable chunk,
            int? chunkNumber = null,
            bool isFinalChunk = false,
            bool isBackgroundUpdate = true)
            => Update(chunk, ChunkedTableUpdateMode.Append, chunkNumber, isFinalChunk, isBackgroundUpdate);

        public void Update(
            DataTable source,
            ChunkedTableUpdateMode mode = ChunkedTableUpdateMode.Replace,
            int? chunkNumber = null,
            bool isFinalChunk = false,
            bool isBackgroundUpdate = false)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            var context = UpdateContext;
            if (MarshalUpdatesToContext && context != null && SynchronizationContext.Current != context)
            {
                Exception? captured = null;
                context.Send(_ =>
                {
                    try
                    {
                        UpdateCore(source, mode, chunkNumber, isFinalChunk, isBackgroundUpdate);
                    }
                    catch (Exception ex)
                    {
                        captured = ex;
                    }
                }, null);

                if (captured != null)
                    throw captured;

                return;
            }

            UpdateCore(source, mode, chunkNumber, isFinalChunk, isBackgroundUpdate);
        }

        public void ApplyColumnMetadata(IEnumerable<DynamicClass.ColumnInfo>? columns)
        {
            if (columns == null) return;

            foreach (var column in columns)
            {
                if (string.IsNullOrWhiteSpace(column.Name))
                    continue;

                SetColumnMetadata(
                    column.Name,
                    column.DisplayName,
                    ResolveUnit(column),
                    column.Parameter);
            }
        }

        public void SetColumnMetadata(
            string columnName,
            string? displayName = null,
            string? unit = null,
            string? parameter = null)
        {
            if (string.IsNullOrWhiteSpace(columnName))
                throw new ArgumentException("Column name cannot be empty.", nameof(columnName));

            var info = new ChunkedTableColumnInfo
            {
                ColumnName = columnName,
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? columnName : displayName!,
                Unit = unit ?? string.Empty,
                Parameter = parameter ?? string.Empty
            };

            _columnMetadata[columnName] = info;

            var existingColumn = Columns[columnName];
            if (existingColumn != null)
                ApplyMetadataToColumn(existingColumn, info);

            existingColumn = _firstChunkData.Columns[columnName];
            if (existingColumn != null)
                ApplyMetadataToColumn(existingColumn, info);

            existingColumn = _afterFirstChunkData.Columns[columnName];
            if (existingColumn != null)
                ApplyMetadataToColumn(existingColumn, info);
        }

        public void StartBackgroundLoad(Func<CancellationToken, Task> loadAsync, CancellationToken cancellationToken = default)
        {
            if (loadAsync == null) throw new ArgumentNullException(nameof(loadAsync));

            CancelBackgroundLoad();

            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _backgroundLoadCts = linkedCts;
            BackgroundLoadException = null;
            IsBackgroundLoading = true;
            IsComplete = false;

            BackgroundLoadTask = Task.Run(async () =>
            {
                try
                {
                    await loadAsync(linkedCts.Token).ConfigureAwait(false);
                    IsComplete = !linkedCts.IsCancellationRequested;
                }
                catch (OperationCanceledException) when (linkedCts.IsCancellationRequested)
                {
                    IsComplete = false;
                }
                catch (Exception ex)
                {
                    BackgroundLoadException = ex;
                    IsComplete = false;
                }
                finally
                {
                    IsBackgroundLoading = false;
                    if (ReferenceEquals(_backgroundLoadCts, linkedCts))
                        _backgroundLoadCts = null;
                    linkedCts.Dispose();
                }
            }, CancellationToken.None);
        }

        public void CancelBackgroundLoad()
        {
            var cts = _backgroundLoadCts;
            if (cts != null && !cts.IsCancellationRequested)
                cts.Cancel();
        }

        private void UpdateCore(
            DataTable source,
            ChunkedTableUpdateMode mode,
            int? chunkNumber,
            bool isFinalChunk,
            bool isBackgroundUpdate)
        {
            var rowsBefore = Rows.Count;
            var incomingRows = source.Rows.Count;
            var updateArgs = new ChunkedTableUpdateEventArgs(
                this,
                source,
                mode,
                rowsBefore,
                incomingRows,
                chunkNumber,
                isFinalChunk,
                isBackgroundUpdate);

            Exception? error = null;
            var success = false;
            IsUpdating = true;
            LastUpdateException = null;

            try
            {
                OnUpdate?.Invoke(this, updateArgs);
                Updating?.Invoke(this, updateArgs);

                lock (_updateSync)
                {
                    if (!string.IsNullOrWhiteSpace(source.TableName))
                        TableName = source.TableName;

                    UpdateCompanionTableNames();

                    EnsureSchema(this, source);
                    MinimumCapacity = Math.Max(MinimumCapacity, Rows.Count + incomingRows);

                    BeginLoadData();
                    try
                    {
                        if (mode == ChunkedTableUpdateMode.Replace)
                            Rows.Clear();

                        CopyRows(source);
                    }
                    finally
                    {
                        EndLoadData();
                    }

                    UpdateSegmentTables(source, mode);
                    UpdateCount++;
                }

                if (isFinalChunk)
                    IsComplete = true;

                success = true;
            }
            catch (Exception ex)
            {
                error = ex;
                LastUpdateException = ex;
                throw;
            }
            finally
            {
                IsUpdating = false;
                Updated?.Invoke(this, new ChunkedTableUpdatedEventArgs(
                    this,
                    source,
                    mode,
                    rowsBefore,
                    Rows.Count,
                    incomingRows,
                    chunkNumber,
                    isFinalChunk,
                    isBackgroundUpdate,
                    success,
                    error));
            }
        }

        private void UpdateCompanionTableNames()
        {
            _firstChunkData.TableName = $"{TableName}_FirstChunk";
            _afterFirstChunkData.TableName = $"{TableName}_AfterFirstChunk";
        }

        private void UpdateSegmentTables(DataTable source, ChunkedTableUpdateMode mode)
        {
            if (mode == ChunkedTableUpdateMode.Replace)
            {
                ReplaceTableContent(_firstChunkData, source);
                EnsureSchema(_afterFirstChunkData, source);
                ClearRows(_afterFirstChunkData);
                return;
            }

            AppendTableContent(_afterFirstChunkData, source);
        }

        private void ReplaceTableContent(DataTable target, DataTable source)
        {
            EnsureSchema(target, source);
            target.MinimumCapacity = Math.Max(target.MinimumCapacity, source.Rows.Count);

            target.BeginLoadData();
            try
            {
                target.Rows.Clear();
                CopyRows(target, source);
            }
            finally
            {
                target.EndLoadData();
            }
        }

        private void AppendTableContent(DataTable target, DataTable source)
        {
            EnsureSchema(target, source);
            target.MinimumCapacity = Math.Max(target.MinimumCapacity, target.Rows.Count + source.Rows.Count);

            target.BeginLoadData();
            try
            {
                CopyRows(target, source);
            }
            finally
            {
                target.EndLoadData();
            }
        }

        private static void ClearRows(DataTable table)
        {
            table.BeginLoadData();
            try
            {
                table.Rows.Clear();
            }
            finally
            {
                table.EndLoadData();
            }
        }

        private void EnsureSchema(DataTable target, DataTable source)
        {
            foreach (DataColumn sourceColumn in source.Columns)
            {
                DataColumn targetColumn;
                if (!target.Columns.Contains(sourceColumn.ColumnName))
                {
                    targetColumn = new DataColumn(sourceColumn.ColumnName, sourceColumn.DataType)
                    {
                        AllowDBNull = sourceColumn.AllowDBNull,
                        Caption = string.IsNullOrWhiteSpace(sourceColumn.Caption)
                            ? sourceColumn.ColumnName
                            : sourceColumn.Caption
                    };

                    if (sourceColumn.DataType == typeof(string))
                        targetColumn.MaxLength = sourceColumn.MaxLength;

                    target.Columns.Add(targetColumn);
                }
                else
                {
                    targetColumn = target.Columns[sourceColumn.ColumnName]
                        ?? throw new InvalidOperationException($"Column '{sourceColumn.ColumnName}' was not found after schema check.");
                    if (targetColumn.DataType != sourceColumn.DataType)
                    {
                        throw new InvalidOperationException(
                            $"Column '{sourceColumn.ColumnName}' type changed from '{targetColumn.DataType.FullName}' to '{sourceColumn.DataType.FullName}'.");
                    }

                    if (!string.IsNullOrWhiteSpace(sourceColumn.Caption))
                        targetColumn.Caption = sourceColumn.Caption;
                }

                CopyExtendedProperties(sourceColumn, targetColumn);

                if (_columnMetadata.TryGetValue(sourceColumn.ColumnName, out var metadata))
                    ApplyMetadataToColumn(targetColumn, metadata);

                targetColumn.SetOrdinal(sourceColumn.Ordinal);
            }
        }

        private void CopyRows(DataTable source)
            => CopyRows(this, source);

        private void CopyRows(DataTable target, DataTable source)
        {
            foreach (DataRow sourceRow in source.Rows)
            {
                var targetRow = target.NewRow();
                foreach (DataColumn sourceColumn in source.Columns)
                {
                    if (target.Columns.Contains(sourceColumn.ColumnName))
                        targetRow[sourceColumn.ColumnName] = sourceRow[sourceColumn] ?? DBNull.Value;
                }

                target.Rows.Add(targetRow);

                if (AcceptChangesAfterUpdate)
                    targetRow.AcceptChanges();
            }
        }

        private static void CopyExtendedProperties(DataColumn sourceColumn, DataColumn targetColumn)
        {
            foreach (var key in sourceColumn.ExtendedProperties.Keys.Cast<object>().ToArray())
                targetColumn.ExtendedProperties[key] = sourceColumn.ExtendedProperties[key];
        }

        private static void ApplyMetadataToColumn(DataColumn column, ChunkedTableColumnInfo metadata)
        {
            column.Caption = metadata.DisplayName;
            column.ExtendedProperties["ColumnName"] = metadata.ColumnName;
            column.ExtendedProperties["DisplayName"] = metadata.DisplayName;
            column.ExtendedProperties["Unit"] = metadata.Unit;
            column.ExtendedProperties["Parameter"] = metadata.Parameter;
        }

        private static string ResolveUnit(DynamicClass.ColumnInfo column)
        {
            return FirstNonEmpty(
                column.Unit,
                column.DefaultUnit,
                column.ShowUnit,
                column.ReportUnit,
                column.InputUnit,
                column.LastUsedUnit);
        }

        private static string FirstNonEmpty(params string?[] values)
            => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
    }
}
