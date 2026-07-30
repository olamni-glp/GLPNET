<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Wave 4 (062) — Terminal-state ledger (T034 / SC-008)

**Closed:** 2026-07-30 · **Branch:** `062-wave-4-consolidated-parallel-safe-fillers`
**Purpose:** SC-008 — confirm every item is in a terminal state (delivered /
delivered-as-study / explicitly deferred), with **no item silently dropped**.

## Success criteria → terminal state

| SC | Item | Terminal state | Evidence |
|----|------|----------------|----------|
| SC-001 | Depgraph mark-and-recompute + trends | **delivered** | T006–T011; codeconv depgraph 66/66 |
| SC-002 | Three feasibility studies (programme/LLVM, C++ engine, many-instances) | **delivered-as-study** (per Assumptions) | T012–T014, `research/*.md`, each with go/no-go + risks |
| SC-003 | Multi-accept ≥2 clients, zero dropped | **delivered** | T019; `MultiAcceptTests` (2+5 clients, distinct links, PendingCount 0); glp_link 161/161 |
| SC-004 | Compiled-IL-on-the-wire remote == local | **delivered** | T016–T018 + T021; execute-on-B == local (succeed/fail/suspend), incl. over real ZMQ; il_codec 64/64 |
| SC-005 | GLP multi-client control program + regression case | **delivered** | T023–T025; REPL Section A31; `control_demo` → succeeds |
| SC-006 | No baseline regression | **delivered** | REPL 546/546, engine `dart test` 11/11, il_codec 64/64, glp_link 161/161, wire_registry 6/6, engine sln 0 errors. **Gleam re-baseline pending at T037** (gated on the (b) capability). |
| SC-007 | §1.14 proposals + zero unapproved lang/runtime change | **delivered** | T026/T027 proposals; operator approvals recorded; NO Dart structural change (parity-verify + pins only) |
| SC-008 | Every item terminal, nothing dropped | **this ledger** | see below |

## Wave items — terminal disposition (SC-008)

- **US1 (depgraph), US2 (studies), US3 (engine/transport), US4 (control program),
  US5 (§1.14 items):** all tasks T001–T033 marked `[X]` in `tasks.md`. **Delivered.**
- **US3 §1.14 fork (T028):** resolved by operator — target BOTH C# + Gleam. **Delivered.**
- **Decision (a) — ZMQ/NetMQ:** operator ruled "full, integrated capability" (not a
  deferred study). **Delivered** as `ZmqTransport` (NetMQ PAIR base) + envelope-over-ZMQ
  execute-on-B (T020–T022).
- **Decision (b) — Gleam REPL conjunction-query gap:** an orthogonal Gleam-MVP frontend
  limitation surfaced during US5 T030 (the Gleam REPL parser rejects `a, b.`
  conjunction goals; Dart already accepts them). Operator ruled "build it as a full MVP
  capability via `/bk-3rtask`." **In progress — tracked, not dropped:** a `/bk-3rtask`
  team is implementing it in `glp_gleam/` in parallel; it ships in **this same increment**
  (T037 ship is gated on its completion + a green Gleam re-baseline). This is the item's
  recorded terminal path; it is NOT silently dropped.

## Phase 8 remaining

- T035 `/bk-codify` wins — in progress.
- T036 fleet UPDATE → ariellas — pending.
- T037 final full-suite sweep (incl. Gleam re-baseline) → `/bk-analyze` → ship via
  GitFlow (CalVer coordinated with ariellas) — **gated on decision (b) completion.**
