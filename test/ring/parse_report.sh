#!/usr/bin/env bash
# test/ring/parse_report.sh — the C4 conformance-report parser (feature 101, T015).
#
# Validates one ring's report against contracts/ring-delivery.md C4. This is the
# MECHANISM the T006-T008 guards exercise; it is what actually refuses a malformed
# report, and it must refuse for a NAMED reason so the operator can fix it.
#
# Required fields (C4):
#   ring          mandatory — results are per-ring (FR-008)
#   denominator   mandatory — a report without one is unparseable (SC-002). "204 agreed"
#                 is not a result; 204 out of what is.
#   attempted / agreed / diverged / excused
#                 attempted = agreed + diverged + excused EXACTLY (SC-007). Not >=, not
#                 approximately: a mismatch means cases vanished between running and
#                 reporting, and which ones is then unknowable.
#   excused[N].reason
#                 mandatory for every excused case (FR-007). An excused case with no
#                 reason is indistinguishable from a case nobody ran — it is a gap
#                 wearing a better word.
#   not_run       mandatory (FR-006) — names what was not run. A silently-empty result
#                 is a FAILURE, not a clean sweep. Use "none" to assert nothing was
#                 skipped; omitting the field is not the same claim.
#
# Usage: parse_report.sh <report-file>
# Exit:  0 well-formed · 1 malformed (reasons on stdout) · 2 unusable input.

set -u
REPORT="${1:-}"
[ -n "$REPORT" ] || { echo "parse_report: usage: parse_report.sh <report-file>" >&2; exit 2; }
[ -f "$REPORT" ] || { echo "parse_report: no such report file: $REPORT" >&2; exit 2; }

field() {
    # Last occurrence wins; CR-tolerant; value trimmed.
    sed -n "s/^$1:[[:space:]]*//p" "$REPORT" | tr -d '\r' | tail -1
}

ERRORS=""
err() { ERRORS="$ERRORS
  - $1"; }

RING="$(field ring)"
DENOM="$(field denominator)"
ATTEMPTED="$(field attempted)"
AGREED="$(field agreed)"
DIVERGED="$(field diverged)"
EXCUSED="$(field excused)"
NOT_RUN="$(field not_run)"

[ -n "$RING" ]   || err "missing mandatory field 'ring' — results are per-ring (FR-008); an unlabelled report cannot be attributed"
[ -n "$DENOM" ]  || err "missing mandatory field 'denominator' — a report without a denominator is unparseable (SC-002)"
[ -n "$NOT_RUN" ] || err "missing mandatory field 'not_run' — a report must name what it did not run (FR-006); a silent-empty result is a failure, not a clean sweep"

is_num() { case "$1" in ''|*[!0-9]*) return 1 ;; *) return 0 ;; esac }

for pair in "attempted:$ATTEMPTED" "agreed:$AGREED" "diverged:$DIVERGED" "excused:$EXCUSED"; do
    k="${pair%%:*}"; v="${pair#*:}"
    if [ -z "$v" ]; then
        err "missing mandatory count '$k'"
    elif ! is_num "$v"; then
        err "count '$k' is not a non-negative integer: '$v'"
    fi
done
if [ -n "$DENOM" ] && ! is_num "$DENOM"; then
    err "'denominator' is not a non-negative integer: '$DENOM'"
fi

# SC-007 — the counts must reconcile exactly.
if is_num "${ATTEMPTED:-x}" && is_num "${AGREED:-x}" && is_num "${DIVERGED:-x}" && is_num "${EXCUSED:-x}"; then
    SUM=$((AGREED + DIVERGED + EXCUSED))
    if [ "$ATTEMPTED" -ne "$SUM" ]; then
        err "counts do not reconcile: attempted=$ATTEMPTED but agreed+diverged+excused=$AGREED+$DIVERGED+$EXCUSED=$SUM (SC-007 requires exact equality; the difference is $((ATTEMPTED - SUM)) case(s) that vanished between running and reporting)"
    fi
    if is_num "${DENOM:-x}" && [ "$ATTEMPTED" -gt "$DENOM" ]; then
        err "attempted=$ATTEMPTED exceeds denominator=$DENOM — more cases were run than exist"
    fi
    # Codex review 20260904T055230Z (P1): checking only attempted > denominator left the
    # under-run direction wide open. `denominator: 206 / attempted: 1 / agreed: 1 /
    # not_run: none` passed, and the aggregate then marked that ring GREEN while silently
    # dropping 205 declared cases. Every denominator case must be exercised or explicitly
    # accounted for (FR-006, SC-007) — so the shortfall must be named in not_run[], never
    # left to be inferred from an arithmetic gap nobody computes.
    if is_num "${DENOM:-x}" && [ "$ATTEMPTED" -lt "$DENOM" ]; then
        SHORT=$((DENOM - ATTEMPTED))
        case "${NOT_RUN%% *}" in
            ''|none)
                err "attempted=$ATTEMPTED is $SHORT short of denominator=$DENOM, but not_run says '${NOT_RUN:-<empty>}' — $SHORT declared case(s) are unaccounted for. A report may run fewer cases than it declares ONLY if it names what it did not run (FR-006/SC-007)" ;;
        esac
    fi
fi

# FR-007 — every excused case carries a reason.
if is_num "${EXCUSED:-x}" && [ "${EXCUSED:-0}" -gt 0 ]; then
    i=0
    while [ "$i" -lt "$EXCUSED" ]; do
        r="$(sed -n "s/^excused\[$i\]\.reason:[[:space:]]*//p" "$REPORT" | tr -d '\r' | tail -1)"
        c="$(sed -n "s/^excused\[$i\]\.case:[[:space:]]*//p" "$REPORT" | tr -d '\r' | tail -1)"
        [ -n "$c" ] || err "excused[$i] has no 'case' — an unnamed exclusion cannot be reviewed"
        [ -n "$r" ] || err "excused[$i] has no 'reason' — a reasonless exclusion is indistinguishable from a case nobody ran (FR-007)"
        i=$((i + 1))
    done
fi

if [ -n "$ERRORS" ]; then
    echo "MALFORMED REPORT: $REPORT"
    printf '%s\n' "$ERRORS"
    exit 1
fi

echo "OK ring=$RING denominator=$DENOM attempted=$ATTEMPTED agreed=$AGREED diverged=$DIVERGED excused=$EXCUSED not_run=$NOT_RUN"
exit 0
