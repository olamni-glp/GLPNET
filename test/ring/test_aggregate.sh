#!/usr/bin/env bash
# test/ring/test_aggregate.sh — guards C4-R of
# specs/101-gleam-capability-delivery/contracts/ring-delivery.md (feature 101, T009).
#
# T009 test_unbuilt_ring_never_reads_as_pass — positive control for SC-006.
#
# THIS IS THE MOST IMPORTANT GUARD IN THE FEATURE. The single most likely way this work
# ships a lie is: the BEAM ring builds and passes, the AtomVM ring is never built, and the
# aggregate reports green because it only ever saw one ring's results. Nobody notices,
# because a green aggregate is exactly what a fully-passing run looks like.
#
# So the control builds ONE ring and asserts the aggregate REFUSES. Not "warns". Not
# "reports 1/2". Refuses, with a non-zero exit, naming the ring it could not read.
#
# Run: bash test/ring/test_aggregate.sh

set -u
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
. "$SCRIPT_DIR/lib/harness.sh"
RING_SUITE="test_aggregate"

AGG="$SCRIPT_DIR/aggregate.sh"
TMP="$(mktemp -d 2>/dev/null || echo "${TMPDIR:-/tmp}/ring_agg_$$")"
mkdir -p "$TMP"
trap 'rm -rf "$TMP"' EXIT INT TERM

echo "== $RING_SUITE =="

_agg_missing() { [ ! -f "$AGG" ]; }

# The rings this feature must deliver. Both are required for an aggregate to be complete;
# see contracts/ring-delivery.md C1 and 008 FR-017/FR-018.
RINGS_REQUIRED="beam atomvm"

write_report() {
    local dir="$1" ring="$2"
    mkdir -p "$dir"
    cat > "$dir/$ring.report" <<EOF
ring: $ring
denominator: 206
attempted: 206
agreed: 206
diverged: 0
excused: 0
not_run: none
EOF
}

# ---------------------------------------------------------------------------
# T009 — one ring built, aggregate must refuse.
# ---------------------------------------------------------------------------
test_unbuilt_ring_never_reads_as_pass() {
    local name="test_unbuilt_ring_never_reads_as_pass"
    if _agg_missing; then
        pending "$name" "aggregate (test/ring/aggregate.sh) lands at T015/T021"
        return 0
    fi

    local d="$TMP/one-ring"
    write_report "$d" beam          # beam only; atomvm deliberately absent

    local out rc
    out="$( bash "$AGG" --reports "$d" --require "$RINGS_REQUIRED" 2>&1 )"; rc=$?

    if [ "$rc" -eq 0 ]; then
        fail "$name" "aggregate returned SUCCESS with the atomvm ring unbuilt — an unbuilt ring read as a pass (SC-006). This is the failure mode the feature exists to prevent."
        return 0
    fi
    # Refused — but it must name the ring it could not read, or the operator cannot act.
    assert_contains "$name" "$out" "atomvm"
}

# ---------------------------------------------------------------------------
# The control that keeps T009 honest: an aggregate that refuses unconditionally would
# pass T009 while being useless. With BOTH rings present and passing, it must succeed.
# ---------------------------------------------------------------------------
test_complete_aggregate_is_accepted() {
    local name="test_complete_aggregate_is_accepted"
    if _agg_missing; then
        pending "$name" "aggregate (test/ring/aggregate.sh) lands at T015/T021"
        return 0
    fi
    local d="$TMP/both-rings"
    write_report "$d" beam
    write_report "$d" atomvm

    local out rc
    out="$( bash "$AGG" --reports "$d" --require "$RINGS_REQUIRED" 2>&1 )"; rc=$?
    if [ "$rc" -eq 0 ]; then
        pass "$name"
    else
        fail "$name" "aggregate refused a COMPLETE, all-passing two-ring set (rc=$rc) — an unconditional refusal makes T009 vacuous: $out"
    fi
}

# ---------------------------------------------------------------------------
# A ring present but FAILING must not be laundered into a pass by the aggregate either.
# Distinct from T009: there the ring was missing, here it is present and red.
# ---------------------------------------------------------------------------
test_failing_ring_is_not_laundered() {
    local name="test_failing_ring_is_not_laundered"
    if _agg_missing; then
        pending "$name" "aggregate (test/ring/aggregate.sh) lands at T015/T021"
        return 0
    fi
    local d="$TMP/one-red"
    write_report "$d" beam
    mkdir -p "$d"
    cat > "$d/atomvm.report" <<'EOF'
ring: atomvm
denominator: 206
attempted: 206
agreed: 200
diverged: 6
excused: 0
not_run: none
EOF
    local out rc
    out="$( bash "$AGG" --reports "$d" --require "$RINGS_REQUIRED" 2>&1 )"; rc=$?
    if [ "$rc" -eq 0 ]; then
        fail "$name" "aggregate returned SUCCESS with 6 divergences on the atomvm ring"
    else
        pass "$name"
    fi
}

# ---------------------------------------------------------------------------
# The subtler sibling of T009, and the one that nearly slipped through. A ring can be
# PRESENT, well-formed, and honest — reporting UNREAD with a named reason (T019/R4) —
# and still have `diverged: 0`. An aggregate that greens on "no divergences" would
# launder that vacuous report into a pass: zero divergences out of ZERO CASES is not
# agreement. Same failure as a missing ring, just with a file present.
# ---------------------------------------------------------------------------
test_unread_ring_is_not_laundered() {
    local name="test_unread_ring_is_not_laundered"
    if _agg_missing; then
        pending "$name" "aggregate (test/ring/aggregate.sh) lands at T015/T021"
        return 0
    fi
    local d="$TMP/one-unread"
    write_report "$d" beam
    mkdir -p "$d"
    cat > "$d/atomvm.report" <<'EOF'
ring: atomvm
denominator: 206
attempted: 0
agreed: 0
diverged: 0
excused: 0
not_run: atomvm-conformance (toolchain absent; host is target-side)
EOF
    local out rc
    out="$( bash "$AGG" --reports "$d" --require "$RINGS_REQUIRED" 2>&1 )"; rc=$?
    if [ "$rc" -eq 0 ]; then
        fail "$name" "aggregate returned SUCCESS with the atomvm ring UNREAD (0 attempted) — 0 divergences out of 0 cases was read as agreement"
        return 0
    fi
    assert_contains "$name" "$out" "atomvm"
}

test_unbuilt_ring_never_reads_as_pass
test_complete_aggregate_is_accepted
test_failing_ring_is_not_laundered
test_unread_ring_is_not_laundered

ring_summary
exit $?
