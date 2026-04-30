using Aarohi.Core.DeviceManager.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
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
    }

    public sealed class LiveDataClient : IDisposable
    {
        public const string DefaultPipeName = "CommunicationServicePipe";

        public event EventHandler<DeviceValuesReceivedEventArgs>? ValuesReceived;

        private readonly string _pipeName;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly ConcurrentDictionary<string, TaskCompletionSource<PipeEnvelope>> _pending = new();
        private readonly SemaphoreSlim _writeLock = new(1, 1);

        private NamedPipeClientStream? _pipe;
        private StreamReader? _reader;
        private StreamWriter? _writer;
        private CancellationTokenSource? _cts;
        private Task? _readLoop;

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
            if (IsConnected && _readLoop != null && !_readLoop.IsCompleted)
                return;

            await DisposeConnectionAsync();

            _pipe = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            try
            {
                await _pipe.ConnectAsync(timeout.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"Could not connect to the communication service pipe '{_pipeName}'. Make sure CommunicationService is running.");
            }

            _reader = new StreamReader(_pipe, Encoding.UTF8, false, 4096, leaveOpen: true);
            _writer = new StreamWriter(_pipe, new UTF8Encoding(false), 4096, leaveOpen: true) { AutoFlush = true };

            _cts = new CancellationTokenSource();
            _readLoop = Task.Run(() => ReadLoopAsync(_cts.Token));
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
                        Message = err?.Message ?? "Error from communication service.",
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
                        Message = err?.Message ?? "Error from communication service.",
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
                        Message = err?.Message ?? "Error from communication service.",
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

        public async Task<CommunicationServiceRegisterResult> WriteRegisterAsync(
            Guid deviceId,
            string registerName,
            object? value,
            CancellationToken cancellationToken = default)
        {
            if (deviceId == Guid.Empty)
                throw new ArgumentException("Device ID is required.", nameof(deviceId));
            if (string.IsNullOrWhiteSpace(registerName))
                throw new ArgumentException("Register name is required.", nameof(registerName));

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
                            ? registerName
                            : ack.RegisterName;
                        string message = string.IsNullOrWhiteSpace(ack?.DeviceName)
                            ? $"Wrote '{targetRegisterName}'."
                            : $"Wrote '{targetRegisterName}' on '{ack.DeviceName}'.";

                        return new CommunicationServiceRegisterResult
                        {
                            Success = true,
                            Message = message,
                            CorrelationId = correlationId
                        };
                    }

                    ErrorPayload? err = JsonSerializer.Deserialize<ErrorPayload>(
                        response.Payload.GetRawText(), _jsonOptions);
                    return new CommunicationServiceRegisterResult
                    {
                        Success = false,
                        Message = err?.Message ?? "Error from communication service.",
                        CorrelationId = correlationId
                    };
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

        public async Task DisconnectAsync()
        {
            await DisposeConnectionAsync();
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
                        catch
                        {
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (IOException)
            {
            }
            catch (Exception)
            {
            }
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

            foreach (TaskCompletionSource<PipeEnvelope> tcs in _pending.Values)
                tcs.TrySetCanceled();
            _pending.Clear();
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _reader?.Dispose();
            _writer?.Dispose();
            _pipe?.Dispose();
            _writeLock.Dispose();
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
            public Guid DeviceId { get; set; }
            public string DeviceName { get; set; } = string.Empty;
            public string RegisterName { get; set; } = string.Empty;
            public int Address { get; set; }
            public object? RequestedValue { get; set; }
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
