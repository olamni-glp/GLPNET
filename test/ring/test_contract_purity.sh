#!/usr/bin/env bash
# test/ring/test_contract_purity.sh — guards C1-R and C2-R of
# specs/101-gleam-capability-delivery/contracts/ring-delivery.md (feature 101, T004 + T005).
#
# T004 test_runtime_dep_in_contract_fails_build   — positive control for SC-004
# T005 test_admission_by_name_is_refused          — positive control for SC-005
#
# Both are POSITIVE CONTROLS: they do not check that a good tree passes, they check that
# a deliberately bad one FAILS. A purity rule that has never rejected anything is not a
# rule, it is a comment. Written before C1-R exists (C6), so before T013 lands these
# report `pending` — which the harness scores as not-a-pass.
#
# Run: bash test/ring/test_contract_purity.sh

set -u
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
. "$SCRIPT_DIR/lib/harness.sh"
RING_SUITE="test_contract_purity"

GLEAM_DIR="$REPO_ROOT/glp_gleam"
CONTRACT_DIR="$GLEAM_DIR/src/glp/contract"

command -v gleam >/dev/null 2>&1 || {
    echo "test_contract_purity.sh: gleam not on PATH" >&2; exit 2; }

echo "== $RING_SUITE =="

# ---------------------------------------------------------------------------
# T004 — introduce a third-party runtime dependency into the contract and assert
# the build FAILS (C1-R / SC-004).
#
# The injected module imports `gleam/erlang/process`, which is a third-party runtime
# (gleam_erlang -> the BEAM). If C1-R is enforced, the purity check rejects it. The
# scratch module is removed on every exit path, including interrupt.
# ---------------------------------------------------------------------------
INJECT="$CONTRACT_DIR/_purity_probe.gleam"
cleanup_inject() { rm -f "$INJECT"; }
trap cleanup_inject EXIT INT TERM

test_runtime_dep_in_contract_fails_build() {
    local name="test_runtime_dep_in_contract_fails_build"

    if [ ! -d "$CONTRACT_DIR" ]; then
        pending "$name" "glp/contract/ does not exist (lands at T013)"
        return 0
    fi

    # Is there a purity check to exercise at all? C1-R is the thing under test; if it
    # has not been written, say so rather than passing on its absence.
    if ! _c1r_exists; then
        pending "$name" "C1-R purity check not implemented yet (T013); nothing would reject the probe"
        return 0
    fi

    mkdir -p "$CONTRACT_DIR"
    cat > "$INJECT" <<'PROBE'
//// TEST SCRATCH — positive control for C1-R (test/ring/test_contract_purity.sh).
//// Deliberately imports a third-party runtime into the L0 contract. The build MUST
//// fail. If you are reading this in a committed tree, the control did not clean up.
import gleam/erlang/process

pub fn probe() -> process.Pid {
  process.self()
}
PROBE

    # The build gate is the purity checker PLUS the compiler. C1-R says "a build that
    # introduces a runtime dependency into the contract FAILS" — the checker is the
    # mechanism that makes that true, since `gleam build` alone compiles such an import
    # perfectly happily (measured 2026-09-03).
    local out rc
    out="$( bash "$C1R_CHECKER" 2>&1 )"; rc=$?
    if [ "$rc" -eq 0 ]; then
        out="$out
$( cd "$GLEAM_DIR" && gleam build 2>&1 )"; rc=$?
    fi
    cleanup_inject

    if [ "$rc" -ne 0 ]; then
        # It failed — now check it failed for the RIGHT reason, naming the offender.
        case "$out" in
            *contract*|*purity*|*C1-R*|*runtime*)
                pass "$name" ;;
            *)
                fail "$name" "build failed, but not on a purity ground — the failure must name the contract/runtime violation, else it is an accident. rc=$rc" ;;
        esac
    else
        fail "$name" "a third-party runtime import into glp/contract/ BUILT SUCCESSFULLY — C1-R does not hold (SC-004)"
    fi
}

# _c1r_exists — is a contract-purity check actually wired in?
#
# It must name an EXECUTABLE ARTIFACT, never a token. The first version of this predicate
# grepped the tree for the string 'C1-R' — and matched the doc comment in
# src/glp/contract/surface.gleam, concluding that enforcement existed because prose
# mentioned it. The suite's own first run caught it. Mentioning a rule is not enforcing
# one, and a detector that cannot tell the difference will certify an empty tree.
#
# Conservative by construction: absent the checker file, report absent. Never infer
# enforcement from documentation.
C1R_CHECKER="$REPO_ROOT/test/ring/check_contract_purity.sh"
_c1r_exists() { [ -f "$C1R_CHECKER" ]; }

# ---------------------------------------------------------------------------
# T005 — offer glp_gleam to L0 on the strength of the word "Gleam" and assert the
# refusal QUOTES THE NAME (C2-R / SC-005).
#
# The real case, and the reason this guard is not hypothetical: LATTICE line 35 names
# the polyglot-L0 service set as kv/, mailbox/, network/. glp_gleam/src/ contains none
# of them — it is a GLP language runtime plus ZeroMQ/TCP transports. Admission by the
# shared word "Gleam" is exactly the mistake this refuses.
# ---------------------------------------------------------------------------
test_admission_by_name_is_refused() {
    local name="test_admission_by_name_is_refused"
    local admit="$REPO_ROOT/test/ring/admit.sh"

    if [ ! -x "$admit" ] && [ ! -f "$admit" ]; then
        pending "$name" "C2 admission (test/ring/admit.sh) lands at T020"
        return 0
    fi

    local out rc
    out="$( bash "$admit" --subtree glp_gleam --justification 'it is Gleam' 2>&1 )"; rc=$?

    if [ "$rc" -eq 0 ]; then
        fail "$name" "glp_gleam was ADMITTED on a name-only justification — C2-R does not hold (SC-005)"
        return 0
    fi
    assert_contains "$name" "$out" "glp_gleam"
}

# ---------------------------------------------------------------------------
# Guard on the guard: the L0 service-set premise this whole test rests on must be
# checked, not remembered. If glp_gleam ever DOES grow kv/mailbox/network, the T005
# rationale above is stale and someone must revisit it rather than trust this file.
# ---------------------------------------------------------------------------
test_l0_service_set_premise_still_holds() {
    local name="test_l0_service_set_premise_still_holds"
    local found=""
    for svc in kv mailbox network; do
        [ -e "$GLEAM_DIR/src/glp/$svc" ] && found="$found $svc"
        [ -e "$GLEAM_DIR/src/glp/$svc.gleam" ] && found="$found $svc.gleam"
    done
    if [ -n "$found" ]; then
        fail "$name" "glp_gleam now carries L0 service-set modules ($found) — the SC-005 rationale is stale, revisit C2 rather than trusting this test"
    else
        pass "$name"
    fi
}

test_runtime_dep_in_contract_fails_build
test_admission_by_name_is_refused
test_l0_service_set_premise_still_holds

ring_summary
exit $?
