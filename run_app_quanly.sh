#!/usr/bin/env bash
# ============================================================================
#  GM95 - run_app_quanly.sh  (o ROOT du an — tuong duong run_app_quanly.bat)
#  Mo app client Manager_Perfoment (Avalonia UI, .NET 8) tren Linux.
#  - Doc server.base_url tu App/Manager_Perfoment/config.json (jq, fallback python3)
#  - Kiem tra server co chay khong (goi /dgrpi/health); neu chua -> nhac bat run_server.sh
#  - dotnet run --project App/Manager_Perfoment
#  Chiu duoc duong dan co khoang trang.
# ============================================================================
set -u

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

APP_DIR="$ROOT_DIR/App/Manager_Perfoment"
APP_CONFIG="$APP_DIR/config.json"

fail() {
    echo
    echo "[THAT BAI] Xem thong bao loi ben tren."
    exit 1
}

echo
echo "=========================================================="
echo "  GM95 - Run App (Manager_Perfoment)"
echo "=========================================================="
echo "  App : $APP_DIR"
echo

# --- Kiem tra dotnet SDK ---
if ! command -v dotnet >/dev/null 2>&1; then
    echo "[LOI] Khong tim thay 'dotnet'. Cai .NET 8 SDK: https://dotnet.microsoft.com/download/dotnet/8.0"
    echo "      Ubuntu: sudo apt install -y dotnet-sdk-8.0"
    fail
fi

if [[ ! -f "$APP_CONFIG" ]]; then
    echo "[LOI] Khong thay config app tai \"$APP_CONFIG\""
    fail
fi

# --- Doc server.base_url tu config.json cua app ---
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

BASE_URL="$(json_get "$APP_CONFIG" server.base_url)"

if [[ -z "$BASE_URL" ]]; then
    echo "[CANH BAO] Khong doc duoc server.base_url tu config.json. Van tiep tuc mo app."
else
    echo "  Server : $BASE_URL"
    echo
    echo "[1/2] Kiem tra server co dang chay..."
    # -k: chap nhan dev certificate tu ky khi server chay https.
    if curl -kfsS -m 3 -o /dev/null "$BASE_URL/dgrpi/health" 2>/dev/null; then
        echo "      OK: server dang chay."
    else
        echo "      [CANH BAO] Chua ket noi duoc server tai $BASE_URL."
        echo "      Hay mo server truoc bang:  ./run_server.sh"
        echo "      (Van tiep tuc mo app; bam 'Lam moi' trong app sau khi server chay.)"
    fi
fi
echo

echo "[2/2] Build va mo app..."
echo
dotnet run --project "$APP_DIR"
RC=$?

echo
echo "App da dong (ma thoat $RC)."
exit $RC
