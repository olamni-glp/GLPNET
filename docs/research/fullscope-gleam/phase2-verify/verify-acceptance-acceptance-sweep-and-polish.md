<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Verify verdict — `verify-acceptance-acceptance-sweep-and-polish` (b3-c1-017)

**Feature**: 059 · **Wave**: 2 (verify) · **Run**: `mrun-7e6cfbf0a9fb` · **Date**: 2026-07-27 · **Backing detail_ids**: `acceptance-sweep-and-polish`, `cross-runtime-pair-capstone`

## Runnable evidence (fresh-session reproducible)

| Check | Command | Result |
|---|---|---|
| Cross-Gleam capstone rig script | `find . -name run_link_tests_cross_gleam.sh` | **ABSENT** (only `test/link/run_link_tests{,_cross,_dart}.sh` exist — no `_cross_gleam.sh`) |
| Acceptance evidence file | `find specs/050-full-gleam-combined -iname 'acceptance*.md'` | **ABSENT** |
| Capstone tasks state | `specs/050-full-gleam-combined/tasks.md:147-170` (T059–T068) | **all unchecked** (`- [ ]`), 7 in range |
| String references | `rg -l run_link_tests_cross_gleam` | only `.md` spec/plan/handover files (references, not the script) |

## Verdict

| detail_id | verdict | basis |
|---|---|---|
| `acceptance-sweep-and-polish` | **ABSENT (unstarted)** — expected | No acceptance.md; SC-001..SC-009 sweep not executed as a capstone artifact. |
| `cross-runtime-pair-capstone` | **ABSENT (unstarted)** — expected | The C#↔Gleam 16/16 rig (`run_link_tests_cross_gleam.sh`) does not exist; T059–T063 unchecked. |

**Overall: DELIVERED-as-verify** — the WP's job is to confirm the capstone remains unstarted and scope
its work; both detail_ids are confirmed unstarted with reproducible evidence. This does **not** start the
capstone.

## Work-scope statement (feeds the close/accept WPs)

The 16/16 cross-runtime capstone (`build-…`/`accept-full-scope-regression`, Wave 5) must, from a fresh
session: (a) author `test/link/run_link_tests_cross_gleam.sh` extending the 025 rig to boot the Gleam
REPL as a role host beside `out/csharp/glp_repl`; (b) load the 8 pair programs clean on Gleam; (c) drive
C#↔Gleam 16/16 green over TCP (both directions, FR-016); (d) run over QUIC-WS under WSL vs the C# peer
(SC-008) — **gated by T098 `close-quic-sideprocess-relay-smoketest`** per the 2026-07-27 ruling; (e)
wire the quiescence oracle into completion detection (SC-005); (f) emit `acceptance.md` with an evidence
row per SC-001..SC-009. This is an M2-LOCK hard gate — the wave-5 sweep depends on it.
