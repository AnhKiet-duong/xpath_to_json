@echo off
setlocal
cd /d "%~dp0XPathScanner"

echo === Building XPathScanner ===
dotnet build XPathScanner.sln
if errorlevel 1 (
    echo.
    echo BUILD FAILED.
    exit /b 1
)

echo.
echo BUILD OK.
if /i "%~1"=="run" (
    echo Starting XPathScanner.App...
    start "" "XPathScanner.App\bin\Debug\net8.0-windows\XPathScanner.App.exe"
)

endlocal
