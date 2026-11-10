using System;
using System.IO;
using System.Windows.Forms;
using LaptopKeyboardLocker.Core;

namespace LaptopKeyboardLocker.Tests
{
    public static class ConfigManagerTests
    {
        public static void RunAll(TestAssert assert)
        {
            assert.Section("ConfigManager & Build Script Integrity 單元測試 (Persistence & Build Safety)");

            string tempFile = Path.Combine(Path.GetTempPath(), "keylock_test_" + Guid.NewGuid().ToString("N") + ".ini");

            try
            {
                // 1. 完整儲存與讀取往返測試 (Round-trip)
                AppSettings custom = new AppSettings
                {
                    Language = AppLanguage.English,
                    SelectedMode = LockMode.HardwareDisable,
                    Hotkey = new HotkeyModel(true, false, true, false, Keys.U),
                    AutoUnlockTimeoutMinutes = 30,
                    ShowOverlayHud = false,
                    AutoRestoreOnDisconnect = true,
                    StartWithWindows = true,
                    PlayNotificationSound = false,
                    CloseToTray = true
                };

                ConfigManager.SaveToFile(custom, tempFile);
                AppSettings loaded = ConfigManager.LoadFromFile(tempFile);

                assert.AreEqual((int)AppLanguage.English, (int)loaded.Language,
                    "讀取之 Language 應與儲存值一致");
                assert.AreEqual((int)LockMode.HardwareDisable, (int)loaded.SelectedMode,
                    "讀取之 SelectedMode 應與儲存值一致");
                assert.AreEqual("Ctrl + Shift + U", loaded.Hotkey.ToString(),
                    "讀取之自訂熱鍵應為 Ctrl + Shift + U");
                assert.AreEqual(30, loaded.AutoUnlockTimeoutMinutes,
                    "讀取之 AutoUnlockTimeoutMinutes 應為 30");
                assert.IsFalse(loaded.ShowOverlayHud,
                    "讀取之 ShowOverlayHud 應為 false");
                assert.IsFalse(loaded.PlayNotificationSound,
                    "讀取之 PlayNotificationSound 應為 false");

                // 2. 防呆測試：若設定檔內為不合法單鍵熱鍵，載入時應自動校正為預設值 Ctrl + Alt + L
                File.WriteAllText(tempFile, "HotkeyCtrl=false\nHotkeyAlt=false\nHotkeyShift=false\nHotkeyWin=false\nHotkeyKey=A\n");
                AppSettings sanitized = ConfigManager.LoadFromFile(tempFile);
                assert.AreEqual("Ctrl + Alt + L", sanitized.Hotkey.ToString(),
                    "當設定檔遭手動改為不合法單鍵 'A' 時，應自動校正還原為預設 Ctrl + Alt + L");

                // 3. 損毀檔案容錯測試
                File.WriteAllText(tempFile, "InvalidLineWithoutEquals\nAutoUnlockTimeoutMinutes=NotANumber\n");
                AppSettings fallback = ConfigManager.LoadFromFile(tempFile);
                assert.AreEqual(15, fallback.AutoUnlockTimeoutMinutes,
                    "當設定檔格式異常時，應安全回退至預設 15 分鐘逾時設定");

                // 4. DiagnosticLogger JSON 字串轉義測試
                string escaped = DiagnosticLogger.EscapeJson("Path\\Test\"Quote\nLine");
                assert.AreEqual("Path\\\\Test\\\"Quote\\nLine", escaped,
                    "DiagnosticLogger.EscapeJson 應正確轉義反斜線、雙引號與換行字元");

                // 5. 建置腳本 (build.bat) 防止 echo 未轉義 '>' 重新導向污染執行檔之迴歸測試
                string buildBatPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "build.bat");
                if (File.Exists(buildBatPath))
                {
                    string[] batLines = File.ReadAllLines(buildBatPath);
                    bool hasDangerousEchoRedirect = false;
                    foreach (string rawLine in batLines)
                    {
                        string trimmed = rawLine.Trim();
                        if (trimmed.StartsWith("echo ", StringComparison.OrdinalIgnoreCase) && trimmed.Contains(">"))
                        {
                            hasDangerousEchoRedirect = true;
                        }
                    }
                    assert.IsFalse(hasDangerousEchoRedirect,
                        "build.bat 中的 echo 指令不可包含未轉義的 '>' 重新導向符號，防止覆寫破壞執行檔");
                }
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }
    }
}
