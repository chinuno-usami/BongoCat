@echo off
chcp 65001 >nul
echo ========================================================
echo   BongoCat Steam LAN Sync - Patch Installer
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
    echo Please run this batch script inside your BongoCat game directory.
    pause
    exit /b 1
)

echo Target directory: %MANAGED_DIR%

if not exist "%MANAGED_DIR%\Assembly-CSharp.dll.orig" (
    echo Backing up original Assembly-CSharp.dll ...
    copy "%MANAGED_DIR%\Assembly-CSharp.dll" "%MANAGED_DIR%\Assembly-CSharp.dll.orig" >nul
)

echo Installing BongoSync.dll ...
copy /y "%~dp0..\BongoCat-steam\BongoCat_Data\Managed\BongoSync.dll" "%MANAGED_DIR%\" >nul 2>nul
if not exist "%MANAGED_DIR%\BongoSync.dll" (
    copy /y "%~dp0BongoSync.dll" "%MANAGED_DIR%\" >nul
)

echo Installing patched Assembly-CSharp.dll ...
if exist "%~dp0Assembly-CSharp.patched.dll" (
    copy /y "%~dp0Assembly-CSharp.patched.dll" "%MANAGED_DIR%\Assembly-CSharp.dll" >nul
) else if exist "%~dp0..\BongoCat-steam\BongoCat_Data\Managed\Assembly-CSharp.dll" (
    copy /y "%~dp0..\BongoCat-steam\BongoCat_Data\Managed\Assembly-CSharp.dll" "%MANAGED_DIR%\" >nul
) else if exist "%~dp0Assembly-CSharp.dll" (
    copy /y "%~dp0Assembly-CSharp.dll" "%MANAGED_DIR%\" >nul
)

echo.
echo [SUCCESS] Patch installed successfully!
echo You can now start BongoCat on Steam, and it will automatically listen on UDP port 39824.
echo.
pause
