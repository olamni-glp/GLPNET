#!/usr/bin/env bash
# Regression smoke test: AOT-compiled REPL exe must load root self.glp and
# successfully spawn self.glp procedures (`:=/2`, `now/1`, `'_output'/1`).
#
# History: the ch02 tutorial exposed a silent-load-failure bug in the AOT exe
# because `Platform.script.resolve('../../programs/self.glp')` overshoots when
# the .exe lives one directory shallower than the .dart source. The fix lives
# in glp_runtime/bin/glp_repl.dart (_resolveRootSelfGlpPath walks up to find
# the enclosing glp_runtime/ directory). This script builds a fresh AOT exe
# and runs ex-02 + ex-03 to ensure the regression stays fixed.
#
# Per Constitution Principle V (Test-First), this script is invoked by
# run_all_tests.sh as Section Q.

set -e

GLP_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
GLP_RUNTIME="$GLP_DIR/glp_runtime"

# Discover dart binary (matches run_all_tests.sh's heuristic)
if [ -z "$DART" ]; then
    if command -v dart >/dev/null 2>&1; then
        DART="dart"
    elif [ -x "/c/Users/gavri/dart-sdk/bin/dart" ]; then
        DART="/c/Users/gavri/dart-sdk/bin/dart"
    elif [ -x "/opt/homebrew/bin/dart" ]; then
        DART="/opt/homebrew/bin/dart"
    else
        echo "ERROR: dart not found; set DART env var or install Dart SDK" >&2
        exit 1
    fi
fi

# Helper to convert MSYS-style paths to Windows-mixed paths the AOT exe accepts.
to_repl_path() {
    if command -v cygpath >/dev/null 2>&1; then
        cygpath -m "$1"
    else
        printf "%s" "$1"
    fi
}

cd "$GLP_RUNTIME"

# Build a fresh AOT exe to a test-only filename so we don't clobber the user's
# main glp_repl.exe (which may be in use by an interactive REPL).
SMOKE_EXE="$GLP_RUNTIME/glp_repl_smoke.exe"
rm -f "$SMOKE_EXE"
echo "Compiling AOT REPL exe for smoke test..."
"$DART" compile exe bin/glp_repl.dart -o "$SMOKE_EXE" >/dev/null 2>&1

if [ ! -f "$SMOKE_EXE" ]; then
    echo "FAIL: AOT compilation produced no exe"
    exit 1
fi

# The exe must be invoked via a Windows-mixed-path .glp argument so the REPL
# resolver can open it.
EX02_GLP="$(to_repl_path "$GLP_DIR/olamni/tutorial/ch02/exercise-02/ch-02-ex-02-append-and-sum.glp")"
EX03_GLP="$(to_repl_path "$GLP_DIR/olamni/tutorial/ch02/exercise-03/ch-02-ex-03-timed-append.glp")"

PASS=0
FAIL=0

check_aot() {
    local name="$1" pattern="$2" output="$3"
    # -i: NTFS reports canonical directory case (e.g. D:\BSTDEV\...\GLPNET), so the
    # exe's self-reported path may differ in case from the repo-relative patterns
    # depending on the invoking cwd — case-insensitive matching keeps the check
    # cwd-independent on Windows (observed 2026-08-02: suite cwd canonicalized to
    # uppercase → the glp(net)? path pattern missed → false FAIL).
    if echo "$output" | grep -qi -- "$pattern"; then
        echo "  PASS: $name"
        PASS=$((PASS + 1))
    else
        echo "  FAIL: $name"
        echo "    Expected pattern: $pattern"
        echo "    Output:"
        echo "$output" | sed 's/^/      /'
        FAIL=$((FAIL + 1))
    fi
}

echo ""
echo "=== Section Q: AOT REPL exe regression smoke ==="
echo ""

# Test 1: AOT exe must report loading self.glp from the CORRECT path
# (under the repo root, not from the parent of the repo root).
echo "--- AOT self.glp load path ---"
output=$(printf ':quit\n' | "$SMOKE_EXE" 2>&1)
check_aot "AOT exe loads self.glp from correct path" "Loaded root self.glp from:.*glp\(net\)\?[/\\\\]programs[/\\\\]self.glp" "$output"

# Test 2: AOT exe must successfully run ex-02 (uses := arithmetic from self.glp)
echo ""
echo "--- AOT ex-02 (:=/2 arithmetic) ---"
output=$(printf "%s\nappend_and_sum([1,2,3], [4,5,6], Sum).\n:quit\n" "$EX02_GLP" | "$SMOKE_EXE" 2>&1)
check_aot "AOT ex-02 file loads cleanly"           "✓ Loaded:" "$output"
check_aot "AOT ex-02 runs := arithmetic to Sum=21" "Sum = 21"  "$output"
check_aot "AOT ex-02 reports succeeds"             "→ succeeds" "$output"
check_aot "AOT ex-02 does NOT emit Spawn error"    "Sum = 21"  "$output"
if echo "$output" | grep -q "Spawn could not find procedure label"; then
    echo "  FAIL: AOT ex-02 produced Spawn-not-found error (regression)"
    FAIL=$((FAIL + 1))
fi

# Test 3: AOT exe must successfully run ex-03 (uses now/1 + '_output'/1 + := from self.glp)
echo ""
echo "--- AOT ex-03 (now/1 + '_output'/1 + :=/2) ---"
output=$(printf "%s\ntimed_append([1,2,3], [a,b,c], Zs).\n:quit\n" "$EX03_GLP" | "$SMOKE_EXE" 2>&1)
check_aot "AOT ex-03 file loads cleanly"           "✓ Loaded:"                   "$output"
check_aot "AOT ex-03 emits elapsed_ms output"      "elapsed_ms("                 "$output"
check_aot "AOT ex-03 binds Zs correctly"           "Zs = \[1, 2, 3, a, b, c\]"   "$output"
check_aot "AOT ex-03 reports succeeds"             "→ succeeds"                   "$output"
if echo "$output" | grep -q "Spawn could not find procedure label"; then
    echo "  FAIL: AOT ex-03 produced Spawn-not-found error (regression)"
    FAIL=$((FAIL + 1))
fi

# Cleanup
rm -f "$SMOKE_EXE"

echo ""
echo "======================================"
echo "AOT smoke: PASS=$PASS FAIL=$FAIL"
echo "======================================"

if [ "$FAIL" -gt 0 ]; then
    exit 1
fi
exit 0
