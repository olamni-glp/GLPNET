#!/usr/bin/env bash
# test/ring/coverage_matrix.sh — the source × ring coverage matrix (feature 101, T021 ·
# FR-006 / FR-008).
#
# Names BOTH AXES and what was not read. A coverage claim with only one axis is not a
# coverage claim: "206/206 passed" says nothing about which rings ran it, and "both rings
# built" says nothing about what they were run against. The failure this prevents is a
# number that is true along one axis and silently absent along the other.
#
# The third column is the one that does the work. Every cell is one of:
#
#   measured   the suite ran on that ring against that source, and these are its numbers
#   UNREAD     it did not run, and the cell says why, named
#
# There is no blank and no implied cell. A gap in a matrix is read as "fine" by whoever
# skims it; a cell reading UNREAD with a reason is not.
#
# Usage: bash test/ring/coverage_matrix.sh [--reports <dir>]
# Exit:  0 matrix emitted (this is a REPORT, not a gate — the gate is aggregate.sh).

set -u
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
REPORTS="$SCRIPT_DIR/reports"

while [ $# -gt 0 ]; do
    case "$1" in
        --reports) REPORTS="${2:-}"; shift 2 ;;
        *) echo "coverage_matrix: unknown argument '$1'" >&2; exit 2 ;;
    esac
done

RINGS="beam atomvm"

# The SOURCE axis: what the rings are measured against.
#   pinned-corpus  test/parity/corpus.list — 206 cases, the cross-runtime oracle
#   host-side      conformance inside the MAUI Blazor Hybrid host (target-side)
SOURCES="pinned-corpus host-side"

get() { sed -n "s/^$1:[[:space:]]*//p" "$2" | tr -d '\r' | tail -1; }

echo "== source × ring coverage matrix =="
echo "   generated: $(date -u +%Y-%m-%dT%H:%M:%SZ)"
echo "   reports:   $REPORTS"
echo ""
printf '   %-16s | %-10s | %s\n' "source" "ring" "coverage"
printf '   %-16s-+-%-10s-+-%s\n' "----------------" "----------" "--------------------------------"

NOT_READ=""

for src in $SOURCES; do
    for ring in $RINGS; do
        f="$REPORTS/$ring.report"
        cell="UNREAD"
        why=""

        if [ ! -f "$f" ]; then
            why="ring not built (no $ring.report)"
        else
            att="$(get attempted "$f")"; agr="$(get agreed "$f")"
            div="$(get diverged "$f")"; exc="$(get excused "$f")"
            den="$(get denominator "$f")"
            nr="$(get not_run "$f")"

            case "$src" in
              pinned-corpus)
                if [ "${att:-0}" -gt 0 ]; then
                    cell="measured"
                    why="$agr/$den agreed, $div diverged, $exc excused"
                else
                    why="${nr:-no cases attempted}"
                fi
                ;;
              host-side)
                # Host-side conformance is target-side for BOTH rings: the MAUI Blazor
                # Hybrid host is not in this repo (`maui` = 0 in product code). Never
                # synthesize a stand-in to fill this row.
                why="MAUI Blazor Hybrid host is target-side and absent from glpnet; no stand-in host is substituted (R4)"
                ;;
            esac
        fi

        if [ "$cell" = "UNREAD" ]; then
            NOT_READ="$NOT_READ
     - $src × $ring: $why"
            printf '   %-16s | %-10s | %s\n' "$src" "$ring" "UNREAD"
        else
            printf '   %-16s | %-10s | %s (%s)\n' "$src" "$ring" "$cell" "$why"
        fi
    done
done

echo ""
echo "   NOT READ (named, never left blank — FR-006):"
if [ -n "$NOT_READ" ]; then
    printf '%s\n' "$NOT_READ"
else
    echo "     (none — every cell measured)"
fi

echo ""
echo "   Reading this matrix:"
echo "     * a measured cell is evidence about ONE ring against ONE source, and nothing more;"
echo "     * the pinned corpus is 206 cases, NOT the 384-test unified suite — 100% here is"
echo "       not total semantic equivalence;"
echo "     * the aggregate verdict is test/ring/aggregate.sh, which REFUSES while any"
echo "       required ring is unbuilt or unread. This matrix reports; it does not gate."
exit 0
