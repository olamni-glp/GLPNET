#!/usr/bin/env bash
# test/parity/lib/normalize.sh — shared output-normalization rules (feature 050).
# Sourced by BOTH record_dart_goldens.sh and run_gleam_corpus.sh so the recorder and the
# comparator normalize identically (contracts/corpus-parity.md).
# Host: git-bash or WSL.
#
# T002 skeleton — the real rule set (strip prompts/timing noise, stabilize variable
# numbering) lands with T038.

normalize_output() {
  # T038: pass-through until the rule set is defined.
  cat
}
