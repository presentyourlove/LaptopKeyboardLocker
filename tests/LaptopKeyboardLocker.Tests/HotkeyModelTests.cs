using System;
using System.Windows.Forms;
using LaptopKeyboardLocker.Core;
using LaptopKeyboardLocker.Core.Native;

namespace LaptopKeyboardLocker.Tests
{
    public static class HotkeyModelTests
    {
        public static void RunAll(TestAssert assert)
        {
            assert.Section("HotkeyModel 單元測試 (Hotkey & Validation Logic)");

            // 1. 預設值測試
            HotkeyModel def = new HotkeyModel();
            assert.IsTrue(def.Control && def.Alt && !def.Shift && !def.Windows && def.KeyCode == Keys.L,
                "預設熱鍵應為 Ctrl + Alt + L");
            assert.AreEqual("Ctrl + Alt + L", def.ToString(),
                "ToString() 應正確格式化為 'Ctrl + Alt + L'");

            // 2. 防誤設單鍵驗證規則 (方案 A)
            HotkeyModel singleLetter = new HotkeyModel(false, false, false, false, Keys.A);
            assert.IsFalse(singleLetter.IsValid(),
                "單一普通字母鍵 'A' (無修飾鍵) 應被判定為無效熱鍵");

            HotkeyModel modifierOnly = new HotkeyModel(true, false, false, false, Keys.ControlKey);
            assert.IsFalse(modifierOnly.IsValid(),
                "單純修飾鍵 ControlKey 不可作為有效熱鍵");

            HotkeyModel standaloneF12 = new HotkeyModel(false, false, false, false, Keys.F12);
            assert.IsTrue(standaloneF12.IsValid(),
                "特殊功能鍵 F12 應允許單鍵使用");

            HotkeyModel validCustom = new HotkeyModel(true, false, true, false, Keys.K);
            assert.IsTrue(validCustom.IsValid(),
                "組合鍵 Ctrl + Shift + K 應判定為有效熱鍵");

            // 3. 內部修飾鍵狀態比對 (IsMatch 多載)
            assert.IsTrue(def.IsMatch(Keys.L, true, true, false, false),
                "當 Ctrl 與 Alt 按下且按鍵為 L 時，IsMatch 應回傳 true");
            assert.IsFalse(def.IsMatch(Keys.L, true, false, false, false),
                "當缺少 Alt 修飾鍵時，IsMatch 應回傳 false");
            assert.IsFalse(def.IsMatch(Keys.L, true, true, false, true),
                "當多出未預期的 Win 修飾鍵時，IsMatch 應回傳 false");
            assert.IsFalse(def.IsMatch(Keys.K, true, true, false, false),
                "當主鍵碼不符時，IsMatch 應回傳 false");

            // 4. Win32 修飾鍵旗標轉換與 Clone / ResetToDefault
            uint flags = validCustom.GetWin32Modifiers();
            uint expectedFlags = Win32Api.MOD_CONTROL | Win32Api.MOD_SHIFT | Win32Api.MOD_NOREPEAT;
            assert.AreEqual((int)expectedFlags, (int)flags,
                "GetWin32Modifiers() 應正確計算 Win32 修飾鍵旗標");

            HotkeyModel cloned = validCustom.Clone();
            assert.AreEqual("Ctrl + Shift + K", cloned.ToString(),
                "Clone() 應完整複製熱鍵屬性");

            cloned.ResetToDefault();
            assert.AreEqual("Ctrl + Alt + L", cloned.ToString(),
                "ResetToDefault() 應將熱鍵還原為 Ctrl + Alt + L");
        }
    }
}

