<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Verify verdict — `verify-parity-differential-harness` (b3-c2-016)

**Feature**: 059 · **Wave**: 2 (verify) · **Run**: `mrun-7e6cfbf0a9fb` · **Date**: 2026-07-27 · **Backing detail_ids**: `differential-harness`, `engine-csharp-parity`, `gap-fork-case-corpus`, `instance-load-and-run`, `performance-sanity-bound`, `program-corpus`, `regression-guard`, `test-harness-corpus-parity`

> **⚠ This verdict HALTS on a drift finding (see below). It does not certify M1 corpus parity as
> fresh-session-reproducible on this branch.** Per the WP risk note, re-run divergence is escalated,
> never patched inline.

## Runnable evidence (fresh-session, this run)

| Check | Command | Result |
|---|---|---|
| Differential harness (3 runtimes) | `run_differential.sh - "X:=2+3"` (DART set, C# REPL built, gleam) | **all AGREE** (dart/csharp/gleam → `X = 5, succeeds`), rc=0 |
| Gleam suite (regression guard) | `gleam test` | **508/0** (grew from the recorded 443 → grow-only holds) |
| Dart REPL suite | `bash test/run_all_tests.sh` | green, 0 fail |
| C# reference (partial) | `dotnet test glp_link.tests` | **152/0** (full 727 across 7 suites not all re-run this session) |
| Corpus parity | `bash test/parity/run_gleam_corpus.sh` | **agree=162, diverge=44, blocked=0**; **10× bound PASS** (gleam 10.0s vs dart 41.0s); `CORPUS_RC=44` |

## Classification of the 44 (Bug-Protocol required)

All 44 flagged "divergences" are **`MISSING golden`** entries — no committed reference golden exists to
compare against (the special `a24g_atom`/`a24h`/`a25`–`a30`/`a29v/w`, `gap_g1/g2/g3/g8`, `fork_1`
cases). There are **zero content DIVERGE lines**: every entry that HAS a golden (162) **agrees**. Only
44 committed goldens per runtime exist (`test/parity/goldens/runtime/`), while the corpus is larger, so
`run_gleam_corpus.sh` alone cannot reproduce the recorded figure — the dart reference goldens must be
re-recorded first (`test/parity/record_dart_goldens.sh`, needs dart).

**Classification: evidence-reproducibility drift, NOT a Gleam codec/behaviour divergence.** (Same
discipline as the Profile-C QUIC / AtomVM env-vs-absence rule: a missing reference ≠ a wrong result.)

## Verdict (per detail_id)

| detail_id | verdict | basis |
|---|---|---|
| `differential-harness` | **DELIVERED** | `run_differential.sh` re-run; `X:=2+3` all-3 agree. |
| `engine-csharp-parity` | **DELIVERED (partial re-run)** | C# column agrees in differential; `glp_link` 152/0 (full 727 not all re-run). |
| `performance-sanity-bound` | **DELIVERED** | 10× bound PASS (gleam 10.0s vs dart 41.0s). |
| `regression-guard` | **DELIVERED** | `gleam test` 508 ≥ recorded 443 (grow-only). |
| `instance-load-and-run` | **DELIVERED** | corpus runs on the Gleam instance (162 loaded+agreed). |
| `program-corpus` / `test-harness-corpus-parity` | **⚠ NOT REPRODUCIBLE on this branch** | 44 missing reference goldens → recorded "201/206" not one-command reproducible; RC=44. |
| `gap-fork-case-corpus` | **PARTIAL** | `gap_g*` / `fork_1` are in the 44 missing-golden set (fork_1 is the known owner-gated `<circular>` discriminator, recorded — not a fresh divergence). |

## HALT — escalation (engineer decision)

The corpus-parity evidence is **not fresh-session reproducible** on `059` as committed: 44 reference
goldens are absent, so `run_gleam_corpus.sh` returns rc=44. This is surfaced, not patched. Resolution
options (engineer's call): (a) run `test/parity/record_dart_goldens.sh` (dart present here) to re-record
the 44 and re-compare, closing the gap; or (b) commit the full golden set to the branch; or (c) accept a
reduced golden set and amend the recorded parity figure. **No golden was written and no code changed by
this verify.**
