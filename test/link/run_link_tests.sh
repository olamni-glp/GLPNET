#!/usr/bin/env bash
# =============================================================================
# Two-process real-TCP link integration tests (feature 025 runtime-integration).
#
# Each test launches TWO separate C# REPL processes over IPv4 localhost (127.0.0.1):
# a CONSUMER (server_listener — binds and waits) and a PRODUCER (client_connector —
# dials when its goal activates). The driver feeds each its goal, captures both
# transcripts to test/link/results/, and asserts an expected substring in the
# consumer's output. This is the "carefully scripted + results captured" rig the
# directive (step 5) requires; it is the C#-side rig today and gains a Dart rig +
# a cross-runtime rig as the Dart mirror lands.
#
# Usage:  bash test/link/run_link_tests.sh
# Exit:   0 if all pass, else the number of failures.
# =============================================================================
set -u

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
# The C# REPL exe is a Windows binary; give it Windows-style (forward-slash) paths.
GLP="$(cygpath -m "$ROOT" 2>/dev/null || echo "$ROOT")"
REPL="$GLP/out/csharp/glp_repl/bin/Debug/net10.0/glp_repl.exe"
RESULTS="$ROOT/test/link/results"
LINKDIR="$GLP/programs/tests/link"

mkdir -p "$RESULTS"
PASS=0
FAIL=0

if [ ! -x "$REPL" ] && [ ! -f "$REPL" ]; then
    echo "ERROR: C# REPL not built at $REPL"
    echo "  build it: (cd out/csharp/glp_repl && dotnet build)"
    exit 1
fi

# run_link_test NAME GLP_FILE CONSUMER_GOAL PRODUCER_GOAL EXPECT_IN_CONSUMER
# Starts the consumer (listener) first in the background, then the producer; the
# connector's connect-retry makes the rendezvous order-independent.
run_link_test() {
    local name="$1" glp="$2" cons_goal="$3" prod_goal="$4" expect="$5" prod_expect="${6:-}"
    local cout="$RESULTS/$name.consumer.out" pout="$RESULTS/$name.producer.out"

    printf 'load %s\n%s\n:quit\n' "$glp" "$cons_goal" | "$REPL" > "$cout" 2>&1 &
    local cpid=$!
    printf 'load %s\n%s\n:quit\n' "$glp" "$prod_goal" | "$REPL" > "$pout" 2>&1
    wait "$cpid"

    local ok=1
    grep -qF "$expect" "$cout" || ok=0
    # Optional producer-side assertion (used by the bidirectional test, where BOTH ends
    # both send and receive — FR-003 — so the producer slot also collects a result).
    if [ -n "$prod_expect" ]; then grep -qF "$prod_expect" "$pout" || ok=0; fi

    if [ "$ok" -eq 1 ]; then
        echo "  PASS: $name  (consumer saw: $expect${prod_expect:+ ; producer saw: $prod_expect})"
        PASS=$((PASS + 1))
    else
        echo "  FAIL: $name  (expected '$expect'${prod_expect:+ + producer '$prod_expect'})"
        echo "      consumer: $(grep -E '=|succeeds|failed|ABORT|Error' "$cout" | tail -3 | tr '\n' '|')"
        echo "      producer: $(grep -E '=|succeeds|failed|ABORT|Error' "$pout" | tail -3 | tr '\n' '|')"
        FAIL=$((FAIL + 1))
    fi
}

echo "======================================"
echo "Link integration: two-process real-TCP (C# REPL x2, 127.0.0.1)"
echo "======================================"

run_link_test "pc_integers" "$LINKDIR/pc.glp" \
    "main(consumer, Got)." "main(producer, X)." \
    "Got = [10, 20, 30]"

run_link_test "pc_strings" "$LINKDIR/pc.glp" \
    "main(consumer, Got)." "main(producer_strs, X)." \
    'Got = ["alice", "bob", "carol"]'

run_link_test "pc_terms" "$LINKDIR/pc.glp" \
    "main(consumer, Got)." "main(producer_terms, X)." \
    "Got = [pt(1, 2), pt(3, 4)]"

run_link_test "link_send_wrapper" "$LINKDIR/pc.glp" \
    "main(consumer, Got)." "main(producer_ls, X)." \
    "Got = [10, 20, 30]"

# Explicit link_recv-chain consumer (3 threaded link_recv) — exercises the path that
# surfaced the runner deref-conflation bug (fixed in 8af18c3a). Pairs with pc.glp's producer.
run_link_test "link_recv_chain" "$LINKDIR/sr.glp" \
    "main_sr(consumer, Got)." "main_sr(producer, X)." \
    "Got = [10, 20, 30]"

# Bidirectional (FR-003): each end both sends and receives over one link; both slots
# collect a result, so we assert BOTH directions.
run_link_test "bidirectional" "$LINKDIR/bidi.glp" \
    "main(peerb, Got)." "main(peera, Got)." \
    "Got = [1, 2, 3]" "Got = [10, 20, 30]"

# Next batch (WIP — fault-monitor+close, path-B request/accept) added here as each is
# debugged to a clean two-process pass.

echo "======================================"
echo "Link tests: PASS=$PASS FAIL=$FAIL"
echo "======================================"
if [ "$FAIL" -eq 0 ]; then
    echo "ALL LINK TESTS PASSED"
else
    echo "SOME LINK TESTS FAILED"
fi
exit "$FAIL"
