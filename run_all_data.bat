@echo off
setlocal EnableDelayedExpansion
REM ============================================================================
REM  GM95 - run_all_data.bat  (o ROOT du an)  ==> CHAY 1 PHAT CO DATA
REM  Quy trinh tu dong:
REM    1. Bat PostgreSQL portable (start_pg.bat)
REM    2. Bat SERVER o CUA SO RIENG (server tu chay migration -> tao bang)
REM    3. Cho server san sang (poll /dgrpi/health)
REM    4. Chay seed_test_data.bat -> nap DU LIEU TEST (vai tram dong moi loai)
REM    5. Mo APP client
REM  => Dung khi muon co du lieu san de test tinh nang.
REM  (Muon database TRONG thi dung run_all_trong.bat)
REM ============================================================================

set "ROOT_DIR=%~dp0"
if "%ROOT_DIR:~-1%"=="\" set "ROOT_DIR=%ROOT_DIR:~0,-1%"

set "SERVER_DIR=%ROOT_DIR%\Server"
set "APP_DIR=%ROOT_DIR%\App\Manager_Perfoment"
set "CONFIG_FILE=%SERVER_DIR%\config.json"

echo(
echo ==========================================================
echo   GM95 - CHAY TAT CA (CO DATA)
echo   PG -^> Server -^> Seed du lieu -^> App
echo ==========================================================
echo(

REM --- Kiem tra dotnet ---
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
REM Server Kestrel thuc te LUON nghe http://localhost:port (tu ghi de config https).
REM App client cung goi http. Nen health-check dung http cung, du config https=true.
set "HEALTH_URL=http://localhost:%SRV_PORT%/dgrpi/health"

echo [1/5] Bao dam PostgreSQL dang chay...
call "%SERVER_DIR%\start_pg.bat"
echo(

echo [2/5] Bat SERVER o cua so rieng (giu cua so do de server chay)...
REM Mo run_server.bat trong 1 cua so CMD moi -> server chay nen, script nay chay tiep.
start "GM95 Server" cmd /k ""%ROOT_DIR%\run_server.bat""
echo(

echo [3/5] Cho server san sang tai %HEALTH_URL% ...
set "READY="
for /L %%i in (1,1,60) do (
    if not defined READY (
        powershell -NoProfile -ExecutionPolicy Bypass -Command ^
          "try { $r = Invoke-WebRequest -Uri '%HEALTH_URL%' -TimeoutSec 2 -UseBasicParsing; if ($r.StatusCode -eq 200) { exit 0 } else { exit 1 } } catch { exit 1 }" >nul 2>&1
        if not errorlevel 1 (
            set "READY=1"
            echo       OK: server da san sang ^(sau %%i giay^).
        ) else (
            <nul set /p "=." >nul
            timeout /t 1 /nobreak >nul
        )
    )
)
echo(
if not defined READY (
    echo [LOI] Server khong san sang sau 60 giay. Xem cua so "GM95 Server" de biet loi.
    goto :fail
)

echo [4/5] Nap DU LIEU TEST vao database...
call "%ROOT_DIR%\seed_test_data.bat"
if errorlevel 1 (
    echo [LOI] Seed du lieu that bai. Xem thong bao o tren.
    goto :fail
)
echo(

echo [5/5] Mo APP client...
echo(
call "%ROOT_DIR%\run_app_quanly.bat"

echo(
echo ==========================================================
echo   XONG. Server dang chay o cua so "GM95 Server".
echo   Dong cua so do (Ctrl+C) khi muon tat server.
echo ==========================================================
endlocal
exit /b 0

:fail
echo(
echo [THAT BAI] Xem thong bao loi ben tren.
endlocal
exit /b 1
