# Security Policy

## 1. Security Overview and Architectural Guarantees

HIDGuard is a defensive host-security application designed to mitigate automated keystroke injection and BadUSB attacks on Windows operating systems. The software is built around strict security and privacy invariants:

### 1.1 Zero-Keylogging Invariant
HIDGuard strictly enforces an absolute privacy guarantee:
- The system never records, logs, stores, or transmits virtual key codes, scan codes, or typed alphanumeric characters.
- Event models in the pipeline and audit trail records (`AuditEvent`) contain only non-sensitive metadata: timestamps, hardware device identifiers (VID/PID), inter-keystroke intervals (in milliseconds), anomaly confidence scores, and countermeasure execution results.
- This invariant is verified via automated unit and integration tests asserting property reflection on all audit and event models.

### 1.2 Local-Only and Zero-Telemetry Invariant
- HIDGuard operates entirely offline and contains zero external telemetry, cloud analytics, or network communications.
- All trust databases, configuration files, and audit logs reside strictly on the local machine under protected user or local app-data paths.

### 1.3 Anti-Lockout Safeguard
- When multiple physical keyboards are present, malicious injection devices can be programmatically disabled at the Windows device tree level via `CM_Disable_DevNode`.
- If only one physical keyboard is connected to the workstation, HIDGuard strictly refuses to disable the device, preventing permanent administrative lockout while still applying temporary input suppression and workstation locking.

---

## 2. Threat Model

HIDGuard is engineered to defend against:
- Malicious USB HID devices (USB Rubber Ducky, Digispark, Teensy, MalDuino, Flipper Zero, Raspberry Pi Pico) delivering high-speed keystroke injection payloads.
- Microcontroller-timed keystroke sequences that exhibit unnaturally uniform inter-key timing distributions (< 1.5 ms standard deviation).
- High-velocity burst attacks exceeding physiological human typing capabilities (> 180 words per minute or inter-keystroke intervals < 20 ms).

Out of Scope:
- Kernel-level keyloggers or rootkits operating with kernel privileges (`ring 0`).
- Compromised firmware of genuine, authorized hardware keyboards.
- Physical attacks where an unauthorized party has prolonged unrestricted manual access to an unlocked physical keyboard.

---

## 3. Supported Versions

Security updates and patches are provided for the following releases:

| Version | Supported |
| :--- | :--- |
| 1.x.x | Yes |
| < 1.0.0 | No |

---

## 4. Reporting a Vulnerability

If you discover a security vulnerability or weakness within HIDGuard, please report it responsibly:

1. **Do not create a public issue**: Security vulnerabilities must not be disclosed publicly until a patch or mitigation is available.
2. **Contact**: Open a GitHub Security Advisory directly in the repository via `Security -> Advisories -> Report a vulnerability`.
3. **Include details**: Provide a comprehensive description of the vulnerability, reproduction steps, attack payload examples, affected operating system versions, and any suggested mitigations.
4. **Response timeline**: You will receive an initial response acknowledging receipt within 48 hours, followed by regular status updates as an assessment and resolution are prepared.
