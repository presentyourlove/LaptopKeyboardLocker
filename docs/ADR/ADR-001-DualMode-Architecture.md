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
- **模式二（內建鍵盤雙層隔離引擎 `DeviceControlManager` + `KeyboardHookManager(internalOnlyFilter)`）**：
  - 第一層（硬體層停用）：使用 `CfgMgr32.dll` (`CM_Locate_DevNode`, `CM_Disable_DevNode`, `CM_Enable_DevNode`, `CM_Get_Parent`) 並強制以 64 位元程序執行。若子集合節點受限，自動向上停用專屬內建複合輸入父節點。
  - 第二層（Raw Input 單一裝置軟體過濾後援）：若系統因硬體限制回報 `CR_NEED_RESTART`，自動啟用 `WM_INPUT` (`RegisterRawInputDevices` + `GetRawInputDeviceInfo` + `PeekMessage`) 單一裝置過濾引擎，僅攔截筆電內建鍵盤並放行外接鍵盤，絕不強迫停用所有鍵盤。
  - 採用 `Win32_PnPEntity` 搭配 `disabled_devices.state` 本地持久化記錄，確保重開機或崩潰後 100% 能找回並啟用內建鍵盤。

### 2. 硬體分類與觸控板絕對保護 (`DeviceClassifier`)
- 支援傳統 `ACPI\PNP0303`、現代輕薄筆電 HID-over-I2C（`HID\MSFT`、`HID\ATK`、`HID\VEN_` 等），以及電競／商用筆電內建嵌入式 HID 控制器（ASUS ROG/TUF N-Key `VID_0B05`、ITE Tech `VID_048D`、MSI SteelSeries `VID_1038`/`VID_1770`、Elan `VID_04F3`、Synaptics `VID_06CB`、Chicony `VID_04F2`）。
- **觸控板／滑鼠強制排除白名單**：凡含 `TOUCHPAD`、`TRACKPAD`、`MOUSE`、`SENSOR` 之裝置一律排除，絕不誤傷筆電觸控板。

### 3. 六重防鎖死安全機制 (6-Tier Fail-Safe)
1. **外接鍵盤拔除自動復原**：監聽 `WM_DEVICECHANGE`（含 300ms 防抖動），外接鍵盤全數斷開時立即復原內建鍵盤。
2. **滑鼠托盤連點解鎖**：連點兩下右下角紅色系統匣圖示立即解鎖。
3. **防呆逾時自動解鎖**：預設 15 分鐘（可自訂）自動解鎖。
4. **關機／登出／睡眠自動復原**：監聽 `SessionEnding`、`PowerModeChanged` 自動解鎖；監聽 `SessionSwitch` 清零鎖屏殘留修飾鍵。
5. **崩潰與退出保護**：掛載 `UnhandledException`、`ThreadException` 與 `ProcessExit` 強制復原硬體。
6. **CLI 緊急救援指令**：支援 `KeyLock.exe --unlock` 強制復原硬體並退出。

### 4. 零按鍵記錄 (Zero Keystroke Logging)、節省 CI 額度與高測試覆蓋率
- 絕不記錄或外拋任何按鍵內容；`DiagnosticLogger` 僅輸出含 `TraceId` 之結構化狀態 JSON 日誌（512KB 自動輪替）。
- 61 項自動化單元測試全數通過，核心模組覆蓋率達 **94.4%**（高於 80% 目標）。
- 提供本機一鍵自動化清理、測試、編譯、上傳與發佈腳本 `publish-github.bat`，並將 GitHub Actions 設為 `workflow_dispatch` 手動觸發，日常推送與發佈 Release **0 消耗** GitHub Actions 免費額度。

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
- `build.bat`, `run-tests.bat`, `publish-github.bat`, `LaptopKeyboardLocker.csproj`, `KeyLock.exe`

