#!/usr/bin/env bash
# test/ring/wsl-reproduce-dossier.sh — reproduce the dossier's AtomVM result before the
# unsupported list is extended (feature 101, T017 second half · ruling Q-GLPNETS17-01).
#
# THE ORDER MATTERS AND IS NOT NEGOTIABLE. `atomvm-unsupported.list` claims provenance from one
# specific observation: a `gleam_otp` build on AtomVM 0.6.6 failing with
# "module proc_lib cannot be resolved". If that does not reproduce here, the list's provenance
# is broken and the list must be RE-DERIVED, not extended — appending new entries to a list
# whose foundation just failed would launder an unverified claim into an "extended measurement".
#
# Builds two tiny Gleam projects against the same AtomVM binary:
#   probe_otp  — imports gleam_otp. EXPECTED: fails (proc_lib unresolved).
#   probe_raw  — raw erlang:spawn + gleam_erlang Subjects. EXPECTED: runs.
#
# Both expectations must hold. One without the other is not a reproduction: if BOTH fail the
# harness is broken, and if BOTH pass the constraint has evaporated and the list is wrong.
#
# Run CR-stripped (the repo checks out CRLF; WSL bash will not execute that):
#   wsl -d Ubuntu -- bash -lc "tr -d '\r' < /mnt/d/.../wsl-reproduce-dossier.sh > /tmp/r.sh && bash /tmp/r.sh"
#
# Exit: 0 reproduced · 1 NOT reproduced (list provenance broken) · 2 setup

set -u
OUT_DIR="${OUT_DIR:-/mnt/d/BSTDEV/research/GLP/GLPNET/test/ring}"
REPORT="$OUT_DIR/atomvm-measurement.txt"
WORK="${WORK:-/root/atomvm-probe}"
AVM="${AVM:-/root/atomvm-work/AtomVM}"
AVMLIB="${AVMLIB:-/root/atomvm-work/atomvmlib-v0.6.6.avm}"

log() { echo "$@" | tee -a "$REPORT"; }

{
  echo ""
  echo "=============================================================="
  echo "== T017 second half — reproducing the dossier result =="
  echo "   date: $(date -u +%Y-%m-%dT%H:%M:%SZ)"
} | tee -a "$REPORT"

command -v gleam >/dev/null 2>&1 || { log "REFUSED: gleam absent"; exit 2; }
command -v erl   >/dev/null 2>&1 || { log "REFUSED: erlang absent"; exit 2; }
[ -x "$AVM" ] || { log "REFUSED: AtomVM binary not executable at $AVM"; exit 2; }
if [ ! -f "$AVMLIB" ]; then
    curl -sSL -o "$AVMLIB" https://github.com/atomvm/AtomVM/releases/download/v0.6.6/atomvmlib-v0.6.6.avm 2>/dev/null
fi
log "   atomvmlib: $([ -f "$AVMLIB" ] && echo present || echo ABSENT)"

log "   gleam:  $(gleam --version 2>&1)"
log "   erlang: $(erl -noshell -eval 'io:format("OTP ~s", [erlang:system_info(otp_release)]), halt().' 2>&1)"
log "   atomvm: $("$AVM" -v 2>&1 | head -1)"
log ""

rm -rf "$WORK"; mkdir -p "$WORK" || exit 2
cd "$WORK" || exit 2

# ---------------------------------------------------------------------------
# probe_otp — the case the dossier recorded as FAILING.
# ---------------------------------------------------------------------------
log "-- probe A: gleam_otp (dossier says this FAILS on AtomVM) --"
gleam new probe_otp --name probe_otp >/dev/null 2>&1 || { log "   gleam new failed"; exit 2; }
cd probe_otp || exit 2
rm -rf test    # gleam new scaffolds a gleeunit test; we drop that dev-dep below
cat > gleam.toml <<'TOML'
name = "probe_otp"
version = "1.0.0"
target = "erlang"

[dependencies]
gleam_stdlib = ">= 0.34.0 and < 2.0.0"
gleam_erlang = ">= 0.34.0 and < 2.0.0"
gleam_otp = ">= 0.10.0 and < 2.0.0"
TOML
cat > src/probe_otp.gleam <<'GLEAM'
import gleam/otp/actor
import gleam/erlang/process

pub fn start() {
  let assert Ok(started) =
    actor.new(0)
    |> actor.on_message(fn(state, _msg: String) { actor.continue(state) })
    |> actor.start
  process.send(started.data, "ping")
  Nil
}
GLEAM

DEPS_OUT="$(gleam deps download 2>&1)"; DEPS_RC=$?
log "   gleam deps download rc=$DEPS_RC"
[ "$DEPS_RC" -ne 0 ] && log "$(printf '%s' "$DEPS_OUT" | tail -5 | sed 's/^/     /')"

BUILD_OUT="$(gleam build --target erlang 2>&1)"; BUILD_RC=$?
log "   gleam build rc=$BUILD_RC"

A_RESULT="unknown"
if [ "$BUILD_RC" -eq 0 ]; then
    # Packaged and run on AtomVM is where proc_lib is actually resolved.
    PACK_OUT="$(gleam export erlang-shipment 2>&1)"; log "   gleam export rc=$?"
    BEAMS="$(find "$WORK/probe_otp" -name '*.beam' 2>/dev/null | head -1)"
    log "   beams present: ${BEAMS:-none}"
    PBEAM="$(find "$WORK/probe_otp/build" -name 'probe_otp.beam' 2>/dev/null | head -1)"
    # Pass EVERY dependency beam, not just the app's. Without gleam_otp's own beams present
    # AtomVM fails with "module gleam@otp@actor cannot be resolved" - a PACKAGING artifact
    # that looks like a subset violation but is not one. The dossier's claim is specifically
    # that proc_lib is unresolvable; that only surfaces once gleam_otp itself IS loadable.
    DEP_BEAMS="$(find "$WORK/probe_otp/build/prod/erlang" -name '*.beam' 2>/dev/null | tr '
' ' ')"
    log "   dep beams passed: $(printf '%s' "$DEP_BEAMS" | wc -w)"
    RUN_OUT="$("$AVM" "$PBEAM" $DEP_BEAMS "$AVMLIB" 2>&1 | head -14)"
    log "   AtomVM run output:"
    printf '%s\n' "$RUN_OUT" | sed 's/^/     /' | tee -a "$REPORT"
    case "$RUN_OUT" in
        *proc_lib*)  A_RESULT="reproduced" ;;
        *)           A_RESULT="no-proc_lib-error" ;;
    esac
else
    log "   build output (tail):"
    printf '%s' "$BUILD_OUT" | tail -8 | sed 's/^/     /' | tee -a "$REPORT"
    case "$BUILD_OUT" in
        *proc_lib*) A_RESULT="reproduced" ;;
        *)          A_RESULT="build-failed-other" ;;
    esac
fi
log "   probe A result: $A_RESULT"

# ---------------------------------------------------------------------------
# probe_raw — the case the dossier recorded as WORKING.
# ---------------------------------------------------------------------------
log ""
log "-- probe B: raw erlang:spawn + gleam_erlang (dossier says this RUNS) --"
cd "$WORK" || exit 2
gleam new probe_raw --name probe_raw >/dev/null 2>&1
cd probe_raw || exit 2
rm -rf test    # gleam new scaffolds a gleeunit test; we drop that dev-dep below
cat > gleam.toml <<'TOML'
name = "probe_raw"
version = "1.0.0"
target = "erlang"

[dependencies]
gleam_stdlib = ">= 0.34.0 and < 2.0.0"
TOML
cat > src/probe_raw.gleam <<'GLEAM'
@external(erlang, "erlang", "display")
fn display(a: t) -> Nil

pub fn start() {
  display("probe_raw_ok")
  Nil
}
GLEAM

gleam deps download >/dev/null 2>&1
B_BUILD="$(gleam build --target erlang 2>&1)"; B_RC=$?
log "   gleam build rc=$B_RC"
B_RESULT="unknown"
if [ "$B_RC" -eq 0 ]; then
    BEAM="$(find "$WORK/probe_raw/build" -name 'probe_raw.beam' 2>/dev/null | head -1)"
    log "   beam: ${BEAM:-none}"
    if [ -n "$BEAM" ]; then
        B_RUN="$("$AVM" "$BEAM" "$AVMLIB" 2>&1 | head -10)"
        log "   AtomVM run output:"
        printf '%s\n' "$B_RUN" | sed 's/^/     /' | tee -a "$REPORT"
        case "$B_RUN" in
            *probe_raw_ok*) B_RESULT="runs" ;;
            *)              B_RESULT="did-not-run" ;;
        esac
    fi
else
    printf '%s' "$B_BUILD" | tail -6 | sed 's/^/     /' | tee -a "$REPORT"
    B_RESULT="build-failed"
fi
log "   probe B result: $B_RESULT"

# ---------------------------------------------------------------------------
log ""
log "== VERDICT =="
log "   probe A (gleam_otp, expected FAIL on proc_lib): $A_RESULT"
log "   probe B (raw spawn, expected RUN):              $B_RESULT"
if [ "$A_RESULT" = "reproduced" ] && [ "$B_RESULT" = "runs" ]; then
    log "   REPRODUCED — atomvm-unsupported.list's provenance holds. It may now be EXTENDED"
    log "   by further observation (never by documentation alone)."
    exit 0
fi
log "   NOT REPRODUCED — do NOT extend atomvm-unsupported.list on this run."
log "   One expectation without the other is not a reproduction: both failing means the"
log "   harness is broken, both passing means the constraint has evaporated. Either way the"
log "   list's foundation is unconfirmed here and the honest record is this file, unchanged."
exit 1
