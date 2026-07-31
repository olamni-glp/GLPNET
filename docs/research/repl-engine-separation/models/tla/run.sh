#!/usr/bin/env bash
# Reproduction — crash/restore/resume TLA+ model for the 061 split (T035, FR-040).
# CANONICAL run path: WSL2 (OpenJDK + tla2tools.jar; see tool-versions.txt). run.ps1 wraps this.
#
# Three TLC runs on CrashRestore.tla:
#   (1) pass.cfg — the implemented semantics (synchronous ship-on-bind, restore
#       re-arms past the committed chain): NoDup + Ordered + NoCommittedLoss +
#       EventuallyAllObserved over ALL crash points. Expect 0 errors.
#   (2) dup.cfg  — NEGATIVE CONTROL: restore re-ships the committed chain
#       (egress armed at zero). Expect TLC to FIND the NoDup violation.
#   (3) loss.cfg — NEGATIVE CONTROL: bind/ship as separate steps (async egress).
#       Expect TLC to FIND the NoCommittedLoss (temporal) violation.
# PASS = run 1 clean AND runs 2-3 each produce their counterexample.
# Exit 0 = PASS, 1 = FAIL.
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
JAR="${TLA_TOOLS_JAR:-$HOME/.local/lib/tla2tools.jar}"
[ -f "$JAR" ] || { echo "tla2tools.jar not found at $JAR (set TLA_TOOLS_JAR)" >&2; exit 1; }
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
cp "$HERE/CrashRestore.tla" "$HERE"/pass.cfg "$HERE"/dup.cfg "$HERE"/loss.cfg "$WORK/"
cd "$WORK"

java -cp "$JAR" tlc2.TLC -h 2>/dev/null | head -1 || true
java -version 2>&1 | head -1

echo "=== (1) PASS RUN: implemented semantics (pass.cfg) ==="
out1="$(java -XX:+UseParallelGC -cp "$JAR" tlc2.TLC -workers 1 -config pass.cfg CrashRestore.tla)" \
    || { echo "$out1"; echo "FAIL: pass.cfg reported a violation" >&2; exit 1; }
echo "$out1" | tail -12
echo "$out1" | grep -q "Model checking completed. No error has been found." \
    || { echo "FAIL: pass.cfg did not complete cleanly" >&2; exit 1; }

echo "=== (2) NEGATIVE CONTROL: rearm-at-zero re-ships committed work (dup.cfg) ==="
set +e
out2="$(java -XX:+UseParallelGC -cp "$JAR" tlc2.TLC -workers 1 -config dup.cfg CrashRestore.tla)"
rc2=$?
set -e
echo "$out2" | grep -E "Invariant NoDup is violated|Error:" | head -3
[ $rc2 -ne 0 ] && echo "$out2" | grep -q "Invariant NoDup is violated" \
    || { echo "FAIL: dup.cfg did NOT produce the expected NoDup counterexample" >&2; exit 1; }

echo "=== (3) NEGATIVE CONTROL: async ship loses committed work (loss.cfg) ==="
set +e
out3="$(java -XX:+UseParallelGC -cp "$JAR" tlc2.TLC -workers 1 -config loss.cfg CrashRestore.tla)"
rc3=$?
set -e
echo "$out3" | grep -E "Temporal properties were violated|Error:" | head -3
[ $rc3 -ne 0 ] && echo "$out3" | grep -q "Temporal properties were violated" \
    || { echo "FAIL: loss.cfg did NOT produce the expected NoCommittedLoss counterexample" >&2; exit 1; }

echo ""
echo "RESULT: PASS — at-most-once committed-stream consistency holds over all crash points;"
echo "both negative controls produce their counterexamples (the properties are not vacuous)."
