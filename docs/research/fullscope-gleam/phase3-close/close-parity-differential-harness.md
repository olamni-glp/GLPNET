<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Close evidence — `T080 close-parity-differential-harness` (b3-c1-041)

**Feature**: 059 · **Wave**: 3 (close) · **Run**: `mrun-7e6cfbf0a9fb` · **Date**: 2026-07-27
**Closes the HALT in**: `verify-parity-differential-harness` (b3-c2-016)
**Commits**: `9bf9d02f` (T051 CRLF fix + harden), `8627f7f3` (repo line-ending policy)

## The HALT and its true root cause

The Wave-2 verify verdict HALTed because `run_gleam_corpus.sh` returned **rc=44** with 44
"`MISSING golden`" entries, and classified it as *evidence-reproducibility drift* — hypothesising
that the reference goldens were absent and must be re-recorded (option a) or the full set committed
(option b).

Investigation this session proved that diagnosis was **symptom-level and wrong on the cause**, and
that option (b) would have been **harmful**:

- The 44 runtime goldens (and `load.golden`) **were already committed** and byte-correct
  (`git ls-files test/parity/goldens/runtime/` = 44 files; blobs LF).
- The real cause was a **CRLF line-ending bug**: the repo runs `core.autocrlf=true` with no
  `.gitattributes`, so a Windows checkout smudges LF→CRLF. The parity bash scripts parse
  `corpus.list` with `read -r line` and never strip `\r`, so block id `a1` became `a1\r`, the golden
  path became `test/parity/goldens/runtime/a1\r.golden`, and **every** runtime block reported
  `MISSING` (44 blocks → rc=44). MSYS `grep`/`awk`/`cat` silently strip `\r` in their output, which
  is exactly why the verdict could see only the symptom.
- `record_dart_goldens.sh` (option a/b) has the **identical** CRLF parsing bug — re-recording on a
  Windows checkout would have written goldens to filenames literally containing a carriage return
  (`a1\r.golden`), corrupting the golden directory. Bug Protocol prevented that.

Proof of cause: stripping the trailing `\r` before the existence check flips the result from
**miss=44 → miss=0** deterministically.

## Fix (infrastructure, not symptom — DISCIPLINE §1.3)

1. Comprehensive repo line-ending policy in `/.gitattributes` (`* text=auto eol=lf` + Windows-script
   `crlf` + binary pins); 2 stray CRLF doc blobs renormalized to LF. Retires the per-subtree CRLF
   patches (`glp_gleam/.gitattributes`, macaroon-v2).
2. Hardened the three parity/test read-loop scripts to strip trailing `\r`
   (`run_gleam_corpus.sh`, `record_dart_goldens.sh`, `run_book_tests.sh`).
3. Refreshed the working tree `.sh` files to LF.

## Runnable evidence (fresh-session reproducible, this branch)

| Check | Command | Result |
|---|---|---|
| Corpus parity (one command) | `bash test/parity/run_gleam_corpus.sh` | **agree=206, diverge=0, blocked=0, gap/fork=0, rc=0** |
| Missing goldens | (same run) | **0** (was 44) |
| 10× wall-clock bound (SC-009) | (same run) | **PASS** |
| Differential harness (3 runtimes) | `run_differential.sh - "X:=2+3"` | all AGREE (dart/csharp/gleam → `X=5, succeeds`), rc=0 |
| Regression guard (grow-only) | `gleam test` | **508/0** (≥ recorded 443) |

## Verdict resolution (per detail_id)

| detail_id | was | now |
|---|---|---|
| `differential-harness` | DELIVERED | DELIVERED (unchanged) |
| `engine-csharp-parity` | DELIVERED (partial) | DELIVERED (unchanged) |
| `performance-sanity-bound` | DELIVERED | DELIVERED (10× PASS) |
| `regression-guard` | DELIVERED | DELIVERED (508 grow-only) |
| `instance-load-and-run` | DELIVERED | DELIVERED |
| `program-corpus` / `test-harness-corpus-parity` | ⚠ NOT REPRODUCIBLE | **DELIVERED** — rc=0, 206 agree, one-command reproducible |
| `gap-fork-case-corpus` | PARTIAL | **DELIVERED** — `gap_g*`/`fork_1` were CRLF-missing, not divergent; now AGREE |

**Close status: CLOSED to named-reference parity.** The differential harness produces one-command,
fresh-session-reproducible full-corpus parity on `059`; the HALT is resolved at root cause.
