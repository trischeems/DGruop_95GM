@echo off
setlocal EnableDelayedExpansion
REM ============================================================================
REM  DGroup - start_pg.bat
REM  Portable PostgreSQL 16 cho Windows (may DEV).
REM  - Doc port/user/pass/dbname tu Server\config.json (khoi dgroup_postgress)
REM  - Neu chua co PG16 portable trong Server\pgsql -> tai zip binaries + giai nen
REM  - Neu chua co data dir Server\pgdata -> initdb (encoding UTF8)
REM  - Start bang pg_ctl theo port trong config
REM  - Lan dau: tao database (user = superuser tao boi initdb voi password config)
REM  Chiu duoc duong dan co khoang trang. Parse JSON bang PowerShell cho chinh xac.
REM ============================================================================

REM --- Xac dinh thu muc chua file .bat nay (%~dp0 co dau \ cuoi) ---
set "SCRIPT_DIR=%~dp0"
if "%SCRIPT_DIR:~-1%"=="\" set "SCRIPT_DIR=%SCRIPT_DIR:~0,-1%"

set "CONFIG_FILE=%SCRIPT_DIR%\config.json"
set "PG_HOME=%SCRIPT_DIR%\pgsql"
set "PG_BIN=%PG_HOME%\bin"
set "PG_DATA=%SCRIPT_DIR%\pgdata"
set "PG_LOG=%SCRIPT_DIR%\pg_log.txt"
set "DL_DIR=%SCRIPT_DIR%\pg_download"

REM --- URL tai ban PostgreSQL 16 portable (zip binaries chinh thuc EnterpriseDB) ---
REM Trang chinh thuc: https://www.enterprisedb.com/download-postgresql-binaries
REM Link truc tiep zip binaries PG 16 (Windows x64). Doi so build khi co ban moi.
set "PG_ZIP_URL=https://get.enterprisedb.com/postgresql/postgresql-16.4-1-windows-x64-binaries.zip"
set "PG_ZIP=%DL_DIR%\postgresql-16-win-x64-binaries.zip"

echo(
echo ==========================================================
echo   DGroup - Portable PostgreSQL 16 (Windows DEV)
echo ==========================================================
echo   Script dir : %SCRIPT_DIR%
echo   Config     : %CONFIG_FILE%
echo(

if not exist "%CONFIG_FILE%" (
    echo [LOI] Khong tim thay config.json tai: "%CONFIG_FILE%"
    goto :fail
)

REM ---------------------------------------------------------------------------
REM  DOC CONFIG BANG POWERSHELL (chinh xac hon findstr; chiu duoc JSON long nhau)
REM  In ra cac dong "KEY=VALUE" roi FOR /F nap vao bien moi truong.
REM ---------------------------------------------------------------------------
set "PG_PORT="
set "PG_USER="
set "PG_PASS="
set "PG_DBNAME="
set "PG_HOST="

for /f "usebackq delims=" %%L in (`powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$c = Get-Content -Raw -LiteralPath '%CONFIG_FILE%' | ConvertFrom-Json; $p = $c.dgroup_postgress; Write-Output ('PG_PORT=' + $p.port); Write-Output ('PG_USER=' + $p.user); Write-Output ('PG_PASS=' + $p.pass); Write-Output ('PG_DBNAME=' + $p.dbname); Write-Output ('PG_HOST=' + $p.host)"`) do (
    set "%%L"
)

if not defined PG_PORT (
    echo [LOI] Khong doc duoc cau hinh tu config.json ^(khoi dgroup_postgress^).
    echo       Kiem tra PowerShell co san va config.json dung dinh dang JSON.
    goto :fail
)

echo   PG_HOST    : %PG_HOST%
echo   PG_PORT    : %PG_PORT%
echo   PG_USER    : %PG_USER%
echo   PG_DBNAME  : %PG_DBNAME%
echo   PG_DATA    : %PG_DATA%
echo(

REM ---------------------------------------------------------------------------
REM  1) NEU CHUA CO PG16 PORTABLE -> TAI + GIAI NEN
REM ---------------------------------------------------------------------------
if exist "%PG_BIN%\pg_ctl.exe" goto :have_binaries

echo [1/4] Chua thay PostgreSQL portable trong "%PG_HOME%".
echo       Tien hanh tai zip binaries PG16...
if not exist "%DL_DIR%" mkdir "%DL_DIR%"

if not exist "%PG_ZIP%" (
    echo       Dang tai: %PG_ZIP_URL%
    powershell -NoProfile -ExecutionPolicy Bypass -Command ^
      "$ProgressPreference='SilentlyContinue'; try { Invoke-WebRequest -Uri '%PG_ZIP_URL%' -OutFile '%PG_ZIP%' -UseBasicParsing } catch { Write-Host $_.Exception.Message; exit 1 }"
    if errorlevel 1 (
        echo [LOI] Tai that bai. Hay tai thu cong tu:
        echo       https://www.enterprisedb.com/download-postgresql-binaries
        echo       roi giai nen sao cho co "%PG_BIN%\pg_ctl.exe"
        goto :fail
    )
) else (
    echo       Da co file zip tai san: "%PG_ZIP%"
)

echo       Dang giai nen vao "%PG_HOME%"...
REM Zip cua EnterpriseDB co thu muc goc "pgsql\" -> giai vao SCRIPT_DIR
REM se tao thang "%SCRIPT_DIR%\pgsql".
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "try { Expand-Archive -LiteralPath '%PG_ZIP%' -DestinationPath '%SCRIPT_DIR%' -Force } catch { Write-Host $_.Exception.Message; exit 1 }"
if errorlevel 1 (
    echo [LOI] Giai nen that bai.
    goto :fail
)

if not exist "%PG_BIN%\pg_ctl.exe" (
    echo [LOI] Sau khi giai nen van khong thay "%PG_BIN%\pg_ctl.exe".
    echo       Kiem tra cau truc zip ^(co the goc khong phai "pgsql\"^).
    goto :fail
)
echo       OK: da co PostgreSQL portable.

:have_binaries
echo [1/4] PostgreSQL portable: OK  ^("%PG_BIN%"^)

REM ---------------------------------------------------------------------------
REM  2) NEU CHUA CO DATA DIR -> initdb (UTF8)
REM ---------------------------------------------------------------------------
set "FIRST_INIT=0"
if exist "%PG_DATA%\PG_VERSION" goto :have_data

echo [2/4] Chua co data dir -^> initdb ^(UTF8^)...
if not exist "%PG_DATA%" mkdir "%PG_DATA%"

REM Luu superuser password ra file tam de initdb doc (tranh nhap tay).
set "PWFILE=%TEMP%\dgroup_pgpw_%RANDOM%.txt"
> "%PWFILE%" echo(%PG_PASS%

REM Superuser = user trong config (de dong nhat chuoi ket noi).
"%PG_BIN%\initdb.exe" -D "%PG_DATA%" -U "%PG_USER%" --pwfile="%PWFILE%" -E UTF8 --locale=C --auth-host=scram-sha-256 --auth-local=scram-sha-256
set "INITRC=%ERRORLEVEL%"
del /q "%PWFILE%" >nul 2>&1
if not "%INITRC%"=="0" (
    echo [LOI] initdb that bai ^(ma loi %INITRC%^).
    goto :fail
)
set "FIRST_INIT=1"
echo       OK: initdb xong. Superuser = %PG_USER%.
goto :data_done

:have_data
echo [2/4] Data dir da ton tai: OK

:data_done

REM ---------------------------------------------------------------------------
REM  3) START BANG pg_ctl THEO PORT TRONG CONFIG
REM ---------------------------------------------------------------------------
echo [3/4] Khoi dong PostgreSQL tren port %PG_PORT%...

"%PG_BIN%\pg_ctl.exe" -D "%PG_DATA%" status >nul 2>&1
if not errorlevel 1 (
    echo       PostgreSQL da dang chay san. Bo qua buoc start.
    goto :ensure_db
)

REM -o truyen tham so cho postgres: set port + chi lang nghe localhost tren may dev.
REM QUAN TRONG: phai start pg_ctl trong mot console AN rieng (Start-Process
REM -WindowStyle Hidden), KHONG chay truc tiep. Neu chay truc tiep, postgres se
REM dinh console hien tai -> bam Ctrl+C dung server .NET (hoac dong cua so) la
REM Windows gui CTRL_C/CLOSE cho ca postgres, PG chet giua chung (exception
REM 0xC000013A trong pg_log.txt) va server bao loi 10054/10061.
REM Van giu -w (doi PG san sang). LUU Y: dung $p.WaitForExit() chi doi rieng
REM pg_ctl; KHONG dung Start-Process -Wait vi PS 5.1 -Wait doi ca cay process
REM con (gom postgres.exe) -> treo vinh vien.
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$q=[char]34; $a='-D '+$q+$env:PG_DATA+$q+' -l '+$q+$env:PG_LOG+$q+' -o '+$q+'-p '+$env:PG_PORT+' -c listen_addresses=localhost'+$q+' -w start'; $p=Start-Process -FilePath ($env:PG_BIN+'\pg_ctl.exe') -ArgumentList $a -WindowStyle Hidden -PassThru; $p.WaitForExit(); exit $p.ExitCode"
if errorlevel 1 (
    echo [LOI] pg_ctl start that bai. Xem log: "%PG_LOG%"
    goto :fail
)
echo       OK: PostgreSQL dang chay. Log: "%PG_LOG%"

:ensure_db
REM ---------------------------------------------------------------------------
REM  4) TAO DATABASE NEU CHUA CO (user da la superuser do initdb tao)
REM ---------------------------------------------------------------------------
echo [4/4] Kiem tra / tao database "%PG_DBNAME%"...
set "PGPASSWORD=%PG_PASS%"

set "DB_EXISTS="
REM  LUU Y cmd.exe: lenh trong for /f (`...`) chay qua "cmd /c" an. Neu chuoi lenh
REM  VUA bat dau VUA ket thuc bang dau nhay ", cmd /c cat cap nhay ngoai cung ->
REM  hong duong dan .exe. Vi vay pushd vao PG_BIN roi goi psql.exe tuong doi
REM  (chuoi khong con bat dau bang ") de tranh loi.
pushd "%PG_BIN%"
for /f "usebackq delims=" %%D in (`.\psql.exe -h localhost -p %PG_PORT% -U %PG_USER% -d postgres -tAc "SELECT 1 FROM pg_database WHERE datname='%PG_DBNAME%'"`) do set "DB_EXISTS=%%D"
popd

if "%DB_EXISTS%"=="1" (
    echo       Database "%PG_DBNAME%" da ton tai: OK
) else (
    echo       Tao database "%PG_DBNAME%" ^(owner=%PG_USER%, encoding=UTF8^)...
    "%PG_BIN%\psql.exe" -h localhost -p %PG_PORT% -U %PG_USER% -d postgres -v ON_ERROR_STOP=1 -c "CREATE DATABASE \"%PG_DBNAME%\" OWNER \"%PG_USER%\" ENCODING 'UTF8' TEMPLATE template0;"
    if errorlevel 1 (
        echo [LOI] Tao database that bai.
        set "PGPASSWORD="
        goto :fail
    )
    echo       OK: da tao database.
)
set "PGPASSWORD="

echo(
echo ==========================================================
echo   HOAN TAT. PostgreSQL 16 dang chay.
echo ----------------------------------------------------------
echo   Host    : %PG_HOST%
echo   Port    : %PG_PORT%
echo   User    : %PG_USER%
echo   DB      : %PG_DBNAME%
echo   Data    : %PG_DATA%
echo   Log     : %PG_LOG%
echo(
echo   Chuoi ket noi Npgsql:
echo   Host=%PG_HOST%;Port=%PG_PORT%;Database=%PG_DBNAME%;Username=%PG_USER%;Password=***
echo(
echo   Ket noi thu bang psql:
echo   "%PG_BIN%\psql.exe" -h %PG_HOST% -p %PG_PORT% -U %PG_USER% -d %PG_DBNAME%
echo(
echo   Dung server: chay  stop_pg.bat
echo ==========================================================
endlocal
exit /b 0

:fail
echo(
echo [THAT BAI] Xem thong bao loi ben tren.
set "PGPASSWORD="
endlocal
exit /b 1
