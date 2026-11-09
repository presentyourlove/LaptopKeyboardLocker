# Security Policy & Privacy Transparency

## Zero Keystroke Logging Guarantee

**LaptopKeyboardLocker (`KeyLock.exe`)** uses the Windows Low-Level Keyboard Hook API (`WH_KEYBOARD_LL`) in **Mode 1** to block keyboard input when locked. We enforce strict architectural safeguards to protect user privacy:

1. **No Keystroke Storage or Transmission**:
   - Individual key codes (`vkCode`) are never stored in memory buffers, never written to disk, and never passed outside `KeyboardHookManager`.
   - The application is **100% offline** and makes zero network requests.
2. **Diagnostic Logging Scope**:
   - `DiagnosticLogger` only records high-level application lifecycle events (e.g., `AppStart`, `SoftwareHookStarted`, `InternalKeyboardDisabled`, `InternalKeyboardRestored`) in structured JSONL format (`diagnostics.jsonl`) with an automatic **512 KB** rotation limit.
3. **Hardware Fail-Safe Design**:
   - In **Mode 2** (`CfgMgr32`), touchpads, mice, and sensors are strictly whitelisted and excluded via `DeviceClassifier`.
   - System shutdown, sleep, crash handlers, external keyboard disconnect sensors, and `KeyLock.exe --unlock` ensure internal keyboards are always restored safely.

## Reporting a Vulnerability

If you discover a security or hardware-safety issue, please open a GitHub Issue or contact the maintainers directly. We aim to address critical issues within 48 hours.

