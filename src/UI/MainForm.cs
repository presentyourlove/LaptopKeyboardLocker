using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using LaptopKeyboardLocker.Core;

namespace LaptopKeyboardLocker.UI
{
    /// <summary>
    /// 主控制面板視窗，支援中英雙語即時切換、雙模選擇、自訂熱鍵錄製、安全防呆設定與 UAC 輔助提權。
    /// </summary>
    public class MainForm : Form
    {
        private readonly TrayAppContext appContext;
        private bool isRecordingHotkey = false;
        private bool allowRealClose = false;

        // 頂部語系與狀態列
        private Label lblLanguage;
        private ComboBox cmbLanguage;
        private Label lblStatus;
        private Label lblPrivilege;
        private Button btnElevate;
        private Button btnToggleLock;

        // 模式選擇區
        private GroupBox grpMode;
        private RadioButton rbModeSoftware;
        private RadioButton rbModeHardware;

        // 熱鍵設定區
        private GroupBox grpHotkey;
        private Label lblHotkey;
        private TextBox txtHotkey;
        private Button btnRecordHotkey;
        private Button btnResetHotkey;

        // 安全與防呆設定區
        private GroupBox grpSafety;
        private Label lblAutoTimeout;
        private NumericUpDown numTimeout;
        private Label lblUnitMinutes;
        private CheckBox chkRestoreOnDisconnect;
        private CheckBox chkShowHud;
        private CheckBox chkPlaySound;
        private CheckBox chkStartWithWindows;

        private bool isUpdatingUi = false;

        public MainForm(TrayAppContext context)
        {
            this.appContext = context;
            this.DoubleBuffered = true;
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.KeyPreview = true;

            InitializeComponent();
            LoadSettingsToUi();
            ApplyLocalization();
            UpdateLockStateUi();

            LocalizationManager.LanguageChanged += OnLanguageChanged;
        }

        public void SetAllowRealClose(bool allow)
        {
            allowRealClose = allow;
        }

        private void InitializeComponent()
        {
            this.Size = new Size(560, 520);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(248, 249, 250);
            this.Font = new Font("Microsoft JhengHei UI", 9.5f, FontStyle.Regular);

            // 1. 頂部語系選擇與權限狀態
            lblLanguage = new Label
            {
                Location = new Point(20, 18),
                Size = new Size(150, 24),
                TextAlign = ContentAlignment.MiddleLeft
            };

            cmbLanguage = new ComboBox
            {
                Location = new Point(170, 15),
                Size = new Size(150, 26),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbLanguage.Items.Add("繁體中文 (Chinese)");
            cmbLanguage.Items.Add("English");
            cmbLanguage.SelectedIndexChanged += OnCmbLanguageChanged;

            btnElevate = new Button
            {
                Location = new Point(365, 13),
                Size = new Size(160, 28),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(255, 243, 205),
                ForeColor = Color.FromArgb(133, 100, 4),
                Cursor = Cursors.Hand
            };
            btnElevate.Click += OnBtnElevateClick;

            lblStatus = new Label
            {
                Location = new Point(20, 52),
                Size = new Size(340, 26),
                Font = new Font("Microsoft JhengHei UI", 11.0f, FontStyle.Bold),
                ForeColor = Color.FromArgb(40, 167, 69)
            };

            lblPrivilege = new Label
            {
                Location = new Point(365, 54),
                Size = new Size(165, 22),
                Font = new Font("Microsoft JhengHei UI", 8.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(108, 117, 125),
                TextAlign = ContentAlignment.MiddleRight
            };

            // 2. 一鍵鎖定大按鈕
            btnToggleLock = new Button
            {
                Location = new Point(20, 85),
                Size = new Size(505, 50),
                Font = new Font("Microsoft JhengHei UI", 12.0f, FontStyle.Bold),
                BackColor = Color.FromArgb(0, 123, 255),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnToggleLock.FlatAppearance.BorderSize = 0;
            btnToggleLock.Click += OnBtnToggleLockClick;

            // 3. 模式選擇區
            grpMode = new GroupBox
            {
                Location = new Point(20, 148),
                Size = new Size(505, 92)
            };

            rbModeSoftware = new RadioButton
            {
                Location = new Point(16, 26),
                Size = new Size(475, 26),
                Checked = true
            };
            rbModeSoftware.CheckedChanged += OnModeRadioChanged;

            rbModeHardware = new RadioButton
            {
                Location = new Point(16, 56),
                Size = new Size(475, 26)
            };
            rbModeHardware.CheckedChanged += OnModeRadioChanged;

            grpMode.Controls.Add(rbModeSoftware);
            grpMode.Controls.Add(rbModeHardware);

            // 4. 快速鍵設定區
            grpHotkey = new GroupBox
            {
                Location = new Point(20, 250),
                Size = new Size(505, 72)
            };

            lblHotkey = new Label
            {
                Location = new Point(16, 30),
                Size = new Size(135, 24),
                TextAlign = ContentAlignment.MiddleLeft
            };

            txtHotkey = new TextBox
            {
                Location = new Point(155, 28),
                Size = new Size(160, 26),
                ReadOnly = true,
                TextAlign = HorizontalAlignment.Center,
                BackColor = Color.White,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
            };
            txtHotkey.Leave += OnTxtHotkeyLeave;

            btnRecordHotkey = new Button
            {
                Location = new Point(325, 26),
                Size = new Size(85, 28),
                UseVisualStyleBackColor = true,
                Cursor = Cursors.Hand
            };
            btnRecordHotkey.Click += OnBtnRecordHotkeyClick;

            btnResetHotkey = new Button
            {
                Location = new Point(415, 26),
                Size = new Size(76, 28),
                UseVisualStyleBackColor = true,
                Cursor = Cursors.Hand
            };
            btnResetHotkey.Click += OnBtnResetHotkeyClick;

            grpHotkey.Controls.Add(lblHotkey);
            grpHotkey.Controls.Add(txtHotkey);
            grpHotkey.Controls.Add(btnRecordHotkey);
            grpHotkey.Controls.Add(btnResetHotkey);

            // 5. 安全、防呆與偏好設定區
            grpSafety = new GroupBox
            {
                Location = new Point(20, 332),
                Size = new Size(505, 138)
            };

            lblAutoTimeout = new Label
            {
                Location = new Point(16, 25),
                Size = new Size(185, 24),
                TextAlign = ContentAlignment.MiddleLeft
            };

            numTimeout = new NumericUpDown
            {
                Location = new Point(205, 23),
                Size = new Size(65, 26),
                Minimum = 0,
                Maximum = 240,
                Value = 15
            };
            numTimeout.ValueChanged += OnSafetySettingChanged;

            lblUnitMinutes = new Label
            {
                Location = new Point(278, 25),
                Size = new Size(210, 24),
                TextAlign = ContentAlignment.MiddleLeft
            };

            chkRestoreOnDisconnect = new CheckBox
            {
                Location = new Point(16, 54),
                Size = new Size(475, 24),
                Checked = true
            };
            chkRestoreOnDisconnect.CheckedChanged += OnSafetySettingChanged;

            chkShowHud = new CheckBox
            {
                Location = new Point(16, 80),
                Size = new Size(245, 24),
                Checked = true
            };
            chkShowHud.CheckedChanged += OnSafetySettingChanged;

            chkPlaySound = new CheckBox
            {
                Location = new Point(265, 80),
                Size = new Size(230, 24),
                Checked = true
            };
            chkPlaySound.CheckedChanged += OnSafetySettingChanged;

            chkStartWithWindows = new CheckBox
            {
                Location = new Point(16, 106),
                Size = new Size(475, 24),
                Checked = false
            };
            chkStartWithWindows.CheckedChanged += OnSafetySettingChanged;

            grpSafety.Controls.Add(lblAutoTimeout);
            grpSafety.Controls.Add(numTimeout);
            grpSafety.Controls.Add(lblUnitMinutes);
            grpSafety.Controls.Add(chkRestoreOnDisconnect);
            grpSafety.Controls.Add(chkShowHud);
            grpSafety.Controls.Add(chkPlaySound);
            grpSafety.Controls.Add(chkStartWithWindows);

            this.Controls.Add(lblLanguage);
            this.Controls.Add(cmbLanguage);
            this.Controls.Add(btnElevate);
            this.Controls.Add(lblStatus);
            this.Controls.Add(lblPrivilege);
            this.Controls.Add(btnToggleLock);
            this.Controls.Add(grpMode);
            this.Controls.Add(grpHotkey);
            this.Controls.Add(grpSafety);
        }

        private void LoadSettingsToUi()
        {
            isUpdatingUi = true;
            try
            {
                AppSettings s = ConfigManager.Current;
                cmbLanguage.SelectedIndex = (s.Language == AppLanguage.English) ? 1 : 0;
                rbModeSoftware.Checked = (s.SelectedMode == LockMode.SoftwareHook);
                rbModeHardware.Checked = (s.SelectedMode == LockMode.HardwareDisable);
                txtHotkey.Text = s.Hotkey.ToString();
                numTimeout.Value = Math.Min(Math.Max(0, s.AutoUnlockTimeoutMinutes), 240);
                chkRestoreOnDisconnect.Checked = s.AutoRestoreOnDisconnect;
                chkShowHud.Checked = s.ShowOverlayHud;
                chkPlaySound.Checked = s.PlayNotificationSound;
                chkStartWithWindows.Checked = s.StartWithWindows;
            }
            finally
            {
                isUpdatingUi = false;
            }
        }

        private void OnLanguageChanged(object sender, EventArgs e)
        {
            ApplyLocalization();
            UpdateLockStateUi();
        }

        public void ApplyLocalization()
        {
            this.Text = LocalizationManager.Get("AppTitle");
            lblLanguage.Text = LocalizationManager.Get("LblLanguage");

            bool isAdmin = DeviceControlManager.IsAdministrator();
            lblPrivilege.Text = isAdmin
                ? LocalizationManager.Get("PrivAdmin")
                : LocalizationManager.Get("PrivStandard");
            btnElevate.Text = LocalizationManager.Get("BtnElevate");
            btnElevate.Visible = !isAdmin;

            grpMode.Text = LocalizationManager.Get("GrpMode");
            rbModeSoftware.Text = LocalizationManager.Get("ModeSoftware");
            rbModeHardware.Text = LocalizationManager.Get("ModeHardware");

            grpHotkey.Text = LocalizationManager.Get("GrpHotkey");
            lblHotkey.Text = LocalizationManager.Get("LblHotkey");
            btnRecordHotkey.Text = LocalizationManager.Get("BtnRecordHotkey");
            btnResetHotkey.Text = LocalizationManager.Get("BtnResetHotkey");

            grpSafety.Text = LocalizationManager.Get("GrpSafety");
            lblAutoTimeout.Text = LocalizationManager.Get("ChkAutoTimeout");
            lblUnitMinutes.Text = LocalizationManager.Get("UnitMinutes");
            chkRestoreOnDisconnect.Text = LocalizationManager.Get("ChkRestoreOnDisconnect");
            chkShowHud.Text = LocalizationManager.Get("ChkShowHud");
            chkPlaySound.Text = LocalizationManager.Get("ChkPlaySound");
            chkStartWithWindows.Text = LocalizationManager.Get("ChkStartWithWindows");
        }

        public void UpdateLockStateUi()
        {
            bool locked = appContext != null && appContext.IsLocked;
            if (locked)
            {
                lblStatus.Text = LocalizationManager.Get("StatusLocked");
                lblStatus.ForeColor = Color.FromArgb(220, 53, 69);
                btnToggleLock.Text = LocalizationManager.Get("BtnUnlock");
                btnToggleLock.BackColor = Color.FromArgb(220, 53, 69);
            }
            else
            {
                lblStatus.Text = LocalizationManager.Get("StatusUnlocked");
                lblStatus.ForeColor = Color.FromArgb(40, 167, 69);
                btnToggleLock.Text = LocalizationManager.Get("BtnLock");
                btnToggleLock.BackColor = Color.FromArgb(0, 123, 255);
            }
        }

        private void OnCmbLanguageChanged(object sender, EventArgs e)
        {
            if (isUpdatingUi) return;

            AppLanguage newLang = (cmbLanguage.SelectedIndex == 1)
                ? AppLanguage.English
                : AppLanguage.TraditionalChinese;

            ConfigManager.Current.Language = newLang;
            ConfigManager.Save(ConfigManager.Current);
            LocalizationManager.CurrentLanguage = newLang;
        }

        private void OnBtnElevateClick(object sender, EventArgs e)
        {
            PromptAndRestartAsAdmin();
        }

        private bool PromptAndRestartAsAdmin()
        {
            DialogResult res = MessageBox.Show(
                this,
                LocalizationManager.Get("UacPromptMessage"),
                LocalizationManager.Get("UacPromptTitle"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );

            if (res == DialogResult.Yes)
            {
                if (appContext != null)
                {
                    appContext.RestartElevated();
                }
                return true;
            }
            return false;
        }

        private void OnModeRadioChanged(object sender, EventArgs e)
        {
            if (isUpdatingUi) return;

            if (rbModeHardware.Checked)
            {
                // 方案 A：切換至模式二時，若尚未具備管理員權限，主動詢問是否提權重啟
                if (!DeviceControlManager.IsAdministrator())
                {
                    ConfigManager.Current.SelectedMode = LockMode.HardwareDisable;
                    ConfigManager.Save(ConfigManager.Current);

                    if (!PromptAndRestartAsAdmin())
                    {
                        isUpdatingUi = true;
                        rbModeSoftware.Checked = true;
                        ConfigManager.Current.SelectedMode = LockMode.SoftwareHook;
                        ConfigManager.Save(ConfigManager.Current);
                        isUpdatingUi = false;
                    }
                    return;
                }

                ConfigManager.Current.SelectedMode = LockMode.HardwareDisable;
            }
            else
            {
                ConfigManager.Current.SelectedMode = LockMode.SoftwareHook;
            }

            ConfigManager.Save(ConfigManager.Current);
        }

        public void SelectSoftwareModeInUi()
        {
            isUpdatingUi = true;
            rbModeSoftware.Checked = true;
            rbModeHardware.Checked = false;
            isUpdatingUi = false;
        }

        private void OnBtnToggleLockClick(object sender, EventArgs e)
        {
            if (appContext != null)
            {
                appContext.ToggleLockFromUi();
            }
        }

        private void CancelHotkeyRecording()
        {
            if (!isRecordingHotkey) return;
            isRecordingHotkey = false;
            txtHotkey.Text = ConfigManager.Current.Hotkey.ToString();
            txtHotkey.BackColor = Color.White;
        }

        private void OnTxtHotkeyLeave(object sender, EventArgs e)
        {
            CancelHotkeyRecording();
        }

        private void OnBtnRecordHotkeyClick(object sender, EventArgs e)
        {
            isRecordingHotkey = true;
            txtHotkey.Text = LocalizationManager.Get("RecordingPrompt");
            txtHotkey.BackColor = Color.FromArgb(255, 249, 219);
            txtHotkey.Focus();
        }

        private void OnBtnResetHotkeyClick(object sender, EventArgs e)
        {
            isRecordingHotkey = false;
            ConfigManager.Current.Hotkey.ResetToDefault();
            ConfigManager.Save(ConfigManager.Current);
            txtHotkey.Text = ConfigManager.Current.Hotkey.ToString();
            txtHotkey.BackColor = Color.White;

            if (appContext != null)
            {
                appContext.RefreshGlobalHotkeyRegistration();
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (isRecordingHotkey)
            {
                e.SuppressKeyPress = true;
                e.Handled = true;

                Keys key = e.KeyCode;

                // 按下 Esc 鍵安靜取消錄製並恢復原熱鍵顯示
                if (key == Keys.Escape)
                {
                    CancelHotkeyRecording();
                    return;
                }

                if (HotkeyModel.IsModifierKeyOnly(key))
                {
                    return;
                }

                HotkeyModel candidate = new HotkeyModel(
                    e.Control,
                    e.Alt,
                    e.Shift,
                    false,
                    key
                );

                isRecordingHotkey = false;
                txtHotkey.BackColor = Color.White;

                if (!candidate.IsValid())
                {
                    MessageBox.Show(
                        this,
                        LocalizationManager.Get("InvalidHotkeyNotice"),
                        LocalizationManager.Get("AppTitle"),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );
                    txtHotkey.Text = ConfigManager.Current.Hotkey.ToString();
                    return;
                }

                ConfigManager.Current.Hotkey = candidate;
                ConfigManager.Save(ConfigManager.Current);
                txtHotkey.Text = candidate.ToString();

                if (appContext != null)
                {
                    bool registered = appContext.RefreshGlobalHotkeyRegistration();
                    if (!registered)
                    {
                        MessageBox.Show(
                            this,
                            LocalizationManager.Get("HotkeyConflictNotice"),
                            LocalizationManager.Get("AppTitle"),
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information
                        );
                    }
                }
                return;
            }

            base.OnKeyDown(e);
        }

        private void OnSafetySettingChanged(object sender, EventArgs e)
        {
            if (isUpdatingUi) return;

            AppSettings s = ConfigManager.Current;
            s.AutoUnlockTimeoutMinutes = (int)numTimeout.Value;
            s.AutoRestoreOnDisconnect = chkRestoreOnDisconnect.Checked;
            s.ShowOverlayHud = chkShowHud.Checked;
            s.PlayNotificationSound = chkPlaySound.Checked;
            s.StartWithWindows = chkStartWithWindows.Checked;

            ConfigManager.Save(s);
            StartupManager.SetStartWithWindows(s.StartWithWindows);

            if (appContext != null)
            {
                appContext.ApplyUpdatedSettings();
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // 選項 A：使用者點擊右上角 [X] 時縮小至系統匣背景常駐
            if (!allowRealClose && e.CloseReason == CloseReason.UserClosing && ConfigManager.Current.CloseToTray)
            {
                CancelHotkeyRecording();
                e.Cancel = true;
                this.Hide();
                if (appContext != null)
                {
                    appContext.ShowFirstMinimizeBalloon();
                }
                return;
            }

            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                LocalizationManager.LanguageChanged -= OnLanguageChanged;
            }
            base.Dispose(disposing);
        }
    }
}
