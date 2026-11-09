using System;
using System.Collections.Generic;

namespace LaptopKeyboardLocker.Core
{
    public enum AppLanguage
    {
        TraditionalChinese = 0,
        English = 1
    }

    /// <summary>
    /// 管理應用程式多語系介面字串字典與動態語系切換通知。
    /// </summary>
    public static class LocalizationManager
    {
        private static AppLanguage currentLanguage = AppLanguage.TraditionalChinese;
        public static event EventHandler LanguageChanged;

        public static AppLanguage CurrentLanguage
        {
            get { return currentLanguage; }
            set
            {
                if (currentLanguage != value)
                {
                    currentLanguage = value;
                    OnLanguageChanged();
                }
            }
        }

        private static readonly Dictionary<string, string> StringsZh = new Dictionary<string, string>
        {
            // 視窗標題與狀態
            { "AppTitle", "筆電鍵盤鎖定工具 (KeyLock)" },
            { "LblLanguage", "介面語系 (Language)：" },
            { "StatusUnlocked", "當前狀態：🟢 正常未鎖定 (Unlocked)" },
            { "StatusLocked", "當前狀態：🔒 鍵盤已鎖定 (Locked)" },
            { "PrivAdmin", "權限：系統管理員 (Administrator)" },
            { "PrivStandard", "權限：標準使用者 (Standard)" },
            { "BtnElevate", "🛡️ 以管理員重啟 (Elevate)" },

            // 按鈕與控制
            { "BtnLock", "🔒 立即鎖定鍵盤 (Lock Keyboard)" },
            { "BtnUnlock", "🔓 解除鎖定 (Unlock Keyboard)" },
            { "GrpMode", "鎖定模式選擇 (Locking Mode)" },
            { "ModeSoftware", "模式一：全域軟體鎖定 (免管理員權限，適合鍵盤清潔/防貓踩)" },
            { "ModeHardware", "模式二：僅停用筆電內建鍵盤 (需管理員權限，外接鍵盤可打字)" },

            // 快速鍵與設定
            { "GrpHotkey", "快速鍵設定 (Hotkey Settings)" },
            { "LblHotkey", "解鎖/鎖定快速鍵：" },
            { "BtnRecordHotkey", "錄製熱鍵" },
            { "BtnResetHotkey", "還原預設" },
            { "RecordingPrompt", "請按下組合鍵 (按 Esc 取消)..." },
            { "InvalidHotkeyNotice", "為防止誤觸，自訂熱鍵必須包含至少一個修飾鍵 (Ctrl/Alt/Shift/Win) 搭配一般鍵，或使用 F1~F12 功能鍵。" },
            { "HotkeyConflictNotice", "注意：此組合鍵目前已被系統其他常駐軟體佔用，可能無法在未鎖定時透過熱鍵直接上鎖（建議更換其他組合鍵）。" },

            // 防呆與安全選項
            { "GrpSafety", "安全、防呆與偏好設定 (Safety & Preferences)" },
            { "ChkAutoTimeout", "防呆逾時自動解鎖：" },
            { "UnitMinutes", "分鐘 (0 為停用)" },
            { "ChkRestoreOnDisconnect", "當外接鍵盤被拔除時自動復原內建鍵盤" },
            { "ChkStartWithWindows", "隨 Windows 開機自動靜默啟動" },
            { "ChkShowHud", "鎖定時在螢幕右下角顯示提示遮罩 (HUD)" },
            { "ChkPlaySound", "鎖定／解鎖時播放輕微提示音" },

            // HUD 遮罩文字
            { "HudTitle", "🔒 鍵盤已鎖定 (Keyboard Locked)" },
            { "HudHotkeyPrefix", "解鎖熱鍵 (Unlock): " },
            { "HudSubtitle", "亦可連點右下角系統匣紅色圖示解鎖" },

            // 系統匣與選單
            { "TrayShow", "顯示主面板 (Open Dashboard)" },
            { "TrayLock", "鎖定鍵盤 (Lock)" },
            { "TrayUnlock", "解鎖鍵盤 (Unlock)" },
            { "TrayExit", "完全結束 (Exit)" },
            { "TrayTipLocked", "KeyLock: 鍵盤鎖定中 (連點兩下可解鎖)" },
            { "TrayTipUnlocked", "KeyLock: 正常運行中" },
            { "TrayMinimizedTitle", "KeyLock 已縮小至系統匣" },
            { "TrayMinimizedText", "程式仍在背景運行中，您可隨時透過熱鍵或點擊右下角圖示操作。" },

            // 提權、PS/2 降級與警告
            { "UacPromptTitle", "需要系統管理員權限" },
            { "UacPromptMessage", "「模式二（停用內建硬體鍵盤）」或高權限視窗攔截需要 Windows 系統管理員權限。\n是否要立即以系統管理員身分重新啟動？" },
            { "Ps2FallbackTitle", "硬體限制與智慧模式切換" },
            { "Ps2FallbackMessage", "您的筆電內建鍵盤屬於傳統 PS/2 匯流排裝置，Windows 限制無法在不重開機的情況下動態停用。\n\n是否要立即智慧切換為「模式一（全域軟體攔截）」並完成鎖定？" },
            { "ExternalDisconnectedNotice", "偵測到外接鍵盤已全數移除，系統已自動為您復原筆電內建鍵盤以確保安全！" }
        };

        private static readonly Dictionary<string, string> StringsEn = new Dictionary<string, string>
        {
            // Title & Status
            { "AppTitle", "Laptop Keyboard Locker (KeyLock)" },
            { "LblLanguage", "Language:" },
            { "StatusUnlocked", "Current Status: 🟢 Unlocked" },
            { "StatusLocked", "Current Status: 🔒 Keyboard Locked" },
            { "PrivAdmin", "Privilege: Administrator" },
            { "PrivStandard", "Privilege: Standard User" },
            { "BtnElevate", "🛡️ Run as Admin" },

            // Buttons & Controls
            { "BtnLock", "🔒 Lock Keyboard Now" },
            { "BtnUnlock", "🔓 Unlock Keyboard" },
            { "GrpMode", "Locking Mode Selection" },
            { "ModeSoftware", "Mode 1: Global Software Hook (No Admin, ideal for cleaning/cats)" },
            { "ModeHardware", "Mode 2: Disable Internal Keyboard Only (Admin required, external typing)" },

            // Hotkey & Settings
            { "GrpHotkey", "Hotkey Settings" },
            { "LblHotkey", "Lock/Unlock Hotkey:" },
            { "BtnRecordHotkey", "Record" },
            { "BtnResetHotkey", "Default" },
            { "RecordingPrompt", "Press key combo (Esc to cancel)..." },
            { "InvalidHotkeyNotice", "To prevent accidental unlocking, custom hotkeys must include at least one modifier (Ctrl/Alt/Shift/Win) + a key, or use F1~F12." },
            { "HotkeyConflictNotice", "Note: This hotkey is currently occupied by another application in Windows. Consider choosing a different key combination." },

            // Safety & Options
            { "GrpSafety", "Safety & Preferences" },
            { "ChkAutoTimeout", "Auto-unlock safety timeout:" },
            { "UnitMinutes", "minutes (0 to disable)" },
            { "ChkRestoreOnDisconnect", "Auto-restore internal keyboard when external keyboard is disconnected" },
            { "ChkStartWithWindows", "Start silently with Windows on boot" },
            { "ChkShowHud", "Show floating HUD reminder when locked" },
            { "ChkPlaySound", "Play notification sound on lock/unlock" },

            // HUD Overlay
            { "HudTitle", "🔒 Keyboard Locked" },
            { "HudHotkeyPrefix", "Unlock Hotkey: " },
            { "HudSubtitle", "Or double-click the red tray icon to unlock" },

            // Tray & Menu
            { "TrayShow", "Open Dashboard" },
            { "TrayLock", "Lock Keyboard" },
            { "TrayUnlock", "Unlock Keyboard" },
            { "TrayExit", "Exit Application" },
            { "TrayTipLocked", "KeyLock: Keyboard Locked (Double-click to unlock)" },
            { "TrayTipUnlocked", "KeyLock: Running normally" },
            { "TrayMinimizedTitle", "KeyLock Minimized to Tray" },
            { "TrayMinimizedText", "KeyLock is running in the background. Press your hotkey or double-click the tray icon anytime." },

            // UAC, PS/2 Fallback & Alerts
            { "UacPromptTitle", "Administrator Privileges Required" },
            { "UacPromptMessage", "Mode 2 (Hardware Device Control) requires Windows Administrator privileges.\nDo you want to restart KeyLock with administrator privileges now?" },
            { "Ps2FallbackTitle", "Hardware Limitation & Smart Fallback" },
            { "Ps2FallbackMessage", "Your laptop's internal keyboard uses a legacy PS/2 bus that Windows cannot dynamically disable without a reboot.\n\nWould you like to automatically switch to Mode 1 (Global Software Hook) and lock the keyboard now?" },
            { "ExternalDisconnectedNotice", "All external keyboards disconnected! Internal keyboard was automatically restored for safety." }
        };

        /// <summary>
        /// 依鍵值取得當前語系之對應翻譯字串。
        /// </summary>
        public static string Get(string key)
        {
            Dictionary<string, string> dict = (currentLanguage == AppLanguage.English) ? StringsEn : StringsZh;
            string value;
            if (dict.TryGetValue(key, out value))
            {
                return value;
            }

            if (StringsZh.TryGetValue(key, out value))
            {
                return value;
            }

            return key;
        }

        /// <summary>
        /// 取得所有支援的語系鍵值清單（供單元測試驗證繁中與英文對照完整性）。
        /// </summary>
        public static IEnumerable<string> GetAllKeys()
        {
            return StringsZh.Keys;
        }

        private static void OnLanguageChanged()
        {
            if (LanguageChanged != null)
            {
                LanguageChanged(null, EventArgs.Empty);
            }
        }
    }
}
