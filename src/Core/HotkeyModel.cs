using System;
using System.Text;
using System.Windows.Forms;
using LaptopKeyboardLocker.Core.Native;

namespace LaptopKeyboardLocker.Core
{
    /// <summary>
    /// 定義使用者自訂的快速鍵模型、防誤設組合鍵驗證規則與匹配比對邏輯。
    /// </summary>
    public class HotkeyModel
    {
        public bool Control { get; set; }
        public bool Alt { get; set; }
        public bool Shift { get; set; }
        public bool Windows { get; set; }
        public Keys KeyCode { get; set; }

        public HotkeyModel()
        {
            ResetToDefault();
        }

        public HotkeyModel(bool ctrl, bool alt, bool shift, bool win, Keys key)
        {
            Control = ctrl;
            Alt = alt;
            Shift = shift;
            Windows = win;
            KeyCode = key;
        }

        /// <summary>
        /// 還原為系統預設熱鍵 (Ctrl + Alt + L)。
        /// </summary>
        public void ResetToDefault()
        {
            Control = true;
            Alt = true;
            Shift = false;
            Windows = false;
            KeyCode = Keys.L;
        }

        /// <summary>
        /// 檢查某個鍵碼本身是否為修飾鍵（不可單獨作為主觸發鍵）。
        /// </summary>
        public static bool IsModifierKeyOnly(Keys key)
        {
            return key == Keys.ControlKey || key == Keys.LControlKey || key == Keys.RControlKey ||
                   key == Keys.Menu || key == Keys.LMenu || key == Keys.RMenu ||
                   key == Keys.ShiftKey || key == Keys.LShiftKey || key == Keys.RShiftKey ||
                   key == Keys.LWin || key == Keys.RWin || key == Keys.None;
        }

        /// <summary>
        /// 檢查某個鍵碼是否為允許免修飾鍵獨立使用的特殊功能鍵 (F1~F12, Pause)。
        /// </summary>
        public static bool IsStandaloneFunctionKey(Keys key)
        {
            return (key >= Keys.F1 && key <= Keys.F12) || key == Keys.Pause;
        }

        /// <summary>
        /// 驗證此熱鍵設定是否符合防誤設安全規範（方案 A）：
        /// 1. 主鍵不可為空或單純修飾鍵。
        /// 2. 除非是 F1~F12 或 Pause，否則必須至少搭配一個修飾鍵 (Ctrl/Alt/Shift/Win)。
        /// </summary>
        public bool IsValid()
        {
            if (IsModifierKeyOnly(KeyCode))
            {
                return false;
            }

            bool hasAnyModifier = Control || Alt || Shift || Windows;
            if (!hasAnyModifier && !IsStandaloneFunctionKey(KeyCode))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 轉換為 Win32 RegisterHotKey 所需的修飾鍵旗標組合。
        /// </summary>
        public uint GetWin32Modifiers()
        {
            uint modifiers = 0;
            if (Alt) modifiers |= Win32Api.MOD_ALT;
            if (Control) modifiers |= Win32Api.MOD_CONTROL;
            if (Shift) modifiers |= Win32Api.MOD_SHIFT;
            if (Windows) modifiers |= Win32Api.MOD_WIN;
            modifiers |= Win32Api.MOD_NOREPEAT;
            return modifiers;
        }

        /// <summary>
        /// 依據傳入的實體修飾鍵狀態檢查是否匹配此熱鍵組合（由 KeyboardHookManager 內部狀態表呼叫，無懼 Hook 攔截影響）。
        /// </summary>
        public bool IsMatch(Keys pressedKey, bool isCtrlDown, bool isAltDown, bool isShiftDown, bool isWinDown)
        {
            if (pressedKey != KeyCode)
            {
                return false;
            }

            return isCtrlDown == Control &&
                   isAltDown == Alt &&
                   isShiftDown == Shift &&
                   isWinDown == Windows;
        }

        /// <summary>
        /// 相容輔助方法：透過 Win32 GetAsyncKeyState 檢查當前系統按鍵狀態。
        /// </summary>
        public bool IsMatch(Keys pressedKey)
        {
            if (pressedKey != KeyCode)
            {
                return false;
            }

            const int keyStatePressedMask = 0x8000;
            bool isCtrlDown = ((Win32Api.GetAsyncKeyState(Win32Api.VK_LCONTROL) & keyStatePressedMask) != 0) ||
                              ((Win32Api.GetAsyncKeyState(Win32Api.VK_RCONTROL) & keyStatePressedMask) != 0) ||
                              ((Win32Api.GetAsyncKeyState((int)Keys.ControlKey) & keyStatePressedMask) != 0);
            bool isAltDown = ((Win32Api.GetAsyncKeyState(Win32Api.VK_LMENU) & keyStatePressedMask) != 0) ||
                             ((Win32Api.GetAsyncKeyState(Win32Api.VK_RMENU) & keyStatePressedMask) != 0) ||
                             ((Win32Api.GetAsyncKeyState((int)Keys.Menu) & keyStatePressedMask) != 0);
            bool isShiftDown = ((Win32Api.GetAsyncKeyState(Win32Api.VK_LSHIFT) & keyStatePressedMask) != 0) ||
                               ((Win32Api.GetAsyncKeyState(Win32Api.VK_RSHIFT) & keyStatePressedMask) != 0) ||
                               ((Win32Api.GetAsyncKeyState((int)Keys.ShiftKey) & keyStatePressedMask) != 0);
            bool isWinDown = ((Win32Api.GetAsyncKeyState(Win32Api.VK_LWIN) & keyStatePressedMask) != 0) ||
                             ((Win32Api.GetAsyncKeyState(Win32Api.VK_RWIN) & keyStatePressedMask) != 0);

            return IsMatch(pressedKey, isCtrlDown, isAltDown, isShiftDown, isWinDown);
        }

        public HotkeyModel Clone()
        {
            return new HotkeyModel(Control, Alt, Shift, Windows, KeyCode);
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            if (Control) sb.Append("Ctrl + ");
            if (Alt) sb.Append("Alt + ");
            if (Shift) sb.Append("Shift + ");
            if (Windows) sb.Append("Win + ");
            sb.Append(KeyCode.ToString());
            return sb.ToString();
        }
    }
}
