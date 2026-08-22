#!/bin/bash
# GLP Unified Test Suite v1.0
# Replaces: full_run_repl_tests.sh + run_typechecker_repl_tests.sh
# All runtime test programs are well-typed.
#
# Sections:
#   A - Typed Runtime Tests (load + run queries + check output)
#   B - Type-Check-Only Positive Tests (load succeeds)
#   C - Negative Type Tests (load must be rejected)
#   D - SRSW Violation Tests (load must be rejected)
#   E - Invalid Guard Test (true in guard rejected)
#   F - CSSG Modules (modular play tests via project-directory loading)
#   G - Social Graph Simulated UI Modules (project-directory loading)
#   H - CSSN Modules (project-directory loading, plays 1-12)
#   I - self.glp Procedure Tests (shared procs, shadowing, local shadow, type error)
#   J - CSSG v2 Modules (child_agent with parent(X) output keys)
#   K - CSSN v2 Modules (child_agent with blocking consent)
#   L - Dynamic Module Dispatch Tests (activate + M # goal)
#   M - Multi-Isolate (madGLP) Tests (dart test, CSSN v2, one isolate per agent)
#   N - Bonds V2 Modules (project-directory loading, plays 1-12)
#   O - Bonds V2 Multi-Isolate Tests (dart test, one isolate per agent)
#   P - Module Boundary Enforcement Tests (exported vs private procedures)
#   Q - AOT REPL exe regression smoke (root self.glp path resolution)

set -e

DART=${DART:-$(which dart 2>/dev/null || echo "/home/user/dart-sdk/bin/dart")}
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# to_repl_path: convert an MSYS / Git-Bash style absolute path (e.g. /d/foo
# or /tmp/foo) to a form the Windows-native Dart REPL can open via File().
# Identity on platforms without cygpath (Linux, macOS).
to_repl_path() {
    if command -v cygpath >/dev/null 2>&1; then
        cygpath -m "$1"
    else
        printf "%s" "$1"
    fi
}

GLP_DIR="$(to_repl_path "$SCRIPT_DIR/..")"
GLP_RUNTIME="$(to_repl_path "$GLP_DIR/glp_runtime")"
TYPED="$(to_repl_path "$GLP_DIR/programs/tests/typed")"
BOOK="$(to_repl_path "$GLP_DIR/programs/typed_book")"
TC_DIR="$(to_repl_path "$GLP_RUNTIME/test/programs/typechecker")"
MODED="$(to_repl_path "$GLP_RUNTIME/test/programs/moded_types")"
QUIC="$(to_repl_path "$GLP_DIR/programs/tests/quic")"

cd "$GLP_RUNTIME"

# Compile REPL to kernel snapshot for faster startup
REPL_SNAPSHOT=".dart_tool/repl.dill"
NEEDS_RECOMPILE=false
if [ ! -f "$REPL_SNAPSHOT" ]; then
    NEEDS_RECOMPILE=true
elif [ -n "$(find lib bin -name '*.dart' -newer "$REPL_SNAPSHOT" 2>/dev/null | head -1)" ]; then
    NEEDS_RECOMPILE=true
fi
if [ "$NEEDS_RECOMPILE" = true ]; then
    echo "Compiling REPL snapshot..."
    mkdir -p .dart_tool
    $DART compile kernel -o "$REPL_SNAPSHOT" bin/glp_repl.dart 2>/dev/null || true
fi
if [ -f "$REPL_SNAPSHOT" ]; then
    REPL="$REPL_SNAPSHOT"
else
    REPL="bin/glp_repl.dart"
fi

echo "======================================"
echo "   GLP Unified Test Suite v1.0        "
echo "======================================"
echo ""

PASS=0
FAIL=0
SKIP=0
UNSEARCHABLE=0
SKIP_LOG=""
SECTION_KEYS=""

# --- 078 verification receipts: a check that did not run must never read as one that passed ---
# T046: `check` only ever had PASS and FAIL, so a section that could not run contributed
# NOTHING to the totals and the suite still printed "ALL TESTS PASSED". A skip is now counted,
# carries a reason, and is re-listed in the summary.
skip() {
    local name="$1" reason="$2"
    echo "  SKIP: $name — $reason"
    SKIP=$((SKIP + 1))
    SKIP_LOG="${SKIP_LOG}
  SKIP        $name — $reason"
}

# T047: a check whose subject is STALE has not examined the thing it claims to examine.
# Measured 2026-08-13: Section U was run against a binary built 37h BEFORE the source it was
# supposed to be testing, and the stale result was read as a real defect in feature 077.
# UNSEARCHABLE says "I could not look", which is not the same as "I looked and found nothing".
unsearchable() {
    local name="$1" reason="$2"
    echo "  UNSEARCHABLE: $name — $reason"
    UNSEARCHABLE=$((UNSEARCHABLE + 1))
    SKIP_LOG="${SKIP_LOG}
  UNSEARCHABLE $name — $reason"
}

# T045: receipts are keyed by (letter, slugified-title), NEVER by letter alone — `Section I` is
# declared twice in this file (self.glp procedures, and the cross-runtime Gleam x C# suite), so a
# letter-keyed receipt would silently merge two unrelated sections. Registering the composite key
# here makes a genuine duplicate fail loudly instead of collapsing.
section() {
    local letter="$1" title="$2"
    local slug
    slug=$(printf '%s' "$title" | tr '[:upper:]' '[:lower:]' | sed -E 's/[^a-z0-9]+/-/g; s/^-+//; s/-+$//')
    local key="${letter}:${slug}"
    case "$SECTION_KEYS" in
        *"|$key|"*)
            echo "  FAIL: section receipt key '$key' is declared twice — receipts would merge two sections"
            FAIL=$((FAIL + 1))
            ;;
    esac
    SECTION_KEYS="${SECTION_KEYS}|$key|"
    echo "=== Section $letter: $title ==="
}

# Newest mtime under a set of paths, at FULL precision (find's %T@ is seconds.nanoseconds).
# Prints nothing and returns 1 when the scan finds no eligible file or fails — callers MUST treat
# that as "could not measure", never as timestamp 0. Mapping a failed measurement to a number is
# how a guard silently stops guarding (codexreview A4).
newest_mtime() {
    local newest="" f t
    for f in "$@"; do
        [ -e "$f" ] || continue
        # B3: without pipefail the pipeline reports head's status, so a find that dies partway
        # yields a PARTIAL maximum that reads as authoritative — an unreadable newer source would
        # be omitted and a stale binary called fresh. Scope the option to this subshell.
        t=$(set -o pipefail; find "$f" -type f -printf '%T@\n' 2>/dev/null | sort -rn | head -1) || return 1
        [ -n "$t" ] && { [ -z "$newest" ] || awk -v a="$t" -v b="$newest" 'BEGIN{exit !(a>b)}'; } && newest="$t"
    done
    [ -n "$newest" ] || return 1
    echo "$newest"
}

# Newest mtime of actual SOURCE for the C# REPL, ignoring build output.
# Two corrections, both found by measuring rather than assuming:
#   1. A naive newest-file scan returned glp_crdtmsg.deps.json — a BUILD ARTIFACT. Artifacts are
#      rewritten by every build, so the "source" time tracked the binary's and the guard could
#      almost never fire. (glp_crdtmsg is in fact a real dependency; the defect was measuring its
#      build output, not its relatedness.)
#   2. The scan pointed at csharp/, which is NOT where this exe comes from. glp_repl.csproj lives
#      at out/csharp/glp_repl/ and references out/csharp/glp_runtime_net.csproj,
#      csharp/glp_link and csharp/glp_crdtmsg. Scanning csharp/ wholesale watched ~20 unrelated
#      projects and MISSED the exe's own sources entirely — so it would not have caught the
#      2026-08-13 stale-binary incident it was written for.
# The roots below are glp_repl.exe's actual dependency closure, taken from its ProjectReferences.
newest_src_mtime() {
    local t roots=""
    local r
    for r in "$@"; do [ -d "$r" ] && roots="$roots $r"; done
    [ -n "$roots" ] || return 1
    # shellcheck disable=SC2086
    t=$(set -o pipefail; find $roots -type f \
            \( -name '*.cs' -o -name '*.csproj' -o -name '*.props' -o -name '*.targets' -o -name '*.sln' \) \
            -not -path '*/bin/*' -not -path '*/obj/*' \
            -printf '%T@\n' 2>/dev/null | sort -rn | head -1) || return 1
    [ -n "$t" ] || return 1
    echo "$t"
}

# a >= b on decimal seconds. The old integer compare truncated to whole seconds, so a source edited
# in the SAME second as the build read as "not newer" and the stale binary ran (codexreview A2).
mtime_ge() { awk -v a="$1" -v b="$2" 'BEGIN{exit !(a>=b)}'; }

check() {
    local name="$1" pattern="$2" source="$3"
    if echo "$source" | grep -q "$pattern"; then
        echo "  PASS: $name"
        PASS=$((PASS + 1))
    else
        echo "  FAIL: $name (expected: $pattern)"
        FAIL=$((FAIL + 1))
    fi
}

check_not() {
    local name="$1" pattern="$2" source="$3"
    if echo "$source" | grep -q "$pattern"; then
        echo "  FAIL: $name (should NOT match: $pattern)"
        FAIL=$((FAIL + 1))
    else
        echo "  PASS: $name"
        PASS=$((PASS + 1))
    fi
}

# =============================================================================
# SECTION A: TYPED RUNTIME TESTS (load + type-check + run queries)
# =============================================================================
section "A" "Typed Runtime Tests"
echo ""

# --- A1: p, merge_simple, merge_standalone, metainterpreter ---
echo "--- A1: p, merge, metainterpreter ---"
a1=$($DART run "$REPL" <<HEREDOC
$TYPED/p.glp
$BOOK/streams/producers_consumers/merge_simple.glp
$TYPED/merge_standalone.glp
$TYPED/run1.glp
p(X).
merge([1,2,3], [a,b], Xs).
merge2([c,d], Out).
clause(p(a), B).
run(true).
run(merge([a,b],[b],X)).
runA(X2).
run2(Xr2).
:quit
HEREDOC
2>&1)

check "p(X) unification" "X = a" "$a1"
check "Merge [1,2,3]+[a,b]" "Xs = \[1, a, 2, b, 3\]" "$a1"
check "Clause lookup" "B = true" "$a1"
check "run(true)" "succeeds" "$a1"
check "Meta merge" "X = \[a, b, b\]" "$a1"
check "runA empty merge" "X2 = \[\]" "$a1"

# --- A2: Append, Reverse, Copy ---
echo "--- A2: Append, Reverse, Copy ---"
a2=$($DART run "$REPL" <<HEREDOC
$BOOK/recursive/list_processing/append.glp
$BOOK/recursive/list_processing/reverse.glp
$BOOK/recursive/list_processing/copy.glp
append([a,b], [c,d], Zs).
append([], [x,y], Zs2).
append([a,b], [], Zs3).
reverse([a,b,c], Ys).
reverse([], Ys2).
reverse([x], Ys3).
copy([a,b,c], Yc).
copy([], Yc2).
:quit
HEREDOC
2>&1)

check "Append two lists" "Zs = \[a, b, c, d\]" "$a2"
check "Append empty+list" "Zs2 = \[x, y\]" "$a2"
check "Append list+empty" "Zs3 = \[a, b\]" "$a2"
check "Reverse list" "Ys = \[c, b, a\]" "$a2"
check "Reverse empty" "Ys2 = \[\]" "$a2"
check "Reverse single" "Ys3 = \[x\]" "$a2"
check "Copy list" "Yc = \[a, b, c\]" "$a2"
check "Copy empty" "Yc2 = \[\]" "$a2"

# --- A3: Quicksort ---
echo "--- A3: Quicksort ---"
a3=$($DART run "$REPL" <<HEREDOC
$BOOK/recursive/list_processing/quicksort.glp
quicksort([],Xq1).
quicksort([1],Xq2).
quicksort([1,2],Xq3).
quicksort([1,6,4,2,7,4,2,6],Xq4).
quicksort([1,3,4,2,5],Xq5).
quicksort([a],Xq6).
quicksort([1|X?],Xq7).
:quit
HEREDOC
2>&1)

check "Quicksort empty" "Xq1 = \[\]" "$a3"
check "Quicksort single" "Xq2 = \[1\]" "$a3"
check "Quicksort two" "Xq3 = \[1, 2\]" "$a3"
check "Quicksort larger" "Xq4 = \[1, 2, 2, 4, 4, 6, 6, 7\]" "$a3"
check "Quicksort five" "Xq5 = \[1, 2, 3, 4, 5\]" "$a3"
check "Quicksort non-number" "Xq6 = <unbound>" "$a3"
check "Quicksort unbound tail" "Xq7 = <unbound>" "$a3"

# --- A4: Insertion Sort ---
echo "--- A4: Insertion Sort ---"
a4=$($DART run "$REPL" <<HEREDOC
$BOOK/recursive/list_processing/insertion_sort.glp
insertion_sort([],Xi1).
insertion_sort([3],Xi2).
insertion_sort([3,4],Xi3).
insertion_sort([3,4,2,3,6,1,2],Xi4).
:quit
HEREDOC
2>&1)

check "Insertion sort empty" "Xi1 = \[\]" "$a4"
check "Insertion sort single" "Xi2 = \[3\]" "$a4"
check "Insertion sort two" "Xi3 = \[3, 4\]" "$a4"
check "Insertion sort larger" "Xi4 = \[1, 2, 2, 3, 3, 4, 6\]" "$a4"

# --- A5: Bubble Sort --- (REMOVED: bubble_sort.glp fails type checking at load time)

# --- A6: Ordered merge ---
echo "--- A6: Ordered merge ---"
a6=$($DART run "$REPL" <<HEREDOC
$BOOK/recursive/list_processing/merge_ordered.glp
merge([1,3,5], [2,4,6], Zop).
merge([1,2,3], [2,3,4], Zop2).
merge([], [1,2], Zop3).
:quit
HEREDOC
2>&1)

check "Ordered merge" "Zop = \[1, 2, 3, 4, 5, 6\]" "$a6"
check "Ordered merge duplicates" "Zop2 = \[1, 2, 2, 3, 3, 4\]" "$a6"
check "Ordered merge empty" "Zop3 = \[1, 2\]" "$a6"

# --- A7: Fair merge ---
echo "--- A7: Fair merge ---"
a7=$($DART run "$REPL" <<HEREDOC
$BOOK/streams/producers_consumers/fair_merge.glp
merge([a,b,c], [x,y,z], Zfs).
merge([a,b], [x,y,z], Zfs2).
:quit
HEREDOC
2>&1)

check "Fair merge equal" "Zfs = \[a, x, b, y, c, z\]" "$a7"
check "Fair merge unequal" "Zfs2 = \[a, x, b, y, z\]" "$a7"

# --- A8: Gates ---
echo "--- A8: Logic gates ---"
a8=$($DART run "$REPL" <<HEREDOC
$BOOK/constants/gates.glp
and([one,zero,one], [one,one,zero], OutA).
or([one,zero,one], [one,one,zero], OutO).
and([one,one], [one,one], OutA2).
or([zero,zero], [zero,zero], OutO2).
:quit
HEREDOC
2>&1)

check "AND gate" "OutA = \[one, zero, zero\]" "$a8"
check "OR gate" "OutO = \[one, one, one\]" "$a8"
check "AND all ones" "OutA2 = \[one, one\]" "$a8"
check "OR all zeros" "OutO2 = \[zero, zero\]" "$a8"

# --- A9: Arithmetic (sum, fib, factorial, hanoi, primes, inner_product) ---
echo "--- A9: Arithmetic programs ---"
a9=$($DART run "$REPL" <<HEREDOC
$BOOK/recursive/list_processing/inner_product.glp
$BOOK/recursive/arithmetic_trees/fibonacci.glp
$BOOK/recursive/arithmetic_trees/factorial.glp
$BOOK/recursive/arithmetic_trees/hanoi.glp
$BOOK/recursive/arithmetic_trees/primes.glp
inner_product([1,2,3], [4,5,6], Sipf).
fib(0, Ff0).
fib(1, Ff1).
fib(3, Ff3).
fib(10, Ff10).
factorial(1, Fac1).
factorial(2, Fac2).
factorial(3, Fac3).
factorial(5, Fac5).
hanoi(0, a, c, Mh0).
hanoi(1, a, c, Mh1).
hanoi(2, a, c, Mh2).
primes(20, Ps20).
primes(10, Ps10).
:quit
HEREDOC
2>&1)

check "Inner product" "Sipf = 32" "$a9"
check "Fibonacci 0" "Ff0 = 0" "$a9"
check "Fibonacci 1" "Ff1 = 1" "$a9"
check "Fibonacci 3" "Ff3 = 2" "$a9"
check "Fibonacci 10" "Ff10 = 55" "$a9"
check "Factorial 1" "Fac1 = 1" "$a9"
check "Factorial 2" "Fac2 = 2" "$a9"
check "Factorial 3" "Fac3 = 6" "$a9"
check "Factorial 5" "Fac5 = 120" "$a9"
check "Hanoi 0" "succeeds" "$a9"
check "Hanoi 1" "succeeds" "$a9"
check "Hanoi 2" "succeeds" "$a9"
check "Primes 20" "Ps20 = \[2, 3, 5, 7, 11, 13, 17, 19\]" "$a9"
check "Primes 10" "Ps10 = \[2, 3, 5, 7\]" "$a9"

# --- A10: Multiply ---
echo "--- A10: Multiply ---"
a10=$($DART run "$REPL" <<HEREDOC
$TYPED/multiply.glp
multiply(3, [1,2,3,4], Ym1).
multiply(5, [], Ym2).
:quit
HEREDOC
2>&1)

check "Multiply stream" "Ym1 = \[3, 6, 9, 12\]" "$a10"
check "Multiply empty" "Ym2 = \[\]" "$a10"

# --- A11: Struct demo, depth, paa, guards, misc ---
echo "--- A11: Structure and pattern tests ---"
a11=$($DART run "$REPL" <<HEREDOC
$TYPED/struct_demo.glp
$TYPED/depth_test.glp
$TYPED/paa.glp
$TYPED/no_guard.glp
$TYPED/with_guard.glp
$TYPED/two_struct_list.glp
$TYPED/nonground_list.glp
$TYPED/reader_output.glp
$TYPED/assign_reader_test.glp
build_person(P).
bin_nest(val, Xbn).
ter_all(a, b, c, Xta).
tree3(val, Xtr3).
multi_w(p, q, Xmw).
p(Xpaa1, Xpaa1?).
no_guard([5,x,y], Xng).
with_guard([5,x,y], Xwg).
test([foo(a), bar(b)]).
test_list_in_body([1,2,3,4], Xngl).
build_list(a, b, Xbld).
unwrap([hello,world], Xunw).
identity(foo, Xid).
assign_reader(hello, Xar).
:quit
HEREDOC
2>&1)

check "Build person" "P = person" "$a11"
check "Nested binary" "Xbn = outer(inner(val, b), c)" "$a11"
check "Ternary all vars" "Xta = triple(a, b, c)" "$a11"
check "Deep binary tree" "Xtr3 = node(node(leaf(val), leaf(a)), leaf(b))" "$a11"
check "Multiple writers" "Xmw = pair(wrap(p), wrap(q))" "$a11"
check "p(X,X?) succeeds" "Xpaa1 = a" "$a11"
check "No guard" "Xng = \[5, a, b" "$a11"
check "With guard" "Xwg = \[5, a, b" "$a11"
check "Two struct list" "succeeds" "$a11"
check "Non-ground list pass" "Xngl = \[1, 2, 3, 4\]" "$a11"
check "Build list" "Xbld = \[a, b\]" "$a11"
check "Unwrap" "Xunw = hello" "$a11"
check "Identity" "Xid = foo" "$a11"
check "Assign reader" "Xar = hello" "$a11"

# --- A12: Arithmetic guards, comparisons, otherwise, guard_reader ---
echo "--- A12: Arithmetic guards and otherwise ---"
a12=$($DART run "$REPL" <<HEREDOC
$TYPED/arith_guard_ground.glp
$TYPED/arith_comparison.glp
$TYPED/otherwise_guard.glp
$TYPED/guard_reader.glp
compare_and_use(3, 5, Rag1).
max(7, 4, M1).
in_range(5, 1, 10, Rir1).
in_range(15, 1, 10, Rir2).
compare_expr(1, 5, Rce1).
arith_eq(5, 5, Raeq1).
arith_eq(5, 3, Raeq2).
arith_neq(5, 3, Raneq1).
arith_neq(5, 5, Raneq2).
expr_eq(4, 6, Reeq1).
test_lt(3, 5, Rlt1).
test_gt(5, 3, Rgt1).
test_le(5, 5, Rle1).
test_ge(3, 5, Rge1).
classify(5, Rcl1).
classify(-3, Rcl2).
classify(0, Rcl3).
grade(95, G1).
grade(75, G2).
grade(55, G3).
type_of(42, T1).
type_of(hello, T2).
guard_ground(42).
guard_int(7).
guard_compare(3, 5).
guard_known_valid(hello, Ygr).
:quit
HEREDOC
2>&1)

check "compare_and_use" "Rag1 = pair(3, 5)" "$a12"
check "max" "M1 = 7" "$a12"
check "in_range yes" "Rir1 = yes" "$a12"
check "in_range no" "Rir2 = no" "$a12"
check "compare_expr" "Rce1 = pair(1, 5)" "$a12"
check "arith_eq equal" "Raeq1 = equal" "$a12"
check "arith_eq not equal" "Raeq2 = not_equal" "$a12"
check "arith_neq" "Raneq1 = not_equal" "$a12"
check "arith_neq equal" "Raneq2 = equal" "$a12"
check "expr_eq" "Reeq1 = equal" "$a12"
check "test_lt" "Rlt1 = yes" "$a12"
check "test_gt" "Rgt1 = yes" "$a12"
check "test_le" "Rle1 = yes" "$a12"
check "test_ge fails" "Rge1 = no" "$a12"
check "classify positive" "Rcl1 = positive" "$a12"
check "classify negative" "Rcl2 = negative" "$a12"
check "classify zero" "Rcl3 = zero" "$a12"
check "grade a" "G1 = a" "$a12"
check "grade c" "G2 = c" "$a12"
check "grade f" "G3 = f" "$a12"
check "type integer" "T1 = integer" "$a12"
check "type string" "T2 = string" "$a12"
check "guard_ground" "succeeds" "$a12"
check "guard_int" "succeeds" "$a12"
check "guard_compare" "succeeds" "$a12"
check "guard_known_valid" "Ygr = hello" "$a12"

# --- A13: Ground equal, guard negation ---
echo "--- A13: Ground equal and guard negation ---"
a13=$($DART run "$REPL" <<HEREDOC
$TYPED/test_ground_equal.glp
$TYPED/test_guard_negation.glp
test(a, a, R1).
test(a, b, R2).
test(foo(1,2), foo(1,2), R3).
test(foo(1,2), foo(1,3), R4).
test([1,2,3], [1,2,3], R5).
test([1,2], [1,3], R6).
test_neg_int(5, Rn1).
test_neg_int(hello, Rn2).
test_neg_number(3.14, Rn3).
test_neg_number(hello, Rn4).
test_neg_eq(5, 5, Rn5).
test_neg_eq(5, 3, Rn6).
:quit
HEREDOC
2>&1)

check "equal atoms" "R1 = equal" "$a13"
check "not equal atoms" "R2 = not_equal" "$a13"
check "equal structs" "R3 = equal" "$a13"
check "not equal structs" "R4 = not_equal" "$a13"
check "equal lists" "R5 = equal" "$a13"
check "not equal lists" "R6 = not_equal" "$a13"
check "neg int is_int" "Rn1 = is_int" "$a13"
check "neg int not_int" "Rn2 = not_int" "$a13"
check "neg number is_num" "Rn3 = is_num" "$a13"
check "neg number not_num" "Rn4 = not_num" "$a13"
check "neg eq equal" "Rn5 = eq" "$a13"
check "neg eq not equal" "Rn6 = neq" "$a13"

# --- A14: Circular terms ---
echo "--- A14: Circular term tests ---"
a14=$($DART run "$REPL" <<HEREDOC
$TYPED/circular_test.glp
is_ground(foo, Rc1).
is_ground(f(a,b), Rc2).
test_equal(foo, foo, Rc3).
test_equal(foo, bar, Rc4).
test_self_equal(f(a,b), Rc5).
show(hello, Xshow).
:quit
HEREDOC
2>&1)

check "ground foo" "Rc1 = yes" "$a14"
check "ground f(a,b)" "Rc2 = yes" "$a14"
check "equal foo foo" "Rc3 = yes" "$a14"
check "equal foo bar" "Rc4 = no" "$a14"
check "self equal" "Rc5 = yes" "$a14"
check "show" "Xshow = hello" "$a14"

# --- A15: Arithmetic fixed (uses :=) ---
echo "--- A15: Arithmetic with := ---"
a15=$($DART run "$REPL" <<HEREDOC
$TYPED/arithmetic_fixed.glp
add(5, 3, Xadd).
multiply(4, 7, Ymul).
compute(Zcomp).
subtract(10, 3, Xsub).
:quit
HEREDOC
2>&1)

check "add 5+3" "Xadd = 8" "$a15"
check "multiply 4*7" "Ymul = 28" "$a15"
check "compute (2*3)+4" "Zcomp = 10" "$a15"
check "subtract 10-3" "Xsub = 7" "$a15"

# --- A16: Arithmetic kernels ---
echo "--- A16: Arithmetic kernels ---"
a16=$($DART run "$REPL" <<HEREDOC
$TYPED/test_arithmetic_kernels.glp
test_idiv(10, 3, Rak1).
test_abs(-5, Rak2).
test_sqrt(16, Rak3).
test_pow(2, 10, Rak4).
test_floor(3.7, Rak5).
test_ceil(3.2, Rak6).
:quit
HEREDOC
2>&1)

check "idiv" "Rak1 = 3" "$a16"
check "abs" "Rak2 = 5" "$a16"
check "sqrt" "Rak3 = 4" "$a16"
check "pow" "Rak4 = 1024" "$a16"
check "floor" "Rak5 = 3" "$a16"
check "ceil" "Rak6 = 4" "$a16"

# --- A17: Guards comprehensive ---
echo "--- A17: Guards comprehensive ---"
a17=$($DART run "$REPL" <<HEREDOC
$TYPED/test_guards_comprehensive.glp
test_list_ok([1,2,3], Rgc1).
test_string_ok("hello", Rgc2).
test_constant_ok(foo, Rgc3).
:quit
HEREDOC
2>&1)

check "list guard" "Rgc1 = ok" "$a17"
check "string guard" "Rgc2 = ok" "$a17"
check "constant guard" "Rgc3 = ok" "$a17"

# --- A18: Constant ground, gethead ---
echo "--- A18: Constant ground, gethead ---"
a18=$($DART run "$REPL" <<HEREDOC
$TYPED/constant_ground_test.glp
$TYPED/gethead_test.glp
test_constant(foo, Rcgt1).
test_gethead(Rgh1).
:quit
HEREDOC
2>&1)

check "constant ground" "Rcgt1 = foo" "$a18"
check "gethead" "Rgh1 = a" "$a18"

# --- A18b: Parameterized proc decl with bare type var ---
echo "--- A18b: Param bare typevar ---"
a18b=$($DART run "$REPL" <<HEREDOC
$TYPED/param_bare_typevar.glp
test_gethead_param(Rpbt1).
:quit
HEREDOC
2>&1)

check "param bare typevar" "Rpbt1 = a" "$a18b"

# --- A19: Defined guards ---
echo "--- A19: Defined guards ---"
a19=$($DART run "$REPL" <<HEREDOC
$TYPED/test_defined_guards.glp
test(ch(Adg?, Bdg), Rdg1).
test(foo, Rdg2).
test(Xdg?, Rdg3).
:quit
HEREDOC
2>&1)

check "defined guard match" "Rdg1 = ok" "$a19"
check "defined guard fail" "Rdg2 = not_channel" "$a19"
check "defined guard suspend" "suspended" "$a19"

# --- A20: Channel guards ---
# new_channel/send/receive are prelude defined guards, unfolded by the PE.
echo "--- A20: Channel guards ---"
a20=$($DART run "$REPL" <<HEREDOC
$TYPED/test_channel_guards.glp
make_pair(MpC1, MpC2).
:quit
HEREDOC
2>&1)

check "channel make_pair succeeds" "succeeds" "$a20"

# --- A21: Comprehensive defined guards ---
echo "--- A21: Comprehensive defined guards ---"
a21=$($DART run "$REPL" <<HEREDOC
$TYPED/test_defined_guards_all.glp
make_pair(Call1, Call2).
bind_response(yes, RespYes, LocalYes).
bind_response(no, RespNo, LocalNo).
test_channel(ch(TchA?, TchB), TchR1).
test_channel(foo, TchR2).
test_channel(p(TpaA, TpaB), TchR3).
test_pair(p(TprA, TprB), TprR1).
test_pair(foo, TprR2).
test_wrapper(w(TwrX), TwrR1).
test_wrapper(foo, TwrR2).
test_nested(w(p(TnA, TnB)), TnR1).
test_nested(w(hello), TnR2).
test_nested(foo, TnR3).
test_wrap(hello, TwpR).
test_deep(foo, TdpR).
test_triple(1, 2, TtrR).
:quit
HEREDOC
2>&1)

check "DG make_pair succeeds" "succeeds" "$a21"
check "DG bind yes" 'RespYes = accept(ch(' "$a21"
check "DG bind yes local" 'LocalYes = ch(' "$a21"
check "DG bind no" "RespNo = no" "$a21"
check "DG bind no local" "LocalNo = none" "$a21"
check "DG channel ok" "TchR1 = ok" "$a21"
check "DG channel fail atom" "TchR2 = not_channel" "$a21"
check "DG channel fail pair" "TchR3 = not_channel" "$a21"
check "DG pair ok" "TprR1 = ok" "$a21"
check "DG pair fail" "TprR2 = not_pair" "$a21"
check "DG wrapper ok" "TwrR1 = ok" "$a21"
check "DG wrapper fail" "TwrR2 = not_wrapper" "$a21"
check "DG nested pair" "TnR1 = wrapper_with_pair" "$a21"
check "DG nested wrapper" "TnR2 = just_wrapper" "$a21"
check "DG nested neither" "TnR3 = neither" "$a21"
check "DG wrap binding" "TwpR = wrapped(hello)" "$a21"
check "DG deep binding" "TdpR = outer(inner(foo))" "$a21"
check "DG triple" "TtrR = pair(1, 2)" "$a21"

# --- A22: Wait test ---
echo "--- A22: Wait test ---"
a22=$($DART run "$REPL" <<HEREDOC
$TYPED/test_time.glp
wait_test(Xwait).
:quit
HEREDOC
2>&1)

check "wait test" "Xwait = done" "$a22"

# --- A23: DiffList ---
# dl_append/dl_to_list are prelude defined guards, unfolded by the PE.
echo "--- A23: Difference lists ---"
a23=$($DART run "$REPL" <<HEREDOC
$TYPED/diff_list.glp
$TYPED/bb_diff.glp
Xdl = foo\bar.
test_dl_to_list([1,2,3]\\[], Ldtl).
:quit
HEREDOC
2>&1)

check "DL term parses" 'Xdl = \\(foo, bar)' "$a23"
check "DL dl_to_list" 'Ldtl = \[1, 2, 3\]' "$a23"

# --- A24: Suspension tests ---
echo "--- A24: Suspension tests ---"
a24=$($DART run "$REPL" <<HEREDOC
$TYPED/test_bob.glp
$TYPED/test_nested_suspend.glp
$TYPED/test_guard_suspend.glp
bob(Xbob?).
level1(Xlv1?).
level2([Xlv2?|Rlv2]).
level3([wrapper(Xlv3?)|Rlv3]).
wait_ground(Xwg?).
:quit
HEREDOC
2>&1)

check "bob suspend" "suspended" "$a24"
check "level1 suspend" "suspended" "$a24"
check "level2 suspend" "suspended" "$a24"
check "level3 suspend" "suspended" "$a24"
check "guard ground suspend" "suspended" "$a24"

# --- A24b: FR-034 compound-operand-suspend (nested unbound reader in a compound) ---
echo "--- A24b: FR-034 compound-operand suspend ---"
a24b=$($DART run "$REPL" <<HEREDOC
$TYPED/compound_suspend.glp
eq_compound(pair(a, X?), R).
run_eq_wake.
:quit
HEREDOC
2>&1)

check "FR-034 compound nested-reader suspends" "suspended" "$a24b"
check "FR-034 compound reader reactivates once" "succeeds" "$a24b"

# A24c: negative control — a nested unbound WRITER is a definite FAIL, not a suspend
a24c=$($DART run "$REPL" <<HEREDOC
$TYPED/compound_suspend.glp
eq_compound(pair(a, W), R2).
:quit
HEREDOC
2>&1)

check_not "FR-034 nested writer does not suspend" "suspended" "$a24c"

# --- A24d: FR-033/SC-005 atom/1 guard (paper-kernel synonym of string/1) ---
echo "--- A24d: FR-033 atom/1 guard ---"
a24d=$($DART run "$REPL" <<HEREDOC
$TYPED/atom_guard.glp
is_atom(hello, R).
is_atom(Yd?, R5).
echo_atom(world, R6).
:quit
HEREDOC
2>&1)

check "FR-033 atom succeeds on atom" "succeeds" "$a24d"
check "FR-033 atom suspends on unbound reader" "suspended" "$a24d"
check "FR-033 atom is ground-implying (echo compiles+runs)" "R6 = wrap(world)" "$a24d"

# A24e: atom/1 negatives — number / compound / [] must FAIL (not succeed, not suspend)
a24e=$($DART run "$REPL" <<HEREDOC
$TYPED/atom_guard.glp
is_atom(42, Rn).
is_atom(foo(1), Rc).
is_atom([], Rb).
:quit
HEREDOC
2>&1)

check_not "FR-033 atom rejects non-atoms (no succeed)" "succeeds" "$a24e"
check_not "FR-033 atom rejects non-atoms (no suspend)" "suspended" "$a24e"

# --- A24f: FR-037/SC-006 @< @> @=< @>= standard-order term comparison ---
echo "--- A24f: FR-037 @< family (standard-order term comparison) ---"
a24f=$($DART run "$REPL" <<HEREDOC
$TYPED/order_guards.glp
lt(1, 2, R1).
le(1, foo, R2).
lt(f(1), g(1,2), R3).
lt(apple, banana, R4).
le(5, 5, R5).
lt(5, 5, R6).
gt(banana, apple, R7).
lt_strict(Wf?, 5, R8).
order_pair(1, 2, R9).
:quit
HEREDOC
2>&1)

check "FR-037 @< numeric" "R1 = yes" "$a24f"
check "FR-037 @=< cross-class Number<atom" "R2 = yes" "$a24f"
check "FR-037 @< compound by arity" "R3 = yes" "$a24f"
check "FR-037 @< string lexicographic" "R4 = yes" "$a24f"
check "FR-037 @=< equal" "R5 = yes" "$a24f"
check "FR-037 @< equal is false" "R6 = no" "$a24f"
check "FR-037 @> reverse" "R7 = yes" "$a24f"
check "FR-037 @< suspends on unbound operand" "suspended" "$a24f"
check "FR-037 @< ground-implying (SC-006 multi-read compiles)" "R9 = pair(1, 2)" "$a24f"

# --- A24g: FR-037/FR-033 three-valued MIDDLE case — suspend-then-reactivate-
# EXACTLY-once for @< and atom/1. Each wake goal runs in its OWN session so the
# single "succeeds" verdict unambiguously witnesses the one reactivation: the
# guard suspends on an unbound reader, a sibling waker binds it, the guard wakes
# once and commits. (A24f/A24d already cover the suspend + succeed + fail rows;
# this is the reactivation row.) Mirrors the A24b compound wake. ---
echo "--- A24g: @< / atom reactivate-exactly-once ---"
a24g_lt=$($DART run "$REPL" <<HEREDOC
$TYPED/order_guards.glp
run_lt_wake.
:quit
HEREDOC
2>&1)

check "FR-037 @< reactivates exactly once (wakes and commits)" "succeeds" "$a24g_lt"
check_not "FR-037 @< wake not left suspended" "suspended" "$a24g_lt"

a24g_atom=$($DART run "$REPL" <<HEREDOC
$TYPED/atom_guard.glp
run_atom_wake.
:quit
HEREDOC
2>&1)

check "FR-033 atom reactivates exactly once (wakes and commits)" "succeeds" "$a24g_atom"
check_not "FR-033 atom wake not left suspended" "suspended" "$a24g_atom"

# --- A24h: FR-038 =\= UNTOUCHED regression — arithmetic disequality still works
# exactly: succeed on differing ground numbers, fail (otherwise -> no) on equal,
# suspend on an unbound reader. Standing witness that the @< / atom additions
# (T010/T011) did not disturb the existing arithmetic-comparison machinery. ---
echo "--- A24h: FR-038 =\\= untouched regression ---"
a24h=$($DART run "$REPL" <<HEREDOC
$TYPED/arith_diseq.glp
ne(1, 2, Rne1).
ne(3, 3, Rne2).
ne_strict(Wne?, 2, Rne3).
:quit
HEREDOC
2>&1)

check "FR-038 =\\= succeeds on differing numbers" "Rne1 = yes" "$a24h"
check "FR-038 =\\= fails on equal numbers (otherwise)" "Rne2 = no" "$a24h"
check "FR-038 =\\= suspends on unbound reader" "suspended" "$a24h"

# --- A25: Quoted functor and body ---
echo "--- A25: Quoted functor and body ---"
a25=$($DART run "$REPL" <<HEREDOC
$TYPED/quoted_functor_test.glp
$TYPED/quoted_body_test.glp
'_test_kernel'(5, Rqf1).
double(5, Rqb1).
X = '_equator'(E, stop).
:quit
HEREDOC
2>&1)

check "quoted functor" "Rqf1 = 6" "$a25"
check "double 5" "Rqb1 = 10" "$a25"
check "quoted in struct" "_equator" "$a25"

# --- A26: Univ, assignment, MWM (stdlib, no file needed) ---
echo "--- A26: Univ, assignment, MWM ---"
a26=$($DART run "$REPL" <<HEREDOC
T1 =.. [foo].
T2 =.. [bar, x, y].
foo(a, b) =.. L1.
bar(1, 2, 3) =.. L2.
Xu1 = foo.
Xu2 = 42.
Xu3 = foo(a, b).
Xu4 = [1, 2, 3].
Xu5 = foo(bar(a)).
Xu6 = Y?.
Xa1 := 3.
Xa2 := 5 + 3.
Xa3 := 10 - 4.
Xa4 := 4 * 7.
Xa5 := 20 / 4.
Xa6 := 5 + 3 * 2.
Xa7 := (5 + 3) * 2.
Xa8 := -5.
mwm([], Xmwm1).
mwm([stream([1,2,3])], Xmwm2).
mwm([stream([a,b]), stream([1,2])], Xmwm3).
:quit
HEREDOC
2>&1)

check "Univ compose foo" "T1 = foo()" "$a26"
check "Univ compose bar" "T2 = bar(x, y)" "$a26"
check "Univ decompose foo(a,b)" "L1 = \[foo, a, b\]" "$a26"
check "Univ decompose bar(1,2,3)" "L2 = \[bar, 1, 2, 3\]" "$a26"
check "Unify atom" "Xu1 = foo" "$a26"
check "Unify number" "Xu2 = 42" "$a26"
check "Unify struct" "Xu3 = foo(a, b)" "$a26"
check "Unify list" "Xu4 = \[1, 2, 3\]" "$a26"
check "Unify nested" "Xu5 = foo(bar(a))" "$a26"
check "Unify suspend" "succeeds" "$a26"
check "Assign 3" "Xa1 = 3" "$a26"
check "Assign add" "Xa2 = 8" "$a26"
check "Assign sub" "Xa3 = 6" "$a26"
check "Assign mul" "Xa4 = 28" "$a26"
check "Assign div" "Xa5 = 5" "$a26"
check "Assign precedence" "Xa6 = 11" "$a26"
check "Assign parens" "Xa7 = 16" "$a26"
check "Assign negative" "Xa8 = -5" "$a26"
check "MWM empty" "Xmwm1 = \[\]" "$a26"
check "MWM single" "Xmwm2 = \[1, 2, 3\]" "$a26"
check "MWM two streams" "Xmwm3 = \[a, b, 1, 2\]" "$a26"

# --- A27: Reader-to-reader bug (befriend_intro) ---
echo "--- A27: Reader-to-reader fail ---"
a27=$($DART run "$REPL" <<HEREDOC
$TYPED/test_befriend_intro_bug.glp
med(charlie, ch([msg(agent, _user, befriend_intro(bob, alice, X?)) | Xs], Y), ch(Us?, Vs), [], 2).
:quit
HEREDOC
2>&1)
check_not "reader-to-reader no reduction" "req(2)" "$a27"

# --- A28: Module guard ---
echo "--- A28: Module guard ---"
a28=$($DART run "$REPL" <<HEREDOC
$TYPED/module_guard.glp
test_not_module(42, Rm1).
:quit
HEREDOC
2>&1)
check "module guard ~module(42)" "Rm1 = not_module" "$a28"

# --- A29: 049 policy guard satisfiable/2 (form (a) runtime-defined guard) ---
# Ordered-outcome assertion: the REPL executes goals sequentially, so the
# sequence of "→ succeeds|failed|suspended" lines attributes each goal.
# Suspend cases are step-limited by the REPL (suspended ≠ hang, 049 research R6).
echo "--- A29: 049 policy guard (form a) ---"
a29w=$($DART run "$REPL" <<HEREDOC
$TYPED/policy_guard_worked.glp
test_wx1.
test_wx2.
test_wx3.
test_wx4(Rwx4?).
:quit
HEREDOC
2>&1)
a29w_seq=$(echo "$a29w" | grep -oE '→ (succeeds|failed|suspended)' | tr '\n' ' ')
if [ "$a29w_seq" = "→ succeeds → failed → failed → suspended " ]; then
    echo "  PASS: policy guard worked examples wx1-wx4 (S/F/F/Susp)"
    PASS=$((PASS + 1))
else
    echo "  FAIL: policy guard worked examples wx1-wx4 (got: $a29w_seq)"
    FAIL=$((FAIL + 1))
fi

a29v=$($DART run "$REPL" <<HEREDOC
$TYPED/policy_guard_vectors.glp
test_wx1.
test_wx2.
test_wx3.
test_wx4(Rwx4?).
test_v05.
test_v06.
test_v07.
test_v08.
test_v09.
test_v10.
test_v11(Tv11?).
test_v12(Rv12?).
:quit
HEREDOC
2>&1)
a29v_seq=$(echo "$a29v" | grep -oE '→ (succeeds|failed|suspended)' | tr '\n' ' ')
a29v_expected="→ succeeds → failed → failed → suspended → succeeds → failed → succeeds → succeeds → failed → failed → suspended → failed "
if [ "$a29v_seq" = "$a29v_expected" ]; then
    echo "  PASS: policy guard vectors (12/12 outcome map)"
    PASS=$((PASS + 1))
else
    echo "  FAIL: policy guard vectors (got: $a29v_seq)"
    FAIL=$((FAIL + 1))
fi

# SC-009 (a) ≡ (b) equivalence: the same vector goals re-run under form (a)
# (GLP_POLICY_GUARD_FORM=a routes satisfiable/2 to the user-program defined-guard
# table instead of the form-(b) system primitive) must produce the identical map.
a29a=$(GLP_POLICY_GUARD_FORM=a $DART run "$REPL" <<HEREDOC
$TYPED/policy_guard_vectors.glp
test_wx1.
test_wx2.
test_wx3.
test_wx4(Rwx4?).
test_v05.
test_v06.
test_v07.
test_v08.
test_v09.
test_v10.
test_v11(Tv11?).
test_v12(Rv12?).
:quit
HEREDOC
2>&1)
a29a_seq=$(echo "$a29a" | grep -oE '→ (succeeds|failed|suspended)' | tr '\n' ' ')
if [ "$a29a_seq" = "$a29v_expected" ]; then
    echo "  PASS: policy guard vectors form (a) reference (12/12, SC-009 equivalence)"
    PASS=$((PASS + 1))
else
    echo "  FAIL: policy guard vectors form (a) reference (got: $a29a_seq)"
    FAIL=$((FAIL + 1))
fi

# --- A30: 049 policy guard form (b) system primitive (declaration-only caller) ---
echo "--- A30: 049 policy guard (form b system primitive) ---"
a30=$($DART run "$REPL" <<HEREDOC
$TYPED/policy_guard_formb.glp
test_b1.
test_b2(Rb2?).
test_b3(Rb3?).
:quit
HEREDOC
2>&1)
a30_seq=$(echo "$a30" | grep -oE '→ (succeeds|failed|suspended)' | tr '\n' ' ')
if [ "$a30_seq" = "→ succeeds → failed → suspended " ]; then
    echo "  PASS: form (b) satisfiable/2 with no user clauses (S/F/Susp)"
    PASS=$((PASS + 1))
else
    echo "  FAIL: form (b) satisfiable/2 with no user clauses (got: $a30_seq)"
    FAIL=$((FAIL + 1))
fi

# --- A31: 062 US4 multi-client control program (per-client streams + broadcast + merge) ---
echo "--- A31: 062 US4 multi-client control program ---"
a31=$($DART run "$REPL" <<HEREDOC
$TYPED/multi_client_control.glp
control_demo(X).
:quit
HEREDOC
2>&1)

check "US4 control_demo loads (type-checks + compiles)" "Loaded:" "$a31"
check "US4 control_demo succeeds" "succeeds" "$a31"
check "US4 client c1 replies pong" "pong(c1)" "$a31"
check "US4 client c2 replies pong" "pong(c2)" "$a31"
check "US4 client c3 replies pong" "pong(c3)" "$a31"
check "US4 clients reply bye" "bye(c1)" "$a31"

# --- A32: 062 US5 §1.14 pins — nested-structure head matching + abandon-operation ---
# Pins the parity-confirmed semantics (T029/T030) as Dart-REPL regressions.
# Nested WRITE (make_person), nested READ (get_age/get_city), nested soft-fail
# (age/=weight mismatch), and abandon-operation (anonymous writer `_` drops the
# stream tail in first_only). Negative (_? reader) lives in Section C.
echo "--- A32: 062 US5 nested-structure + abandon pins ---"
a32=$($DART run "$REPL" <<HEREDOC
$TYPED/struct_demo.glp
$TYPED/abandon_stream.glp
make_person(alice, thirty, seattle, P).
get_age(person(alice, age(thirty), city(seattle)), A).
get_city(person(alice, age(thirty), city(seattle)), C).
get_age(person(alice, weight(eighty), city(seattle)), A2).
first_only([a, b, c], Y).
first_only([], Z).
:quit
HEREDOC
2>&1)

check "US5 nested WRITE make_person" "P = person(alice, age(thirty), city(seattle))" "$a32"
check "US5 nested READ get_age" "A = thirty" "$a32"
check "US5 nested READ get_city" "C = seattle" "$a32"
check "US5 nested soft-fail (functor mismatch)" "A2 = <unbound>" "$a32"
check "US5 abandon stream-tail first_only" "Y = first(a)" "$a32"
check "US5 abandon empty coverage" "Z = empty" "$a32"
a32_seq=$(echo "$a32" | grep -oE '→ (succeeds|failed|suspended)' | tr '\n' ' ')
if [ "$a32_seq" = "→ succeeds → succeeds → succeeds → failed → succeeds → succeeds " ]; then
    echo "  PASS: US5 pin outcome sequence (3 succeed, soft-fail, 2 succeed)"
    PASS=$((PASS + 1))
else
    echo "  FAIL: US5 pin outcome sequence (got: $a32_seq)"
    FAIL=$((FAIL + 1))
fi

SECTION_A_PASS=$PASS
SECTION_A_FAIL=$FAIL

echo ""
echo "Section A: $SECTION_A_PASS passed, $SECTION_A_FAIL failed"
echo ""

# =============================================================================
# SECTION B: TYPE-CHECK-ONLY POSITIVE TESTS
# (Load each file, verify "Loaded:" message, use :clear between files)
# =============================================================================
section "B" "Positive Type Check Tests"
echo ""

POSITIVE_FILES=(
    # --- typechecker/positive ---
    "$TC_DIR/positive/merge_basic.glp"
    "$TC_DIR/positive/append_list.glp"
    "$TC_DIR/positive/copy_stream.glp"
    "$TC_DIR/positive/dl_append.glp"
    "$TC_DIR/positive/new_channel.glp"
    "$TC_DIR/positive/monitor.glp"
    "$TC_DIR/positive/int_list_sum.glp"
    "$TC_DIR/positive/nat_operations.glp"
    "$TC_DIR/positive/process_complete.glp"
    "$TC_DIR/positive/disjoint_primitives.glp"
    "$TC_DIR/positive/universal_structured_term.glp"
    "$TC_DIR/positive/guards_all.glp"
    "$TC_DIR/positive/merge_variable_coverage_base.glp"
    "$TC_DIR/positive/merge_variable_coverage_mixed.glp"
    "$TC_DIR/positive/merge_variable_coverage_recursive.glp"
    "$TC_DIR/positive/book/universal_accepts_structured.glp"

    # --- moded_types/valid ---
    "$MODED/valid/append.glp"
    "$MODED/valid/counter.glp"
    "$MODED/valid/simple_io.glp"
    "$MODED/valid/merge.glp"
    "$MODED/valid/union_alias_basic.glp"
    "$MODED/valid/union_alias_simple.glp"
    "$MODED/valid/union_alias_three.glp"

    # --- moded_types/valid/embedded ---
    "$MODED/valid/embedded/counter_show.glp"
    "$MODED/valid/embedded/input_with_input_embedded.glp"
    "$MODED/valid/embedded/output_with_input_embedded.glp"
    "$MODED/valid/embedded/output_with_output_embedded.glp"

    # --- moded_types/valid/universal ---
    "$MODED/valid/universal/any_copy.glp"
    "$MODED/valid/universal/any_multi_clause.glp"
    "$MODED/valid/universal/list_with_any_element.glp"
    "$MODED/valid/universal/any_constant_at_output.glp"
    "$MODED/valid/universal/any_constant_at_input.glp"
    "$MODED/valid/universal/any_empty_list.glp"

    # --- typed_book/constants ---
    "$BOOK/constants/circuits.glp"
    "$BOOK/constants/gates.glp"
    "$BOOK/constants/gates_simple.glp"

    # --- typed_book/recursive/arithmetic_trees ---
    "$BOOK/recursive/arithmetic_trees/ackermann.glp"
    "$BOOK/recursive/arithmetic_trees/exp.glp"
    "$BOOK/recursive/arithmetic_trees/factorial.glp"
    "$BOOK/recursive/arithmetic_trees/fibonacci.glp"
    "$BOOK/recursive/arithmetic_trees/gcd_integer.glp"
    "$BOOK/recursive/arithmetic_trees/lesseq.glp"
    "$BOOK/recursive/arithmetic_trees/min.glp"
    "$BOOK/recursive/arithmetic_trees/natural_numbers.glp"
    "$BOOK/recursive/arithmetic_trees/plus.glp"
    "$BOOK/recursive/arithmetic_trees/primes.glp"
    "$BOOK/recursive/arithmetic_trees/times.glp"

    # --- typed_book/recursive/list_processing ---
    "$BOOK/recursive/list_processing/append.glp"
    "$BOOK/recursive/list_processing/copy.glp"
    "$BOOK/recursive/list_processing/delete.glp"
    "$BOOK/recursive/list_processing/dl_append.glp"
    "$BOOK/recursive/list_processing/filter_even.glp"
    "$BOOK/recursive/list_processing/inner_product.glp"
    "$BOOK/recursive/list_processing/inner_product_iter.glp"
    "$BOOK/recursive/list_processing/insertion_sort.glp"
    "$BOOK/recursive/list_processing/length.glp"
    "$BOOK/recursive/list_processing/map_inc.glp"
    "$BOOK/recursive/list_processing/maxlist.glp"
    "$BOOK/recursive/list_processing/member.glp"
    "$BOOK/recursive/list_processing/merge_ordered.glp"
    "$BOOK/recursive/list_processing/merge_sort.glp"
    "$BOOK/recursive/list_processing/nth.glp"
    "$BOOK/recursive/list_processing/polygon_area.glp"
    "$BOOK/recursive/list_processing/quicksort.glp"
    "$BOOK/recursive/list_processing/reverse.glp"
    "$BOOK/recursive/list_processing/reverse_naive.glp"
    "$BOOK/recursive/list_processing/translate.glp"
    "$BOOK/recursive/list_processing/variants/quicksort_original.glp"

    # --- typed_book/recursive/structure_processing ---
    "$BOOK/recursive/structure_processing/binary_tree.glp"
    "$BOOK/recursive/structure_processing/list_to_bst.glp"
    "$BOOK/recursive/structure_processing/substitute.glp"
    "$BOOK/recursive/structure_processing/traversals.glp"
    "$BOOK/recursive/structure_processing/tree_sum.glp"

    # --- typed_book/social_graph ---
    "$BOOK/social_graph/channel.glp"
    "$BOOK/social_graph/typed_social_agent.glp"

    # --- typed_book/social_networks ---
    "$BOOK/social_networks/broadcast.glp"
    "$BOOK/social_networks/replicate.glp"
    "$BOOK/social_networks/replicate2.glp"
    "$BOOK/social_networks/replicate3.glp"

    # --- typed_book/streams/buffered_communication ---
    "$BOOK/streams/buffered_communication/hollow_integers.glp"

    # --- typed_book/streams/producers_consumers ---
    "$BOOK/streams/producers_consumers/biased_merge.glp"
    "$BOOK/streams/producers_consumers/cooperative.glp"
    "$BOOK/streams/producers_consumers/distribute.glp"
    "$BOOK/streams/producers_consumers/distribute_binary.glp"
    "$BOOK/streams/producers_consumers/distribute_ground.glp"
    "$BOOK/streams/producers_consumers/distribute_indexed.glp"
    "$BOOK/streams/producers_consumers/fair_merge.glp"
    "$BOOK/streams/producers_consumers/merge_dynamic.glp"
    "$BOOK/streams/producers_consumers/merge_simple.glp"
    "$BOOK/streams/producers_consumers/merge_tree.glp"
    "$BOOK/streams/producers_consumers/mwm.glp"
    "$BOOK/streams/producers_consumers/producer_consumer.glp"
    "$BOOK/streams/producers_consumers/producer_consumer_countdown.glp"

    # --- typed_book/misc ---
    "$BOOK/test_bug.glp"
    "$BOOK/test_friend.glp"
    "$BOOK/test_lookup2.glp"

    # --- subtyping positive tests ---
    "$TC_DIR/positive/subtyping/basic_readop_fileop.glp"
    "$TC_DIR/positive/subtyping/constants_fewer_alternatives.glp"
    "$TC_DIR/positive/subtyping/contravariant_response_slot.glp"
    "$TC_DIR/positive/subtyping/direct_constant_subtype.glp"
    "$TC_DIR/positive/subtyping/struct_fewer_functors.glp"

    # --- module guard test ---
    "$TYPED/module_guard.glp"

    # --- T012/FR-037/FR-033 guard SRSW-relaxation positives: @< and atom/1 are
    # ground-implying, so a var they ground may be read multiply (order_pair,
    # echo_atom). These files must type-check cleanly (SC-006 positive). ---
    "$TYPED/order_guards.glp"
    "$TYPED/atom_guard.glp"

    # --- feature 076 (known-issues Issue 4): occurrence-pair licensing. A body-atom
    #     WRITER at a declared consume position is mode-consistent when its SRSW pair
    #     is a head-flipped reader (an output hole). issue4_bind_later pins the
    #     canonical `=` shape plus the pre-076 workaround form; head_flip_general
    #     pins the same rule over user-defined procedures at depth 1 and depth 2
    #     (rule: docs/type system/well-typed-clause.md, Definition 5.7 clause 2
    #     amendment; matrix row 3). ---
    "$TYPED/issue4_bind_later.glp"
    "$TYPED/head_flip_general.glp"

    # --- parameterized types ---
    "$TYPED/param_stream_integer.glp"
    "$TYPED/param_channel.glp"
    "$TYPED/param_procedure_inference.glp"

    # --- feature 050: GLP-native true-QUIC link (US1) — the one-bind program over a "quic"
    #     link_id loads clean (SRSW + type-check + compile). The genuine one-bind wire crossing
    #     is proven by xUnit QuicLinkOneBindTests (real MsQuic) + the two-host acceptance run. ---
    "$QUIC/quic_one_bind.glp"

    # --- feature 050 US4: the role-parameterized cross-host MESH program — opens every peer-pair
    #     link as a GLP goal (FR-003/FR-012), ships crdtmsg/7 envelopes (post-US2 wire), and
    #     collects faults on the per-link monitor stream. Loads clean (SRSW + type-check + compile);
    #     the genuine multi-accept mesh / reliability / cyber behaviour is proven by xUnit
    #     QuicMeshTests/QuicReliabilityTests/QuicCyberTests + the two-host acceptance run (T043). ---
    "$QUIC/quic_mesh.glp"

    # --- feature 050 follow-on: the full-duplex CHAT demo — bounded symmetric exchange (each side
    #     sends LineCount lines and collects the peer's before closing, so send-done never tears the
    #     link down under collect). Loads clean (SRSW + type-check + compile). ---
    "$QUIC/quic_chat.glp"
)

# Build REPL input: load each positive file with :clear between
B_INPUT=""
for f in "${POSITIVE_FILES[@]}"; do
    B_INPUT+="$f"$'\n'
    B_INPUT+=":clear"$'\n'
done
B_INPUT+=":quit"$'\n'

b_output=$(echo "$B_INPUT" | $DART run "$REPL" 2>&1)

B_PASS=0
B_FAIL=0
FAILED_POSITIVE=()
for f in "${POSITIVE_FILES[@]}"; do
    name=$(basename "$f" .glp)
    # Check for errors first
    if echo "$b_output" | grep -q "Type errors in $f"; then
        echo "  FAIL: $name (unexpected type errors)"
        B_FAIL=$((B_FAIL + 1))
        FAIL=$((FAIL + 1))
        FAILED_POSITIVE+=("$f")
    elif echo "$b_output" | grep -q "SRSW violations in $f"; then
        echo "  FAIL: $name (unexpected SRSW violations)"
        B_FAIL=$((B_FAIL + 1))
        FAIL=$((FAIL + 1))
        FAILED_POSITIVE+=("$f")
    elif echo "$b_output" | grep -q "Error loading $f"; then
        echo "  FAIL: $name (loading error)"
        B_FAIL=$((B_FAIL + 1))
        FAIL=$((FAIL + 1))
        FAILED_POSITIVE+=("$f")
    elif echo "$b_output" | grep -q "Loaded: $f"; then
        echo "  PASS: $name"
        B_PASS=$((B_PASS + 1))
        PASS=$((PASS + 1))
    else
        echo "  FAIL: $name (unknown failure)"
        B_FAIL=$((B_FAIL + 1))
        FAIL=$((FAIL + 1))
        FAILED_POSITIVE+=("$f")
    fi
done

echo ""
echo "Section B: $B_PASS passed, $B_FAIL failed"
if [ ${#FAILED_POSITIVE[@]} -gt 0 ]; then
    echo "Failed positive tests:"
    for f in "${FAILED_POSITIVE[@]}"; do
        echo "  - $f"
    done
fi
echo ""

# =============================================================================
# SECTION C: NEGATIVE TYPE TESTS (must be rejected)
# =============================================================================
section "C" "Negative Type Tests"
echo ""

NEGATIVE_FILES=(
    # --- typechecker/negative/coverage ---
    "$TC_DIR/negative/coverage/merge_missing_both_nil.glp"
    "$TC_DIR/negative/coverage/merge_missing_first_nil.glp"
    "$TC_DIR/negative/coverage/merge_missing_cons.glp"

    # --- typechecker/negative/head ---
    "$TC_DIR/negative/head/merge_wrong_constant.glp"
    "$TC_DIR/negative/head/merge_wrong_functor.glp"

    # --- typechecker/negative/body ---
    "$TC_DIR/negative/body/merge_undefined_proc.glp"
    "$TC_DIR/negative/body/merge_wrong_mode.glp"

    # --- typechecker/negative/complementarity ---
    "$TC_DIR/negative/complementarity/merge_type_mismatch.glp"
    "$TC_DIR/negative/complementarity/merge_swapped_vars.glp"

    # --- typechecker/negative/type_def ---
    "$TC_DIR/negative/type_def/merge_undefined_type.glp"

    # --- typechecker/negative (top level) ---
    "$TC_DIR/negative/merge_incomplete.glp"
    "$TC_DIR/negative/missing_coverage.glp"
    "$TC_DIR/negative/non_complementary_types.glp"
    "$TC_DIR/negative/append_bad_type.glp"
    "$TC_DIR/negative/constant_at_wrong_type.glp"
    "$TC_DIR/negative/functor_mismatch.glp"
    "$TC_DIR/negative/channel_non_complementary.glp"

    # --- moded_types/invalid ---
    "$MODED/invalid/reader_at_input.glp"
    "$MODED/invalid/writer_at_output.glp"
    "$MODED/invalid/call_mode_mismatch.glp"
    "$MODED/invalid/embedded_mode_error.glp"
    "$MODED/invalid/union_alias_overlap.glp"
    "$MODED/invalid/union_alias_refs_alias.glp"

    # --- moded_types/invalid/embedded ---
    "$MODED/invalid/embedded/counter_wrong_mode.glp"

    # --- moded_types/invalid/deep ---
    "$MODED/invalid/deep/accumulator_wrong_mode.glp"
    "$MODED/invalid/deep/channel_wrong_inversion.glp"
    "$MODED/invalid/deep/correct_type_wrong_annotation.glp"
    "$MODED/invalid/deep/double_nesting_error.glp"
    "$MODED/invalid/deep/list_tail_mode_error.glp"
    "$MODED/invalid/deep/mixed_clauses.glp"
    "$MODED/invalid/deep/nested_struct_wrong_mode.glp"
    "$MODED/invalid/deep/pair_list_wrong_mode.glp"
    "$MODED/invalid/deep/recursive_type_deep_error.glp"
    "$MODED/invalid/deep/response_slot_no_embedded.glp"

    # --- moded_types/invalid/universal ---
    "$MODED/invalid/universal/any_list_cons.glp"
    "$MODED/invalid/universal/any_mixed_clauses.glp"
    "$MODED/invalid/universal/any_reduce_pattern.glp"
    "$MODED/invalid/universal/any_struct_at_input.glp"
    "$MODED/invalid/universal/any_struct_at_output.glp"

    # --- subtyping negative tests ---
    "$TC_DIR/negative/subtyping/wrong_direction_fileop_readop.glp"
    "$TC_DIR/negative/subtyping/contravariant_wrong_direction.glp"
    "$TC_DIR/negative/subtyping/disjoint_types.glp"
    "$TC_DIR/negative/subtyping/arg_type_mismatch.glp"

    # --- parameterized types negative ---
    "$TYPED/param_arity_mismatch.glp"

    # --- parameterized proc decl negative (Case A: own clauses checked) ---
    "$TC_DIR/negative/body/param_merge_wrong_mode.glp"

    # --- T012/FR-036 DECLINED guards: == \== \= reader/1 are NOT GLP guards and
    # a clause using one in guard position MUST be rejected at load (the first
    # three are not even GLP tokens -> syntax error; reader/1 is an undefined
    # guard predicate -> type error). Enforces the decline, not merely "unimplemented". ---
    "$TYPED/decline_eq_bad.glp"
    "$TYPED/decline_neq_bad.glp"
    "$TYPED/decline_struct_diseq_bad.glp"
    "$TYPED/decline_reader_bad.glp"
    # 062 US5 §1.14 item 1 (abandon-operation) negative: _? anonymous reader
    # in a clause position must be rejected (typed-glp-manual §9.1).
    "$TYPED/abandon_reader_bad.glp"
    # feature 076 negative control (matrix row 4): the occurrence-pair license
    # requires the head hole as positive evidence, so a body-atom writer at a
    # consume position whose pair is absent, is in the body, or is at a head
    # consume position must still be rejected - no over-acceptance.
    "$TYPED/head_flip_negative.glp"
)

# Build REPL input with :clear between each negative file
C_INPUT=""
for f in "${NEGATIVE_FILES[@]}"; do
    C_INPUT+="$f"$'\n'
    C_INPUT+=":clear"$'\n'
done
C_INPUT+=":quit"$'\n'

c_output=$(echo "$C_INPUT" | $DART run "$REPL" 2>&1)

C_PASS=0
C_FAIL=0
for f in "${NEGATIVE_FILES[@]}"; do
    name=$(basename "$f" .glp)
    if echo "$c_output" | grep -q "Loaded: $f"; then
        echo "  FAIL: $name (expected rejection, got loaded)"
        C_FAIL=$((C_FAIL + 1))
        FAIL=$((FAIL + 1))
    else
        echo "  PASS: $name (rejected)"
        C_PASS=$((C_PASS + 1))
        PASS=$((PASS + 1))
    fi
done

echo ""
echo "Section C: $C_PASS passed, $C_FAIL failed"
echo ""

# =============================================================================
# SECTION D: SRSW VIOLATION TESTS
# =============================================================================
section "D" "SRSW Violation Tests"
echo ""

SRSW_FILES=(
    "$TC_DIR/negative/head/merge_reader_at_input.glp"
    "$TC_DIR/negative/head/merge_writer_at_output.glp"
)

for f in "${SRSW_FILES[@]}"; do
    name=$(basename "$f" .glp)
    srsw_out=$(echo -e "$f\n:quit" | $DART run "$REPL" 2>&1)
    if echo "$srsw_out" | grep -qi "SRSW violation\|Error loading"; then
        echo "  PASS: $name (rejected)"
        PASS=$((PASS + 1))
    else
        echo "  FAIL: $name (should be rejected)"
        FAIL=$((FAIL + 1))
    fi
done

# Also test merge_with_reader
SRSW_MWR="$GLP_DIR/programs/tests/archive/repl/merge_with_reader.glp"
if [ -f "$SRSW_MWR" ]; then
    srsw_mwr_out=$(echo -e "$SRSW_MWR\n:quit" | $DART run "$REPL" 2>&1)
    if echo "$srsw_mwr_out" | grep -qi "SRSW violation"; then
        echo "  PASS: merge_with_reader (SRSW rejected)"
        PASS=$((PASS + 1))
    else
        echo "  FAIL: merge_with_reader (should be rejected)"
        FAIL=$((FAIL + 1))
    fi
fi

echo ""

# =============================================================================
# SECTION E: INVALID GUARD TEST
# =============================================================================
section "E" "Invalid Guard Test"
echo ""

TMP_GUARD=$(mktemp /tmp/glp_test.XXXXXX)
mv "$TMP_GUARD" "${TMP_GUARD}.glp"
TMP_GUARD="${TMP_GUARD}.glp"
cat > "$TMP_GUARD" << 'TMPEOF'
bad_guard(X?) :- true | X = done.
TMPEOF
# Convert /tmp/... path to a form the Windows REPL can open
TMP_GUARD_REPL="$(to_repl_path "$TMP_GUARD")"

guard_out=$(echo -e "$TMP_GUARD_REPL\n:quit" | $DART run "$REPL" 2>&1)
rm -f "$TMP_GUARD"

if echo "$guard_out" | grep -q '"true" is not a guard'; then
    echo "  PASS: true in guard position rejected"
    PASS=$((PASS + 1))
else
    echo "  FAIL: true in guard position should be rejected"
    FAIL=$((FAIL + 1))
fi

echo ""

# =============================================================================
# Section F: CSSG Modules (modular play tests)
# =============================================================================
section "F" "CSSG Modules"
echo ""

cssg_result=$(bash "$SCRIPT_DIR/cssg_modules_test.sh" 2>&1)
cssg_pass=$(echo "$cssg_result" | grep "^Total:" | sed 's/.*Passed: \([0-9]*\).*/\1/')
cssg_fail=$(echo "$cssg_result" | grep "^Total:" | sed 's/.*Failed: \([0-9]*\).*/\1/')

if [ -n "$cssg_pass" ] && [ -n "$cssg_fail" ]; then
    PASS=$((PASS + cssg_pass))
    FAIL=$((FAIL + cssg_fail))
    echo "$cssg_result" | grep -E "PASS:|FAIL:|CSSG|Using"
else
    echo "  FAIL: cssg_modules_test.sh did not produce expected output"
    FAIL=$((FAIL + 1))
fi

echo ""

# =============================================================================
# Section G: Social Graph Simulated UI Modules (project-directory loading)
# =============================================================================
section "G" "Social Graph Simulated UI Modules"
echo ""

SGSIM="$GLP_DIR/programs/social_graph_simulated_ui_modules"

# Loading
g_load=$($DART run "$REPL" <<HEREDOC
$SGSIM
:quit
HEREDOC
2>&1)

check "SG-SIM project loads" "Loaded project" "$g_load"
check_not "SG-SIM no type errors" "Type checking failed" "$g_load"
check_not "SG-SIM no load errors" "Error loading" "$g_load"

# Silent plays (play1-play3)
echo "--- Silent plays (play1-play3) ---"
for play_num in 1 2 3; do
    g_play=$($DART run "$REPL" <<HEREDOC
$SGSIM
play${play_num}.
:quit
HEREDOC
2>&1)
    check "SG play${play_num} succeeds" "succeeds\|suspended" "$g_play"
done

# Tagged plays (fplay1-fplay3) with output checks
echo "--- Tagged plays (fplay1-fplay3) ---"

g_fp1=$($DART run "$REPL" <<HEREDOC
$SGSIM
fplay1.
:quit
HEREDOC
2>&1)

check "SG fplay1 succeeds" "succeeds\|suspended" "$g_fp1"
check "SG fplay1 alice connected bob" "tagged(alice.*connected(bob)" "$g_fp1"
check "SG fplay1 charlie connected alice" "tagged(charlie.*connected(alice)" "$g_fp1"

g_fp2=$($DART run "$REPL" <<HEREDOC
$SGSIM
fplay2.
:quit
HEREDOC
2>&1)

check "SG fplay2 succeeds" "succeeds\|suspended" "$g_fp2"
check "SG fplay2 rejected" "tagged(alice.*rejected" "$g_fp2"

g_fp3=$($DART run "$REPL" <<HEREDOC
$SGSIM
fplay3.
:quit
HEREDOC
2>&1)

check "SG fplay3 succeeds" "succeeds\|suspended" "$g_fp3"

echo ""

# =============================================================================
# Section H: CSSN Modules (project-directory loading)
# =============================================================================
section "H" "CSSN Modules"
echo ""

CSSN="$GLP_DIR/programs/cssn_modules"

# Loading
h_load=$($DART run "$REPL" <<HEREDOC
$CSSN
:quit
HEREDOC
2>&1)

check "CSSN project loads" "Loaded project" "$h_load"
check_not "CSSN no type errors" "Type checking failed" "$h_load"
check_not "CSSN no load errors" "Error loading" "$h_load"

# fplay1-3: Basic social graph
echo "--- Basic social graph (fplay1-fplay3) ---"

h_fp1=$($DART run "$REPL" <<HEREDOC
$CSSN
fplay1.
:quit
HEREDOC
2>&1)

check "CSSN fplay1 succeeds" "succeeds\|suspended" "$h_fp1"
check "CSSN fplay1 alice connected bob" "tagged(alice.*connected(bob)" "$h_fp1"
check "CSSN fplay1 charlie connected alice" "tagged(charlie.*connected(alice)" "$h_fp1"

h_fp2=$($DART run "$REPL" <<HEREDOC
$CSSN
fplay2.
:quit
HEREDOC
2>&1)

check "CSSN fplay2 succeeds" "succeeds\|suspended" "$h_fp2"
check "CSSN fplay2 rejected" "tagged(alice.*rejected" "$h_fp2"

h_fp3=$($DART run "$REPL" <<HEREDOC
$CSSN
fplay3.
:quit
HEREDOC
2>&1)

check "CSSN fplay3 succeeds" "succeeds\|suspended" "$h_fp3"

# fplay4-7: CSSG (child-safe social graph)
echo "--- CSSG plays (fplay4-fplay7) ---"

h_fp4=$($DART run "$REPL" <<HEREDOC
$CSSN
fplay4.
:quit
HEREDOC
2>&1)

check "CSSN fplay4 succeeds" "succeeds\|suspended" "$h_fp4"
check "CSSN fplay4 carol connected dave" "tagged(carol.*connected(dave)" "$h_fp4"

for play_num in 5 6 7; do
    h_fpN=$($DART run "$REPL" <<HEREDOC
$CSSN
fplay${play_num}.
:quit
HEREDOC
2>&1)
    check "CSSN fplay${play_num} succeeds" "succeeds\|suspended" "$h_fpN"
done

# fplay8-10: CSSN groups
echo "--- CSSN group plays (fplay8-fplay10) ---"

h_fp8=$($DART run "$REPL" <<HEREDOC
$CSSN
fplay8.
:quit
HEREDOC
2>&1)

check "CSSN fplay8 succeeds" "succeeds\|suspended" "$h_fp8"
check "CSSN fplay8 group_joined" "tagged(alice.*group_joined" "$h_fp8"
check "CSSN fplay8 group_received" "group_received" "$h_fp8"

h_fp9=$($DART run "$REPL" <<HEREDOC
$CSSN
fplay9.
:quit
HEREDOC
2>&1)

check "CSSN fplay9 succeeds" "succeeds\|suspended" "$h_fp9"

h_fp10=$($DART run "$REPL" <<HEREDOC
$CSSN
fplay10.
:quit
HEREDOC
2>&1)

check "CSSN fplay10 succeeds" "succeeds\|suspended" "$h_fp10"

# fplay11-12: Large CSSN scenarios
echo "--- Large CSSN plays (fplay11-fplay12) ---"

h_fp11=$($DART run "$REPL" <<HEREDOC
$CSSN
fplay11.
:quit
HEREDOC
2>&1)

check "CSSN fplay11 succeeds" "succeeds\|suspended" "$h_fp11"
check "CSSN fplay11 tagged output" "tagged(" "$h_fp11"

h_fp12=$($DART run "$REPL" <<HEREDOC
$CSSN
fplay12.
:quit
HEREDOC
2>&1)

check "CSSN fplay12 succeeds" "succeeds\|suspended" "$h_fp12"
check "CSSN fplay12 tagged output" "tagged(" "$h_fp12"

echo ""

# =============================================================================
# Section I: self.glp Procedure Tests
# =============================================================================
section "I" "self.glp Procedure Tests"
echo ""

SELFPROC_TESTS="$GLP_DIR/programs/tests"

# --- I1: self.glp shared procedure ---
echo "--- I1: self.glp shared procedure ---"
i1=$($DART run "$REPL" <<HEREDOC
$SELFPROC_TESTS/module_self_procs
test_self_proc(5, R).
:quit
HEREDOC
2>&1)

check "self.glp shared proc loads" "Loaded project" "$i1"
check "self.glp shared proc result" "R = 10" "$i1"

# --- I2: self.glp shadowing ---
echo "--- I2: self.glp shadowing ---"
i2=$($DART run "$REPL" <<HEREDOC
$SELFPROC_TESTS/module_self_shadow
test_shadow(X, Y).
:quit
HEREDOC
2>&1)

check "self.glp shadow loads" "Loaded project" "$i2"
check "self.glp shadow outer" "X = outer" "$i2"
check "self.glp shadow inner" "Y = inner" "$i2"

# --- I3: Local shadows self.glp ---
echo "--- I3: Local shadows self.glp ---"
i3=$($DART run "$REPL" <<HEREDOC
$SELFPROC_TESTS/module_self_local_shadow
test_local_shadow(R).
:quit
HEREDOC
2>&1)

check "local shadow loads" "Loaded project" "$i3"
check "local shadow result" "R = from_local" "$i3"

# --- I4: Type error in self.glp (negative) ---
echo "--- I4: Type error in self.glp (negative) ---"
i4=$($DART run "$REPL" <<HEREDOC
$SELFPROC_TESTS/module_self_type_error
:quit
HEREDOC
2>&1)

check "self.glp type error rejected" "Type checking failed\|type.*error\|Error" "$i4"
check_not "self.glp type error not loaded" "Loaded project" "$i4"

echo ""

# =============================================================================
# Section J: CSSG v2 Modules (child_agent with parent(X) output keys)
# =============================================================================
section "J" "CSSG v2 Modules"
echo ""

CSSG_V2="$GLP_DIR/programs/cssg_modules_v2"

# Loading
j_load=$($DART run "$REPL" <<HEREDOC
$CSSG_V2
:quit
HEREDOC
2>&1)

check "CSSG v2 project loads" "Loaded project" "$j_load"
check_not "CSSG v2 no type errors" "Type checking failed" "$j_load"

# fplay4-7: child_agent plays
echo "--- CSSG v2 child_agent plays (fplay4-fplay7) ---"

j_fp4=$($DART run "$REPL" <<HEREDOC
$CSSG_V2
fplay4.
:quit
HEREDOC
2>&1)

check "CSSG v2 fplay4 succeeds" "succeeds\|suspended" "$j_fp4"
check "CSSG v2 fplay4 carol connected dave" "tagged(carol.*connected(dave)" "$j_fp4"

for play_num in 5 6 7; do
    j_fpN=$($DART run "$REPL" <<HEREDOC
$CSSG_V2
fplay${play_num}.
:quit
HEREDOC
2>&1)
    check "CSSG v2 fplay${play_num} succeeds" "succeeds\|suspended" "$j_fpN"
done

echo ""

# =============================================================================
# Section K: CSSN v2 Modules (child_agent with blocking consent)
# =============================================================================
section "K" "CSSN v2 Modules"
echo ""

CSSN_V2="$GLP_DIR/programs/cssn_modules_v2"

# Loading
k_load=$($DART run "$REPL" <<HEREDOC
$CSSN_V2
:quit
HEREDOC
2>&1)

check "CSSN v2 project loads" "Loaded project" "$k_load"
check_not "CSSN v2 no type errors" "Type checking failed" "$k_load"

# fplay1-3: Basic social graph (adult-only, unchanged)
echo "--- CSSN v2 basic social graph (fplay1-fplay3) ---"

for play_num in 1 2 3; do
    k_fpN=$($DART run "$REPL" <<HEREDOC
$CSSN_V2
fplay${play_num}.
:quit
HEREDOC
2>&1)
    check "CSSN v2 fplay${play_num} succeeds" "succeeds\|suspended" "$k_fpN"
done

# fplay4-7: child_agent befriending
echo "--- CSSN v2 child_agent befriending (fplay4-fplay7) ---"

k_fp4=$($DART run "$REPL" <<HEREDOC
$CSSN_V2
fplay4.
:quit
HEREDOC
2>&1)

check "CSSN v2 fplay4 succeeds" "succeeds\|suspended" "$k_fp4"
check "CSSN v2 fplay4 carol connected dave" "tagged(carol.*connected(dave)" "$k_fp4"

for play_num in 5 6 7; do
    k_fpN=$($DART run "$REPL" <<HEREDOC
$CSSN_V2
fplay${play_num}.
:quit
HEREDOC
2>&1)
    check "CSSN v2 fplay${play_num} succeeds" "succeeds\|suspended" "$k_fpN"
done

# fplay8-10: CSSN groups
echo "--- CSSN v2 group plays (fplay8-fplay10) ---"

k_fp8=$($DART run "$REPL" <<HEREDOC
$CSSN_V2
fplay8.
:quit
HEREDOC
2>&1)

check "CSSN v2 fplay8 succeeds" "succeeds\|suspended" "$k_fp8"
check "CSSN v2 fplay8 group_joined" "tagged(alice.*group_joined" "$k_fp8"

for play_num in 9 10; do
    k_fpN=$($DART run "$REPL" <<HEREDOC
$CSSN_V2
fplay${play_num}.
:quit
HEREDOC
2>&1)
    check "CSSN v2 fplay${play_num} succeeds" "succeeds\|suspended" "$k_fpN"
done

# fplay11: child-managed group with blocking consent
echo "--- CSSN v2 blocking consent play (fplay11) ---"

k_fp11=$($DART run "$REPL" <<HEREDOC
$CSSN_V2
fplay11.
:quit
HEREDOC
2>&1)

check "CSSN v2 fplay11 succeeds" "succeeds\|suspended" "$k_fp11"
check "CSSN v2 fplay11 tagged output" "tagged(" "$k_fp11"

# fplay12: adult-managed group with children
echo "--- CSSN v2 adult-managed group play (fplay12) ---"

k_fp12=$($DART run "$REPL" <<HEREDOC
$CSSN_V2
fplay12.
:quit
HEREDOC
2>&1)

check "CSSN v2 fplay12 succeeds" "succeeds\|suspended" "$k_fp12"
check "CSSN v2 fplay12 tagged output" "tagged(" "$k_fp12"

echo ""

# =============================================================================
# Section L: Dynamic Module Dispatch Tests
# =============================================================================
section "L" "Dynamic Module Dispatch Tests"
echo ""

DD="$GLP_DIR/programs/tests/dynamic_dispatch"

# --- L1: Activate module and dispatch double via client ---
echo "--- L1: Dynamic dispatch double ---"
l1=$($DART run "$REPL" <<HEREDOC
$DD/math_service.glp
:activate math_service
$DD/dispatch_client.glp
test_double(5, X).
:quit
HEREDOC
2>&1)

check "math_service activated" "Activated module" "$l1"
check "test_double(5, X) = 10" "X = 10" "$l1"

# --- L2: Triple dispatch ---
echo "--- L2: Dynamic dispatch triple ---"
l2=$($DART run "$REPL" <<HEREDOC
$DD/math_service.glp
:activate math_service
$DD/dispatch_client.glp
test_triple(4, X).
:quit
HEREDOC
2>&1)

check "test_triple(4, X) = 12" "X = 12" "$l2"

# --- L3: Add_ten dispatch ---
echo "--- L3: Dynamic dispatch add_ten ---"
l3=$($DART run "$REPL" <<HEREDOC
$DD/math_service.glp
:activate math_service
$DD/dispatch_client.glp
test_add_ten(7, X).
:quit
HEREDOC
2>&1)

check "test_add_ten(7, X) = 17" "X = 17" "$l3"

echo ""

# =============================================================================
# Section M: Multi-Isolate (madGLP) Tests
# =============================================================================

section "M" "Multi-Isolate (madGLP) Tests"
echo ""

MAD_RESULT=$("$DART" test "$GLP_RUNTIME/test/multiagent/cssn_v2_isolate_test.dart" 2>&1)
MAD_EXIT=$?

if [ $MAD_EXIT -eq 0 ]; then
    # Count passing tests from output like "+13: All tests passed!"
    MAD_PASSED=$(echo "$MAD_RESULT" | grep -oE '\+[0-9]+' | tail -1 | tr -d '+')
    MAD_PASSED=${MAD_PASSED:-13}
    echo "  PASS: All $MAD_PASSED multi-isolate tests passed"
    PASS=$((PASS + MAD_PASSED))
else
    # Extract failure count
    MAD_FAILED=$(echo "$MAD_RESULT" | grep -oE '\-[0-9]+' | tail -1 | tr -d '-')
    MAD_FAILED=${MAD_FAILED:-1}
    MAD_PASSED=$(echo "$MAD_RESULT" | grep -oE '\+[0-9]+' | tail -1 | tr -d '+')
    MAD_PASSED=${MAD_PASSED:-0}
    echo "  FAIL: $MAD_FAILED multi-isolate test(s) failed ($MAD_PASSED passed)"
    echo "$MAD_RESULT" | tail -20
    PASS=$((PASS + MAD_PASSED))
    FAIL=$((FAIL + MAD_FAILED))
fi

echo ""

# =============================================================================
# Section N: Bonds V2 Modules (project-directory loading, plays 1-12)
# =============================================================================
section "N" "Bonds V2 Modules"
echo ""

BONDS_V2="$GLP_DIR/programs/bonds_v2"

# Loading
n_load=$($DART run "$REPL" <<HEREDOC
$BONDS_V2
:quit
HEREDOC
2>&1)

check "Bonds v2 project loads" "Loaded project" "$n_load"
check_not "Bonds v2 no type errors" "Type checking failed" "$n_load"

# fplay1: solo mint
echo "--- Bonds v2 solo mint (fplay1) ---"

n_fp1=$($DART run "$REPL" <<HEREDOC
$BONDS_V2
fplay1.
:quit
HEREDOC
2>&1)

check "Bonds v2 fplay1 succeeds" "succeeds" "$n_fp1"
check "Bonds v2 fplay1 minted" "tagged(alice.*minted" "$n_fp1"

# fplay2: befriend + trade
echo "--- Bonds v2 befriend + trade (fplay2) ---"

n_fp2=$($DART run "$REPL" <<HEREDOC
$BONDS_V2
fplay2.
:quit
HEREDOC
2>&1)

check "Bonds v2 fplay2 succeeds" "succeeds" "$n_fp2"
check "Bonds v2 fplay2 connected" "tagged(alice.*connected(bob)" "$n_fp2"
check "Bonds v2 fplay2 trade_completed" "trade_completed" "$n_fp2"

# fplay3-6: trade variations
echo "--- Bonds v2 trade plays (fplay3-fplay6) ---"

for play_num in 3 4 5 6; do
    n_fpN=$($DART run "$REPL" <<HEREDOC
$BONDS_V2
fplay${play_num}.
:quit
HEREDOC
2>&1)
    check "Bonds v2 fplay${play_num} succeeds" "succeeds" "$n_fpN"
done

# fplay4b: time-dependent trade
echo "--- Bonds v2 time-dependent trade (fplay4b) ---"

n_fp4b=$($DART run "$REPL" <<HEREDOC
$BONDS_V2
fplay4b.
:quit
HEREDOC
2>&1)

check "Bonds v2 fplay4b succeeds" "succeeds" "$n_fp4b"

# fplay8-9: buyback + symmetric trade
echo "--- Bonds v2 buyback + symmetric (fplay8-fplay9) ---"

for play_num in 8 9; do
    n_fpN=$($DART run "$REPL" <<HEREDOC
$BONDS_V2
fplay${play_num}.
:quit
HEREDOC
2>&1)
    check "Bonds v2 fplay${play_num} succeeds" "succeeds" "$n_fpN"
done

# fplay10-11: escrow
echo "--- Bonds v2 escrow plays (fplay10-fplay11) ---"

n_fp10=$($DART run "$REPL" <<HEREDOC
$BONDS_V2
fplay10.
:quit
HEREDOC
2>&1)

check "Bonds v2 fplay10 succeeds" "succeeds" "$n_fp10"
check "Bonds v2 fplay10 escrow" "escrow" "$n_fp10"

n_fp11=$($DART run "$REPL" <<HEREDOC
$BONDS_V2
fplay11.
:quit
HEREDOC
2>&1)

check "Bonds v2 fplay11 succeeds" "succeeds" "$n_fp11"
check "Bonds v2 fplay11 escrow_cancelled" "escrow_cancelled" "$n_fp11"

# fplay12: village market (6 agents)
echo "--- Bonds v2 village market (fplay12) ---"

n_fp12=$($DART run "$REPL" <<HEREDOC
$BONDS_V2
:limit 5000000
fplay12.
:quit
HEREDOC
2>&1)

check "Bonds v2 fplay12 succeeds" "succeeds" "$n_fp12"
check "Bonds v2 fplay12 tagged output" "tagged(" "$n_fp12"

echo ""

# =============================================================================
# Section O: Bonds V2 Multi-Isolate Tests
# =============================================================================

section "O" "Bonds V2 Multi-Isolate Tests"
echo ""

BONDS_MAD_RESULT=$("$DART" test "$GLP_RUNTIME/test/multiagent/bonds_v2_isolate_test.dart" 2>&1)
BONDS_MAD_EXIT=$?

if [ $BONDS_MAD_EXIT -eq 0 ]; then
    BONDS_MAD_PASSED=$(echo "$BONDS_MAD_RESULT" | grep -oE '\+[0-9]+' | tail -1 | tr -d '+')
    BONDS_MAD_PASSED=${BONDS_MAD_PASSED:-12}
    echo "  PASS: All $BONDS_MAD_PASSED bonds_v2 multi-isolate tests passed"
    PASS=$((PASS + BONDS_MAD_PASSED))
else
    BONDS_MAD_FAILED=$(echo "$BONDS_MAD_RESULT" | grep -oE '\-[0-9]+' | tail -1 | tr -d '-')
    BONDS_MAD_FAILED=${BONDS_MAD_FAILED:-1}
    BONDS_MAD_PASSED=$(echo "$BONDS_MAD_RESULT" | grep -oE '\+[0-9]+' | tail -1 | tr -d '+')
    BONDS_MAD_PASSED=${BONDS_MAD_PASSED:-0}
    echo "  FAIL: $BONDS_MAD_FAILED bonds_v2 multi-isolate test(s) failed ($BONDS_MAD_PASSED passed)"
    echo "$BONDS_MAD_RESULT" | tail -20
    PASS=$((PASS + BONDS_MAD_PASSED))
    FAIL=$((FAIL + BONDS_MAD_FAILED))
fi

echo ""

# =============================================================================
# SECTION P: MODULE BOUNDARY ENFORCEMENT TESTS
# =============================================================================
section "P" "Module Boundary Enforcement Tests"
echo ""

echo "--- Module boundary: exported vs private ---"
output=$($DART run "$REPL" <<HEREDOC
$TYPED/test_module_boundary.glp
public_proc(5, X).
private_proc(5, X).
:quit
HEREDOC
2>&1)
check "public_proc(5,X) returns X=6" "X = 6" "$output"
check_not "private_proc not callable from REPL" "X = 7" "$output"
check "private_proc fails or not found" "not found\|failed\|Error" "$output"

echo ""

# =============================================================================
# SECTION Q: AOT REPL EXE REGRESSION SMOKE
# =============================================================================
# Regression coverage for the ch02-era path-resolution bug where the AOT-
# compiled REPL exe failed to load programs/self.glp because Platform.script
# resolution overshoots when the .exe lives one directory shallower than the
# .dart source. Delegated to test/run_aot_smoke.sh which builds a fresh exe
# and asserts ex-02 + ex-03 produce their locked bindings.
section "Q" "AOT REPL exe regression smoke"
echo ""
# Note: `|| true` and `set +e` guards needed because `grep -c` returns exit 1
# when count is 0, which would trip the parent script's `set -e`.
set +e
AOT_SMOKE_RESULT=$(bash "$SCRIPT_DIR/run_aot_smoke.sh" 2>&1)
AOT_SMOKE_EXIT=$?
AOT_SMOKE_PASSED=$(echo "$AOT_SMOKE_RESULT" | grep -c "^  PASS:" || true)
AOT_SMOKE_FAILED=$(echo "$AOT_SMOKE_RESULT" | grep -c "^  FAIL:" || true)
AOT_SMOKE_PASSED=${AOT_SMOKE_PASSED:-0}
AOT_SMOKE_FAILED=${AOT_SMOKE_FAILED:-0}
set -e
if [ "$AOT_SMOKE_EXIT" -eq 0 ]; then
    echo "  PASS: All $AOT_SMOKE_PASSED AOT smoke checks passed"
    PASS=$((PASS + AOT_SMOKE_PASSED))
else
    echo "  FAIL: $AOT_SMOKE_FAILED AOT smoke check(s) failed ($AOT_SMOKE_PASSED passed)"
    echo "$AOT_SMOKE_RESULT" | tail -20
    PASS=$((PASS + AOT_SMOKE_PASSED))
    FAIL=$((FAIL + AOT_SMOKE_FAILED))
fi
echo ""

# =============================================================================
# SECTION R: CH07 CLUSTER PROJECTS (TUTORIAL-MIRROR TESTS)
# =============================================================================
#
# **SUPERSEDED 2026-05-04** — This section was added by the prior ch07
# implementation (commit 26e01792, 2026-05-02) and tests the now-stale
# olamni/tutorial/ch07/{simple-multimodule,cssg-modules}/ subdirectory copies
# of programs/cssg_modules/. The current ch07 (v2026.05.04) uses the canonical
# project directly — no derivative copies. Section R's tests still pass (the
# subdirs are preserved on disk per the no-removal directive) but their value
# is purely historical: they test that the prior copies haven't drifted from
# canonical, which doesn't constrain the current chapter's correctness.
# Disposition pending: either delete these tests or repurpose them as direct
# canonical-load smoke tests. See olamni/tutorial/ch07/ch07_tutorial.md for
# the current chapter shape.
#
# Original prior-implementation comment block follows:
#
# Per spec FR-014 + Q-amendment Q-FR014a (Section letter R, NOT S):
# R-1: Cluster A simple-multimodule project loads via REPL project-loading mode
#      and runs each of plays 1-3 (per Q1+Q5+Q1a; cluster A's pruned boot.glp
#      retains plays 1-3 + fplay 1-3).
# R-2: Cluster B cssg-modules project files are byte-exact copies of canonical
#      programs/cssg_modules/ (per FR-003 + Q-FR003a). After stripping the 6-line
#      ch07 header from each tutorial-side file, diff against canonical MUST
#      return zero differences. The 6-line header structure is the contract per
#      specs/008-tutorial-ch07/contracts/glp-file-format.md.
# Spec amendment to test-mirror-format.md: the original awk heuristic counted
# ALL leading %% lines, which over-counted because canonical files also start
# with %% comments. The fixed-line-count `tail -n +7` approach is the contract
# in force; the header is exactly 6 lines for byte-exact files.
section "R" "ch07 cluster projects"
echo ""

# R-1: Cluster A simple-multimodule load + plays 1-3
echo "--- R-1: cluster A simple-multimodule load + plays ---"

CLUSTER_A_DIR="$(to_repl_path "$GLP_DIR/olamni/tutorial/ch07/simple-multimodule")"

output=$($DART run "$REPL" <<HEREDOC
$CLUSTER_A_DIR
:quit
HEREDOC
2>&1)
check "cluster A loads via project mode" "Loaded project" "$output"

for play in play1 play2 play3; do
    output=$($DART run "$REPL" <<HEREDOC
$CLUSTER_A_DIR
:limit 1000000
$play.
:quit
HEREDOC
    2>&1)
    check "cluster A $play succeeds or suspended" "succeeds\|suspended" "$output"
done

# R-2: Cluster B byte-equivalence diff (after stripping 6-line ch07 header)
echo "--- R-2: cluster B byte-equivalence to canonical ---"

CSSG_CANONICAL="$GLP_DIR/programs/cssg_modules"
CLUSTER_B_DIR="$GLP_DIR/olamni/tutorial/ch07/cssg-modules"

for f in self.glp agent.glp ui/mediator.glp ui/actors.glp boot.glp mad_boot.glp; do
    CANONICAL="$CSSG_CANONICAL/$f"
    TUTORIAL="$CLUSTER_B_DIR/$f"
    # Strip the 6-line ch07 header per glp-file-format.md contract.
    if diff <(tail -n +7 "$TUTORIAL") "$CANONICAL" > /dev/null 2>&1; then
        check "byte-equivalent: $f" "ok" "ok"
    else
        DIFF_OUT=$(diff <(tail -n +7 "$TUTORIAL") "$CANONICAL" | head -5)
        check "byte-equivalent: $f" "ok" "DRIFT in $f: $DIFF_OUT"
    fi
done

echo ""

# =============================================================================
# SECTION I: US5 CROSS-RUNTIME DISTRIBUTED SUITE (Gleam × C#; feature 060
# T047/FR-030 — results report alongside the other sections). Needs gleam on
# PATH + the built C# REPL; absent either, an EXPLICIT skip (never silent).
# =============================================================================
section "I" "Cross-runtime Gleam × C# link suite (US5)"
# SCRIPT_DIR-anchored: the suite cd'd into glp_runtime/ at the top, so relative
# repo-root paths do not resolve here.
# --- 078 T047: establish build freshness ONCE, before ANY section that uses glp_repl.exe ---
# Sections I (cross-runtime), T (064 service-box drills) and U (077 cyclic diagnostics) all run
# this same binary. Gating only Section U would let the other two keep presenting the output of a
# stale build as authoritative — which is precisely the 2026-08-13 failure mode.
GLPREPL_EXE="$SCRIPT_DIR/../out/csharp/glp_repl/bin/Debug/net10.0/glp_repl.exe"
GLPREPL_STALE=0
GLPREPL_STALE_WHY=""
if [ -f "$GLPREPL_EXE" ]; then
    _b=$(newest_mtime "$GLPREPL_EXE") || _b=""
    _s=$(newest_src_mtime \
            "$SCRIPT_DIR/../out/csharp/glp_repl" \
            "$SCRIPT_DIR/../out/csharp/lib" \
            "$SCRIPT_DIR/../csharp/glp_link" \
            "$SCRIPT_DIR/../csharp/glp_crdtmsg") || _s=""
    if [ -z "$_b" ] || [ -z "$_s" ]; then
        GLPREPL_STALE=1
        GLPREPL_STALE_WHY="could not establish build freshness (bin='${_b:-unreadable}' src='${_s:-unreadable}')"
    elif mtime_ge "$_s" "$_b"; then
        GLPREPL_STALE=1
        GLPREPL_STALE_WHY="glp_repl.exe is NOT NEWER than its source (bin $(date -d @"${_b%%.*}" '+%Y-%m-%d %H:%M' 2>/dev/null || echo "$_b") src $(date -d @"${_s%%.*}" '+%Y-%m-%d %H:%M' 2>/dev/null || echo "$_s")) — rebuild with 'dotnet build' before trusting it"
    fi
fi

CSREPL_BIN="$SCRIPT_DIR/../out/csharp/glp_repl/bin/Debug/net10.0/glp_repl.exe"
if [ "$GLPREPL_STALE" -eq 1 ] && [ -f "$CSREPL_BIN" ]; then
    unsearchable "Section I (cross-runtime Gleam x C# link suite)" "$GLPREPL_STALE_WHY"
elif command -v gleam >/dev/null 2>&1 && [ -f "$CSREPL_BIN" ]; then
    if bash "$SCRIPT_DIR/parity/cross_runtime/run_all.sh"; then
        check "US5 cross-runtime suite (Gleam × C#)" "ok" "ok"
    else
        check "US5 cross-runtime suite (Gleam × C#)" "ok" "FAILED (scenario detail above)"
    fi
else
    skip "Section I (cross-runtime Gleam x C# link suite)" "needs gleam on PATH + built C# REPL ($CSREPL_BIN)"
fi

echo ""

# =============================================================================
# SECTION S: MS_MESSAGE DURABLE MESH MESSAGING (063 US2 — SC-004 DRILL GATE)
# =============================================================================
# The SC-004 disconnect drill (N=1000: recipient offline, originator restart,
# exactly-once in-order delivery, zero re-observations on recipient restart)
# is the acceptance gate for the durable first-hop mesh-messaging tool
# (specs/063-wave-5-consolidated-captured-triad, T024). Standalone gate:
#   ms_message/.venv/Scripts/python ms_message/tests/drill_disconnect.py 1000
# Runs only where the ms_message venv exists (Windows/glpnet host); an absent
# venv prints an EXPLICIT skip line — never a silent pass.
section "S" "ms_message durable mesh messaging (SC-004 drill)"

MSMSG_PY="$GLP_DIR/ms_message/.venv/Scripts/python.exe"
[ -f "$MSMSG_PY" ] || MSMSG_PY="$GLP_DIR/ms_message/.venv/bin/python"
if [ -f "$MSMSG_PY" ]; then
    output=$("$MSMSG_PY" "$GLP_DIR/ms_message/tests/drill_disconnect.py" 1000 2>&1)
    check "S-1: SC-004 disconnect drill N=1000 (exactly-once, in order)" "=> PASS" "$output"
    output=$("$MSMSG_PY" -m pytest "$GLP_DIR/ms_message/tests" -q 2>&1)
    check "S-2: ms_message unit suite green" " passed" "$output"
else
    skip "Section S (ms_message durable mesh)" "ms_message venv absent — standalone gate: ms_message/tests/drill_disconnect.py (see section header)"
fi

echo ""

# =============================================================================
# SECTION T: 064 DURABLE LISTENER SERVICE BOX DRILLS (T012)
# =============================================================================
# The US1 restart drill (register + relaunch + peer-connect, missing-program
# diagnostic, SC-005 transcript identity) and the US2 history drill (durable
# append before observation, replay in receipt order exactly once, idempotent
# second restart) from specs/064-durable-listener-service-box. Both need the
# built C# REPL + DOTNET_ROOT + QUIC trust material; absent the exe, an
# EXPLICIT skip line names the standalone gates — never a silent pass.
# The history drill runs at its default N (SC-002's N=100 is the release gate,
# run standalone: bash test/service_box/history_drill.sh 100).
section "T" "064 service-box drills (resume + history)"
# set +e for this whole section (same guard Section U and the cross-runtime sections use):
# the drills `exit $FAIL` (non-zero) when QUIC trust material (glpquick.pfx) is absent on a
# host, and `output=$(bash drill.sh)` under the script's top-level `set -e` would then ABORT
# the entire suite — killing every later section instead of recording a section FAIL. check()
# never exits, so guarding here turns a host-specific drill failure into a normal FAIL line.
set +e
SBREPL_BIN="$SCRIPT_DIR/../out/csharp/glp_repl/bin/Debug/net10.0/glp_repl.exe"
if [ "$GLPREPL_STALE" -eq 1 ] && [ -f "$SBREPL_BIN" ]; then
    unsearchable "Section T (064 service-box drills)" "$GLPREPL_STALE_WHY"
elif [ -f "$SBREPL_BIN" ]; then
    output=$(bash "$SCRIPT_DIR/service_box/resume_drill.sh" 2>&1)
    check "T-1: US1 resume drill (auto-arm, diagnostics, SC-005 transcript)" "resume drill: PASS=7 FAIL=0" "$output"
    output=$(bash "$SCRIPT_DIR/service_box/history_drill.sh" 2>&1)
    check "T-2: US2 history drill (order, exactly-once, idempotent restart)" "history drill: PASS=4 FAIL=0" "$output"
else
    skip "Section T (064 service-box drills)" "C# REPL not built ($SBREPL_BIN) — standalone gates: test/service_box/resume_drill.sh + test/service_box/history_drill.sh"
fi
set -e

echo ""

# =============================================================================
# SECTION U: GUARDED TERM-TRAVERSAL — CYCLIC-TERM COMPILER DIAGNOSTICS (077)
# =============================================================================
# Feature 077 turns the F-069-1 crash class (a cyclic Term overflowing the C#
# compiler's substitution/resolve walkers with an uncatchable StackOverflow)
# into a catchable CompileError diagnostic (FR-004). These load cyclic and
# acyclic GLP programs into the C# REPL and assert: cyclic => diagnostic + NO
# stack overflow + clean exit (SC-001/SC-002); deep-acyclic + DAG => load OK,
# never falsely rejected (SC-006/FR-006). The structural-family guard is
# additionally unit-probed (out/csharp/term_traversal_probe) since a cyclic
# AST node reaching codegen can only be built programmatically, not authored.
section "U" "Guarded term-traversal cyclic diagnostics (077)"
# set +e for this whole section: the C# REPL exits non-zero on some diagnostics and
# `grep`/`grep -q` return 1 on no-match — under the script's set -e that would abort
# the suite (same hazard the cross-runtime section guards above). check() never exits.
set +e
CSREPL_BIN="$SCRIPT_DIR/../out/csharp/glp_repl/bin/Debug/net10.0/glp_repl.exe"
# GLP_DIR is already a Windows (cygpath -m) path; the C# REPL is a native exe and
# CANNOT open MSYS-mount paths like /d/foo, so pass it the Windows form.
CYCLIC_DIR="$GLP_DIR/programs/tests/cyclic"
# Section U reuses the ONE freshness fact established before Section I — measuring it twice
# invites the two answers to disagree.
CSREPL_STALE=$GLPREPL_STALE
if [ ! -f "$CSREPL_BIN" ]; then
    # B2: previously BOTH branches required the file to exist, so an absent binary made the whole
    # section disappear from the accounting entirely — the exact "not-run reads as a pass" hole.
    skip "Section U (077 cyclic diagnostics)" "built C# REPL not found ($CSREPL_BIN)"
elif [ "$CSREPL_STALE" -eq 1 ]; then
    unsearchable "Section U (077 cyclic diagnostics)" "$GLPREPL_STALE_WHY"
fi
if [ -f "$CSREPL_BIN" ] && [ "$CSREPL_STALE" -eq 0 ]; then
    # U-1/U-2: every cyclic-= program (the class 069 DEC F3 had to exclude) must
    # compile to a catchable Cyclic-term diagnostic with NO StackOverflow (SC-001/
    # SC-002, T024). Glob POSIX-side; pass the Windows path to the native REPL.
    for prog in "$SCRIPT_DIR"/../programs/tests/cyclic/cyclic_*.glp; do
        [ -f "$prog" ] || continue
        name=$(basename "$prog")
        out=$(printf 'load %s\n:quit\n' "$CYCLIC_DIR/$name" | timeout 60 "$CSREPL_BIN" 2>&1)
        check "U-1 [$name]: compiles to a Cyclic-term diagnostic (SC-002)" "Cyclic term detected" "$out"
        if echo "$out" | grep -q "Stack overflow"; then
            check "U-2 [$name]: raises NO StackOverflow (SC-001)" "no-overflow" "STACK-OVERFLOW-PRESENT"
        else
            check "U-2 [$name]: raises NO StackOverflow (SC-001)" "no-overflow" "no-overflow"
        fi
    done

    # U-3: acyclic fixtures MUST still load — not falsely rejected (SC-006 / FR-006).
    # deep_acyclic covers the body-phase partial-list-in-struct shape ([h|Var] nested in a
    # struct) that the 077 codexreview found the codegen structural guard falsely rejecting;
    # dag_shared covers a shared (DAG) subterm. A MISSING fixture FAILS LOUD — a silent skip
    # is exactly what let that codegen false-positive go undetected (codexreview 077).
    for acy in deep_acyclic dag_shared; do
        if [ -f "$CYCLIC_DIR/$acy.glp" ]; then
            out=$(printf 'load %s\n:quit\n' "$CYCLIC_DIR/$acy.glp" | timeout 60 "$CSREPL_BIN" 2>&1)
            if echo "$out" | grep -qE "Cyclic term detected|Stack overflow|Error loading"; then
                check "U-3 [$acy]: acyclic loads, not falsely rejected (SC-006)" "loads-ok" "FALSELY-REJECTED"
            else
                check "U-3 [$acy]: acyclic loads, not falsely rejected (SC-006)" "loads-ok" "loads-ok"
            fi
        else
            check "U-3 [$acy]: fixture present (SC-006 coverage)" "present" "MISSING-FIXTURE"
        fi
    done

    # U-4: structural + REAL-walker guard probe — a programmatically cyclic AST node =>
    # catchable CompileError; deep/DAG acyclic terms traverse OK (SC-001/SC-003/SC-006).
    # A cyclic AST node can only be built programmatically (not authored in GLP source), so
    # this probe (InternalsVisibleTo) is the ONLY positive cycle-DETECTION assertion. The
    # C# REPL is built (we are inside its guard); the probe ships in the SAME solution, so a
    # MISSING probe FAILS LOUD — it does NOT silently skip, which would leave the
    # cycle-detection guarantee ungated (codexreview 077).
    PROBE="$SCRIPT_DIR/../out/csharp/term_traversal_probe/bin/Debug/net10.0/term_traversal_probe.exe"
    if [ -f "$PROBE" ]; then
        out=$("$PROBE" 2>&1)
        check "U-4: structural + real-walker guard probe (SC-001/SC-003/SC-006)" "PROBE OK" "$out"
    else
        check "U-4: probe present (SC-001/SC-003 cycle-detection coverage)" "present" "MISSING-PROBE"
    fi
fi
set -e

echo ""

# =============================================================================
# SUMMARY
# =============================================================================
TOTAL=$((PASS + FAIL))
NOTRUN=$((SKIP + UNSEARCHABLE))

echo "======================================"
echo "Total: $TOTAL | Passed: $PASS | Failed: $FAIL | Skipped: $SKIP | Unsearchable: $UNSEARCHABLE"
echo "======================================"

# 078 FR: never let a not-run check be read as a passing one. The counts above are of checks that
# ACTUALLY RAN; anything that could not run is enumerated here rather than silently absent.
if [ $NOTRUN -gt 0 ]; then
    echo ""
    echo "$NOTRUN check group(s) DID NOT RUN — these are NOT passes:$SKIP_LOG"
    echo ""
fi

# The exit STATUS is what a merge gate actually consumes. Printing "these groups did not run" and
# then returning 0 tells the truth on stdout and a lie to the caller (codexreview A1), which is the
# very defect 078 exists to eliminate. Incomplete runs get their own non-zero code so they stay
# distinguishable from real failures.
if [ $FAIL -eq 0 ] && [ $NOTRUN -eq 0 ]; then
    echo "ALL TESTS PASSED!"
    exit 0
elif [ $FAIL -eq 0 ]; then
    echo "INCOMPLETE — all $TOTAL executed checks passed but $NOTRUN group(s) did not run (exit 2)"
    exit 2
else
    echo "SOME TESTS FAILED"
    exit 1
fi
