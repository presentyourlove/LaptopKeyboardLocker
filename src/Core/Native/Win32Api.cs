using System;
using System.Runtime.InteropServices;
using System.Text;

namespace LaptopKeyboardLocker.Core.Native
{
    /// <summary>
    /// 提供 Windows 底層 Win32 API 交互介面，包含 Hook、熱鍵、裝置管理、High-DPI 與廣播通知。
    /// 嚴格遵循零 Magic Number 規範。
    /// </summary>
    internal static class Win32Api
    {
        #region Constants & Enums

        // 鍵盤低階攔截常數 (Low-Level Keyboard Hook)
        public const int WH_KEYBOARD_LL = 13;
        public const int WM_KEYDOWN = 0x0100;
        public const int WM_KEYUP = 0x0101;
        public const int WM_SYSKEYDOWN = 0x0104;
        public const int WM_SYSKEYUP = 0x0105;

        // 左右修飾鍵虛擬鍵碼 (Virtual Key Codes)
        public const int VK_LCONTROL = 0xA2;
        public const int VK_RCONTROL = 0xA3;
        public const int VK_LMENU = 0xA4;    // Left Alt
        public const int VK_RMENU = 0xA5;    // Right Alt
        public const int VK_LSHIFT = 0xA0;
        public const int VK_RSHIFT = 0xA1;
        public const int VK_LWIN = 0x5B;
        public const int VK_RWIN = 0x5C;

        // 熱鍵相關訊息與修飾鍵旗標
        public const int WM_HOTKEY = 0x0312;
        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_SHIFT = 0x0004;
        public const uint MOD_WIN = 0x0008;
        public const uint MOD_NOREPEAT = 0x4000;

        // 裝置變更廣播訊息 (用於外接鍵盤拔插感知)
        public const int WM_DEVICECHANGE = 0x0219;
        public const int DBT_DEVNODES_CHANGED = 0x0007;

        // CfgMgr32 設定管理員回傳碼
        public const uint CR_SUCCESS = 0x00000000;
        public const uint CR_NEED_RESTART = 0x00000017;
        public const uint CR_ACCESS_DENIED = 0x00000033;

        // CfgMgr32 裝置旗標
        public const uint CM_LOCATE_DEVNODE_NORMAL = 0x00000000;
        public const uint CM_DISABLE_PERSIST = 0x00000001;

        // Raw Input 與單一鍵盤來源辨識常數
        public const int WM_INPUT = 0x00FF;
        public const uint RID_INPUT = 0x10000003;
        public const uint RIDI_DEVICENAME = 0x20000007;
        public const uint RIDEV_INPUTSINK = 0x00000100;
        public const ushort HID_USAGE_PAGE_GENERIC = 0x01;
        public const ushort HID_USAGE_GENERIC_KEYBOARD = 0x06;
        public const uint PM_REMOVE = 0x0001;
        public const uint LLKHF_EXTENDED = 0x01;
        public const uint LLKHF_INJECTED = 0x10;
        public const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        public const uint KEYEVENTF_KEYUP = 0x0002;
        public const ushort RI_KEY_BREAK = 0x0001;
        public const ushort RI_KEY_E0 = 0x0002;

        #endregion

        #region Delegates & Structs

        public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        public struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public UIntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RAWINPUTDEVICE
        {
            public ushort usUsagePage;
            public ushort usUsage;
            public uint dwFlags;
            public IntPtr hwndTarget;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RAWINPUTHEADER
        {
            public uint dwType;
            public uint dwSize;
            public IntPtr hDevice;
            public IntPtr wParam;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RAWKEYBOARD
        {
            public ushort MakeCode;
            public ushort Flags;
            public ushort Reserved;
            public ushort VKey;
            public uint Message;
            public uint ExtraInformation;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RAWINPUTKEYBOARD
        {
            public RAWINPUTHEADER header;
            public RAWKEYBOARD keyboard;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MSG
        {
            public IntPtr hwnd;
            public uint message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public int pt_x;
            public int pt_y;
        }

        #endregion

        #region User32 APIs

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetProcessDPIAware();

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool RegisterRawInputDevices(
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] RAWINPUTDEVICE[] pRawInputDevices,
            uint uiNumDevices,
            uint cbSize);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetRawInputData(
            IntPtr hRawInput,
            uint uiCommand,
            out RAWINPUTKEYBOARD pData,
            ref uint pcbSize,
            uint cbSizeHeader);

        [DllImport("user32.dll", SetLastError = true, EntryPoint = "GetRawInputDeviceInfoW", CharSet = CharSet.Unicode)]
        public static extern uint GetRawInputDeviceInfo(
            IntPtr hDevice,
            uint uiCommand,
            StringBuilder pData,
            ref uint pcbSize);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool PeekMessage(
            out MSG lpMsg,
            IntPtr hWnd,
            uint wMsgFilterMin,
            uint wMsgFilterMax,
            uint wRemoveMsg);

        [DllImport("user32.dll")]
        public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        #endregion

        #region CfgMgr32 APIs (Device Node Control)

        [DllImport("cfgmgr32.dll", SetLastError = true, EntryPoint = "CM_Locate_DevNodeW", CharSet = CharSet.Unicode)]
        public static extern uint CM_Locate_DevNode(out uint pdnDevInst, string pDeviceID, uint ulFlags);

        [DllImport("cfgmgr32.dll", SetLastError = true)]
        public static extern uint CM_Disable_DevNode(uint dnDevInst, uint ulFlags);

        [DllImport("cfgmgr32.dll", SetLastError = true)]
        public static extern uint CM_Enable_DevNode(uint dnDevInst, uint ulFlags);

        [DllImport("cfgmgr32.dll", SetLastError = true, EntryPoint = "CM_Get_Device_IDW", CharSet = CharSet.Unicode)]
        public static extern uint CM_Get_Device_ID(uint dnDevInst, StringBuilder buffer, uint bufferLen, uint ulFlags);

        [DllImport("cfgmgr32.dll", SetLastError = true)]
        public static extern uint CM_Get_Device_ID_Size(out uint pulLen, uint dnDevInst, uint ulFlags);

        [DllImport("cfgmgr32.dll", SetLastError = true)]
        public static extern uint CM_Get_Parent(out uint pdnDevInst, uint dnDevInst, uint ulFlags);

        #endregion
    }
}
