# HIDGuard Configuration Reference

## 1. Overview and Storage

HIDGuard maintains its operational parameters in a structured JSON configuration file located at:

```
%LocalAppData%\HIDGuard\config.json
```

If the file does not exist upon application launch, HIDGuard writes a default configuration file with safe defensive parameters and loads it.

### 1.1 Atomic Persistence and Validation

- Configuration writes are executed atomically using temporary file creation and replacement (`File.Replace`), preventing file corruption in the event of unexpected process termination or power loss.
- Any modification to `config.json` is validated against schema boundaries before application. If an invalid value is supplied, the configuration loader rejects the change, preserves running parameters, and logs a structured error.

### 1.2 Dynamic Runtime Hot-Reload

- HIDGuard observes `config.json` via a background `FileSystemWatcher`.
- When changes are committed to the file on disk (or updated via the Settings view in the WPF Dashboard), `IConfigurationManager.ConfigurationChanged` triggers runtime updates across active services without requiring an application restart.

---

## 2. Configuration Schema

### 2.1 Timing Thresholds (`Timing`)

Controls the sliding window statistical analysis of keystroke intervals.

| Property | Type | Default | Valid Range | Description |
| :--- | :--- | :--- | :--- | :--- |
| `WindowSize` | integer | `30` | 5 - 500 | Number of recent keystrokes retained in the sliding timing window per device. |
| `HumanFeasibilityFloorMs` | double | `25.0` | 0.1 - 200.0 | Absolute physical speed floor in milliseconds. Consecutive intervals below this are flagged as physiologically impossible for humans. |
| `SubHumanRatioThreshold` | double | `0.35` | 0.01 - 1.0 | Fraction of intervals in the window below the feasibility floor required to trigger an anomaly. |
| `CoefficientOfVariationThreshold` | double | `0.18` | 0.01 - 1.0 | Ratio of standard deviation to mean. Values below this threshold indicate machine-uniform typing. |
| `MinimumIntervalThresholdMs` | double | `8.0` | 0.1 - `HumanFeasibilityFloorMs` | Boundary below which intervals trigger extreme velocity penalties. |
| `BurstLengthThreshold` | integer | `15` | 3 - 200 | Number of uninterrupted fast keystrokes considered a high-speed injection burst. |
| `BurstPauseFloorMs` | double | `70.0` | `HumanFeasibilityFloorMs` - 500.0 | Minimum pause length in milliseconds that separates distinct typing bursts. |
| `ObservationWindowKeyCount` | integer | `60` | 10 - 1000 | Number of keystrokes during which a newly connected unknown device remains under strict observation. |
| `ObservationWindowScoreMultiplier` | double | `1.25` | 1.0 - 3.0 | Multiplier applied to anomaly scores during the observation window. |
| `SuspicionScoreThreshold` | double | `65.0` | 0.1 - 100.0 | Composite score threshold (0-100) above which active countermeasures are triggered. |

### 2.2 Policy Options (`Policy`)

Governs device enrollment and enforcement decisions.

| Property | Type | Default | Allowed Values | Description |
| :--- | :--- | :--- | :--- | :--- |
| `DefaultUnknownDeviceAction` | string | `AllowWithObservation` | `AllowWithObservation`, `BlockUntilApproved`, `PromptUser` | Initial handling mode for unrecognized USB HID keyboards. |
| `DefaultAnomalyAction` | string | `LockWorkstation` | `AlertOnly`, `LockWorkstation`, `BlockDevice`, `LockAndBlock` | Countermeasure applied when an anomaly score exceeds `SuspicionScoreThreshold`. |
| `AutoEnrollFirstRun` | boolean | `true` | `true`, `false` | Automatically enrolls all keyboards present on system during the initial run as `Trusted`. |
| `EnforceMinimumOneActiveKeyboard` | boolean | `true` | `true`, `false` | Prevents hardware disabling if only one physical keyboard is attached, avoiding administrative lockout. |

### 2.3 Countermeasure Configuration (`Countermeasures`)

Controls specific mitigation actions executed upon attack detection.

| Property | Type | Default | Valid Range | Description |
| :--- | :--- | :--- | :--- | :--- |
| `LockWorkstationEnabled` | boolean | `true` | `true`, `false` | Calls Win32 `LockWorkStation` immediately upon attack detection. |
| `DeviceDisableEnabled` | boolean | `true` | `true`, `false` | Disables the offensive device node via Windows SetupAPI (`CM_Disable_DevNode`). Requires administrator privileges. |
| `KeySuppressionEnabled` | boolean | `true` | `true`, `false` | Suppresses keystrokes in flight via `WH_KEYBOARD_LL` low-level hook. |
| `TemporarySuppressionSeconds` | integer | `3` | 1 - 60 | Duration in seconds during which the low-level hook continues suppressing keystrokes following an attack. |

### 2.4 Incident Store Configuration (`Incidents`)

Governs the DPAPI-encrypted incident storage and forensic payload buffering.

| Property | Type | Default | Valid Range | Description |
| :--- | :--- | :--- | :--- | :--- |
| `EnableOptInPayloadCapture` | boolean | `false` | `true`, `false` | Explicit opt-in permission to buffer triggering keystrokes for forensic analysis. Must be explicitly enabled by administrator. |
| `CaptureWindowKeystrokes` | integer | `64` | 1 - 1024 | Capacity of volatile in-memory ring buffer holding trigger keystrokes per device. |
| `MaxIncidentCount` | integer | `1000` | > 0 | Maximum number of incident files retained on disk before oldest files are purged. |
| `RetentionDays` | integer | `30` | > 0 | Number of days incident files are retained before scheduled maintenance purges them. |
| `MaxPayloadBytes` | integer | `4096` | 1 - 65536 | Maximum allowable byte size of encrypted payload per incident record. |
| `PurgeIntervalMinutes` | integer | `60` | > 0 | Frequency of background maintenance cycle that sweeps expired incident records. |
| `StorageDirectory` | string | `""` | Valid directory path | Custom directory for encrypted incident records. When empty, defaults to `%LocalAppData%\HIDGuard\Incidents`. |

### 2.5 Logging Configuration (`Logging`)

Controls structured Serilog diagnostic log output.

| Property | Type | Default | Allowed Values | Description |
| :--- | :--- | :--- | :--- | :--- |
| `MinimumLevel` | string | `Information` | `Verbose`, `Debug`, `Information`, `Warning`, `Error`, `Fatal` | Minimum log severity level persisted to disk. |
| `LogDirectory` | string | `""` | Valid directory path | Directory for diagnostic logs. Defaults to `%LocalAppData%\HIDGuard\Logs`. |
| `RetainedFileCountLimit` | integer | `30` | 1 - 365 | Maximum number of rolling log files retained on disk. |
| `FileSizeLimitBytes` | integer | `52428800` | 1024 - 1073741824 | Maximum file size in bytes (50 MB default) before log roll occurs. |
| `EnableConsoleLogging` | boolean | `true` | `true`, `false` | Enables console output during command-line or simulator runs. |

### 2.6 User Interface Configuration (`Ui`)

Controls dashboard and tray application display options.

| Property | Type | Default | Allowed Values | Description |
| :--- | :--- | :--- | :--- | :--- |
| `StartMinimizedToTray` | boolean | `true` | `true`, `false` | Starts application hidden in the Windows notification tray without displaying the dashboard window. |
| `NotifyOnAllowedDevices` | boolean | `false` | `true`, `false` | Displays toast notification when a trusted device connects. |
| `NotifyOnBlockedDevices` | boolean | `true` | `true`, `false` | Displays warning notification when an unknown or blocked device is detected. |
| `Theme` | string | `Dark` | `Dark`, `Light`, `System` | UI color palette theme. |

---

## 3. Example `config.json`

```json
{
  "Timing": {
    "WindowSize": 30,
    "HumanFeasibilityFloorMs": 25.0,
    "SubHumanRatioThreshold": 0.35,
    "CoefficientOfVariationThreshold": 0.18,
    "MinimumIntervalThresholdMs": 8.0,
    "BurstLengthThreshold": 15,
    "BurstPauseFloorMs": 70.0,
    "ObservationWindowKeyCount": 60,
    "ObservationWindowScoreMultiplier": 1.25,
    "SuspicionScoreThreshold": 65.0
  },
  "Policy": {
    "DefaultUnknownDeviceAction": "AllowWithObservation",
    "DefaultAnomalyAction": "LockWorkstation",
    "AutoEnrollFirstRun": true,
    "EnforceMinimumOneActiveKeyboard": true
  },
  "Countermeasures": {
    "LockWorkstationEnabled": true,
    "DeviceDisableEnabled": true,
    "KeySuppressionEnabled": true,
    "TemporarySuppressionSeconds": 3
  },
  "Incidents": {
    "EnableOptInPayloadCapture": false,
    "CaptureWindowKeystrokes": 64,
    "MaxIncidentCount": 1000,
    "RetentionDays": 30,
    "MaxPayloadBytes": 4096,
    "PurgeIntervalMinutes": 60,
    "StorageDirectory": ""
  },
  "Logging": {
    "MinimumLevel": "Information",
    "LogDirectory": "",
    "RetainedFileCountLimit": 30,
    "FileSizeLimitBytes": 52428800,
    "EnableConsoleLogging": true
  },
  "Ui": {
    "StartMinimizedToTray": true,
    "NotifyOnAllowedDevices": false,
    "NotifyOnBlockedDevices": true,
    "Theme": "Dark"
  }
}
```
