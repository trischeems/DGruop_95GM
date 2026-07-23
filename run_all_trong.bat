@echo off
setlocal EnableDelayedExpansion
REM ============================================================================
REM  DGroup - run_all_trong.bat  (o ROOT du an)  ==> CHAY 1 PHAT, DATABASE TRONG
REM  Quy trinh tu dong:
REM    1. Bat PostgreSQL portable (start_pg.bat)
REM    2. Bat SERVER o CUA SO RIENG (server tu chay migration -> tao bang)
REM    3. Cho server san sang (poll /dgrpi/health)
REM    4. XOA SACH database (clear_all_data.sql) -> khong con du lieu giao dich
REM    5. Mo APP client (mo len la trong tron)
REM  => Dung khi muon bat dau voi database SACH.
REM  (Muon co du lieu test thi dung run_all_data.bat)
REM ============================================================================

set "ROOT_DIR=%~dp0"
if "%ROOT_DIR:~-1%"=="\" set "ROOT_DIR=%ROOT_DIR:~0,-1%"

set "SERVER_DIR=%ROOT_DIR%\Server"
set "APP_DIR=%ROOT_DIR%\App\Manager_Perfoment"
set "CONFIG_FILE=%SERVER_DIR%\config.json"
set "PG_BIN=%SERVER_DIR%\pgsql\bin"
set "CLEAR_SQL=%SERVER_DIR%\Apps\ManagerPerformance\Database\Seed\clear_all_data.sql"

REM --- Tenant dich (tham so 1, mac dinh public) ---
set "TENANT=%~1"
if "%TENANT%"=="" set "TENANT=public"

echo(
echo ==========================================================
echo   DGroup - CHAY TAT CA (DATABASE TRONG)
echo   PG -^> Server -^> Xoa sach DB -^> App
echo ==========================================================
echo   Tenant : %TENANT%
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
if not exist "%CLEAR_SQL%" (
    echo [LOI] Khong thay file xoa: "%CLEAR_SQL%"
    goto :fail
)

REM --- Doc port + https + thong tin DB tu config.json ---
set "SRV_PORT="
set "SRV_HTTPS="
set "PG_PORT="
set "PG_USER="
set "PG_PASS="
set "PG_DBNAME="
set "PG_HOST="
for /f "usebackq delims=" %%L in (`powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$c = Get-Content -Raw -LiteralPath '%CONFIG_FILE%' | ConvertFrom-Json; $p = $c.dgroup_postgress; Write-Output ('SRV_PORT=' + $c.server.port); Write-Output ('SRV_HTTPS=' + ([string]$c.https).ToLower()); Write-Output ('PG_PORT=' + $p.port); Write-Output ('PG_USER=' + $p.user); Write-Output ('PG_PASS=' + $p.pass); Write-Output ('PG_DBNAME=' + $p.dbname); Write-Output ('PG_HOST=' + $p.host)"`) do (
    set "%%L"
)
if not defined SRV_PORT (
    echo [LOI] Khong doc duoc cau hinh tu config.json.
    goto :fail
)
REM Server Kestrel thuc te LUON nghe http://localhost:port (tu ghi de config https).
REM App client cung goi http. Nen health-check dung http cung, du config https=true.
set "HEALTH_URL=http://localhost:%SRV_PORT%/dgrpi/health"

echo [1/5] Bao dam PostgreSQL dang chay...
call "%SERVER_DIR%\start_pg.bat"
echo(

echo [2/5] Bat SERVER o cua so rieng (giu cua so do de server chay)...
start "DGroup Server" cmd /k ""%ROOT_DIR%\run_server.bat""
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
    echo [LOI] Server khong san sang sau 60 giay. Xem cua so "DGroup Server" de biet loi.
    goto :fail
)

echo [4/5] XOA SACH database (de trong)...
set "PGPASSWORD=%PG_PASS%"
set "PGCLIENTENCODING=UTF8"
"%PG_BIN%\psql.exe" -h %PG_HOST% -p %PG_PORT% -U %PG_USER% -d %PG_DBNAME% ^
    --set=client_encoding=UTF8 -v ON_ERROR_STOP=1 -v tenant=%TENANT% -f "%CLEAR_SQL%"
if errorlevel 1 (
    echo [LOI] Xoa database that bai. Xem thong bao o tren.
    goto :fail
)
echo(

echo [5/5] Mo APP client...
echo(
call "%ROOT_DIR%\run_app_quanly.bat"

echo(
echo ==========================================================
echo   XONG. Database TRONG. Server dang chay o cua so "DGroup Server".
echo   Dong cua so do (Ctrl+C) khi muon tat server.
echo ==========================================================
endlocal
exit /b 0

:fail
echo(
echo [THAT BAI] Xem thong bao loi ben tren.
endlocal
exit /b 1
