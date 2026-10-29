---
title: Aarohi Dynamic Runtime Developer Settings Engine
tags: [aarohi-core, runtime-config, developer-settings, wpf, winforms, architecture, guide]
date: 2026-10-29
author: Aarohi Architecture Team
version: 1.0.14.77
---

# ⚙️ Aarohi Dynamic Runtime Developer Settings Engine

> [!NOTE]
> **Core Purpose:**  
> Eliminate the need to recompile, rebuild, or restart desktop applications (IPTS, IMTS, SmartAutoTester, Motor Analyzer, etc.) when tweaking engineering thresholds, curve tensions, timing offsets, or communication tolerances. Everything is managed dynamically in real time from persistent Windows Registry storage with in-memory caching and live event broadcasting.

---

## 🏗️ Architecture Overview

The system is partitioned into two layers across Aarohi standard libraries:

```
┌────────────────────────────────────────────────────────────────────────┐
│                        Consuming Application                           │
│              (IPTS, IMTS, SmartAutoTester, Motor Analyzer)             │
│                                                                        │
│   • Reads: DevConfig.GetDouble("CurveSmoothnessValue", 0.5)            │
│   • Subscribes: DevConfig.SettingChanged += OnSettingChanged          │
│   • Global Hotkey: CTRL + SHIFT + D (Gated to Dev@Aarohi)              │
│   • Settings Menu: Registry Settings Manager (Dev@Aarohi / Admin)      │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
          ┌─────────────────────────┴────────────────────────┐
          │                                                  │
          ▼                                                  ▼
┌───────────────────────────────────┐    ┌───────────────────────────────────┐
│     Aarohi.Wpf.Controls.Lib       │    │            Aarohi Core            │
│       (Aarohi.Wpf.Controls)       │    │             (Aarohi)              │
├───────────────────────────────────┤    ├───────────────────────────────────┤
│ • DeveloperSettingsWindow.xaml    │    │ • DevConfig (Static Facade)       │
│ • AddEditDevSettingWindow.xaml    │    │ • Concurrent In-Memory Cache      │
│ • WindowInteropHelper floating    │    │ • DeveloperSettingsRegistryService│
│   over WinForms ShowDialog modals │    │ • GlobalDevShortcutFilter (Filter)│
└───────────────────────────────────┘    │ • RegistryHelper (Scoped per App) │
                                         └───────────────────────────────────┘
                                                             │
                                                             ▼
                                                Windows Registry (HKCU)
                                 Software\Aarohi Embedded Systems Pvt Ltd\{AppName}\DeveloperSettingsManager
```

---

## 🔐 3-Tier Security & Access Hierarchy

To safeguard machine safety and field integrity, access is strictly governed:

| Security Tier | User Profile | Global Hotkey (`Ctrl+Shift+D`) | Settings Screen Menu | Typical Use Case |
| :--- | :--- | :---: | :---: | :--- |
| **Tier 1** | **`Dev@Aarohi`** | ✅ **Active Everywhere** *(even inside modal test screens)* | ✅ **Visible & Accessible** | Aarohi R&D developers on-site or during remote diagnosis. |
| **Tier 2** | **Admin / DevAccess** | ❌ *Disabled* | ✅ **Visible & Accessible** | Plant engineers & senior testing heads adjusting machine tolerances. |
| **Tier 3** | **Regular Operator** | ❌ *Disabled* | ❌ **Hidden & Blocked** *(even if normal Settings permission granted)* | Operators running day-to-day tests on the shop floor. |

---

## 🚀 How to Use in Any Aarohi Application (5 Simple Steps)

### Step 1: Install NuGet Packages

Add references in your `.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="Aarohi" Version="1.0.14.77" />
  <PackageReference Include="Aarohi.Wpf.Controls" Version="1.0.0.81" />
  <PackageReference Include="Aarohi.ExtendedUI" Version="1.0.3" />
</ItemGroup>
```

> [!TIP]
> Both packages are registered on our internal NuGet feed `D:\Nugets\` and department file share.

---

### Step 2: Initialize in `Program.cs` at Application Startup

Set your application name and configure the security delegates before starting your main loop:

```csharp
using Aarohi.Globals;
using Aarohi.Classes;
using Aarohi.Configuration.DeveloperSettings;
using AarohiWpfControls.Controls.DeveloperSettings;

[STAThread]
static void Main(string[] args)
{
    // 1. Identify application namespace
    AGLobals.ValueHolders.currentApp = AGLobals.Names.IPTS; // or "IMTS", "MotorAnalyzer", etc.
    DynamicClass.Soft_Name = AGLobals.Names.IPTS;

    // 2. Configure Developer Settings security & invocation delegates
    DevConfig.CurrentUserProvider = () => Globals.UserName;
    DevConfig.HasAdminOrDevAccessProvider = () => Globals.IsAdminOrDevUser();
    DevConfig.OpenSettingsWindowAction = () => DeveloperSettingsWindow.OpenOrActivate();

    // 3. Initialize engine (loads in-memory cache & hooks global Ctrl+Shift+D)
    DevConfig.Initialize(enableGlobalShortcut: true);

    ApplicationConfiguration.Initialize();
    Application.Run(new FormMain());
}
```

---

### Step 3: Consume Values Anywhere with Auto-Seeding (Fail-Safe)

Replace hardcoded constants or magic numbers with `DevConfig` typed getters.

#### Best Practice: Define in a Global Properties Class
```csharp
using Aarohi.Configuration.DeveloperSettings;

public static class Globals
{
    // Pump curve smoothness factor (ScottPlot spline tension)
    public static double CurveSmoothnessValue => 
        DevConfig.GetDouble("CurveSmoothnessValue", 0.5, "Pump Curve Smoothness Factor");

    // Timeout tolerance before declaring PLC packet loss (ms)
    public static int PlcTimeoutMs => 
        DevConfig.GetInt("PlcTimeoutMs", 1500, "PLC Communication Watchdog Timeout (ms)");

    // Enable experimental high-frequency logging
    public static bool EnableHighFreqLogging => 
        DevConfig.GetBool("EnableHighFreqLogging", false, "High-Frequency Raw Stream Logging");
}
```

> [!IMPORTANT]
> **Automatic Registry Self-Seeding:**  
> If the key does not yet exist in the Windows Registry when your app runs, `DevConfig` **automatically creates and seeds** it with the provided `defaultValue` and `displayName` silently! Developers never need to manually write registry migration scripts.

---

### Step 4: Hook Live Changes in Your Active Windows / Screens

When a developer changes a value in the floating settings window, your forms can update their visuals, formulas, or timers **instantly without restarting or reopening**:

```csharp
using Aarohi.Configuration.DeveloperSettings;

public partial class FormPumpTest : Form
{
    public FormPumpTest()
    {
        InitializeComponent();

        // 1. Subscribe to live configuration changes
        DevConfig.SettingChanged += OnDevSettingChanged;

        // 2. Unsubscribe on form close to prevent memory leaks
        FormClosed += (s, e) =>
        {
            DevConfig.SettingChanged -= OnDevSettingChanged;
        };
    }

    private void OnDevSettingChanged(object? sender, SettingChangedEventArgs e)
    {
        // 3. Check which setting was modified
        if (string.Equals(e.VariableName, "CurveSmoothnessValue", StringComparison.OrdinalIgnoreCase))
        {
            // 4. Safely dispatch to UI thread if modal or background thread
            if (IsHandleCreated && !IsDisposed)
            {
                BeginInvoke(new Action(() =>
                {
                    // Live re-render ScottPlot with the new tension value!
                    RefreshCurvePlot(Globals.CurveSmoothnessValue);
                }));
            }
        }
    }
}
```

---

### Step 5: Add Settings Menu Entry (Optional, for Non-Shortcut Users)

In your application's **Settings** or **Administration** user control:

```csharp
using Aarohi.Configuration.DeveloperSettings;
using AarohiWpfControls.Controls.DeveloperSettings;

// Render the action button only if user is Dev@Aarohi or Admin
if (DevConfig.CanAccessSettingsUI)
{
    AddActionRow(
        rowTag: "registry-developer-settings",
        title: "Registry Settings Manager",
        subTitle: "Developer-only configuration of runtime parameters.",
        click: (s, e) => DeveloperSettingsWindow.OpenOrActivate());
}
```

---

## 🔍 How It Works Inside IPTS (Live Real-World Pilot)

### Scenario: Adjusting Pump Curve Smoothness Live

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│ 1. User is running a live test inside FormTSDemoManager (ShowDialog modal).     │
│                                                                                 │
│ 2. Dev@Aarohi presses [CTRL + SHIFT + D].                                       │
│    -> GlobalDevShortcutFilter intercepts WM_KEYDOWN at the OS message loop.     │
│    -> Validates CurrentUserProvider() == "Dev@Aarohi".                          │
│    -> DeveloperSettingsWindow.OpenOrActivate() opens and floats ON TOP of       │
│       the modal test dialog (via WindowInteropHelper handle ownership).        │
│                                                                                 │
│ 3. Developer edits "CurveSmoothnessValue" from 0.50 -> 0.20 and clicks Save.    │
│    -> Value is serialized to Registry JSON:                                     │
│       {"DisplayName":"Pump Curve Smoothness Factor","Value":"0.2"}             │
│    -> In-memory cache is updated instantly.                                    │
│    -> DevConfig.NotifySettingChanged(...) fires SettingChanged event.          │
│                                                                                 │
│ 4. FormTSDemoManager receives SettingChanged event:                             │
│    -> Dispatches via BeginInvoke to UI message loop.                            │
│    -> Calls RefreshPumpCurveFromGrid(Globals.CurveSmoothnessValue).             │
│    -> ScottPlot re-renders the curve spline live before the user's eyes!        │
│    -> Derived graphs (Efficiency, Current, Power in NewFormTSDemoManager) also  │
│       re-render instantly!                                                      │
└─────────────────────────────────────────────────────────────────────────────────┘
```

---

## 📋 API Reference Quickcard

### `DevConfig` (Facade)

| Method / Property | Return Type | Description |
| :--- | :--- | :--- |
| `Initialize(bool enableGlobalShortcut, ISettingsStorageProvider?)` | `void` | Bootstraps engine, loads cache, and registers global filter. |
| `GetString(string varName, string defaultVal, string? dispName)` | `string` | Gets string; self-seeds registry if missing. |
| `GetDouble(string varName, double defaultVal, string? dispName)` | `double` | Culture-invariant float parser with fail-safe fallback. |
| `GetInt(string varName, int defaultVal, string? dispName)` | `int` | Invariant 32-bit int parser with fallback. |
| `GetBool(string varName, bool defaultVal, string? dispName)` | `bool` | Invariant boolean parser (supports true/false, 1/0). |
| `SetValue(string varName, string value, string? dispName)` | `bool` | Updates cache, persists to registry, fires `SettingChanged`. |
| `RefreshCache()` | `void` | Force re-reads all keys from storage into RAM cache. |
| `SettingChanged` | `event` | Fires whenever any setting is modified or deleted. |
| `CanUseDevShortcut` | `bool` | Returns `true` if current user is `Dev@Aarohi`. |
| `CanAccessSettingsUI` | `bool` | Returns `true` if `Dev@Aarohi` OR Admin/DevAccess. |
| `OpenOrActivateSettingsWindow()` | `void` | Invokes the configured UI launcher delegate. |

### `DeveloperSettingsWindow` (WPF UI)

| Method | Description |
| :--- | :--- |
| `DeveloperSettingsWindow.OpenOrActivate(Window? owner = null, IntPtr? parentHwnd = null)` | Single-instance launcher that anchors to the active WinForms window handle, bringing existing window to front or instantiating a new one. |

---

## 🛠️ Storage Details (Windows Registry Schema)

- **Root Location:** `HKEY_CURRENT_USER\Software\Aarohi Embedded Systems Pvt Ltd\{currentApp}\DeveloperSettingsManager`
- **Value Name:** `VariableName` (e.g. `CurveSmoothnessValue`, `PlcTimeoutMs`)
- **Value Type:** `REG_SZ` (String)
- **Value Data:** JSON object:
  ```json
  {
    "DisplayName": "Pump Curve Smoothness Factor",
    "Value": "0.5"
  }
  ```

> [!TIP]
> Because the path dynamically uses `AGLobals.ValueHolders.currentApp`, each Aarohi product (`IPTS`, `IMTS`, `SmartAutoTester`) automatically gets completely isolated settings in the registry with zero conflicts!
