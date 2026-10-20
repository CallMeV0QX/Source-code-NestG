@echo off
echo ========================================================
echo        Building NestG Launcher Solution (.NET 8)
echo ========================================================
echo.

dotnet --version >nul 2>&1
if %errorlevel% neq 0 (
    echo [ERROR] dotnet CLI not found! Please install .NET 8 or .NET 9 SDK.
    pause
    exit /b 1
)

echo [*] Building solution NestG.sln...
dotnet build "%~dp0NestG.sln" -c Release

if %errorlevel% equ 0 (
    echo.
    echo ========================================================
    echo  [SUCCESS] Build completed successfully!
    echo.
    echo  Launcher EXE: src\NestG_Launcher\bin\Release\net8.0-windows\NestG launcher.exe
    echo  Updater EXE:  src\NestG_Updater\bin\Release\net8.0-windows\NestG.Updater.exe
    echo   [!] Ban co the chay truc tiep "NestG launcher.exe" trong thu muc tren.
    echo ========================================================
) else (
    echo.
    echo [ERROR] Build failed! Please review the error log above.
)

pause
