using System;
using System.Collections.Generic;
using System.IO;
using System.Management;
using System.Security.Principal;
using System.Text;
using LaptopKeyboardLocker.Core.Native;

namespace LaptopKeyboardLocker.Core
{
    public class KeyboardDeviceInfo
    {
        public string DeviceId { get; set; }
        public string Name { get; set; }
        public bool IsInternal { get; set; }
        public string Status { get; set; }
    }

    public enum DeviceDisableResult
    {
        Success = 0,
        NeedRestart = 1,
        AccessDenied = 2,
        DeviceNotFound = 3,
        Failed = 4
    }

    /// <summary>
    /// 負責模式二（內建鍵盤硬體控制）：精準識別並停用/啟用筆電內建鍵盤 (PS/2, ACPI 或 I2C)。
    /// 採用 Win32_PnPEntity 搭配本地狀態檔持久化記錄，確保異常斷電或重啟後 100% 找回並復原被停用的內建鍵盤。
    /// </summary>
    public class DeviceControlManager
    {
        private readonly List<string> disabledInternalDeviceIds = new List<string>();
        private static string resolvedStateFilePath;

        public List<string> DisabledDeviceIds
        {
            get { return disabledInternalDeviceIds; }
        }

        private static string GetStateFilePath()
        {
            if (!string.IsNullOrEmpty(resolvedStateFilePath))
            {
                return resolvedStateFilePath;
            }

            string portablePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "disabled_devices.state");
            try
            {
                string probe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".state_probe.tmp");
                File.WriteAllText(probe, "ok", Encoding.UTF8);
                File.Delete(probe);
                resolvedStateFilePath = portablePath;
                return resolvedStateFilePath;
            }
            catch
            {
                string appDataDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "LaptopKeyboardLocker"
                );
                try
                {
                    if (!Directory.Exists(appDataDir))
                    {
                        Directory.CreateDirectory(appDataDir);
                    }
                    resolvedStateFilePath = Path.Combine(appDataDir, "disabled_devices.state");
                }
                catch
                {
                    resolvedStateFilePath = portablePath;
                }
                return resolvedStateFilePath;
            }
        }

        private void PersistDisabledDeviceIds()
        {
            try
            {
                string stateFile = GetStateFilePath();
                if (disabledInternalDeviceIds.Count == 0)
                {
                    if (File.Exists(stateFile))
                    {
                        File.Delete(stateFile);
                    }
                }
                else
                {
                    File.WriteAllLines(stateFile, disabledInternalDeviceIds.ToArray(), Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                DiagnosticLogger.Warn("PersistStateError", ex.Message);
            }
        }

        private List<string> LoadPersistedDisabledDeviceIds()
        {
            List<string> list = new List<string>();
            try
            {
                string stateFile = GetStateFilePath();
                if (File.Exists(stateFile))
                {
                    string[] lines = File.ReadAllLines(stateFile, Encoding.UTF8);
                    foreach (string line in lines)
                    {
                        string trimmed = line.Trim();
                        if (!string.IsNullOrEmpty(trimmed) && !list.Contains(trimmed))
                        {
                            list.Add(trimmed);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DiagnosticLogger.Warn("LoadStateError", ex.Message);
            }
            return list;
        }

        /// <summary>
        /// 判斷目前處理程序是否具備系統管理員權限 (Elevated Administrator)。
        /// </summary>
        public static bool IsAdministrator()
        {
            try
            {
                using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                {
                    WindowsPrincipal principal = new WindowsPrincipal(identity);
                    return principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 列舉系統中所有鍵盤裝置（使用 Win32_PnPEntity 確保即使處於已停用 ErrorCode=22 狀態也能完整列出）。
        /// </summary>
        public List<KeyboardDeviceInfo> GetKeyboardDevices()
        {
            List<KeyboardDeviceInfo> list = new List<KeyboardDeviceInfo>();

            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                    "SELECT DeviceID, Name, Status, ConfigManagerErrorCode FROM Win32_PnPEntity WHERE PNPClass = 'Keyboard'"))
                using (ManagementObjectCollection collection = searcher.Get())
                {
                    foreach (ManagementObject obj in collection)
                    {
                        string deviceId = obj["DeviceID"] != null ? obj["DeviceID"].ToString() : string.Empty;
                        string name = obj["Name"] != null ? obj["Name"].ToString() : "Keyboard Device";
                        string status = obj["Status"] != null ? obj["Status"].ToString() : "OK";

                        if (string.IsNullOrEmpty(deviceId)) continue;

                        bool isInternal = DeviceClassifier.IsInternal(deviceId, name);
                        list.Add(new KeyboardDeviceInfo
                        {
                            DeviceId = deviceId,
                            Name = name,
                            IsInternal = isInternal,
                            Status = status
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                DiagnosticLogger.Warn("EnumerateKeyboardsError", ex.Message);
            }

            return list;
        }

        /// <summary>
        /// 停用系統中的筆電內建鍵盤。
        /// </summary>
        public DeviceDisableResult DisableInternalKeyboard()
        {
            if (!IsAdministrator())
            {
                DiagnosticLogger.Warn("DisableHardwareDenied", "Not running as administrator");
                return DeviceDisableResult.AccessDenied;
            }

            List<KeyboardDeviceInfo> devices = GetKeyboardDevices();
            bool foundAnyInternal = false;
            bool anyDisabled = false;
            bool needRestart = false;

            foreach (KeyboardDeviceInfo dev in devices)
            {
                if (dev.IsInternal)
                {
                    foundAnyInternal = true;
                    uint devInst;
                    uint result = Win32Api.CM_Locate_DevNode(out devInst, dev.DeviceId, Win32Api.CM_LOCATE_DEVNODE_NORMAL);
                    if (result == Win32Api.CR_SUCCESS)
                    {
                        uint disableRes = Win32Api.CM_Disable_DevNode(devInst, 0);
                        if (disableRes == Win32Api.CR_SUCCESS)
                        {
                            if (!disabledInternalDeviceIds.Contains(dev.DeviceId))
                            {
                                disabledInternalDeviceIds.Add(dev.DeviceId);
                            }
                            anyDisabled = true;
                            DiagnosticLogger.Info("InternalKeyboardDisabled", dev.DeviceId);
                        }
                        else
                        {
                            if (disableRes == Win32Api.CR_NEED_RESTART)
                            {
                                // 若系統回報需要重開機才生效，立即呼叫 Enable 取消擱置的停用標記，防止下次開機鎖死
                                Win32Api.CM_Enable_DevNode(devInst, 0);
                            }

                            // 針對複合式內建 HID 鍵盤（如 ASUS N-Key VID_0B05&PID_1854 之 COL02 子集合），嘗試停用其專屬父層輸入控制器節點
                            if (TryDisableCompositeInternalParent(devInst, dev.DeviceId, dev.Name))
                            {
                                anyDisabled = true;
                            }
                            else if (disableRes == Win32Api.CR_NEED_RESTART)
                            {
                                needRestart = true;
                                DiagnosticLogger.Warn("InternalKeyboardNeedRestart", dev.DeviceId);
                            }
                        }
                    }
                }
            }

            PersistDisabledDeviceIds();

            if (!foundAnyInternal)
            {
                return DeviceDisableResult.DeviceNotFound;
            }

            if (anyDisabled)
            {
                return DeviceDisableResult.Success;
            }

            if (needRestart)
            {
                return DeviceDisableResult.NeedRestart;
            }

            return DeviceDisableResult.Failed;
        }

        private bool TryDisableCompositeInternalParent(uint childDevInst, string childDeviceId, string childName)
        {
            try
            {
                if (!DeviceClassifier.IsKnownLaptopInternalHid(
                    (childDeviceId ?? string.Empty).ToUpperInvariant(),
                    (childName ?? string.Empty).ToUpperInvariant()))
                {
                    return false;
                }

                uint parentDevInst;
                if (Win32Api.CM_Get_Parent(out parentDevInst, childDevInst, 0) != Win32Api.CR_SUCCESS)
                {
                    return false;
                }

                string parentId = GetDeviceIdFromInst(parentDevInst);
                if (string.IsNullOrEmpty(parentId))
                {
                    return false;
                }

                if (!DeviceClassifier.IsKnownLaptopInternalHid(parentId.ToUpperInvariant(), string.Empty))
                {
                    return false;
                }

                uint parentDisableRes = Win32Api.CM_Disable_DevNode(parentDevInst, 0);
                if (parentDisableRes == Win32Api.CR_SUCCESS)
                {
                    if (!disabledInternalDeviceIds.Contains(parentId))
                    {
                        disabledInternalDeviceIds.Add(parentId);
                    }
                    DiagnosticLogger.Info("InternalCompositeParentDisabled", parentId);
                    return true;
                }
                else if (parentDisableRes == Win32Api.CR_NEED_RESTART)
                {
                    Win32Api.CM_Enable_DevNode(parentDevInst, 0);
                }
            }
            catch (Exception ex)
            {
                DiagnosticLogger.Warn("CompositeParentDisableError", ex.Message);
            }
            return false;
        }

        private static string GetDeviceIdFromInst(uint devInst)
        {
            uint len;
            if (Win32Api.CM_Get_Device_ID_Size(out len, devInst, 0) != Win32Api.CR_SUCCESS || len == 0)
            {
                return string.Empty;
            }

            StringBuilder sb = new StringBuilder((int)len + 2);
            if (Win32Api.CM_Get_Device_ID(devInst, sb, len + 2, 0) == Win32Api.CR_SUCCESS)
            {
                return sb.ToString();
            }
            return string.Empty;
        }

        /// <summary>
        /// 復原啟用所有先前被停用的內建鍵盤（包含記憶體紀錄、本地持久化檔案與 PnP 掃描）。
        /// </summary>
        public bool EnableInternalKeyboard()
        {
            if (!IsAdministrator())
            {
                return false;
            }

            bool allRestored = true;

            // 1. 合併持久化記錄中的 DeviceId（因應斷電或崩潰重啟）
            List<string> persistedIds = LoadPersistedDisabledDeviceIds();
            foreach (string id in persistedIds)
            {
                if (!disabledInternalDeviceIds.Contains(id))
                {
                    disabledInternalDeviceIds.Add(id);
                }
            }

            // 2. 復原記錄中的裝置節點
            foreach (string deviceId in disabledInternalDeviceIds)
            {
                uint devInst;
                uint result = Win32Api.CM_Locate_DevNode(out devInst, deviceId, Win32Api.CM_LOCATE_DEVNODE_NORMAL);
                if (result == Win32Api.CR_SUCCESS)
                {
                    uint enableRes = Win32Api.CM_Enable_DevNode(devInst, 0);
                    if (enableRes == Win32Api.CR_SUCCESS)
                    {
                        DiagnosticLogger.Info("InternalKeyboardRestored", deviceId);
                    }
                    else
                    {
                        allRestored = false;
                    }
                }
            }

            disabledInternalDeviceIds.Clear();
            PersistDisabledDeviceIds();

            // 3. 掃描 PnP 樹中所有標記為內建的鍵盤與處於已停用 (ErrorCode=22) 的內建複合控制器，確保 100% 復原
            List<KeyboardDeviceInfo> devices = GetKeyboardDevices();
            foreach (KeyboardDeviceInfo dev in devices)
            {
                if (dev.IsInternal)
                {
                    uint devInst;
                    if (Win32Api.CM_Locate_DevNode(out devInst, dev.DeviceId, Win32Api.CM_LOCATE_DEVNODE_NORMAL) == Win32Api.CR_SUCCESS)
                    {
                        Win32Api.CM_Enable_DevNode(devInst, 0);
                    }
                }
            }

            try
            {
                using (ManagementObjectSearcher disabledSearcher = new ManagementObjectSearcher(
                    "SELECT DeviceID, Name FROM Win32_PnPEntity WHERE ConfigManagerErrorCode = 22"))
                using (ManagementObjectCollection disabledCollection = disabledSearcher.Get())
                {
                    foreach (ManagementObject obj in disabledCollection)
                    {
                        string devId = obj["DeviceID"] != null ? obj["DeviceID"].ToString() : string.Empty;
                        string devName = obj["Name"] != null ? obj["Name"].ToString() : string.Empty;
                        if (!string.IsNullOrEmpty(devId) && DeviceClassifier.IsInternal(devId, devName))
                        {
                            uint devInst;
                            if (Win32Api.CM_Locate_DevNode(out devInst, devId, Win32Api.CM_LOCATE_DEVNODE_NORMAL) == Win32Api.CR_SUCCESS)
                            {
                                Win32Api.CM_Enable_DevNode(devInst, 0);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DiagnosticLogger.Warn("ScanDisabledInternalError", ex.Message);
            }

            return allRestored;
        }

        /// <summary>
        /// 檢查目前系統中是否仍存在可用的外接鍵盤。
        /// </summary>
        public bool HasConnectedExternalKeyboard()
        {
            List<KeyboardDeviceInfo> devices = GetKeyboardDevices();
            foreach (KeyboardDeviceInfo dev in devices)
            {
                if (DeviceClassifier.IsExternal(dev.DeviceId, dev.Name) &&
                    dev.Status.Equals("OK", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
