using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using LaptopKeyboardLocker.Core.Native;

namespace LaptopKeyboardLocker.Core
{
    /// <summary>
    /// 負責模式一（軟體全域鎖定）：透過 Win32 WH_KEYBOARD_LL 攔截鍵盤按鍵事件（包含一般鍵、多媒體鍵、音量鍵與 CapsLock）。
    /// 具備內部實體修飾鍵狀態追蹤表、修飾鍵 WM_KEYUP 放行防卡鍵機制、1ms 非同步解鎖派發與零按鍵記錄 (Zero Keystroke Logging) 安全設計。
    /// </summary>
    public class KeyboardHookManager : IDisposable
    {
        private IntPtr hookId = IntPtr.Zero;
        private Win32Api.LowLevelKeyboardProc procDelegate;
        private bool isLocked = false;
        private SynchronizationContext uiSyncContext;
        private HotkeyModel cachedHotkey;

        // 內部維護之實體修飾鍵按下狀態表（解決 WH_KEYBOARD_LL 攔截後 GetAsyncKeyState 失靈問題）
        private bool isCtrlPressed = false;
        private bool isAltPressed = false;
        private bool isShiftPressed = false;
        private bool isWinPressed = false;

        public event EventHandler UnlockRequested;

        public bool IsLocked
        {
            get { return isLocked; }
        }

        public KeyboardHookManager()
        {
            // 保留委派參考，防止被 Garbage Collector 回收導致 Native 回呼崩潰
            procDelegate = HookCallback;
            uiSyncContext = SynchronizationContext.Current;
        }

        /// <summary>
        /// 重置內部追蹤的修飾鍵狀態（於鎖定啟動、解鎖或 Windows 鎖屏切換 SessionSwitch 時呼叫）。
        /// </summary>
        public void ResetModifierStates()
        {
            isCtrlPressed = false;
            isAltPressed = false;
            isShiftPressed = false;
            isWinPressed = false;
        }

        /// <summary>
        /// 更新記憶體中快取的解鎖熱鍵模型（避免在 HookCallback 中觸發磁碟讀取）。
        /// </summary>
        public void RefreshCachedHotkey()
        {
            cachedHotkey = ConfigManager.Current.Hotkey.Clone();
        }

        /// <summary>
        /// 啟動全域鍵盤攔截鎖定。
        /// </summary>
        public bool StartLock()
        {
            if (isLocked) return true;

            uiSyncContext = SynchronizationContext.Current ?? uiSyncContext;
            RefreshCachedHotkey();

            // 初始化啟動瞬間的實體修飾鍵狀態
            const int keyStatePressedMask = 0x8000;
            isCtrlPressed = ((Win32Api.GetAsyncKeyState(Win32Api.VK_LCONTROL) & keyStatePressedMask) != 0) ||
                            ((Win32Api.GetAsyncKeyState(Win32Api.VK_RCONTROL) & keyStatePressedMask) != 0);
            isAltPressed = ((Win32Api.GetAsyncKeyState(Win32Api.VK_LMENU) & keyStatePressedMask) != 0) ||
                           ((Win32Api.GetAsyncKeyState(Win32Api.VK_RMENU) & keyStatePressedMask) != 0);
            isShiftPressed = ((Win32Api.GetAsyncKeyState(Win32Api.VK_LSHIFT) & keyStatePressedMask) != 0) ||
                             ((Win32Api.GetAsyncKeyState(Win32Api.VK_RSHIFT) & keyStatePressedMask) != 0);
            isWinPressed = ((Win32Api.GetAsyncKeyState(Win32Api.VK_LWIN) & keyStatePressedMask) != 0) ||
                           ((Win32Api.GetAsyncKeyState(Win32Api.VK_RWIN) & keyStatePressedMask) != 0);

            IntPtr moduleHandle = Win32Api.GetModuleHandle(null);
            hookId = Win32Api.SetWindowsHookEx(Win32Api.WH_KEYBOARD_LL, procDelegate, moduleHandle, 0);

            if (hookId == IntPtr.Zero)
            {
                int error = Marshal.GetLastWin32Error();
                DiagnosticLogger.Error("HookStartFailed", "Win32Error=" + error);
                return false;
            }

            isLocked = true;
            DiagnosticLogger.Info("SoftwareHookStarted", "Mode=SoftwareHook");
            return true;
        }

        /// <summary>
        /// 解除全域鍵盤攔截。
        /// </summary>
        public void StopLock()
        {
            if (!isLocked) return;

            if (hookId != IntPtr.Zero)
            {
                Win32Api.UnhookWindowsHookEx(hookId);
                hookId = IntPtr.Zero;
            }

            ResetModifierStates();
            isLocked = false;
            DiagnosticLogger.Info("SoftwareHookStopped", "Mode=SoftwareHook");
        }

        private static bool UpdateModifierFlag(Keys key, bool isDown, ref bool ctrl, ref bool alt, ref bool shift, ref bool win)
        {
            if (key == Keys.LControlKey || key == Keys.RControlKey || key == Keys.ControlKey)
            {
                ctrl = isDown;
                return true;
            }
            if (key == Keys.LMenu || key == Keys.RMenu || key == Keys.Menu)
            {
                alt = isDown;
                return true;
            }
            if (key == Keys.LShiftKey || key == Keys.RShiftKey || key == Keys.ShiftKey)
            {
                shift = isDown;
                return true;
            }
            if (key == Keys.LWin || key == Keys.RWin)
            {
                win = isDown;
                return true;
            }
            return false;
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int msgType = wParam.ToInt32();
                bool isKeyDown = (msgType == Win32Api.WM_KEYDOWN || msgType == Win32Api.WM_SYSKEYDOWN);
                bool isKeyUp = (msgType == Win32Api.WM_KEYUP || msgType == Win32Api.WM_SYSKEYUP);

                if (isKeyDown || isKeyUp)
                {
                    Win32Api.KBDLLHOOKSTRUCT hookStruct = (Win32Api.KBDLLHOOKSTRUCT)
                        Marshal.PtrToStructure(lParam, typeof(Win32Api.KBDLLHOOKSTRUCT));

                    Keys key = (Keys)hookStruct.vkCode;

                    // 1. 更新內部修飾鍵狀態表
                    bool isModifierKey = UpdateModifierFlag(
                        key,
                        isKeyDown,
                        ref isCtrlPressed,
                        ref isAltPressed,
                        ref isShiftPressed,
                        ref isWinPressed
                    );

                    // 2. 防卡鍵保護：若為修飾鍵的放開事件 (WM_KEYUP)，予以放行讓 Windows 釋放啟動鎖定前已按下的修飾鍵狀態
                    if (isKeyUp && isModifierKey)
                    {
                        return Win32Api.CallNextHookEx(hookId, nCode, wParam, lParam);
                    }

                    // 3. 若為一般鍵按下事件，比對是否命中自訂解鎖熱鍵（絕不儲存或記錄按鍵內容）
                    if (isKeyDown && !isModifierKey)
                    {
                        HotkeyModel targetHotkey = cachedHotkey ?? ConfigManager.Current.Hotkey;
                        if (targetHotkey.IsMatch(key, isCtrlPressed, isAltPressed, isShiftPressed, isWinPressed))
                        {
                            DispatchUnlockAsync();
                            // 攔截吞掉觸發解鎖的那顆按鍵，避免在當前視窗多打出字元
                            return new IntPtr(1);
                        }
                    }

                    // 4. 完整攔截所有其他按鍵事件（含多媒體鍵、音量鍵、CapsLock 等）
                    return new IntPtr(1);
                }
            }

            return Win32Api.CallNextHookEx(hookId, nCode, wParam, lParam);
        }

        private void DispatchUnlockAsync()
        {
            if (UnlockRequested == null) return;

            if (uiSyncContext != null)
            {
                uiSyncContext.Post(delegate
                {
                    if (UnlockRequested != null)
                    {
                        UnlockRequested(this, EventArgs.Empty);
                    }
                }, null);
            }
            else
            {
                ThreadPool.QueueUserWorkItem(delegate
                {
                    if (UnlockRequested != null)
                    {
                        UnlockRequested(this, EventArgs.Empty);
                    }
                });
            }
        }

        public void Dispose()
        {
            StopLock();
            GC.SuppressFinalize(this);
        }
    }
}
