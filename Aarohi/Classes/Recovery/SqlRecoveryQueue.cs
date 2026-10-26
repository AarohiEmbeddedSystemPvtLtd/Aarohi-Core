#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Aarohi.Classes.Recovery
{
    public sealed class RecoveryQueueStatus
    {
        public int Pending { get; init; }
        public int Blocked { get; init; }
        public int Volatile { get; init; }
        public long Lost { get; init; }
        public bool StorageAvailable { get; init; }
        public bool SqlAvailable { get; init; }
        public string StoragePath { get; init; } = "";
        public string? LastError { get; init; }
    }

    /// <summary>Explicitly created only by recovery-enabled applications. Does not modify legacy DynamicClass methods.</summary>
    public sealed class SqlRecoveryQueue : IAsyncDisposable
    {
        private sealed class Item
        {
            public required RecoveryBatch Batch { get; init; }
            public bool Durable { get; set; }
            public bool Blocked { get; set; }
            public string? Error { get; set; }
            public int MemoryBytes { get; set; }
        }
        private sealed class JournalEvent
        {
            public int Version { get; set; } = 1;
            public string Kind { get; set; } = "";
            public long Sequence { get; set; }
            public RecoveryBatch? Batch { get; set; }
            public string? Error { get; set; }
        }
        private sealed class Envelope
        {
            public string Payload { get; set; } = "";
            public string Checksum { get; set; } = "";
        }

        private readonly Func<SqlConnection> _connectionFactory;
        private readonly RecoveryOptions _options;
        private readonly SemaphoreSlim _gate = new(1, 1);
        private readonly SemaphoreSlim _wake = new(0, int.MaxValue);
        private readonly CancellationTokenSource _cts = new();
        private readonly SortedDictionary<long, Item> _pending = new();
        private readonly string _path;
        private FileStream? _ownership;
        private FileStream? _active;
        private Task? _worker;
        private long _sequence;
        private long _memoryBytes;
        private long _lost;
        private int _segmentIndex;
        private bool _storageAvailable;
        private bool _sqlAvailable;
        private bool _disposed;
        private string? _error;

        public string StoragePath => _path;
        public event Action<RecoveryQueueStatus>? StatusChanged;

        private SqlRecoveryQueue(Func<SqlConnection> factory, RecoveryOptions options)
        {
            _connectionFactory = factory;
            _options = options;
            if (!options.RecoveryEnabled) throw new InvalidOperationException("Recovery is opt-in. Set RecoveryEnabled=true.");
            if (string.IsNullOrWhiteSpace(options.ApplicationId) || options.ApplicationId.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-' && c != '_'))
                throw new ArgumentException("ApplicationId must contain only letters, numbers, '-' and '_'.");
            if (options.MemoryLimitBytes < 1024 || options.SegmentLimitBytes < 1024 || options.SqlCommandTimeoutSeconds < 1)
                throw new ArgumentException("Recovery storage limits and SQL timeout must be positive.");

            using SqlConnection connection = factory();
            var cs = new SqlConnectionStringBuilder(connection.ConnectionString);
            if (string.IsNullOrWhiteSpace(cs.DataSource) || string.IsNullOrWhiteSpace(cs.InitialCatalog))
                throw new ArgumentException("SQL recovery requires an explicit SQL Server and Initial Catalog.");
            string identity = cs.DataSource.Trim().ToUpperInvariant() + "|" + cs.InitialCatalog.Trim().ToUpperInvariant();
            string dbHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))[..16];
            string root = options.StorageRoot ?? Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            if (string.IsNullOrWhiteSpace(root)) throw new DirectoryNotFoundException("Windows shared application-data folder is unavailable.");
            _path = Path.Combine(root, "Aarohi", "Recovery", options.ApplicationId, dbHash);
            if (OperatingSystem.IsWindows())
            {
                string? driveRoot = Path.GetPathRoot(Path.GetFullPath(_path));
                if (driveRoot == null || driveRoot.StartsWith(@"\\", StringComparison.Ordinal) ||
                    new DriveInfo(driveRoot).DriveType != DriveType.Fixed)
                    throw new IOException("Recovery storage must be on an approved persistent local drive.");
            }
        }

        /// <summary>Validates the approved storage folder and recovers previous sessions before enabling writes.</summary>
        public static Task<SqlRecoveryQueue> OpenAsync(Func<SqlConnection> connectionFactory,
            RecoveryOptions options, CancellationToken ct = default) => Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            var queue = new SqlRecoveryQueue(connectionFactory, options);
            try
            {
                Directory.CreateDirectory(queue._path);
                queue._ownership = new FileStream(Path.Combine(queue._path, "owner.lock"), FileMode.OpenOrCreate,
                    FileAccess.ReadWrite, FileShare.None); // Only one owner per journal and database target.
                queue.ValidateFolder();
                queue.LoadJournal();
                queue._storageAvailable = true;
                queue._worker = Task.Run(() => queue.WorkerLoopAsync(queue._cts.Token));
                return queue;
            }
            catch
            {
                queue._active?.Dispose(); queue._ownership?.Dispose(); queue._cts.Dispose();
                queue._gate.Dispose(); queue._wake.Dispose();
                throw; // Caller's setup UI chooses a customer-approved folder. Never pop up inside the DLL.
            }
        }, ct);

        private void ValidateFolder()
        {
            string file = Path.Combine(_path, "write-test-" + Guid.NewGuid().ToString("N"));
            string renamed = file + ".renamed";
            try
            {
                using (var stream = new FileStream(file, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { stream.WriteByte(42); stream.Flush(true); }
                if (File.ReadAllBytes(file).Single() != 42) throw new IOException("Storage validation failed.");
                File.Move(file, renamed);
                File.Delete(renamed);
            }
            finally
            {
                try { if (File.Exists(file)) File.Delete(file); } catch { }
                try { if (File.Exists(renamed)) File.Delete(renamed); } catch { }
            }
        }

        /// <summary>Journal-first acceptance. Returns immediately after storage commit; SQL replay runs separately.</summary>
        public Task<RecoveryReceipt> EnqueueAsync(RecoveryBatch batch, CancellationToken ct = default) => Task.Run(async () =>
        {
            ArgumentNullException.ThrowIfNull(batch);
            batch.Validate(_options.MaxStatementsPerGroup);
            // Deep clone to prevent application edits after submission.
            RecoveryBatch snapshot = JsonSerializer.Deserialize<RecoveryBatch>(JsonSerializer.Serialize(batch))!;
            await _gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                Item? duplicate = _pending.Values.FirstOrDefault(p => p.Batch.OperationId == snapshot.OperationId);
                if (duplicate != null)
                    return new RecoveryReceipt { OperationId = snapshot.OperationId,
                        Status = duplicate.Durable ? RecoveryWriteStatus.QueuedDurable : RecoveryWriteStatus.QueuedVolatile,
                        Message = "This logical operation is already pending; it was not appended twice." };
                snapshot.Sequence = ++_sequence;
                var entry = new JournalEvent { Kind = "add", Sequence = snapshot.Sequence, Batch = snapshot };
                int size = Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(entry));
                bool durable;
                try
                {
                    Append(entry);
                    _storageAvailable = true;
                    durable = true;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    _storageAvailable = false;
                    _error = "Journal write failed: " + ex.Message;
                    durable = false;
                }
                if (!durable && _memoryBytes + size > _options.MemoryLimitBytes)
                {
                    _lost++;
                    Publish();
                    return new RecoveryReceipt { OperationId = snapshot.OperationId, Status = RecoveryWriteStatus.Rejected,
                        Message = "CRITICAL: SQL operation was not retained; recovery storage and memory capacity are unavailable." };
                }
                _pending.Add(snapshot.Sequence, new Item { Batch = snapshot, Durable = durable, MemoryBytes = durable ? 0 : size });
                if (!durable) _memoryBytes += size;
                if (_wake.CurrentCount == 0) _wake.Release();
                Publish();
                return new RecoveryReceipt { OperationId = snapshot.OperationId,
                    Status = durable ? RecoveryWriteStatus.QueuedDurable : RecoveryWriteStatus.QueuedVolatile,
                    Message = durable ? "Stored in crash-safe recovery journal." : "RAM only; data will be lost if application exits before recovery." };
            }
            finally { _gate.Release(); }
        }, ct);

        public async Task<RecoveryQueueStatus> GetStatusAsync(CancellationToken ct = default)
        {
            await _gate.WaitAsync(ct).ConfigureAwait(false);
            try { return Snapshot(); }
            finally { _gate.Release(); }
        }
        public async Task<bool> HasPendingStreamAsync(string streamKey, CancellationToken ct = default)
        {
            await _gate.WaitAsync(ct).ConfigureAwait(false);
            try { return _pending.Values.Any(x => x.Batch.StreamKey == streamKey); }
            finally { _gate.Release(); }
        }

        /// <summary>Allows an administrator to retry a blocked group after repairing its underlying SQL/schema issue.</summary>
        public async Task RetryBlockedAsync(Guid operationId, CancellationToken ct = default)
        {
            await _gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var found = _pending.FirstOrDefault(k => k.Value.Batch.OperationId == operationId);
                Item? item = found.Value;
                if (item == null || !item.Blocked) return;
                if (item.Durable) Append(new JournalEvent { Kind = "unblock", Sequence = found.Key });
                item.Blocked = false;
                item.Error = null;
                _wake.Release();
                Publish();
            }
            finally { _gate.Release(); }
        }

        private async Task WorkerLoopAsync(CancellationToken ct)
        {
            int consecutiveFailures = 0;
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    RecoveryQueueStatus status = await GetStatusAsync(ct).ConfigureAwait(false);
                    if (status.Pending == 0 || status.Pending == status.Blocked)
                    {
                        // Wait for newly queued data or an administrator retry; no idle SQL polling.
                        await _wake.WaitAsync(60000, ct).ConfigureAwait(false);
                        continue;
                    }
                    if (consecutiveFailures > 0)
                    {
                        int delay = consecutiveFailures switch { 1 => 5000, 2 => 10000, 3 => 20000, 4 => 40000, _ => 60000 };
                        // Deliberately ignore enqueue signals during backoff: prevents one SQL probe per measurement.
                        await Task.Delay(delay, ct).ConfigureAwait(false);
                    }
                    bool progressed = await ProcessPendingAsync(ct).ConfigureAwait(false);
                    consecutiveFailures = progressed ? 0 : Math.Min(5, consecutiveFailures + 1);
                    if (progressed) await Task.Yield();
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
                catch (Exception ex)
                {
                    _error = "Recovery worker error: " + ex.Message;
                    Publish();
                    try { await Task.Delay(60000, ct).ConfigureAwait(false); }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
                }
            }
        }

        private async Task<bool> ProcessPendingAsync(CancellationToken ct)
        {
            Item? item;
            await _gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                // Try to durably persist any RAM-only records before accessing SQL again.
                foreach (var entry in _pending.Where(x => !x.Value.Durable).ToArray())
                {
                    try
                    {
                        Append(new JournalEvent { Kind = "add", Sequence = entry.Key, Batch = entry.Value.Batch });
                        entry.Value.Durable = true;
                        _memoryBytes -= entry.Value.MemoryBytes;
                        entry.Value.MemoryBytes = 0;
                        _storageAvailable = true;
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        _storageAvailable = false;
                        _error = "Storage not restored: " + ex.Message;
                        break;
                    }
                }
                var blocked = new HashSet<string>(StringComparer.Ordinal);
                item = null;
                foreach (Item candidate in _pending.Values)
                {
                    if (candidate.Blocked) { blocked.Add(candidate.Batch.StreamKey); continue; }
                    if (blocked.Contains(candidate.Batch.StreamKey)) continue;
                    item = candidate; break;
                }
                if (item == null)
                {
                    Publish();
                    return false;
                }
            }
            finally { _gate.Release(); }

            try
            {
                await RecoverySqlExecutor.CommitAsync(_connectionFactory, item.Batch, _options.SqlCommandTimeoutSeconds, ct).ConfigureAwait(false);
                await _gate.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    _sqlAvailable = true;
                    _error = null;
                    if (item.Durable)
                    {
                        try
                        {
                            Append(new JournalEvent { Kind = "ack", Sequence = item.Batch.Sequence });
                            _storageAvailable = true;
                        }
                        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                        {
                            _storageAvailable = false;
                            _error = "SQL committed but journal acknowledgment failed: " + ex.Message;
                            // Keep queued: replay ledger makes further attempts idempotent.
                            Publish();
                            return false;
                        }
                    }
                    _pending.Remove(item.Batch.Sequence);
                    _memoryBytes -= item.MemoryBytes;
                    if (_pending.Count == 0 && _storageAvailable) CleanupCompletedJournal();
                    Publish();
                    return true;
                }
                finally { _gate.Release(); }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                await _gate.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    if (RecoverySqlExecutor.IsTransient(ex)) _sqlAvailable = false;
                    else
                    {
                        item.Blocked = true;
                        item.Error = ex.Message;
                        if (item.Durable)
                            try { Append(new JournalEvent { Kind = "block", Sequence = item.Batch.Sequence, Error = ex.Message }); }
                            catch (IOException io) { _storageAvailable = false; _error = io.Message; }
                            catch (UnauthorizedAccessException io) { _storageAvailable = false; _error = io.Message; }
                    }
                    _error = ex.Message;
                    Publish();
                    return !RecoverySqlExecutor.IsTransient(ex); // Try next independent stream after permanent SQL error.
                }
                finally { _gate.Release(); }
            }
        }

        private static string Hash(string content) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
        private string[] Segments() => Directory.GetFiles(_path, "journal-*.jsonl").OrderBy(x => x, StringComparer.Ordinal).ToArray();
        private string SegmentPath(int index) => Path.Combine(_path, $"journal-{index:D8}.jsonl");

        private void LoadJournal()
        {
            foreach (string path in Segments())
            {
                string number = Path.GetFileNameWithoutExtension(path).Substring("journal-".Length);
                if (!int.TryParse(number, out int index)) throw new IOException("Unexpected journal filename.");
                _segmentIndex = Math.Max(_segmentIndex, index);
                byte[] all = File.ReadAllBytes(path);
                int lastNewline = Array.LastIndexOf(all, (byte)'\n');
                if (lastNewline != all.Length - 1)
                {
                    // A power failure may leave an unacknowledged partial line. Preserve it for investigation.
                    File.Copy(path, path + ".incomplete-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"), true);
                    using var fix = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None);
                    fix.SetLength(lastNewline + 1);
                    fix.Flush(true);
                    all = all[..(lastNewline + 1)];
                }
                int from = 0;
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i] != '\n') continue;
                    string line = Encoding.UTF8.GetString(all, from, i - from).TrimEnd('\r');
                    from = i + 1;
                    if (line.Length == 0) continue;
                    Envelope env = JsonSerializer.Deserialize<Envelope>(line) ?? throw new InvalidDataException("Invalid journal envelope.");
                    if (!string.Equals(env.Checksum, Hash(env.Payload), StringComparison.Ordinal))
                        throw new InvalidDataException($"Journal checksum mismatch in {Path.GetFileName(path)}. Preserve files and contact support.");
                    JournalEvent evt = JsonSerializer.Deserialize<JournalEvent>(env.Payload) ?? throw new InvalidDataException("Invalid journal record.");
                    if (evt.Version != 1) throw new InvalidDataException($"Unsupported journal schema version {evt.Version}.");
                    _sequence = Math.Max(_sequence, evt.Sequence);
                    switch (evt.Kind)
                    {
                        case "add":
                            if (evt.Batch == null || evt.Batch.Sequence != evt.Sequence) throw new InvalidDataException("Inconsistent journal operation.");
                            evt.Batch.Validate(_options.MaxStatementsPerGroup);
                            _pending[evt.Sequence] = new Item { Batch = evt.Batch, Durable = true };
                            break;
                        case "ack": _pending.Remove(evt.Sequence); break;
                        case "block":
                            if (_pending.TryGetValue(evt.Sequence, out Item? blocked)) { blocked.Blocked = true; blocked.Error = evt.Error; }
                            break;
                        case "unblock":
                            if (_pending.TryGetValue(evt.Sequence, out Item? unblocked)) { unblocked.Blocked = false; unblocked.Error = null; }
                            break;
                        default: throw new InvalidDataException("Unsupported recovery journal event.");
                    }
                }
            }
            if (_segmentIndex == 0) _segmentIndex = 1;
            _active = new FileStream(SegmentPath(_segmentIndex), FileMode.Append, FileAccess.Write, FileShare.Read);
        }

        private void Append(JournalEvent evt)
        {
            if (_active == null) // Attempt to reopen after a previous storage I/O error.
            {
                // Revalidate tail of current segment before appending after interrupted writes.
                string path = SegmentPath(_segmentIndex);
                byte[] bytes = File.Exists(path) ? File.ReadAllBytes(path) : Array.Empty<byte>();
                int end = Array.LastIndexOf(bytes, (byte)'\n') + 1;
                if (end < bytes.Length)
                {
                    using var repair = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None);
                    repair.SetLength(end);
                    repair.Flush(true);
                }
                _active = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
            }
            string payload = JsonSerializer.Serialize(evt);
            string jsonl = JsonSerializer.Serialize(new Envelope { Payload = payload, Checksum = Hash(payload) }) + "\n";
            byte[] bytesToWrite = Encoding.UTF8.GetBytes(jsonl);
            try
            {
                if (_active.Length + bytesToWrite.Length > _options.SegmentLimitBytes && _active.Length > 0)
                {
                    _active.Dispose(); _active = null;
                    _segmentIndex++;
                    _active = new FileStream(SegmentPath(_segmentIndex), FileMode.Append, FileAccess.Write, FileShare.Read);
                }
                _active.Write(bytesToWrite, 0, bytesToWrite.Length);
                _active.Flush(true);
            }
            catch
            {
                _active?.Dispose(); _active = null;
                throw;
            }
        }

        private void CleanupCompletedJournal()
        {
            // Only compact when EVERY record has been acknowledged. SQL ledger provides crash-safe replay.
            _active?.Dispose(); _active = null;
            foreach (string file in Segments()) File.Delete(file);
            _segmentIndex = 1;
            _active = new FileStream(SegmentPath(_segmentIndex), FileMode.Append, FileAccess.Write, FileShare.Read);
        }

        private RecoveryQueueStatus Snapshot() => new()
        {
            Pending = _pending.Count,
            Blocked = _pending.Values.Count(v => v.Blocked),
            Volatile = _pending.Values.Count(v => !v.Durable),
            Lost = _lost,
            StorageAvailable = _storageAvailable,
            SqlAvailable = _sqlAvailable,
            StoragePath = _path,
            LastError = _error
        };
        private void Publish()
        {
            Action<RecoveryQueueStatus>? notify = StatusChanged;
            if (notify == null) return;
            RecoveryQueueStatus status = Snapshot();
            _ = Task.Run(() =>
            {
                try { notify(status); }
                catch { /* UI subscriber failures cannot affect journal or SQL. */ }
            });
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;
            _cts.Cancel();
            try { if (_worker != null) await _worker.ConfigureAwait(false); } catch (OperationCanceledException) { }
            await _gate.WaitAsync().ConfigureAwait(false);
            try { _active?.Dispose(); _ownership?.Dispose(); }
            finally { _gate.Release(); }
            _cts.Dispose(); _gate.Dispose(); _wake.Dispose();
        }
    }
}
