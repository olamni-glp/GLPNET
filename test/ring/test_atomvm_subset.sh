#!/usr/bin/env bash
# test/ring/test_atomvm_subset.sh — guards C3 of
# specs/101-gleam-capability-delivery/contracts/ring-delivery.md (feature 101, T018).
#
# Positive control for the AtomVM subset gate (FR-004): inject a construct measured as
# unsupported into the ring and assert the BUILD-TIME gate REFUSES, naming it.
#
# The converse matters just as much here, and it is not hypothetical. The gate's first
# version used a substring match and reported all 8 constructs as violations — because it
# matched `glp/ring/atomvm.gleam`'s own `unsupported()` list, the declaration of what it
# forbids. That is the third detector in this feature to match the text describing the thing
# rather than the thing (the C1-R gate matched its own doc comment; `admit.sh` matched itself).
# So `test_declaration_is_not_a_violation` is a permanent regression guard for that shape.
#
# Run: bash test/ring/test_atomvm_subset.sh

set -u
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
. "$SCRIPT_DIR/lib/harness.sh"
RING_SUITE="test_atomvm_subset"

GATE="$SCRIPT_DIR/check_atomvm_subset.sh"
RING="$REPO_ROOT/glp_gleam/src/glp/ring/atomvm.gleam"
PROBE="$REPO_ROOT/glp_gleam/src/glp/ring/_atomvm_probe.gleam"

cleanup() { rm -f "$PROBE"; }
trap cleanup EXIT INT TERM

echo "== $RING_SUITE =="

_gate_missing() { [ ! -f "$GATE" ]; }

# ---------------------------------------------------------------------------
# The control that keeps the refusal honest: with nothing forbidden present, the gate
# must PASS. A gate that refuses unconditionally satisfies the injection test and is useless.
# ---------------------------------------------------------------------------
test_clean_ring_passes() {
    local name="test_clean_ring_passes"
    if _gate_missing; then
        pending "$name" "the C3 gate (check_atomvm_subset.sh) lands at T018"
        return 0
    fi
    local out rc
    out="$( bash "$GATE" 2>&1 )"; rc=$?
    if [ "$rc" -eq 0 ]; then
        pass "$name"
    else
        fail "$name" "the gate refused a clean ring — an unconditional refusal makes the injection test vacuous: $out"
    fi
}

# ---------------------------------------------------------------------------
# T018 — inject a forbidden construct, assert a build-time refusal that NAMES it.
# ---------------------------------------------------------------------------
test_unsupported_construct_refuses_at_build_time() {
    local name="test_unsupported_construct_refuses_at_build_time"
    if _gate_missing; then
        pending "$name" "the C3 gate (check_atomvm_subset.sh) lands at T018"
        return 0
    fi

    cat > "$PROBE" <<'PROBE_EOF'
//// TEST SCRATCH — positive control for C3 (test/ring/test_atomvm_subset.sh).
//// Deliberately imports a construct measured as outside AtomVM's subset. The gate MUST
//// refuse and name it. If you are reading this in a committed tree, the control did not
//// clean up.
import gleam/otp/actor

pub fn probe() -> Nil {
  actor.to_erlang_start_result
  Nil
}
PROBE_EOF

    local out rc
    out="$( bash "$GATE" --scope "$REPO_ROOT/glp_gleam/src/glp/ring" 2>&1 )"; rc=$?
    cleanup

    if [ "$rc" -eq 0 ]; then
        fail "$name" "a forbidden import (gleam/otp) into the AtomVM ring PASSED the gate — C3 does not hold (FR-004)"
        return 0
    fi
    # Refused — it must name the construct, or the developer cannot act on it.
    assert_contains "$name" "$out" "gleam/otp"
}

# ---------------------------------------------------------------------------
# Regression guard: the ring's own declaration of the forbidden list must NOT be read as a
# use of it. This is the defect the gate shipped with and it would silently make the gate
# useless in the other direction — permanently red, so permanently ignored.
# ---------------------------------------------------------------------------
test_declaration_is_not_a_violation() {
    local name="test_declaration_is_not_a_violation"
    if _gate_missing; then
        pending "$name" "the C3 gate (check_atomvm_subset.sh) lands at T018"
        return 0
    fi
    if [ ! -f "$RING" ]; then
        pending "$name" "glp/ring/atomvm.gleam lands at T018"
        return 0
    fi
    if ! grep -q 'proc_lib' "$RING"; then
        fail "$name" "the ring no longer declares the forbidden list, so this regression guard is vacuous — check the list has not been lost"
        return 0
    fi
    local rc
    bash "$GATE" >/dev/null 2>&1; rc=$?
    if [ "$rc" -eq 0 ]; then
        pass "$name"
    else
        fail "$name" "the gate reports a violation against a ring whose only mention of the constructs is its own declaration of them — the detector is matching text, not use"
    fi
}

# ---------------------------------------------------------------------------
# The list must not silently become empty. An empty list makes every gate run pass, and the
# pass would look identical to a real one.
# ---------------------------------------------------------------------------
test_list_is_not_empty() {
    local name="test_list_is_not_empty"
    local list="$SCRIPT_DIR/atomvm-unsupported.list"
    if [ ! -f "$list" ]; then
        pending "$name" "atomvm-unsupported.list lands at T017"
        return 0
    fi
    local n
    n="$(grep -cvE '^[[:space:]]*(#|$)' "$list" 2>/dev/null || echo 0)"
    if [ "${n:-0}" -gt 0 ]; then
        pass "$name"
    else
        fail "$name" "the unsupported list is empty — every gate run would pass, indistinguishably from a real pass"
    fi
}

test_clean_ring_passes
test_unsupported_construct_refuses_at_build_time
test_declaration_is_not_a_violation
test_list_is_not_empty

ring_summary
exit $?
