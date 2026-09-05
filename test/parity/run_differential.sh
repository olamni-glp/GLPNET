#!/usr/bin/env bash
# test/parity/run_differential.sh <program.glp|-> <goal> — run the SAME program+goal on
# the Dart, C#, and Gleam GLP REPLs, normalize each outcome (the shared lib/normalize.sh),
# and report per-runtime + agree/diverge with a three-column view on divergence
# (feature 050, T041; FR-012 — closes MISS-04, the missing three-way differential).
# Exit code = number of DIVERGENT unordered pairs among {dart,csharp,gleam} (0 = all agree).
# Host: git-bash (native). Pass "-" as the program to run a prelude-only goal.
#
# Usage: run_differential.sh <program-relpath|-> "<goal>"

set -u
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
. "$SCRIPT_DIR/lib/normalize.sh"

[ "$#" -ge 2 ] || { echo "usage: run_differential.sh <program.glp|-> \"<goal>\"" >&2; exit 2; }
PROG="$1"; GOAL="$2"

DART="${DART:-$(command -v dart 2>/dev/null || echo /c/src/flutter/bin/cache/dart-sdk/bin/dart)}"
# TFM comes from the csproj, never a literal (test/lib/tfm.sh explains why this was
# pinned to net10.0 in seven places and what that cost).
. "$(dirname "${BASH_SOURCE[0]}")/../lib/tfm.sh"
CSREPL="$(glp_repl_exe "$REPO_ROOT")" || { echo "run_differential.sh: cannot resolve the C# REPL target framework from out/csharp/glp_repl/glp_repl.csproj - refusing rather than falling back to a stale binary" >&2; exit 2; }

to_repl_path() { if command -v cygpath >/dev/null 2>&1; then cygpath -m "$1"; else printf '%s' "$1"; fi; }

# Extract + normalize the GOAL's outcome from a raw transcript. The goal is the LAST
# `GLP> ` segment before `Goodbye!` (one optional load precedes it); segmenting on the
# prompt and dropping the load/quit segments isolates it, then normalize_outcome.
goal_outcome() {
  printf '%s\n' "$1" | tr -d '\r' \
    | awk '/^GLP> /{seg++; a[seg]=""} seg>0{a[seg]=a[seg] $0 "\n"}
           END{
             # the goal segment is the second-last (last is "Goodbye!"); if only one
             # data segment exists (no load, no goodbye captured) use the last.
             g = (seg>=2) ? seg-1 : seg
             printf "%s", a[g]
           }' \
    | normalize_outcome
}

# Build a REPL session (load line optional) for a given load-path form.
session_input() {  # $1 = load path form ("" to skip)
  { [ -n "$1" ] && printf 'load %s\n' "$1"; printf '%s\n:quit\n' "$GOAL"; }
}

run_dart() {
  local lp=""; [ "$PROG" != "-" ] && lp="$(to_repl_path "$REPO_ROOT/$PROG")"
  session_input "$lp" | ( cd "$REPO_ROOT" && "$DART" run glp_runtime/bin/glp_repl.dart ) 2>&1
}
run_csharp() {
  local lp=""; [ "$PROG" != "-" ] && lp="$(to_repl_path "$REPO_ROOT/$PROG")"
  session_input "$lp" | "$CSREPL" 2>&1
}
run_gleam() {
  local lp=""; [ "$PROG" != "-" ] && lp="../$PROG"
  session_input "$lp" | ( cd "$REPO_ROOT/glp_gleam" && gleam run ) 2>/dev/null
}

echo "=== differential: program='$PROG' goal='$GOAL' ==="

D_OK=1; C_OK=1; G_OK=1
DART_OUT="$(goal_outcome "$(run_dart)")"
if [ ! -f "$CSREPL" ]; then echo "  (C# REPL not built at $CSREPL — skipping C# column)"; C_OK=0; CS_OUT=""; else CS_OUT="$(goal_outcome "$(run_csharp)")"; fi
if ! command -v gleam >/dev/null 2>&1; then echo "  (gleam not on PATH — skipping Gleam column)"; G_OK=0; GL_OUT=""; else GL_OUT="$(goal_outcome "$(run_gleam)")"; fi

printf -- '--- dart ---\n%s\n--- csharp ---\n%s\n--- gleam ---\n%s\n' \
  "$DART_OUT" "${CS_OUT:-<skipped>}" "${GL_OUT:-<skipped>}"

# Count divergent pairs among the runtimes that actually ran. Every divergent
# pair emits a NAMED `verdict: fail` line — case id + both normalized outcomes,
# actionable without re-running (T028, FR-017, contracts/corpus-report.md
# invariant 5). The first runtime named in the pair is the expectation side
# (dart is the reference whenever it participates).
CASE_ID="$PROG::$GOAL"
compact() { printf '%s' "$1" | tr '\n' ';' | sed 's/;*$//'; }
pairs=0; diverged=""
cmp_pair() {  # $1=name-a $2=out-a $3=ok-a  $4=name-b $5=out-b $6=ok-b
  [ "$3" = 1 ] && [ "$6" = 1 ] || return 0
  if [ "$2" != "$5" ]; then
    pairs=$((pairs+1)); diverged+=" $1/$4"
    echo "verdict: fail case=$CASE_ID pair=$1/$4 expected=$(compact "$2") observed=$(compact "$5")"
  fi
}
cmp_pair dart "$DART_OUT" "$D_OK" csharp "$CS_OUT" "$C_OK"
cmp_pair dart "$DART_OUT" "$D_OK" gleam "$GL_OUT" "$G_OK"
cmp_pair csharp "$CS_OUT" "$C_OK" gleam "$GL_OUT" "$G_OK"

echo ""
if [ "$pairs" -eq 0 ]; then
  echo "verdict: pass case=$CASE_ID"
  echo "RESULT: all runtimes AGREE (normalized)"
else
  echo "RESULT: $pairs divergent pair(s):$diverged"
fi
exit "$pairs"
