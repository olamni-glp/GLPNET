#!/usr/bin/env bash
# test/ring/test_list_single_source.sh — Principle VIII guard for the AtomVM construct list
# (feature 101, analyze finding A1, 2026-09-04).
#
# THE DEFECT THIS EXISTS FOR. The list of constructs measured as outside AtomVM's subset lives
# in two places:
#
#   test/ring/atomvm-unsupported.list   — read by the build gate (check_atomvm_subset.sh)
#   glp/ring/atomvm.gleam::unsupported() — the ring's own declaration, in Gleam
#
# The module's doc comment claimed it "documents rather than duplicates" the file. It does not:
# it is a second copy. Nothing failed if they diverged — the gate would go on enforcing the
# file while the ring advertised something else, and the mismatch would be invisible because
# both halves individually look fine. Constitution Principle VIII (Single Source of Truth)
# names exactly this shape.
#
# The honest fix would be to generate one from the other. Gleam has no build-time file read,
# so instead the duplication is made LOUD: this guard fails the moment the two disagree. A
# duplicate that is checked is survivable; a duplicate that is merely promised not to drift is
# the thing that drifts.
#
# Run: bash test/ring/test_list_single_source.sh

set -u
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
. "$SCRIPT_DIR/lib/harness.sh"
RING_SUITE="test_list_single_source"

LIST="$SCRIPT_DIR/atomvm-unsupported.list"
RING="$REPO_ROOT/glp_gleam/src/glp/ring/atomvm.gleam"

echo "== $RING_SUITE =="

# Constructs from the file: first tab-separated field of every non-comment line.
list_from_file() {
    grep -vE '^[[:space:]]*(#|$)' "$LIST" 2>/dev/null \
        | cut -f1 | tr -d '\r' | sed 's/[[:space:]]*$//' | sort -u
}

# Constructs from the Gleam module: the string literals inside unsupported()'s list body.
# Bounded to that function so an unrelated literal elsewhere in the module cannot leak in.
list_from_ring() {
    awk '/pub fn unsupported\(\)/{inside=1} inside{print} inside&&/^}/{exit}' "$RING" 2>/dev/null \
        | grep -oE '"[^"]+"' | tr -d '"' | sort -u
}

test_both_sources_exist() {
    local name="test_both_sources_exist"
    local missing=""
    [ -f "$LIST" ] || missing="$missing $LIST"
    [ -f "$RING" ] || missing="$missing $RING"
    if [ -n "$missing" ]; then
        fail "$name" "missing:$missing"
    else
        pass "$name"
    fi
}

test_list_and_ring_agree() {
    local name="test_list_and_ring_agree"
    if [ ! -f "$LIST" ] || [ ! -f "$RING" ]; then
        pending "$name" "both the list and the ring module must exist (T017/T018)"
        return 0
    fi

    local a b only_file only_ring
    a="$(list_from_file)"
    b="$(list_from_ring)"

    if [ -z "$a" ]; then
        fail "$name" "the file lists no constructs — an empty list makes every gate run pass, indistinguishably from a real pass"
        return 0
    fi
    if [ -z "$b" ]; then
        fail "$name" "the ring module declares no constructs — extraction found nothing inside unsupported(), so this guard would be vacuous"
        return 0
    fi

    only_file="$(comm -23 <(printf '%s\n' "$a") <(printf '%s\n' "$b") | tr '\n' ' ')"
    only_ring="$(comm -13 <(printf '%s\n' "$a") <(printf '%s\n' "$b") | tr '\n' ' ')"

    if [ -n "$only_file$only_ring" ]; then
        fail "$name" "the two copies of the construct list have DRIFTED — only in atomvm-unsupported.list: [${only_file:-none}] · only in glp/ring/atomvm.gleam: [${only_ring:-none}]. The gate enforces the file; the ring advertises its own copy. Reconcile them (Principle VIII)."
    else
        pass "$name"
    fi
}

# The guard must be able to notice a difference. If comm/awk extraction silently returned the
# same thing regardless of input, the check above would pass forever.
test_drift_would_be_detected() {
    local name="test_drift_would_be_detected"
    local tmp; tmp="$(mktemp 2>/dev/null || echo "${TMPDIR:-/tmp}/drift_$$")"
    printf 'alpha\nbeta\n' > "$tmp.a"
    printf 'alpha\n'        > "$tmp.b"
    local d
    d="$(comm -23 "$tmp.a" "$tmp.b" | tr '\n' ' ')"
    rm -f "$tmp" "$tmp.a" "$tmp.b"
    case "$d" in
        *beta*) pass "$name" ;;
        *) fail "$name" "set-difference does not work in this shell; the drift check above cannot detect anything" ;;
    esac
}

test_both_sources_exist
test_drift_would_be_detected
test_list_and_ring_agree

ring_summary
exit $?
