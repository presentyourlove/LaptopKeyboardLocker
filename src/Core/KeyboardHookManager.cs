using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using LaptopKeyboardLocker.Core.Native;

namespace LaptopKeyboardLocker.Core
{
    /// <summary>
    /// 負責鍵盤攔截與單一裝置過濾：
    /// 1. 模式一（全域軟體鎖定）：攔截所有鍵盤按鍵事件（包含一般鍵、多媒體鍵、音量鍵與 CapsLock）。
    /// 2. 模式二智慧後援（僅攔截內建鍵盤過濾模式）：結合 Win32 Raw Input (WM_INPUT) 辨識硬體來源，僅攔截筆電內建鍵盤，放行外接鍵盤正常打字。
    /// 具備內部實體修飾鍵狀態追蹤表、修飾鍵 WM_KEYUP 放行防卡鍵機制、1ms 非同步解鎖派發與零按鍵記錄 (Zero Keystroke Logging) 安全設計。
    /// </summary>
    public class KeyboardHookManager : IDisposable
    {
        private static readonly UIntPtr PassthroughExtraInfoTag = new UIntPtr(0x4B4C4F4B); // "KLOK"

        private IntPtr hookId = IntPtr.Zero;
        private Win32Api.LowLevelKeyboardProc procDelegate;
        private bool isLocked = false;
        private bool isInternalOnlyFilter = false;
        private SynchronizationContext uiSyncContext;
        private HotkeyModel cachedHotkey;

        // Raw Input 裝置來源識別快取與狀態
        private RawInputSinkWindow rawInputWindow;
        private readonly Dictionary<IntPtr, bool> deviceIsInternalCache = new Dictionary<IntPtr, bool>();
        private bool lastRawDeviceWasExternal = false;
        private int lastExternalInputTick = 0;
        private uint lastSuppressedVk = 0;
        private uint lastSuppressedScan = 0;
        private uint lastSuppressedFlags = 0;
        private bool lastSuppressedWasDown = false;
        private int lastSuppressedTick = 0;

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

        public bool IsInternalOnlyFilter
        {
            get { return isInternalOnlyFilter; }
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
        /// 啟動全域鍵盤攔截鎖定（攔截所有鍵盤）。
        /// </summary>
        public bool StartLock()
        {
            return StartLock(false);
        }

        /// <summary>
        /// 啟動鍵盤攔截鎖定。
        /// 當 internalOnlyFilter 為 true 時，透過 Win32 Raw Input 辨識來源裝置，僅攔截筆電內建鍵盤並放行外接鍵盤。
        /// </summary>
        public bool StartLock(bool internalOnlyFilter)
        {
            if (isLocked) return true;

            uiSyncContext = SynchronizationContext.Current ?? uiSyncContext;
            RefreshCachedHotkey();
            isInternalOnlyFilter = internalOnlyFilter;
            deviceIsInternalCache.Clear();
            lastRawDeviceWasExternal = false;
            lastExternalInputTick = 0;
            lastSuppressedVk = 0;

            if (isInternalOnlyFilter)
            {
                EnsureRawInputRegistered();
            }

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
            DiagnosticLogger.Info(
                "SoftwareHookStarted",
                isInternalOnlyFilter ? "Mode=InternalOnlySoftwareFilter" : "Mode=SoftwareHook"
            );
            return true;
        }

        /// <summary>
        /// 解除鍵盤攔截。
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
            isInternalOnlyFilter = false;
            DiagnosticLogger.Info("SoftwareHookStopped", "Mode=SoftwareHook");
        }

        private void EnsureRawInputRegistered()
        {
            try
            {
                if (rawInputWindow == null)
                {
                    rawInputWindow = new RawInputSinkWindow(this);
                }

                Win32Api.RAWINPUTDEVICE[] rid = new Win32Api.RAWINPUTDEVICE[1];
                rid[0].usUsagePage = Win32Api.HID_USAGE_PAGE_GENERIC;
                rid[0].usUsage = Win32Api.HID_USAGE_GENERIC_KEYBOARD;
                rid[0].dwFlags = Win32Api.RIDEV_INPUTSINK;
                rid[0].hwndTarget = rawInputWindow.Handle;

                Win32Api.RegisterRawInputDevices(
                    rid,
                    1,
                    (uint)Marshal.SizeOf(typeof(Win32Api.RAWINPUTDEVICE))
                );
            }
            catch (Exception ex)
            {
                DiagnosticLogger.Warn("RawInputRegisterError", ex.Message);
            }
        }

        private bool IsRawInputDeviceInternal(IntPtr hDevice)
        {
            if (hDevice == IntPtr.Zero)
            {
                return false;
            }

            bool cached;
            if (deviceIsInternalCache.TryGetValue(hDevice, out cached))
            {
                return cached;
            }

            uint charCount = 0;
            Win32Api.GetRawInputDeviceInfo(hDevice, Win32Api.RIDI_DEVICENAME, null, ref charCount);
            if (charCount == 0)
            {
                return true;
            }

            StringBuilder sb = new StringBuilder((int)charCount + 2);
            uint res = Win32Api.GetRawInputDeviceInfo(hDevice, Win32Api.RIDI_DEVICENAME, sb, ref charCount);
            if (res == uint.MaxValue)
            {
                return true;
            }

            string rawPath = sb.ToString();
            bool isInternal = DeviceClassifier.IsRawDevicePathInternal(rawPath);
            deviceIsInternalCache[hDevice] = isInternal;
            return isInternal;
        }

        private bool TryInspectPendingRawInput(out bool fromInternalDevice)
        {
            fromInternalDevice = true;
            if (rawInputWindow == null || rawInputWindow.Handle == IntPtr.Zero)
            {
                return false;
            }

            Win32Api.MSG msg;
            if (Win32Api.PeekMessage(out msg, rawInputWindow.Handle, Win32Api.WM_INPUT, Win32Api.WM_INPUT, Win32Api.PM_REMOVE))
            {
                uint headerSize = (uint)Marshal.SizeOf(typeof(Win32Api.RAWINPUTHEADER));
                uint dataSize = (uint)Marshal.SizeOf(typeof(Win32Api.RAWINPUTKEYBOARD));
                Win32Api.RAWINPUTKEYBOARD rawKb;
                if (Win32Api.GetRawInputData(msg.lParam, Win32Api.RID_INPUT, out rawKb, ref dataSize, headerSize) != uint.MaxValue)
                {
                    fromInternalDevice = IsRawInputDeviceInternal(rawKb.header.hDevice);
                    lastRawDeviceWasExternal = !fromInternalDevice;
                    if (!fromInternalDevice)
                    {
                        lastExternalInputTick = Environment.TickCount;
                    }
                    return true;
                }
            }

            return false;
        }

        private void OnRawInputMessageReceived(IntPtr lParam)
        {
            if (!isLocked || !isInternalOnlyFilter) return;

            uint headerSize = (uint)Marshal.SizeOf(typeof(Win32Api.RAWINPUTHEADER));
            uint dataSize = (uint)Marshal.SizeOf(typeof(Win32Api.RAWINPUTKEYBOARD));
            Win32Api.RAWINPUTKEYBOARD rawKb;
            if (Win32Api.GetRawInputData(lParam, Win32Api.RID_INPUT, out rawKb, ref dataSize, headerSize) == uint.MaxValue)
            {
                return;
            }

            bool isInternal = IsRawInputDeviceInternal(rawKb.header.hDevice);
            lastRawDeviceWasExternal = !isInternal;

            if (!isInternal)
            {
                int now = Environment.TickCount;
                lastExternalInputTick = now;

                // 若外接鍵盤的首顆按鍵在 WM_INPUT 抵達前幾毫秒剛被 Hook 暫時攔截，立即重播放行
                if (lastSuppressedVk != 0 &&
                    rawKb.keyboard.VKey == lastSuppressedVk &&
                    unchecked(now - lastSuppressedTick) <= 25)
                {
                    uint flags = 0;
                    if ((lastSuppressedFlags & Win32Api.LLKHF_EXTENDED) != 0 ||
                        (rawKb.keyboard.Flags & Win32Api.RI_KEY_E0) != 0)
                    {
                        flags |= Win32Api.KEYEVENTF_EXTENDEDKEY;
                    }
                    if (!lastSuppressedWasDown || (rawKb.keyboard.Flags & Win32Api.RI_KEY_BREAK) != 0)
                    {
                        flags |= Win32Api.KEYEVENTF_KEYUP;
                    }

                    uint vkToReplay = lastSuppressedVk;
                    uint scanToReplay = lastSuppressedScan;
                    lastSuppressedVk = 0;
                    Win32Api.keybd_event((byte)vkToReplay, (byte)scanToReplay, flags, PassthroughExtraInfoTag);
                }
            }
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

                    // 0. 若為模式二單獨過濾重播之外接鍵盤事件，直接放行
                    if ((hookStruct.flags & Win32Api.LLKHF_INJECTED) != 0 &&
                        hookStruct.dwExtraInfo == PassthroughExtraInfoTag)
                    {
                        return Win32Api.CallNextHookEx(hookId, nCode, wParam, lParam);
                    }

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

                    // 4. 若處於「僅攔截內建鍵盤模式 (isInternalOnlyFilter)」，透過 Raw Input 檢查是否來自外接鍵盤
                    if (isInternalOnlyFilter)
                    {
                        bool fromInternal;
                        if (TryInspectPendingRawInput(out fromInternal))
                        {
                            if (!fromInternal)
                            {
                                return Win32Api.CallNextHookEx(hookId, nCode, wParam, lParam);
                            }
                        }
                        else if (lastRawDeviceWasExternal &&
                                 unchecked(Environment.TickCount - lastExternalInputTick) <= 180)
                        {
                            return Win32Api.CallNextHookEx(hookId, nCode, wParam, lParam);
                        }

                        lastSuppressedVk = hookStruct.vkCode;
                        lastSuppressedScan = hookStruct.scanCode;
                        lastSuppressedFlags = hookStruct.flags;
                        lastSuppressedWasDown = isKeyDown;
                        lastSuppressedTick = Environment.TickCount;
                    }

                    // 5. 完整攔截筆電內建鍵盤（或模式一所有鍵盤）按鍵事件
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
            if (rawInputWindow != null)
            {
                if (rawInputWindow.Handle != IntPtr.Zero)
                {
                    rawInputWindow.DestroyHandle();
                }
                rawInputWindow = null;
            }
            GC.SuppressFinalize(this);
        }

        private sealed class RawInputSinkWindow : NativeWindow
        {
            private readonly KeyboardHookManager owner;

            public RawInputSinkWindow(KeyboardHookManager parent)
            {
                this.owner = parent;
                CreateHandle(new CreateParams());
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == Win32Api.WM_INPUT)
                {
                    owner.OnRawInputMessageReceived(m.LParam);
                }
                base.WndProc(ref m);
            }
        }
    }
}
