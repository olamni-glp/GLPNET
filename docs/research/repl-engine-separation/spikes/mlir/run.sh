#!/usr/bin/env bash
# Reproduction — MLIR/GLP-dialect round-trip validation spike (US4, 027, T020).
# CANONICAL run path: WSL2 (the real compiled-LLVM mlir.ir wheel is Linux-only;
# there is no cp314 Windows wheel — see tool-versions.txt). run.ps1 wraps this.
#
# Runs harness.py against the REAL mlir.ir bindings and asserts the deterministic
# round-trip oracle decode(encode(p)) == p on ILFRAG-1. Exit 0 = PASS, 1 = FAIL.
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
VENV_PY="${MLIR_VENV_PY:-$HOME/mlir-spike/wheel-venv/bin/python}"

if [[ ! -x "$VENV_PY" ]]; then
  echo "run.sh: MLIR wheel venv python not found at $VENV_PY" >&2
  echo "        (provisioned at T017; see spikes/mlir/tool-versions.txt)" >&2
  exit 1
fi

"$VENV_PY" -c 'import mlir.ir as ir; print("run.sh: mlir.ir bindings:", ir.__file__)'
cd "$HERE"
exec "$VENV_PY" harness.py
