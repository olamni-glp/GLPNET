#!/usr/bin/env bash
# =============================================================================
# test/parity/cross_runtime/lib.sh — shared driver for the CROSS-RUNTIME
# distributed suite: Gleam GLP instance × C# GLP instance over real TCP
# (feature 060 US5, T043 scaffold; FR-026..FR-030).
#
# Modeled on the proven Dart×C# rig (test/link/run_link_tests_cross.sh): each
# scenario runs ONE end on the Gleam REPL (`gleam run` from glp_gleam/) and the
# OTHER on the C# REPL, over 127.0.0.1 on the ports baked into the .glp
# programs. Both runtimes load the SAME .glp source and speak the SAME wire
# (FrameCodec + PayloadSerializer byte-parity), so the two independently-written
# runtimes must interoperate transparently.
#
# Every scenario runs in BOTH directions (FR-028):
#   G→C : consumer on Gleam, producer on C#
#   C→G : consumer on C#,   producer on Gleam
#
# SC-007 / T048: every peer is wall-clock-capped (PEER_TIMEOUT); a hung peer
# makes the scenario REPORT A FAILURE instead of blocking forever.
# =============================================================================

CR_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
CR_GLP="$(cygpath -m "$CR_ROOT" 2>/dev/null || echo "$CR_ROOT")"
CR_GLEAM_DIR="$CR_ROOT/glp_gleam"
CR_CSREPL="$CR_GLP/out/csharp/glp_repl/bin/Debug/net10.0/glp_repl.exe"
CR_RESULTS="$CR_ROOT/test/parity/cross_runtime/results"
CR_LINKDIR="programs/tests/link"

PEER_TIMEOUT="${PEER_TIMEOUT:-90}"
CR_PASS=0
CR_FAIL=0

mkdir -p "$CR_RESULTS"

cr_require_csharp() {
    if [ ! -f "$CR_CSREPL" ]; then
        echo "ERROR: C# REPL not built at $CR_CSREPL"
        echo "  build it: (cd out/csharp/glp_repl && dotnet build)"
        exit 1
    fi
}

# gleam_repl <out> <glp-rel-to-repo> <goal> — one Gleam REPL process.
gleam_repl() {
    ( cd "$CR_GLEAM_DIR" && printf 'load ../%s\n%s\n:quit\n' "$2" "$3" \
        | timeout "$PEER_TIMEOUT" gleam run ) > "$1" 2>&1
}

# cs_repl <out> <glp-rel-to-repo> <goal> — one C# REPL process (repo-root cwd).
cs_repl() {
    ( cd "$CR_ROOT" && printf 'load %s\n%s\n:quit\n' "$2" "$3" \
        | timeout "$PEER_TIMEOUT" "$CR_CSREPL" ) > "$1" 2>&1
}

# cross_test NAME GLP CONS_GOAL PROD_GOAL EXPECT_CONS [EXPECT_PROD]
# Runs the scenario twice: G→C then C→G. The consumer end starts first (in the
# background); the connector's connect-retry makes the rendezvous
# order-independent.
cross_test() {
    local name="$1" glp="$2" cons="$3" prod="$4" ec="$5" ep="${6:-}"

    # --- G→C : consumer=Gleam, producer=C# ---
    local cout="$CR_RESULTS/$name.GtoC.consumer.out" pout="$CR_RESULTS/$name.GtoC.producer.out"
    gleam_repl "$cout" "$glp" "$cons" &
    local cpid=$!
    sleep 4   # let the Gleam listener compile+bind before the producer dials
    cs_repl "$pout" "$glp" "$prod"
    wait "$cpid"
    local ok=1
    grep -qF "$ec" "$cout" || ok=0
    if [ -n "$ep" ]; then grep -qF "$ep" "$pout" || ok=0; fi
    if [ "$ok" -eq 1 ]; then
        echo "  PASS: $name [G→C]  (Gleam consumer saw: $ec${ep:+ ; C# producer saw: $ep})"
        CR_PASS=$((CR_PASS + 1))
    else
        echo "  FAIL: $name [G→C]  (expected '$ec'${ep:+ + producer '$ep'})"
        echo "      consumer(Gleam): $(grep -E '=|succeeds|suspended|failed|ABORT|Error' "$cout" | tail -3 | tr '\n' '|')"
        echo "      producer(C#):    $(grep -E '=|succeeds|suspended|failed|ABORT|Error' "$pout" | tail -3 | tr '\n' '|')"
        CR_FAIL=$((CR_FAIL + 1))
    fi

    # --- C→G : consumer=C#, producer=Gleam ---
    cout="$CR_RESULTS/$name.CtoG.consumer.out"; pout="$CR_RESULTS/$name.CtoG.producer.out"
    cs_repl "$cout" "$glp" "$cons" &
    cpid=$!
    sleep 2
    gleam_repl "$pout" "$glp" "$prod"
    wait "$cpid"
    ok=1
    grep -qF "$ec" "$cout" || ok=0
    if [ -n "$ep" ]; then grep -qF "$ep" "$pout" || ok=0; fi
    if [ "$ok" -eq 1 ]; then
        echo "  PASS: $name [C→G]  (C# consumer saw: $ec${ep:+ ; Gleam producer saw: $ep})"
        CR_PASS=$((CR_PASS + 1))
    else
        echo "  FAIL: $name [C→G]  (expected '$ec'${ep:+ + producer '$ep'})"
        echo "      consumer(C#):    $(grep -E '=|succeeds|suspended|failed|ABORT|Error' "$cout" | tail -3 | tr '\n' '|')"
        echo "      producer(Gleam): $(grep -E '=|succeeds|suspended|failed|ABORT|Error' "$pout" | tail -3 | tr '\n' '|')"
        CR_FAIL=$((CR_FAIL + 1))
    fi
}

cr_summary() {
    local suite="$1"
    echo "======================================"
    echo "$suite: PASS=$CR_PASS FAIL=$CR_FAIL"
    echo "======================================"
    exit "$CR_FAIL"
}
