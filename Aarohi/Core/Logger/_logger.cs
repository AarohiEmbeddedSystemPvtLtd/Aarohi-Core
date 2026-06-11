using Aarohi.Classes;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.MSSqlServer;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using Timer = System.Threading.Timer;

namespace Aarohi.Core.Logger
{
    public enum LogLevel
    {
        Trace = 0,
        Debug = 1,
        Info = 2,
        Warn = 3,
        Error = 4,
        Fatal = 5
    }

    public sealed class TextLoggerOptions
    {
        public TextLoggerOptions()
        {
            DirectoryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
        }

        public string DirectoryPath { get; init; }
        public string FileNamePrefix { get; init; } = "DefaultLog";
        public string Extension { get; init; } = ".txt";
        public int FlushIntervalSeconds { get; init; } = 2;
        public int BatchSize { get; init; } = 100;
        public Dictionary<string, object>? CommonFields { get; init; }
        public Func<string?>? UserNameProvider { get; init; }
        public string SessionCode { get; init; }
        public bool UseJsonLines { get; init; } = true;
        public bool UseUtcTimestamps { get; init; } = false;
    }

    internal sealed record LogEntry(DateTimeOffset Timestamp, LogLevel Level, string Source, string Message, Exception? Exception, Dictionary<string, object>? Extras);

    public static class _logger 
    {
        private static bool _useSerilog = false;
        private static bool _initialized = false;
        private static TextLoggerOptions _options = new();
        private static readonly ConcurrentQueue<LogEntry> _queue = new();
        private static readonly object _flushLock = new();
        private static Timer? _flushTimer;
        private static int _pendingCount;

        /// <summary>
        /// Initializes the logger in Legacy Mode (writing logs locally to daily rolling text/JSON files in batches).
        /// </summary>
        /// <param name="options">Optional text logger options (log path, batch sizes, common fields, etc.).</param>
        public static void Init(TextLoggerOptions? options = null)
        {
            if (_initialized) return;

            _useSerilog = false;
            _options = options ?? new TextLoggerOptions();
            Directory.CreateDirectory(_options.DirectoryPath);

            _flushTimer = new Timer(_ => TryFlush(), null,
                dueTime: TimeSpan.FromSeconds(_options.FlushIntervalSeconds),
                period: TimeSpan.FromSeconds(_options.FlushIntervalSeconds));

            _initialized = true;
        }

        /// <summary>
        /// Initializes the logger in Serilog Mode (routing all logs asynchronously to a SQL database table and local backup text files).
        /// Also automatically hooks DynamicClass.LogSink to redirect SQL execution logs into Serilog.
        /// </summary>
        /// <param name="connectionString">The SQL Server database connection string.</param>
        /// <param name="tableName">The name of the database table where logs will be stored (defaults to "AppLogs").</param>
        /// <param name="fileNamePrefix">The filename prefix for the local rolling backup text logs (defaults to "DefaultLog").</param>
        public static void InitSerilog(string connectionString, string tableName = "AppLogs", string fileNamePrefix = "DefaultLog")
        {
            if (_initialized) return;

            // 1. Setup Custom Database Column Options
            var columnOptions = new ColumnOptions();
            columnOptions.AdditionalColumns = new Collection<SqlColumn>
            {
                new SqlColumn { ColumnName = "UserName", DataType = System.Data.SqlDbType.NVarChar, DataLength = 100, PropertyName = "username" },
                new SqlColumn { ColumnName = "Operation", DataType = System.Data.SqlDbType.NVarChar, DataLength = 100, PropertyName = "operation" },
                new SqlColumn { ColumnName = "SchemaName", DataType = System.Data.SqlDbType.NVarChar, DataLength = 100, PropertyName = "schema" },
                new SqlColumn { ColumnName = "TableName", DataType = System.Data.SqlDbType.NVarChar, DataLength = 100, PropertyName = "table" },
                new SqlColumn { ColumnName = "KeyColumn", DataType = System.Data.SqlDbType.NVarChar, DataLength = 100, PropertyName = "keyColumn" },
                new SqlColumn { ColumnName = "DurationMs", DataType = System.Data.SqlDbType.BigInt, PropertyName = "durationMs" },
                new SqlColumn { ColumnName = "SqlStatement", DataType = System.Data.SqlDbType.NVarChar, DataLength = -1, PropertyName = "sql" },
                new SqlColumn { ColumnName = "RowCount", DataType = System.Data.SqlDbType.Int, PropertyName = "rowCount" }
            };

            string logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            Directory.CreateDirectory(logDirectory);
            string localLogPath = Path.Combine(logDirectory, $"{fileNamePrefix}_.txt");

            // Enable Serilog SelfLog diagnostics (writes errors like DB write failures)
            Serilog.Debugging.SelfLog.Enable(msg =>
            {
                try
                {
                    File.AppendAllText(Path.Combine(logDirectory, "serilog_selflog.txt"), $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {msg}{Environment.NewLine}");
                }
                catch { }
            });

            // 2. Build Serilog Logger Configuration
            Serilog.Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                // Write locally to file (Primary backup)
                .WriteTo.File(localLogPath, rollingInterval: RollingInterval.Day, outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                // Write asynchronously in batches to SQL Server (Non-blocking UI)
                .WriteTo.Async(a => a.MSSqlServer(
                    connectionString: connectionString,
                    sinkOptions: new MSSqlServerSinkOptions
                    {
                        TableName = tableName,
                        AutoCreateSqlTable = false,
                        BatchPostingLimit = 50,
                        BatchPeriod = TimeSpan.FromSeconds(2)
                    },
                    columnOptions: columnOptions
                ))
                .CreateLogger();

            // 3. Hook DynamicClass to Serilog automatically
            DynamicClass.LogSink = (level, message, source, exception, extras) =>
            {
                LogToSerilog(level, message, source, exception, extras);
            };

            _useSerilog = true;
            _initialized = true;
        }

        /// <summary>
        /// Gracefully shuts down the logger and flushes all pending log entries from memory queues to the database or text files.
        /// Call this once during application exit.
        /// </summary>
        public static void Shutdown()
        {
            if (!_initialized) return;

            _initialized = false;

            if (_useSerilog)
            {
                Serilog.Log.CloseAndFlush();
            }
            else
            {
                try
                {
                    _flushTimer?.Change(Timeout.Infinite, Timeout.Infinite);
                    _flushTimer?.Dispose();
                }
                catch { /* ignore */ }
                finally
                {
                    _flushTimer = null;
                }

                TryFlush(force: true);
            }
        }

        /// <summary>
        /// Logs a message at the specified severity level with optional source tags, exceptions, and metadata.
        /// </summary>
        /// <param name="level">The severity level of the log (Trace, Debug, Info, etc.).</param>
        /// <param name="message">The text log message.</param>
        /// <param name="source">An optional identifier for the module or source context producing the log.</param>
        /// <param name="ex">An optional Exception object associated with the log event.</param>
        /// <param name="extras">An optional dictionary of key-value pairs representing custom metadata context.</param>
        public static void Log(
            LogLevel level,
            string message,
            string source = "",
            Exception? ex = null,
            Dictionary<string, object>? extras = null)
        {
            EnsureInit();

            if (_useSerilog)
            {
                LogToSerilog(level, message, source, ex, extras);
                return;
            }

            // --- LEGACY LOGGING LOGIC ---
            Dictionary<string, object>? mergedExtras = null;
            try
            {
                mergedExtras = MergeExtras(extras, _options.CommonFields);
            }
            catch
            {
                mergedExtras = extras is null
                    ? null
                    : new Dictionary<string, object>(extras, StringComparer.OrdinalIgnoreCase);
            }

            TryAddField(ref mergedExtras, "username", _options.UserNameProvider);
            TryAddField(ref mergedExtras, "SessionCode", _options.SessionCode);

            var entry = new LogEntry(
                Timestamp: DateTimeOffset.UtcNow,
                Level: level,
                Source: source ?? string.Empty,
                Message: message ?? string.Empty,
                Exception: ex,
                Extras: mergedExtras
            );

            _queue.Enqueue(entry);

            if (Interlocked.Increment(ref _pendingCount) >= _options.BatchSize)
                TryFlush();
        }

        private static void LogToSerilog(
            LogLevel level,
            string message,
            string source,
            Exception? ex,
            Dictionary<string, object>? extras)
        {
            LogEventLevel serilogLevel = level switch
            {
                LogLevel.Trace => LogEventLevel.Verbose,
                LogLevel.Debug => LogEventLevel.Debug,
                LogLevel.Info => LogEventLevel.Information,
                LogLevel.Warn => LogEventLevel.Warning,
                LogLevel.Error => LogEventLevel.Error,
                LogLevel.Fatal => LogEventLevel.Fatal,
                _ => LogEventLevel.Information
            };

            var logger = Serilog.Log.ForContext("SourceContext", source);

            // Automatically inject username if provider is configured
            string? username = _options?.UserNameProvider?.Invoke();
            if (!string.IsNullOrWhiteSpace(username))
            {
                logger = logger.ForContext("username", username);
            }

            if (extras != null)
            {
                foreach (var kvp in extras)
                {
                    logger = logger.ForContext(kvp.Key, kvp.Value);
                }
            }

            if (ex != null)
                logger.Write(serilogLevel, ex, message);
            else
                logger.Write(serilogLevel, message);
        }

        private static void EnsureInit()
        {
            if (_initialized) return;
            Init();
        }

        // --- LEGACY UTILITIES ---
        private static void TryAddField(ref Dictionary<string, object>? dict, string key, Func<string?>? provider)
        {
            if (provider is null) return;
            string? value;
            try { value = provider(); } catch { return; }
            if (string.IsNullOrWhiteSpace(value)) return;
            dict ??= new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            dict[key] = value!;
        }

        private static void TryAddField(ref Dictionary<string, object>? dict, string key, string provider)
        {
            if (provider is null) return;
            string? value;
            try { value = provider; } catch { return; }
            if (string.IsNullOrWhiteSpace(value)) return;
            dict ??= new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            dict[key] = value!;
        }

        /// <summary>
        /// Logs a trace message. Usually used for highly detailed diagnostic events.
        /// </summary>
        /// <param name="msg">The log message.</param>
        /// <param name="src">The source module or class context.</param>
        /// <param name="extras">Optional key-value metadata properties.</param>
        public static void Trace(string msg, string src = "", Dictionary<string, object>? extras = null) => Log(LogLevel.Trace, msg, src, null, extras);

        /// <summary>
        /// Logs a debug message. Used for information useful during software troubleshooting.
        /// </summary>
        /// <param name="msg">The log message.</param>
        /// <param name="src">The source module or class context.</param>
        /// <param name="extras">Optional key-value metadata properties.</param>
        public static void Debug(string msg, string src = "", Dictionary<string, object>? extras = null) => Log(LogLevel.Debug, msg, src, null, extras);

        /// <summary>
        /// Logs an informational message. Used to record standard application checkpoints (e.g. system start, successful actions).
        /// </summary>
        /// <param name="msg">The log message.</param>
        /// <param name="src">The source module or class context.</param>
        /// <param name="extras">Optional key-value metadata properties.</param>
        public static void Info(string msg, string src = "", Dictionary<string, object>? extras = null) => Log(LogLevel.Info, msg, src, null, extras);

        /// <summary>
        /// Logs a warning message. Used to denote unexpected deviations or minor, non-blocking issues.
        /// </summary>
        /// <param name="msg">The log message.</param>
        /// <param name="src">The source module or class context.</param>
        /// <param name="ex">Optional Exception details associated with this warning.</param>
        /// <param name="extras">Optional key-value metadata properties.</param>
        public static void Warn(string msg, string src = "", Exception? ex = null, Dictionary<string, object>? extras = null) => Log(LogLevel.Warn, msg, src, ex, extras);

        /// <summary>
        /// Logs an error message. Used to denote failures that block a specific task or operation but do not crash the application.
        /// </summary>
        /// <param name="msg">The log message.</param>
        /// <param name="src">The source module or class context.</param>
        /// <param name="ex">Optional Exception details associated with this error.</param>
        /// <param name="extras">Optional key-value metadata properties.</param>
        public static void Error(string msg, string src = "", Exception? ex = null, Dictionary<string, object>? extras = null) => Log(LogLevel.Error, msg, src, ex, extras);

        /// <summary>
        /// Logs a critical/fatal message. Used to report severe failures that lead to application crash or data corruption.
        /// </summary>
        /// <param name="msg">The log message.</param>
        /// <param name="src">The source module or class context.</param>
        /// <param name="ex">Optional Exception details associated with this fatal event.</param>
        /// <param name="extras">Optional key-value metadata properties.</param>
        public static void Fatal(string msg, string src = "", Exception? ex = null, Dictionary<string, object>? extras = null) => Log(LogLevel.Fatal, msg, src, ex, extras);

        private static Dictionary<string, object>? MergeExtras(Dictionary<string, object>? a, Dictionary<string, object>? b)
        {
            if (a is null && b is null) return null;
            var d = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            if (b != null) foreach (var kv in b) d[kv.Key] = kv.Value;
            if (a != null) foreach (var kv in a) d[kv.Key] = kv.Value;
            return d;
        }

        private static void TryFlush(bool force = false)
        {
            if (!_initialized && !force) return;
            if (!force && _queue.IsEmpty) return;
            if (!Monitor.TryEnter(_flushLock)) return;

            try
            {
                var max = force ? int.MaxValue : _options.BatchSize;
                var batch = new List<LogEntry>(Math.Min(_queue.Count, max));
                while (batch.Count < max && _queue.TryDequeue(out var item))
                    batch.Add(item);

                if (batch.Count == 0) return;
                AppendToDailyFiles(batch);
            }
            catch { }
            finally { Monitor.Exit(_flushLock); }
        }

        private static void AppendToDailyFiles(List<LogEntry> entries)
        {
            var groups = entries.GroupBy(e => GetDateKey(e.Timestamp));
            foreach (var g in groups)
            {
                var filePath = GetFilePathForDate(g.Key);
                using var fs = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                using var sw = new StreamWriter(fs, new UTF8Encoding(false));
                foreach (var e in g) sw.WriteLine(FormatLine(e));
            }
        }

        private static DateTime GetDateKey(DateTimeOffset ts) => (_options.UseUtcTimestamps ? ts.UtcDateTime : ts.LocalDateTime).Date;
        private static string GetFilePathForDate(DateTime date) => Path.Combine(_options.DirectoryPath, $"{_options.FileNamePrefix}_{date:yyyy-MM-dd}{_options.Extension}");

        private static string FormatLine(LogEntry e)
        {
            var ts = _options.UseUtcTimestamps ? e.Timestamp.ToUniversalTime() : e.Timestamp.ToLocalTime();
            var tsStr = ts.ToString("yyyy-MM-dd HH:mm:ss.fff zzz");

            if (_options.UseJsonLines)
            {
                var obj = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["timestamp"] = tsStr,
                    ["level"] = e.Level.ToString(),
                    ["source"] = e.Source,
                    ["message"] = e.Message
                };
                if (e.Exception != null)
                {
                    obj["exceptionType"] = e.Exception.GetType().FullName;
                    obj["exceptionMessage"] = e.Exception.Message;
                    obj["stackTrace"] = e.Exception.StackTrace;
                }
                if (e.Extras != null && e.Extras.Count > 0)
                    obj["extras"] = e.Extras;

                return JsonSerializer.Serialize(obj);
            }
            else
            {
                static string OneLine(string s) => (s ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
                var exPart = e.Exception != null ? OneLine($"{e.Exception.GetType().FullName}: {e.Exception.Message} | {e.Exception.StackTrace}") : "";
                var extrasJson = (e.Extras != null && e.Extras.Count > 0) ? JsonSerializer.Serialize(e.Extras) : "";
                return $"{tsStr}\t{e.Level}\t{OneLine(e.Source)}\t{OneLine(e.Message)}\t{exPart}\t{extrasJson}";
            }
        }

        public static TextLoggerOptions GetOptionsSnapshot()
        {
            EnsureInit();
            return new TextLoggerOptions
            {
                DirectoryPath = _options.DirectoryPath,
                FileNamePrefix = _options.FileNamePrefix,
                Extension = _options.Extension,
                FlushIntervalSeconds = _options.FlushIntervalSeconds,
                BatchSize = _options.BatchSize,
                UseJsonLines = _options.UseJsonLines,
                UseUtcTimestamps = _options.UseUtcTimestamps,
                CommonFields = _options.CommonFields is null ? null : new Dictionary<string, object>(_options.CommonFields, StringComparer.OrdinalIgnoreCase),
                UserNameProvider = _options.UserNameProvider,
                SessionCode = _options.SessionCode
            };
        }

    }
}

#region OLD CODE
//using System;
//using System.Collections.Concurrent;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using System.Text;
//using System.Text.Json;
//using System.Threading;
//using Timer = System.Threading.Timer;

//namespace Aarohi.Core.Logger
//{
//    public enum LogLevel
//    {
//        Trace = 0,
//        Debug = 1,
//        Info = 2,
//        Warn = 3,
//        Error = 4,
//        Fatal = 5
//    }

//    public sealed class TextLoggerOptions
//    {
//        public TextLoggerOptions()
//        {
//            DirectoryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
//        }

//        /// <summary>Directory where log files will be stored.</summary>
//        public string DirectoryPath { get; init; }

//        /// <summary>Example: "DefaultLog" -> "DefaultLog_2026-01-09.txt"</summary>
//        public string FileNamePrefix { get; init; } = "DefaultLog";

//        /// <summary>".txt" or ".log"</summary>
//        public string Extension { get; init; } = ".txt";

//        public int FlushIntervalSeconds { get; init; } = 2;
//        public int BatchSize { get; init; } = 100;

//        /// <summary>Common fields merged into every log entry extras.</summary>
//        public Dictionary<string, object>? CommonFields { get; init; }
//        public Func<string?>? UserNameProvider { get; init; }
//        public string SessionCode { get; init; }
//        /// <summary>
//        /// If true: one JSON object per line (best for parsing & keeping stack traces clean).
//        /// If false: tab-separated human-readable line.
//        /// </summary>
//        public bool UseJsonLines { get; init; } = true;

//        /// <summary>Use UTC timestamps in the file (recommended for servers).</summary>
//        public bool UseUtcTimestamps { get; init; } = false;
//    }

//    internal sealed record LogEntry(
//        DateTimeOffset Timestamp,
//        LogLevel Level,
//        string Source,
//        string Message,
//        Exception? Exception,
//        Dictionary<string, object>? Extras
//    );

//    public static class _logger
//    {
//        public static void Init(TextLoggerOptions? options = null)
//        {
//            if (_initialized) return;

//            _options = options ?? new TextLoggerOptions();
//            Directory.CreateDirectory(_options.DirectoryPath);

//            _flushTimer = new Timer(_ => TryFlush(), null,
//                dueTime: TimeSpan.FromSeconds(_options.FlushIntervalSeconds),
//                period: TimeSpan.FromSeconds(_options.FlushIntervalSeconds));

//            _initialized = true;
//        }

//        public static void Shutdown()
//        {
//            if (!_initialized) return;

//            _initialized = false;

//            try
//            {
//                _flushTimer?.Change(Timeout.Infinite, Timeout.Infinite);
//                _flushTimer?.Dispose();
//            }
//            catch { /* ignore */ }
//            finally
//            {
//                _flushTimer = null;
//            }

//            TryFlush(force: true);
//        }


//        public static void Log(
//            LogLevel level,
//            string message,
//            string source = "",
//            Exception? ex = null,
//            Dictionary<string, object>? extras = null)
//        {
//            EnsureInit();

//            // Merge extras safely (never mutate the caller's dictionary)
//            Dictionary<string, object>? mergedExtras = null;
//            try
//            {
//                mergedExtras = MergeExtras(extras, _options.CommonFields);
//            }
//            catch
//            {
//                mergedExtras = extras is null
//                    ? null
//                    : new Dictionary<string, object>(extras, StringComparer.OrdinalIgnoreCase);
//            }

//            TryAddField(ref mergedExtras, "username", _options.UserNameProvider);
//            TryAddField(ref mergedExtras, "SessionCode", _options.SessionCode);

//            var entry = new LogEntry(
//                Timestamp: DateTimeOffset.UtcNow,
//                Level: level,
//                Source: source ?? string.Empty,
//                Message: message ?? string.Empty,
//                Exception: ex,
//                Extras: mergedExtras
//            );

//            _queue.Enqueue(entry);

//            if (Interlocked.Increment(ref _pendingCount) >= _options.BatchSize)
//                TryFlush();
//        }

//        private static int _pendingCount;

//        private static void TryAddField(
//            ref Dictionary<string, object>? dict,
//            string key,
//            Func<string?>? provider)
//        {
//            if (provider is null) return;

//            string? value;
//            try { value = provider(); }
//            catch { return; }

//            if (string.IsNullOrWhiteSpace(value)) return;

//            dict ??= new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
//            dict[key] = value!;
//        }
//        private static void TryAddField(
//            ref Dictionary<string, object>? dict,
//            string key,
//            string provider)
//        {
//            if (provider is null) return;

//            string? value;
//            try { value = provider; }
//            catch { return; }

//            if (string.IsNullOrWhiteSpace(value)) return;

//            dict ??= new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
//            dict[key] = value!;
//        }

//        public static void Trace(string msg, string src = "", Dictionary<string, object>? extras = null) => Log(LogLevel.Trace, msg, src, null, extras);
//        public static void Debug(string msg, string src = "", Dictionary<string, object>? extras = null) => Log(LogLevel.Debug, msg, src, null, extras);
//        public static void Info(string msg, string src = "", Dictionary<string, object>? extras = null) => Log(LogLevel.Info, msg, src, null, extras);
//        public static void Warn(string msg, string src = "", Exception? ex = null, Dictionary<string, object>? extras = null) => Log(LogLevel.Warn, msg, src, ex, extras);
//        public static void Error(string msg, string src = "", Exception? ex = null, Dictionary<string, object>? extras = null) => Log(LogLevel.Error, msg, src, ex, extras);
//        public static void Fatal(string msg, string src = "", Exception? ex = null, Dictionary<string, object>? extras = null) => Log(LogLevel.Fatal, msg, src, ex, extras);

//        private static void EnsureInit()
//        {
//            if (_initialized) return;
//            Init();
//        }

//        private static Dictionary<string, object>? MergeExtras(Dictionary<string, object>? a, Dictionary<string, object>? b)
//        {
//            if (a is null && b is null) return null;

//            var d = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
//            if (b != null) foreach (var kv in b) d[kv.Key] = kv.Value;
//            if (a != null) foreach (var kv in a) d[kv.Key] = kv.Value;
//            return d;
//        }

//        private static void TryFlush(bool force = false)
//        {
//            if (!_initialized && !force) return;
//            if (!force && _queue.IsEmpty) return;

//            if (!Monitor.TryEnter(_flushLock)) return;

//            try
//            {
//                var max = force ? int.MaxValue : _options.BatchSize;

//                var batch = new List<LogEntry>(Math.Min(_queue.Count, max));
//                while (batch.Count < max && _queue.TryDequeue(out var item))
//                    batch.Add(item);

//                if (batch.Count == 0) return;

//                AppendToDailyFiles(batch);
//            }
//            catch
//            {
//                // Swallow to avoid crashing the app on logging failure
//            }
//            finally
//            {
//                Monitor.Exit(_flushLock);
//            }
//        }

//        private static void AppendToDailyFiles(List<LogEntry> entries)
//        {
//            // Group by date so logs always go into correct day's file (even across midnight)
//            var groups = entries.GroupBy(e => GetDateKey(e.Timestamp));

//            foreach (var g in groups)
//            {
//                var filePath = GetFilePathForDate(g.Key);

//                using var fs = new FileStream(
//                    filePath,
//                    FileMode.Append,
//                    FileAccess.Write,
//                    FileShare.ReadWrite);

//                using var sw = new StreamWriter(fs, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

//                foreach (var e in g)
//                {
//                    sw.WriteLine(FormatLine(e));
//                }
//            }
//        }

//        private static DateTime GetDateKey(DateTimeOffset ts)
//        {
//            var t = _options.UseUtcTimestamps ? ts.UtcDateTime : ts.LocalDateTime;
//            return t.Date;
//        }

//        private static string GetFilePathForDate(DateTime date)
//        {
//            var name = $"{_options.FileNamePrefix}_{date:yyyy-MM-dd}{_options.Extension}";
//            return Path.Combine(_options.DirectoryPath, name);
//        }

//        private static string FormatLine(LogEntry e)
//        {
//            var ts = _options.UseUtcTimestamps ? e.Timestamp.ToUniversalTime() : e.Timestamp.ToLocalTime();
//            var tsStr = ts.ToString("yyyy-MM-dd HH:mm:ss.fff zzz");

//            if (_options.UseJsonLines)
//            {
//                var obj = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
//                {
//                    ["timestamp"] = tsStr,
//                    ["level"] = e.Level.ToString(),
//                    ["source"] = e.Source,
//                    ["message"] = e.Message
//                };

//                if (e.Exception != null)
//                {
//                    obj["exceptionType"] = e.Exception.GetType().FullName;
//                    obj["exceptionMessage"] = e.Exception.Message;
//                    obj["stackTrace"] = e.Exception.StackTrace;
//                }

//                if (e.Extras != null && e.Extras.Count > 0)
//                    obj["extras"] = e.Extras;

//                return JsonSerializer.Serialize(obj);
//            }
//            else
//            {
//                // Tab-separated single-line (safe for most editors)
//                static string OneLine(string s) =>
//                    (s ?? "").Replace("\r", " ").Replace("\n", " ").Trim();

//                var exPart = "";
//                if (e.Exception != null)
//                    exPart = OneLine($"{e.Exception.GetType().FullName}: {e.Exception.Message} | {e.Exception.StackTrace}");

//                var extrasJson = (e.Extras != null && e.Extras.Count > 0)
//                    ? JsonSerializer.Serialize(e.Extras)
//                    : "";

//                return $"{tsStr}\t{e.Level}\t{OneLine(e.Source)}\t{OneLine(e.Message)}\t{exPart}\t{extrasJson}";
//            }
//        }

//        public static TextLoggerOptions GetOptionsSnapshot()
//        {
//            EnsureInit();

//            // Create a safe snapshot (avoid exposing the live reference)
//            return new TextLoggerOptions
//            {
//                DirectoryPath = _options.DirectoryPath,
//                FileNamePrefix = _options.FileNamePrefix,
//                Extension = _options.Extension,
//                FlushIntervalSeconds = _options.FlushIntervalSeconds,
//                BatchSize = _options.BatchSize,
//                UseJsonLines = _options.UseJsonLines,
//                UseUtcTimestamps = _options.UseUtcTimestamps,
//                CommonFields = _options.CommonFields is null
//                    ? null
//                    : new Dictionary<string, object>(_options.CommonFields, StringComparer.OrdinalIgnoreCase),
//                UserNameProvider = _options.UserNameProvider,
//                SessionCode = _options.SessionCode
//            };
//        }

//        private static readonly ConcurrentQueue<LogEntry> _queue = new();
//        private static readonly object _flushLock = new();
//        private static Timer? _flushTimer;
//        private static bool _initialized = false;
//        private static TextLoggerOptions _options = new();
//    }
//}
#endregion