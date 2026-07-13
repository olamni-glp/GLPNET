#!/usr/bin/env bash
# test/parity/lib/normalize.sh — shared output-normalization rules (feature 050).
# Sourced by BOTH record_dart_goldens.sh (recorder) and run_gleam_corpus.sh
# (comparator) so Dart and Gleam outputs are normalized IDENTICALLY
# (contracts/corpus-parity.md, T038). Host: git-bash or WSL.
#
# The rules absorb three RENDERING differences that are not semantic divergences
# (corpus-manifest.md §6): the REPL prompt/chrome, variable numbering, and the two
# instances' distinct unbound-variable rendering. A genuine outcome difference is a
# FORK/port-bug (Bug Protocol), never normalized away.

# ---------------------------------------------------------------------------
# normalize_outcome — canonicalize ONE goal's outcome text block (stdin -> stdout).
# Applied per-goal-segment by the recorder and per-goal-output by the comparator.
#   * strip CR (Windows CRLF)
#   * strip a leading "GLP> " prompt from each line
#   * canonicalize the unbound-variable rendering to a single token <unbound>:
#       - Dart prints a fully-unbound query result as "<unbound>"
#       - Gleam renders an unbound query var as "X<digits>" (envelope var id) with
#         NO heap-only reader "?" (restart-note item 5a)
#     so " = X12" / " = _G7" / " = <unbound>" all fold to " = <unbound>".
#   * stabilize any remaining internal variable numbering (_G<n> / _<n>) to _Gn
#   * drop a heap-only trailing reader "?" on a bare variable token
#   * trim trailing blank lines
# ---------------------------------------------------------------------------
normalize_outcome() {
  tr -d '\r' \
    | sed -E 's/^GLP> //' \
    | sed -E 's/= [A-Z][A-Za-z_]*[0-9]+([[:space:]]|$)/= <unbound>\1/g' \
    | sed -E 's/_G[0-9]+/_Gn/g; s/_[0-9]+/_Gn/g' \
    | sed -E 's/([A-Za-z_][A-Za-z0-9_]*)\?([[:space:],)]|\]|$)/\1\2/g' \
    | awk '{ln[NR]=$0} END{last=NR; while(last>0 && ln[last]=="") last--; s=1; while(s<=last && ln[s]=="") s++; for(i=s;i<=last;i++) print ln[i]}'
}

# ---------------------------------------------------------------------------
# classify_load — given a load OUTCOME line for one file, emit the binary parity
# token plus (tab-separated) the informational stage:
#   "LOADED\t-"                      the file loaded clean
#   "REJECTED\t{guard|srsw|type|parse|other}"
# The binary token is the diff target; the stage is informational (manifest §1).
# Input (stdin): the single "... Loaded: <path>" or "Error loading <path>: <reason>" line.
# ---------------------------------------------------------------------------
classify_load() {
  local line; line="$(tr -d '\r' | sed -E 's/^GLP> //')"
  if printf '%s' "$line" | grep -q 'Loaded:'; then
    printf 'LOADED\t-\n'
    return
  fi
  local stage=other
  case "$line" in
    *'is not a guard'*)                          stage=guard ;;
    *SRSW*|*'single-writer'*|*'single-reader'*)  stage=srsw ;;
    *TypeError*|*'Type error'*|*'type error'*|*UnknownType*|*'mode mismatch'*|*complementar*) stage=type ;;
    *Parse*|*'parse error'*|*Syntax*|*Unexpected*) stage=parse ;;
    *) stage=other ;;
  esac
  printf 'REJECTED\t%s\n' "$stage"
}
