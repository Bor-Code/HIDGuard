# Changelog

All notable changes to the HIDGuard project will be documented in this file.

The format is based on Keep a Changelog, and this project adheres to Semantic Versioning.

## [1.0.0] - 2026-10-17

### Added

- Defensive Keystroke Timing Engine (`HidGuard.Core`):
  - High-precision sliding window timing analyzer using Welford's algorithm for online mean and variance calculation.
  - Multi-parameter anomaly scoring evaluating physiological speed limits (inter-keystroke intervals < 25 ms), words-per-minute ceilings, and microcontroller delay loop uniformity.
  - Asynchronous bounded channel input pipeline (`System.Threading.Channels`) isolating Win32 Raw Input capture from timing analysis.

- Multi-Tier Active Countermeasures (`HidGuard.Platform.Windows`):
  - Low-level keyboard hook (`WH_KEYBOARD_LL`) suppressing synthetic keystrokes in flight before delivery to active applications.
  - Immediate workstation locking via `LockWorkStation` Win32 API.
  - Hardware device tree node disabling via PnP Configuration Manager API (`CM_Disable_DevNode`).
  - Single-keyboard anti-lockout safeguard preventing administrative lockout when only one physical keyboard is attached.

- Privacy Guarantees and Encrypted Incident Storage:
  - Zero-keylogger guarantee by design: standard audit events and telemetry streams never capture or persist key codes or alphanumeric characters.
  - Bounded volatile ring buffer (`IncidentPayloadBuffer`) capturing keystroke context strictly during active anomaly detection windows.
  - DPAPI encryption (`DataProtectionScope.CurrentUser`) safeguarding local incident storage (`EncryptedIncidentStore`) on disk.
  - Background maintenance service (`IncidentMaintenanceService`) managing incident retention and size boundaries.
  - Non-sensitive metadata report export functionality supporting JSON and CSV formats.

- Enterprise SOC and SIEM Integration:
  - Security event forwarder service (`ISecurityEventForwarder`) delivering security alerts in ArcSight Common Event Format (CEF) and RFC 5424 Syslog format.
  - Real-time transport support over UDP and TCP sockets with non-blocking resilience and comprehensive diagnostic logging.
  - Direct Windows Application Event Log integration under event source `HIDGuard` with dedicated event IDs for lifecycle and countermeasure actions.

- Real-Time Hardware Telemetry Visualizer (`HidGuard.App`):
  - Dedicated telemetry dashboard view displaying live inter-keystroke interval scatter graphs, moving averages, and anomaly threshold lines.
  - Statistical histogram visualizing interval frequency distributions with color-coded risk bands.
  - Live session metrics tracking peak typing speed, mean interval, jitter standard deviation, and anomaly count per hardware device.
  - Integrated Attack Simulator Studio (`SimulatorViewModel` and `SimulatorView`) enabling operators to configure, execute, and monitor BadUSB attack profiles, DuckyScript payloads, safe dry-run emulation, and 7-scenario E2E compliance verification with real-time console output.

- Advanced BadUSB Attack Simulator (`HidGuard.Simulator`):
  - Hak5 DuckyScript 1.0 and 2.0 parser and execution engine with dry-run capabilities.
  - Specialized attack profiles including high-velocity burst injection, ultra-low jitter machine uniformity, Gaussian human typing emulation, stealth micro-burst injection, and multi-stage exploit execution.
  - Automated 7-scenario end-to-end verification runner validating full-pipeline detection and suppression accuracy.

- Modern Windows Desktop Application and Packaging:
  - Dark-theme WPF desktop dashboard with system tray integration and single-instance mutex enforcement.
  - Dynamic configuration manager supporting runtime parameter updates via `FileSystemWatcher`.
  - Self-contained 64-bit release distribution packaging script (`scripts/package.ps1`).
  - Automated Inno Setup installer compilation script (`scripts/build-installer.ps1`) and modern installer specification (`installer/HIDGuard.iss`).
  - Fully automated GitHub Actions continuous integration and release workflows.
