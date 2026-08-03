#!/usr/bin/env bash
# =============================================================================
# test/parity/cross_runtime/round_trip.sh — term identity across the
# cross-runtime link (feature 060 US5, T044/T048; FR-027, SC-006, SC-007).
#
# A term sent between the two runtimes must arrive IDENTICAL to the original:
# integers, strings, nested structures, lists, and chained receives — each in
# BOTH directions (FR-028). The wire is FrameCodec + the PayloadSerializer term
# format, byte-parity on both runtimes.
#
# UNBOUND VARIABLES — declared OUT OF SCOPE, not silently passed (spec edge
# rule): the base link is GROUND-RELAY (ruling R-7 / contract D-4 — the
# `ground(Msg?)` gate excludes unbound variables from the wire by design;
# variable transport is the globalize path, deferred with the reliability
# sublayer to T052/glink). A variable reaching the encoder is refused loudly.
#
# T048/SC-007: every peer runs under PEER_TIMEOUT; a scenario that would block
# indefinitely FAILS instead of hanging the suite.
# =============================================================================
set -u
. "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
cr_require_csharp

echo "======================================"
echo "US5 round_trip: term identity Gleam × C#, both directions"
echo "======================================"

cross_test "rt_integers"    "$CR_LINKDIR/pc.glp" "main(consumer, Got)." "main(producer, X)."       "Got = [10, 20, 30]"
cross_test "rt_strings"     "$CR_LINKDIR/pc.glp" "main(consumer, Got)." "main(producer_strs, X)."  'Got = ["alice", "bob", "carol"]'
cross_test "rt_structs"     "$CR_LINKDIR/pc.glp" "main(consumer, Got)." "main(producer_terms, X)." "Got = [pt(1, 2), pt(3, 4)]"
cross_test "rt_send_face"   "$CR_LINKDIR/pc.glp" "main(consumer, Got)." "main(producer_ls, X)."    "Got = [10, 20, 30]"
cross_test "rt_recv_chain"  "$CR_LINKDIR/sr.glp" "main_sr(consumer, Got)." "main_sr(producer, X)." "Got = [10, 20, 30]"
cross_test "rt_monitor_eos" "$CR_LINKDIR/mon.glp" "main(consumer, R)." "main(producer, X)."        'res([7, 8, 9], [closed(link_id('

echo "  OUT-OF-SCOPE: unbound-variable round-trip — base link is ground-relay"
echo "  (R-7/D-4: ground(Msg?) gates the wire; variable transport = globalize"
echo "  path, deferred to the reliability slice). Refused loudly, never silent."

cr_summary "US5 round_trip"
