using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Timer = System.Threading.Timer;

namespace AarohiWpfControls.Helper_Classes
{
    /// <summary>
    /// Log severity levels supported by _WpfLogger.
    /// </summary>
    public enum WpfLogLevel
    {
        Trace = 0,
        Debug = 1,
        Info = 2,
        Warn = 3,
        Error = 4,
        Fatal = 5
    }

    /// <summary>
    /// Configuration options for _WpfLogger.
    /// </summary>
    public sealed class WpfLoggerOptions
    {
        public WpfLoggerOptions()
        {
            string appName = AppDomain.CurrentDomain.FriendlyName;

            if (string.IsNullOrWhiteSpace(appName))
                appName = "AarohiWpfApp";

            appName = SanitizeFolderName(appName);

            DirectoryPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Aarohi",
                appName,
                "Logs");

            SessionCode = DateTime.Now.ToString("yyyyMMddHHmmss");
        }

        /// <summary>
        /// Directory where log files will be stored.
        /// For WPF production apps, LocalApplicationData is recommended instead of application base directory.
        /// </summary>
        public string DirectoryPath { get; init; }

        /// <summary>
        /// Log file prefix.
        /// Example: "IMTS_TT" creates "IMTS_TT_2026-05-05.log".
        /// </summary>
        public string FileNamePrefix { get; init; } = "WpfLog";

        /// <summary>
        /// Log file extension.
        /// Recommended: ".log" or ".txt".
        /// </summary>
        public string Extension { get; init; } = ".log";

        /// <summary>
        /// Background flush interval in seconds.
        /// </summary>
        public int FlushIntervalSeconds { get; init; } = 2;

        /// <summary>
        /// Number of queued log entries after which logger tries to flush immediately.
        /// </summary>
        public int BatchSize { get; init; } = 100;

        /// <summary>
        /// Common fields added to every log entry.
        /// Example: application name, machine name, company name, version, etc.
        /// </summary>
        public Dictionary<string, object>? CommonFields { get; init; }

        /// <summary>
        /// Optional function that returns the current logged-in username.
        /// </summary>
        public Func<string?>? UserNameProvider { get; init; }

        /// <summary>
        /// Session code added to every log entry.
        /// Useful for filtering logs from one application run.
        /// </summary>
        public string SessionCode { get; init; }

        /// <summary>
        /// If true, each line is written as one JSON object.
        /// If false, log is written as tab-separated human-readable text.
        /// </summary>
        public bool UseJsonLines { get; init; } = true;

        /// <summary>
        /// If true, timestamps are written in UTC.
        /// If false, timestamps are written in local time.
        /// </summary>
        public bool UseUtcTimestamps { get; init; } = false;

        private static string SanitizeFolderName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');

            return name.Trim();
        }
    }

    internal sealed record WpfLogEntry(
        DateTimeOffset Timestamp,
        WpfLogLevel Level,
        string Source,
        string Message,
        Exception? Exception,
        Dictionary<string, object>? Extras
    );

    /// <summary>
    /// WPF-friendly asynchronous text logger.
    /// </summary>
    /// <remarks>
    /// This logger is designed for WPF desktop applications.
    /// It writes logs to daily files, supports JSON lines or tab-separated text,
    /// automatically flushes in the background, and can hook into WPF unhandled exceptions.
    ///
    /// Recommended initialization location:
    /// App.xaml.cs -> OnStartup(...)
    ///
    /// Recommended shutdown location:
    /// App.xaml.cs -> OnExit(...)
    /// </remarks>
    public static class _WpfLogger
    {
        private static readonly ConcurrentQueue<WpfLogEntry> _queue = new();
        private static readonly object _flushLock = new();

        private static Timer? _flushTimer;
        private static bool _initialized;
        private static bool _exceptionHandlersRegistered;
        private static int _pendingCount;

        private static WpfLoggerOptions _options = new();

        /// <summary>
        /// Initializes the WPF logger.
        /// </summary>
        /// <param name="options">Logger options. If null, default LocalApplicationData path is used.</param>
        /// <param name="registerWpfExceptionHandlers">
        /// True to register WPF DispatcherUnhandledException, AppDomain.UnhandledException,
        /// TaskScheduler.UnobservedTaskException, and Application.Exit handlers.
        /// </param>
        public static void Init(
            WpfLoggerOptions? options = null,
            bool registerWpfExceptionHandlers = true)
        {
            if (_initialized)
                return;

            _options = options ?? new WpfLoggerOptions();

            if (_options.FlushIntervalSeconds <= 0 || _options.BatchSize <= 0)
                _options = CloneWithSafeFlushInterval(_options);

            Directory.CreateDirectory(_options.DirectoryPath);

            _initialized = true;

            if (registerWpfExceptionHandlers)
                RegisterWpfExceptionHandlers();

            _flushTimer = new Timer(
                _ => TryFlush(),
                null,
                dueTime: TimeSpan.FromSeconds(_options.FlushIntervalSeconds),
                period: TimeSpan.FromSeconds(_options.FlushIntervalSeconds));
        }

        /// <summary>
        /// Initializes logger with simple WPF defaults.
        /// </summary>
        /// <param name="applicationName">Application name used in LocalApplicationData log folder.</param>
        /// <param name="filePrefix">Daily log file prefix.</param>
        /// <param name="userNameProvider">Optional current username provider.</param>
        public static void InitDefault(
            string applicationName,
            string filePrefix,
            Func<string?>? userNameProvider = null)
        {
            string safeAppName = SanitizePathPart(applicationName);

            Init(new WpfLoggerOptions
            {
                DirectoryPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Aarohi",
                    safeAppName,
                    "Logs"),

                FileNamePrefix = string.IsNullOrWhiteSpace(filePrefix) ? safeAppName : filePrefix,
                Extension = ".log",
                FlushIntervalSeconds = 2,
                BatchSize = 100,
                UseJsonLines = true,
                UseUtcTimestamps = false,
                SessionCode = DateTime.Now.ToString("yyyyMMddHHmmss"),
                UserNameProvider = userNameProvider,

                CommonFields = new Dictionary<string, object>
                {
                    ["application"] = safeAppName,
                    ["machineName"] = Environment.MachineName,
                    ["processId"] = Environment.ProcessId
                }
            });
        }

        /// <summary>
        /// Writes a log entry.
        /// </summary>
        public static void Log(
            WpfLogLevel level,
            string message,
            string source = "",
            Exception? ex = null,
            Dictionary<string, object>? extras = null)
        {
            EnsureInit();

            Dictionary<string, object>? mergedExtras;

            try
            {
                mergedExtras = MergeExtras(extras, _options.CommonFields);
            }
            catch
            {
                mergedExtras = extras == null
                    ? null
                    : new Dictionary<string, object>(extras, StringComparer.OrdinalIgnoreCase);
            }

            TryAddField(ref mergedExtras, "username", _options.UserNameProvider);
            TryAddField(ref mergedExtras, "sessionCode", _options.SessionCode);

            var entry = new WpfLogEntry(
                Timestamp: DateTimeOffset.UtcNow,
                Level: level,
                Source: source ?? string.Empty,
                Message: message ?? string.Empty,
                Exception: ex,
                Extras: mergedExtras);

            _queue.Enqueue(entry);

            if (Interlocked.Increment(ref _pendingCount) >= _options.BatchSize)
                TryFlush();
        }

        /// <summary>Writes a trace log.</summary>
        public static void Trace(string message, string source = "", Dictionary<string, object>? extras = null)
            => Log(WpfLogLevel.Trace, message, source, null, extras);

        /// <summary>Writes a debug log.</summary>
        public static void Debug(string message, string source = "", Dictionary<string, object>? extras = null)
            => Log(WpfLogLevel.Debug, message, source, null, extras);

        /// <summary>Writes an information log.</summary>
        public static void Info(string message, string source = "", Dictionary<string, object>? extras = null)
            => Log(WpfLogLevel.Info, message, source, null, extras);

        /// <summary>Writes a warning log.</summary>
        public static void Warn(string message, string source = "", Exception? ex = null, Dictionary<string, object>? extras = null)
            => Log(WpfLogLevel.Warn, message, source, ex, extras);

        /// <summary>Writes an error log.</summary>
        public static void Error(string message, string source = "", Exception? ex = null, Dictionary<string, object>? extras = null)
            => Log(WpfLogLevel.Error, message, source, ex, extras);

        /// <summary>Writes a fatal log.</summary>
        public static void Fatal(string message, string source = "", Exception? ex = null, Dictionary<string, object>? extras = null)
            => Log(WpfLogLevel.Fatal, message, source, ex, extras);

        /// <summary>
        /// Flushes queued logs to disk immediately.
        /// </summary>
        public static void Flush()
        {
            TryFlush(force: true);
        }

        /// <summary>
        /// Stops the logger timer and flushes remaining logs.
        /// Call this from WPF App.OnExit.
        /// </summary>
        public static void Shutdown()
        {
            if (!_initialized)
                return;

            _initialized = false;

            try
            {
                _flushTimer?.Change(Timeout.Infinite, Timeout.Infinite);
                _flushTimer?.Dispose();
            }
            catch
            {
                // Ignore logger shutdown errors.
            }
            finally
            {
                _flushTimer = null;
            }

            TryFlush(force: true);
        }

        /// <summary>
        /// Returns a snapshot of current logger options.
        /// </summary>
        public static WpfLoggerOptions GetOptionsSnapshot()
        {
            EnsureInit();

            return new WpfLoggerOptions
            {
                DirectoryPath = _options.DirectoryPath,
                FileNamePrefix = _options.FileNamePrefix,
                Extension = _options.Extension,
                FlushIntervalSeconds = _options.FlushIntervalSeconds,
                BatchSize = _options.BatchSize,
                UseJsonLines = _options.UseJsonLines,
                UseUtcTimestamps = _options.UseUtcTimestamps,
                UserNameProvider = _options.UserNameProvider,
                SessionCode = _options.SessionCode,
                CommonFields = _options.CommonFields == null
                    ? null
                    : new Dictionary<string, object>(_options.CommonFields, StringComparer.OrdinalIgnoreCase)
            };
        }

        /// <summary>
        /// Returns the current day's log file path based on current options.
        /// </summary>
        public static string GetTodayLogFilePath()
        {
            EnsureInit();

            DateTime date = _options.UseUtcTimestamps
                ? DateTime.UtcNow.Date
                : DateTime.Now.Date;

            return GetFilePathForDate(date);
        }

        private static void EnsureInit()
        {
            if (_initialized)
                return;

            Init();
        }

        private static void RegisterWpfExceptionHandlers()
        {
            if (_exceptionHandlersRegistered)
                return;

            try
            {
                if (Application.Current != null)
                {
                    Application.Current.DispatcherUnhandledException += OnDispatcherUnhandledException;
                    Application.Current.Exit += OnApplicationExit;
                }

                AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
                TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

                _exceptionHandlersRegistered = true;
            }
            catch
            {
                // Ignore exception hook registration errors.
            }
        }

        private static void OnDispatcherUnhandledException(
            object sender,
            DispatcherUnhandledExceptionEventArgs e)
        {
            Error(
                "WPF dispatcher unhandled exception.",
                "WPF.DispatcherUnhandledException",
                e.Exception);

            Flush();

            // Do not set e.Handled here.
            // Let the application decide whether it should continue or crash.
        }

        private static void OnApplicationExit(object sender, ExitEventArgs e)
        {
            Info("WPF application exiting.", "WPF.Application.Exit");
            Shutdown();
        }

        private static void OnAppDomainUnhandledException(
            object sender,
            UnhandledExceptionEventArgs e)
        {
            Exception? ex = e.ExceptionObject as Exception;

            Fatal(
                "AppDomain unhandled exception.",
                "AppDomain.UnhandledException",
                ex,
                new Dictionary<string, object>
                {
                    ["isTerminating"] = e.IsTerminating
                });

            Flush();
        }

        private static void OnUnobservedTaskException(
            object? sender,
            UnobservedTaskExceptionEventArgs e)
        {
            Error(
                "Unobserved task exception.",
                "TaskScheduler.UnobservedTaskException",
                e.Exception);

            Flush();

            // Do not call e.SetObserved().
            // Logger should not change application behavior.
        }

        private static void TryAddField(
            ref Dictionary<string, object>? dict,
            string key,
            Func<string?>? provider)
        {
            if (provider == null)
                return;

            string? value;

            try
            {
                value = provider();
            }
            catch
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(value))
                return;

            dict ??= new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            dict[key] = value;
        }

        private static void TryAddField(
            ref Dictionary<string, object>? dict,
            string key,
            string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            dict ??= new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            dict[key] = value;
        }

        private static Dictionary<string, object>? MergeExtras(
            Dictionary<string, object>? a,
            Dictionary<string, object>? b)
        {
            if (a == null && b == null)
                return null;

            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            if (b != null)
            {
                foreach (var item in b)
                    result[item.Key] = item.Value;
            }

            if (a != null)
            {
                foreach (var item in a)
                    result[item.Key] = item.Value;
            }

            return result;
        }

        private static void TryFlush(bool force = false)
        {
            if (!_initialized && !force)
                return;

            if (!force && _queue.IsEmpty)
                return;

            if (!Monitor.TryEnter(_flushLock))
                return;

            int dequeuedCount = 0;

            try
            {
                int max = force ? int.MaxValue : _options.BatchSize;

                var batch = new List<WpfLogEntry>(Math.Min(_queue.Count, max));

                while (batch.Count < max && _queue.TryDequeue(out var item))
                    batch.Add(item);

                dequeuedCount = batch.Count;

                if (batch.Count == 0)
                    return;

                AppendToDailyFiles(batch);
            }
            catch
            {
                // Logging must never crash the application.
            }
            finally
            {
                if (dequeuedCount > 0)
                    Interlocked.Add(ref _pendingCount, -dequeuedCount);

                if (_pendingCount < 0)
                    Interlocked.Exchange(ref _pendingCount, 0);

                Monitor.Exit(_flushLock);
            }
        }

        private static void AppendToDailyFiles(List<WpfLogEntry> entries)
        {
            Directory.CreateDirectory(_options.DirectoryPath);

            var groups = entries.GroupBy(e => GetDateKey(e.Timestamp));

            foreach (var group in groups)
            {
                string filePath = GetFilePathForDate(group.Key);

                using var fs = new FileStream(
                    filePath,
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.ReadWrite);

                using var sw = new StreamWriter(
                    fs,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

                foreach (var entry in group)
                    sw.WriteLine(FormatLine(entry));
            }
        }

        private static DateTime GetDateKey(DateTimeOffset timestamp)
        {
            DateTime value = _options.UseUtcTimestamps
                ? timestamp.UtcDateTime
                : timestamp.LocalDateTime;

            return value.Date;
        }

        private static string GetFilePathForDate(DateTime date)
        {
            string fileName = $"{_options.FileNamePrefix}_{date:yyyy-MM-dd}{_options.Extension}";
            return Path.Combine(_options.DirectoryPath, fileName);
        }

        private static string FormatLine(WpfLogEntry entry)
        {
            DateTimeOffset timestamp = _options.UseUtcTimestamps
                ? entry.Timestamp.ToUniversalTime()
                : entry.Timestamp.ToLocalTime();

            string timestampText = timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff zzz");

            if (_options.UseJsonLines)
                return FormatJsonLine(entry, timestampText);

            return FormatTextLine(entry, timestampText);
        }

        private static string FormatJsonLine(WpfLogEntry entry, string timestampText)
        {
            var obj = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["timestamp"] = timestampText,
                ["level"] = entry.Level.ToString(),
                ["source"] = entry.Source,
                ["message"] = entry.Message
            };

            if (entry.Exception != null)
            {
                obj["exceptionType"] = entry.Exception.GetType().FullName;
                obj["exceptionMessage"] = entry.Exception.Message;
                obj["stackTrace"] = entry.Exception.StackTrace;
            }

            if (entry.Extras != null && entry.Extras.Count > 0)
                obj["extras"] = entry.Extras;

            try
            {
                return JsonSerializer.Serialize(obj);
            }
            catch
            {
                return $"{timestampText}\t{entry.Level}\t{OneLine(entry.Source)}\t{OneLine(entry.Message)}";
            }
        }

        private static string FormatTextLine(WpfLogEntry entry, string timestampText)
        {
            string exceptionPart = string.Empty;

            if (entry.Exception != null)
            {
                exceptionPart = OneLine(
                    $"{entry.Exception.GetType().FullName}: {entry.Exception.Message} | {entry.Exception.StackTrace}");
            }

            string extrasJson = string.Empty;

            if (entry.Extras != null && entry.Extras.Count > 0)
            {
                try
                {
                    extrasJson = JsonSerializer.Serialize(entry.Extras);
                }
                catch
                {
                    extrasJson = string.Empty;
                }
            }

            return $"{timestampText}\t{entry.Level}\t{OneLine(entry.Source)}\t{OneLine(entry.Message)}\t{exceptionPart}\t{extrasJson}";
        }

        private static string OneLine(string? value)
        {
            return (value ?? string.Empty)
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Trim();
        }

        private static string SanitizePathPart(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                value = "AarohiWpfApp";

            foreach (char c in Path.GetInvalidFileNameChars())
                value = value.Replace(c, '_');

            return value.Trim();
        }

        private static WpfLoggerOptions CloneWithSafeFlushInterval(WpfLoggerOptions options)
        {
            return new WpfLoggerOptions
            {
                DirectoryPath = options.DirectoryPath,
                FileNamePrefix = options.FileNamePrefix,
                Extension = options.Extension,
                FlushIntervalSeconds = 2,
                BatchSize = options.BatchSize <= 0 ? 100 : options.BatchSize,
                CommonFields = options.CommonFields,
                UserNameProvider = options.UserNameProvider,
                SessionCode = options.SessionCode,
                UseJsonLines = options.UseJsonLines,
                UseUtcTimestamps = options.UseUtcTimestamps
            };
        }
    }
}