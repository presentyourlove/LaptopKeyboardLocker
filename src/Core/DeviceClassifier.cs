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

            // 1. 優先確認傳統 ACPI / PS/2 與現代 HID-over-I2C 筆電內建鍵盤硬體特徵
            // PNP0303 / PNP030B / PNP0320: 標準 PS/2 內建鍵盤
            // ACPI\*: ACPI 系統匯流排原生鍵盤節點
            // I2C\*: I2C 控制器節點
            // HID\MSFT*, HID\ATK*, HID\ELAN*, HID\DLLK*, HID\LEN*, HID\SYNA*, HID\VEN_*: 現代輕薄筆電 HID-over-I2C 內建鍵盤
            if (id.Contains("PNP0303") || id.Contains("PNP030B") || id.Contains("PNP0320") ||
                id.StartsWith("ACPI\\", StringComparison.OrdinalIgnoreCase) ||
                id.StartsWith("I2C\\", StringComparison.OrdinalIgnoreCase) ||
                id.StartsWith("HID\\MSFT", StringComparison.OrdinalIgnoreCase) ||
                id.StartsWith("HID\\ATK", StringComparison.OrdinalIgnoreCase) ||
                id.StartsWith("HID\\ELAN", StringComparison.OrdinalIgnoreCase) ||
                id.StartsWith("HID\\DLLK", StringComparison.OrdinalIgnoreCase) ||
                id.StartsWith("HID\\LEN", StringComparison.OrdinalIgnoreCase) ||
                id.StartsWith("HID\\SYNA", StringComparison.OrdinalIgnoreCase) ||
                id.StartsWith("HID\\VEN_", StringComparison.OrdinalIgnoreCase))
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
    }
}
