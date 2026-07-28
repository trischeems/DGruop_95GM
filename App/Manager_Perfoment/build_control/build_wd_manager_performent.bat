@echo off
setlocal EnableDelayedExpansion
REM ============================================================================
REM  GM95 - build_wd_manager_performent.bat  (o App\Manager_Perfoment\build_control)
REM  Build app client Manager_Perfoment cho WINDOWS + dong goi installer Inno Setup.
REM  Chay tren may Windows da cai: .NET 8 SDK + Inno Setup 6 (ISCC.exe).
REM
REM  Cach dung:  build_wd_manager_performent.bat [version]
REM              (vi du: build_wd_manager_performent.bat 1.2.0 -- mac dinh 1.0.0)
REM
REM  Ket qua (trong thu muc build_control\out\):
REM    out\win-x64\publish\   ban build self-contained (khach KHONG can cai .NET)
REM    out\installer\Setup_Manager_Perfoment_<version>_win-x64.exe
REM  Chiu duoc duong dan co khoang trang.
REM ============================================================================

set "SCRIPT_DIR=%~dp0"
if "%SCRIPT_DIR:~-1%"=="\" set "SCRIPT_DIR=%SCRIPT_DIR:~0,-1%"
for %%I in ("%SCRIPT_DIR%\..") do set "APP_DIR=%%~fI"

set "CSPROJ=%APP_DIR%\GM95.App.ManagerPerformance.csproj"
set "ISS_FILE=%SCRIPT_DIR%\manager_performent.iss"
set "OUT_DIR=%SCRIPT_DIR%\out"
set "PUBLISH_DIR=%OUT_DIR%\win-x64\publish"
set "INSTALLER_DIR=%OUT_DIR%\installer"

set "VERSION=%~1"
if not defined VERSION set "VERSION=1.0.0"

echo(
echo ==========================================================
echo   GM95 - Build Windows (Manager_Perfoment) v%VERSION%
echo ==========================================================
echo   App     : %APP_DIR%
echo   Publish : %PUBLISH_DIR%
echo(

REM --- Kiem tra dotnet SDK ---
where dotnet >nul 2>&1
if errorlevel 1 (
    echo [LOI] Khong tim thay 'dotnet'. Cai .NET 8 SDK: https://dotnet.microsoft.com/download/dotnet/8.0
    goto :fail
)

if not exist "%CSPROJ%" (
    echo [LOI] Khong thay project tai "%CSPROJ%"
    goto :fail
)

if not exist "%ISS_FILE%" (
    echo [LOI] Khong thay script Inno Setup tai "%ISS_FILE%"
    goto :fail
)

REM --- Tim Inno Setup 6 (ISCC.exe) TRUOC khi build, do khong phi cong publish ---
set "ISCC="
where ISCC.exe >nul 2>&1 && set "ISCC=ISCC.exe"
if not defined ISCC if exist "%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe" set "ISCC=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
if not defined ISCC if exist "%ProgramFiles%\Inno Setup 6\ISCC.exe" set "ISCC=%ProgramFiles%\Inno Setup 6\ISCC.exe"
if not defined ISCC if exist "%LocalAppData%\Programs\Inno Setup 6\ISCC.exe" set "ISCC=%LocalAppData%\Programs\Inno Setup 6\ISCC.exe"
if not defined ISCC (
    echo [LOI] Khong tim thay ISCC.exe cua Inno Setup 6.
    echo       Cai tai: https://jrsoftware.org/isdl.php  ^(hoac them ISCC.exe vao PATH^)
    goto :fail
)
echo   ISCC    : %ISCC%
echo(

REM --- [1/2] dotnet publish: Release, win-x64, self-contained ---
echo [1/2] dotnet publish ^(Release, win-x64, self-contained^)...
if exist "%PUBLISH_DIR%" rd /s /q "%PUBLISH_DIR%"
dotnet publish "%CSPROJ%" -c Release -r win-x64 --self-contained true -p:Version=%VERSION% -o "%PUBLISH_DIR%"
if errorlevel 1 (
    echo [LOI] dotnet publish that bai.
    goto :fail
)
echo(

REM --- [2/2] Dong goi installer bang Inno Setup ---
echo [2/2] Bien dich Inno Setup ^(manager_performent.iss^)...
"%ISCC%" "/DAppVersion=%VERSION%" "/DPublishDir=%PUBLISH_DIR%" "/O%INSTALLER_DIR%" "%ISS_FILE%"
if errorlevel 1 (
    echo [LOI] Bien dich Inno Setup that bai.
    goto :fail
)

echo(
echo ==========================================================
echo   XONG. Installer nam o:
echo   %INSTALLER_DIR%\Setup_Manager_Perfoment_%VERSION%_win-x64.exe
echo ==========================================================
endlocal
exit /b 0

:fail
echo(
echo [THAT BAI] Xem thong bao loi ben tren.
endlocal
exit /b 1
