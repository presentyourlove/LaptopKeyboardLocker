# ADR-001: 雙模筆電鍵盤鎖定工具 (`LaptopKeyboardLocker`) 架構決策總覽

## 狀態 (Status)
已實作並驗證通過 (Implemented & Verified)

## 背景 (Context)
Windows 筆記型電腦使用者在以下兩種核心場景需要鎖定鍵盤：
1. **桌面清潔與防誤觸（防貓咪踩踏／幼童誤觸）**：希望免系統管理員權限、隨開即鎖，並完整攔截一般鍵、多媒體鍵與音量鍵。
2. **外接機械鍵盤工作（將外接鍵盤架在筆電上）**：希望精準停用筆電內建鍵盤，但外接 USB／藍牙鍵盤仍能完全正常打字。

同時，底層鍵盤攔截與硬體裝置控制在 Windows 10/11 環境下存在多項技術陷阱（如低階 Hook 吞掉修飾鍵造成滑鼠滾輪卡鍵、WMI `Win32_Keyboard` 隱藏已停用裝置、I2C 觸控板複合節點誤傷、WOW64 拒絕呼叫 `CfgMgr32` 等）。

## 核心決策 (Decisions)

### 1. 雙模式引擎設計 (Dual-Mode Engine)
- **模式一（全域軟體鎖定 `KeyboardHookManager`）**：
  - 使用 Win32 `WH_KEYBOARD_LL` 低階鍵盤攔截，免管理員權限。
  - **內部實體修飾鍵狀態追蹤**：自行追蹤 `VK_LCONTROL`/`VK_RCONTROL`、`VK_LMENU`/`VK_RMENU`、`VK_LSHIFT`/`VK_RSHIFT`、`VK_LWIN`/`VK_RWIN`，解決按鍵被攔截後 `GetAsyncKeyState` 失靈問題。
  - **放行修飾鍵 `WM_KEYUP`**：防止進入鎖定時 `Ctrl`/`Alt` 卡住導致滑鼠滾輪變網頁縮放。
  - **1ms 極速非同步派發**：命中解鎖熱鍵時立即吞掉觸發鍵，並透過 `SynchronizationContext.Post` 非同步解鎖，避免觸發 `LowLevelHooksTimeout`。
- **模式二（內建鍵盤硬體停用 `DeviceControlManager`）**：
  - 使用 `CfgMgr32.dll` (`CM_Locate_DevNode`, `CM_Disable_DevNode`, `CM_Enable_DevNode`) 並強制以 64 位元程序執行。
  - 採用 `Win32_PnPEntity WHERE PNPClass = 'Keyboard'` 搭配 `disabled_devices.state` 本地持久化記錄，確保重開機或崩潰後 100% 能找回並啟用內建鍵盤。
  - 遇傳統 PS/2 鍵盤回報 `CR_NEED_RESTART` 時，自動取消停用標記並智慧引導降級至模式一。

### 2. 硬體分類與觸控板絕對保護 (`DeviceClassifier`)
- 支援傳統 `ACPI\PNP0303` 與現代輕薄筆電 HID-over-I2C（`HID\MSFT`、`HID\ATK`、`HID\VEN_` 等）。
- **觸控板／滑鼠強制排除白名單**：凡含 `TOUCHPAD`、`TRACKPAD`、`MOUSE`、`SENSOR` 之裝置一律排除，絕不誤傷筆電觸控板。

### 3. 六重防鎖死安全機制 (6-Tier Fail-Safe)
1. **外接鍵盤拔除自動復原**：監聽 `WM_DEVICECHANGE`（含 300ms 防抖動），外接鍵盤全數斷開時立即復原內建鍵盤。
2. **滑鼠托盤連點解鎖**：連點兩下右下角紅色系統匣圖示立即解鎖。
3. **防呆逾時自動解鎖**：預設 15 分鐘（可自訂）自動解鎖。
4. **關機／登出／睡眠自動復原**：監聽 `SessionEnding`、`PowerModeChanged` 自動解鎖；監聽 `SessionSwitch` 清零鎖屏殘留修飾鍵。
5. **崩潰與退出保護**：掛載 `UnhandledException`、`ThreadException` 與 `ProcessExit` 強制復原硬體。
6. **CLI 緊急救援指令**：支援 `KeyLock.exe --unlock` 強制復原硬體並退出。

### 4. 零按鍵記錄 (Zero Keystroke Logging) 與高測試覆蓋率
- 絕不記錄或外拋任何按鍵內容；`DiagnosticLogger` 僅輸出含 `TraceId` 之結構化狀態 JSON 日誌（512KB 自動輪替）。
- 51 項自動化單元測試全數通過，核心模組覆蓋率達 **93.9%**（高於 80% 目標）。

## 專案檔案清單 (Project Files)
- `src/Core/Native/Win32Api.cs`
- `src/Core/DiagnosticLogger.cs`
- `src/Core/DeviceClassifier.cs`
- `src/Core/HotkeyModel.cs`
- `src/Core/LocalizationManager.cs`
- `src/Core/ConfigManager.cs`
- `src/Core/StartupManager.cs`
- `src/Core/LockStateManager.cs`
- `src/Core/KeyboardHookManager.cs`
- `src/Core/DeviceControlManager.cs`
- `src/UI/OverlayHudForm.cs`
- `src/UI/MainForm.cs`
- `src/UI/TrayAppContext.cs`
- `src/Program.cs`
- `tests/LaptopKeyboardLocker.Tests/*.cs` & `tests/TestRunner.cs`
- `build.bat`, `run-tests.bat`, `LaptopKeyboardLocker.csproj`, `KeyLock.exe`

