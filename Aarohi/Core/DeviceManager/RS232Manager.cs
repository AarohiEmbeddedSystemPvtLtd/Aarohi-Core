using System;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Aarohi.Core.DeviceManager
{
    public sealed class RS232Manager : IDisposable
    {
        private readonly object _sync = new object();
        private SerialPort? _port;

        private readonly StringBuilder _textBuffer = new StringBuilder(4096);
        private TaskCompletionSource<string>? _pendingReplyTcs;

        // ===== Config =====
        public string PortName { get; set; } = "COM1";
        public int BaudRate { get; set; } = 9600;
        public Parity Parity { get; set; } = Parity.None;
        public int DataBits { get; set; } = 8;
        public StopBits StopBits { get; set; } = StopBits.One;
        public Handshake Handshake { get; set; } = Handshake.None;

        /// <summary>Line delimiter used for line-based parsing. Common: "\r\n" or "\n"</summary>
        public string NewLine { get; set; } = "\r\n";

        /// <summary>Read/Write timeouts on SerialPort (ms). For async waiting we use our own timeout too.</summary>
        public int ReadTimeoutMs { get; set; } = 300;
        public int WriteTimeoutMs { get; set; } = 300;

        /// <summary>If true, line parsing uses NewLine. If false, you can rely on RawBytesReceived.</summary>
        public bool EnableLineParsing { get; set; } = true;

        // ===== State =====
        public bool IsOpen
        {
            get { lock (_sync) return _port != null && _port.IsOpen; }
        }

        // ===== Events =====
        /// <summary>Raised when a full line is received (based on NewLine).</summary>
        public event Action<string>? LineReceived;

        /// <summary>Raised when raw bytes come in.</summary>
        public event Action<byte[]>? RawBytesReceived;

        /// <summary>Raised on open/close.</summary>
        public event Action<bool>? ConnectionStateChanged;

        /// <summary>Raised when SerialPort throws in receive event.</summary>
        public event Action<Exception>? Error;

        // ===== Public API =====
        public void Open()
        {
            lock (_sync)
            {
                if (_port != null && _port.IsOpen) return;

                _port = new SerialPort(PortName, BaudRate, Parity, DataBits, StopBits)
                {
                    Handshake = Handshake,
                    NewLine = NewLine,
                    ReadTimeout = ReadTimeoutMs,
                    WriteTimeout = WriteTimeoutMs,
                    Encoding = Encoding.ASCII // change if device uses different encoding
                };

                _port.DataReceived += Port_DataReceived;
                _port.ErrorReceived += Port_ErrorReceived;

                _port.Open();
            }

            ConnectionStateChanged?.Invoke(true);
        }

        public void Close()
        {
            SerialPort? p;
            lock (_sync)
            {
                p = _port;
                _port = null;

                _pendingReplyTcs?.TrySetCanceled();
                _pendingReplyTcs = null;
                _textBuffer.Clear();
            }

            if (p != null)
            {
                try
                {
                    p.DataReceived -= Port_DataReceived;
                    p.ErrorReceived -= Port_ErrorReceived;

                    if (p.IsOpen) p.Close();
                    p.Dispose();
                }
                catch { /* keep close silent */ }
            }

            ConnectionStateChanged?.Invoke(false);
        }

        public void Send(string text, bool appendNewLine = true)
        {
            SerialPort? p;
            lock (_sync) p = _port;

            if (p == null || !p.IsOpen) throw new InvalidOperationException("RS232 port is not open.");

            var payload = appendNewLine ? (text + NewLine) : text;
            p.Write(payload);
        }

        public void SendBytes(byte[] data)
        {
            SerialPort? p;
            lock (_sync) p = _port;

            if (p == null || !p.IsOpen) throw new InvalidOperationException("RS232 port is not open.");
            p.Write(data, 0, data.Length);
        }

        /// <summary>
        /// Sends a command and waits until a reply containing expectedSubstring is received (or any line if expectedSubstring is null/empty).
        /// This is also useful for "TestConnection".
        /// </summary>
        public async Task<string> QueryAsync(
            string command,
            string? expectedSubstring = null,
            int timeoutMs = 1500,
            bool appendNewLine = true,
            CancellationToken ct = default)
        {
            if (!IsOpen) Open();

            Task<string> waitTask;
            lock (_sync)
            {
                // Only one pending query at a time (simple & safe).
                if (_pendingReplyTcs != null)
                    throw new InvalidOperationException("A query is already in progress. Wait for it to finish.");

                _pendingReplyTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
                waitTask = _pendingReplyTcs.Task;
            }

            try
            {
                // Send after setting TCS (avoid race where reply comes instantly)
                Send(command, appendNewLine);

                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(timeoutMs);

                var completed = await Task.WhenAny(waitTask, Task.Delay(Timeout.Infinite, timeoutCts.Token));
                if (completed != waitTask)
                    throw new TimeoutException($"No reply within {timeoutMs} ms for command: {command}");

                var reply = await waitTask; // already completed

                if (!string.IsNullOrWhiteSpace(expectedSubstring) &&
                    reply.IndexOf(expectedSubstring, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    throw new TimeoutException($"Reply received but did not match expected text. Reply: {reply}");
                }

                return reply;
            }
            finally
            {
                lock (_sync)
                {
                    _pendingReplyTcs = null;
                }
            }
        }

        /// <summary>
        /// Connection testing:
        /// 1) If no testCommand provided -> just tries Open() and returns true if open succeeded.
        /// 2) If testCommand provided -> sends it and waits for a reply (optionally checks expectedSubstring).
        /// </summary>
        public async Task<bool> TestConnection(
            string? testCommand = null,
            string? expectedSubstring = null,
            int timeoutMs = 1500,
            bool appendNewLine = true,
            CancellationToken ct = default)
        {
            try
            {
                if (!IsOpen) Open();

                // Basic "port open" check
                if (string.IsNullOrWhiteSpace(testCommand))
                {
                    // Optional: you can also check modem lines if your hardware supports it:
                    // bool dsr = _port?.DsrHolding ?? false;
                    // bool cts = _port?.CtsHolding ?? false;
                    return true;
                }

                // Command-reply check
                _ = await QueryAsync(testCommand, expectedSubstring, timeoutMs, appendNewLine, ct);
                return true;
            }
            catch
            {
                return false;
            }
        }

        // ===== Sync wrappers =====

        public string Query(
            string command,
            string? expectedSubstring = null,
            int timeoutMs = 1500,
            bool appendNewLine = true,
            CancellationToken ct = default)
        {
            // Safe sync wait pattern
            return QueryAsync(command, expectedSubstring, timeoutMs, appendNewLine, ct)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
        }

        public bool TestConnectionSync(
            string? testCommand = null,
            string? expectedSubstring = null,
            int timeoutMs = 1500,
            bool appendNewLine = true,
            CancellationToken ct = default)
        {
            return TestConnection(testCommand, expectedSubstring, timeoutMs, appendNewLine, ct)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
        }


        // ===== Serial event handlers =====
        private void Port_ErrorReceived(object sender, SerialErrorReceivedEventArgs e)
        {
            Error?.Invoke(new IOException($"Serial error: {e.EventType}"));
        }

        private void Port_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            SerialPort? p;
            lock (_sync) p = _port;
            if (p == null) return;

            try
            {
                int bytesToRead = p.BytesToRead;
                if (bytesToRead <= 0) return;

                var buffer = new byte[bytesToRead];
                int n = p.Read(buffer, 0, buffer.Length);
                if (n <= 0) return;

                if (n != buffer.Length)
                {
                    var trimmed = new byte[n];
                    Buffer.BlockCopy(buffer, 0, trimmed, 0, n);
                    buffer = trimmed;
                }

                RawBytesReceived?.Invoke(buffer);

                if (!EnableLineParsing) return;

                // Convert to text (ASCII by default)
                var chunk = p.Encoding.GetString(buffer);

                string? completedLine = null;

                lock (_sync)
                {
                    _textBuffer.Append(chunk);

                    // Extract last complete line (you can loop if you want ALL lines)
                    int idx;
                    while ((idx = _textBuffer.ToString().IndexOf(NewLine, StringComparison.Ordinal)) >= 0)
                    {
                        completedLine = _textBuffer.ToString(0, idx);
                        _textBuffer.Remove(0, idx + NewLine.Length);

                        // Fire event outside lock
                        var lineToRaise = completedLine;

                        // If a query is pending, complete it on first received line
                        _pendingReplyTcs?.TrySetResult(lineToRaise);

                        // Raise LineReceived
                        ThreadPool.QueueUserWorkItem(_ => LineReceived?.Invoke(lineToRaise));
                    }
                }
            }
            catch (Exception ex)
            {
                Error?.Invoke(ex);
            }
        }

        public void Dispose()
        {
            Close();
        }
    }

}
