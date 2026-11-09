using System;
using LaptopKeyboardLocker.Core;

namespace LaptopKeyboardLocker.Tests
{
    public static class DeviceClassifierTests
    {
        public static void RunAll(TestAssert assert)
        {
            assert.Section("DeviceClassifier 單元測試 (Hardware Classification & Touchpad Safety)");

            // 1. 傳統 PS/2 與 ACPI 筆電內建鍵盤測試
            assert.IsTrue(DeviceClassifier.IsInternal(@"ACPI\PNP0303\4&1D401FB5&0", "Standard PS/2 Keyboard"),
                "ACPI\\PNP0303 標準 PS/2 應被識別為筆電內建鍵盤");
            assert.IsTrue(DeviceClassifier.IsInternal(@"ACPI\MSFT0001\1", "PC/AT Enhanced PS/2 Keyboard"),
                "ACPI\\MSFT0001 應被識別為筆電內建鍵盤");

            // 2. 現代輕薄筆電 HID-over-I2C 內建鍵盤測試
            assert.IsTrue(DeviceClassifier.IsInternal(@"HID\MSFT0001&Col01\5&2A8B3C&0&0000", "HID Keyboard Device"),
                "HID\\MSFT0001 (Surface/現代筆電 I2C) 應被識別為內建鍵盤");
            assert.IsTrue(DeviceClassifier.IsInternal(@"HID\ATK3001&Col01\4&112233", "HID Keyboard Device"),
                "HID\\ATK3001 (ASUS ZenBook I2C) 應被識別為內建鍵盤");
            assert.IsTrue(DeviceClassifier.IsInternal(@"HID\VEN_DLLK&DEV_09D9&Col01", "HID Keyboard Device"),
                "HID\\VEN_DLLK (Dell XPS I2C) 應被識別為內建鍵盤");

            // 3. 觸控板、滑鼠與感測器絕對排除白名單測試 (防止誤傷觸控板)
            assert.AreEqual((int)DeviceCategory.Unknown,
                (int)DeviceClassifier.Classify(@"HID\ELAN1200&Col02", "ELAN I2C Filter Touchpad"),
                "帶有 Touchpad 名稱的 I2C 複合裝置必須被強制排除，不可誤判為內建鍵盤");
            assert.AreEqual((int)DeviceCategory.Unknown,
                (int)DeviceClassifier.Classify(@"HID\SYNA2393&Col01", "Synaptics Precision Trackpad"),
                "帶有 Trackpad 名稱的 Synaptics 裝置必須被強制排除");
            assert.AreEqual((int)DeviceCategory.Unknown,
                (int)DeviceClassifier.Classify(@"HID\MSFT0002", "HID-compliant Mouse"),
                "帶有 Mouse 名稱的裝置必須被強制排除");

            // 4. 外接 USB 與藍牙鍵盤測試
            assert.IsTrue(DeviceClassifier.IsExternal(@"USB\VID_046D&PID_C52B\6&1A2B3C", "Logitech USB Input Device"),
                "USB\\VID_046D 應被識別為外接鍵盤");
            assert.IsTrue(DeviceClassifier.IsExternal(@"HID\VID_1532&PID_022A&MI_00", "HID Keyboard Device"),
                "HID\\VID_1532 (USB HID 機械鍵盤) 應被識別為外接鍵盤");
            assert.IsTrue(DeviceClassifier.IsExternal(@"BTHENUM\{00001124-0000-1000-8000-00805F9B34FB}", "Bluetooth Wireless Keyboard"),
                "BTHENUM 藍牙鍵盤應被識別為外接鍵盤");

            // 5. 空值與邊界防呆測試
            assert.AreEqual((int)DeviceCategory.Unknown, (int)DeviceClassifier.Classify(null, null),
                "Null 輸入應安全回傳 Unknown");
            assert.AreEqual((int)DeviceCategory.Unknown, (int)DeviceClassifier.Classify("", ""),
                "空字串輸入應安全回傳 Unknown");
        }
    }
}

