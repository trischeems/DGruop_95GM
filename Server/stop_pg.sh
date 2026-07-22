#!/usr/bin/env bash
# ============================================================================
#  DGroup - stop_pg.sh  (tuong duong stop_pg.bat cho Linux/Ubuntu)
#  Dung PostgreSQL 16 dang chay tren may nay.
#  Dung pg_ctl stop -m fast (dong cac ket noi dang chay, rollback tx do dang,
#  roi tat sach se). Doc data dir = Server/pgdata.
#  Chiu duoc duong dan co khoang trang.
# ============================================================================
set -u

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

PG_HOME="$SCRIPT_DIR/pgsql"
PG_DATA="$SCRIPT_DIR/pgdata"

fail() {
    echo
    echo "[THAT BAI] Xem thong bao loi ben tren."
    exit 1
}

echo
echo "=========================================================="
echo "  DGroup - Stop PostgreSQL 16 (Linux)"
echo "=========================================================="
echo "  Data : $PG_DATA"
echo

# Tim pg_ctl cung thu tu voi start_pg.sh (portable -> PGDG -> pg_config).
PG_BIN=""
if [[ -x "$PG_HOME/bin/pg_ctl" ]]; then
    PG_BIN="$PG_HOME/bin"
elif [[ -x "/usr/lib/postgresql/16/bin/pg_ctl" ]]; then
    PG_BIN="/usr/lib/postgresql/16/bin"
elif command -v pg_config >/dev/null 2>&1; then
    PG_BIN="$(pg_config --bindir)"
fi

if [[ -z "$PG_BIN" || ! -x "$PG_BIN/pg_ctl" ]]; then
    echo "[LOI] Khong thay pg_ctl (PostgreSQL 16 chua duoc cai?)."
    echo "      Chay start_pg.sh truoc de xem huong dan cai dat."
    fail
fi

if [[ ! -f "$PG_DATA/PG_VERSION" ]]; then
    echo "[LOI] Khong thay data dir hop le tai \"$PG_DATA\"."
    fail
fi

# Kiem tra trang thai truoc khi stop.
if ! "$PG_BIN/pg_ctl" -D "$PG_DATA" status >/dev/null 2>&1; then
    echo "  PostgreSQL hien KHONG chay. Khong can dung."
else
    echo "  Dang dung PostgreSQL (mode: fast)..."
    if ! "$PG_BIN/pg_ctl" -D "$PG_DATA" -m fast -w stop; then
        echo "[LOI] pg_ctl stop that bai. Thu lai hoac kiem tra pg_log.txt."
        fail
    fi
    echo "  OK: da dung PostgreSQL."
fi

echo
echo "=========================================================="
echo "  Hoan tat."
echo "=========================================================="
exit 0
