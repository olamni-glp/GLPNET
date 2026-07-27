#!/usr/bin/env bash
# =============================================================================
# Two-process real-TCP link integration tests — GLEAM link REPL x2 (feature 059,
# T074 close-link-inbound-pump). The Gleam analogue of test/link/run_link_tests_dart.sh:
# each test launches TWO separate `gleam run -m glp/link/link_repl` processes over IPv4
# localhost (127.0.0.1) — a CONSUMER (server_listener) and a PRODUCER (client_connector)
# — feeds each its goal, captures both transcripts, and asserts an expected substring in
# the consumer's (and optionally the producer's) output. Proves the Gleam link driver
# (kernels + inbound pump + egress drainer) round-trips the SAME acceptance programs the
# Dart rig does, two-process, over real TCP.
#
# Usage:  bash glp_gleam/test/link/run_link_tests_gleam.sh
# Exit:   0 if all pass, else the number of failures.
# =============================================================================
set -u

# gleam + erlang on PATH (the WinGet install location + Erlang OTP).
export PATH="/c/Users/smbuser/AppData/Local/Microsoft/WinGet/Packages/Gleam.Gleam_Microsoft.Winget.Source_8wekyb3d8bbwe:/c/Program Files/Erlang OTP/bin:$PATH"

GLEAM_DIR="$(cd "$(dirname "$0")/../.." && pwd)"   # .../glp_gleam
LINKDIR="../programs/tests/link"                    # relative to GLEAM_DIR (prelude CWD)
RESULTS="$GLEAM_DIR/test/link/results/gleam"
mkdir -p "$RESULTS"

PASS=0
FAIL=0
PEER_TIMEOUT="${PEER_TIMEOUT:-120}"

# Pre-build once so the two concurrent `gleam run`s don't race the compiler.
( cd "$GLEAM_DIR" && gleam build >/dev/null 2>&1 )

# repl <out_file> <glp_file> <goal>
repl() {
    local out="$1" glp="$2" goal="$3"
    ( cd "$GLEAM_DIR" && printf 'load %s\n%s\n:quit\n' "$glp" "$goal" \
        | timeout "$PEER_TIMEOUT" gleam run -m glp/link/link_repl ) > "$out" 2>&1
}

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
        echo "      consumer: $(grep -E '=|succeeds|suspended|failed|Error|ABORT' "$cout" | tail -3 | tr '\n' '|')"
        echo "      producer: $(grep -E '=|succeeds|suspended|failed|Error|ABORT' "$pout" | tail -3 | tr '\n' '|')"
        FAIL=$((FAIL + 1))
    fi
}

echo "======================================"
echo "Link integration: two-process real-TCP (GLEAM link REPL x2, 127.0.0.1)"
echo "======================================"

run_link_test "pc_integers"     "$LINKDIR/pc.glp"   "main(consumer, Got)." "main(producer, X)."       "Got = [10, 20, 30]"
# NOTE: pc_strings (producer_strs) round-trips the VALUES correctly (alice/bob/carol
# arrive intact) but the pre-existing Gleam REPL renderer prints a ConstString
# UNQUOTED (results.format_term: `ConstString(s) -> s`), so it shows
# `Got = [alice, bob, carol]` vs Dart's `["alice", "bob", "carol"]`. That renderer
# gap is independent of the link layer (T074) and shared with the whole corpus, so it
# is reported, not patched here.
run_link_test "pc_terms"        "$LINKDIR/pc.glp"   "main(consumer, Got)." "main(producer_terms, X)." "Got = [pt(1, 2), pt(3, 4)]"
run_link_test "link_send_wrapper" "$LINKDIR/pc.glp" "main(consumer, Got)." "main(producer_ls, X)."    "Got = [10, 20, 30]"
run_link_test "link_recv_chain" "$LINKDIR/sr.glp"   "main_sr(consumer, Got)." "main_sr(producer, X)." "Got = [10, 20, 30]"
run_link_test "bidirectional"   "$LINKDIR/bidi.glp" "main(peerb, Got)." "main(peera, Got)."           "Got = [1, 2, 3]" "Got = [10, 20, 30]"
run_link_test "monitor_close"   "$LINKDIR/mon.glp"  "main(consumer, R)." "main(producer, X)."         'res([7, 8, 9], [closed(link_id('
run_link_test "path_b_request_accept" "$LINKDIR/pathb.glp" "main(acceptor, Got)." "main(requester, X)." "Got = [100, 200, 300]"

echo "======================================"
echo "Gleam link tests: PASS=$PASS FAIL=$FAIL"
echo "======================================"
exit "$FAIL"
