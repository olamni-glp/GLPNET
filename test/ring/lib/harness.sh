#!/usr/bin/env bash
# test/ring/lib/harness.sh — shared assertion + reporting helpers for the per-ring
# conformance guards (feature 101, T003).
#
# Sits alongside test/parity/lib/, never replacing it: test/parity/ is the pinned
# cross-runtime corpus (206 cases, Dart-vs-Gleam agreement); test/ring/ is the
# per-ring delivery evidence for contracts/ring-delivery.md C1-C6.
#
# THREE OUTCOMES, and the third is the point of this file.
#
#   pass     the guard held
#   fail     the guard is violated
#   pending  the guarded artifact does not exist yet
#
# `pending` is NOT a pass and MUST NOT be counted as one. These guards are written
# before the guards they protect (C6), so a pre-implementation run is legitimately
# red — but it must be red in a way that names what is missing, rather than green in
# a way that reads as evidence. A suite with any pending case exits non-zero and says
# so. That is the same rule C4-R applies to rings: an unbuilt thing never reads as a
# pass. This repo has shipped four checks that could not fail; the harness itself is
# the first place to refuse that shape.
#
# `skip` is a fourth outcome, reserved for C5: a test whose PREMISE does not hold on
# this platform. It requires a named reason and is reported separately, never folded
# into pass. A skip with no reason is a harness error, not a skip.

set -u

RING_PASS=0
RING_FAIL=0
RING_PENDING=0
RING_SKIP=0
RING_FAILED_NAMES=""
RING_PENDING_NAMES=""

_ring_name() { printf '%s' "${RING_SUITE:-suite}::$1"; }

pass()    { RING_PASS=$((RING_PASS + 1));    echo "  pass    $(_ring_name "$1")"; }

fail() {
    RING_FAIL=$((RING_FAIL + 1))
    RING_FAILED_NAMES="$RING_FAILED_NAMES $(_ring_name "$1")"
    echo "  FAIL    $(_ring_name "$1")"
    [ $# -gt 1 ] && echo "          $2"
    return 0
}

# pending NAME REASON — the artifact under guard does not exist yet. Reason mandatory.
pending() {
    if [ $# -lt 2 ] || [ -z "${2:-}" ]; then
        echo "  HARNESS ERROR: pending '$1' with no reason" >&2
        RING_FAIL=$((RING_FAIL + 1))
        return 0
    fi
    RING_PENDING=$((RING_PENDING + 1))
    RING_PENDING_NAMES="$RING_PENDING_NAMES $(_ring_name "$1")"
    echo "  pending $(_ring_name "$1")"
    echo "          not yet implemented: $2"
    return 0
}

# skip NAME REASON — C5: the premise does not hold on this platform. Reason mandatory,
# and it is printed, so a vacuous test is visible instead of silently green.
skip() {
    if [ $# -lt 2 ] || [ -z "${2:-}" ]; then
        echo "  HARNESS ERROR: skip '$1' with no named reason (C5 requires one)" >&2
        RING_FAIL=$((RING_FAIL + 1))
        return 0
    fi
    RING_SKIP=$((RING_SKIP + 1))
    echo "  skip    $(_ring_name "$1")"
    echo "          premise does not hold here: $2"
    return 0
}

# assert_true NAME CONDITION_DESC  (reads $? of the caller's preceding test)
assert_eq() {
    local name="$1" expected="$2" actual="$3"
    if [ "$expected" = "$actual" ]; then
        pass "$name"
    else
        fail "$name" "expected='$expected' actual='$actual'"
    fi
}

assert_contains() {
    local name="$1" haystack="$2" needle="$3"
    case "$haystack" in
        *"$needle"*) pass "$name" ;;
        *) fail "$name" "output does not contain '$needle'" ;;
    esac
}

# ring_summary — print the aggregate and return the suite exit status.
# Exit 0 iff there is at least one pass and zero fails and zero pendings.
ring_summary() {
    local total=$((RING_PASS + RING_FAIL + RING_PENDING + RING_SKIP))
    echo ""
    echo "  --- ${RING_SUITE:-suite} ---"
    echo "  attempted=$total pass=$RING_PASS fail=$RING_FAIL pending=$RING_PENDING skip=$RING_SKIP"
    if [ "$RING_FAIL" -gt 0 ]; then
        echo "  RESULT: RED — failed:$RING_FAILED_NAMES"
        return 1
    fi
    if [ "$RING_PENDING" -gt 0 ]; then
        echo "  RESULT: PENDING — the guard is in place but what it guards is unbuilt:$RING_PENDING_NAMES"
        echo "  (pending is not a pass — C4-R: an unbuilt thing never reads as a pass)"
        return 2
    fi
    if [ "$RING_PASS" -eq 0 ]; then
        echo "  RESULT: VACUOUS — no case asserted anything. Treated as failure (C5)."
        return 3
    fi
    echo "  RESULT: GREEN"
    return 0
}

# repo_root — resolve from this file's location, so scripts are cwd-independent.
ring_repo_root() {
    ( cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd )
}
