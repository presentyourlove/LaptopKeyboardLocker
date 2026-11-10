using System;

namespace LaptopKeyboardLocker.Core
{
    public enum DeviceCategory
    {
        InternalKeyboard = 0,
        ExternalKeyboard = 1,
        Unknown = 2
    }

    /// <summary>
    /// 純邏輯裝置硬體識別分類器，用以區分筆記型電腦內建鍵盤（傳統 PS/2 與現代 I2C-HID）與外接鍵盤（USB / 藍牙）。
    /// 具備觸控板與滑鼠強制排除保護機制，防止誤停用觸控板。
    /// </summary>
    public static class DeviceClassifier
    {
        /// <summary>
        /// 依據硬體 Device ID 與裝置描述名稱判斷鍵盤類別。
        /// </summary>
        public static DeviceCategory Classify(string deviceId, string deviceName)
        {
            if (string.IsNullOrEmpty(deviceId) && string.IsNullOrEmpty(deviceName))
            {
                return DeviceCategory.Unknown;
            }

            string id = (deviceId ?? string.Empty).ToUpperInvariant();
            string name = (deviceName ?? string.Empty).ToUpperInvariant();

            // 0. 觸控板、滑鼠、指紋辨識與感測器絕對排除白名單（防止複合 I2C 節點誤傷觸控板）
            if (name.Contains("TOUCHPAD") || name.Contains("TRACKPAD") || name.Contains("CLICKPAD") ||
                name.Contains("MOUSE") || name.Contains("POINTING") || name.Contains("TOUCH SCREEN") ||
                name.Contains("TOUCHSCREEN") || name.Contains("FINGERPRINT") || name.Contains("SENSOR") ||
                id.Contains("TOUCHPAD") || id.Contains("MOUSE"))
            {
                return DeviceCategory.Unknown;
            }

            // 1. 優先確認傳統 ACPI / PS/2、現代 HID-over-I2C 與電競/商用筆電內建嵌入式 HID 鍵盤硬體特徵
            // PNP0303 / PNP030B / PNP0320: 標準 PS/2 內建鍵盤
            // ACPI\*: ACPI 系統匯流排原生鍵盤節點
            // I2C\*: I2C 控制器節點
            // HID\MSFT*, HID\ATK*, HID\ELAN*, HID\DLLK*, HID\LEN*, HID\SYNA*, HID\VEN_*: 現代輕薄筆電 HID-over-I2C 內建鍵盤
            // ASUS N-Key (VID_0B05&PID_1854 等) / ITE 內建控制器 (VID_048D) / MSI SteelSeries 內建鍵盤
            if (id.Contains("PNP0303") || id.Contains("PNP030B") || id.Contains("PNP0320") ||
                id.StartsWith("ACPI\\", StringComparison.OrdinalIgnoreCase) ||
                id.StartsWith("I2C\\", StringComparison.OrdinalIgnoreCase) ||
                id.StartsWith("HID\\MSFT", StringComparison.OrdinalIgnoreCase) ||
                id.StartsWith("HID\\ATK", StringComparison.OrdinalIgnoreCase) ||
                id.StartsWith("HID\\ELAN", StringComparison.OrdinalIgnoreCase) ||
                id.StartsWith("HID\\DLLK", StringComparison.OrdinalIgnoreCase) ||
                id.StartsWith("HID\\LEN", StringComparison.OrdinalIgnoreCase) ||
                id.StartsWith("HID\\SYNA", StringComparison.OrdinalIgnoreCase) ||
                id.StartsWith("HID\\VEN_", StringComparison.OrdinalIgnoreCase) ||
                IsKnownLaptopInternalHid(id, name))
            {
                return DeviceCategory.InternalKeyboard;
            }

            // 2. 外接鍵盤特徵（USB 介面與藍牙配對裝置）
            if (id.Contains("USB\\") ||
                id.StartsWith("HID\\VID_", StringComparison.OrdinalIgnoreCase) ||
                id.Contains("BTHENUM\\") || id.Contains("BTHLEDEVICE\\") || id.Contains("BLUETOOTH") ||
                name.Contains("USB") || name.Contains("BLUETOOTH") || name.Contains("WIRELESS"))
            {
                return DeviceCategory.ExternalKeyboard;
            }

            // 3. 名稱關鍵字補充判斷
            if (name.Contains("PS/2") || name.Contains("INTERNAL") || name.Contains("BUILT-IN") ||
                name.Contains("PC/AT") || name.Contains("I2C HID"))
            {
                return DeviceCategory.InternalKeyboard;
            }

            return DeviceCategory.Unknown;
        }

        /// <summary>
        /// 識別電競與現代筆電常見透過內部 USB/I2C 橋接之嵌入式內建鍵盤控制器（如 ASUS ROG/TUF N-Key、ITE Tech、MSI SteelSeries、Elan、Chicony）。
        /// </summary>
        public static bool IsKnownLaptopInternalHid(string upperId, string upperName)
        {
            if (string.IsNullOrEmpty(upperId))
            {
                return false;
            }

            string safeName = upperName ?? string.Empty;

            // ASUS ROG / TUF / Zephyrus / Zenbook / Vivobook 筆電內建 N-Key 鍵盤控制器 (VID_0B05)
            // 排除華碩外接機械鍵盤型號關鍵字
            if (upperId.Contains("VID_0B05&"))
            {
                if (safeName.Contains("FALCHION") || safeName.Contains("AZOTH") ||
                    safeName.Contains("SCOPE") || safeName.Contains("CLAYMORE") ||
                    safeName.Contains("FLARE") || safeName.Contains("TUF GAMING K"))
                {
                    return false;
                }
                return true;
            }

            // ITE Tech (VID_048D) / Elan (VID_04F3) / Synaptics (VID_06CB) / Chicony (VID_04F2) 筆電內建嵌入式鍵盤控制器
            if (upperId.Contains("VID_048D&") ||
                upperId.Contains("VID_04F3&") ||
                upperId.Contains("VID_06CB&") ||
                upperId.Contains("VID_04F2&"))
            {
                return true;
            }

            // MSI 筆電內建 SteelSeries / 嵌入式鍵盤控制器
            if (upperId.Contains("VID_1038&PID_1122") ||
                upperId.Contains("VID_1038&PID_113A") ||
                upperId.Contains("VID_1770&PID_FF00"))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// 便捷方法：是否為內建筆電鍵盤。
        /// </summary>
        public static bool IsInternal(string deviceId, string deviceName)
        {
            return Classify(deviceId, deviceName) == DeviceCategory.InternalKeyboard;
        }

        /// <summary>
        /// 便捷方法：是否為外接鍵盤。
        /// </summary>
        public static bool IsExternal(string deviceId, string deviceName)
        {
            return Classify(deviceId, deviceName) == DeviceCategory.ExternalKeyboard;
        }

        /// <summary>
        /// 將 Win32 Raw Input 裝置路徑（例如 \\?\HID#VID_0B05&amp;PID_1854...）正規化為標準 PnP Device ID（HID\VID_0B05&amp;PID_1854...）。
        /// </summary>
        public static string NormalizeRawDevicePath(string rawDevicePath)
        {
            if (string.IsNullOrEmpty(rawDevicePath))
            {
                return string.Empty;
            }

            string cleaned = rawDevicePath.Trim();
            if (cleaned.StartsWith("\\\\?\\", StringComparison.OrdinalIgnoreCase))
            {
                cleaned = cleaned.Substring(4);
            }
            int guidIdx = cleaned.IndexOf("#{", StringComparison.OrdinalIgnoreCase);
            if (guidIdx > 0)
            {
                cleaned = cleaned.Substring(0, guidIdx);
            }
            return cleaned.Replace('#', '\\');
        }

        /// <summary>
        /// 判斷 Win32 Raw Input 裝置路徑是否屬於筆電內建鍵盤。
        /// </summary>
        public static bool IsRawDevicePathInternal(string rawDevicePath)
        {
            string normalized = NormalizeRawDevicePath(rawDevicePath);
            return IsInternal(normalized, string.Empty);
        }
    }
}
