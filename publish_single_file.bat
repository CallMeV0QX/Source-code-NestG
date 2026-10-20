@echo off
echo ========================================================
echo        Publishing NestG Launcher (Standalone Distribution)
echo ========================================================
echo.

echo [*] Publishing NestG Launcher to dist\Launcher...
dotnet publish "%~dp0src\NestG_Launcher\NestG launcher.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o "%~dp0dist\Launcher"

echo.
echo [*] Publishing NestG Updater to dist\Updater...
dotnet publish "%~dp0src\NestG_Updater\NestG.Updater.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o "%~dp0dist\Updater"

if %errorlevel% equ 0 (
    echo.
    echo ========================================================
    echo  [SUCCESS] Publish completed!
    echo.
    echo  Launcher EXE: dist\Launcher\NestG launcher.exe
    echo  Updater EXE:  dist\Updater\NestG.Updater.exe
    echo   [!] Ban co the chay truc tiep "NestG launcher.exe" trong dist\Launcher.
    echo ========================================================
) else (
    echo.
    echo [ERROR] Publish failed!
)

pause
