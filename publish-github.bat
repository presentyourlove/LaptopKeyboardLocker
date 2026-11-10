@echo off
setlocal
chcp 65001 >nul
cd /d "%~dp0"

set "TAG=%~1"
if "%TAG%"=="" set "TAG=v1.1.0"
set "COMMIT_MSG=%~2"
if "%COMMIT_MSG%"=="" set "COMMIT_MSG=feat(release): update KeyLock.exe and release %TAG%"

set "GH_EXE=gh"
if exist "C:\Program Files\GitHub CLI\gh.exe" (
    set "GH_EXE=C:\Program Files\GitHub CLI\gh.exe"
)

echo ========================================================
echo  [1/5] Cleaning outdated and temporary files...
echo ========================================================
del /q "diagnostics.jsonl" "settings.ini" "disabled_devices.state" "TestRunner.exe" "*.tmp" 2>nul
del /q "..\diagnostics.jsonl" "..\settings.ini" "..\disabled_devices.state" 2>nul

echo ========================================================
echo  [2/5] Running automated unit tests (^>80%% coverage)...
echo ========================================================
call "%~dp0run-tests.bat"
if %ERRORLEVEL% neq 0 (
    echo [ERROR] Unit tests failed. Aborting publish.
    exit /b 1
)

echo ========================================================
echo  [3/5] Building KeyLock.exe and syncing binary...
echo ========================================================
call "%~dp0build.bat"
if %ERRORLEVEL% neq 0 (
    echo [ERROR] Build failed. Aborting publish.
    exit /b 1
)
copy /y "%~dp0KeyLock.exe" "%~dp0..\KeyLock.exe" >nul 2>&1

echo ========================================================
echo  [4/5] Uploading source code to GitHub (origin/main)...
echo ========================================================
git add -A
git diff --cached --quiet
if %ERRORLEVEL% neq 0 (
    git commit -m "%COMMIT_MSG%"
)
git push origin main
if %ERRORLEVEL% neq 0 (
    echo [ERROR] Git push failed.
    exit /b 1
)

echo ========================================================
echo  [5/5] Publishing GitHub Release (%TAG%) with KeyLock.exe...
echo ========================================================
"%GH_EXE%" release view "%TAG%" >nul 2>&1
if %ERRORLEVEL% equ 0 (
    "%GH_EXE%" release upload "%TAG%" "%~dp0KeyLock.exe" --clobber
) else (
    "%GH_EXE%" release create "%TAG%" "%~dp0KeyLock.exe" --title "LaptopKeyboardLocker %TAG%" --generate-notes
)

echo ========================================================
echo  [SUCCESS] Uploaded to GitHub and published Release %TAG%!
echo ========================================================
exit /b 0
