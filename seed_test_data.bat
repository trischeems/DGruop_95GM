@echo off
setlocal EnableDelayedExpansion
REM ============================================================================
REM  GM95 - seed_test_data.bat
REM  Nap DU LIEU TEST (vai tram dong moi loai) vao schema tenant de test app.
REM  - Doc cau hinh DB tu Server\config.json (khoi gm95_postgress) - NGUON SU THAT
REM  - Dung psql portable trong Server\pgsql
REM  - GIU nguyen reference (warehouses/units/categories/stages), chi lam moi
REM    du lieu giao dich (materials, stock, bom, don SX, xuat/nhap, canh bao...).
REM  - Chay lai nhieu lan an toan (script tu DELETE + reset identity truoc khi nap).
REM
REM  Cach dung:
REM     seed_test_data.bat            :: nap vao tenant "public" (mac dinh)
REM     seed_test_data.bat mytenant   :: nap vao schema tenant khac (phai da ton tai)
REM
REM  YEU CAU: PostgreSQL da chay (Server\start_pg.bat) va tenant/schema da migrate.
REM ============================================================================

set "SCRIPT_DIR=%~dp0"
if "%SCRIPT_DIR:~-1%"=="\" set "SCRIPT_DIR=%SCRIPT_DIR:~0,-1%"

set "CONFIG_FILE=%SCRIPT_DIR%\Server\config.json"
set "PG_BIN=%SCRIPT_DIR%\Server\pgsql\bin"
set "SEED_SQL=%SCRIPT_DIR%\Server\Apps\ManagerPerformance\Database\Seed\seed_test_data.sql"

REM --- Tenant dich (tham so 1, mac dinh public) ---
set "TENANT=%~1"
if "%TENANT%"=="" set "TENANT=public"

echo(
echo ==========================================================
echo   GM95 - Seed du lieu TEST
echo ==========================================================
echo   Tenant     : %TENANT%
echo   Seed SQL   : %SEED_SQL%
echo(

if not exist "%CONFIG_FILE%" (
    echo [LOI] Khong tim thay config.json tai: "%CONFIG_FILE%"
    exit /b 1
)
if not exist "%PG_BIN%\psql.exe" (
    echo [LOI] Khong tim thay psql.exe. Hay chay Server\start_pg.bat truoc.
    exit /b 1
)
if not exist "%SEED_SQL%" (
    echo [LOI] Khong tim thay file seed: "%SEED_SQL%"
    exit /b 1
)

REM --- Doc cau hinh DB tu config.json bang PowerShell ---
set "PG_PORT="
set "PG_USER="
set "PG_PASS="
set "PG_DBNAME="
set "PG_HOST="
for /f "usebackq delims=" %%L in (`powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$c = Get-Content -Raw -LiteralPath '%CONFIG_FILE%' | ConvertFrom-Json; $p = $c.gm95_postgress; Write-Output ('PG_PORT=' + $p.port); Write-Output ('PG_USER=' + $p.user); Write-Output ('PG_PASS=' + $p.pass); Write-Output ('PG_DBNAME=' + $p.dbname); Write-Output ('PG_HOST=' + $p.host)"`) do (
    set "%%L"
)

if not defined PG_PORT (
    echo [LOI] Khong doc duoc cau hinh DB tu config.json.
    exit /b 1
)

echo   Host:Port  : %PG_HOST%:%PG_PORT%
echo   Database   : %PG_DBNAME%   User: %PG_USER%
echo(
echo   Dang nap du lieu... (script tu don du lieu cu truoc khi nap)
echo(

set "PGPASSWORD=%PG_PASS%"
REM Ep client encoding = UTF8: file seed .sql la UTF-8 (tieng Viet co dau).
REM Neu khong ep, console Windows co the doc theo WIN1252 -> loi byte 0x90.
set "PGCLIENTENCODING=UTF8"

REM  -v tenant=%TENANT% : truyen ten schema vao SQL (script dung :"tenant").
"%PG_BIN%\psql.exe" -h %PG_HOST% -p %PG_PORT% -U %PG_USER% -d %PG_DBNAME% ^
    --set=client_encoding=UTF8 -v ON_ERROR_STOP=1 -v tenant=%TENANT% -f "%SEED_SQL%"

if errorlevel 1 (
    echo(
    echo [LOI] Nap du lieu THAT BAI. Kiem tra thong bao loi o tren.
    exit /b 1
)

echo(
echo ==========================================================
echo   HOAN TAT - da nap du lieu test vao tenant "%TENANT%".
echo   Mo app bang run_app_quanly.bat de kiem tra tinh nang.
echo ==========================================================
endlocal
