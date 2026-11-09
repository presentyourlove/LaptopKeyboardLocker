using System;
using System.Threading;
using System.Windows.Forms;
using LaptopKeyboardLocker.Core;
using LaptopKeyboardLocker.Core.Native;
using LaptopKeyboardLocker.UI;

namespace LaptopKeyboardLocker
{
    internal static class Program
    {
        private const string MutexName = @"Local\LaptopKeyboardLocker_SingleInstance_Mutex";

        [STAThread]
        private static void Main(string[] args)
        {
            // 1. 啟用 High-DPI 原生防模糊支援
            try
            {
                Win32Api.SetProcessDPIAware();
            }
            catch
            {
            }

            bool startMinimized = false;
            bool startLocked = false;
            bool emergencyUnlockAndExit = false;

            if (args != null)
            {
                foreach (string rawArg in args)
                {
                    string arg = (rawArg ?? string.Empty).Trim().ToLowerInvariant();
                    if (arg == "--minimized" || arg == "--tray" || arg == "-m")
                    {
                        startMinimized = true;
                    }
                    else if (arg == "--lock" || arg == "-l")
                    {
                        startLocked = true;
                    }
                    else if (arg == "--unlock" || arg == "-u")
                    {
                        emergencyUnlockAndExit = true;
                    }
                }
            }

            // 2. 緊急救援指令 (--unlock)：強制復原硬體並立即退出
            if (emergencyUnlockAndExit)
            {
                DiagnosticLogger.Info("CliEmergencyUnlock", "Executing emergency unlock via CLI");
                EmergencyHardwareRestore();
                return;
            }

            // 3. 單一實例檢查 (Mutex)
            bool createdNew;
            using (Mutex mutex = new Mutex(true, MutexName, out createdNew))
            {
                if (!createdNew)
                {
                    return;
                }

                // 4. 掛載崩潰與退出防鎖死安全保護
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += OnThreadException;
                AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
                AppDomain.CurrentDomain.ProcessExit += OnProcessExit;

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                DiagnosticLogger.Info("AppStart", "Admin=" + DeviceControlManager.IsAdministrator());

                using (TrayAppContext context = new TrayAppContext(startMinimized, startLocked))
                {
                    Application.Run(context);
                }
            }
        }

        private static void OnThreadException(object sender, ThreadExceptionEventArgs e)
        {
            if (e.Exception != null)
            {
                DiagnosticLogger.Error("ThreadException", e.Exception.Message);
            }
            EmergencyHardwareRestore();
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = e.ExceptionObject as Exception;
            if (ex != null)
            {
                DiagnosticLogger.Error("UnhandledException", ex.Message);
            }
            EmergencyHardwareRestore();
        }

        private static void OnProcessExit(object sender, EventArgs e)
        {
            EmergencyHardwareRestore();
        }

        private static void EmergencyHardwareRestore()
        {
            try
            {
                if (DeviceControlManager.IsAdministrator())
                {
                    DeviceControlManager mgr = new DeviceControlManager();
                    mgr.EnableInternalKeyboard();
                }
            }
            catch
            {
            }
        }
    }
}

