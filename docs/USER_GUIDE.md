# HIDGuard User Guide

This guide explains what HIDGuard does, the exact threat it solves, and how to use every feature in the application in plain, practical terms.

---

## 1. What is HIDGuard and Why Do You Need It?

### The Problem: BadUSB & Keystroke Injection Attacks
When you plug a normal USB flash drive into your computer, Windows treats it as a storage device, and your antivirus scans the files on it. 

However, attackers use specialized microcontrollers disguised as ordinary USB drives, such as the **Hak5 USB Rubber Ducky**, **Flipper Zero**, **Digispark**, **Teensy**, or **Raspberry Pi Pico**.

When plugged into your machine, these devices tell Windows:
> "I am not a USB drive. I am a standard USB keyboard."

Because of standard USB specifications, Windows unconditionally trusts any connected keyboard without requiring special drivers or user permissions. The moment it connects, the microcontroller transmits pre-recorded malicious commands at hardware speed (hundreds of keystrokes per second):
- Opens the Run prompt (`Win + R`),
- Launches a hidden PowerShell console,
- Downloads a payload, dumps credentials, or opens a remote backdoor.

The entire attack is finished in **less than 500 milliseconds (half a second)**. Traditional antivirus and EDR solutions do not catch this because no untrusted executable file was inserted—to the operating system, it looks like a person typed on a keyboard very fast.

---

### The Solution: How HIDGuard Protects You
HIDGuard does not scan files. Instead, it continuously measures the **physical and biological rhythm** of every keystroke arriving at your machine:

1. **The Biological Floor**: Human fingers have mechanical limits. No human typist can sustain consecutive keystrokes under 20-25 milliseconds apart.
2. **Natural Human Rhythm (Jitter)**: Human typing is naturally irregular. Even fast typists pause between different letters (varying between 40 ms and 150 ms).
3. **Synthetic Machine Uniformity**: Attack devices send keystrokes with ultra-low, robotic variance (often exactly 5.0 ms per stroke).

The moment HIDGuard detects synthetic speed and machine-uniform rhythm, it triggers active defenses in microseconds:
- **Swallows Malicious Keystrokes**: Intercepts and drops keystrokes in flight before Windows or terminal applications can process them (`WH_KEYBOARD_LL`).
- **Locks the Workstation**: Immediately locks the Windows user session (`LockWorkStation`).
- **Disables the Rogue Hardware**: If multiple keyboards are attached, disables the attacking USB device at the Windows hardware tree level (`CM_Disable_DevNode`).

---

## 2. Walkthrough of the User Interface

HIDGuard features a dark cybersecurity dashboard with a sidebar menu and top navigation bar.

### Top Bar Controls
- **Quick Theme Switcher (Red / Green / Blue)**: Instantly switches the interface between Red-Black (Default), Green-Black, and Blue-Black themes without restarting the app.
- **PROTECTION ACTIVE Indicator**: Shows whether real-time background protection is actively monitoring keystrokes.
- **Pause / Enable Protection Button**: Temporarily pauses active interception when you want to conduct maintenance or manual testing.

---

### Tab 1: Overview
The primary command center:
- **Protection State Banner**: Confirms real-time pipeline status and UAC elevation level.
- **Live Counter Cards**:
  - *Processed Keystrokes*: Total keystrokes evaluated since launch.
  - *Suppressed Keystrokes*: Synthetic keystrokes successfully blocked during an attack.
  - *Incidents Detected*: Total attack attempts detected.
  - *Trusted Hardware*: Count of verified, enrolled keyboards.
- **Recent Defense Actions**: A chronological list of recent mitigations, showing timestamps, actions taken, and outcomes.

---

### Tab 2: Connected Devices
Displays all physical and virtual input hardware attached to the system:
- **Trust Badges**:
  - *Trusted*: Legitimate keyboards approved by you.
  - *Unverified*: Newly connected hardware that has not yet been audited.
  - *Blocked*: Hardware flagged during an attack and disabled.
- **Hardware Metadata**: Shows Hardware ID (VID/PID) and device path for audit verification.

---

### Tab 3: Live Metrics
Visualizes the mathematical timing engine while you type:
- **Inter-Keystroke Interval**: The exact milliseconds elapsed between your last two keypresses.
- **Typing Speed (WPM)**: Estimated words per minute calculated via real-time sliding windows.
- **Variance & Jitter (Coefficient of Variation)**: Measures typing rhythm. Humans produce natural variance; attack hardware produces near-zero variance.
- **Latency Histogram**: Distribution graph showing your typical keystroke timings.

---

### Tab 4: Attack Simulator & Research Studio
A safe laboratory environment that lets you test HIDGuard without buying expensive attack hardware:
- **Attack Profiles**: Choose from 8 pre-configured attack scripts (PowerShell Download Cradle, Mimikatz Memory Dump, UAC Bypass, Reverse Shell, Ransomware Simulation, etc.).
- **Safe Dry-Run Mode**: Safely tests the attack logic against HIDGuard without harming your machine or executing real malware.
- **Speed & Jitter Controls**: Adjust playback speed (WPM / CPM) and injection delay to observe how HIDGuard responds.
- **Play (Execute) Button**: Runs the simulated attack. You can watch the console output stream in real time and view the automated verification results table below.

---

### Tab 5: Audit Log
An immutable, forensically verifiable ledger:
- **Cryptographic Event Record**: Records every device authorization, suppression event, and policy decision with microsecond timestamps.
- **Export Report**: Export audit logs in JSON or CSV format for compliance, incident response, or SIEM ingestion.

---

### Tab 6: Settings
Configuration options for system administrators and power users:
- **Human Feasibility Floor (ms)**: Default 25 ms. Intervals faster than this threshold contribute to suspicion scores.
- **Suspicion Score Threshold**: The confidence score (0-100) required to trigger countermeasures (Default 65).
- **Countermeasures Checklist**: Toggle workstation locking, device disabling, and temporary keystroke swallowing.
- **Visual Themes**: Select between Red-Black, Green-Black, or Blue-Black themes.

---

## 3. Real-World Scenarios

### Scenario A: An Attacker Plugs an Evil USB Device into Your Machine
1. While you step away from your desk, someone plugs a BadUSB device into your PC.
2. The device begins typing at 300+ WPM to download malware.
3. HIDGuard detects the superhuman speed within the first 10-15 keystrokes:
   - Suppresses remaining keys (the Run prompt or terminal never receives the command).
   - Locks your Windows workstation.
   - Disables the rogue device.
4. The attack is thwarted before any code executes.

### Scenario B: You Plug in a New Legitimate Keyboard
1. When you connect a new keyboard, a non-intrusive prompt appears: *"New USB Keyboard Detected"*.
2. The prompt displays the device name and Hardware ID.
3. Because you connected the device yourself, click **"Trust Device"**.
4. The keyboard is permanently registered to your local trust database.

### Scenario C: Fast Typing, Copy-Paste, and Password Managers
- **Pasting Text (`Ctrl+V`)**: Windows handles clipboard pasting via internal memory buffers rather than individual keystrokes. HIDGuard sees only the trigger combo and never produces false alarms.
- **Fast Human Typists**: Even professional typists typing over 150 WPM exhibit natural human rhythm variation. HIDGuard's multi-factor scorer ensures legitimate typists are never blocked.
- **Password Managers (1Password, Bitwarden, KeePass)**: Software auto-typing utilities are tagged with the Windows `LLKHF_INJECTED` flag. HIDGuard recognizes software inputs and does not confuse them with rogue USB hardware.

---

## 4. Frequently Asked Questions (FAQ)

**Q: Does HIDGuard record my passwords, credit cards, or private messages?**  
**A**: Absolutely not. HIDGuard strictly enforces a **Zero-Keylogging Privacy Invariant**. During normal operation, key characters, scan codes, and text are never logged, stored in memory, or saved to disk. The engine only processes timing timestamps and device IDs.

**Q: What happens if an attack occurs and I only have one keyboard? Will I get locked out?**  
**A**: No. HIDGuard includes an **Anti-Lockout Safeguard**. If only a single keyboard is detected on the system, HIDGuard will suppress malicious keystrokes and lock the workstation, but will deliberately refuse to disable the physical device node, ensuring you can still log back in.

**Q: Does HIDGuard require an internet connection?**  
**A**: No. HIDGuard operates 100% offline. It contains zero telemetry, tracking code, or external network calls.

**Q: How do I launch the application?**  
**A**: Double-click `run-hidguard.bat` in the root folder, or directly run `publish\HidGuard.App.exe`.
