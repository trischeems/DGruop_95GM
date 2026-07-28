@echo off
setlocal EnableDelayedExpansion
REM ============================================================================
REM  GM95 - run_app_quanly.bat  (o ROOT du an)
REM  Mo app client Manager_Perfoment (Avalonia UI, .NET 8).
REM  - Doc server.base_url tu App\Manager_Perfoment\config.json bang PowerShell
REM  - Kiem tra server co chay khong (goi /dgrpi/health); neu chua -> nhac bat run_server.bat
REM  - dotnet run --project App\Manager_Perfoment
REM  Chiu duoc duong dan co khoang trang.
REM ============================================================================

set "ROOT_DIR=%~dp0"
if "%ROOT_DIR:~-1%"=="\" set "ROOT_DIR=%ROOT_DIR:~0,-1%"

set "APP_DIR=%ROOT_DIR%\App\Manager_Perfoment"
set "APP_CONFIG=%APP_DIR%\config.json"

echo(
echo ==========================================================
echo   GM95 - Run App (Manager_Perfoment)
echo ==========================================================
echo   App : %APP_DIR%
echo(

REM --- Kiem tra dotnet SDK ---
where dotnet >nul 2>&1
if errorlevel 1 (
    echo [LOI] Khong tim thay 'dotnet'. Cai .NET 8 SDK: https://dotnet.microsoft.com/download/dotnet/8.0
    goto :fail
)

if not exist "%APP_CONFIG%" (
    echo [LOI] Khong thay config app tai "%APP_CONFIG%"
    goto :fail
)

REM --- Doc server.base_url tu config.json cua app ---
set "BASE_URL="
for /f "usebackq delims=" %%L in (`powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$c = Get-Content -Raw -LiteralPath '%APP_CONFIG%' | ConvertFrom-Json; Write-Output ('BASE_URL=' + $c.server.base_url)"`) do (
    set "%%L"
)

if not defined BASE_URL (
    echo [CANH BAO] Khong doc duoc server.base_url tu config.json. Van tiep tuc mo app.
) else (
    echo   Server : %BASE_URL%
    echo(
    echo [1/2] Kiem tra server co dang chay...
    powershell -NoProfile -ExecutionPolicy Bypass -Command ^
      "try { $r = Invoke-WebRequest -Uri ('%BASE_URL%/dgrpi/health') -TimeoutSec 3 -UseBasicParsing; if ($r.StatusCode -eq 200) { exit 0 } else { exit 1 } } catch { exit 1 }"
    if errorlevel 1 (
        echo       [CANH BAO] Chua ket noi duoc server tai %BASE_URL%.
        echo       Hay mo server truoc bang:  run_server.bat
        echo       ^(Van tiep tuc mo app; bam 'Lam moi' trong app sau khi server chay.^)
    ) else (
        echo       OK: server dang chay.
    )
)
echo(

echo [2/2] Build va mo app...
echo(
dotnet run --project "%APP_DIR%"
set "RC=%ERRORLEVEL%"

echo(
echo App da dong ^(ma thoat %RC%^).
endlocal
exit /b %RC%

:fail
echo(
echo [THAT BAI] Xem thong bao loi ben tren.
endlocal
exit /b 1
