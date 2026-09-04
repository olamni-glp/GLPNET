#!/usr/bin/env bash
# test/ring/admit.sh — C2 admission (feature 101, T020 · FR-002).
#
# Admission is by MEASURED EVIDENCE, never by a name (008 FR-018).
#
# TWO DIFFERENT QUESTIONS, and conflating them is how the first version of this script
# got it wrong. It asked "does the subtree import the contract surface?" — but the
# surface was MEASURED FROM glp_gleam, so glp_gleam matched 62 of its own modules and
# admitted itself. A predicate that a subtree satisfies by being itself is not a test.
#
#   --to l0     Is this subtree part of the runtime-free polyglot L0 layer?
#               Requires BOTH:
#                 (a) it provides the L0 service set — kv/, mailbox/, network/
#                     (LATTICE line 35), and
#                 (b) it is runtime-free — L0 admits zero third-party runtime deps.
#
#   --to <ring> Is this subtree a realization held to the L0 contract, for one ring?
#               Requires measured consumption of the contract surface by a subtree that
#               is not itself the contract.
#
# The SC-005 case: `glp_gleam` offered to L0 on the strength of the word "Gleam". It
# fails BOTH L0 conditions — it carries none of kv/mailbox/network (it is a GLP language
# runtime plus ZeroMQ/TCP transports), and 29 of its 100 modules depend on the BEAM. It
# must be refused, with the name quoted.
#
# Usage:
#   admit.sh --subtree <path-or-name> [--to l0|beam|atomvm] [--justification <text>]
#
# Exit: 0 admitted · 1 refused (quoting the name) · 2 cannot decide.

set -u
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
SURFACE="$SCRIPT_DIR/contract-surface.list"
ANALYSIS="$SCRIPT_DIR/T012-import-analysis.json"
ANALYZER="$SCRIPT_DIR/analyze_imports.py"
MEASURED_ON="unknown"

SUBTREE=""; TARGET="l0"; JUSTIFICATION=""
while [ $# -gt 0 ]; do
    case "$1" in
        --subtree)       SUBTREE="${2:-}"; shift 2 ;;
        --to|--ring)     TARGET="${2:-}"; shift 2 ;;
        --justification) JUSTIFICATION="${2:-}"; shift 2 ;;
        *) echo "admit: unknown argument '$1'" >&2; exit 2 ;;
    esac
done

[ -n "$SUBTREE" ] || { echo "admit: --subtree is required" >&2; exit 2; }

DIR=""
for cand in "$SUBTREE" "$REPO_ROOT/$SUBTREE"; do
    [ -d "$cand" ] && { DIR="$cand"; break; }
done
if [ -z "$DIR" ]; then
    echo "REFUSED: '$SUBTREE'"
    echo "  reason: no such subtree in this repo — nothing to measure."
    exit 1
fi

echo "admission request"
echo "  subtree:  $SUBTREE  ($DIR)"
echo "  to:       $TARGET"
if [ -n "$JUSTIFICATION" ]; then
    echo "  justification: \"$JUSTIFICATION\""
    echo "                 [recorded; NOT evidence — C2 admits on measurement only]"
fi

# --- evidence (a): the L0 polyglot service set, LATTICE line 35 -------------
SERVICE_HITS=""; SERVICE_MISSING=""
for svc in kv mailbox network; do
    if [ -e "$DIR/src/glp/$svc" ] || [ -e "$DIR/src/glp/$svc.gleam" ] \
       || [ -e "$DIR/$svc" ] || [ -e "$DIR/src/$svc" ]; then
        SERVICE_HITS="$SERVICE_HITS $svc"
    else
        SERVICE_MISSING="$SERVICE_MISSING $svc"
    fi
done

# --- evidence (b): runtime freedom, from the T012 measurement ---------------
# Codex review 20260904T055230Z (P1): runtime freedom was read from the COMMITTED whole-tree
# glp_gleam analysis regardless of which subtree was being offered. A genuine L0 subtree
# carrying kv/mailbox/network would be refused because 29 unrelated glp_gleam modules are
# tainted, and the script could never establish the REQUESTED subtree's actual dependencies.
# Measure the subtree that was asked about.
TAINTED="unmeasured"
PY_BIN=""
for c in python3 python; do command -v "$c" >/dev/null 2>&1 && { PY_BIN="$c"; break; }; done
if [ -n "$PY_BIN" ] && [ -f "$ANALYZER" ]; then
    SUB_JSON="$( "$PY_BIN" "$ANALYZER" --root "$DIR" --json 2>/dev/null )"
    if [ -n "$SUB_JSON" ]; then
        TAINTED="$( printf '%s' "$SUB_JSON" | "$PY_BIN" -c '
import json,sys
try:
    d=json.load(sys.stdin); print(len(d.get("runtime_tainted",{})))
except Exception:
    print("unmeasured")' 2>/dev/null )"
        MEASURED_ON="$DIR (measured now)"
    fi
fi
if [ "$TAINTED" = "unmeasured" ] && [ -f "$ANALYSIS" ] && [ -n "$PY_BIN" ]; then
    TAINTED="$( "$PY_BIN" -c '
import json,sys
d=json.load(open(sys.argv[1],encoding="utf-8"))
print(len(d.get("runtime_tainted",{})))' "$ANALYSIS" 2>/dev/null )"
    MEASURED_ON="whole glp_gleam tree (FALLBACK - not the requested subtree)"
fi

echo "  measured:"
echo "    L0 service set present:${SERVICE_HITS:- none}   missing:${SERVICE_MISSING:- none}"
echo "    runtime-tainted modules: $TAINTED   [scope: $MEASURED_ON]"
echo ""

case "$TARGET" in
  l0)
    REFUSE=0; REASONS=""
    if [ -n "$SERVICE_MISSING" ]; then
        REFUSE=1
        REASONS="$REASONS
  - it does not provide the polyglot-L0 service set (missing:$SERVICE_MISSING —
    LATTICE line 35). Being written in Gleam is not evidence of membership."
    fi
    if [ "$TAINTED" != "unmeasured" ] && [ "${TAINTED:-0}" -gt 0 ] 2>/dev/null; then
        REFUSE=1
        REASONS="$REASONS
  - it is not runtime-free: $TAINTED module(s) depend on a third-party runtime (the
    BEAM, via gleam/erlang or Erlang FFI). L0 admits zero third-party runtime deps,
    which is precisely why the contract sits at L0 and each ring realizes it
    separately (008 FR-017)."
    fi
    if [ "$TAINTED" = "unmeasured" ]; then
        echo "CANNOT DECIDE: '$SUBTREE'"
        echo "  runtime freedom is unmeasured (missing $ANALYSIS)."
        echo "  Run: python test/ring/analyze_imports.py --json > $ANALYSIS"
        echo "  An unmeasured premise is never resolved in favour of admission."
        exit 2
    fi
    if [ "$REFUSE" -eq 1 ]; then
        echo "REFUSED: '$SUBTREE' is not admissible to L0."
        printf '%s\n' "$REASONS"
        exit 1
    fi
    echo "ADMITTED: '$SUBTREE' to L0"
    echo "  evidence: provides$SERVICE_HITS; runtime-tainted modules = 0"
    exit 0
    ;;

  beam|atomvm)
    [ -f "$SURFACE" ] || { echo "admit: missing $SURFACE — ring admission cannot be measured, and MUST NOT be assumed" >&2; exit 2; }
    # Codex review 20260904T055230Z (P1): `beam` and `atomvm` executed the IDENTICAL branch, so
    # any subtree consuming one listed module could be admitted to either sibling ring — an
    # AtomVM-only realization could be admitted as BEAM. That destroys the one-realization-per-
    # runtime separation the whole feature rests on (LATTICE line 27). Require a realization
    # module actually named for the target ring.
    RING_MOD="$(find "$DIR" -name "${TARGET}.gleam" 2>/dev/null | head -1)"
    if [ -z "$RING_MOD" ]; then
        echo "REFUSED: '$SUBTREE' for ring '$TARGET'"
        echo "  reason: it carries no realization module named '${TARGET}.gleam'. A subtree that"
        echo "          merely consumes the contract is not a realization OF THIS RING — admitting"
        echo "          it to either sibling would collapse the one-realization-per-runtime rule."
        exit 1
    fi
    echo "  target realization: $RING_MOD"
    # A realization is a subtree that consumes the contract and is NOT the tree the
    # contract was measured from. Self-admission is refused explicitly.
    if [ "$(cd "$DIR" && pwd -P)" = "$(cd "$REPO_ROOT/glp_gleam" && pwd -P)" ]; then
        echo "REFUSED: '$SUBTREE'"
        echo "  reason: this is the tree the contract surface was measured FROM. It cannot"
        echo "          be admitted as a consumer of itself — that predicate is satisfied by"
        echo "          identity, not by evidence."
        exit 1
    fi
    CONSUMED=0; MATCHED=""
    while IFS= read -r mod; do
        mod="${mod%$'\r'}"
        case "$mod" in ''|'#'*) continue ;; esac
        if grep -rqs -E "^[[:space:]]*import[[:space:]]+${mod}([[:space:]]|$)" \
                "$DIR" --include='*.gleam' 2>/dev/null; then
            CONSUMED=$((CONSUMED + 1)); MATCHED="$MATCHED $mod"
        fi
    done < "$SURFACE"
    if [ "$CONSUMED" -eq 0 ]; then
        echo "REFUSED: '$SUBTREE' for ring '$TARGET'"
        echo "  reason: it consumes NO module of the measured L0 contract surface."
        echo "          Admission is by measured contract consumption, never by a name."
        exit 1
    fi
    echo "ADMITTED: '$SUBTREE' to ring '$TARGET'"
    echo "  evidence: consumes $CONSUMED contract module(s):$MATCHED"
    exit 0
    ;;

  *)
    echo "admit: unknown target '$TARGET' (expected l0, beam or atomvm)" >&2
    exit 2 ;;
esac
