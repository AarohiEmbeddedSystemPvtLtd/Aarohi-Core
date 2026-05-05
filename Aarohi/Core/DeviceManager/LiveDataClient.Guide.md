# LiveDataClient Usage Guide

This document explains how to use the shared `LiveDataClient` backend in `Aarohi-Core` for:

- live read streaming from `CommunicationService`
- register writes
- multi-device usage
- lifecycle management in desktop apps

The implementation lives in:

- `Aarohi.Core.DeviceManager.LiveDataClient`
- `Aarohi.Core.DeviceManager.DeviceValuesReceivedEventArgs`
- `Aarohi.Core.DeviceManager.CommunicationServiceRegisterResult`

File:

- `Aarohi\Core\DeviceManager\LiveDataClient.cs`

## 1. What This Backend Does

`LiveDataClient` is a persistent named-pipe client for `CommunicationService`.

It gives you one backend object that can:

- connect to the named pipe
- register a device and its profile with the service
- receive live values continuously through the `ValuesReceived` event
- write a value to a writable register
- update polling rate
- unregister devices

This is backend-only. It does not depend on any UI code.

## 2. Architecture

The runtime flow is:

1. Your app creates one `LiveDataClient`.
2. Your app subscribes to `ValuesReceived`.
3. Your app sends `RegisterDeviceAsync(device, profile)`.
4. `CommunicationService` stores that registration in memory.
5. `CommunicationService` starts polling the device in the background.
6. The service pushes `DeviceValues` messages back through the pipe.
7. `LiveDataClient` raises `ValuesReceived`.
8. Your app handles the values and updates its own state/UI.
9. When needed, your app calls `WriteRegisterAsync(...)`.

Important: registration is held in service memory only. If `CommunicationService` restarts, you must register the devices again.

## 3. Project / Reference Requirements

To use this backend in another app:

1. Reference `Aarohi.csproj` or `Aarohi.dll`.
2. Import:

```csharp
using Aarohi.Core.DeviceManager;
using Aarohi.Core.DeviceManager.Models;
```

Current target notes:

- `Aarohi.csproj` targets `net9.0-windows`
- `Aarohi.csproj` has `UseWindowsForms=true`

That means the consuming app should also be a Windows-targeted .NET app.

## 4. Service Requirement

`LiveDataClient` talks to a named pipe:

- default pipe name: `CommunicationServicePipe`

If you use the default constructor:

```csharp
var client = new LiveDataClient();
```

it will try to connect to `CommunicationServicePipe`.

If your service uses a different pipe name:

```csharp
var client = new LiveDataClient("MyCustomPipe");
```

## 5. Public API Summary

Main members:

```csharp
public sealed class LiveDataClient : IDisposable
{
    public const string DefaultPipeName = "CommunicationServicePipe";
    public bool IsConnected { get; }
    public event EventHandler<DeviceValuesReceivedEventArgs>? ValuesReceived;

    public Task EnsureConnectedAsync(CancellationToken cancellationToken = default);

    public Task<CommunicationServiceRegisterResult> RegisterDeviceAsync(
        DeviceInstance device,
        DeviceProfile profile,
        int? updateRateMs = null,
        CancellationToken cancellationToken = default);

    public Task<CommunicationServiceRegisterResult> SetUpdateRateAsync(
        Guid deviceId,
        int updateRateMs,
        CancellationToken cancellationToken = default);

    public Task<CommunicationServiceRegisterResult> UnregisterDeviceAsync(
        Guid deviceId,
        CancellationToken cancellationToken = default);

    public Task<CommunicationServiceRegisterResult> WriteRegisterAsync(
        Guid deviceId,
        string registerName,
        string? value,
        CancellationToken cancellationToken = default);

    public Task<CommunicationServiceRegisterResult> WriteRegisterAsync(
        Guid deviceId,
        string registerName,
        object? value,
        CancellationToken cancellationToken = default);

    public Task<CommunicationServiceRegisterResult> WriteRegisterByAddressAsync(
        Guid deviceId,
        int address,
        string? value,
        CancellationToken cancellationToken = default);

    public Task<CommunicationServiceRegisterResult> WriteRegisterByAddressAsync(
        Guid deviceId,
        int address,
        object? value,
        CancellationToken cancellationToken = default);

    public Task DisconnectAsync();
    public void Dispose();
}
```

Read event payload:

```csharp
public sealed class DeviceValuesReceivedEventArgs : EventArgs
{
    public Guid DeviceId { get; init; }
    public string DeviceName { get; init; }
    public DateTime TimeStamp { get; init; }
    public IReadOnlyList<DeviceRegisterValue> Values { get; init; }
}
```

Command result:

```csharp
public sealed class CommunicationServiceRegisterResult
{
    public bool Success { get; init; }
    public string Message { get; init; }
    public string CorrelationId { get; init; }
}
```

## 6. Critical Model Requirements Before Registering

Before calling `RegisterDeviceAsync`, make sure the data you pass is valid.

### 6.1 DeviceInstance

At minimum:

- `device.Id` should be a stable non-empty `Guid`
- `device.Name` or `device.TagName` should be set
- `device.Communication` must be configured

Strong recommendation:

```csharp
device.Id = Guid.NewGuid();
```

Do not leave `device.Id` empty.

Why this matters:

- if `device.Id == Guid.Empty`, the service may generate a new ID internally
- your app will not automatically receive that generated ID back
- later calls like `WriteRegisterAsync(device.Id, ...)` or `UnregisterDeviceAsync(device.Id)` may fail if your local object still has `Guid.Empty`

So always assign the device ID yourself before registration.

### 6.2 DeviceProfile

At minimum:

- `profile.Name` should be set
- `profile.Registers` must contain the tags/registers to read/write

### 6.3 RegisterDefinition

For each register you want to use, make sure these are meaningful:

- `RegisterName`
- `DisplayName`
- `RegisterArea`
- `Address` and/or `StartAddress`
- `DataType`
- `RegisterCount` for multi-word values
- `Access`
- `Type`

Write behavior depends on `Access` and `Type`.

Write is valid only when:

- `Access == RegisterAccess.ReadWrite`, or
- `Access == RegisterAccess.WriteOnly`

Write is rejected when:

- `Access == RegisterAccess.ReadOnly`
- `Type == RegisterDefinitionType.VMreg`


For Siemens S7 writes, the client now sends the requested value as a string to the service. The service converts that string using the target register's `DataType`, `Scale`, and `Offset`, so calls like `WriteRegisterAsync(deviceId, "Start", "true")`, `WriteRegisterAsync(deviceId, "Speed", "1450")`, and `WriteRegisterAsync(deviceId, "BatchName", "ABC")` all follow the register definition.

## 7. Minimal Single-Device Read Example

This is the simplest real usage pattern.

```csharp
using Aarohi.Core.DeviceManager;
using Aarohi.Core.DeviceManager.Models;

var profile = new DeviceProfile
{
    Id = Guid.NewGuid(),
    Name = "Flow Meter Profile",
    Registers = new List<RegisterDefinition>
    {
        new()
        {
            Id = Guid.NewGuid(),
            RegisterName = "FlowRate",
            DisplayName = "Flow Rate",
            RegisterArea = RegisterArea.InputRegister,
            Address = 30001,
            StartAddress = 0,
            DataType = RegisterDataType.Float,
            RegisterCount = 2,
            Access = RegisterAccess.ReadOnly,
            Type = RegisterDefinitionType.ModbusRegister,
            Unit = "m3/hr"
        },
        new()
        {
            Id = Guid.NewGuid(),
            RegisterName = "SetPoint",
            DisplayName = "Set Point",
            RegisterArea = RegisterArea.HoldingRegister,
            Address = 40001,
            StartAddress = 0,
            DataType = RegisterDataType.Float,
            RegisterCount = 2,
            Access = RegisterAccess.ReadWrite,
            Type = RegisterDefinitionType.ModbusRegister
        }
    }
};

var device = new DeviceInstance
{
    Id = Guid.NewGuid(),
    Name = "Flow Meter 1",
    TagName = "FM-01",
    ProfileId = profile.Id,
    ProfileName = profile.Name,
    Communication = new CommunicationSettings
    {
        Mode = CommunicationMode.ModbusTcpIp,
        TcpIp = new TcpIpSettings
        {
            IpAddress = "192.168.0.50",
            Port = 502
        },
        Modbus = new ModbusCommonSettings
        {
            SlaveId = 1,
            PollingIntervalMs = 1000,
            TimeoutMs = 1000,
            RetryCount = 1
        }
    }
};

var client = new LiveDataClient();

client.ValuesReceived += (sender, e) =>
{
    Console.WriteLine($"[{e.TimeStamp:HH:mm:ss}] Device: {e.DeviceName} ({e.DeviceId})");

    foreach (DeviceRegisterValue value in e.Values)
    {
        Console.WriteLine(
            $"  {value.RegisterName} | Parsed={value.ParsedValue} | " +
            $"Quality={value.Quality} | CommOk={value.IsCommunicationOk}");
    }
};

CommunicationServiceRegisterResult registerResult =
    await client.RegisterDeviceAsync(device, profile, updateRateMs: 1000);

if (!registerResult.Success)
    throw new Exception(registerResult.Message);

Console.WriteLine("Device registered. Live values will now arrive through ValuesReceived.");
```

## 8. How Reading Works

You do not call a `ReadAsync` method repeatedly.

Instead:

- register the device once
- keep the `LiveDataClient` alive
- handle `ValuesReceived`

Each event contains:

- one device identity
- one timestamp
- a batch of register values for that device poll cycle

This means:

- `ValuesReceived` is the live stream
- your app is responsible for storing the latest values if needed
- your app can build caches like `Dictionary<Guid, Dictionary<string, DeviceRegisterValue>>`

## 9. How to Write a Value

Once a device is already registered, write by:

- `deviceId`
- `registerName`
- `value`

Example:

```csharp
CommunicationServiceRegisterResult writeResult =
    await client.WriteRegisterAsync(device.Id, "SetPoint", 12.5);

if (!writeResult.Success)
{
    Console.WriteLine(writeResult.Message);
}
else
{
    Console.WriteLine(writeResult.Message);
}
```

Examples of values you may pass:

- `true` for coil / bool
- `10` for integer
- `12.5` for float/double
- `"ABC123"` for string registers

The communication service converts the value according to the register data type.

Important:

- the register must exist in the registered profile
- the register should be identified by its `RegisterName`
- the register must be writable

Best practice:

- pass `RegisterName`, not `DisplayName`
- treat `RegisterName` as the stable backend key

## 10. Current Write Limitation

The service can resolve a write by:

- `registerName`
- or register `address`

But the current shared `LiveDataClient` exposes only:

```csharp
WriteRegisterAsync(Guid deviceId, string registerName, object? value)
```

So today the shared library write path is register-name based.

If another app needs address-based writes, add an overload later.

## 11. Multi-Device Usage

Yes, multi-device is supported.

Use one `LiveDataClient` and register multiple devices on that same client.

Example:

```csharp
var client = new LiveDataClient();

client.ValuesReceived += (sender, e) =>
{
    Console.WriteLine($"Device event from {e.DeviceName} ({e.DeviceId})");
};

foreach ((DeviceInstance device, DeviceProfile profile) item in devices)
{
    CommunicationServiceRegisterResult result =
        await client.RegisterDeviceAsync(item.device, item.profile);

    if (!result.Success)
        Console.WriteLine($"{item.device.Name}: {result.Message}");
}
```

How this works internally:

- `CommunicationService` stores runtimes by `DeviceId`
- each registered device gets its own polling loop
- each pushed event identifies the source device

Best practice for multi-device apps:

- keep a `Dictionary<Guid, DeviceInstance>` or `Dictionary<Guid, string>`
- route each incoming event by `e.DeviceId`
- store latest values per device in your own cache

Example cache:

```csharp
var latestValues = new Dictionary<Guid, Dictionary<string, DeviceRegisterValue>>();

client.ValuesReceived += (sender, e) =>
{
    if (!latestValues.TryGetValue(e.DeviceId, out var deviceMap))
    {
        deviceMap = new Dictionary<string, DeviceRegisterValue>(StringComparer.OrdinalIgnoreCase);
        latestValues[e.DeviceId] = deviceMap;
    }

    foreach (DeviceRegisterValue value in e.Values)
        deviceMap[value.RegisterName] = value;
};
```

## 12. Update Rate

You can change polling rate after registration:

```csharp
CommunicationServiceRegisterResult rateResult =
    await client.SetUpdateRateAsync(device.Id, 500);
```

This updates the polling interval for that registered device in the service.

## 13. Unregister and Cleanup

When your app no longer needs a device:

```csharp
await client.UnregisterDeviceAsync(device.Id);
```

When your app shuts down:

```csharp
await client.DisconnectAsync();
client.Dispose();
```

Recommended shutdown order for multi-device apps:

1. unregister devices you registered
2. disconnect
3. dispose

## 14. Recommended App Lifecycle Pattern

This is the safest general pattern:

1. Create one `LiveDataClient` for the app/session.
2. Subscribe to `ValuesReceived`.
3. Register one or more devices.
4. Keep the client alive for the whole working session.
5. Write values when needed.
6. Update rates when needed.
7. Unregister devices on close.
8. Dispose the client on app shutdown.

Do not create one `LiveDataClient` per device unless you have a strong reason. One shared client is the intended usage pattern.

## 15. WinForms / WPF Threading Notes

`ValuesReceived` is raised from backend read-loop work, not from your UI thread.

That means:

- in WinForms, use `Invoke` / `BeginInvoke`
- in WPF, use `Dispatcher.Invoke` / `Dispatcher.BeginInvoke`
- do not touch UI controls directly inside the raw event handler unless you have marshaled to the UI thread

WinForms example:

```csharp
client.ValuesReceived += (sender, e) =>
{
    if (InvokeRequired)
    {
        BeginInvoke(new Action(() => HandleDeviceValues(e)));
        return;
    }

    HandleDeviceValues(e);
};
```

## 16. Connection and Timeout Behavior

Current behavior in `LiveDataClient`:

- pipe connect timeout: 5 seconds
- register/update/unregister/write command timeout: 10 seconds

Typical failure cases:

- `CommunicationService` is not running
- wrong pipe name
- invalid device/profile data
- writing to a read-only or virtual register
- service restart after registration

If `CommunicationService` restarts:

- the client may reconnect later
- but the service runtime registry is lost
- you must register all devices again

## 17. Common Errors and Fixes

### Error: could not connect to the communication service pipe

Meaning:

- the pipe is not available
- the service is not running
- or pipe name does not match

Fix:

- start `CommunicationService`
- confirm the pipe name
- call `new LiveDataClient("CorrectPipeName")` if needed

### Error: register write failed / device was not found

Meaning:

- the device was never registered
- the service restarted and lost registration
- or you are using the wrong `deviceId`

Fix:

- make sure `device.Id` is set before registration
- register the device again

### Error: register was not found

Meaning:

- the `registerName` does not match a register from the registered profile

Fix:

- use the exact `RegisterName`
- do not rely on human display text as the backend key

### Error: register is read-only

Meaning:

- `Access` is not writable

Fix:

- only write registers with `ReadWrite` or `WriteOnly`

### Error: virtual register cannot be written

Meaning:

- the register type is `VMreg`

Fix:

- write only real device registers, not calculated virtual tags

## 18. Best Practices

- Always assign `device.Id` before registration.
- Use one `LiveDataClient` per app/session.
- Treat `RegisterName` as the backend key.
- Keep your own latest-value cache in the app.
- Re-register all devices after service restart.
- Marshal read events to the UI thread in desktop apps.
- Keep the `ValuesReceived` handler lightweight.
- Unregister devices on shutdown.

## 19. Current Scope of the Shared Backend

The shared backend currently covers:

- pipe connection management
- device registration
- live read event streaming
- write by register name
- update rate
- unregister

It does not currently provide:

- UI helpers
- automatic latest-value cache
- automatic re-registration after service restart
- address-based write overload
- a higher-level multi-device session manager

Those can be added later if needed.

## 20. Short Practical Recipe

If you just want the shortest working mental model:

1. Build a valid `DeviceInstance`.
2. Build or load a valid `DeviceProfile`.
3. Make sure `device.Id` is not empty.
4. Create one `LiveDataClient`.
5. Subscribe to `ValuesReceived`.
6. Call `RegisterDeviceAsync(device, profile)`.
7. Read values from the event.
8. Call `WriteRegisterAsync(device.Id, "RegisterName", value)` when needed.
9. Call `UnregisterDeviceAsync(device.Id)` and `Dispose()` on shutdown.
