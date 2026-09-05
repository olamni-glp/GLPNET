#!/usr/bin/env bash
# test/ring/run_beam_ring_no_dart.sh — T016 · FR-010 / SC-001.
#
# Run the pinned corpus on the BEAM ring with NO DART TOOLCHAIN ON PATH, and record the
# result as a C4 conformance report.
#
# The refuter this exists to catch: **any case that only passes because Dart is present.**
# glpnet's delivery mode is resynthesis, never copy — the Gleam ring has to stand up on
# its own, on a machine where the Dart reference implementation does not exist. A parity
# number measured with Dart sitting on PATH cannot distinguish "the Gleam port is correct"
# from "something quietly shelled out to the reference".
#
# The harness compares against goldens RECORDED EARLIER (test/parity/goldens/), which is
# what makes this possible: the reference's answers are already on disk, so the comparison
# needs the recording, not the toolchain. That distinction is the point, and it is checked
# here rather than assumed — this script asserts `dart` is genuinely unreachable before it
# runs anything, so a PATH that failed to strip cannot produce a false pass.
#
# Usage: bash test/ring/run_beam_ring_no_dart.sh [--out <report-dir>]
# Exit:  0 corpus green without Dart · 1 divergences or Dart still reachable · 2 setup.

set -u
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && cd .. && pwd)"

OUT="$SCRIPT_DIR/reports"
while [ $# -gt 0 ]; do
    case "$1" in
        --out) OUT="${2:-}"; shift 2 ;;
        *) echo "run_beam_ring_no_dart: unknown argument '$1'" >&2; exit 2 ;;
    esac
done
mkdir -p "$OUT"

CORPUS="$REPO_ROOT/test/parity/run_gleam_corpus.sh"
[ -f "$CORPUS" ] || { echo "missing $CORPUS" >&2; exit 2; }

# --- strip Dart from PATH ---------------------------------------------------
# Drop every PATH entry that provides a dart executable, rather than pattern-matching
# directory names: a machine can have Dart somewhere this script cannot guess.
STRIPPED=""
OLD_IFS="$IFS"; IFS=":"
for d in $PATH; do
    [ -z "$d" ] && continue
    if [ -x "$d/dart" ] || [ -x "$d/dart.exe" ] || [ -x "$d/dart.bat" ]; then
        continue
    fi
    STRIPPED="${STRIPPED:+$STRIPPED:}$d"
done
IFS="$OLD_IFS"

export PATH="$STRIPPED"
unset DART 2>/dev/null || true

echo "== T016 · BEAM ring corpus, no Dart toolchain =="

# --- prove the premise before trusting the result ---------------------------
if command -v dart >/dev/null 2>&1; then
    echo "REFUSED: dart is STILL reachable at $(command -v dart) after stripping PATH."
    echo "  A run under these conditions proves nothing about independence from the"
    echo "  reference toolchain, so it is not reported as evidence (FR-009: a test whose"
    echo "  premise does not hold is not silently passed)."
    exit 1
fi
echo "  premise: dart is not on PATH — verified"

command -v gleam >/dev/null 2>&1 || { echo "REFUSED: gleam is not on PATH either; nothing to run" >&2; exit 2; }
echo "  gleam:   $(command -v gleam)"
echo ""

LOG="$OUT/beam-no-dart.log"
bash "$CORPUS" > "$LOG" 2>&1
CORPUS_RC=$?

AGREE="$(sed -n 's/.*agree=\([0-9]*\).*/\1/p' "$LOG" | tail -1)"
DIVERGE="$(sed -n 's/.*diverge=\([0-9]*\).*/\1/p' "$LOG" | tail -1)"
BLOCKED="$(sed -n 's/.*blocked=\([0-9]*\).*/\1/p' "$LOG" | tail -1)"
TOTAL="$(sed -n 's/^total:[[:space:]]*\([0-9]*\).*/\1/p' "$LOG" | tail -1)"

# Codex review 20260904T055230Z (P2): the corpus denominator also carries gap/fork and
# missing-golden outcomes, but excused was derived from BLOCKED alone. The moment an expected
# gap/fork existed, this report could claim `not_run: none` while silently omitting those cases
# and their reasons - and with the old parser that still produced a GREEN report.
GAPFORK="$(grep -oE 'gap/fork=[0-9]+' "$LOG" | tail -1 | cut -d= -f2)"
OOS="$(grep -oE '^out_of_scope:[[:space:]]*[0-9]+' "$LOG" | tail -1 | grep -oE '[0-9]+$')"

AGREE="${AGREE:-0}"; DIVERGE="${DIVERGE:-0}"; BLOCKED="${BLOCKED:-0}"; TOTAL="${TOTAL:-0}"
GAPFORK="${GAPFORK:-0}"; OOS="${OOS:-0}"
EXCUSED=$((BLOCKED + GAPFORK + OOS))
ATTEMPTED=$((AGREE + DIVERGE + EXCUSED))

echo "  corpus rc=$CORPUS_RC  total=$TOTAL agree=$AGREE diverge=$DIVERGE blocked=$BLOCKED gap/fork=$GAPFORK out_of_scope=$OOS"
echo "  (full log: $LOG)"
echo ""

REPORT="$OUT/beam.report"
{
    echo "ring: beam"
    echo "denominator: ${TOTAL:-0}"
    echo "attempted: $ATTEMPTED"
    echo "agreed: $AGREE"
    echo "diverged: $DIVERGE"
    echo "excused: $EXCUSED"
    if [ "$EXCUSED" -gt 0 ]; then
        i=0
        while [ "$i" -lt "$EXCUSED" ]; do
            echo "excused[$i].case: see $LOG"
            echo "excused[$i].reason: classified in test/parity/expected.list as blocked, expected-divergence (gap/fork) or out-of-scope; see the log for the per-case reason"
            i=$((i + 1))
        done
    fi
    echo "not_run: none (whole pinned corpus attempted with dart absent from PATH)"
} > "$REPORT"

echo "  wrote $REPORT"
bash "$SCRIPT_DIR/parse_report.sh" "$REPORT" || {
    echo "REFUSED: the report this run produced does not satisfy C4." >&2; exit 1; }

if [ "$CORPUS_RC" -ne 0 ] || [ "$DIVERGE" -ne 0 ]; then
    echo ""
    echo "REFUSED: the corpus did not agree with Dart absent (rc=$CORPUS_RC, diverge=$DIVERGE)."
    echo "  Any case that passes only with Dart present is exactly the refuter for FR-010."
    exit 1
fi

echo ""
echo "GREEN — $AGREE/$TOTAL agreed with no Dart toolchain on PATH (FR-010, SC-001)."
exit 0
