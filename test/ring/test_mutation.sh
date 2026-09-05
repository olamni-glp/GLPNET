#!/usr/bin/env bash
# test/ring/test_mutation.sh — guards C6 of
# specs/101-gleam-capability-delivery/contracts/ring-delivery.md (feature 101, T010).
#
# T010 test_weakened_guard_turns_suite_red — SC-003.
#
# The rule being tested is not about the code, it is about the SUITE: if you replace a
# ring-placement guard with a no-op, the acceptance suite must go RED. A mutation test
# that stays green under a no-op validator is the exact inverse of the evidence required
# — it certifies that the suite cannot detect the thing it exists to detect.
#
# This repo has shipped that shape before. Wave-22's review found a mutation test that
# stayed GREEN under a no-op validator, in a feature whose whole subject was verification.
# So this file mutates for real: it neuters a guard on disk, runs the suite, restores the
# original from a saved copy, and asserts the run in between was red.
#
# Safety: the original is copied before mutation and restored on EVERY exit path
# (EXIT/INT/TERM). If this script is killed with -9 the mutated file survives — the
# restore check at the end names the backup so it can be put back by hand.
#
# Run: bash test/ring/test_mutation.sh

set -u
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
. "$SCRIPT_DIR/lib/harness.sh"
RING_SUITE="test_mutation"

TMP="$(mktemp -d 2>/dev/null || echo "${TMPDIR:-/tmp}/ring_mut_$$")"
mkdir -p "$TMP"

# The guard under mutation and the suite that must notice.
TARGET="$SCRIPT_DIR/admit.sh"            # the C2 ring-placement guard (T020)
SUITE="$SCRIPT_DIR/test_contract_purity.sh"
BACKUP="$TMP/admit.sh.orig"

restore() {
    if [ -f "$BACKUP" ]; then
        cp -f "$BACKUP" "$TARGET" 2>/dev/null \
            && echo "  (restored $TARGET from backup)" \
            || echo "  !! COULD NOT RESTORE $TARGET — original is at $BACKUP" >&2
    fi
}
trap 'restore; rm -rf "$TMP"' EXIT INT TERM

echo "== $RING_SUITE =="

test_weakened_guard_turns_suite_red() {
    local name="test_weakened_guard_turns_suite_red"

    if [ ! -f "$TARGET" ]; then
        pending "$name" "the ring-placement guard (test/ring/admit.sh) lands at T020; there is nothing to weaken yet"
        return 0
    fi

    # Baseline: the suite must be GREEN before mutating, or a red result afterwards
    # proves nothing — it could have been red all along.
    local base_rc
    bash "$SUITE" >/dev/null 2>&1; base_rc=$?
    if [ "$base_rc" -ne 0 ]; then
        fail "$name" "baseline suite is already non-green (rc=$base_rc) — a mutation cannot be attributed until the baseline passes. Fix the suite first."
        return 0
    fi

    # Mutate: replace the guard with a no-op that admits everything.
    cp -f "$TARGET" "$BACKUP"
    cat > "$TARGET" <<'NOOP'
#!/usr/bin/env bash
# MUTANT — installed by test/ring/test_mutation.sh. Admits everything, unconditionally.
# If you are reading this in a committed tree, a mutation run was killed before restoring.
exit 0
NOOP

    local mut_rc
    bash "$SUITE" >/dev/null 2>&1; mut_rc=$?

    restore
    rm -f "$BACKUP"

    if [ "$mut_rc" -eq 0 ]; then
        fail "$name" "the suite stayed GREEN with the admission guard replaced by \`exit 0\` — the suite cannot detect the thing it exists to detect (SC-003)"
    else
        pass "$name"
    fi
}

# ---------------------------------------------------------------------------
# Guard on the mutation harness itself: prove the restore works. If restore is broken,
# every future mutation run silently leaves a neutered guard behind and the suite is
# permanently blind. Cheap to check, catastrophic to miss.
# ---------------------------------------------------------------------------
test_restore_is_faithful() {
    local name="test_restore_is_faithful"
    local probe="$TMP/probe.txt" probe_backup="$TMP/probe.orig"
    printf 'original-content\n' > "$probe"
    cp -f "$probe" "$probe_backup"
    printf 'MUTANT\n' > "$probe"
    cp -f "$probe_backup" "$probe"
    if [ "$(cat "$probe")" = "original-content" ]; then
        pass "$name"
    else
        fail "$name" "copy-restore does not round-trip on this filesystem; mutation runs would leave mutants behind"
    fi
}

test_restore_is_faithful
test_weakened_guard_turns_suite_red

ring_summary
exit $?
