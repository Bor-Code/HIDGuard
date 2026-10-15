# Threat Model, Limitations, and Evasion Analysis

## 1. Threat Model

HIDGuard is engineered to defend Windows endpoints against automated keystroke injection hardware masquerading as standard Human Interface Devices (HID).

### 1.1 In-Scope Attack Vectors

1. **High-Velocity Microcontroller Injections**:
   - Hardware devices including Hak5 USB Rubber Ducky, Digispark (ATTiny85), Teensy, MalDuino, Flipper Zero (BadUSB applet), and Raspberry Pi Pico.
   - Payloads programmed to open run dialogs, command prompts, or administrative shells and execute malicious commands at speeds exceeding 150 words per minute or inter-keystroke intervals under 25 milliseconds.

2. **Low-Variance Firmware Loops**:
   - Injection firmware that transmits keystrokes inside tight microcontroller loops with virtually zero timing variance (inter-keystroke interval standard deviation under 1.5 milliseconds).

3. **Unauthorized Peripheral Insertion**:
   - Unknown physical USB keyboards attached to an endpoint during active desktop sessions, evaluated through the persistent Trust Store.

### 1.2 Out-of-Scope Threat Vectors

1. **Kernel-Level Input Injection (Ring 0)**:
   - Rootkits, filter drivers, or compromised kernel modules operating in kernel space (`win32kbase.sys`, `kbdclass.sys`). Such threats operate below user-mode Windows subsystem boundaries and bypass user-mode Raw Input (`WM_INPUT`) and low-level keyboard hooks (`WH_KEYBOARD_LL`).

2. **Compromised Firmware on Authorized Keyboards**:
   - Malicious firmware implants (e.g., hardware keyloggers or compromised internal keyboard controllers) residing on devices already explicitly enrolled as `Trusted` in the Trust Store.

3. **Prolonged Unsupervised Physical Access**:
   - An adversary possessing prolonged, unrestricted physical access to an unlocked workstation who manually types commands at biological human speeds.

---

## 2. Honest Limitations and Known Evasion Techniques

In accordance with rigorous defensive engineering principles, HIDGuard documents known architectural limitations and theoretical evasion avenues.

### 2.1 Randomized Jitter and Human Emulation Attacks

- **Mechanism**: An adversary can program injection firmware with randomized delays drawn from normal or uniform distributions (e.g., mean interval of 120 ms with a standard deviation of 35 ms), explicitly mimicking human biological typing rhythms.
- **Defensive Impact**: Such attacks evade pure timing-based anomaly detection algorithms because the statistical characteristics fall within normal human parameters.
- **Operational Trade-Off**: Evading timing heuristics forces the attack to execute at human speeds. A multi-stage payload that would execute in 350 milliseconds under standard injection requires 15 to 45 seconds under human-emulated timing. This drastically widens the physical attack window, allowing the logged-in user or nearby personnel to observe visible text rendering on the screen and physically intervene (e.g., unplugging the device or closing the window).

### 2.2 Hardware Identity Spoofing (VID/PID Cloning)

- **Mechanism**: The USB 2.0/3.0 specification does not mandate cryptographic attestation of device descriptors. A microcontroller can be programmed with the exact Vendor ID (VID), Product ID (PID), Manufacturer String, and Serial Number of a legitimate, authorized enterprise keyboard.
- **Defensive Impact**: If an adversary clones the hardware identifiers of an enrolled `Trusted` keyboard, the Trust Store treats the rogue device as authorized unless device-path topology checks detect a discrepancy.
- **Mitigation**: HIDGuard tracks full device paths (`\\?\HID#VID_xxxx&PID_xxxx...`) containing hub port indices where available from Windows Raw Input, rather than relying solely on raw VID/PID pairs. However, physical hardware spoofing remains an inherent limitation of the USB protocol until cryptographic device authentication (such as USB Type-C Authentication Specification) is universally adopted by peripheral manufacturers.

### 2.3 Single-Keyboard Anti-Lockout Safeguard

- **Mechanism**: When only one physical keyboard is detected on a workstation, HIDGuard strictly refuses to disable the device node via `CM_Disable_DevNode`.
- **Defensive Impact**: While malicious keystrokes are still intercepted and suppressed via `WH_KEYBOARD_LL` and the workstation is locked via `LockWorkStation`, the physical hardware node remains enabled.
- **Rationale**: Disabling the sole keyboard attached to a machine creates an unrecoverable administrative lockout, requiring a hard reboot or external management intervention. Preventing denial-of-service against the legitimate user is prioritized over permanent device disabling in single-device configurations.

---

## 3. Non-Human Legitimate Input Handling

Legitimate Windows desktop environments frequently encounter non-human or automated keystroke patterns that must not be falsely flagged or blocked.

### 3.1 Clipboard Pasting (`Ctrl+V`, `Shift+Insert`)

- Pasting large blocks of text does not generate individual keystrokes in Windows. The operating system handles clipboard transfers through internal OLE and Win32 messaging (`WM_PASTE`), transferring the entire text buffer directly into the target control's memory.
- HIDGuard observes only the single `Ctrl+V` key combination, producing zero inter-keystroke interval bursts and zero anomaly flags.

### 3.2 Password Manager Auto-Type

- Enterprise password managers (e.g., 1Password, KeePass, Bitwarden) inject credentials via the Win32 `SendInput` API.
- As documented in the Raw Input interoperability analysis, `SendInput` keystrokes carry a device handle of zero (`HANDLE.Zero`) and are flagged with `LLKHF_INJECTED` in low-level keyboard hooks.
- HIDGuard isolates physical USB hardware tracking from synthetic software-injected events, preventing false-positive blocking of authorized password managers.

### 3.3 Windows Accessibility and On-Screen Keyboard (`osk.exe`)

- Windows On-Screen Keyboard and assistive technologies generate simulated inputs via internal accessibility subsystems.
- These inputs are identified as software-generated and bypass physical device anomaly scoring pipelines.

### 3.4 Remote Desktop Protocol (RDP, Citrix, VNC)

- Keystrokes originating from remote desktop client sessions arrive through terminal services virtual keyboard drivers (e.g., `TS_KBD`).
- Remote sessions are registered under distinct virtual device paths, isolating remote administrative typing from local physical USB port monitoring.

### 3.5 High-Speed Human Typists

- Skilled typists, court stenographers, and competitive gamers can reach speeds of 120 to 160 words per minute during bursts of familiar text.
- Human biomechanics dictate that even during maximum-speed typing, inter-keystroke intervals retain natural variance (coefficient of variation consistently exceeds 0.20), and mechanical finger transitions never collapse to zero or sub-15-millisecond thresholds across consecutive keystrokes.
- HIDGuard's multi-factor scoring model combines interval velocity with variance and distribution uniformity, preventing false positives during legitimate high-speed human typing.
