@echo off
setlocal EnableDelayedExpansion
REM ============================================================================
REM  DGroup - run_server.bat  (o ROOT du an)
REM  Build & chay server .NET 8 (ASP.NET Core, kieu Controller).
REM  - Doc server.port + https tu Server\config.json bang PowerShell
REM  - Bao dam PostgreSQL portable dang chay (goi Server\start_pg.bat neu can)
REM  - dotnet run --project Server
REM  - In URL http(s)://localhost:port
REM  Chiu duoc duong dan co khoang trang.
REM ============================================================================

set "ROOT_DIR=%~dp0"
if "%ROOT_DIR:~-1%"=="\" set "ROOT_DIR=%ROOT_DIR:~0,-1%"

set "SERVER_DIR=%ROOT_DIR%\Server"
set "CONFIG_FILE=%SERVER_DIR%\config.json"

echo(
echo ==========================================================
echo   DGroup - Run Server (.NET 8)
echo ==========================================================
echo   Root   : %ROOT_DIR%
echo   Server : %SERVER_DIR%
echo(

REM --- Kiem tra dotnet SDK ---
where dotnet >nul 2>&1
if errorlevel 1 (
    echo [LOI] Khong tim thay 'dotnet'. Cai .NET 8 SDK: https://dotnet.microsoft.com/download/dotnet/8.0
    goto :fail
)

if not exist "%CONFIG_FILE%" (
    echo [LOI] Khong thay config.json tai "%CONFIG_FILE%"
    goto :fail
)

REM --- Doc port + https tu config.json ---
set "SRV_PORT="
set "SRV_HTTPS="
for /f "usebackq delims=" %%L in (`powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$c = Get-Content -Raw -LiteralPath '%CONFIG_FILE%' | ConvertFrom-Json; Write-Output ('SRV_PORT=' + $c.server.port); Write-Output ('SRV_HTTPS=' + ([string]$c.https).ToLower())"`) do (
    set "%%L"
)

if not defined SRV_PORT (
    echo [LOI] Khong doc duoc server.port tu config.json.
    goto :fail
)

set "SCHEME=http"
if /i "%SRV_HTTPS%"=="true" set "SCHEME=https"

echo   Port   : %SRV_PORT%
echo   Scheme : %SCHEME%
echo(

REM --- Bao dam PostgreSQL portable dang chay (goi start_pg.bat) ---
REM start_pg.bat tu bo qua neu PG da chay. Bo dong duoi neu ban tu quan ly PG.
echo [1/2] Bao dam PostgreSQL dang chay...
call "%SERVER_DIR%\start_pg.bat"
if errorlevel 1 (
    echo [CANH BAO] start_pg.bat bao loi. Van tiep tuc chay server ^(co the PG da chay^).
)
echo(

REM --- Bien moi truong cho Kestrel/ASP.NET Core ---
REM ASPNETCORE_URLS ep Kestrel lang nghe dung port trong config.
set "ASPNETCORE_ENVIRONMENT=Development"
set "ASPNETCORE_URLS=%SCHEME%://localhost:%SRV_PORT%"
set "DOTNET_CLI_TELEMETRY_OPTOUT=1"

echo [2/2] Build va chay server .NET 8...
echo(
echo ==========================================================
echo   Server URL: %SCHEME%://localhost:%SRV_PORT%
echo   (Ctrl+C de dung server)
echo ==========================================================
echo(

REM Chay tu ROOT, tro toi project Server. Kestrel doc ASPNETCORE_URLS o tren.
dotnet run --project "%SERVER_DIR%"
set "RC=%ERRORLEVEL%"

echo(
echo Server da dung ^(ma thoat %RC%^).
endlocal
exit /b %RC%

:fail
echo(
echo [THAT BAI] Xem thong bao loi ben tren.
endlocal
exit /b 1
