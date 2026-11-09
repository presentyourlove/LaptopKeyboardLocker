using System;
using System.Windows.Forms;
using Microsoft.Win32;

namespace LaptopKeyboardLocker.Core
{
    /// <summary>
    /// 負責管理「隨 Windows 開機自動靜默啟動」之註冊表設定 (HKCU Run，免管理員權限)。
    /// </summary>
    public static class StartupManager
    {
        private const string RunRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppRegistryValueName = "LaptopKeyboardLocker";

        /// <summary>
        /// 設定是否隨 Windows 開機自動啟動（附帶 --minimized 參數靜默駐留系統匣）。
        /// </summary>
        public static bool SetStartWithWindows(bool enable)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunRegistryPath, true))
                {
                    if (key == null) return false;

                    if (enable)
                    {
                        string exePath = Application.ExecutablePath;
                        string command = string.Format("\"{0}\" --minimized", exePath);
                        key.SetValue(AppRegistryValueName, command, RegistryValueKind.String);
                        DiagnosticLogger.Info("StartupEnabled", command);
                    }
                    else
                    {
                        if (key.GetValue(AppRegistryValueName) != null)
                        {
                            key.DeleteValue(AppRegistryValueName, false);
                            DiagnosticLogger.Info("StartupDisabled", AppRegistryValueName);
                        }
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                DiagnosticLogger.Warn("StartupRegistryError", ex.Message);
                return false;
            }
        }
    }
}

