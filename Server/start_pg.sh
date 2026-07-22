#!/usr/bin/env bash
# ============================================================================
#  DGroup - start_pg.sh  (tuong duong start_pg.bat cho Linux/Ubuntu)
#  PostgreSQL 16 cho may Ubuntu (DEV/PROD).
#  - Doc port/user/pass/dbname tu Server/config.json (khoi dgroup_postgress)
#  - Dung binaries PG16: uu tien Server/pgsql/bin (portable neu co),
#    roi den ban cai apt PGDG (/usr/lib/postgresql/16/bin), roi pg_config.
#    Neu chua cai -> in huong dan cai apt PGDG (khong Docker).
#  - Neu chua co data dir Server/pgdata -> initdb (encoding UTF8)
#  - Start bang pg_ctl theo port trong config
#  - Lan dau: tao database (user = superuser tao boi initdb voi password config)
#  Chiu duoc duong dan co khoang trang. Parse JSON bang jq (fallback python3).
# ============================================================================
set -u

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

CONFIG_FILE="$SCRIPT_DIR/config.json"
PG_HOME="$SCRIPT_DIR/pgsql"
PG_DATA="$SCRIPT_DIR/pgdata"
PG_LOG="$SCRIPT_DIR/pg_log.txt"
# Socket unix de o /tmp: thu muc mac dinh /var/run/postgresql cua Ubuntu
# thuoc user "postgres", user thuong khong ghi duoc.
PG_SOCKET_DIR="/tmp"

fail() {
    echo
    echo "[THAT BAI] Xem thong bao loi ben tren."
    exit 1
}

echo
echo "=========================================================="
echo "  DGroup - PostgreSQL 16 (Linux)"
echo "=========================================================="
echo "  Script dir : $SCRIPT_DIR"
echo "  Config     : $CONFIG_FILE"
echo

if [[ ! -f "$CONFIG_FILE" ]]; then
    echo "[LOI] Khong tim thay config.json tai: \"$CONFIG_FILE\""
    fail
fi

# initdb / postgres tu choi chay bang root.
if [[ "$(id -u)" == "0" ]]; then
    echo "[LOI] Khong chay script nay bang root/sudo (PostgreSQL khong cho chay bang root)."
    fail
fi

# ---------------------------------------------------------------------------
#  TIM BINARIES PG16
# ---------------------------------------------------------------------------
PG_BIN=""
if [[ -x "$PG_HOME/bin/pg_ctl" ]]; then
    PG_BIN="$PG_HOME/bin"
elif [[ -x "/usr/lib/postgresql/16/bin/pg_ctl" ]]; then
    PG_BIN="/usr/lib/postgresql/16/bin"
elif command -v pg_config >/dev/null 2>&1; then
    PG_BIN="$(pg_config --bindir)"
fi

if [[ -z "$PG_BIN" || ! -x "$PG_BIN/pg_ctl" ]]; then
    echo "[LOI] Khong tim thay PostgreSQL 16 (pg_ctl). Cai PGDG tren Ubuntu:"
    echo "        sudo apt install -y postgresql-common"
    echo "        sudo /usr/share/postgresql-common/pgdg/apt.postgresql.org.sh -y"
    echo "        sudo apt install -y postgresql-16"
    echo "      (Chi can package binaries; script nay tu quan ly data dir rieng.)"
    fail
fi

# ---------------------------------------------------------------------------
#  DOC CONFIG (jq neu co, fallback python3)
# ---------------------------------------------------------------------------
json_get() { # json_get <file> <duong.dan.khoa>  vd: json_get "$CONFIG_FILE" dgroup_postgress.port
    local file="$1" path="$2" val=""
    if command -v jq >/dev/null 2>&1; then
        val="$(jq -r ".$path" "$file" 2>/dev/null)"
        [[ "$val" == "null" ]] && val=""
    else
        val="$(python3 - "$path" "$file" 2>/dev/null <<'PY'
import json, sys
obj = json.load(open(sys.argv[2]))
for key in sys.argv[1].split('.'):
    obj = obj.get(key) if isinstance(obj, dict) else None
if obj is None:
    pass
elif isinstance(obj, bool):
    print("true" if obj else "false")
else:
    print(obj)
PY
)"
    fi
    printf '%s' "$val"
}

PG_PORT="$(json_get "$CONFIG_FILE" dgroup_postgress.port)"
PG_USER="$(json_get "$CONFIG_FILE" dgroup_postgress.user)"
PG_PASS="$(json_get "$CONFIG_FILE" dgroup_postgress.pass)"
PG_DBNAME="$(json_get "$CONFIG_FILE" dgroup_postgress.dbname)"
PG_HOST="$(json_get "$CONFIG_FILE" dgroup_postgress.host)"

if [[ -z "$PG_PORT" ]]; then
    echo "[LOI] Khong doc duoc cau hinh tu config.json (khoi dgroup_postgress)."
    echo "      Kiem tra jq/python3 co san va config.json dung dinh dang JSON."
    fail
fi

echo "  PG_BIN     : $PG_BIN"
echo "  PG_HOST    : $PG_HOST"
echo "  PG_PORT    : $PG_PORT"
echo "  PG_USER    : $PG_USER"
echo "  PG_DBNAME  : $PG_DBNAME"
echo "  PG_DATA    : $PG_DATA"
echo

echo "[1/4] PostgreSQL binaries: OK (\"$PG_BIN\")"

# ---------------------------------------------------------------------------
#  2) NEU CHUA CO DATA DIR -> initdb (UTF8)
# ---------------------------------------------------------------------------
if [[ -f "$PG_DATA/PG_VERSION" ]]; then
    echo "[2/4] Data dir da ton tai: OK"
else
    echo "[2/4] Chua co data dir -> initdb (UTF8)..."
    mkdir -p "$PG_DATA"

    # Luu superuser password ra file tam de initdb doc (tranh nhap tay).
    PWFILE="$(mktemp)"
    chmod 600 "$PWFILE"
    printf '%s\n' "$PG_PASS" > "$PWFILE"

    # Superuser = user trong config (de dong nhat chuoi ket noi).
    if ! "$PG_BIN/initdb" -D "$PG_DATA" -U "$PG_USER" --pwfile="$PWFILE" \
            -E UTF8 --locale=C --auth-host=scram-sha-256 --auth-local=scram-sha-256; then
        rm -f "$PWFILE"
        echo "[LOI] initdb that bai."
        fail
    fi
    rm -f "$PWFILE"
    echo "      OK: initdb xong. Superuser = $PG_USER."
fi

# ---------------------------------------------------------------------------
#  3) START BANG pg_ctl THEO PORT TRONG CONFIG
# ---------------------------------------------------------------------------
echo "[3/4] Khoi dong PostgreSQL tren port $PG_PORT..."

if "$PG_BIN/pg_ctl" -D "$PG_DATA" status >/dev/null 2>&1; then
    echo "      PostgreSQL da dang chay san. Bo qua buoc start."
else
    # -o truyen tham so cho postgres: set port + chi lang nghe localhost.
    # Tren Linux pg_ctl tu detach postgres (setsid), khong bi dinh Ctrl+C
    # cua console nhu Windows -> chay truc tiep, van giu -w (doi PG san sang).
    if ! "$PG_BIN/pg_ctl" -D "$PG_DATA" -l "$PG_LOG" \
            -o "-p $PG_PORT -c listen_addresses=localhost -c unix_socket_directories='$PG_SOCKET_DIR'" \
            -w start; then
        echo "[LOI] pg_ctl start that bai. Xem log: \"$PG_LOG\""
        fail
    fi
    echo "      OK: PostgreSQL dang chay. Log: \"$PG_LOG\""
fi

# ---------------------------------------------------------------------------
#  4) TAO DATABASE NEU CHUA CO (user da la superuser do initdb tao)
# ---------------------------------------------------------------------------
echo "[4/4] Kiem tra / tao database \"$PG_DBNAME\"..."
export PGPASSWORD="$PG_PASS"

DB_EXISTS="$("$PG_BIN/psql" -h localhost -p "$PG_PORT" -U "$PG_USER" -d postgres -tAc \
    "SELECT 1 FROM pg_database WHERE datname='$PG_DBNAME'" 2>/dev/null || true)"

if [[ "$DB_EXISTS" == "1" ]]; then
    echo "      Database \"$PG_DBNAME\" da ton tai: OK"
else
    echo "      Tao database \"$PG_DBNAME\" (owner=$PG_USER, encoding=UTF8)..."
    if ! "$PG_BIN/psql" -h localhost -p "$PG_PORT" -U "$PG_USER" -d postgres -v ON_ERROR_STOP=1 \
            -c "CREATE DATABASE \"$PG_DBNAME\" OWNER \"$PG_USER\" ENCODING 'UTF8' TEMPLATE template0;"; then
        echo "[LOI] Tao database that bai."
        unset PGPASSWORD
        fail
    fi
    echo "      OK: da tao database."
fi
unset PGPASSWORD

echo
echo "=========================================================="
echo "  HOAN TAT. PostgreSQL 16 dang chay."
echo "----------------------------------------------------------"
echo "  Host    : $PG_HOST"
echo "  Port    : $PG_PORT"
echo "  User    : $PG_USER"
echo "  DB      : $PG_DBNAME"
echo "  Data    : $PG_DATA"
echo "  Log     : $PG_LOG"
echo
echo "  Chuoi ket noi Npgsql:"
echo "  Host=$PG_HOST;Port=$PG_PORT;Database=$PG_DBNAME;Username=$PG_USER;Password=***"
echo
echo "  Ket noi thu bang psql:"
echo "  \"$PG_BIN/psql\" -h $PG_HOST -p $PG_PORT -U $PG_USER -d $PG_DBNAME"
echo
echo "  Dung server: chay  stop_pg.sh"
echo "=========================================================="
exit 0
