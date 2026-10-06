@echo off
cd /d "%~dp0"
echo [1/3] dotnet publish calistiriliyor...
dotnet publish "StokVeresiyeApp\StokVeresiyeApp.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "StokVeresiyeApp\publish"
if errorlevel 1 goto error

copy /y "Kullanim_Kilavuzu.html" "StokVeresiyeApp\publish\Kullanim_Kilavuzu.html" >nul

echo [2/3] Inno Setup ile paketleniyor...
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" "StokVeresiyeApp\setup.iss"
if errorlevel 1 goto error

echo.
echo ========================================================
echo KURULUM BASARIYLA OLUSTURULDU!
echo Setup_Output\Bilensis_Setup_v2.4.0.exe
echo ========================================================
goto done

:error
echo [HATA] Islem sirasinda bir hata olustu!

:done
