#!/usr/bin/env bash
# Cross-mode parity regression test: the same goal on the same .glp file
# should produce the same bindings whether invoked via the JIT (.dart),
# kernel snapshot (.dill), or AOT (.exe) entry point.
#
# History: the ch02 tutorial exposed a JIT/AOT divergence — `dart run` worked
# but the AOT exe failed because of a path-resolution bug in the entry-point
# script. This script asserts byte-equivalent bindings across all three modes
# (modulo the build banner / wallclock lines).

set -e

GLP_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
GLP_RUNTIME="$GLP_DIR/glp_runtime"

if [ -z "$DART" ]; then
    if command -v dart >/dev/null 2>&1; then
        DART="dart"
    elif [ -x "/c/Users/gavri/dart-sdk/bin/dart" ]; then
        DART="/c/Users/gavri/dart-sdk/bin/dart"
    elif [ -x "/opt/homebrew/bin/dart" ]; then
        DART="/opt/homebrew/bin/dart"
    else
        echo "ERROR: dart not found; set DART env var" >&2
        exit 1
    fi
fi

to_repl_path() {
    if command -v cygpath >/dev/null 2>&1; then
        cygpath -m "$1"
    else
        printf "%s" "$1"
    fi
}

cd "$GLP_RUNTIME"

# Build kernel snapshot + AOT exe (both fresh).
DILL=".dart_tool/repl_parity.dill"
EXE="glp_repl_parity.exe"
mkdir -p .dart_tool
"$DART" compile kernel bin/glp_repl.dart -o "$DILL" >/dev/null 2>&1
"$DART" compile exe bin/glp_repl.dart -o "$EXE" >/dev/null 2>&1

EX02_GLP="$(to_repl_path "$GLP_DIR/olamni/tutorial/ch02/exercise-02/ch-02-ex-02-append-and-sum.glp")"
INPUT="$EX02_GLP"$'\nappend_and_sum([1,2,3], [4,5,6], Sum).\n:quit\n'

# Strip lines that legitimately differ across modes (banner, build wallclock,
# Platform.script-derived "Loaded from" paths, and the prompt prefix on echo lines).
strip_volatile() {
    # Keep only lines that contain the goal binding result or the success marker,
    # with any leading "GLP> " prompt prefix removed.
    grep -E "(Sum =|→ succeeds|→ failed|→ suspended|Spawn could not find)" | sed 's/^GLP> //'
}

JIT_OUT=$(printf "%s" "$INPUT" | "$DART" run bin/glp_repl.dart 2>&1 | strip_volatile)
DILL_OUT=$(printf "%s" "$INPUT" | "$DART" run "$DILL"          2>&1 | strip_volatile)
AOT_OUT=$(printf "%s" "$INPUT" | "./$EXE"                       2>&1 | strip_volatile)

# Cleanup
rm -f "$DILL" "$EXE"

PASS=0
FAIL=0

echo ""
echo "=== Cross-mode parity (ex-02 primary goal) ==="
echo ""

check_parity() {
    local name="$1" expected="$2" actual="$3"
    if [ "$expected" = "$actual" ]; then
        echo "  PASS: $name"
        PASS=$((PASS + 1))
    else
        echo "  FAIL: $name"
        echo "    Expected: |$expected|"
        echo "    Actual:   |$actual|"
        FAIL=$((FAIL + 1))
    fi
}

EXPECTED=$'Sum = 21\n→ succeeds'
check_parity "JIT  produces locked binding" "$EXPECTED" "$JIT_OUT"
check_parity "DILL produces locked binding" "$EXPECTED" "$DILL_OUT"
check_parity "AOT  produces locked binding" "$EXPECTED" "$AOT_OUT"

# Pairwise diff (the key assertion: same input, same output across modes)
check_parity "JIT == DILL" "$JIT_OUT" "$DILL_OUT"
check_parity "JIT == AOT"  "$JIT_OUT" "$AOT_OUT"
check_parity "DILL == AOT" "$DILL_OUT" "$AOT_OUT"

echo ""
echo "Cross-mode parity: PASS=$PASS FAIL=$FAIL"
if [ "$FAIL" -gt 0 ]; then
    exit 1
fi
exit 0
