@echo off
setlocal

echo ========================================================
echo HIDGuard - Executable Yayinlama ve Derleme Araci
echo ========================================================
echo.

echo [1/2] Framework-bagimli executable yayinlaniyor (publish/)...
dotnet publish src\HidGuard.App\HidGuard.App.csproj -c Release -o publish\
if %ERRORLEVEL% NEQ 0 (
    echo Hata: Yayinlama basarisiz oldu.
    exit /b %ERRORLEVEL%
)

echo.
echo [2/2] Bagimsiz (Self-Contained win-x64) executable yayinlaniyor (dist\app\)...
dotnet publish src\HidGuard.App\HidGuard.App.csproj -c Release -r win-x64 --self-contained true -o dist\app\
if %ERRORLEVEL% NEQ 0 (
    echo Hata: Self-contained yayinlama basarisiz oldu.
    exit /b %ERRORLEVEL%
)

echo.
echo ========================================================
echo Yayinlama basariyla tamamlandi!
echo - Hizli calistirilabilir dosya: publish\HidGuard.App.exe
echo - Bagimsiz dagitim paketi:      dist\app\HidGuard.App.exe
echo ========================================================
