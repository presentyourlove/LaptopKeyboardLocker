@echo off
chcp 65001 >nul
setlocal

echo [1/2] Compiling Unit Test Suite (64-bit Native C# Compiler)...
set CSC="C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

if not exist %CSC% (
    echo [ERROR] Could not find 64-bit C# compiler at %CSC%
    exit /b 1
)

%CSC% /nologo /target:exe /platform:anycpu /optimize+ ^
    /out:"%~dp0TestRunner.exe" ^
    /r:System.dll ^
    /r:System.Core.dll ^
    /r:System.Windows.Forms.dll ^
    "%~dp0src\Core\Native\Win32Api.cs" ^
    "%~dp0src\Core\DiagnosticLogger.cs" ^
    "%~dp0src\Core\DeviceClassifier.cs" ^
    "%~dp0src\Core\HotkeyModel.cs" ^
    "%~dp0src\Core\LocalizationManager.cs" ^
    "%~dp0src\Core\ConfigManager.cs" ^
    "%~dp0src\Core\LockStateManager.cs" ^
    "%~dp0tests\LaptopKeyboardLocker.Tests\HotkeyModelTests.cs" ^
    "%~dp0tests\LaptopKeyboardLocker.Tests\DeviceClassifierTests.cs" ^
    "%~dp0tests\LaptopKeyboardLocker.Tests\LocalizationTests.cs" ^
    "%~dp0tests\LaptopKeyboardLocker.Tests\ConfigManagerTests.cs" ^
    "%~dp0tests\LaptopKeyboardLocker.Tests\LockStateManagerTests.cs" ^
    "%~dp0tests\TestRunner.cs"

if %ERRORLEVEL% neq 0 (
    echo [ERROR] Unit test compilation failed!
    exit /b %ERRORLEVEL%
)

echo [2/2] Running Automated Unit Tests...
"%~dp0TestRunner.exe"
set TEST_EXIT_CODE=%ERRORLEVEL%

if exist "%~dp0TestRunner.exe" del /q "%~dp0TestRunner.exe"
exit /b %TEST_EXIT_CODE%

