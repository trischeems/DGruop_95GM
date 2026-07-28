#!/usr/bin/env bash
# ============================================================================
#  GM95 - run_server.sh  (o ROOT du an — tuong duong run_server.bat)
#  Build & chay server .NET 8 (ASP.NET Core, kieu Controller) tren Linux.
#  - Doc server.port + https tu Server/config.json (jq, fallback python3)
#  - Bao dam PostgreSQL dang chay (goi Server/start_pg.sh neu can)
#  - dotnet run --project Server
#  - In URL http(s)://localhost:port
#  Chiu duoc duong dan co khoang trang.
# ============================================================================
set -u

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

SERVER_DIR="$ROOT_DIR/Server"
CONFIG_FILE="$SERVER_DIR/config.json"

fail() {
    echo
    echo "[THAT BAI] Xem thong bao loi ben tren."
    exit 1
}

echo
echo "=========================================================="
echo "  GM95 - Run Server (.NET 8)"
echo "=========================================================="
echo "  Root   : $ROOT_DIR"
echo "  Server : $SERVER_DIR"
echo

# --- Kiem tra dotnet SDK ---
if ! command -v dotnet >/dev/null 2>&1; then
    echo "[LOI] Khong tim thay 'dotnet'. Cai .NET 8 SDK: https://dotnet.microsoft.com/download/dotnet/8.0"
    echo "      Ubuntu: sudo apt install -y dotnet-sdk-8.0"
    fail
fi

if [[ ! -f "$CONFIG_FILE" ]]; then
    echo "[LOI] Khong thay config.json tai \"$CONFIG_FILE\""
    fail
fi

# --- Doc port + https tu config.json (jq neu co, fallback python3) ---
json_get() { # json_get <file> <duong.dan.khoa>
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

SRV_PORT="$(json_get "$CONFIG_FILE" server.port)"
SRV_HTTPS="$(json_get "$CONFIG_FILE" https | tr '[:upper:]' '[:lower:]')"

if [[ -z "$SRV_PORT" ]]; then
    echo "[LOI] Khong doc duoc server.port tu config.json."
    fail
fi

SCHEME="http"
[[ "$SRV_HTTPS" == "true" ]] && SCHEME="https"

echo "  Port   : $SRV_PORT"
echo "  Scheme : $SCHEME"
echo

# --- Bao dam PostgreSQL dang chay (goi start_pg.sh) ---
# start_pg.sh tu bo qua neu PG da chay. Bo doan duoi neu ban tu quan ly PG.
echo "[1/2] Bao dam PostgreSQL dang chay..."
if ! bash "$SERVER_DIR/start_pg.sh"; then
    echo "[CANH BAO] start_pg.sh bao loi. Van tiep tuc chay server (co the PG da chay)."
fi
echo

# --- Bien moi truong cho Kestrel/ASP.NET Core ---
# ASPNETCORE_URLS ep Kestrel lang nghe dung port trong config.
export ASPNETCORE_ENVIRONMENT="Development"
export ASPNETCORE_URLS="$SCHEME://localhost:$SRV_PORT"
export DOTNET_CLI_TELEMETRY_OPTOUT=1

# Khi https: bao dam co dev certificate cho Kestrel (tao neu chua co, khong loi neu da co).
if [[ "$SCHEME" == "https" ]]; then
    dotnet dev-certs https >/dev/null 2>&1 || true
fi

echo "[2/2] Build va chay server .NET 8..."
echo
echo "=========================================================="
echo "  Server URL: $SCHEME://localhost:$SRV_PORT"
if [[ "$SCHEME" == "https" ]]; then
    echo "  (LUU Y: Program.cs hien bind ListenLocalhost khong TLS -> neu Kestrel"
    echo "   log 'Now listening on: http://...' thi dung http://localhost:$SRV_PORT)"
fi
echo "  (Ctrl+C de dung server)"
echo "=========================================================="
echo

# Chay tu ROOT, tro toi project Server. Kestrel doc ASPNETCORE_URLS o tren.
dotnet run --project "$SERVER_DIR"
RC=$?

echo
echo "Server da dung (ma thoat $RC)."
exit $RC
