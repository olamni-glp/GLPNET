#!/usr/bin/env bash
# test/ring/run_ring_tests.sh — the per-ring conformance suite (feature 101, T003).
#
# Runs every guard under test/ring/ and folds their results. Alongside, never replacing,
# test/parity/run_gleam_corpus.sh: parity measures Dart-vs-Gleam agreement over the 206
# pinned cases; this measures whether the RING DELIVERY contract (C1-C6) holds.
#
# EXIT CODES — the distinction matters and is the whole point:
#   0  GREEN    every guard held
#   1  RED      a guard is violated
#   2  PENDING  guards are in place, what they guard is not built yet
#
# A PENDING run is NOT a pass and does not exit 0. Before T012-T021 land, that is the
# expected and correct state: the guards are written first (C6), so they legitimately
# have nothing to hold yet. What they must never do is go quiet about it.
#
# Run: bash test/ring/run_ring_tests.sh

set -u
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

SUITES="
test_contract_purity.sh
test_report_shape.sh
test_aggregate.sh
test_mutation.sh
test_platform_conditional.sh
test_atomvm_subset.sh
test_list_single_source.sh
test_retention.sh
"

GREEN=0; RED=0; PENDING=0; OTHER=0
RED_NAMES=""; PENDING_NAMES=""

echo "=========================================="
echo " test/ring — ring delivery conformance"
echo " contracts/ring-delivery.md C1-C6"
echo "=========================================="
echo ""

for s in $SUITES; do
    [ -f "$SCRIPT_DIR/$s" ] || { echo "  (missing suite: $s)"; RED=$((RED+1)); RED_NAMES="$RED_NAMES $s"; continue; }
    bash "$SCRIPT_DIR/$s"
    rc=$?
    case "$rc" in
        0) GREEN=$((GREEN+1)) ;;
        1) RED=$((RED+1));     RED_NAMES="$RED_NAMES $s" ;;
        2) PENDING=$((PENDING+1)); PENDING_NAMES="$PENDING_NAMES $s" ;;
        *) OTHER=$((OTHER+1)); RED_NAMES="$RED_NAMES $s(rc=$rc)" ;;
    esac
    echo ""
done

echo "=========================================="
echo " ring suite summary"
echo "   green=$GREEN  red=$RED  pending=$PENDING  other=$OTHER"

if [ "$RED" -gt 0 ] || [ "$OTHER" -gt 0 ]; then
    echo "   RESULT: RED —$RED_NAMES"
    echo "=========================================="
    exit 1
fi

if [ "$PENDING" -gt 0 ]; then
    echo "   RESULT: PENDING —$PENDING_NAMES"
    echo "   The guards hold nothing yet: T012-T021 are unbuilt."
    echo "   This is NOT a pass. Exit 2."
    echo "=========================================="
    exit 2
fi

echo "   RESULT: GREEN"
echo ""
echo "   This means THE GUARDS HOLD. It does not mean the capability is delivered."
echo "   Those are different claims and this line exists so they are not conflated:"
echo "   the delivery verdict is test/ring/aggregate.sh, which REFUSES while any"
echo "   required ring is unbuilt or unread. Run it:"
echo "     bash test/ring/aggregate.sh --reports test/ring/reports --require \"beam atomvm\""
echo "=========================================="
exit 0
