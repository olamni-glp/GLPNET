#!/usr/bin/env bash
# test/ring/test_retention.sh — guards FR-005 (feature 101, T022).
#
# T022 test_no_dart_or_corpus_leaves_glpnet
#
# Found by the 2026-09-03 analyze pass: FR-005 was the ONE requirement in this feature
# with no task against it. That is not a coincidence — it is a NEGATIVE requirement, and
# negative requirements are the ones that go unguarded, because nothing fails when they
# are silently violated. Everything still builds; everything still passes; the delivered
# set has just quietly grown a copy of the Dart reference.
#
# glpnet's delivery mode is RESYNTHESIS, NEVER COPY. The delivered set is the Gleam
# contract plus its per-ring realizations. `glp_runtime/` (the Dart reference),
# `glp_multiagent/` (the Flutter app) and `programs/` (the .glp corpus) are RETAINED here
# and delivered nowhere. They stay as the oracle the rings are measured against.
#
# Run: bash test/ring/test_retention.sh

set -u
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
. "$SCRIPT_DIR/lib/harness.sh"
RING_SUITE="test_retention"

echo "== $RING_SUITE =="

# The manifest of what is actually delivered. Until T013/T014/T018 produce one, this
# guard is pending — and pending is not a pass.
MANIFEST="$SCRIPT_DIR/delivery-manifest.txt"

# Trees that are retained in glpnet and must never appear in a delivery manifest.
FORBIDDEN_PREFIXES="glp_runtime/ glp_multiagent/ programs/"

test_no_dart_or_corpus_leaves_glpnet() {
    local name="test_no_dart_or_corpus_leaves_glpnet"

    if [ ! -f "$MANIFEST" ]; then
        pending "$name" "delivery manifest ($MANIFEST) is produced by T013/T014/T018; there is no delivered set to check yet"
        return 0
    fi

    local offenders=""
    while IFS= read -r line; do
        line="${line%$'\r'}"                    # CRLF-tolerant (059 T051 root cause)
        case "$line" in ''|'#'*) continue ;; esac
        for p in $FORBIDDEN_PREFIXES; do
            case "$line" in
                "$p"*) offenders="$offenders$line " ;;
            esac
        done
    done < "$MANIFEST"

    if [ -n "$offenders" ]; then
        fail "$name" "the delivery set contains retained-only files — glpnet resynthesizes, it does not copy (FR-005): $offenders"
    else
        pass "$name"
    fi
}

# ---------------------------------------------------------------------------
# The control. An EMPTY manifest trivially contains no forbidden file, so the check
# above would score green on a manifest that delivers nothing at all. Assert the
# manifest actually carries the Gleam delivery, or the guard is vacuous.
# ---------------------------------------------------------------------------
test_manifest_is_not_vacuously_empty() {
    local name="test_manifest_is_not_vacuously_empty"

    if [ ! -f "$MANIFEST" ]; then
        pending "$name" "delivery manifest is produced by T013/T014/T018"
        return 0
    fi

    local delivered
    delivered="$(grep -c '^glp_gleam/' "$MANIFEST" 2>/dev/null || echo 0)"
    if [ "${delivered:-0}" -gt 0 ]; then
        pass "$name"
    else
        fail "$name" "the manifest names no glp_gleam/ file — an empty delivery set passes the FR-005 check for the wrong reason"
    fi
}

# ---------------------------------------------------------------------------
# The retained trees must still BE here. FR-005 is retention, not deletion: if a future
# change removes glp_runtime/ or programs/, the parity oracle this whole feature measures
# against is gone, and 206/206 would then be a statement about nothing.
# ---------------------------------------------------------------------------
test_retained_trees_are_still_present() {
    local name="test_retained_trees_are_still_present"
    local missing=""
    for d in glp_runtime glp_multiagent programs; do
        [ -d "$REPO_ROOT/$d" ] || missing="$missing $d"
    done
    if [ -n "$missing" ]; then
        fail "$name" "retained tree(s) missing from glpnet:$missing — the Dart/corpus oracle the rings are measured against is gone"
    else
        pass "$name"
    fi
}

test_no_dart_or_corpus_leaves_glpnet
test_manifest_is_not_vacuously_empty
test_retained_trees_are_still_present

ring_summary
exit $?
