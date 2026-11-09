using System;
using LaptopKeyboardLocker.Core;

namespace LaptopKeyboardLocker.Tests
{
    public static class LockStateManagerTests
    {
        public static void RunAll(TestAssert assert)
        {
            assert.Section("LockStateManager 單元測試 (State Machine, 400ms Cooldown & Fail-Safes)");

            LockStateManager mgr = new LockStateManager
            {
                TimeoutMinutes = 10,
                CooldownMilliseconds = 400
            };

            DateTime t0 = new DateTime(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);

            // 1. 初始狀態與鎖定切換測試
            assert.IsFalse(mgr.IsLocked, "初始狀態應為未鎖定");
            bool locked = mgr.Lock(LockMode.SoftwareHook, t0);
            assert.IsTrue(locked && mgr.IsLocked, "呼叫 Lock() 應成功進入鎖定狀態");

            // 2. 400ms 熱鍵切換防彈跳冷卻 (Toggle Cooldown) 測試
            DateTime tBounce = t0.AddMilliseconds(120); // 僅過 120ms (小於 400ms)
            bool bounceUnlock = mgr.Toggle(LockMode.SoftwareHook, tBounce);
            assert.IsFalse(bounceUnlock, "在 400ms 防彈跳冷卻期間內 (120ms) 重複觸發 Toggle 應被阻擋忽略");
            assert.IsTrue(mgr.IsLocked, "防彈跳阻擋後應維持鎖定狀態，不會誤解鎖");

            DateTime tAfterCooldown = t0.AddMilliseconds(450); // 超過 400ms
            bool normalUnlock = mgr.Toggle(LockMode.SoftwareHook, tAfterCooldown);
            assert.IsTrue(normalUnlock && !mgr.IsLocked, "超過 400ms 冷卻期後呼叫 Toggle 應正常解除鎖定");

            // 3. 防呆逾時自動解鎖測試
            DateTime tLock2 = t0.AddMinutes(1);
            mgr.Lock(LockMode.SoftwareHook, tLock2);
            bool autoUnlockEventFired = false;
            mgr.AutoUnlockTriggered += delegate { autoUnlockEventFired = true; };

            assert.IsFalse(mgr.CheckTimeout(tLock2.AddMinutes(5)),
                "未達 10 分鐘逾時門檻 (僅過 5 分鐘) 時，不應觸發自動解鎖");
            assert.IsTrue(mgr.CheckTimeout(tLock2.AddMinutes(10)),
                "達到 10 分鐘逾時門檻時，CheckTimeout 應自動解鎖並回傳 true");
            assert.IsTrue(!mgr.IsLocked && autoUnlockEventFired,
                "逾時後狀態應變為未鎖定且觸發 AutoUnlockTriggered 事件");

            // 4. 機制 C：外接鍵盤全斷開自動復原測試
            DateTime tLock3 = t0.AddMinutes(20);
            mgr.Lock(LockMode.HardwareDisable, tLock3);
            bool externalLostEventFired = false;
            mgr.ExternalKeyboardsLostTriggered += delegate { externalLostEventFired = true; };

            assert.IsFalse(mgr.CheckExternalKeyboardsConnection(true, true),
                "當外接鍵盤仍連線時，不應觸發斷開自動解鎖");
            assert.IsTrue(mgr.CheckExternalKeyboardsConnection(false, true),
                "當處於模式二且外接鍵盤全數斷開時，應立即強制自動解鎖復原");
            assert.IsTrue(!mgr.IsLocked && externalLostEventFired,
                "外接鍵盤斷開後應解除鎖定並觸發 ExternalKeyboardsLostTriggered 事件");
        }
    }
}

