#!/usr/bin/env bash
# test/ring/check_contract_purity.sh — the C1-R enforcement gate (feature 101, T013).
#
# THIS IS THE MECHANISM, not a test of it. `gleam build` compiles a `gleam/erlang/process`
# import inside glp/contract/ perfectly happily (measured 2026-09-03) — the compiler has no
# opinion about architectural layers. So C1-R has to be enforced by something, and this is
# that something. Its positive control lives in test/ring/test_contract_purity.sh, which
# injects a runtime import here and asserts this script rejects it.
#
# The rule (contracts/ring-delivery.md C1):
#   No module under glp_gleam/src/glp/contract/ may depend on a third-party runtime,
#   directly or transitively.
#
# "Transitively" is load-bearing. A contract module that imports a runtime-free-looking
# module which itself imports gleam/erlang is just as tainted — the runtime still gets
# dragged in at build time. The transitive closure is computed by analyze_imports.py; this
# script is the gate that reads it.
#
# Exit: 0 pure · 1 violation (naming the offending module and the chain) · 2 cannot check.

set -u
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
GLEAM_SRC="$REPO_ROOT/glp_gleam/src"
CONTRACT_DIR="$GLEAM_SRC/glp/contract"
ANALYZER="$SCRIPT_DIR/analyze_imports.py"

PY="${PYTHON:-}"
if [ -z "$PY" ]; then
    for c in python3 python; do
        command -v "$c" >/dev/null 2>&1 && { PY="$c"; break; }
    done
fi
[ -n "$PY" ] || { echo "check_contract_purity: no python on PATH — cannot compute the transitive closure, and a direct-only scan would be a false pass" >&2; exit 2; }
[ -f "$ANALYZER" ] || { echo "check_contract_purity: missing $ANALYZER" >&2; exit 2; }

if [ ! -d "$CONTRACT_DIR" ]; then
    echo "check_contract_purity: no glp/contract/ tree — nothing to check" >&2
    exit 2
fi

# Ask the analyzer which modules are runtime-tainted (transitively), then intersect with
# the contract package. Any intersection at all is a C1-R violation.
REPORT="$( "$PY" "$ANALYZER" --json 2>&1 )" || {
    echo "check_contract_purity: analyzer failed:" >&2; echo "$REPORT" >&2; exit 2; }

VIOLATIONS="$(
    printf '%s' "$REPORT" | "$PY" -c '
import json, sys
try:
    d = json.load(sys.stdin)
except Exception as e:
    print("PARSE_ERROR " + str(e)); raise SystemExit(0)
bad = {m: v for m, v in d.get("runtime_tainted", {}).items()
       if m.startswith("glp/contract/")}
for m, v in sorted(bad.items()):
    chain = " -> ".join(v.get("path_to_taint", [m]))
    why = "; ".join(v.get("reasons", [])) or "via " + chain
    print(f"{m}\t{why}\t{chain}")
# Codex review 20260904T055230Z (P2): a contract module the analyzer could not READ (bad
# permissions, invalid encoding) landed in not_read[] and was then simply not looked at here,
# so the gate printed "C1-R OK". Purity cannot be established for a module nobody read - an
# unread contract module must REFUSE, not become a silent pass.
for e in d.get("not_read", []):
    m = e.get("module", "?")
    if m.startswith("glp/contract/"):
        why = e.get("reason", "?")
        print(m + "\tUNREADABLE - purity cannot be established: " + str(why) + "\t(not read)")
'
)"

case "$VIOLATIONS" in
    PARSE_ERROR*) echo "check_contract_purity: $VIOLATIONS" >&2; exit 2 ;;
esac

if [ -n "$VIOLATIONS" ]; then
    echo "C1-R VIOLATION — the L0 contract must carry no third-party runtime dependency."
    echo ""
    printf '%s\n' "$VIOLATIONS" | while IFS="$(printf '\t')" read -r mod why chain; do
        echo "  module: $mod"
        echo "    runtime dependency: $why"
        echo "    import chain:       $chain"
    done
    echo ""
    echo "  BEAM and AtomVM are both third-party runtimes (008 FR-017). The contract stays"
    echo "  runtime-free and each ring realizes it — see contracts/ring-delivery.md C1."
    exit 1
fi

COUNT="$(find "$CONTRACT_DIR" -name '*.gleam' 2>/dev/null | wc -l | tr -d ' ')"
echo "C1-R OK — $COUNT contract module(s), no third-party runtime dependency (transitive closure checked)"
exit 0
