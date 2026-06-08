#!/usr/bin/env bash
# =============================================================================
# Two-process real-TCP link integration tests — DART REPL x2 (feature 025 Phase D).
#
# The Dart mirror of run_link_tests.sh (which drives the C# REPL). Each test launches
# TWO separate native-Dart REPL processes over IPv4 localhost (127.0.0.1): a CONSUMER
# (server_listener) and a PRODUCER (client_connector), feeds each its goal, captures
# both transcripts to test/link/results/dart/, and asserts an expected substring in the
# consumer's (and optionally the producer's) output. Proves the Dart link layer runs the
# SAME example programs as the C# rig, two-process, over real TCP (GATE D, Dart<->Dart).
#
# Usage:  bash test/link/run_link_tests_dart.sh
# Exit:   0 if all pass, else the number of failures.
# =============================================================================
set -u

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
GLP="$(cygpath -m "$ROOT" 2>/dev/null || echo "$ROOT")"
DART="${DART:-C:/Users/gavri/dart-sdk/bin/dart.exe}"
RT="$GLP/glp_runtime"
RESULTS="$ROOT/test/link/results/dart"
LINKDIR="$GLP/programs/tests/link"

mkdir -p "$RESULTS"
PASS=0
FAIL=0

# repl <out_file> <glp_file> <goal>  — run one Dart REPL process from glp_runtime/.
repl() {
    local out="$1" glp="$2" goal="$3"
    ( cd "$RT" && printf 'load %s\n%s\n:quit\n' "$glp" "$goal" | "$DART" run bin/glp_repl.dart ) > "$out" 2>&1
}

# run_link_test NAME GLP_FILE CONSUMER_GOAL PRODUCER_GOAL EXPECT_IN_CONSUMER [EXPECT_IN_PRODUCER]
run_link_test() {
    local name="$1" glp="$2" cons_goal="$3" prod_goal="$4" expect="$5" prod_expect="${6:-}"
    local cout="$RESULTS/$name.consumer.out" pout="$RESULTS/$name.producer.out"

    repl "$cout" "$glp" "$cons_goal" &
    local cpid=$!
    repl "$pout" "$glp" "$prod_goal"
    wait "$cpid"

    local ok=1
    grep -qF "$expect" "$cout" || ok=0
    if [ -n "$prod_expect" ]; then grep -qF "$prod_expect" "$pout" || ok=0; fi

    if [ "$ok" -eq 1 ]; then
        echo "  PASS: $name  (consumer saw: $expect${prod_expect:+ ; producer saw: $prod_expect})"
        PASS=$((PASS + 1))
    else
        echo "  FAIL: $name  (expected '$expect'${prod_expect:+ + producer '$prod_expect'})"
        echo "      consumer: $(grep -E '=|succeeds|suspended|failed|ABORT|Error' "$cout" | tail -3 | tr '\n' '|')"
        echo "      producer: $(grep -E '=|succeeds|suspended|failed|ABORT|Error' "$pout" | tail -3 | tr '\n' '|')"
        FAIL=$((FAIL + 1))
    fi
}

echo "======================================"
echo "Link integration: two-process real-TCP (DART REPL x2, 127.0.0.1)"
echo "======================================"

run_link_test "pc_integers"     "$LINKDIR/pc.glp"   "main(consumer, Got)." "main(producer, X)."        "Got = [10, 20, 30]"
run_link_test "pc_strings"      "$LINKDIR/pc.glp"   "main(consumer, Got)." "main(producer_strs, X)."   'Got = ["alice", "bob", "carol"]'
run_link_test "pc_terms"        "$LINKDIR/pc.glp"   "main(consumer, Got)." "main(producer_terms, X)."  "Got = [pt(1, 2), pt(3, 4)]"
run_link_test "link_send_wrapper" "$LINKDIR/pc.glp" "main(consumer, Got)." "main(producer_ls, X)."     "Got = [10, 20, 30]"
run_link_test "link_recv_chain" "$LINKDIR/sr.glp"   "main_sr(consumer, Got)." "main_sr(producer, X)."  "Got = [10, 20, 30]"
run_link_test "bidirectional"   "$LINKDIR/bidi.glp" "main(peerb, Got)." "main(peera, Got)."            "Got = [1, 2, 3]" "Got = [10, 20, 30]"
run_link_test "path_b_request_accept" "$LINKDIR/pathb.glp" "main(acceptor, Got)." "main(requester, X)." "Got = [100, 200, 300]"
run_link_test "monitor_close"   "$LINKDIR/mon.glp"  "main(consumer, R)." "main(producer, X)."          'res([7, 8, 9], [closed(link_id('

echo "======================================"
echo "Dart link tests: PASS=$PASS FAIL=$FAIL"
echo "======================================"
if [ "$FAIL" -eq 0 ]; then echo "ALL DART LINK TESTS PASSED"; else echo "SOME DART LINK TESTS FAILED"; fi
exit "$FAIL"
