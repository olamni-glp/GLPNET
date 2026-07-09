#!/usr/bin/env bash
# T028 / FR-015 #7 regression driver (feature 049): >1 MiB envelope must arrive WHOLE on the relay's
# stdout (data plane) with control lines on stderr — the pre-fix relay misrouted the >buffer line
# fragment-by-fragment to stderr (silent data loss).
#
# Erlang/escript are NOT on PATH on Olamnit — default to the documented install; override with
#   ERLANG_BIN=<dir of erlc/escript>  PYTHON_EXE=<python>  bash gleam_quic/test/run_glpq_ffi_reassembly_test.sh
# (on gavri the toolchain is on PATH: ERLANG_BIN can point at $(dirname $(command -v erlc)))
set -e
HERE="$(cd "$(dirname "$0")" && pwd)"
ERLANG_BIN="${ERLANG_BIN:-/c/Program Files/Erlang OTP/bin}"
PYTHON_EXE="${PYTHON_EXE:-python}"
BUILD="$HERE/_build_test"
mkdir -p "$BUILD"

"$ERLANG_BIN/erlc" -o "$BUILD" "$HERE/../src/glpq_ffi.erl"

OUT="$BUILD/out.txt"; ERR="$BUILD/err.txt"
# `sleep 12 |` holds the relay's stdin open so an immediate stdin EOF cannot close the port before
# the child's data arrives; the relay itself returns on the child's exit_status (~1 s).
sleep 12 | "$ERLANG_BIN/escript" "$HERE/glpq_ffi_reassembly_test.escript" "$BUILD" "$PYTHON_EXE" "$HERE/emit_big_envelope.py" > "$OUT" 2> "$ERR"

"$PYTHON_EXE" -c "
import sys
out = open(sys.argv[1], 'rb').read().rstrip(b'\r\n')
err = open(sys.argv[2], 'rb').read()
assert out.startswith(b'{') and out.endswith(b'}'), 'data envelope not intact on stdout'
assert len(out) == 2097154, 'expected 2097154 envelope bytes, got %d' % len(out)
assert b'{' not in err, 'data fragment misrouted to stderr (finding #7 regressed)'
assert b'READY test' in err, 'control line missing from the stderr route'
print('PASS: >1 MiB envelope reassembled whole on stdout; control line on stderr')
" "$OUT" "$ERR"
