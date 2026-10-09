@echo off
chcp 65001 >nul
echo ========================================================
echo   BİLENSİS - Kurulum Paketi (Setup) Oluşturucu
echo ========================================================
echo.

set PROJECT_DIR=%~dp0StokVeresiyeApp
set OUTPUT_DIR=%~dp0Setup_Output

echo [1/3] Proje Self-Contained Olarak Yayımlanıyor (Publish)...
dotnet publish "%PROJECT_DIR%\StokVeresiyeApp.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "%PROJECT_DIR%\publish"

if errorlevel 1 (
    echo.
    echo [HATA] Dotnet publish adımı başarısız oldu!
    pause
    exit /b 1
)

echo.
echo [2/3] Inno Setup Derleyicisi Kontrol Ediliyor...
set ISCC="C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
if not exist %ISCC% (
    set ISCC="C:\Program Files\Inno Setup 6\ISCC.exe"
)

if not exist %ISCC% (
    echo [HATA] Inno Setup 6 (ISCC.exe) bulunamadı!
    pause
    exit /b 1
)

echo.
echo [3/3] Inno Setup ile Kurulum Dosyası (Setup.exe) Paketleniyor...
%ISCC% "%PROJECT_DIR%\setup.iss"

if errorlevel 1 (
    echo.
    echo [HATA] Inno Setup derleme adımı başarısız oldu!
    pause
    exit /b 1
)

echo.
echo [Ek Adım] WinRAR Arşivi Kontrol Ediliyor...
set RAR_EXE="C:\Program Files\WinRAR\Rar.exe"
if exist %RAR_EXE% (
    %RAR_EXE% a -ep1 "%OUTPUT_DIR%\Bilensis_Setup_v2.6.0.rar" "%OUTPUT_DIR%\Bilensis_Setup_v2.6.0.exe" >nul
    echo   RAR Arşivi de Hazırlandı: %OUTPUT_DIR%\Bilensis_Setup_v2.6.0.rar
)

echo.
echo ========================================================
echo   KURULUM DOSYASI BAŞARIYLA OLUŞTURULDU!
echo   Dosya Konumu: %OUTPUT_DIR%\Bilensis_Setup_v2.6.0.exe
echo ========================================================
echo.
