#!/usr/bin/env bash
# test/ring/test_codexreview_fixes.sh — regression guards for the codex review of
# 2026-09-04 (run 20260904T055230Z), feature 101.
#
# Every case here REPRODUCES THE ATTACK the review described and asserts it is now refused.
# A fix whose failure mode cannot be demonstrated is indistinguishable from no fix at all —
# and this feature has already shipped three detectors that matched their own documentation,
# so "I changed the code" is not evidence.
#
# Each test names the finding it guards.
#
# Run: bash test/ring/test_codexreview_fixes.sh

set -u
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
. "$SCRIPT_DIR/lib/harness.sh"
RING_SUITE="test_codexreview_fixes"

PARSER="$SCRIPT_DIR/parse_report.sh"
AGG="$SCRIPT_DIR/aggregate.sh"
TMP="$(mktemp -d 2>/dev/null || echo "${TMPDIR:-/tmp}/cxfix_$$")"
mkdir -p "$TMP"
trap 'rm -rf "$TMP"' EXIT INT TERM

echo "== $RING_SUITE =="

write() { mkdir -p "$(dirname "$1")"; cat > "$1"; }

# ---------------------------------------------------------------------------
# P1 — "Reject reports with unaccounted denominator cases" (parse_report.sh)
# The attack: declare 206 cases, run 1, agree on it, claim not_run: none. Previously
# accepted, and the aggregate then marked the ring GREEN having silently dropped 205.
# ---------------------------------------------------------------------------
test_under_run_report_is_rejected() {
    local name="test_under_run_report_is_rejected"
    write "$TMP/under.report" <<'EOF'
ring: beam
denominator: 206
attempted: 1
agreed: 1
diverged: 0
excused: 0
not_run: none
EOF
    local out rc
    out="$( bash "$PARSER" "$TMP/under.report" 2>&1 )"; rc=$?
    if [ "$rc" -eq 0 ]; then
        fail "$name" "1-of-206 with not_run:none was ACCEPTED — 205 declared cases silently dropped"
    else
        assert_contains "$name" "$out" "unaccounted"
    fi
}

# The converse: an under-run that NAMES what it did not run is legitimate and must pass,
# or the fix would simply forbid partial runs rather than forbid unaccounted ones.
test_under_run_with_named_reason_is_accepted() {
    local name="test_under_run_with_named_reason_is_accepted"
    write "$TMP/named.report" <<'EOF'
ring: beam
denominator: 206
attempted: 1
agreed: 1
diverged: 0
excused: 0
not_run: 205 link cases (ZeroMQ transport unavailable on this host)
EOF
    local out rc
    out="$( bash "$PARSER" "$TMP/named.report" 2>&1 )"; rc=$?
    if [ "$rc" -eq 0 ]; then
        pass "$name"
    else
        fail "$name" "an under-run that NAMES its not_run was rejected — the guard forbids partial runs rather than unaccounted ones: $out"
    fi
}

# ---------------------------------------------------------------------------
# P1 — "Verify each report's ring identity before aggregating" (aggregate.sh)
# The attack: cp beam.report atomvm.report. Two green 'ring: beam' reports, one named
# atomvm.report, previously produced a GREEN aggregate with no AtomVM result in existence.
# ---------------------------------------------------------------------------
test_copied_report_is_refused() {
    local name="test_copied_report_is_refused"
    local d="$TMP/copied"
    write "$d/beam.report" <<'EOF'
ring: beam
denominator: 206
attempted: 206
agreed: 206
diverged: 0
excused: 0
not_run: none
EOF
    cp "$d/beam.report" "$d/atomvm.report"     # the attack, in one command
    local out rc
    out="$( bash "$AGG" --reports "$d" --require "beam atomvm" 2>&1 )"; rc=$?
    if [ "$rc" -eq 0 ]; then
        fail "$name" "a COPY of beam.report named atomvm.report produced a GREEN aggregate — SC-006 defeated by cp"
    else
        assert_contains "$name" "$out" "IDENTITY MISMATCH"
    fi
}

# ---------------------------------------------------------------------------
# P2 — "Keep the mandatory ring set non-overridable" (aggregate.sh)
# The attack: --require beam. A public flag that narrows the aggregate to the rings that
# happen to be built is C4-R masking with extra steps.
# ---------------------------------------------------------------------------
test_narrowed_require_is_refused() {
    local name="test_narrowed_require_is_refused"
    local d="$TMP/narrow"
    write "$d/beam.report" <<'EOF'
ring: beam
denominator: 206
attempted: 206
agreed: 206
diverged: 0
excused: 0
not_run: none
EOF
    local out rc
    out="$( bash "$AGG" --reports "$d" --require "beam" 2>&1 )"; rc=$?
    if [ "$rc" -eq 0 ]; then
        fail "$name" "--require beam produced a GREEN aggregate with AtomVM absent — C4-R bypassed by a flag"
    else
        assert_contains "$name" "$out" "mandatory ring"
    fi
}

# ---------------------------------------------------------------------------
# P1 — "Detect every third-party runtime dependency" (analyze_imports.py)
# The attack: import a third-party package that is neither gleam/erlang nor an FFI. The
# old detector recognised only those two and would classify the module runtime-free.
# ---------------------------------------------------------------------------
test_non_erlang_third_party_import_is_tainted() {
    local name="test_non_erlang_third_party_import_is_tainted"
    local probe="$REPO_ROOT/glp_gleam/src/glp/contract/_thirdparty_probe.gleam"
    cat > "$probe" <<'EOF'
//// TEST SCRATCH — positive control for the analyzer's allow-list (codex P1).
import gleam/otp/actor

pub fn probe() -> Nil {
  actor.to_erlang_start_result
  Nil
}
EOF
    local out rc
    out="$( bash "$SCRIPT_DIR/check_contract_purity.sh" 2>&1 )"; rc=$?
    rm -f "$probe"
    if [ "$rc" -eq 0 ]; then
        fail "$name" "a gleam/otp import into the contract passed C1-R — a third-party runtime that is not gleam/erlang still slips through"
    else
        assert_contains "$name" "$out" "_thirdparty_probe"
    fi
}

# ---------------------------------------------------------------------------
# P1 — "Validate the target runtime during ring admission" (admit.sh)
# The attack: offer a subtree with no <ring>.gleam to a ring. Previously beam and atomvm
# ran the identical branch, so an AtomVM-only realization could be admitted as BEAM.
# ---------------------------------------------------------------------------
test_ring_admission_requires_target_realization() {
    local name="test_ring_admission_requires_target_realization"
    local admit="$SCRIPT_DIR/admit.sh"
    if [ ! -f "$admit" ]; then
        pending "$name" "admit.sh lands at T020"
        return 0
    fi
    # glp_gleam/src/glp/contract carries no beam.gleam — it is not a BEAM realization.
    local out rc
    out="$( bash "$admit" --subtree glp_gleam/src/glp/contract --to beam 2>&1 )"; rc=$?
    if [ "$rc" -eq 0 ]; then
        fail "$name" "a subtree with no beam.gleam was admitted to the BEAM ring — one-realization-per-runtime is not enforced"
    else
        pass "$name"
    fi
}

test_under_run_report_is_rejected
test_under_run_with_named_reason_is_accepted
test_copied_report_is_refused
test_narrowed_require_is_refused
test_non_erlang_third_party_import_is_tainted
test_ring_admission_requires_target_realization

ring_summary
exit $?
