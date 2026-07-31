#!/usr/bin/env bash
# Reproduction — FULL wire-protocol SPIN model for the 061 split (T015, FR-040).
# CANONICAL run path: WSL2 (real SPIN + gcc; see tool-versions.txt). run.ps1 wraps this.
#
# Three verifier runs on wire_protocol.pml:
#   (1) LIVENESS  — LTL request_eventually_answered, fairness (./pan -a -f -N ...)
#   (2) LIVENESS  — LTL deferred_snapshot_eventually_completes, fairness
#   (3) SAFETY    — LTL lines removed so SPIN enables invalid-end-state detection
#                   (deadlock-freedom) + the xs/xr unspecified-reception checks.
# PASS = errors: 0 on ALL runs. Exit 0 = PASS, 1 = FAIL/counterexample.
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
export PATH="${SPIN_BIN_DIR:-$HOME/.local/bin}:$PATH"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
cp "$HERE/wire_protocol.pml" "$WORK/"
cd "$WORK"

spin -V

echo "=== (1) LIVENESS: request_eventually_answered (fairness) ==="
spin -a wire_protocol.pml
gcc -O2 -o pan pan.c
out1="$(./pan -a -f -N request_eventually_answered)"; echo "$out1"
echo "$out1" | grep -qE 'errors: 0' || { echo "FAIL: request_eventually_answered errors != 0" >&2; exit 1; }

echo "=== (2) LIVENESS: deferred_snapshot_eventually_completes (fairness) ==="
out2="$(./pan -a -f -N deferred_snapshot_eventually_completes)"; echo "$out2"
echo "$out2" | grep -qE 'errors: 0' || { echo "FAIL: deferred_snapshot_eventually_completes errors != 0" >&2; exit 1; }

echo "=== (3) SAFETY/DEADLOCK: invalid end states + unspecified receptions (claims removed) ==="
grep -v '^ltl ' wire_protocol.pml > wire_protocol_safety.pml
spin -a wire_protocol_safety.pml
gcc -O2 -o pan_safety pan.c
out3="$(./pan_safety)"; echo "$out3"
echo "$out3" | grep -qE 'errors: 0' || { echo "FAIL: safety errors != 0" >&2; exit 1; }

echo ""
echo "RESULT: PASS — deadlock-freedom + no unspecified receptions + request_eventually_answered + deferred_snapshot_eventually_completes all hold"
