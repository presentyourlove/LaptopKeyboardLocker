# Contributing to LaptopKeyboardLocker

Thank you for your interest in contributing to **LaptopKeyboardLocker (`KeyLock.exe`)**!

## Development & Build Prerequisites

This project is designed to build out-of-the-box on any Windows 10/11 (x64) machine without requiring heavy SDK installations, while also supporting the modern `.NET 8` SDK.

### 1. Running Automated Unit Tests
Before submitting any changes, please run the unit test suite and ensure **100% of tests pass** and core code coverage remains **> 80%**:
```cmd
run-tests.bat
```

### 2. Building the Standalone Executable
To compile `KeyLock.exe` locally:
```cmd
build.bat
```

## Coding & Architecture Guidelines
* **Single Responsibility Principle (SRP)**: Keep pure logic (such as `DeviceClassifier`, `HotkeyModel`, `LockStateManager`) decoupled from Win32 P/Invoke and UI forms so it remains 100% unit-testable.
* **No Magic Numbers**: Define all Win32 constants, virtual key codes, and return codes in `src/Core/Native/Win32Api.cs`.
* **Zero Keystroke Logging**: Never log, store, or emit user keystrokes in `KeyboardHookManager` or `DiagnosticLogger`.
* **Bilingual UI Support**: Every UI string must be registered in both `StringsZh` (Traditional Chinese) and `StringsEn` (English) inside `src/Core/LocalizationManager.cs`.
* **Commit Messages**: Follow the [Conventional Commits](https://www.conventionalcommits.org/) specification (`feat:`, `fix:`, `docs:`, `refactor:`, `test:`).

