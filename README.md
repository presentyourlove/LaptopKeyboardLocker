# LaptopKeyboardLocker (`KeyLock.exe`)

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Platform: Windows 10/11](https://img.shields.io/badge/Platform-Windows%2010%2F11%20(x64)-0078D6.svg)](#)
[![Test Coverage: 94.4%](https://img.shields.io/badge/Coverage-94.4%25-brightgreen.svg)](#building--automated-testing-80-coverage)
[![Zero Dependencies](https://img.shields.io/badge/Portable-Single%20EXE%20(~60KB)-orange.svg)](#)

A lightweight, zero-dependency, dual-mode **One-Click Laptop Keyboard Locker** for Windows 10/11. Built for cleaning your laptop keyboard, preventing cat/toddler key presses, or placing an external mechanical keyboard directly on top of your laptop keyboard.

*[English Documentation](#key-features) | [繁體中文完整說明](#繁體中文說明-traditional-chinese-guide)*

---

## Key Features

### 1. Dual-Mode Locking Engine
* **Mode 1: Global Software Hook (`WH_KEYBOARD_LL`)**
  * **No Administrator privileges required** — launches and locks instantly.
  * Blocks all standard keys, multimedia keys, volume controls, and `CapsLock`.
  * Tracks physical left/right modifier keys (`VK_LCONTROL`, `VK_RMENU`, etc.) internally and passes modifier `WM_KEYUP` events through to prevent stuck `Ctrl`/`Alt` keys from affecting mouse scrolling or clicks.
* **Mode 2: Internal Keyboard Isolation (`CfgMgr32` + Win32 Raw Input `WM_INPUT` Dual-Tier Engine)**
  * **Tier 1 (Hardware PnP Control)**: Precisely disables the laptop's built-in keyboard (`ACPI\PNP0303` PS/2, `HID\MSFT*` / `HID\ATK*` I2C, ASUS ROG/TUF N-Key `VID_0B05`, ITE `VID_048D`, MSI SteelSeries `VID_1038`/`VID_1770`, Elan/Synaptics/Chicony) and composite parent nodes while allowing **external USB/Bluetooth keyboards to type 100% normally**.
  * **Tier 2 (Raw Input Per-Device Software Filter Fallback)**: If Windows refuses to unload a keyboard driver without a reboot (`CR_NEED_RESTART`), Mode 2 automatically engages a Win32 Raw Input (`WM_INPUT` + `GetRawInputDeviceInfo`) per-device filter that blocks **only** keystrokes originating from the built-in keyboard while passing external keyboard keystrokes through seamlessly — never forcing you to disable all keyboards.
  * **Touchpad Safety Whitelist**: Strictly excludes `Touchpad`, `Trackpad`, `Mouse`, and `Sensor` endpoints so your laptop touchpad is never affected.

### 2. Custom Hotkeys & Bilingual UI (English / 繁體中文)
* **Default Hotkey**: `Ctrl + Alt + L` to lock/unlock with a **400ms anti-bounce cooldown**.
* **Custom Hotkey Recorder**: Record your own shortcut with built-in anti-misconfiguration validation (requires at least one modifier + a key, or `F1`–`F12` / `Pause`).
* **Instant Language Switch**: Switch UI and system tray menus between **繁體中文 (Traditional Chinese)** and **English** in real time without restarting.

### 3. Six-Tier Fail-Safe Protection (Never Get Locked Out)
1. **External Keyboard Disconnect Sensor**: Monitors `WM_DEVICECHANGE` (with 300ms debounce). If all external keyboards are unplugged while in Mode 2, the built-in keyboard is **automatically restored immediately**.
2. **Mouse Double-Click Unlock**: Double-click the red system tray icon anytime to unlock immediately.
3. **Auto-Unlock Safety Timeout**: Configurable countdown timer (default: **15 minutes**) automatically unlocks the keyboard.
4. **Shutdown / Sleep / Lock-Screen Protection**: Automatically restores hardware on `SessionEnding` (shutdown/logoff) and `PowerModeChanged` (sleep/hibernate), and clears modifier states on `SessionSwitch` (`Win + L`).
5. **Crash & Exit Recovery**: Hooks `UnhandledException`, `ThreadException`, and `ProcessExit` to restore hardware before termination.
6. **Persistent State Self-Healing**: Uses `Win32_PnPEntity` and a local `disabled_devices.state` file so even after a sudden power loss, launching `KeyLock.exe` (or running `KeyLock.exe --unlock`) restores the internal keyboard.

---

## Quick Start & CLI Usage

1. Download or build `KeyLock.exe` (~57 KB standalone portable executable).
2. Double-click `KeyLock.exe` to open the dashboard, or press **`Ctrl + Alt + L`** to lock/unlock.

### Command-Line Arguments (CLI)
```powershell
# Start silently minimized to the system tray (used for Windows startup)
KeyLock.exe --minimized

# Start and immediately lock the keyboard
KeyLock.exe --lock

# Emergency rescue: forcefully restore all internal keyboards and exit immediately
KeyLock.exe --unlock
```

---

## Security & Privacy Transparency (Zero Keystroke Logging)

Because keyboard lockers utilize Windows Low-Level Keyboard Hooks (`WH_KEYBOARD_LL`), security transparency is paramount:
* **100% Offline**: `KeyLock.exe` contains zero network/HTTP calls.
* **Zero Keystroke Logging**: `KeyboardHookManager` only checks whether the current key combination matches your unlock hotkey in memory. It **never** logs, stores, or emits individual keycodes to any file or external module.
* **Auditable & Reproducible**: You can inspect the source code and compile `KeyLock.exe` yourself in 2 seconds using the built-in Windows C# compiler (`build.bat`).

---

## Building & Automated Testing (>80% Coverage)

No external SDK installation is required on Windows 10/11! Both scripts use the native 64-bit compiler (`csc.exe`) included with Windows, or you can use the `.NET 8` SDK (`LaptopKeyboardLocker.csproj`).

```powershell
# 1. Run the automated unit test suite (61 tests, 94.4% core coverage)
.\run-tests.bat

# 2. Build the standalone portable KeyLock.exe
.\build.bat

# 3. Clean, test, build, push to GitHub, and publish GitHub Release in one click
.\publish-github.bat v1.1.0
```

---

# 繁體中文說明 (Traditional Chinese Guide)

**LaptopKeyboardLocker (`KeyLock.exe`)** 是一款專為 Windows 10/11 筆電設計的極致輕量（僅約 60 KB）、免安裝單一執行檔之**一鍵鎖定／解鎖筆電鍵盤工具**。

### 核心特色
1. **雙模鎖定設計**：
   * **模式一（全域軟體鎖定）**：免系統管理員權限，一鍵攔截全鍵盤（含多媒體鍵、音量鍵、CapsLock），適合清理擦拭鍵盤、防止貓咪踩踏或幼童誤觸。內建修飾鍵 `WM_KEYUP` 放行機制，鎖定期間滑鼠滾輪與點擊 100% 正常不卡鍵。
   * **模式二（僅停用筆電內建鍵盤 - 雙層智慧引擎）**：精準識別並停用筆電內建 PS/2、I2C 及電競/商用筆電嵌入式 HID 鍵盤（含 ASUS ROG/TUF N-Key `VID_0B05`、ITE `VID_048D`、MSI SteelSeries `VID_1038` 等），**外接機械鍵盤可 100% 正常打字**。若系統因驅動限制無法動態卸載硬體節點，會自動啟用 **Win32 Raw Input (`WM_INPUT`) 單一裝置過濾引擎**，僅攔截內建鍵盤按鍵並放行外接鍵盤，絕不強迫停用所有鍵盤！
2. **中／英雙語即時切換與自訂熱鍵**：
   * 預設快速鍵為 **`Ctrl + Alt + L`**（具備 400ms 防彈跳冷卻保護）。
   * 支援自訂錄製快速鍵（具防誤設單鍵驗證）與一鍵「還原預設」。
   * 介面提供「繁體中文 / English」下拉選單，即時切換無需重啟。
3. **六重防鎖死安全保護網**：
   * **外接鍵盤拔除自動復原**：於模式二下若外接鍵盤全數斷開，系統立即自動復原啟用內建鍵盤。
   * **滑鼠連點解鎖**：連點兩下右下角系統匣紅色圖示即可立即解鎖。
   * **防呆逾時自動解鎖**：預設 15 分鐘（可自訂）自動解除鎖定。
   * **關機／睡眠／崩潰自動復原**：關機、休眠、程序異常退出或重開機自檢時，皆會自動復原內建鍵盤。
4. **資安透明（零按鍵記錄）與高測試覆蓋率**：
   * 嚴格遵循 **Zero Keystroke Logging**，絕不記錄任何按鍵輸入，完全離線運作。
   * 內建 61 項自動化單元測試（執行 `run-tests.bat`），核心覆蓋率達 **94.4%**。

## License
Licensed under the [MIT License](LICENSE).

