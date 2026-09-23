@echo off
chcp 65001 >nul
echo ========================================================
echo   BongoCat Steam LAN Sync - Patch Uninstaller
echo ========================================================

set "MANAGED_DIR=%~dp0..\BongoCat-steam\BongoCat_Data\Managed"
if not exist "%MANAGED_DIR%" (
    set "MANAGED_DIR=%~dp0..\BongoCat_Data\Managed"
)
if not exist "%MANAGED_DIR%" (
    set "MANAGED_DIR=BongoCat_Data\Managed"
)

if not exist "%MANAGED_DIR%" (
    echo [ERROR] Could not find BongoCat_Data\Managed directory!
    pause
    exit /b 1
)

if exist "%MANAGED_DIR%\Assembly-CSharp.dll.orig" (
    echo Restoring original Assembly-CSharp.dll ...
    copy /y "%MANAGED_DIR%\Assembly-CSharp.dll.orig" "%MANAGED_DIR%\Assembly-CSharp.dll" >nul
    del "%MANAGED_DIR%\Assembly-CSharp.dll.orig" >nul 2>nul
)

if exist "%MANAGED_DIR%\BongoSync.dll" (
    echo Removing BongoSync.dll ...
    del "%MANAGED_DIR%\BongoSync.dll" >nul 2>nul
)

echo.
echo [SUCCESS] Patch uninstalled and original game files restored!
echo.
pause
