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
    | sed -E '/^Error:/d' \
    | sed -E 's/([A-Za-z_][A-Za-z0-9_]*)\?([[:space:],)]|\]|$)/\1\2/g' \
    | sed -E 's/= [A-Z][A-Za-z_]*[0-9]+[[:space:]]*$/= <unbound>/' \
    | awk '
        # Renumber internal variable ids (X<n> / _G<n> / _<n>) that appear in the
        # VALUE part of a "<name> = <value>" line to stable _V<k> tokens, first-seen
        # order, CONSISTENT across the goal (so shared ids -- e.g. the two ends of a
        # channel, ch(X7,X9)/ch(X9,X7) -- keep their sharing pattern). The left-hand
        # query-var NAME is protected (only the substring after " = " is rewritten).
        {
          line=$0; eq=index(line," = ")
          if (eq>0) {
            lhs=substr(line,1,eq+2); rhs=substr(line,eq+3); out=""
            while (match(rhs, /X[0-9]+|_G[0-9]+|_[0-9]+/)) {
              tok=substr(rhs,RSTART,RLENGTH); pre=substr(rhs,1,RSTART-1)
              if (!(tok in map)) map[tok]="_V" (++k)
              out=out pre map[tok]; rhs=substr(rhs,RSTART+RLENGTH)
            }
            print lhs out rhs
          } else { print line }
        }' \
    | awk '
        # Canonicalize binding ORDER within a goal outcome (restart-note item 5b):
        # Gleam splits bound (resolved_bindings) then unbound (var_to_writer) while Dart
        # emits one ordered map, so a mixed multi-var goal lists the same bindings in a
        # different order. The binding SET + the status are the parity signal, not the
        # order -- so sort the "<name> = <value>" lines, keeping the "-> status" line last.
        /^(→|->)/ { status = status $0 "\n"; next }
        /=/       { b[++n]=$0; next }
        { other = other $0 "\n" }          # any non-binding, non-status line: keep as-is
        END {
          for (i=1;i<=n;i++) for (j=i+1;j<=n;j++) if (b[j]<b[i]) { t=b[i]; b[i]=b[j]; b[j]=t }
          printf "%s", other
          for (i=1;i<=n;i++) print b[i]
          printf "%s", status
        }' \
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
