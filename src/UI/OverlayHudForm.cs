using System;
using System.Drawing;
using System.Windows.Forms;
using LaptopKeyboardLocker.Core;

namespace LaptopKeyboardLocker.UI
{
    /// <summary>
    /// 浮動半透明鎖定狀態 HUD 提示視窗，顯示目前鎖定狀態與解鎖熱鍵指引。
    /// 支援多語系即時切換、雙緩衝防閃爍與多螢幕動態右下角定位。
    /// </summary>
    public class OverlayHudForm : Form
    {
        private Label lblTitle;
        private Label lblSubtitle;
        private Label lblHotkey;
        private string currentHotkeyText = "Ctrl + Alt + L";

        private const int WS_EX_TOPMOST = 0x00000008;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_TOPMOST | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
                return cp;
            }
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        public OverlayHudForm()
        {
            this.DoubleBuffered = true;
            InitializeComponent();
            LocalizationManager.LanguageChanged += OnLanguageChanged;
            ApplyLocalization();
        }

        private void InitializeComponent()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.Manual;
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.BackColor = Color.FromArgb(28, 30, 36);
            this.Opacity = 0.90;
            this.Size = new Size(340, 98);
            this.Padding = new Padding(14, 10, 14, 10);

            lblTitle = new Label
            {
                Font = new Font("Microsoft JhengHei UI", 11.0f, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 107, 107),
                Dock = DockStyle.Top,
                Height = 28,
                AutoEllipsis = true
            };

            lblHotkey = new Label
            {
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(230, 235, 240),
                Dock = DockStyle.Top,
                Height = 24,
                AutoEllipsis = true
            };

            lblSubtitle = new Label
            {
                Font = new Font("Microsoft JhengHei UI", 8.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(165, 175, 185),
                Dock = DockStyle.Top,
                Height = 22,
                AutoEllipsis = true
            };

            this.Controls.Add(lblSubtitle);
            this.Controls.Add(lblHotkey);
            this.Controls.Add(lblTitle);

            RepositionToBottomRight();
        }

        private void OnLanguageChanged(object sender, EventArgs e)
        {
            ApplyLocalization();
        }

        public void ApplyLocalization()
        {
            if (lblTitle != null)
            {
                lblTitle.Text = LocalizationManager.Get("HudTitle");
            }
            if (lblHotkey != null)
            {
                lblHotkey.Text = LocalizationManager.Get("HudHotkeyPrefix") + currentHotkeyText;
            }
            if (lblSubtitle != null)
            {
                lblSubtitle.Text = LocalizationManager.Get("HudSubtitle");
            }
        }

        public void UpdateHotkeyDisplay(string hotkeyText)
        {
            currentHotkeyText = hotkeyText ?? "Ctrl + Alt + L";
            if (lblHotkey != null)
            {
                lblHotkey.Text = LocalizationManager.Get("HudHotkeyPrefix") + currentHotkeyText;
            }
        }

        public void RepositionToBottomRight()
        {
            try
            {
                Rectangle workArea = Screen.PrimaryScreen.WorkingArea;
                this.Location = new Point(
                    Math.Max(0, workArea.Right - this.Width - 20),
                    Math.Max(0, workArea.Bottom - this.Height - 20)
                );
            }
            catch
            {
            }
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (this.Visible)
            {
                ApplyLocalization();
                RepositionToBottomRight();
            }
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
