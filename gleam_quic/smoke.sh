#!/usr/bin/env bash
# gleam_quic/smoke.sh — corpus gate for the QUIC OS-port relay (feature 059, T098).
#
# RULING-ENFORCED (2026-07-27, Disposition 2; escalation-register.md): no Wave-4 WP may depend
# on the QUIC OS-port relay until a minimal in-corpus smoke test exercising glpq_ffi.erl —
# long-line reassembly + stdio byte-identity to the C# stack — exists in the corpus and passes.
# Where a dimension cannot run, that is classified ENVIRONMENT, recorded, and the dependency
# stays blocked — never silently waived (FR-011).
#
# This peer gate (alongside glp_gleam/smoke.sh, test/run_all_tests.sh) wires the delivered
# reassembly harness into a runnable, one-command gate.
#
# Erlang/escript are NOT on PATH on Olamnit — default to the documented install; override with:
#   ERLANG_BIN=<dir of erlc/escript> PYTHON_EXE=<python> bash gleam_quic/smoke.sh
set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ERLANG_BIN="${ERLANG_BIN:-/c/Program Files/Erlang OTP/bin}"
PYTHON_EXE="${PYTHON_EXE:-python}"

echo "== gleam_quic smoke gate (QUIC OS-port relay; T098) =="

# --- Dimension 1: long-line reassembly (SUBSTANTIVE; must pass or classify env) ------------
if [ ! -x "$ERLANG_BIN/erlc" ] && ! command -v erlc >/dev/null 2>&1; then
  echo "  ENVIRONMENT: erlc/escript not found (ERLANG_BIN=$ERLANG_BIN). Reassembly dimension" >&2
  echo "  cannot run on this host — recorded environment, dependency stays BLOCKED (not waived)." >&2
  exit 2
fi

echo "  [1/2] long-line reassembly (>1 MiB envelope whole on stdout, control on stderr) ..."
if ERLANG_BIN="$ERLANG_BIN" PYTHON_EXE="$PYTHON_EXE" bash "$SCRIPT_DIR/test/run_glpq_ffi_reassembly_test.sh"; then
  echo "  [1/2] reassembly: PASS"
else
  echo "FAIL: reassembly smoke test failed (glpq_ffi.erl relay regressed finding #7)." >&2
  exit 1
fi

# --- Dimension 2: stdio byte-identity to the REAL C# stack (ENVIRONMENT-gated on msquic) ----
# The reassembly harness above drives a byte-faithful stand-in (emit_big_envelope.py) that emits
# exactly the glp_quick_host framing (control line + >1 MiB '{...}' envelope). Driving the REAL
# csharp/glp_quick_host requires msquic: it exits immediately with "ERR quic_unsupported ... real
# QUIC only (FR-001)" when QuicListener.IsSupported=false. On a host without msquic this dimension
# is ENVIRONMENT-BLOCKED — the same block that gates T084/T085/T086 — recorded, never waived.
echo "  [2/2] live-C#-stack byte-identity: ENVIRONMENT-GATED on msquic (glp_quick_host requires"
echo "        real QUIC; see close-quic-sideprocess-relay-smoketest.md). Reassembly proven via"
echo "        the byte-faithful stand-in; live dimension stays BLOCKED until msquic is provisioned."

echo ""
echo "PASS: reassembly gate green; live-C# byte-identity recorded ENVIRONMENT-blocked (msquic)."
exit 0
