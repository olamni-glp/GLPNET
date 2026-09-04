#!/usr/bin/env bash
# test/ring/aggregate.sh — the C4-R cross-ring aggregate (feature 101, T015/T021).
#
# Folds every ring's conformance report into one verdict, and REFUSES when any required
# ring is missing or red.
#
# C4-R is the rule this implements, and it exists because of a specific, likely failure:
# the BEAM ring builds and passes, the AtomVM ring is never built, and the aggregate
# reports green because it only ever saw one ring. That green is indistinguishable from a
# real one. So a missing ring is a REFUSAL — not a warning, not "1 of 2 rings passed".
# An unbuilt ring never reads as a pass.
#
# Usage:
#   aggregate.sh --reports <dir> [--require "beam atomvm"]
#
# Reads <dir>/<ring>.report, validating each through parse_report.sh (C4).
#
# Exit: 0 all required rings present and green · 1 refused · 2 unusable input.

set -u
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PARSER="$SCRIPT_DIR/parse_report.sh"

REPORTS=""; REQUIRE="beam atomvm"
while [ $# -gt 0 ]; do
    case "$1" in
        --reports) REPORTS="${2:-}"; shift 2 ;;
        --require) REQUIRE="${2:-}"; shift 2 ;;
        *) echo "aggregate: unknown argument '$1'" >&2; exit 2 ;;
    esac
done

# Codex review 20260904T055230Z (P2): `--require` was a free-form override, so `--require beam`
# produced a GREEN aggregate with AtomVM absent — a public flag that bypasses C4-R. The mandatory
# ring set is a property of the feature (both sibling rings, 008 FR-017), not a caller's choice.
# Narrowing it is refused; the flag survives only so a caller can name a SUPERSET for a future
# third ring.
MANDATORY_RINGS="beam atomvm"
for m in $MANDATORY_RINGS; do
    case " $REQUIRE " in
        *" $m "*) ;;
        *) echo "aggregate: REFUSED — '--require $REQUIRE' omits the mandatory ring '$m'." >&2
           echo "  Both sibling rings are always required; an aggregate that can be narrowed to the" >&2
           echo "  rings that happen to be built is exactly the masking C4-R/SC-006 forbids." >&2
           exit 2 ;;
    esac
done

[ -n "$REPORTS" ] || { echo "aggregate: --reports <dir> is required" >&2; exit 2; }
[ -d "$REPORTS" ] || { echo "aggregate: no such reports directory: $REPORTS" >&2; exit 2; }
[ -f "$PARSER" ]  || { echo "aggregate: missing $PARSER" >&2; exit 2; }

echo "cross-ring aggregate"
echo "  reports dir:    $REPORTS"
echo "  required rings: $REQUIRE"
echo ""

MISSING=""; MALFORMED=""; RED=""; OK=""; UNREAD=""
TOT_ATT=0; TOT_AGR=0; TOT_DIV=0; TOT_EXC=0

for ring in $REQUIRE; do
    f="$REPORTS/$ring.report"
    if [ ! -f "$f" ]; then
        MISSING="$MISSING $ring"
        echo "  $ring: NOT BUILT (no $ring.report)"
        continue
    fi

    out="$( bash "$PARSER" "$f" 2>&1 )"; rc=$?
    if [ "$rc" -ne 0 ]; then
        MALFORMED="$MALFORMED $ring"
        echo "  $ring: MALFORMED REPORT"
        printf '%s\n' "$out" | sed 's/^/      /'
        continue
    fi

    get() { sed -n "s/^$1:[[:space:]]*//p" "$f" | tr -d '\r' | tail -1; }

    # Codex review 20260904T055230Z (P1): the aggregate trusted the FILENAME and never checked
    # the report's own mandatory `ring:` field. Copying a green beam.report to atomvm.report
    # therefore produced a GREEN aggregate with no AtomVM result in existence — the exact lie
    # C4-R/SC-006 exists to prevent, achieved by `cp`. The identity must come from the content.
    declared="$(get ring)"
    if [ "$declared" != "$ring" ]; then
        MALFORMED="$MALFORMED $ring"
        echo "  $ring: RING IDENTITY MISMATCH — $ring.report declares 'ring: ${declared:-<missing>}'"
        echo "      A report is identified by its content, not its filename. Refusing rather than"
        echo "      counting one ring's result as another's."
        continue
    fi

    att="$(get attempted)"; agr="$(get agreed)"; div="$(get diverged)"; exc="$(get excused)"
    TOT_ATT=$((TOT_ATT + att)); TOT_AGR=$((TOT_AGR + agr))
    TOT_DIV=$((TOT_DIV + div)); TOT_EXC=$((TOT_EXC + exc))

    nr="$(get not_run)"
    # Normalise: the field may carry an explanatory parenthetical after the token, e.g.
    # "none (whole pinned corpus attempted)". Only the leading token is the claim.
    nr_token="${nr%% *}"

    # An UNREAD ring is NOT a green one. A report with nothing attempted, or one that
    # names something it did not run, is honest — and honestly says the evidence is
    # absent. Counting it as green because `diverged` happens to be 0 would launder a
    # vacuous report into a pass, which is the same failure C4-R forbids for a MISSING
    # ring, just with a file present. Zero divergences out of zero cases is not agreement.
    if [ "$att" -eq 0 ]; then
        UNREAD="$UNREAD $ring"
        echo "  $ring: UNREAD — 0 attempted; not_run: $nr"
    elif [ "$nr_token" != "none" ] && [ -n "$nr_token" ]; then
        UNREAD="$UNREAD $ring"
        echo "  $ring: PARTIAL — $agr/$att agreed but not_run: $nr"
    elif [ "$div" -gt 0 ]; then
        RED="$RED $ring"
        echo "  $ring: RED — $div divergence(s) of $att attempted"
    else
        OK="$OK $ring"
        echo "  $ring: green — $agr/$att agreed, $exc excused"
    fi
done

echo ""
echo "  totals: attempted=$TOT_ATT agreed=$TOT_AGR diverged=$TOT_DIV excused=$TOT_EXC"
echo ""

if [ -n "$MISSING" ] || [ -n "$MALFORMED" ] || [ -n "$UNREAD" ]; then
    echo "REFUSED — the aggregate cannot be reported."
    [ -n "$MISSING" ]   && echo "  unbuilt ring(s):$MISSING"
    [ -n "$MALFORMED" ] && echo "  malformed report(s):$MALFORMED"
    [ -n "$UNREAD" ]    && echo "  unread / partial ring(s):$UNREAD"
    echo ""
    echo "  An unbuilt ring never reads as a pass (C4-R / SC-006). Reporting the rings that"
    echo "  DID build as the whole result is the failure mode this refusal exists to prevent:"
    echo "  that green would be indistinguishable from a complete one."
    exit 1
fi

if [ -n "$RED" ]; then
    echo "REFUSED — ring(s) with divergences:$RED"
    exit 1
fi

echo "GREEN — all required rings present and in agreement:$OK"
exit 0
