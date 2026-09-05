#!/usr/bin/env bash
# =============================================================================
# CROSS-RUNTIME two-process real-TCP link integration tests — Dart REPL ×1 vs
# C# REPL ×1 (feature 025 Phase 8, the release gate T042/T081/SC-002/062).
#
# This is the cross-runtime rig the two same-runtime rigs (run_link_tests.sh =
# C#×2, run_link_tests_dart.sh = Dart×2) anticipated ("gains ... a cross-runtime
# rig as the Dart mirror lands"). Each test runs ONE end on the Dart REPL and the
# OTHER on the C# REPL, over IPv4 localhost (127.0.0.1) on the ports baked into the
# .glp programs. Both runtimes load the SAME .glp source and speak the SAME wire
# (byte-identical FrameCodec + PayloadSerializer, T082), so a Dart end and a C# end
# must interoperate transparently — the proof that the link layer is runtime-agnostic.
#
# Every test is run in BOTH directions:
#   D→C : consumer on Dart, producer on C#
#   C→D : consumer on C#,   producer on Dart
# so neither runtime is privileged as listener or connector.
#
# Usage:  bash test/link/run_link_tests_cross.sh
# Exit:   0 if all pass, else the number of failures.
# =============================================================================
set -u

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
GLP="$(cygpath -m "$ROOT" 2>/dev/null || echo "$ROOT")"
DART="${DART:-C:/Users/gavri/dart-sdk/bin/dart.exe}"
RT="$GLP/glp_runtime"
# TFM comes from the csproj, never a literal (test/lib/tfm.sh explains why this was
# pinned to net10.0 in seven places and what that cost).
. "$(dirname "${BASH_SOURCE[0]}")/../lib/tfm.sh"
CSREPL="$(glp_repl_exe "$GLP")" || { echo "run_link_tests_cross.sh: cannot resolve the C# REPL target framework from out/csharp/glp_repl/glp_repl.csproj - refusing rather than falling back to a stale binary" >&2; exit 2; }
RESULTS="$ROOT/test/link/results/cross"
LINKDIR="$GLP/programs/tests/link"

mkdir -p "$RESULTS"
PASS=0
FAIL=0

if [ ! -f "$CSREPL" ]; then
    echo "ERROR: C# REPL not built at $CSREPL"
    echo "  build it: (cd out/csharp/glp_repl && dotnet build)"
    exit 1
fi

# Per-peer wall-clock cap so a hung REPL (e.g. a clean-shutdown regression that leaves a
# consumer alive) makes the gate REPORT A FAILURE instead of blocking forever on `wait` —
# a release gate that hangs on the very fault it exists to catch is no gate. `timeout`
# kills the peer on expiry (exit 124); its output then lacks the expected substring → FAIL.
# Generous vs the ~7 s Windows dart/PGLite cold-init + normal ~15-20 s run.
PEER_TIMEOUT="${PEER_TIMEOUT:-90}"

# dart_repl <out> <glp> <goal>  — one Dart REPL process from glp_runtime/.
dart_repl() {
    ( cd "$RT" && printf 'load %s\n%s\n:quit\n' "$2" "$3" | timeout "$PEER_TIMEOUT" "$DART" run bin/glp_repl.dart ) > "$1" 2>&1
}
# cs_repl <out> <glp> <goal>  — one C# REPL process (Windows exe; forward-slash paths).
cs_repl() {
    printf 'load %s\n%s\n:quit\n' "$2" "$3" | timeout "$PEER_TIMEOUT" "$CSREPL" > "$1" 2>&1
}

# cross_test NAME GLP CONS_GOAL PROD_GOAL EXPECT_CONS [EXPECT_PROD]
# Runs the case twice: D→C (Dart consumer, C# producer) then C→D (C# consumer, Dart
# producer). The consumer end is always started first (in the background); the
# connector's connect-retry makes the rendezvous order-independent.
cross_test() {
    local name="$1" glp="$2" cons="$3" prod="$4" ec="$5" ep="${6:-}"

    # --- D→C : consumer=Dart, producer=C# ---
    local cout="$RESULTS/$name.DtoC.consumer.out" pout="$RESULTS/$name.DtoC.producer.out"
    dart_repl "$cout" "$glp" "$cons" &
    local cpid=$!
    cs_repl "$pout" "$glp" "$prod"
    wait "$cpid"
    local ok=1
    grep -qF "$ec" "$cout" || ok=0
    if [ -n "$ep" ]; then grep -qF "$ep" "$pout" || ok=0; fi
    if [ "$ok" -eq 1 ]; then
        echo "  PASS: $name [D→C]  (Dart consumer saw: $ec${ep:+ ; C# producer saw: $ep})"
        PASS=$((PASS + 1))
    else
        echo "  FAIL: $name [D→C]  (expected '$ec'${ep:+ + producer '$ep'})"
        echo "      consumer(Dart): $(grep -E '=|succeeds|suspended|failed|ABORT|Error' "$cout" | tail -3 | tr '\n' '|')"
        echo "      producer(C#):   $(grep -E '=|succeeds|suspended|failed|ABORT|Error' "$pout" | tail -3 | tr '\n' '|')"
        FAIL=$((FAIL + 1))
    fi

    # --- C→D : consumer=C#, producer=Dart ---
    cout="$RESULTS/$name.CtoD.consumer.out"; pout="$RESULTS/$name.CtoD.producer.out"
    cs_repl "$cout" "$glp" "$cons" &
    cpid=$!
    dart_repl "$pout" "$glp" "$prod"
    wait "$cpid"
    ok=1
    grep -qF "$ec" "$cout" || ok=0
    if [ -n "$ep" ]; then grep -qF "$ep" "$pout" || ok=0; fi
    if [ "$ok" -eq 1 ]; then
        echo "  PASS: $name [C→D]  (C# consumer saw: $ec${ep:+ ; Dart producer saw: $ep})"
        PASS=$((PASS + 1))
    else
        echo "  FAIL: $name [C→D]  (expected '$ec'${ep:+ + producer '$ep'})"
        echo "      consumer(C#):   $(grep -E '=|succeeds|suspended|failed|ABORT|Error' "$cout" | tail -3 | tr '\n' '|')"
        echo "      producer(Dart): $(grep -E '=|succeeds|suspended|failed|ABORT|Error' "$pout" | tail -3 | tr '\n' '|')"
        FAIL=$((FAIL + 1))
    fi
}

echo "======================================"
echo "Link integration: CROSS-RUNTIME real-TCP (Dart REPL × C# REPL, 127.0.0.1)"
echo "======================================"

cross_test "pc_integers"     "$LINKDIR/pc.glp"   "main(consumer, Got)." "main(producer, X)."        "Got = [10, 20, 30]"
cross_test "pc_strings"      "$LINKDIR/pc.glp"   "main(consumer, Got)." "main(producer_strs, X)."   'Got = ["alice", "bob", "carol"]'
cross_test "pc_terms"        "$LINKDIR/pc.glp"   "main(consumer, Got)." "main(producer_terms, X)."  "Got = [pt(1, 2), pt(3, 4)]"
cross_test "link_send_wrapper" "$LINKDIR/pc.glp" "main(consumer, Got)." "main(producer_ls, X)."     "Got = [10, 20, 30]"
cross_test "link_recv_chain" "$LINKDIR/sr.glp"   "main_sr(consumer, Got)." "main_sr(producer, X)."  "Got = [10, 20, 30]"
cross_test "bidirectional"   "$LINKDIR/bidi.glp" "main(peerb, Got)." "main(peera, Got)."            "Got = [1, 2, 3]" "Got = [10, 20, 30]"
cross_test "path_b_request_accept" "$LINKDIR/pathb.glp" "main(acceptor, Got)." "main(requester, X)." "Got = [100, 200, 300]"
cross_test "monitor_close"   "$LINKDIR/mon.glp"  "main(consumer, R)." "main(producer, X)."          'res([7, 8, 9], [closed(link_id('

echo "======================================"
echo "Cross-runtime link tests: PASS=$PASS FAIL=$FAIL"
echo "======================================"
if [ "$FAIL" -eq 0 ]; then echo "ALL CROSS-RUNTIME LINK TESTS PASSED"; else echo "SOME CROSS-RUNTIME LINK TESTS FAILED"; fi
exit "$FAIL"
