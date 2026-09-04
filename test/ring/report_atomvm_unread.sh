#!/usr/bin/env bash
# test/ring/report_atomvm_unread.sh — T019 · FR-006 / R4.
#
# Emit the AtomVM ring's conformance as **UNREAD with a named reason**. Never as a pass,
# never as a zero that reads like one.
#
# Two separate absences, and they must not be blurred together:
#
#   1. The MAUI Blazor Hybrid host is TARGET-SIDE and is not in this repo. Measured
#      2026-09-03: `maui` occurs 0 times in glpnet product code. There is nothing here to
#      run host-side conformance against.
#   2. The AtomVM TOOLCHAIN is not installed on this machine — no `atomvm`, no `packbeam`,
#      no install under the user profile (measured 2026-09-03). So even the device-free
#      part of the ring cannot be exercised here.
#
# **Do NOT synthesize a stand-in host to make a suite green.** A local Erlang process
# pretending to be the app host would turn this UNREAD into a Measured, and the number it
# produced would be evidence about the stand-in, not about AtomVM. That substitution is
# the precise dishonesty R4 forbids, and it would be invisible in a report that only
# carried counts.
#
# The aggregate refuses on this report (C4-R): 0 attempted is not agreement, and a
# non-"none" not_run is not a clean sweep. That refusal is the correct end state until
# T017/T018 land — it is what an honest incomplete delivery looks like.
#
# Usage: bash test/ring/report_atomvm_unread.sh [--out <report-dir>]
# Exit:  0 report written and C4-valid · 1 the report it wrote is malformed · 2 setup.

set -u
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

OUT="$SCRIPT_DIR/reports"
while [ $# -gt 0 ]; do
    case "$1" in
        --out) OUT="${2:-}"; shift 2 ;;
        *) echo "report_atomvm_unread: unknown argument '$1'" >&2; exit 2 ;;
    esac
done
mkdir -p "$OUT"

echo "== T019 · AtomVM ring conformance =="

# Re-measure both premises rather than trusting the comment above. If either becomes
# false, this script must stop claiming UNREAD and someone must revisit it.
MAUI_HITS="$(grep -ril 'maui' "$REPO_ROOT" \
    --include='*.gleam' --include='*.dart' --include='*.cs' --include='*.py' 2>/dev/null \
    | grep -v '\.specify\|\.claude' | grep -v 'glp/contract/\|glp/ring/' | wc -l | tr -d ' ')"

TOOLCHAIN="absent"
command -v atomvm  >/dev/null 2>&1 && TOOLCHAIN="present"
command -v packbeam >/dev/null 2>&1 && TOOLCHAIN="present"

echo "  MAUI host files in product code: $MAUI_HITS"
echo "  AtomVM toolchain on this host:   $TOOLCHAIN"

if [ "$TOOLCHAIN" = "present" ] || [ "${MAUI_HITS:-0}" -gt 0 ]; then
    echo ""
    echo "  PREMISE CHANGED — this script asserts the host and toolchain are absent, and at"
    echo "  least one of them is now present. Reporting UNREAD would understate what can be"
    echo "  measured. Revisit T017/T018/T019 rather than running this."
    exit 2
fi

REASON="AtomVM toolchain absent on this host (no atomvm/packbeam) and the MAUI Blazor Hybrid host is target-side, absent from this repo; the C3 construct list is a LOWER BOUND measured on AtomVM 0.6.6 only (Q-GLPNETS17-01), so no corpus run has been attempted on this ring"

REPORT="$OUT/atomvm.report"
{
    echo "ring: atomvm"
    echo "denominator: 206"
    echo "attempted: 0"
    echo "agreed: 0"
    echo "diverged: 0"
    echo "excused: 0"
    echo "not_run: atomvm-conformance ($REASON)"
} > "$REPORT"

echo ""
echo "  wrote $REPORT"
bash "$SCRIPT_DIR/parse_report.sh" "$REPORT" || {
    echo "REFUSED: the UNREAD report is itself malformed." >&2; exit 1; }

echo ""
echo "UNREAD recorded. This ring contributes NO pass to the aggregate, by construction."
exit 0
