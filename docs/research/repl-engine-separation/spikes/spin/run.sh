#!/usr/bin/env bash
# Reproduction — Promela/SPIN wire-protocol validation spike (US5, 027, T024).
# CANONICAL run path: WSL2 (real SPIN 6.5.1 + gcc; see tool-versions.txt). run.ps1 wraps this.
#
# Two verifier runs on the minimal front<->back handshake front_back.pml:
#   (1) LIVENESS — the LTL claim `request_eventually_answered` ([] (req_sent -> <> resp_seen)),
#       fairness enabled (./pan -a -f).
#   (2) SAFETY/DEADLOCK — the LTL line removed so SPIN ENABLES invalid-end-state detection
#       (it is "disabled by never claim" when the claim is active), plus assertion + the xs/xr
#       unspecified-reception checks.
# PASS = errors: 0 on BOTH runs. Exit 0 = PASS, 1 = FAIL/counterexample.
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
export PATH="${SPIN_BIN_DIR:-$HOME/.local/bin}:$PATH"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
cp "$HERE/front_back.pml" "$WORK/"
cd "$WORK"

spin -V

echo "=== (1) LIVENESS: LTL request_eventually_answered (fairness) ==="
spin -a front_back.pml
gcc -O2 -o pan pan.c
live_out="$(./pan -a -f)"; echo "$live_out"
echo "$live_out" | grep -qE 'errors: 0' || { echo "FAIL: liveness errors != 0" >&2; exit 1; }

echo "=== (2) SAFETY/DEADLOCK: invalid end states + unspecified receptions (claim removed) ==="
grep -v '^ltl ' front_back.pml > front_back_safety.pml
spin -a front_back_safety.pml
gcc -O2 -o pan_safety pan.c
safe_out="$(./pan_safety)"; echo "$safe_out"
echo "$safe_out" | grep -qE 'errors: 0' || { echo "FAIL: safety errors != 0" >&2; exit 1; }

echo ""
echo "RESULT: PASS — deadlock-freedom + no unspecified receptions + request_eventually_answered all hold (real SPIN 6.5.1)"
