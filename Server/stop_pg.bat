@echo off
setlocal EnableDelayedExpansion
REM ============================================================================
REM  DGroup - stop_pg.bat
REM  Dung PostgreSQL 16 portable dang chay tren may DEV.
REM  Dung pg_ctl stop -m fast (dong cac ket noi dang chay, rollback tx do dang,
REM  roi tat sach se). Doc data dir = Server\pgdata.
REM  Chiu duoc duong dan co khoang trang.
REM ============================================================================

set "SCRIPT_DIR=%~dp0"
if "%SCRIPT_DIR:~-1%"=="\" set "SCRIPT_DIR=%SCRIPT_DIR:~0,-1%"

set "PG_BIN=%SCRIPT_DIR%\pgsql\bin"
set "PG_DATA=%SCRIPT_DIR%\pgdata"

echo(
echo ==========================================================
echo   DGroup - Stop PostgreSQL 16 (Windows DEV)
echo ==========================================================
echo   Data : %PG_DATA%
echo(

if not exist "%PG_BIN%\pg_ctl.exe" (
    echo [LOI] Khong thay pg_ctl.exe tai "%PG_BIN%".
    echo       Co le PostgreSQL portable chua duoc cai. Chay start_pg.bat truoc.
    goto :fail
)

if not exist "%PG_DATA%\PG_VERSION" (
    echo [LOI] Khong thay data dir hop le tai "%PG_DATA%".
    goto :fail
)

REM Kiem tra trang thai truoc khi stop.
"%PG_BIN%\pg_ctl.exe" -D "%PG_DATA%" status >nul 2>&1
if errorlevel 1 (
    echo   PostgreSQL hien KHONG chay. Khong can dung.
    goto :done
)

echo   Dang dung PostgreSQL ^(mode: fast^)...
"%PG_BIN%\pg_ctl.exe" -D "%PG_DATA%" -m fast -w stop
if errorlevel 1 (
    echo [LOI] pg_ctl stop that bai. Thu lai hoac kiem tra pg_log.txt.
    goto :fail
)
echo   OK: da dung PostgreSQL.

:done
echo(
echo ==========================================================
echo   Hoan tat.
echo ==========================================================
endlocal
exit /b 0

:fail
echo(
echo [THAT BAI] Xem thong bao loi ben tren.
endlocal
exit /b 1
