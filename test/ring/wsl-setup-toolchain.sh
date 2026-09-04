#!/usr/bin/env bash
# test/ring/wsl-setup-toolchain.sh — install Erlang + Gleam inside WSL so the AtomVM subset
# can be measured by observation (feature 101, T017 second half · ruling Q-GLPNETS17-01).
#
# Run it CR-stripped — the repo checks out CRLF on Windows and WSL bash will not execute that:
#   wsl -d Ubuntu -- bash -lc "tr -d '\r' < /mnt/d/.../wsl-setup-toolchain.sh > /tmp/s.sh && bash /tmp/s.sh"
#
# Exists as a file rather than an inline command because the PowerShell -> wsl -> bash -lc
# quoting chain mangles nested quotes; two attempts died on it before this file existed.

set -u
LOG="${LOG:-/tmp/wsl-toolchain.log}"
exec > >(tee -a "$LOG") 2>&1

echo "== WSL toolchain setup for AtomVM measurement =="
echo "   date: $(date -u +%Y-%m-%dT%H:%M:%SZ)"
echo "   distro: $(. /etc/os-release 2>/dev/null && echo "$PRETTY_NAME")"
echo ""

export DEBIAN_FRONTEND=noninteractive

echo "-- apt-get update --"
apt-get update -qq 2>&1 | tail -3
echo "   rc=$?"

echo ""
echo "-- install erlang --"
# Ubuntu 26.04 ships erlang in universe; install the pieces a Gleam build needs.
apt-get install -y -qq erlang-base erlang-dev erlang-crypto erlang-inets erlang-ssl rebar3 2>&1 | tail -5
echo "   rc=$?"

echo ""
echo "-- verify erlang --"
if command -v erl >/dev/null 2>&1; then
    erl -noshell -eval 'io:format("OTP ~s~n", [erlang:system_info(otp_release)]), halt().' 2>&1 | head -2
else
    echo "   erl NOT on PATH after install"
fi

echo ""
echo "-- install gleam --"
GLEAM_VER="${GLEAM_VER:-1.12.0}"
ARCH="x86_64-unknown-linux-musl"
URL="https://github.com/gleam-lang/gleam/releases/download/v${GLEAM_VER}/gleam-v${GLEAM_VER}-${ARCH}.tar.gz"
cd /tmp || exit 2
HTTP="$(curl -sSL -o gleam.tar.gz -w '%{http_code}' "$URL")"
echo "   GET $URL -> HTTP $HTTP"
if [ "$HTTP" = "200" ]; then
    tar xzf gleam.tar.gz && install -m 0755 gleam /usr/local/bin/gleam && echo "   installed: $(gleam --version 2>&1)"
else
    echo "   download failed; resolving the real asset name from the API instead"
    curl -sSL "https://api.github.com/repos/gleam-lang/gleam/releases/latest" -o /tmp/gleam-rel.json
    python3 - <<'PYEOF'
import json
try:
    d = json.load(open("/tmp/gleam-rel.json"))
    print("   latest tag:", d.get("tag_name"))
    for a in d.get("assets", []):
        n = a["name"]
        if "linux" in n and "x86_64" in n and n.endswith(".tar.gz"):
            print("   candidate:", n, a["browser_download_url"])
except Exception as e:
    print("   could not read release json:", e)
PYEOF
fi

echo ""
echo "-- summary --"
for t in erl erlc gleam rebar3; do
    if command -v "$t" >/dev/null 2>&1; then
        echo "   $t: $(command -v $t)"
    else
        echo "   $t: ABSENT"
    fi
done
echo ""
echo "log: $LOG"
