@echo off
chcp 65001 >nul
setlocal

echo ========================================================
echo   Building LaptopKeyboardLocker -> KeyLock.exe (64-bit)
echo ========================================================

set CSC="C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

if not exist %CSC% (
    echo [ERROR] 64-bit C# compiler not found at %CSC%
    exit /b 1
)

%CSC% /nologo /target:winexe /platform:anycpu /optimize+ ^
    /out:"%~dp0KeyLock.exe" ^
    /r:System.dll ^
    /r:System.Core.dll ^
    /r:System.Drawing.dll ^
    /r:System.Windows.Forms.dll ^
    /r:System.Management.dll ^
    "%~dp0src\Core\Native\Win32Api.cs" ^
    "%~dp0src\Core\DiagnosticLogger.cs" ^
    "%~dp0src\Core\DeviceClassifier.cs" ^
    "%~dp0src\Core\HotkeyModel.cs" ^
    "%~dp0src\Core\LocalizationManager.cs" ^
    "%~dp0src\Core\ConfigManager.cs" ^
    "%~dp0src\Core\StartupManager.cs" ^
    "%~dp0src\Core\LockStateManager.cs" ^
    "%~dp0src\Core\KeyboardHookManager.cs" ^
    "%~dp0src\Core\DeviceControlManager.cs" ^
    "%~dp0src\UI\OverlayHudForm.cs" ^
    "%~dp0src\UI\MainForm.cs" ^
    "%~dp0src\UI\TrayAppContext.cs" ^
    "%~dp0src\Program.cs"

if %ERRORLEVEL% neq 0 (
    echo [ERROR] Build failed with code %ERRORLEVEL%!
    exit /b %ERRORLEVEL%
)

echo [SUCCESS] Built standalone portable executable: %~dp0KeyLock.exe
exit /b 0

