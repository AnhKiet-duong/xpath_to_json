@echo off
setlocal
REM ============================================================
REM  XPathScanner - Build file .exe chay tren may khac
REM
REM  Cach dung: double-click file nay (hoac chay tu cmd).
REM  Ket qua: file XPathScanner.exe trong thu muc "publish"
REM  ngay canh file bat nay.
REM
REM  Ban nay la SELF-CONTAINED SINGLE-FILE:
REM   - Gop luon .NET 8 runtime vao trong 1 file exe (~70 MB)
REM   - May dich KHONG can cai dat .NET, chay duoc ngay
REM   - Chi can copy 1 file XPathScanner.exe sang may khac
REM ============================================================

set "ROOT=%~dp0"
set "CSPROJ=%ROOT%XPathScanner\XPathScanner.App\XPathScanner.App.csproj"
set "OUT=%ROOT%publish"

echo.
echo [1/2] Dang publish (Release, win-x64, self-contained, single-file)...
echo.

dotnet publish "%CSPROJ%" -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true ^
  -o "%OUT%"

if errorlevel 1 (
  echo.
  echo [LOI] Publish that bai. Xem chi tiet loi o tren.
  echo Go phim bat ky de dong cua so nay.
  pause >nul
  exit /b 1
)

echo.
echo [2/2] THANH CONG!
echo   File exe:  %OUT%\XPathScanner.exe
echo.
echo GHI CHU:
echo  - May dich phai la Windows 64-bit (x64). Neu la 32-bit, doi
echo    "win-x64" thanh "win-x86" o dong dotnet publish trong file nay.
echo  - May dich khong can cai .NET (da gop san runtime).
echo  - Cac file *.pdb la debug symbol, khong can copy theo.
echo.
pause
