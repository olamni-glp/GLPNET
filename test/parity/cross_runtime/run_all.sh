#!/usr/bin/env bash
# =============================================================================
# test/parity/cross_runtime/run_all.sh — the US5 cross-runtime distributed
# suite, one entry point (feature 060 US5, T043/T047; FR-030).
#
# Runs every scenario file and reports the combined result alongside the other
# suites (test/run_all_tests.sh invokes this). Exit = total failures.
# =============================================================================
set -u
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TOTAL=0

for suite in link_both_ways.sh round_trip.sh mismatch.sh; do
    bash "$DIR/$suite"
    TOTAL=$((TOTAL + $?))
done

echo "======================================"
echo "US5 cross-runtime suite total failures: $TOTAL"
if [ "$TOTAL" -eq 0 ]; then echo "ALL CROSS-RUNTIME (Gleam × C#) TESTS PASSED"; fi
echo "======================================"
exit "$TOTAL"
