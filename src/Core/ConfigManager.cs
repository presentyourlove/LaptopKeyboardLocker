using System;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace LaptopKeyboardLocker.Core
{
    public enum LockMode
    {
        /// <summary>
        /// 模式一：全域軟體攔截模式 (免 UAC，適合鍵盤清潔、防貓咪踩踏)
        /// </summary>
        SoftwareHook = 0,

        /// <summary>
        /// 模式二：內建鍵盤硬體停用模式 (需 UAC，外接鍵盤可打字)
        /// </summary>
        HardwareDisable = 1
    }

    /// <summary>
    /// 應用程式偏好設定模型。
    /// </summary>
    public class AppSettings
    {
        public AppLanguage Language { get; set; }
        public LockMode SelectedMode { get; set; }
        public HotkeyModel Hotkey { get; set; }
        public int AutoUnlockTimeoutMinutes { get; set; }
        public bool ShowOverlayHud { get; set; }
        public bool AutoRestoreOnDisconnect { get; set; }
        public bool StartWithWindows { get; set; }
        public bool PlayNotificationSound { get; set; }
        public bool CloseToTray { get; set; }

        public AppSettings()
        {
            Language = AppLanguage.TraditionalChinese;
            SelectedMode = LockMode.SoftwareHook;
            Hotkey = new HotkeyModel();
            AutoUnlockTimeoutMinutes = 15;
            ShowOverlayHud = true;
            AutoRestoreOnDisconnect = true;
            StartWithWindows = false;
            PlayNotificationSound = true;
            CloseToTray = true;
        }
    }

    /// <summary>
    /// 負責設定檔之讀取、儲存與防呆預設值復原。
    /// 支援「可攜優先（程式同目錄），若唯讀則自動回退至 %LOCALAPPDATA%」策略。
    /// </summary>
    public static class ConfigManager
    {
        private static string resolvedSettingsPath;
        private static AppSettings currentSettings;

        public static AppSettings Current
        {
            get
            {
                if (currentSettings == null)
                {
                    currentSettings = Load();
                }
                return currentSettings;
            }
        }

        /// <summary>
        /// 解析設定檔讀寫路徑：優先使用程式同目錄，若無寫入權限則自動回退至 LocalAppData。
        /// </summary>
        public static string GetSettingsFilePath()
        {
            if (!string.IsNullOrEmpty(resolvedSettingsPath))
            {
                return resolvedSettingsPath;
            }

            string portablePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.ini");
            string appDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LaptopKeyboardLocker"
            );
            string appDataPath = Path.Combine(appDataDir, "settings.ini");

            // 若同目錄已存在設定檔且可寫入，或同目錄可建立檔案，優先採用同目錄（可攜模式）
            try
            {
                string testProbe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".write_probe.tmp");
                File.WriteAllText(testProbe, "ok", Encoding.UTF8);
                File.Delete(testProbe);
                resolvedSettingsPath = portablePath;
                return resolvedSettingsPath;
            }
            catch
            {
                try
                {
                    if (!Directory.Exists(appDataDir))
                    {
                        Directory.CreateDirectory(appDataDir);
                    }
                    resolvedSettingsPath = appDataPath;
                }
                catch
                {
                    resolvedSettingsPath = portablePath;
                }
                return resolvedSettingsPath;
            }
        }

        public static AppSettings Load()
        {
            return LoadFromFile(GetSettingsFilePath());
        }

        public static AppSettings LoadFromFile(string filePath)
        {
            AppSettings settings = new AppSettings();

            if (!File.Exists(filePath))
            {
                SaveToFile(settings, filePath);
                return settings;
            }

            try
            {
                string[] lines = File.ReadAllLines(filePath, Encoding.UTF8);
                foreach (string rawLine in lines)
                {
                    string line = rawLine.Trim();
                    if (string.IsNullOrEmpty(line) || line.StartsWith("#") || line.StartsWith(";"))
                    {
                        continue;
                    }

                    int separatorIndex = line.IndexOf('=');
                    if (separatorIndex <= 0) continue;

                    string key = line.Substring(0, separatorIndex).Trim();
                    string val = line.Substring(separatorIndex + 1).Trim();

                    switch (key)
                    {
                        case "Language":
                            int langVal;
                            if (int.TryParse(val, out langVal) && Enum.IsDefined(typeof(AppLanguage), langVal))
                            {
                                settings.Language = (AppLanguage)langVal;
                            }
                            break;
                        case "SelectedMode":
                            int modeVal;
                            if (int.TryParse(val, out modeVal) && Enum.IsDefined(typeof(LockMode), modeVal))
                            {
                                settings.SelectedMode = (LockMode)modeVal;
                            }
                            break;
                        case "HotkeyCtrl":
                            settings.Hotkey.Control = (val.ToLowerInvariant() == "true");
                            break;
                        case "HotkeyAlt":
                            settings.Hotkey.Alt = (val.ToLowerInvariant() == "true");
                            break;
                        case "HotkeyShift":
                            settings.Hotkey.Shift = (val.ToLowerInvariant() == "true");
                            break;
                        case "HotkeyWin":
                            settings.Hotkey.Windows = (val.ToLowerInvariant() == "true");
                            break;
                        case "HotkeyKey":
                            try
                            {
                                Keys parsedKey = (Keys)Enum.Parse(typeof(Keys), val, true);
                                settings.Hotkey.KeyCode = parsedKey;
                            }
                            catch
                            {
                                settings.Hotkey.KeyCode = Keys.L;
                            }
                            break;
                        case "AutoUnlockTimeoutMinutes":
                            int timeoutVal;
                            if (int.TryParse(val, out timeoutVal))
                            {
                                settings.AutoUnlockTimeoutMinutes = Math.Max(0, timeoutVal);
                            }
                            break;
                        case "ShowOverlayHud":
                            settings.ShowOverlayHud = (val.ToLowerInvariant() == "true");
                            break;
                        case "AutoRestoreOnDisconnect":
                            settings.AutoRestoreOnDisconnect = (val.ToLowerInvariant() == "true");
                            break;
                        case "StartWithWindows":
                            settings.StartWithWindows = (val.ToLowerInvariant() == "true");
                            break;
                        case "PlayNotificationSound":
                            settings.PlayNotificationSound = (val.ToLowerInvariant() == "true");
                            break;
                        case "CloseToTray":
                            settings.CloseToTray = (val.ToLowerInvariant() == "true");
                            break;
                    }
                }

                // 防呆驗證：若設定檔內熱鍵遭手動修改為不合法單鍵，自動還原為預設 Ctrl + Alt + L
                if (!settings.Hotkey.IsValid())
                {
                    settings.Hotkey.ResetToDefault();
                }
            }
            catch (Exception ex)
            {
                DiagnosticLogger.Warn("ConfigLoadError", ex.Message);
                settings = new AppSettings();
            }

            return settings;
        }

        public static void Save(AppSettings settings)
        {
            SaveToFile(settings, GetSettingsFilePath());
            currentSettings = settings;
        }

        public static void SaveToFile(AppSettings settings, string filePath)
        {
            try
            {
                if (!settings.Hotkey.IsValid())
                {
                    settings.Hotkey.ResetToDefault();
                }

                StringBuilder sb = new StringBuilder();
                sb.AppendLine("# Laptop Keyboard Locker Configuration");
                sb.AppendLine("# 筆電鍵盤鎖定工具設定檔 (UTF-8)");
                sb.AppendLine("Language=" + (int)settings.Language);
                sb.AppendLine("SelectedMode=" + (int)settings.SelectedMode);
                sb.AppendLine("HotkeyCtrl=" + settings.Hotkey.Control.ToString().ToLowerInvariant());
                sb.AppendLine("HotkeyAlt=" + settings.Hotkey.Alt.ToString().ToLowerInvariant());
                sb.AppendLine("HotkeyShift=" + settings.Hotkey.Shift.ToString().ToLowerInvariant());
                sb.AppendLine("HotkeyWin=" + settings.Hotkey.Windows.ToString().ToLowerInvariant());
                sb.AppendLine("HotkeyKey=" + settings.Hotkey.KeyCode.ToString());
                sb.AppendLine("AutoUnlockTimeoutMinutes=" + settings.AutoUnlockTimeoutMinutes);
                sb.AppendLine("ShowOverlayHud=" + settings.ShowOverlayHud.ToString().ToLowerInvariant());
                sb.AppendLine("AutoRestoreOnDisconnect=" + settings.AutoRestoreOnDisconnect.ToString().ToLowerInvariant());
                sb.AppendLine("StartWithWindows=" + settings.StartWithWindows.ToString().ToLowerInvariant());
                sb.AppendLine("PlayNotificationSound=" + settings.PlayNotificationSound.ToString().ToLowerInvariant());
                sb.AppendLine("CloseToTray=" + settings.CloseToTray.ToString().ToLowerInvariant());

                File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                DiagnosticLogger.Warn("ConfigSaveError", ex.Message);
            }
        }
    }
}
