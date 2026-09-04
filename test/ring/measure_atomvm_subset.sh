#!/usr/bin/env bash
# test/ring/measure_atomvm_subset.sh — the SECOND half of T017 (feature 101, ruling
# Q-GLPNETS17-01: adopt the measured boundary AND install AtomVM to measure exhaustively).
#
# Runs INSIDE a Linux environment (WSL2 Ubuntu on GAVRIELLA) because AtomVM publishes no
# official Windows binary — the host build is generic_unix.
#
# What it does, in order, and it stops at the first thing that does not hold:
#   1. resolves the real AtomVM release assets from the GitHub API — never a guessed URL
#      (a guessed one 404'd on the first attempt, which is why this step exists);
#   2. downloads and unpacks a host build;
#   3. runs it, recording the version actually exercised;
#   4. reproduces the dossier's own result first — gleam_otp must FAIL with
#      "module proc_lib cannot be resolved". If that does not reproduce, the provenance of
#      atomvm-unsupported.list is broken and the list must be RE-DERIVED, not extended.
#
# Everything it learns is written to test/ring/atomvm-measurement.txt as raw observations.
# Nothing is added to atomvm-unsupported.list automatically: an entry there must be an
# observation a human has read, and auto-appending would let an inference in by the back door.
#
# Usage (from Windows):  wsl -d Ubuntu -- bash /mnt/d/BSTDEV/research/GLP/GLPNET/test/ring/measure_atomvm_subset.sh
# Exit: 0 measured · 1 the dossier result did not reproduce · 2 setup/toolchain

set -u
OUT_DIR="${OUT_DIR:-/mnt/d/BSTDEV/research/GLP/GLPNET/test/ring}"
REPORT="$OUT_DIR/atomvm-measurement.txt"
WORK="${WORK:-/root/atomvm-work}"
VERSION="${ATOMVM_VERSION:-v0.6.6}"

mkdir -p "$WORK" || { echo "cannot create $WORK" >&2; exit 2; }

log() { echo "$@" | tee -a "$REPORT"; }

: > "$REPORT" 2>/dev/null || { echo "cannot write $REPORT" >&2; exit 2; }

log "== AtomVM subset measurement — T017 second half =="
log "   host:    $(uname -srm)"
log "   distro:  $(. /etc/os-release 2>/dev/null && echo "$PRETTY_NAME")"
log "   date:    $(date -u +%Y-%m-%dT%H:%M:%SZ)"
log "   target:  AtomVM $VERSION"
log ""

command -v curl >/dev/null 2>&1 || { log "REFUSED: no curl"; exit 2; }
command -v python3 >/dev/null 2>&1 || { log "REFUSED: no python3"; exit 2; }

# --- 1 · resolve the real assets -------------------------------------------
log "-- step 1: resolve release assets (never a guessed URL) --"
API="https://api.github.com/repos/atomvm/AtomVM/releases/tags/$VERSION"
HTTP="$(curl -sSL "$API" -o "$WORK/rel.json" -w '%{http_code}')"
log "   GET $API -> HTTP $HTTP  ($(wc -c < "$WORK/rel.json" 2>/dev/null || echo 0) bytes)"

if [ "$HTTP" != "200" ]; then
    log "REFUSED: the release API did not return 200. Nothing downstream can be trusted;"
    log "         an unmeasured subset must not be resolved in favour of passing."
    exit 2
fi

python3 - "$WORK/rel.json" >> "$REPORT" <<'PYEOF'
import json, sys
d = json.load(open(sys.argv[1]))
print("   tag: %s" % d.get("tag_name"))
assets = d.get("assets", [])
print("   assets: %d" % len(assets))
for a in assets:
    print("     - %s  (%s bytes)" % (a.get("name"), a.get("size")))
PYEOF
cat "$REPORT" | tail -20

# Pick a linux x86_64 host build if one exists.
ASSET_URL="$(python3 - "$WORK/rel.json" <<'PYEOF'
import json, sys
d = json.load(open(sys.argv[1]))
cands = [a for a in d.get("assets", [])
         if "linux" in a["name"].lower() and ("x86_64" in a["name"] or "amd64" in a["name"])
         and not a["name"].endswith(".sha256")]
print(cands[0]["browser_download_url"] if cands else "")
PYEOF
)"

log ""
if [ -z "$ASSET_URL" ]; then
    log "-- no prebuilt linux x86_64 host asset in $VERSION --"
    log "   This is an OBSERVATION, not a failure of the port: AtomVM's releases are"
    log "   predominantly device images (ESP32/STM32/RP2). A generic_unix host build must then"
    log "   be built from source, which is a larger task than this script's scope."
    log "   RECORDED. The list stays a lower bound; see install-atomvm.md."
    exit 2
fi

log "-- step 2: download $ASSET_URL --"
DL="$WORK/atomvm.download"
curl -sSL -o "$DL" "$ASSET_URL" -w '   HTTP %{http_code}, %{size_download} bytes
' | tee -a "$REPORT"
log "   file type: $(file -b "$DL" 2>/dev/null || echo unknown)"

# The v0.6.6 linux asset is a BARE ELF EXECUTABLE, not an archive. The dossier records the
# build as "AtomVM-linux-x86_64-static-mbedtls-v0.6.6"; NO ASSET OF THAT NAME EXISTS in the
# release (measured 2026-09-04) - the real one is "AtomVM-linux-x86_64-v0.6.6". Handle both
# shapes rather than assuming either.
case "$(file -b "$DL" 2>/dev/null)" in
    *gzip*|*tar*) ( cd "$WORK" && tar xzf "$DL" 2>/dev/null ) && log "   unpacked as tarball" ;;
    *ELF*)        cp "$DL" "$WORK/AtomVM" && chmod +x "$WORK/AtomVM" && log "   bare ELF executable -> $WORK/AtomVM" ;;
    *)            log "   unrecognised artifact shape; inspect $WORK manually" ;;
esac
ls -la "$WORK" | head -20 | tee -a "$REPORT"

log ""
log "-- step 3: run it --"
AVM="$(find "$WORK" -maxdepth 3 -type f \( -name 'AtomVM' -o -name 'atomvm' \) 2>/dev/null | head -1)"
if [ -n "$AVM" ]; then
    chmod +x "$AVM" 2>/dev/null
    log "   binary: $AVM"
    "$AVM" --version 2>&1 | head -3 | sed 's/^/   /' | tee -a "$REPORT"
else
    log "   no AtomVM binary found after unpack — RECORDED, list stays a lower bound"
    exit 2
fi

log ""
log "-- step 4: reproduce the dossier result BEFORE extending the list --"
log "   Required: a gleam_otp build must fail with 'module proc_lib cannot be resolved'."
log "   If it does not reproduce, atomvm-unsupported.list must be RE-DERIVED, not extended."
log "   (Requires a Gleam toolchain in this environment; see install-atomvm.md step 2.)"
# Codex review 20260904T055230Z (P1): this step previously performed no build, no packaging and
# no AtomVM invocation, then exited 0 — the documented command could report the advertised
# "0 measured" status without reproducing anything. That is precisely the silent-empty result
# this feature forbids. The reproduction lives in wsl-reproduce-dossier.sh and is now INVOKED,
# with its exit status propagated.
command -v gleam >/dev/null 2>&1 || {
    log "   gleam not present in WSL — reproduction cannot be attempted."
    log ""
    log "INCOMPLETE: AtomVM binary obtained and runnable, but the construct enumeration was NOT"
    log "            verified. Exiting NON-ZERO: a measurement that did not happen must never"
    log "            report success. Install gleam+erlang (wsl-setup-toolchain.sh) and re-run."
    exit 1
}

REPRO="$(dirname "$0")/wsl-reproduce-dossier.sh"
if [ ! -f "$REPRO" ]; then
    log "   reproduction script missing at $REPRO"
    log "INCOMPLETE: cannot verify the list's provenance. Exiting non-zero."
    exit 1
fi
log "   invoking the reproduction: $REPRO"
tr -d '' < "$REPRO" > /tmp/_repro.sh && bash /tmp/_repro.sh
REPRO_RC=$?
log "   reproduction rc=$REPRO_RC"
if [ "$REPRO_RC" -ne 0 ]; then
    log ""
    log "RESULT: the dossier result did NOT reproduce. atomvm-unsupported.list must be"
    log "        RE-DERIVED, not extended. Exiting non-zero."
    exit 1
fi
log ""
log "RESULT: reproduction PASSED — the list's provenance holds on this host."
exit 0

log ""
log "RESULT: see above. No entry was auto-appended to atomvm-unsupported.list —"
log "        every entry there must be an observation a human has read."
exit 0
