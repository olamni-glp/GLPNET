#!/usr/bin/env bash
# test/ring/check_atomvm_subset.sh — the C3 build-time AtomVM subset gate (feature 101, T018).
#
# Refuses at BUILD time, NAMING the offending construct (FR-004). A runtime rejection is a
# silent degrade until the offending path executes — the workaround shape Principle II forbids —
# so this runs as a build gate, not a guard inside the program.
#
# Reads test/ring/atomvm-unsupported.list. 🔴 That list is a LOWER BOUND measured on AtomVM
# 0.6.6 (engineer ruling Q-GLPNETS17-01), not an exhaustive subset. So:
#
#   a PASS here means "none of the constructs we have MEASURED as unsupported are present".
#   It does NOT mean "this will run on AtomVM".
#
# The distinction is printed on every pass, because a gate whose limits are only in a comment
# is a gate whose limits nobody knows. The honest verdict for the ring stays UNREAD until an
# actual AtomVM toolchain runs the corpus (T017 second half, staged in install-atomvm.md).
#
# Scope: the modules the AtomVM ring would carry — the L0 contract plus glp/ring/atomvm and
# whatever it imports. It deliberately does NOT scan glp/ring/beam or the BEAM-only transports:
# those are the OTHER ring, and holding L1b to L1a's subset is precisely the sharing that
# LATTICE line 27 forbids.
#
# Usage: bash test/ring/check_atomvm_subset.sh [--scope <dir>]
# Exit:  0 no measured-unsupported construct present · 1 violation (named) · 2 cannot check.

set -u
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
LIST="$SCRIPT_DIR/atomvm-unsupported.list"
GLEAM_SRC="$REPO_ROOT/glp_gleam/src"

SCOPE=""
while [ $# -gt 0 ]; do
    case "$1" in
        --scope) SCOPE="${2:-}"; shift 2 ;;
        *) echo "check_atomvm_subset: unknown argument '$1'" >&2; exit 2 ;;
    esac
done

[ -f "$LIST" ] || { echo "check_atomvm_subset: missing $LIST — the subset is unmeasured, and an unmeasured subset must not be resolved in favour of passing" >&2; exit 2; }

# Default scope: the contract + the atomvm ring. Both must exist to be checked.
if [ -z "$SCOPE" ]; then
    SCOPE="$GLEAM_SRC/glp/contract"
    ATOMVM_RING="$GLEAM_SRC/glp/ring/atomvm.gleam"
else
    ATOMVM_RING=""
fi
[ -d "$SCOPE" ] || { echo "check_atomvm_subset: no such scope: $SCOPE" >&2; exit 2; }

echo "== C3 · AtomVM subset gate =="
echo "  scope: $SCOPE${ATOMVM_RING:+ + $ATOMVM_RING}"

VIOLATIONS=""
COUNT=0
while IFS="$(printf '\t')" read -r construct reason; do
    construct="${construct%$'\r'}"
    case "$construct" in ''|'#'*) continue ;; esac
    [ -z "$reason" ] && reason="(no reason recorded)"
    COUNT=$((COUNT + 1))

    # Match STRUCTURALLY — an import statement or an FFI target — never a bare substring.
    #
    # This is the third time in this feature that a naive substring detector matched the very
    # text that DESCRIBES what it forbids: the C1-R gate matched its own doc comment, and this
    # gate's first version matched `glp/ring/atomvm.gleam`'s `unsupported()` list, which exists
    # precisely to declare these names. A detector that cannot distinguish "uses proc_lib" from
    # "says the word proc_lib" reports a violation on the module doing the forbidding.
    #
    # So a line counts as a hit only if it is:
    #   * an import  —  ^import <construct>            (path form, e.g. gleam/otp)
    #   * an FFI     —  @external(erlang, "<construct>"  (module form, e.g. proc_lib)
    #   * a qualified call —  <last-segment>(            e.g. process.spawn(
    # A string literal in a list, and any comment, matches none of these.
    HITS=""
    esc="$(printf '%s' "$construct" | sed 's/[.[\*^$\/]/\\&/g')"
    last="${construct##*/}"
    esc_last="$(printf '%s' "$last" | sed 's/[.[\*^$\/]/\\&/g')"
    for target in "$SCOPE" $ATOMVM_RING; do
        [ -e "$target" ] || continue
        while IFS= read -r hit; do
            [ -n "$hit" ] && HITS="$HITS $hit"
        done <<EOF
$(grep -rnE --include='*.gleam' --include='*.erl' \
    -e "^[[:space:]]*import[[:space:]]+${esc}([[:space:]]|\.|/|$)" \
    -e "@external\([[:space:]]*erlang[[:space:]]*,[[:space:]]*\"${esc}\"" \
    -e "(^|[^\"a-zA-Z0-9_])${esc_last}\(" \
    "$target" 2>/dev/null \
    | grep -vE ':[[:space:]]*(//|%)' | cut -d: -f1-2)
EOF
    done

    if [ -n "$HITS" ]; then
        VIOLATIONS="$VIOLATIONS
  construct: $construct
    reason:  $reason
    found:  $HITS"
    fi
done < "$LIST"

if [ -n "$VIOLATIONS" ]; then
    echo ""
    echo "C3 REFUSAL — the AtomVM ring uses a construct measured as unsupported."
    printf '%s\n' "$VIOLATIONS"
    echo ""
    echo "  This refusal is at BUILD time and names the construct, per FR-004. A runtime"
    echo "  rejection would be a silent degrade until that path executed."
    exit 1
fi

echo "  checked $COUNT measured-unsupported construct(s): none present"
echo ""
echo "  🔴 SCOPE OF THIS PASS: the list is a LOWER BOUND measured on AtomVM 0.6.6 only"
echo "     (Q-GLPNETS17-01). A pass means no MEASURED-unsupported construct is present."
echo "     It does NOT mean this will run on AtomVM. The ring stays UNREAD until a real"
echo "     AtomVM toolchain runs the corpus — see test/ring/install-atomvm.md."
exit 0
