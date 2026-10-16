# HIDGuard

Defensive Keystroke Injection & BadUSB Mitigation System for Windows

[English](README.md) | [Türkçe](README.tr.md)

---

## 1. Overview and Threat Model

Traditional endpoint security and antivirus solutions trust USB devices by default upon enumeration. Operating systems lack inherent mechanisms to distinguish between physical human typing on a legitimate keyboard and microcontrollers masquerading as Standard USB Human Interface Devices (HID).

Attack hardware such as the **Hak5 USB Rubber Ducky**, **Digispark**, **Teensy**, **Flipper Zero**, and **Raspberry Pi Pico** exploit this trust. Upon physical insertion, these devices enumerate as compliant USB keyboards and immediately transmit pre-programmed keystrokes at hardware speeds (sub-10 millisecond intervals), opening command prompts or run dialogs to execute malicious code in under 500 milliseconds.

### The Defensive Approach

HIDGuard protects Windows endpoints not through static file signatures, but by analyzing the **biomechanical and temporal properties** of incoming keystrokes:

1. **Physiological Speed Floor**: Human fingers have structural mechanical limits; sustained typing below 25-30 milliseconds per stroke is biologically impossible.
2. **Natural Human Jitter**: Human typing inherently produces variable inter-keystroke intervals (standard deviations typically between 25 ms and 70 ms).
3. **Machine Uniformity Anomaly**: Microcontroller firmware loops deliver keystrokes with ultra-low variance (standard deviations < 1.5 ms), a distinct signature of synthetic injection.
4. **Hardware Identity Validation**: Unrecognized hardware devices are subjected to strict policy evaluation.
5. **Active Multi-Tier Countermeasures**: Upon anomaly detection, HIDGuard suppresses malicious keystrokes in flight via a low-level keyboard hook (`WH_KEYBOARD_LL`), locks the user workstation (`LockWorkStation`), and optionally disables the offensive device at the Windows device-node level (`CM_Disable_DevNode`).

---

## 2. Architecture and Data Flow

HIDGuard follows Clean Architecture principles, ensuring complete decoupling between domain policies, platform interop, user interfaces, and attack simulation tools.

```
+-------------------------------------------------------------+
|               Physical & Synthetic Keystrokes               |
+-------------------------------------------------------------+
                              |
                              v
+-------------------------------------------------------------+
| HidGuard.Platform.Windows (Win32 Interop Layer)             |
| - Message-only HWND Window                                  |
| - RegisterRawInputDevices (RIDEV_INPUTSINK)                 |
| - GetRawInputDeviceInfo Handle Resolution                   |
| - Win32 Keyboard Hook (WH_KEYBOARD_LL)                      |
| - Device Disabler (CM_Disable_DevNode)                      |
| - Workstation Locker (LockWorkStation)                      |
+-----------------------------+-------------------------------+
                              | KeystrokeEvent
                              v
+-------------------------------------------------------------+
| HidGuard.Core (Analysis Engine & Policy Matrix)             |
| - System.Threading.Channels Bounded Pipeline                |
| - KeystrokeTimingAnalyzer (Sliding Window & Welford WPM)    |
| - AnomalyScorer (Confidence Calculation)                    |
| - TrustStore (Persistent Device Enrollment)                 |
| - PolicyEngine (Decision Matrix & Anti-Lockout)             |
| - CountermeasureService (Multi-Tier Defense Orchestration)  |
| - FileAuditTrailService (Privacy-Safe Append-Only Audit)    |
| - IncidentPayloadBuffer (Opt-In RAM Ring Buffer)            |
| - IncidentMaintenanceService (Dynamic Config & Purge Loop)  |
| - EncryptedIncidentStore (DPAPI-Protected Incident Storage) |
+-----------------------------+-------------------------------+
                              | Pipeline Events
              +---------------+---------------+
              |                               |
              v                               v
+-----------------------------+ +-----------------------------+
| HidGuard.App (UI & Tray)    | | HidGuard.Simulator (Tooling)|
| - WPF Dark Theme Dashboard  | | - DuckyScript 1.0/2.0 Engine|
| - Single Instance Mutex     | | - Win32 SendInput Injector  |
| - UAC Elevation Management  | | - Burst & Jitter Profiles   |
| - NotifyIcon Tray Service   | | - E2E Verification Suite    |
+-----------------------------+ +-----------------------------+
```

---

## 3. Core Architectural Guarantees

### 3.1 Zero-Keylogging Privacy Invariant & Encrypted Incident Store
HIDGuard enforces an absolute guarantee regarding user privacy:
- The system **never** records, logs, stores, or transmits virtual key codes, scan codes, or typed alphanumeric characters during normal operation.
- Pipeline event models and audit trail entries (`AuditEvent`) record only non-sensitive metadata: hardware device identifiers (VID/PID), inter-keystroke intervals, anomaly confidence scores, action decisions, and timestamps.
- **Opt-in Incident Payload Capture**: If explicit opt-in payload capture is enabled in configuration (`EnableOptInPayloadCapture = true`), only keystrokes immediately triggering an active attack anomaly are captured from a bounded in-memory ring buffer (`IncidentPayloadBuffer`). Normal background typing is never recorded.
- **In-Memory Ring Buffer & Sanitization**: The buffer retains up to `CaptureWindowKeystrokes` (default: 64, max: 1024) in volatile memory. When an attack is flagged, the snapshot is extracted, the buffer is cleared, and plaintext bytes in managed memory are sanitized using `Array.Clear` immediately after DPAPI encryption.
- **DPAPI Encryption**: Stored payloads are encrypted using Windows Data Protection API (`DataProtectionScope.CurrentUser`) before disk writes, bounded by strict limits (`MaxPayloadBytes`, default: 4 KB; `MaxIncidentCount`, default: 1000) and automatic retention expiry (`RetentionDays`, default: 30 days).
- **Background Maintenance**: `IncidentMaintenanceService` runs an asynchronous background loop purging expired incidents on a configurable interval (`PurgeIntervalMinutes`, default: 60) and dynamically applies configuration changes at runtime without requiring application restarts.
- **Metadata-Only Export**: Incident reports exported for analysis (`ExportMetadataReportAsync`) output exclusively non-sensitive metadata in JSON or CSV format, stripping any raw or encrypted payload data to ensure safe operational sharing without risk of keystroke leakage.
- These invariants are enforced by automated unit test suites verifying reflection properties, DPAPI roundtrips, ring buffer eviction, and zero payload leakage in export formats.

### 3.2 Offline and Zero-Telemetry Operation
- HIDGuard runs entirely offline and contains zero external telemetry, tracking, or network communication code.
- Configuration and audit trails are persisted locally on the host machine.

### 3.3 Anti-Lockout Safeguard
- If multiple physical keyboards are connected, an offensive device can be disabled at the Windows device tree level.
- If only one keyboard is attached, HIDGuard strictly prevents hardware disabling, protecting administrators and users from permanent lockout while maintaining temporary keystroke suppression and workstation locking.

### 3.4 Non-Human Legitimate Input Handling
Legitimate Windows environments process various non-human and automated inputs that must not trigger defensive actions:
- **Clipboard Transfers (`Ctrl+V`, `Shift+Insert`)**: Text pasting operations transfer memory buffers via internal Win32 OLE messaging without emitting sequential keystroke events. HIDGuard observes only the trigger combination and produces zero anomaly alerts.
- **Password Managers**: Tools such as KeePass, 1Password, and Bitwarden utilize the `SendInput` API. The Windows kernel sets `hDevice == IntPtr.Zero` and flags `LLKHF_INJECTED` in low-level hooks. HIDGuard segregates synthetic software input from physical hardware timing windows.
- **Assistive Technologies and On-Screen Keyboard (`osk.exe`)**: Input dispatched by Windows accessibility components is detected as synthetic software input and excluded from physical USB hardware anomaly metrics.
- **Remote Desktop Sessions (RDP, Citrix, VNC)**: Remote desktop input arrives via virtual terminal keyboard drivers (`TS_KBD`), which are segregated under unique virtual device paths distinct from local USB root hubs.
- **High-Velocity Human Typists**: Fast human typing exhibits natural inter-keystroke variance (coefficient of variation exceeding 0.20) and never drops below the 25 ms physiological threshold across consecutive strokes. Multi-parameter scoring prevents false positives.

### 3.5 Honest Limitations and Known Evasion Techniques
In accordance with transparent defensive engineering principles, HIDGuard documents known limitations:
- **Randomized Jitter / Human Emulation**: Microcontrollers programmed to simulate biological human timing (mean ~120 ms, standard deviation ~35 ms) evade statistical timing heuristics. However, doing so expands attack execution duration from 350 milliseconds to 15-45 seconds, providing a realistic physical window for user detection and manual interruption.
- **Hardware Identity Spoofing (VID/PID Cloning)**: USB HID specifications lack cryptographic device attestation. An attacker with physical access to an authorized keyboard can clone its Vendor ID, Product ID, and serial strings. HIDGuard mitigates this by tracking full device path topologies where supported by Windows Raw Input.
- **Kernel-Level Input Injection (Ring 0)**: Rootkits and kernel drivers operating at Ring 0 (`win32kbase.sys`) bypass user-mode Raw Input and low-level keyboard hooks entirely. HIDGuard operates in user mode (Ring 3) and defends against rogue physical peripherals, not compromised OS kernels.
- **Single-Keyboard Anti-Lockout Trade-Off**: When only one physical keyboard is present on an endpoint, hardware disabling via `CM_Disable_DevNode` is suppressed to preserve administrative access. The system relies on temporary keystroke suppression and workstation locking.

---

## 4. Component Deep Dive

### 4.1 Win32 Raw Input Interoperability (`HidGuard.Platform.Windows`)
- **Message-Only Window**: Registers an invisible message-only window (`HWND_MESSAGE`) receiving `WM_INPUT` messages globally across all desktop sessions (`RIDEV_INPUTSINK`).
- **Hardware Identifier Resolution**: Resolves raw device handles (`HANDLE`) to hardware device paths (`\\?\HID#VID_xxxx&PID_xxxx...`) using `GetRawInputDeviceInfo`. Distinguishes software-injected synthetic input (`HANDLE.Zero`) from physical hardware controllers.
- **Active Keystroke Suppression**: Deploys an unmanaged low-level keyboard hook (`SetWindowsHookExW` with `WH_KEYBOARD_LL`) that selectively returns non-zero (`LRESULT(1)`) during active countermeasure periods, discarding synthetic keystrokes before they reach user applications.
- **Windows Event Log Integration**: Implements `WindowsEventLogService` publishing structured security events directly into the Windows Application Event Log under source `HIDGuard` (Event ID 1000 for lifecycle, 1001 for attack detected/workstation locked, 1002 for device disabled, 1003 for anti-lockout protection, 1004 for keystroke suppression, 1005 for device re-enabled).

### 4.2 Analysis and Policy Engine (`HidGuard.Core`)
- **Sliding Window Timing Analyzer**: Evaluates inter-keystroke intervals within a configurable sliding window (default: 30 strokes). Computes real-time mean interval, standard deviation, coefficient of variation, and words-per-minute metrics using Welford's algorithm.
- **Anomaly Scoring Engine**: Calculates a normalized confidence score (0.00 to 1.00) based on physiological threshold violations (< 25 ms interval), extreme typing velocity, and unnatural machine uniformity.
- **Persistent Trust Store**: Maintains hardware authorization records in local JSON storage (`trusted-devices.json`). Allows devices to be marked as `Trusted`, `Unknown`, or `Blocked`.
- **Policy Engine**: Evaluates the risk score against hardware trust levels and attached device counts to select appropriate countermeasures (`Allow`, `AlertOnly`, `LockWorkstation`, `BlockDevice`, `LockAndBlock`).
- **Incident Storage & Forensics Engine**: Manages encrypted incident records on disk with DPAPI protection, dynamic capacity enforcement, opt-in ring buffering, and scheduled maintenance purging.

### 4.3 WPF Dashboard and Tray Application (`HidGuard.App`)
- **Modern Dark Theme**: Styled with a dark-slate design system, responsive card layouts, and status badges.
- **System Tray Lifecycle**: Operates within the Windows taskbar notification area. Overrides window close events (`OnClosing`) to minimize to tray without terminating protection.
- **Single Instance Mutex**: Guarantees a single running instance per session via `Local\HIDGuard_Application_SingleInstance_Mutex`.
- **UAC Awareness**: Detects elevation status and displays an administrative shield badge when elevated operations are available.
- **Incident Forensics Controls**: Settings view includes a dedicated configuration card for incident store parameters (window size, retention, payload bounds). Audit Log view features one-click metadata report export in JSON or CSV format.

### 4.4 Attack Simulator and E2E Verification Suite (`HidGuard.Simulator`)
- **DuckyScript 1.0 / 2.0 Parser**: Parses Hak5 Rubber Ducky scripts supporting `STRING`, `STRINGLN`, `DELAY`, `DEFAULT_DELAY`, `REPEAT`, and complex modifiers (`GUI r`, `CTRL ALT DEL`, `ALT F4`).
- **Attack Profiles**:
  - `BurstAttackProfile`: High-velocity injection (< 10 ms interval).
  - `JitterAttackProfile`: Uniform microcontroller delay loops (15 ms +/- 0.1 ms).
  - `HumanTypingProfile`: Gaussian human typing emulation (130 ms mean, 35 ms std dev).
  - `DuckyScriptAttackProfile`: Full payload execution.
- **Automated Verification Runner**: Executes 5 full-pipeline security tests verifying burst attack neutralization, uniformity detection, human passthrough, DuckyScript suppression, and the privacy invariant.

---

## 5. Test Suite and Quality Metrics

All components are covered by unit and integration test suites using xUnit and Shouldly. The solution compiles with zero warnings under `TreatWarningsAsErrors`.

| Project | Test Count | Status | Scope |
| :--- | :--- | :--- | :--- |
| HidGuard.Core.Tests | 133 | Passed | Timing engine, anomaly scoring, trust store, policy matrix, pipeline orchestration, audit trail, encrypted incident store, ring buffer, maintenance service, event log orchestration |
| HidGuard.Platform.Windows.Tests | 31 | Passed | Raw Input resolution, low-level hook suppression, device disabler, workstation locker, Serilog logging factory, DPAPI data protection, Windows Event Log publisher |
| HidGuard.App.Tests | 25 | Passed | MVVM commands, single instance mutex, device management, metrics, settings, incident export, installer script verification, view navigation, hardware recovery and device re-enablement |
| HidGuard.Simulator.Tests | 28 | Passed | DuckyScript parser, token mapping, attack profiles, 5-scenario end-to-end verification suite |
| **Total Solution** | **217** | **100% Green** | Zero warnings, zero errors |

---

## 6. Building, Running, and Packaging

### 6.1 Prerequisites
- Windows 10 / 11 (x64)
- .NET 8 SDK (v8.0.x or higher)

### 6.2 Build Solution
```powershell
dotnet build HidGuard.sln --configuration Release
```

### 6.3 Run All Tests
```powershell
dotnet test HidGuard.sln --configuration Release
```

### 6.4 Launch the Dashboard Application
```powershell
dotnet run --project src/HidGuard.App/HidGuard.App.csproj --configuration Release
```

### 6.5 Run Attack Simulator and Verification Suite

Run the automated 5-scenario verification report:
```powershell
dotnet run --project tools/HidGuard.Simulator/HidGuard.Simulator.csproj -- --verify
```

Execute simulated attack profiles in safe dry-run mode:
```powershell
dotnet run --project tools/HidGuard.Simulator/HidGuard.Simulator.csproj -- --mode burst --dry-run
dotnet run --project tools/HidGuard.Simulator/HidGuard.Simulator.csproj -- --mode jitter --dry-run
dotnet run --project tools/HidGuard.Simulator/HidGuard.Simulator.csproj -- --mode human --dry-run
```

Launch the interactive terminal menu:
```powershell
dotnet run --project tools/HidGuard.Simulator/HidGuard.Simulator.csproj
```

### 6.6 Create Release Distribution Package
```powershell
powershell -ExecutionPolicy Bypass -File scripts/package.ps1 -Version 1.0.0
```

### 6.7 Build Windows Installer Package
```powershell
powershell -ExecutionPolicy Bypass -File scripts/build-installer.ps1 -Version 1.0.0
```

### 6.8 Configuration Management & Dynamic Hot-Reload
HIDGuard stores settings in `%LocalAppData%\HIDGuard\config.json`. The application employs an atomic write mechanism and a background `FileSystemWatcher` to validate and reload updated parameters at runtime without requiring application restarts. Detailed property descriptions and validation ranges are documented in the [Configuration Reference](docs/configuration.md).

---

## 7. Supporting Documentation and Policies

- [Threat Model, Limitations, and Evasion Analysis](docs/threat-model-and-limitations.md): Detailed evaluation of adversary capabilities, theoretical evasion strategies, and legitimate input segregation.
- [Win32 Raw Input and SendInput Interoperability](docs/raw-input-and-sendinput-interop.md): Empirical verification of Windows input reporting across physical hardware versus synthetic APIs.
- [Configuration Reference](docs/configuration.md): Complete specification of all `config.json` parameters, validation ranges, and operational impacts.
- [Inno Setup Installer Script](installer/HIDGuard.iss): Inno Setup configuration for Windows desktop shortcuts, startup registration, and uninstaller.
- [Contributing Guidelines](.github/CONTRIBUTING.md): Branching model, conventional commits, zero-comment and zero-emoji standards.
- [Security Policy](SECURITY.md): Threat model, privacy guarantees, vulnerability disclosure.
- [License](LICENSE): MIT License.
