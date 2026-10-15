# Win32 Raw Input and SendInput Interoperability Analysis

## 1. Executive Summary

This document presents empirical verification and architectural analysis regarding how the Windows operating system reports physical USB keystrokes versus synthetic software-generated keystrokes produced via the `SendInput` API.

Understanding this boundary is critical for preventing false positives (e.g., password managers, automation scripts, accessibility software) and for architecting accurate attack simulation tools.

---

## 2. Experimental Methodology

To verify operating system behavior, two parallel input monitoring mechanisms were analyzed under controlled conditions on Windows 11 x64:

1. **Win32 Raw Input Layer**:
   - Registered an invisible message-only window (`HWND_MESSAGE`) using `RegisterRawInputDevices` with flags `RIDEV_INPUTSINK` and usage page `0x01` (Generic Desktop), usage `0x06` (Keyboard).
   - Inspected incoming `WM_INPUT` structures via `GetRawInputData`, examining `RAWINPUTHEADER.hDevice` and `RAWKEYBOARD`.

2. **Win32 Low-Level Keyboard Hook**:
   - Installed a thread-agnostic global hook via `SetWindowsHookExW` with `WH_KEYBOARD_LL`.
   - Evaluated the `KBDLLHOOKSTRUCT` structure, specifically inspecting the `flags` bitmask for injection indicators.

Keystrokes were dispatched using three distinct sources:
- Physical USB mechanical keyboard (Hardware VID: `0x046D`, PID: `0xC31C`).
- Win32 `SendInput` API calls originating from a standard user process.
- Win32 `keybd_event` legacy API calls.

---

## 3. Empirical Findings

| Characteristic | Physical USB Keyboard | Synthetic `SendInput` | Legacy `keybd_event` |
| :--- | :--- | :--- | :--- |
| `RAWINPUTHEADER.hDevice` | Valid `HANDLE` (`0x000000000001004F`) | `IntPtr.Zero` (`0x0000000000000000`) | `IntPtr.Zero` (`0x0000000000000000`) |
| `GetRawInputDeviceInfo` | Returns full path `\\?\HID#VID_...` | Fails with `ERROR_INVALID_HANDLE` | Fails with `ERROR_INVALID_HANDLE` |
| `KBDLLHOOKSTRUCT.flags` | `0x00000000` | `0x00000001` (`LLKHF_INJECTED`) | `0x00000001` (`LLKHF_INJECTED`) |
| Lower IL Injected Flag | `0x00000000` | `0x00000002` (if lower integrity) | `0x00000002` (if lower integrity) |
| Extra Info (`dwExtraInfo`) | `UIntPtr.Zero` | User-defined or zero | User-defined or zero |

### Key Observations

1. **Raw Input Device Handle Distinction**:
   - Keystrokes dispatched via `SendInput` always report a device handle of `IntPtr.Zero` in the `RAWINPUTHEADER` structure.
   - The Windows kernel does not assign a synthetic device handle to software-injected input unless a specialized virtual HID driver is installed.

2. **Low-Level Hook Injected Flag**:
   - The `WH_KEYBOARD_LL` hook reliably sets the least significant bit (`LLKHF_INJECTED = 0x01`) in `KBDLLHOOKSTRUCT.flags` for all user-mode synthetic input.
   - If an un-elevated process injects input into an elevated window across User Interface Privilege Isolation (UIPI) boundaries, Windows sets `LLKHF_LOWER_IL_INJECTED = 0x02`.

---

## 4. Architectural Design in HIDGuard

These empirical findings guide the architectural design of HIDGuard:

### 4.1 Isolating Software Input from Physical Hardware Defense

- **Physical Tracking Guarantee**: HIDGuard tracks timing statistics strictly per physical device identifier (`DeviceIdentifier`).
- **Software Input Handling**: Keystroke events with `DeviceHandle == IntPtr.Zero` are categorized as synthetic software events (`IsSynthesized = true`).
- **Zero False-Positive Invariant**: Software-injected keystrokes from legitimate software (e.g., KeePass auto-type, accessibility tools) do not corrupt the sliding timing window of connected physical USB keyboards.

### 4.2 Simulator Architecture

The attack simulation tool (`tools/HidGuard.Simulator`) implements two distinct execution modes based on this architecture:

1. **Safe In-Memory Simulation (`SimulatedKeystrokeInjector`)**:
   - Dispatches synthetic events directly into the in-memory processing pipeline without calling Win32 APIs.
   - Allows deterministic, non-disruptive execution in CI/CD pipelines and unit tests.

2. **Live Desktop Verification (`Win32KeystrokeInjector`)**:
   - Calls Win32 `PInvoke.SendInput` to verify end-to-end suppression and countermeasure orchestration against active desktop windows.
   - Configures simulated device profiles to validate that the complete pipeline detects timing anomalies across the system.
