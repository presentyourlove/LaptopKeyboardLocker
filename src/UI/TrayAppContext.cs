using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Media;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;
using LaptopKeyboardLocker.Core;
using LaptopKeyboardLocker.Core.Native;

namespace LaptopKeyboardLocker.UI
{
    /// <summary>
    /// 系統匣常駐應用程式上下文，負責協調雙模鎖定引擎、全域熱鍵、300ms 硬體防抖動監聽與六重防鎖死安全機制。
    /// </summary>
    public class TrayAppContext : ApplicationContext
    {
        private const int GlobalHotkeyId = 9001;
        private const int DeviceDebounceIntervalMs = 300;
        private const int TimeoutCheckIntervalMs = 5000;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        private readonly LockStateManager stateManager;
        private readonly KeyboardHookManager hookManager;
        private readonly DeviceControlManager deviceManager;
        private readonly MessageReceiverWindow msgWindow;

        private NotifyIcon trayIcon;
        private ContextMenuStrip trayMenu;
        private ToolStripMenuItem menuOpen;
        private ToolStripMenuItem menuToggleLock;
        private ToolStripMenuItem menuExit;

        private MainForm mainForm;
        private OverlayHudForm hudForm;
        private Icon unlockedIcon;
        private Icon lockedIcon;

        private Timer debounceDeviceTimer;
        private Timer safetyTimeoutTimer;
        private bool hasShownMinimizeBalloon = false;
        private bool isDisposed = false;

        public bool IsLocked
        {
            get { return stateManager.IsLocked; }
        }

        public TrayAppContext(bool startMinimized, bool startLocked)
        {
            LocalizationManager.CurrentLanguage = ConfigManager.Current.Language;

            stateManager = new LockStateManager
            {
                CurrentMode = ConfigManager.Current.SelectedMode,
                TimeoutMinutes = ConfigManager.Current.AutoUnlockTimeoutMinutes
            };
            stateManager.LockStateChanged += OnLockStateChanged;
            stateManager.AutoUnlockTriggered += OnAutoUnlockTriggered;
            stateManager.ExternalKeyboardsLostTriggered += OnExternalKeyboardsLostTriggered;

            hookManager = new KeyboardHookManager();
            hookManager.UnlockRequested += OnHookUnlockRequested;

            deviceManager = new DeviceControlManager();

            // 啟動自檢復原：若目前具備管理員權限，啟動時先復原任何上次異常殘留停用的內建鍵盤
            if (DeviceControlManager.IsAdministrator())
            {
                deviceManager.EnableInternalKeyboard();
            }

            CreateDynamicIcons();
            InitializeTrayIcon();

            msgWindow = new MessageReceiverWindow(this);
            RegisterGlobalHotkey();

            // 300ms 硬體變更防抖動計時器
            debounceDeviceTimer = new Timer { Interval = DeviceDebounceIntervalMs };
            debounceDeviceTimer.Tick += OnDebounceDeviceTimerTick;

            // 防呆逾時定時檢查器
            safetyTimeoutTimer = new Timer { Interval = TimeoutCheckIntervalMs };
            safetyTimeoutTimer.Tick += OnSafetyTimeoutTimerTick;
            safetyTimeoutTimer.Start();

            // 掛載系統關機、睡眠與螢幕鎖定事件
            SystemEvents.SessionEnding += OnSessionEnding;
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
            SystemEvents.SessionSwitch += OnSessionSwitch;

            hudForm = new OverlayHudForm();
            hudForm.UpdateHotkeyDisplay(ConfigManager.Current.Hotkey.ToString());

            mainForm = new MainForm(this);
            LocalizationManager.LanguageChanged += OnLanguageChanged;

            if (!startMinimized)
            {
                mainForm.Show();
            }

            if (startLocked)
            {
                PerformLock();
            }
        }

        private void CreateDynamicIcons()
        {
            unlockedIcon = BuildStatusIcon(Color.FromArgb(40, 167, 69)); // 綠色代表正常未鎖定
            lockedIcon = BuildStatusIcon(Color.FromArgb(220, 53, 69));   // 紅色代表已鎖定
        }

        private static Icon BuildStatusIcon(Color badgeColor)
        {
            using (Bitmap bmp = new Bitmap(32, 32))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                using (SolidBrush bgBrush = new SolidBrush(badgeColor))
                {
                    g.FillEllipse(bgBrush, 2, 2, 28, 28);
                }

                using (Pen borderPen = new Pen(Color.White, 2.2f))
                {
                    g.DrawEllipse(borderPen, 2, 2, 28, 28);
                    // 繪製鎖頭簡約符號
                    g.DrawRectangle(borderPen, 10, 15, 12, 9);
                    g.DrawArc(borderPen, 12, 9, 8, 10, 180, 180);
                }

                IntPtr hIcon = bmp.GetHicon();
                try
                {
                    using (Icon tempIcon = Icon.FromHandle(hIcon))
                    {
                        return (Icon)tempIcon.Clone();
                    }
                }
                finally
                {
                    DestroyIcon(hIcon);
                }
            }
        }

        private void InitializeTrayIcon()
        {
            trayMenu = new ContextMenuStrip();
            menuOpen = new ToolStripMenuItem(LocalizationManager.Get("TrayShow"), null, OnMenuOpenClick);
            menuToggleLock = new ToolStripMenuItem(LocalizationManager.Get("TrayLock"), null, OnMenuToggleLockClick);
            menuExit = new ToolStripMenuItem(LocalizationManager.Get("TrayExit"), null, OnMenuExitClick);

            menuOpen.Font = new Font(trayMenu.Font, FontStyle.Bold);
            trayMenu.Items.Add(menuOpen);
            trayMenu.Items.Add(menuToggleLock);
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add(menuExit);

            trayIcon = new NotifyIcon
            {
                Icon = unlockedIcon,
                Text = LocalizationManager.Get("TrayTipUnlocked"),
                ContextMenuStrip = trayMenu,
                Visible = true
            };
            trayIcon.DoubleClick += OnTrayIconDoubleClick;
        }

        private void OnLanguageChanged(object sender, EventArgs e)
        {
            UpdateTrayLocalization();
        }

        private void UpdateTrayLocalization()
        {
            if (menuOpen != null) menuOpen.Text = LocalizationManager.Get("TrayShow");
            if (menuToggleLock != null)
            {
                menuToggleLock.Text = stateManager.IsLocked
                    ? LocalizationManager.Get("TrayUnlock")
                    : LocalizationManager.Get("TrayLock");
            }
            if (menuExit != null) menuExit.Text = LocalizationManager.Get("TrayExit");
            if (trayIcon != null)
            {
                trayIcon.Text = stateManager.IsLocked
                    ? LocalizationManager.Get("TrayTipLocked")
                    : LocalizationManager.Get("TrayTipUnlocked");
            }
        }

        public bool RefreshGlobalHotkeyRegistration()
        {
            UnregisterGlobalHotkey();
            hookManager.RefreshCachedHotkey();
            if (hudForm != null)
            {
                hudForm.UpdateHotkeyDisplay(ConfigManager.Current.Hotkey.ToString());
            }
            if (!stateManager.IsLocked || stateManager.CurrentMode == LockMode.HardwareDisable)
            {
                return RegisterGlobalHotkey();
            }
            return true;
        }

        private bool RegisterGlobalHotkey()
        {
            if (msgWindow == null || msgWindow.Handle == IntPtr.Zero) return false;
            HotkeyModel hk = ConfigManager.Current.Hotkey;
            bool ok = Win32Api.RegisterHotKey(msgWindow.Handle, GlobalHotkeyId, hk.GetWin32Modifiers(), (uint)hk.KeyCode);
            if (!ok)
            {
                DiagnosticLogger.Warn("RegisterHotKeyOccupied", hk.ToString());
            }
            return ok;
        }

        private void UnregisterGlobalHotkey()
        {
            if (msgWindow == null || msgWindow.Handle == IntPtr.Zero) return;
            Win32Api.UnregisterHotKey(msgWindow.Handle, GlobalHotkeyId);
        }

        public void ApplyUpdatedSettings()
        {
            stateManager.TimeoutMinutes = ConfigManager.Current.AutoUnlockTimeoutMinutes;
            if (hudForm != null)
            {
                hudForm.UpdateHotkeyDisplay(ConfigManager.Current.Hotkey.ToString());
                if (stateManager.IsLocked && ConfigManager.Current.ShowOverlayHud)
                {
                    hudForm.Show();
                }
                else
                {
                    hudForm.Hide();
                }
            }
        }

        public void ShowFirstMinimizeBalloon()
        {
            if (!hasShownMinimizeBalloon && trayIcon != null)
            {
                hasShownMinimizeBalloon = true;
                trayIcon.ShowBalloonTip(
                    2500,
                    LocalizationManager.Get("TrayMinimizedTitle"),
                    LocalizationManager.Get("TrayMinimizedText"),
                    ToolTipIcon.Info
                );
            }
        }

        public void ToggleLockFromUi()
        {
            if (stateManager.IsInCooldown(DateTime.UtcNow))
            {
                return;
            }

            if (stateManager.IsLocked)
            {
                PerformUnlock(false);
            }
            else
            {
                PerformLock();
            }
        }

        private void PerformLock()
        {
            if (stateManager.IsLocked || stateManager.IsInCooldown(DateTime.UtcNow))
            {
                return;
            }

            LockMode targetMode = ConfigManager.Current.SelectedMode;

            if (targetMode == LockMode.HardwareDisable)
            {
                if (!DeviceControlManager.IsAdministrator())
                {
                    if (mainForm != null && mainForm.Visible)
                    {
                        DialogResult r = MessageBox.Show(
                            mainForm,
                            LocalizationManager.Get("UacPromptMessage"),
                            LocalizationManager.Get("UacPromptTitle"),
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Question
                        );
                        if (r == DialogResult.Yes)
                        {
                            RestartElevated();
                        }
                    }
                    return;
                }

                DeviceDisableResult disableResult = deviceManager.DisableInternalKeyboard();
                if (disableResult == DeviceDisableResult.Success)
                {
                    stateManager.Lock(LockMode.HardwareDisable);
                    PlaySoundFeedback(true);
                    return;
                }
                else if (disableResult == DeviceDisableResult.NeedRestart ||
                         disableResult == DeviceDisableResult.DeviceNotFound ||
                         disableResult == DeviceDisableResult.Failed)
                {
                    // 方案 A：PS/2 硬體限制智慧降級詢問
                    DialogResult fallbackRes = MessageBox.Show(
                        mainForm,
                        LocalizationManager.Get("Ps2FallbackMessage"),
                        LocalizationManager.Get("Ps2FallbackTitle"),
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Information
                    );

                    if (fallbackRes == DialogResult.Yes)
                    {
                        ConfigManager.Current.SelectedMode = LockMode.SoftwareHook;
                        ConfigManager.Save(ConfigManager.Current);
                        if (mainForm != null)
                        {
                            mainForm.SelectSoftwareModeInUi();
                        }
                        targetMode = LockMode.SoftwareHook;
                    }
                    else
                    {
                        return;
                    }
                }
            }

            // 模式一：全域軟體攔截（進入模式一時暫停 WM_HOTKEY，由低階 Hook 負責解鎖，防止雙重觸發）
            UnregisterGlobalHotkey();
            if (hookManager.StartLock())
            {
                stateManager.Lock(LockMode.SoftwareHook);
                PlaySoundFeedback(true);
            }
            else
            {
                RegisterGlobalHotkey();
            }
        }

        public void PerformUnlock(bool forceBypassCooldown)
        {
            if (!stateManager.IsLocked) return;
            if (!forceBypassCooldown && stateManager.IsInCooldown(DateTime.UtcNow))
            {
                return;
            }

            if (hookManager.IsLocked)
            {
                hookManager.StopLock();
            }

            if (stateManager.CurrentMode == LockMode.HardwareDisable && DeviceControlManager.IsAdministrator())
            {
                deviceManager.EnableInternalKeyboard();
            }

            stateManager.Unlock(DateTime.UtcNow, forceBypassCooldown);
            RegisterGlobalHotkey();
            PlaySoundFeedback(false);
        }

        private void PlaySoundFeedback(bool locked)
        {
            if (!ConfigManager.Current.PlayNotificationSound) return;
            try
            {
                if (locked)
                {
                    SystemSounds.Exclamation.Play();
                }
                else
                {
                    SystemSounds.Asterisk.Play();
                }
            }
            catch
            {
            }
        }

        private void OnHookUnlockRequested(object sender, EventArgs e)
        {
            PerformUnlock(false);
        }

        private void OnLockStateChanged(object sender, EventArgs e)
        {
            bool locked = stateManager.IsLocked;

            if (trayIcon != null)
            {
                trayIcon.Icon = locked ? lockedIcon : unlockedIcon;
            }

            UpdateTrayLocalization();

            if (mainForm != null)
            {
                mainForm.UpdateLockStateUi();
            }

            if (hudForm != null)
            {
                if (locked && ConfigManager.Current.ShowOverlayHud)
                {
                    hudForm.UpdateHotkeyDisplay(ConfigManager.Current.Hotkey.ToString());
                    hudForm.Show();
                }
                else
                {
                    hudForm.Hide();
                }
            }
        }

        private void OnAutoUnlockTriggered(object sender, EventArgs e)
        {
            DiagnosticLogger.Info("AutoTimeoutUnlock", "Timeout reached");
            if (hookManager.IsLocked) hookManager.StopLock();
            if (DeviceControlManager.IsAdministrator()) deviceManager.EnableInternalKeyboard();
            RegisterGlobalHotkey();
        }

        private void OnExternalKeyboardsLostTriggered(object sender, EventArgs e)
        {
            DiagnosticLogger.Warn("ExternalKeyboardsDisconnected", "Auto restoring internal keyboard");
            if (DeviceControlManager.IsAdministrator())
            {
                deviceManager.EnableInternalKeyboard();
            }
            RegisterGlobalHotkey();

            if (trayIcon != null)
            {
                trayIcon.ShowBalloonTip(
                    3500,
                    LocalizationManager.Get("AppTitle"),
                    LocalizationManager.Get("ExternalDisconnectedNotice"),
                    ToolTipIcon.Warning
                );
            }
        }

        internal void OnGlobalHotkeyTriggered()
        {
            ToggleLockFromUi();
        }

        internal void OnDeviceChangeBroadcastReceived()
        {
            // 重新啟動 300ms 防抖動計時器，合併連續廣播
            if (debounceDeviceTimer != null)
            {
                debounceDeviceTimer.Stop();
                debounceDeviceTimer.Start();
            }
        }

        private void OnDebounceDeviceTimerTick(object sender, EventArgs e)
        {
            debounceDeviceTimer.Stop();

            if (stateManager.IsLocked && stateManager.CurrentMode == LockMode.HardwareDisable)
            {
                bool hasExternal = deviceManager.HasConnectedExternalKeyboard();
                stateManager.CheckExternalKeyboardsConnection(
                    hasExternal,
                    ConfigManager.Current.AutoRestoreOnDisconnect
                );
            }
        }

        private void OnSafetyTimeoutTimerTick(object sender, EventArgs e)
        {
            if (stateManager.IsLocked)
            {
                stateManager.CheckTimeout(DateTime.UtcNow);
            }
        }

        private void OnSessionEnding(object sender, SessionEndingEventArgs e)
        {
            DiagnosticLogger.Info("SessionEnding", "Restoring keyboard before shutdown/logoff");
            PerformUnlock(true);
        }

        private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.Suspend)
            {
                DiagnosticLogger.Info("PowerSuspend", "Restoring keyboard before sleep/hibernate");
                PerformUnlock(true);
            }
        }

        private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            // 當使用者按 Win + L 鎖屏或解鎖返回桌面時，重置低階 Hook 內部的修飾鍵狀態，防止 Win 鍵殘留
            if (hookManager != null)
            {
                hookManager.ResetModifierStates();
            }
        }

        private void OnTrayIconDoubleClick(object sender, EventArgs e)
        {
            // 機制 C：若目前處於鎖定狀態，連點兩下系統匣圖示立即緊急解鎖！
            if (stateManager.IsLocked)
            {
                PerformUnlock(true);
                return;
            }

            ShowMainDashboard();
        }

        private void OnMenuOpenClick(object sender, EventArgs e)
        {
            ShowMainDashboard();
        }

        private void OnMenuToggleLockClick(object sender, EventArgs e)
        {
            ToggleLockFromUi();
        }

        private void OnMenuExitClick(object sender, EventArgs e)
        {
            ExitCleanly();
        }

        public void ShowMainDashboard()
        {
            if (mainForm != null)
            {
                if (!mainForm.Visible)
                {
                    mainForm.Show();
                }
                if (mainForm.WindowState == FormWindowState.Minimized)
                {
                    mainForm.WindowState = FormWindowState.Normal;
                }
                mainForm.BringToFront();
                mainForm.Activate();
            }
        }

        public void RestartElevated()
        {
            try
            {
                PerformUnlock(true);

                // 帶入 --elevated 參數，讓新啟動的管理員實例等待最多 5 秒待舊實例釋放 Mutex
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = Application.ExecutablePath,
                    Arguments = "--elevated",
                    UseShellExecute = true,
                    Verb = "runas"
                };
                Process.Start(psi);
                ExitCleanly();
            }
            catch (Exception ex)
            {
                DiagnosticLogger.Warn("ElevateCancelled", ex.Message);
            }
        }

        public void ExitCleanly()
        {
            Dispose(true);
            Application.Exit();
        }

        protected override void Dispose(bool disposing)
        {
            if (!isDisposed)
            {
                if (disposing)
                {
                    PerformUnlock(true);
                    UnregisterGlobalHotkey();

                    if (safetyTimeoutTimer != null)
                    {
                        safetyTimeoutTimer.Stop();
                        safetyTimeoutTimer.Dispose();
                        safetyTimeoutTimer = null;
                    }

                    if (debounceDeviceTimer != null)
                    {
                        debounceDeviceTimer.Stop();
                        debounceDeviceTimer.Dispose();
                        debounceDeviceTimer = null;
                    }

                    SystemEvents.SessionEnding -= OnSessionEnding;
                    SystemEvents.PowerModeChanged -= OnPowerModeChanged;
                    SystemEvents.SessionSwitch -= OnSessionSwitch;
                    LocalizationManager.LanguageChanged -= OnLanguageChanged;

                    if (hookManager != null)
                    {
                        hookManager.Dispose();
                    }

                    if (trayIcon != null)
                    {
                        trayIcon.Visible = false;
                        trayIcon.Dispose();
                        trayIcon = null;
                    }

                    if (trayMenu != null)
                    {
                        trayMenu.Dispose();
                        trayMenu = null;
                    }

                    if (unlockedIcon != null)
                    {
                        unlockedIcon.Dispose();
                        unlockedIcon = null;
                    }

                    if (lockedIcon != null)
                    {
                        lockedIcon.Dispose();
                        lockedIcon = null;
                    }

                    if (hudForm != null)
                    {
                        hudForm.Close();
                        hudForm.Dispose();
                        hudForm = null;
                    }

                    if (mainForm != null)
                    {
                        mainForm.SetAllowRealClose(true);
                        mainForm.Close();
                        mainForm.Dispose();
                        mainForm = null;
                    }

                    if (msgWindow != null && msgWindow.Handle != IntPtr.Zero)
                    {
                        msgWindow.DestroyHandle();
                    }
                }

                isDisposed = true;
            }

            base.Dispose(disposing);
        }

        /// <summary>
        /// 接收全域熱鍵 WM_HOTKEY 與硬體變更廣播 WM_DEVICECHANGE 的隱藏訊息視窗。
        /// </summary>
        private sealed class MessageReceiverWindow : NativeWindow
        {
            private readonly TrayAppContext parent;

            public MessageReceiverWindow(TrayAppContext owner)
            {
                this.parent = owner;
                CreateHandle(new CreateParams());
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == Win32Api.WM_HOTKEY && m.WParam.ToInt32() == GlobalHotkeyId)
                {
                    parent.OnGlobalHotkeyTriggered();
                    return;
                }

                if (m.Msg == Win32Api.WM_DEVICECHANGE && m.WParam.ToInt32() == Win32Api.DBT_DEVNODES_CHANGED)
                {
                    parent.OnDeviceChangeBroadcastReceived();
                }

                base.WndProc(ref m);
            }
        }
    }
}
