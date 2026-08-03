#!/usr/bin/env bash
# =============================================================================
# test/parity/cross_runtime/link_both_ways.sh — bidirectional link
# establishment between the C# and Gleam runtimes (feature 060 US5, T046;
# FR-026: either side initiating; FR-028: both directions).
#
# pc.glp's consumer is the LISTENER and its producer the CONNECTOR, so running
# the scenario in both role assignments proves EITHER runtime can initiate
# (connect) and EITHER can accept (listen). bidi.glp then holds TWO links at
# once — each end listens on one port and connects on the other — proving
# simultaneous initiator+acceptor roles in one instance on both runtimes.
# =============================================================================
set -u
. "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
cr_require_csharp

echo "======================================"
echo "US5 link_both_ways: Gleam × C# establishment, either side initiating"
echo "======================================"

cross_test "pc_integers"   "$CR_LINKDIR/pc.glp"   "main(consumer, Got)." "main(producer, X)." "Got = [10, 20, 30]"
cross_test "bidirectional" "$CR_LINKDIR/bidi.glp" "main(peerb, Got)." "main(peera, Got)."     "Got = [1, 2, 3]" "Got = [10, 20, 30]"

cr_summary "US5 link_both_ways"
