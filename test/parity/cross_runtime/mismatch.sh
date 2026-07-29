#!/usr/bin/env bash
# =============================================================================
# test/parity/cross_runtime/mismatch.sh — capability mismatch is an EXPLICIT
# refusal, never silent misinterpretation (feature 060 US5, T045; FR-029).
#
# Scenario: a program asks for a link over a scheme this instance holds no
# transport capability for (scheme "bogus" — registered on NEITHER runtime).
# The conforming behaviour on BOTH runtimes (FR-028) is a reasoned, visible
# refusal of the establishment — an [ABORT]/unsupported-scheme report and a
# failed/suspended goal — with NOTHING placed on any wire. A silent success,
# or a garbled stream, is the failure mode this test exists to catch.
#
# (Wire-format version skew is locked out one layer down: both runtimes stamp
# FrameCodec version 0x01 and REJECT a frame with any other version byte at
# parse time — exercised by the frame-codec unit suites on each side.)
# =============================================================================
set -u
. "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
cr_require_csharp

echo "======================================"
echo "US5 mismatch: unsupported capability refused explicitly on both runtimes"
echo "======================================"

GOAL='server_listener(link_id("bogus", ep("127.0.0.1", 9260), 1), Link, Faults).'

out="$CR_RESULTS/mismatch.gleam.out"
gleam_repl "$out" "$CR_LINKDIR/pc.glp" "$GOAL"
if grep -qE "no transport registered for scheme|ABORT" "$out" \
   && ! grep -q "succeeds" "$out"; then
    echo "  PASS: mismatch [Gleam]  (explicit refusal, no silent success)"
    CR_PASS=$((CR_PASS + 1))
else
    echo "  FAIL: mismatch [Gleam]  (expected an explicit unsupported-scheme refusal)"
    tail -5 "$out" | sed 's/^/      /'
    CR_FAIL=$((CR_FAIL + 1))
fi

out="$CR_RESULTS/mismatch.cs.out"
cs_repl "$out" "$CR_LINKDIR/pc.glp" "$GOAL"
if grep -qiE "no transport|unsupported|unknown scheme|ABORT" "$out" \
   && ! grep -q "succeeds" "$out"; then
    echo "  PASS: mismatch [C#]  (explicit refusal, no silent success)"
    CR_PASS=$((CR_PASS + 1))
else
    echo "  FAIL: mismatch [C#]  (expected an explicit unsupported-scheme refusal)"
    tail -5 "$out" | sed 's/^/      /'
    CR_FAIL=$((CR_FAIL + 1))
fi

cr_summary "US5 mismatch"
