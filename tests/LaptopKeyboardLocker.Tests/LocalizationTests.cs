using System;
using LaptopKeyboardLocker.Core;

namespace LaptopKeyboardLocker.Tests
{
    public static class LocalizationTests
    {
        public static void RunAll(TestAssert assert)
        {
            assert.Section("LocalizationManager 單元測試 (i18n Bilingual Dictionary & Events)");

            // 1. 繁中與英文切換及事件通知測試
            bool eventFired = false;
            EventHandler handler = delegate { eventFired = true; };
            LocalizationManager.LanguageChanged += handler;

            try
            {
                LocalizationManager.CurrentLanguage = AppLanguage.TraditionalChinese;
                LocalizationManager.CurrentLanguage = AppLanguage.English;
                assert.IsTrue(eventFired, "切換語系時應觸發 LanguageChanged 事件");
                assert.AreEqual("Laptop Keyboard Locker (KeyLock)", LocalizationManager.Get("AppTitle"),
                    "英文語系下 AppTitle 應回傳英文標題");

                LocalizationManager.CurrentLanguage = AppLanguage.TraditionalChinese;
                assert.AreEqual("筆電鍵盤鎖定工具 (KeyLock)", LocalizationManager.Get("AppTitle"),
                    "繁體中文語系下 AppTitle 應回傳繁體中文標題");

                // 2. 驗證所有字典 Key 在中英雙語皆完整非空
                int keyCount = 0;
                bool allKeysPresentInBoth = true;
                foreach (string key in LocalizationManager.GetAllKeys())
                {
                    keyCount++;
                    LocalizationManager.CurrentLanguage = AppLanguage.TraditionalChinese;
                    string zhVal = LocalizationManager.Get(key);

                    LocalizationManager.CurrentLanguage = AppLanguage.English;
                    string enVal = LocalizationManager.Get(key);

                    if (string.IsNullOrEmpty(zhVal) || string.IsNullOrEmpty(enVal) || zhVal == key || enVal == key)
                    {
                        allKeysPresentInBoth = false;
                    }
                }

                assert.IsTrue(keyCount >= 25 && allKeysPresentInBoth,
                    "所有 " + keyCount + " 個介面多語系鍵值在繁中與英文字典中皆應完整存在且非空");

                // 3. 未知鍵值回退測試
                assert.AreEqual("NonExistentKey_123", LocalizationManager.Get("NonExistentKey_123"),
                    "查詢不存在的鍵值時應安全回退鍵名本身而不拋出例外");
            }
            finally
            {
                LocalizationManager.LanguageChanged -= handler;
                LocalizationManager.CurrentLanguage = AppLanguage.TraditionalChinese;
            }
        }
    }
}

