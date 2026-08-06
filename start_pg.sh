#!/usr/bin/env bash
# ============================================================================
#  GM95 - start_pg.sh  (o GOC du an — cho tien, khoi phai vao Server/)
#  Day chi la lop vo: goi thang Server/start_pg.sh (ban that, giu 1 cho duy
#  nhat de khong bi lech phien ban). Moi tham so deu duoc chuyen tiep.
#     ./start_pg.sh          -> khoi dong PostgreSQL 16
#  Dung PostgreSQL: Server/stop_pg.sh
# ============================================================================
set -u

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REAL_SCRIPT="$ROOT_DIR/Server/start_pg.sh"

if [[ ! -f "$REAL_SCRIPT" ]]; then
    echo "[LOI] Khong tim thay \"$REAL_SCRIPT\"."
    echo "      File nay phai nam o goc du an (canh thu muc Server/)."
    exit 1
fi

exec bash "$REAL_SCRIPT" "$@"
