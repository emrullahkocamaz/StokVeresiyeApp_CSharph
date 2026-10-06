@echo off
chcp 65001 >nul
echo ========================================================
echo   BİLENSİS ERP - Kurulum Paketi (Setup) Oluşturucu
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

copy /y "%PROJECT_DIR%\Kullanim_Kilavuzu.html" "%PROJECT_DIR%\publish\Kullanim_Kilavuzu.html" >nul

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
echo ========================================================
echo   KURULUM DOSYASI BAŞARIYLA OLUŞTURULDU!
echo   Dosya Konumu: %OUTPUT_DIR%\Bilensis_Setup_v2.4.0.exe
echo ========================================================
echo.
