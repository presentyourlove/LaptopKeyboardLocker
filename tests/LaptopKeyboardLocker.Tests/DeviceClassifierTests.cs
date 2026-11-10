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

            // 4. 電競與現代筆電內建嵌入式 HID 鍵盤控制器測試 (ASUS ROG/TUF N-Key, ITE, MSI)
            assert.IsTrue(DeviceClassifier.IsInternal(@"HID\VID_0B05&PID_1854&COL02\6&36E90261&0&0001", "HID Keyboard Device"),
                "HID\\VID_0B05&PID_1854 (ASUS ROG/TUF 筆電內置 N-Key 鍵盤) 必須被識別為內建鍵盤");
            assert.IsTrue(DeviceClassifier.IsInternal(@"USB\VID_0B05&PID_1854\5&16BDD27B&0&11", "USB Input Device"),
                "USB\\VID_0B05&PID_1854 (ASUS 內置鍵盤複合父節點) 必須被識別為內建鍵盤");
            assert.IsTrue(DeviceClassifier.IsInternal(@"HID\VID_048D&PID_C100&COL01\6&112233", "HID Keyboard Device"),
                "HID\\VID_048D (ITE Tech 筆電嵌入式鍵盤控制器) 必須被識別為內建鍵盤");
            assert.IsTrue(DeviceClassifier.IsInternal(@"HID\VID_1038&PID_1122&COL01\6&445566", "SteelSeries PS/2 Keyboard"),
                "HID\\VID_1038&PID_1122 (MSI SteelSeries 內建鍵盤) 必須被識別為內建鍵盤");
            assert.IsTrue(DeviceClassifier.IsExternal(@"HID\VID_0B05&PID_193E&MI_00", "ASUS ROG Falchion Wireless"),
                "帶有 Falchion 型號之華碩外接機械鍵盤應被排除於內建之外並識別為外接鍵盤");

            // 5. 外接 USB 與藍牙鍵盤測試
            assert.IsTrue(DeviceClassifier.IsExternal(@"HID\VID_046D&PID_C33C&MI_00\8&2D3670AA&0&0000", "HID Keyboard Device"),
                "HID\\VID_046D&PID_C33C (Logitech G512 外接 USB 鍵盤) 應被識別為外接鍵盤");
            assert.IsTrue(DeviceClassifier.IsExternal(@"USB\VID_046D&PID_C52B\6&1A2B3C", "Logitech USB Input Device"),
                "USB\\VID_046D 應被識別為外接鍵盤");
            assert.IsTrue(DeviceClassifier.IsExternal(@"HID\VID_1532&PID_022A&MI_00", "HID Keyboard Device"),
                "HID\\VID_1532 (USB HID 機械鍵盤) 應被識別為外接鍵盤");
            assert.IsTrue(DeviceClassifier.IsExternal(@"BTHENUM\{00001124-0000-1000-8000-00805F9B34FB}", "Bluetooth Wireless Keyboard"),
                "BTHENUM 藍牙鍵盤應被識別為外接鍵盤");

            // 6. Win32 Raw Input 裝置路徑正規化與內外建判斷測試
            string rawAsusInternal = @"\\?\HID#VID_0B05&PID_1854&COL02#6&36e90261&0&0001#{884b96c3-56ef-11d1-bc8c-00a0c91405dd}";
            string rawLogitechExternal = @"\\?\HID#VID_046D&PID_C33C&MI_00#8&2d3670aa&0&0000#{884b96c3-56ef-11d1-bc8c-00a0c91405dd}";
            assert.AreEqual(@"HID\VID_0B05&PID_1854&COL02\6&36e90261&0&0001",
                DeviceClassifier.NormalizeRawDevicePath(rawAsusInternal),
                "NormalizeRawDevicePath 應正確將 Raw Input 路徑轉為標準 PnP Device ID");
            assert.IsTrue(DeviceClassifier.IsRawDevicePathInternal(rawAsusInternal),
                "IsRawDevicePathInternal 應將華碩內建鍵盤 Raw Input 路徑識別為內建鍵盤");
            assert.IsFalse(DeviceClassifier.IsRawDevicePathInternal(rawLogitechExternal),
                "IsRawDevicePathInternal 應將羅技外接鍵盤 Raw Input 路徑識別為非內建（外接放行）");

            // 7. 空值與邊界防呆測試
            assert.AreEqual((int)DeviceCategory.Unknown, (int)DeviceClassifier.Classify(null, null),
                "Null 輸入應安全回傳 Unknown");
            assert.AreEqual((int)DeviceCategory.Unknown, (int)DeviceClassifier.Classify("", ""),
                "空字串輸入應安全回傳 Unknown");
        }
    }
}

