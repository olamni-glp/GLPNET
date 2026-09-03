#!/usr/bin/env bash
# test/ring/test_report_shape.sh — guards C4 of
# specs/101-gleam-capability-delivery/contracts/ring-delivery.md (feature 101, T006-T008).
#
# T006 test_report_without_denominator_is_rejected  — SC-002
# T007 test_counts_reconcile                        — SC-007
# T008 test_excused_case_without_reason_is_rejected — FR-007
#
# These test the REPORT PARSER, not a report. The parser is the thing that must refuse;
# a report is just the input that proves it does. So each case feeds a deliberately
# malformed report in and asserts rejection, and one case feeds a well-formed one in and
# asserts acceptance — without that last one, "rejects everything" would score green.
#
# Run: bash test/ring/test_report_shape.sh

set -u
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
. "$SCRIPT_DIR/lib/harness.sh"
RING_SUITE="test_report_shape"

PARSER="$SCRIPT_DIR/parse_report.sh"
TMP="$(mktemp -d 2>/dev/null || echo "${TMPDIR:-/tmp}/ring_report_$$")"
mkdir -p "$TMP"
trap 'rm -rf "$TMP"' EXIT INT TERM

echo "== $RING_SUITE =="

_parser_missing() { [ ! -f "$PARSER" ]; }

# feed REPORT_TEXT -> echoes parser output, returns parser rc
feed() {
    printf '%s\n' "$1" > "$TMP/report.txt"
    bash "$PARSER" "$TMP/report.txt" 2>&1
}
feed_rc() {
    printf '%s\n' "$1" > "$TMP/report.txt"
    bash "$PARSER" "$TMP/report.txt" >/dev/null 2>&1
    echo $?
}

# A well-formed report, per C4: ring, denominator, the four counts reconciling, every
# excused entry carrying a reason, and not_run named rather than silently empty.
WELL_FORMED='ring: beam
denominator: 206
attempted: 206
agreed: 204
diverged: 1
excused: 1
excused[0].case: link:zmq_roundtrip
excused[0].reason: ZeroMQ transport not built on this host
not_run: atomvm-host-conformance (MAUI Blazor Hybrid host is target-side, absent here)'

# ---------------------------------------------------------------------------
# T006 (SC-002) — a report with no denominator is UNPARSEABLE, not merely ugly.
# "204 agreed" means nothing without knowing 204 out of what.
# ---------------------------------------------------------------------------
test_report_without_denominator_is_rejected() {
    local name="test_report_without_denominator_is_rejected"
    if _parser_missing; then
        pending "$name" "report parser test/ring/parse_report.sh lands at T015"
        return 0
    fi
    local bad rc
    bad="$(printf '%s\n' "$WELL_FORMED" | grep -v '^denominator:')"
    rc="$(feed_rc "$bad")"
    if [ "$rc" -eq 0 ]; then
        fail "$name" "a report with NO denominator was accepted — SC-002 does not hold"
    else
        assert_contains "$name" "$(feed "$bad")" "denominator"
    fi
}

# ---------------------------------------------------------------------------
# T007 (SC-007) — attempted = agreed + diverged + excused, EXACTLY. Not >=, not
# approximately. A mismatch means cases vanished between running and reporting.
# ---------------------------------------------------------------------------
test_counts_reconcile() {
    local name="test_counts_reconcile"
    if _parser_missing; then
        pending "$name" "report parser test/ring/parse_report.sh lands at T015"
        return 0
    fi
    # 206 != 204 + 1 + 1 + 1 — one case invented on the agreed side.
    local bad rc
    bad="$(printf '%s\n' "$WELL_FORMED" | sed 's/^agreed: 204/agreed: 205/')"
    rc="$(feed_rc "$bad")"
    if [ "$rc" -eq 0 ]; then
        fail "$name" "counts that do not reconcile (206 != 205+1+1) were accepted — SC-007 does not hold"
    else
        pass "$name"
    fi
}

# ---------------------------------------------------------------------------
# T008 (FR-007) — an excused case with no reason is indistinguishable from a case
# nobody ran. That is the whole point: "excused" without a reason is just a gap
# wearing a better word.
# ---------------------------------------------------------------------------
test_excused_case_without_reason_is_rejected() {
    local name="test_excused_case_without_reason_is_rejected"
    if _parser_missing; then
        pending "$name" "report parser test/ring/parse_report.sh lands at T015"
        return 0
    fi
    local bad rc
    bad="$(printf '%s\n' "$WELL_FORMED" | grep -v '^excused\[0\]\.reason:')"
    rc="$(feed_rc "$bad")"
    if [ "$rc" -eq 0 ]; then
        fail "$name" "an excused case with NO reason was accepted — FR-007 does not hold"
    else
        assert_contains "$name" "$(feed "$bad")" "reason"
    fi
}

# ---------------------------------------------------------------------------
# The control that keeps the three above honest. A parser that rejects EVERYTHING
# satisfies T006-T008 trivially and is useless. This asserts the well-formed report
# is accepted, so "reject all" cannot score green.
# ---------------------------------------------------------------------------
test_well_formed_report_is_accepted() {
    local name="test_well_formed_report_is_accepted"
    if _parser_missing; then
        pending "$name" "report parser test/ring/parse_report.sh lands at T015"
        return 0
    fi
    local rc; rc="$(feed_rc "$WELL_FORMED")"
    if [ "$rc" -eq 0 ]; then
        pass "$name"
    else
        fail "$name" "the well-formed C4 report was REJECTED (rc=$rc) — a parser that refuses everything makes T006-T008 vacuous: $(feed "$WELL_FORMED")"
    fi
}

# ---------------------------------------------------------------------------
# FR-006 — not_run[] is mandatory and a silent-empty result is a FAILURE. Omitting
# the field entirely must be rejected; the report has to say what it did not read.
# ---------------------------------------------------------------------------
test_missing_not_run_is_rejected() {
    local name="test_missing_not_run_is_rejected"
    if _parser_missing; then
        pending "$name" "report parser test/ring/parse_report.sh lands at T015"
        return 0
    fi
    local bad rc
    bad="$(printf '%s\n' "$WELL_FORMED" | grep -v '^not_run:')"
    rc="$(feed_rc "$bad")"
    if [ "$rc" -eq 0 ]; then
        fail "$name" "a report omitting not_run[] was accepted — FR-006 requires naming what was not read"
    else
        assert_contains "$name" "$(feed "$bad")" "not_run"
    fi
}

test_report_without_denominator_is_rejected
test_counts_reconcile
test_excused_case_without_reason_is_rejected
test_missing_not_run_is_rejected
test_well_formed_report_is_accepted

ring_summary
exit $?
