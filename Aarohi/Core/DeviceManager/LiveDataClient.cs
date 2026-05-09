using Aarohi.Core.DeviceManager.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Aarohi.Core.DeviceManager
{
    public sealed class DeviceValuesReceivedEventArgs : EventArgs
    {
        public Guid DeviceId { get; set; }
        public string DeviceName { get; set; } = string.Empty;
        public string DeviceTagName { get; set; } = string.Empty;
        public DateTime TimeStamp { get; set; } = DateTime.Now;
        public List<DeviceRegisterValue> Values { get; set; } = new();
    }

    public sealed class CommunicationServiceRegisterResult
    {
        public bool Success { get; init; }
        public string Message { get; init; } = string.Empty;
        public string CorrelationId { get; init; } = string.Empty;
        public string DeviceName { get; init; } = string.Empty;
        public string RegisterName { get; init; } = string.Empty;
        public int Address { get; init; }
        public string? RequestedValue { get; init; }
        public object? ReadBackValue { get; init; }
        public bool VerificationSucceeded { get; init; }
        public int VerificationAttempts { get; init; }

        public override string ToString()
        {
            return $"Success={Success}, VerificationSucceeded={VerificationSucceeded}, DeviceName='{DeviceName}', RegisterName='{RegisterName}', Address={Address}, RequestedValue='{RequestedValue}', ReadBackValue='{ReadBackValue}', VerificationAttempts={VerificationAttempts}, Message='{Message}', CorrelationId='{CorrelationId}'";
        }
    }

    public sealed class CommunicationServiceRegisterReadResult
    {
        public bool Success { get; init; }
        public string Message { get; init; } = string.Empty;
        public string CorrelationId { get; init; } = string.Empty;
        public string DeviceName { get; init; } = string.Empty;
        public string RegisterName { get; init; } = string.Empty;
        public int Address { get; init; }
        public object? RawValue { get; init; }
        public object? ParsedValue { get; init; }
        public string ValueText { get; init; } = string.Empty;
        public double? NumericValue { get; init; }
        public string Unit { get; init; } = string.Empty;
        public string Quality { get; init; } = string.Empty;
        public bool IsCommunicationOk { get; init; }
        public string ErrorMessage { get; init; } = string.Empty;
        public DateTime TimeStamp { get; init; } = DateTime.Now;

        public override string ToString()
        {
            return $"Success={Success}, DeviceName='{DeviceName}', RegisterName='{RegisterName}', Address={Address}, ParsedValue='{ParsedValue}', ValueText='{ValueText}', NumericValue='{NumericValue}', Quality='{Quality}', IsCommunicationOk={IsCommunicationOk}, ErrorMessage='{ErrorMessage}', Message='{Message}', CorrelationId='{CorrelationId}'";
        }
    }

    public sealed class LiveDataClient : IDisposable
    {
        public const string DefaultPipeName = Aarohi.Globals.AGLobals.PipeNames.CommunictionPipe;

        public event EventHandler<DeviceValuesReceivedEventArgs>? ValuesReceived;

        private readonly string _pipeName;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly ConcurrentDictionary<string, TaskCompletionSource<PipeEnvelope>> _pending = new();
        private readonly SemaphoreSlim _connectionLock = new(1, 1);
        private readonly SemaphoreSlim _writeLock = new(1, 1);

        private NamedPipeClientStream? _pipe;
        private StreamReader? _reader;
        private StreamWriter? _writer;
        private CancellationTokenSource? _cts;
        private Task? _readLoop;

        private static void Trace(string message)
        {
            string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [LiveDataClient] {message}";

            try
            {
                Debug.WriteLine(line);
            }
            catch
            {
            }

            try
            {
                Console.WriteLine(line);
            }
            catch
            {
            }
        }

        public bool IsConnected => _pipe?.IsConnected == true;

        public LiveDataClient(string pipeName = DefaultPipeName)
        {
            _pipeName = pipeName;
            _jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Converters = { new JsonStringEnumConverter() }
            };
        }

        public async Task EnsureConnectedAsync(CancellationToken cancellationToken = default)
        {
            await _connectionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (IsConnected && _readLoop != null && !_readLoop.IsCompleted)
                    return;

                await DisposeConnectionAsync().ConfigureAwait(false);
                Trace($"Connecting to pipe '{_pipeName}'.");

                _pipe = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(5));
                try
                {
                    await _pipe.ConnectAsync(timeout.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    throw new TimeoutException($"Could not connect to the communication service pipe '{_pipeName}'. Make sure CommunicationService is running.");
                }

                _reader = new StreamReader(_pipe, Encoding.UTF8, false, 4096, leaveOpen: true);
                _writer = new StreamWriter(_pipe, new UTF8Encoding(false), 4096, leaveOpen: true) { AutoFlush = true };

                _cts = new CancellationTokenSource();
                _readLoop = Task.Run(() => ReadLoopAsync(_cts.Token));
                Trace($"Connected to pipe '{_pipeName}'.");
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        public async Task<CommunicationServiceRegisterResult> RegisterDeviceAsync(
            DeviceInstance device,
            DeviceProfile profile,
            int? updateRateMs = null,
            CancellationToken cancellationToken = default)
        {
            await EnsureConnectedAsync(cancellationToken);

            string correlationId = Guid.NewGuid().ToString("N");
            var tcs = new TaskCompletionSource<PipeEnvelope>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pending[correlationId] = tcs;

            try
            {
                var envelope = new PipeEnvelope
                {
                    Type = "RegisterDevice",
                    CorrelationId = correlationId,
                    UpdateRate = updateRateMs,
                    Payload = JsonSerializer.SerializeToElement(
                        new { Device = device, Profile = profile }, _jsonOptions)
                };

                await SendAsync(envelope, cancellationToken);

                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(10));
                using (timeout.Token.Register(() => tcs.TrySetCanceled()))
                {
                    PipeEnvelope response = await tcs.Task;

                    if (string.Equals(response.Type, "Ack", StringComparison.OrdinalIgnoreCase))
                    {
                        AckPayload? ack = JsonSerializer.Deserialize<AckPayload>(
                            response.Payload.GetRawText(), _jsonOptions);
                        return new CommunicationServiceRegisterResult
                        {
                            Success = ack?.Success ?? false,
                            Message = ack?.Message ?? string.Empty,
                            CorrelationId = correlationId
                        };
                    }

                    ErrorPayload? err = JsonSerializer.Deserialize<ErrorPayload>(
                        response.Payload.GetRawText(), _jsonOptions);
                    return new CommunicationServiceRegisterResult
                    {
                        Success = false,
                        Message = BuildResponseErrorMessage(response.Type, err?.Message, "Error from communication service."),
                        CorrelationId = correlationId
                    };
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException("Communication service did not acknowledge the device in time.");
            }
            finally
            {
                _pending.TryRemove(correlationId, out _);
            }
        }

        public async Task<CommunicationServiceRegisterResult> SetUpdateRateAsync(
            Guid deviceId,
            int updateRateMs,
            CancellationToken cancellationToken = default)
        {
            await EnsureConnectedAsync(cancellationToken);

            string correlationId = Guid.NewGuid().ToString("N");
            var tcs = new TaskCompletionSource<PipeEnvelope>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pending[correlationId] = tcs;

            try
            {
                var envelope = new PipeEnvelope
                {
                    Type = "UpdateRate",
                    CorrelationId = correlationId,
                    UpdateRate = updateRateMs,
                    Payload = JsonSerializer.SerializeToElement(
                        new { DeviceId = deviceId, UpdateRate = updateRateMs }, _jsonOptions)
                };

                await SendAsync(envelope, cancellationToken);

                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(10));
                using (timeout.Token.Register(() => tcs.TrySetCanceled()))
                {
                    PipeEnvelope response = await tcs.Task;

                    if (string.Equals(response.Type, "Ack", StringComparison.OrdinalIgnoreCase))
                    {
                        AckPayload? ack = JsonSerializer.Deserialize<AckPayload>(
                            response.Payload.GetRawText(), _jsonOptions);
                        return new CommunicationServiceRegisterResult
                        {
                            Success = ack?.Success ?? false,
                            Message = ack?.Message ?? string.Empty,
                            CorrelationId = correlationId
                        };
                    }

                    ErrorPayload? err = JsonSerializer.Deserialize<ErrorPayload>(
                        response.Payload.GetRawText(), _jsonOptions);
                    return new CommunicationServiceRegisterResult
                    {
                        Success = false,
                        Message = BuildResponseErrorMessage(response.Type, err?.Message, "Error from communication service."),
                        CorrelationId = correlationId
                    };
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException("Communication service did not acknowledge the update rate request in time.");
            }
            finally
            {
                _pending.TryRemove(correlationId, out _);
            }
        }

        public async Task<CommunicationServiceRegisterResult> UnregisterDeviceAsync(
            Guid deviceId,
            CancellationToken cancellationToken = default)
        {
            await EnsureConnectedAsync(cancellationToken);

            string correlationId = Guid.NewGuid().ToString("N");
            var tcs = new TaskCompletionSource<PipeEnvelope>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pending[correlationId] = tcs;

            try
            {
                var envelope = new PipeEnvelope
                {
                    Type = "UnregisterDevice",
                    CorrelationId = correlationId,
                    Payload = JsonSerializer.SerializeToElement(new { DeviceId = deviceId }, _jsonOptions)
                };

                await SendAsync(envelope, cancellationToken);

                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(10));
                using (timeout.Token.Register(() => tcs.TrySetCanceled()))
                {
                    PipeEnvelope response = await tcs.Task;

                    if (string.Equals(response.Type, "Ack", StringComparison.OrdinalIgnoreCase))
                    {
                        AckPayload? ack = JsonSerializer.Deserialize<AckPayload>(
                            response.Payload.GetRawText(), _jsonOptions);
                        return new CommunicationServiceRegisterResult
                        {
                            Success = ack?.Success ?? false,
                            Message = ack?.Message ?? string.Empty,
                            CorrelationId = correlationId
                        };
                    }

                    ErrorPayload? err = JsonSerializer.Deserialize<ErrorPayload>(
                        response.Payload.GetRawText(), _jsonOptions);
                    return new CommunicationServiceRegisterResult
                    {
                        Success = false,
                        Message = BuildResponseErrorMessage(response.Type, err?.Message, "Error from communication service."),
                        CorrelationId = correlationId
                    };
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException("Communication service did not acknowledge the unregister request in time.");
            }
            finally
            {
                _pending.TryRemove(correlationId, out _);
            }
        }

        public Task<CommunicationServiceRegisterResult> WriteRegisterAsync(
            Guid deviceId,
            string registerName,
            string? value,
            CancellationToken cancellationToken = default)
        {
            return WriteRegisterCoreAsync(deviceId, registerName, null, value, cancellationToken);
        }

        public Task<CommunicationServiceRegisterResult> WriteRegisterAsync(
            Guid deviceId,
            string registerName,
            object? value,
            CancellationToken cancellationToken = default)
        {
            return WriteRegisterCoreAsync(deviceId, registerName, null, ToWriteString(value), cancellationToken);
        }

        public Task<CommunicationServiceRegisterResult> WriteRegisterByAddressAsync(
            Guid deviceId,
            int address,
            string? value,
            CancellationToken cancellationToken = default)
        {
            return WriteRegisterCoreAsync(deviceId, null, address, value, cancellationToken);
        }

        public Task<CommunicationServiceRegisterResult> WriteRegisterByAddressAsync(
            Guid deviceId,
            int address,
            object? value,
            CancellationToken cancellationToken = default)
        {
            return WriteRegisterCoreAsync(deviceId, null, address, ToWriteString(value), cancellationToken);
        }

        public Task<CommunicationServiceRegisterReadResult> ReadRegisterAsync(
            Guid deviceId,
            string registerName,
            CancellationToken cancellationToken = default)
        {
            return ReadRegisterCoreAsync(deviceId, registerName, null, cancellationToken);
        }

        public Task<CommunicationServiceRegisterReadResult> ReadRegisterByAddressAsync(
            Guid deviceId,
            int address,
            CancellationToken cancellationToken = default)
        {
            return ReadRegisterCoreAsync(deviceId, null, address, cancellationToken);
        }

        public async Task<string> ReadRegisterStringAsync(
            Guid deviceId,
            string registerName,
            CancellationToken cancellationToken = default)
        {
            CommunicationServiceRegisterReadResult result = await ReadRegisterAsync(deviceId, registerName, cancellationToken).ConfigureAwait(false);
            if (!result.Success)
                throw new InvalidOperationException(result.Message);

            return result.ValueText;
        }

        public async Task<string> ReadRegisterStringByAddressAsync(
            Guid deviceId,
            int address,
            CancellationToken cancellationToken = default)
        {
            CommunicationServiceRegisterReadResult result = await ReadRegisterByAddressAsync(deviceId, address, cancellationToken).ConfigureAwait(false);
            if (!result.Success)
                throw new InvalidOperationException(result.Message);

            return result.ValueText;
        }

        private async Task<CommunicationServiceRegisterResult> WriteRegisterCoreAsync(
            Guid deviceId,
            string? registerName,
            int? address,
            string? value,
            CancellationToken cancellationToken = default)
        {
            if (deviceId == Guid.Empty)
                throw new ArgumentException("Device ID is required.", nameof(deviceId));

            if (string.IsNullOrWhiteSpace(registerName) && !address.HasValue)
                throw new ArgumentException("Register name or address is required.", nameof(registerName));

            await EnsureConnectedAsync(cancellationToken);

            string correlationId = Guid.NewGuid().ToString("N");
            var tcs = new TaskCompletionSource<PipeEnvelope>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pending[correlationId] = tcs;

            try
            {
                var envelope = new PipeEnvelope
                {
                    Type = "WriteRegister",
                    CorrelationId = correlationId,
                    Payload = JsonSerializer.SerializeToElement(
                        new
                        {
                            DeviceId = deviceId,
                            RegisterName = registerName,
                            Address = address,
                            Value = value
                        },
                        _jsonOptions)
                };

                await SendAsync(envelope, cancellationToken);

                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(10));
                using (timeout.Token.Register(() => tcs.TrySetCanceled()))
                {
                    PipeEnvelope response = await tcs.Task;

                    if (string.Equals(response.Type, "WriteRegisterAck", StringComparison.OrdinalIgnoreCase))
                    {
                        WriteRegisterAckPayload? ack = JsonSerializer.Deserialize<WriteRegisterAckPayload>(
                            response.Payload.GetRawText(), _jsonOptions);

                        string targetRegisterName = string.IsNullOrWhiteSpace(ack?.RegisterName)
                            ? registerName ?? address?.ToString() ?? string.Empty
                            : ack.RegisterName;
                        string message = string.IsNullOrWhiteSpace(ack?.Message)
                            ? (string.IsNullOrWhiteSpace(ack?.DeviceName)
                                ? $"Write completed for '{targetRegisterName}'."
                                : $"Write completed for '{targetRegisterName}' on '{ack.DeviceName}'.")
                            : ack.Message;

                        CommunicationServiceRegisterResult result = new CommunicationServiceRegisterResult
                        {
                            Success = ack?.Success ?? false,
                            Message = message,
                            CorrelationId = correlationId,
                            DeviceName = ack?.DeviceName ?? string.Empty,
                            RegisterName = targetRegisterName,
                            Address = ack?.Address ?? 0,
                            RequestedValue = ack?.RequestedValue,
                            ReadBackValue = NormalizeJsonValue(ack?.ReadBackValue),
                            VerificationSucceeded = ack?.VerificationSucceeded ?? false,
                            VerificationAttempts = ack?.VerificationAttempts ?? 0
                        };

                        Trace($"Write response received: {result}");
                        return result;
                    }

                    ErrorPayload? err = JsonSerializer.Deserialize<ErrorPayload>(
                        response.Payload.GetRawText(), _jsonOptions);
                    CommunicationServiceRegisterResult errorResult = new CommunicationServiceRegisterResult
                    {
                        Success = false,
                        Message = BuildResponseErrorMessage(response.Type, err?.Message, "Error from communication service."),
                        CorrelationId = correlationId
                    };
                    Trace($"Write response failed: {errorResult}");
                    return errorResult;
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException("Communication service did not acknowledge the register write in time.");
            }
            finally
            {
                _pending.TryRemove(correlationId, out _);
            }
        }

        private static string? ToWriteString(object? value)
        {
            if (value == null)
                return null;

            if (value is string stringValue)
                return stringValue;

            if (value is bool boolValue)
                return boolValue ? "true" : "false";

            if (value is IFormattable formattable)
                return formattable.ToString(null, System.Globalization.CultureInfo.InvariantCulture);

            return value.ToString();
        }

        public async Task DisconnectAsync()
        {
            await _connectionLock.WaitAsync().ConfigureAwait(false);
            try
            {
                await DisposeConnectionAsync().ConfigureAwait(false);
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        private async Task<CommunicationServiceRegisterReadResult> ReadRegisterCoreAsync(
            Guid deviceId,
            string? registerName,
            int? address,
            CancellationToken cancellationToken = default)
        {
            if (deviceId == Guid.Empty)
                throw new ArgumentException("Device ID is required.", nameof(deviceId));

            if (string.IsNullOrWhiteSpace(registerName) && !address.HasValue)
                throw new ArgumentException("Register name or address is required.", nameof(registerName));

            await EnsureConnectedAsync(cancellationToken);

            string correlationId = Guid.NewGuid().ToString("N");
            var tcs = new TaskCompletionSource<PipeEnvelope>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pending[correlationId] = tcs;

            try
            {
                var envelope = new PipeEnvelope
                {
                    Type = "ReadRegister",
                    CorrelationId = correlationId,
                    Payload = JsonSerializer.SerializeToElement(
                        new
                        {
                            DeviceId = deviceId,
                            RegisterName = registerName,
                            Address = address
                        },
                        _jsonOptions)
                };

                await SendAsync(envelope, cancellationToken);

                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(10));
                using (timeout.Token.Register(() => tcs.TrySetCanceled()))
                {
                    PipeEnvelope response = await tcs.Task;

                    if (string.Equals(response.Type, "ReadRegisterAck", StringComparison.OrdinalIgnoreCase))
                    {
                        ReadRegisterAckPayload? ack = JsonSerializer.Deserialize<ReadRegisterAckPayload>(
                            response.Payload.GetRawText(), _jsonOptions);

                        string targetRegisterName = string.IsNullOrWhiteSpace(ack?.RegisterName)
                            ? registerName ?? address?.ToString() ?? string.Empty
                            : ack.RegisterName;

                        object? rawValue = NormalizeJsonValue(ack?.RawValue);
                        object? parsedValue = NormalizeJsonValue(ack?.ParsedValue);
                        string valueText = !string.IsNullOrWhiteSpace(ack?.ValueText)
                            ? ack.ValueText
                            : Convert.ToString(parsedValue, CultureInfo.InvariantCulture) ?? string.Empty;

                        string message = string.IsNullOrWhiteSpace(ack?.Message)
                            ? (string.IsNullOrWhiteSpace(ack?.DeviceName)
                                ? $"Read completed for '{targetRegisterName}'."
                                : $"Read completed for '{targetRegisterName}' on '{ack.DeviceName}'.")
                            : ack.Message;

                        CommunicationServiceRegisterReadResult result = new CommunicationServiceRegisterReadResult
                        {
                            Success = ack?.Success ?? false,
                            Message = message,
                            CorrelationId = correlationId,
                            DeviceName = ack?.DeviceName ?? string.Empty,
                            RegisterName = targetRegisterName,
                            Address = ack?.Address ?? 0,
                            RawValue = rawValue,
                            ParsedValue = parsedValue,
                            ValueText = valueText,
                            NumericValue = ack?.NumericValue,
                            Unit = ack?.Unit ?? string.Empty,
                            Quality = ack?.Quality ?? string.Empty,
                            IsCommunicationOk = ack?.IsCommunicationOk ?? false,
                            ErrorMessage = ack?.ErrorMessage ?? string.Empty,
                            TimeStamp = ack?.TimeStamp ?? DateTime.Now
                        };

                        Trace($"Read response received: {result}");
                        return result;
                    }

                    ErrorPayload? err = JsonSerializer.Deserialize<ErrorPayload>(
                        response.Payload.GetRawText(), _jsonOptions);
                    CommunicationServiceRegisterReadResult errorResult = new CommunicationServiceRegisterReadResult
                    {
                        Success = false,
                        Message = BuildResponseErrorMessage(response.Type, err?.Message, "Error from communication service."),
                        CorrelationId = correlationId
                    };
                    Trace($"Read response failed: {errorResult}");
                    return errorResult;
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException("Communication service did not acknowledge the register read in time.");
            }
            finally
            {
                _pending.TryRemove(correlationId, out _);
            }
        }

        private async Task SendAsync(PipeEnvelope envelope, CancellationToken cancellationToken)
        {
            if (_writer == null)
                throw new InvalidOperationException("Not connected to the communication service.");

            string json = JsonSerializer.Serialize(envelope, _jsonOptions);
            await _writeLock.WaitAsync(cancellationToken);
            try
            {
                await _writer.WriteLineAsync(json.AsMemory(), cancellationToken);
                Trace($"TX Type={envelope.Type}, CorrelationId={envelope.CorrelationId}");
            }
            finally
            {
                _writeLock.Release();
            }
        }

        private async Task ReadLoopAsync(CancellationToken cancellationToken)
        {
            if (_reader == null)
                return;

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    string? line = await _reader.ReadLineAsync(cancellationToken);
                    if (line == null)
                        break;

                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    PipeEnvelope? envelope;
                    try
                    {
                        envelope = JsonSerializer.Deserialize<PipeEnvelope>(line, _jsonOptions);
                    }
                    catch
                    {
                        continue;
                    }

                    if (envelope == null)
                        continue;

                    if (!string.Equals(envelope.Type, "DeviceValues", StringComparison.OrdinalIgnoreCase))
                        Trace($"RX Type={envelope.Type}, CorrelationId={envelope.CorrelationId}");

                    if (!string.IsNullOrEmpty(envelope.CorrelationId) &&
                        _pending.TryGetValue(envelope.CorrelationId, out TaskCompletionSource<PipeEnvelope>? tcs))
                    {
                        tcs.TrySetResult(envelope);
                        continue;
                    }

                    if (string.Equals(envelope.Type, "DeviceValues", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            DeviceValuesPayload? response = JsonSerializer.Deserialize<DeviceValuesPayload>(
                                envelope.Payload.GetRawText(), _jsonOptions);
                            if (response?.Values != null)
                            {
                                int badCount = response.Values.Count(x => !x.IsCommunicationOk);
                                if (badCount > 0)
                                {
                                    Trace($"DeviceValues received. Device={response.DeviceTagName}, Count={response.Values.Count}, BadCount={badCount}, Timestamp={response.TimeStamp:O}");
                                }

                                ValuesReceived?.Invoke(this, new DeviceValuesReceivedEventArgs
                                {
                                    DeviceId = response.DeviceId,
                                    DeviceTagName = response.DeviceTagName,
                                    DeviceName = response.DeviceName,
                                    TimeStamp = response.TimeStamp,
                                    Values = response.Values
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            Trace($"Failed to deserialize DeviceValues payload: {ex.Message}");
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Trace("Read loop cancelled.");
            }
            catch (IOException ex)
            {
                Trace($"Read loop IO error: {ex.Message}");
            }
            catch (Exception ex)
            {
                Trace($"Read loop error: {ex.Message}");
            }
            finally
            {
                FailPendingRequests(new IOException("Communication service connection was closed."));
            }
        }

        private void FailPendingRequests(Exception exception)
        {
            foreach (TaskCompletionSource<PipeEnvelope> tcs in _pending.Values)
                tcs.TrySetException(exception);

            _pending.Clear();
        }

        private async Task DisposeConnectionAsync()
        {
            _cts?.Cancel();
            if (_readLoop != null)
            {
                try
                {
                    await _readLoop.ConfigureAwait(false);
                }
                catch
                {
                }

                _readLoop = null;
            }

            _cts?.Dispose();
            _cts = null;

            _reader?.Dispose();
            _reader = null;
            _writer?.Dispose();
            _writer = null;
            _pipe?.Dispose();
            _pipe = null;

            FailPendingRequests(new IOException("Communication service connection was closed."));
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _reader?.Dispose();
            _writer?.Dispose();
            _pipe?.Dispose();
            FailPendingRequests(new IOException("Communication service connection was closed."));
            _connectionLock.Dispose();
            _writeLock.Dispose();
        }

        private static string BuildResponseErrorMessage(string? responseType, string? responseMessage, string fallbackMessage)
        {
            if (!string.IsNullOrWhiteSpace(responseMessage))
                return responseMessage;

            return string.IsNullOrWhiteSpace(responseType)
                ? fallbackMessage
                : $"Unexpected response type '{responseType}' from communication service.";
        }

        private static object? NormalizeJsonValue(object? value)
        {
            if (value is not JsonElement element)
                return value;

            return element.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number when element.TryGetInt32(out int intValue) => intValue,
                JsonValueKind.Number when element.TryGetInt64(out long longValue) => longValue,
                JsonValueKind.Number when element.TryGetDecimal(out decimal decimalValue) => decimalValue,
                JsonValueKind.Number => element.GetDouble(),
                JsonValueKind.Null or JsonValueKind.Undefined => null,
                _ => element.GetRawText()
            };
        }

        private sealed class PipeEnvelope
        {
            public string Type { get; set; } = string.Empty;
            public string CorrelationId { get; set; } = string.Empty;
            public int? UpdateRate { get; set; }
            public JsonElement Payload { get; set; }
        }

        private sealed class AckPayload
        {
            public bool Success { get; set; }
            public string Message { get; set; } = string.Empty;
        }

        private sealed class ErrorPayload
        {
            public string Message { get; set; } = string.Empty;
        }

        private sealed class WriteRegisterAckPayload
        {
            public bool Success { get; set; }
            public bool VerificationSucceeded { get; set; }
            public string Message { get; set; } = string.Empty;
            public Guid DeviceId { get; set; }
            public string DeviceName { get; set; } = string.Empty;
            public string RegisterName { get; set; } = string.Empty;
            public int Address { get; set; }
            public string? RequestedValue { get; set; }
            public object? ReadBackValue { get; set; }
            public int VerificationAttempts { get; set; }
            public DateTime TimeStamp { get; set; }
        }

        private sealed class ReadRegisterAckPayload
        {
            public bool Success { get; set; }
            public string Message { get; set; } = string.Empty;
            public Guid DeviceId { get; set; }
            public string DeviceName { get; set; } = string.Empty;
            public string RegisterName { get; set; } = string.Empty;
            public int Address { get; set; }
            public object? RawValue { get; set; }
            public object? ParsedValue { get; set; }
            public string ValueText { get; set; } = string.Empty;
            public double? NumericValue { get; set; }
            public string Unit { get; set; } = string.Empty;
            public string Quality { get; set; } = string.Empty;
            public bool IsCommunicationOk { get; set; }
            public string ErrorMessage { get; set; } = string.Empty;
            public DateTime TimeStamp { get; set; }
        }

        private sealed class DeviceValuesPayload
        {
            public Guid DeviceId { get; set; }
            public string DeviceName { get; set; } = string.Empty;
            public string DeviceTagName { get; set; } = string.Empty;
            public DateTime TimeStamp { get; set; } = DateTime.Now;
            public List<DeviceRegisterValue> Values { get; set; } = new();
        }
    }
}
