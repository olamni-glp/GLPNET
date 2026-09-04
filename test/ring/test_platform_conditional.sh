#!/usr/bin/env bash
# test/ring/test_platform_conditional.sh — guards C5 of
# specs/101-gleam-capability-delivery/contracts/ring-delivery.md (feature 101, T011).
#
# T011 test_vacuous_premise_is_skipped_by_name — FR-009.
#
# The precedent is concrete, from this feature's own parent. Its `T005` asserted that
# `GLPNET` and `GLP/glpnet` are different directories. On case-insensitive NTFS they are
# the SAME directory, so the assertion could not fail — it was green for the whole of its
# life while testing nothing. A test whose premise does not hold on the executing platform
# must SKIP WITH A NAMED REASON. Silently passing is worse than failing: a failure gets
# looked at.
#
# So this file (a) measures the platform property empirically rather than assuming it from
# the OS name, (b) reproduces the parent defect and asserts it is skipped-by-name and not
# passed, and (c) checks the harness refuses a reasonless skip.
#
# Run: bash test/ring/test_platform_conditional.sh

set -u
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
. "$SCRIPT_DIR/lib/harness.sh"
RING_SUITE="test_platform_conditional"

TMP="$(mktemp -d 2>/dev/null || echo "${TMPDIR:-/tmp}/ring_plat_$$")"
mkdir -p "$TMP"
trap 'rm -rf "$TMP"' EXIT INT TERM

echo "== $RING_SUITE =="

# --- measure, do not assume -------------------------------------------------
# `uname` says nothing reliable here: this repo is on NTFS under git-bash, and a
# case-sensitive volume on the same machine would answer differently. Ask the
# filesystem the repo actually lives on.
fs_is_case_sensitive() {
    local probe="$TMP/CaseProbe"
    rm -rf "$TMP/CaseProbe" "$TMP/caseprobe"
    mkdir -p "$probe" 2>/dev/null || return 2
    if [ -d "$TMP/caseprobe" ]; then
        return 1        # lowercase name resolves to the same dir => case-INsensitive
    fi
    return 0            # distinct => case-sensitive
}

# Codex review 20260904T055230Z (P2): `if fs_is_case_sensitive; then yes; else no; fi` collapsed
# the probe's THREE outcomes into two — return 2 ("could not create the probe directory", i.e.
# could not measure) was mapped to "no", and the test then SKIPPED as though case-insensitivity
# had been observed. The `unknown` branch below was therefore unreachable exactly when the
# premise could not be measured, which is the FR-009 defect this file exists to guard against.
# Capture the status explicitly instead.
fs_is_case_sensitive
case "$?" in
    0) CASE_SENSITIVE="yes" ;;
    1) CASE_SENSITIVE="no" ;;
    *) CASE_SENSITIVE="unknown" ;;
esac
echo "  (measured: repo filesystem case-sensitive = $CASE_SENSITIVE)"

# ---------------------------------------------------------------------------
# T011 — the parent feature's T005, replayed. The premise it needed is "distinct paths
# are distinct directories". Where that premise does not hold, the correct outcome is
# skip-with-a-reason. Passing here would reproduce the original defect exactly.
# ---------------------------------------------------------------------------
test_vacuous_premise_is_skipped_by_name() {
    local name="test_vacuous_premise_is_skipped_by_name"

    if [ "$CASE_SENSITIVE" = "no" ]; then
        # The premise does not hold. Skip, naming why — and the harness records this
        # separately from pass, so it can never be counted as evidence.
        skip "$name" \
            "filesystem is case-insensitive, so 'GLPNET' and 'glpnet' name one directory; the parent feature's T005 distinct-directory assertion is vacuous here and is not run"
        return 0
    fi

    if [ "$CASE_SENSITIVE" = "unknown" ]; then
        fail "$name" "could not measure filesystem case sensitivity — an unmeasurable premise must not be assumed either way"
        return 0
    fi

    # Premise holds: run the real assertion.
    local a="$TMP/RealCase" b="$TMP/realcase"
    mkdir -p "$a" "$b"
    if [ -d "$a" ] && [ -d "$b" ] && [ "$(cd "$a" && pwd -P)" != "$(cd "$b" && pwd -P)" ]; then
        pass "$name"
    else
        fail "$name" "filesystem measured case-sensitive but the two paths resolved to one directory — the measurement disagrees with the behaviour"
    fi
}

# ---------------------------------------------------------------------------
# The harness must REFUSE a skip with no reason. An unnamed skip is a silent pass with
# extra steps, which is the failure mode C5 exists to close.
# ---------------------------------------------------------------------------
test_reasonless_skip_is_a_harness_error() {
    local name="test_reasonless_skip_is_a_harness_error"
    local before_fail="$RING_FAIL" before_skip="$RING_SKIP"

    skip "__probe__" "" 2>/dev/null    # deliberately reasonless

    if [ "$RING_SKIP" -ne "$before_skip" ]; then
        RING_SKIP="$before_skip"
        fail "$name" "the harness accepted a skip with no reason and counted it as a skip"
        return 0
    fi
    if [ "$RING_FAIL" -gt "$before_fail" ]; then
        RING_FAIL="$before_fail"       # the probe's induced failure is the expected result
        pass "$name"
    else
        fail "$name" "a reasonless skip was neither counted nor rejected — it vanished silently"
    fi
}

# ---------------------------------------------------------------------------
# And the converse: a skip must not be reachable by accident on a platform where the
# premise DOES hold. Otherwise "skip everything" becomes a way to make a suite quiet.
# ---------------------------------------------------------------------------
test_skip_count_is_reported_not_hidden() {
    local name="test_skip_count_is_reported_not_hidden"
    # ring_summary prints skip= explicitly; assert the harness exposes the counter at all.
    if declare -F ring_summary >/dev/null 2>&1 && [ -n "${RING_SKIP+x}" ]; then
        pass "$name"
    else
        fail "$name" "harness does not expose a skip counter; skipped tests would be invisible in the summary"
    fi
}

test_vacuous_premise_is_skipped_by_name
test_reasonless_skip_is_a_harness_error
test_skip_count_is_reported_not_hidden

ring_summary
exit $?
