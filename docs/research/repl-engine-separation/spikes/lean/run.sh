#!/usr/bin/env bash
# Reproduction — Lean 4 bounded tactic-loop validation spike (US3, 027, T015).
# CANONICAL run path: WSL2 (real Lean 4.30.0 via elan; see tool-versions.txt). run.ps1 wraps this.
#
# Reproduces the PROVED outcome: splices the loop-discovered tactic block (proof.lean) in place of
# the `sorry` in SRSWPreservation.lean and checks the REAL Lean kernel accepts it (no sorry, no
# error). The bounded-loop SEARCH that found proof.lean (5 attempts / budget 20) is recorded in
# RESULT.md; this script reproduces its verified RESULT deterministically.
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
export PATH="${ELAN_BIN_DIR:-$HOME/.elan/bin}:$PATH"
command -v lean >/dev/null || { echo "run.sh: lean not found (provisioned at T012; see tool-versions.txt)" >&2; exit 1; }
lean --version
cd "$HERE"

# Drive one budgeted loop attempt with the discovered proof → expect "proved" (demonstrates the
# harness loop + budget accounting); then the un-budgeted verify as the deterministic gate.
python3 harness.py reset --budget 20
python3 harness.py attempt --proof proof.lean
python3 harness.py verify  --proof proof.lean
