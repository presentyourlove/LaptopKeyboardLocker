using System;

namespace LaptopKeyboardLocker.Core
{
    /// <summary>
    /// 管理應用程式的核心鎖定狀態、400ms 熱鍵切換防彈跳冷卻、防呆逾時倒數與外接斷線安全機制。
    /// </summary>
    public class LockStateManager
    {
        public const int DefaultCooldownMs = 400;

        private bool isLocked = false;
        private LockMode currentMode = LockMode.SoftwareHook;
        private int timeoutMinutes = 15;
        private int cooldownMilliseconds = DefaultCooldownMs;
        private DateTime lockStartTime;
        private DateTime lastStateChangeTimeUtc = DateTime.MinValue;

        public event EventHandler LockStateChanged;
        public event EventHandler AutoUnlockTriggered;
        public event EventHandler ExternalKeyboardsLostTriggered;

        public bool IsLocked
        {
            get { return isLocked; }
        }

        public LockMode CurrentMode
        {
            get { return currentMode; }
            set { currentMode = value; }
        }

        public int TimeoutMinutes
        {
            get { return timeoutMinutes; }
            set { timeoutMinutes = Math.Max(0, value); }
        }

        public int CooldownMilliseconds
        {
            get { return cooldownMilliseconds; }
            set { cooldownMilliseconds = Math.Max(0, value); }
        }

        public DateTime LockStartTime
        {
            get { return lockStartTime; }
        }

        /// <summary>
        /// 檢查目前是否處於狀態切換後的防彈跳冷卻期間。
        /// </summary>
        public bool IsInCooldown(DateTime currentTimeUtc)
        {
            if (cooldownMilliseconds <= 0 || lastStateChangeTimeUtc == DateTime.MinValue)
            {
                return false;
            }

            TimeSpan diff = currentTimeUtc - lastStateChangeTimeUtc;
            return diff.TotalMilliseconds >= 0 && diff.TotalMilliseconds < cooldownMilliseconds;
        }

        /// <summary>
        /// 請求進入鎖定狀態。
        /// </summary>
        public bool Lock(LockMode mode)
        {
            return Lock(mode, DateTime.UtcNow);
        }

        public bool Lock(LockMode mode, DateTime currentTimeUtc)
        {
            if (isLocked || IsInCooldown(currentTimeUtc))
            {
                return false;
            }

            currentMode = mode;
            isLocked = true;
            lockStartTime = currentTimeUtc;
            lastStateChangeTimeUtc = currentTimeUtc;
            OnLockStateChanged();
            return true;
        }

        /// <summary>
        /// 請求解除鎖定狀態。
        /// </summary>
        public bool Unlock()
        {
            return Unlock(DateTime.UtcNow, false);
        }

        public bool Unlock(DateTime currentTimeUtc, bool forceBypassCooldown)
        {
            if (!isLocked) return false;
            if (!forceBypassCooldown && IsInCooldown(currentTimeUtc))
            {
                return false;
            }

            isLocked = false;
            lastStateChangeTimeUtc = currentTimeUtc;
            OnLockStateChanged();
            return true;
        }

        /// <summary>
        /// 切換鎖定狀態（已鎖定則解鎖，未鎖定則依當前模式鎖定）。
        /// </summary>
        public bool Toggle(LockMode mode)
        {
            return Toggle(mode, DateTime.UtcNow);
        }

        public bool Toggle(LockMode mode, DateTime currentTimeUtc)
        {
            if (IsInCooldown(currentTimeUtc))
            {
                return false;
            }

            if (isLocked)
            {
                return Unlock(currentTimeUtc, false);
            }
            else
            {
                return Lock(mode, currentTimeUtc);
            }
        }

        /// <summary>
        /// 檢查防呆逾時狀態（供定時器或輪詢調用）。
        /// 若設定大於 0 分鐘且鎖定時間已超限，將自動強制執行解鎖。
        /// </summary>
        public bool CheckTimeout(DateTime currentTimeUtc)
        {
            if (!isLocked || timeoutMinutes <= 0)
            {
                return false;
            }

            TimeSpan elapsed = currentTimeUtc - lockStartTime;
            if (elapsed.TotalMinutes >= timeoutMinutes)
            {
                Unlock(currentTimeUtc, true);
                if (AutoUnlockTriggered != null)
                {
                    AutoUnlockTriggered(this, EventArgs.Empty);
                }
                return true;
            }

            return false;
        }

        /// <summary>
        /// 檢查外接鍵盤連線狀態防呆（機制 C）：
        /// 若處於模式二（硬體停用內建鍵盤）且系統中已無任何外接鍵盤，強制自動解鎖復原。
        /// </summary>
        public bool CheckExternalKeyboardsConnection(bool hasExternalKeyboards, bool autoRestoreOptionEnabled)
        {
            if (!isLocked || currentMode != LockMode.HardwareDisable || !autoRestoreOptionEnabled)
            {
                return false;
            }

            if (!hasExternalKeyboards)
            {
                Unlock(DateTime.UtcNow, true);
                if (ExternalKeyboardsLostTriggered != null)
                {
                    ExternalKeyboardsLostTriggered(this, EventArgs.Empty);
                }
                return true;
            }

            return false;
        }

        private void OnLockStateChanged()
        {
            if (LockStateChanged != null)
            {
                LockStateChanged(this, EventArgs.Empty);
            }
        }
    }
}
