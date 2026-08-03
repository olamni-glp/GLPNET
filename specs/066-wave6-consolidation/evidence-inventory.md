<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Evidence inventory (T002) — rulings + receipts for the gate ledger

**Created**: 2026-08-03. Sources verified on disk/board at creation. Rulings are the
engineer's alone (never self-resolved — FR-003, constitution I/II).

## Rulings R1–R12 (all OPEN at creation)

### 3rtask run 20260803T134739Z-fa8a (`.specify/3rtask/runs/20260803T134739Z-fa8a/`)

| id | Ruling needed | Source |
|---|---|---|
| R1 | E9 method question: slice-bound ship-state observation may mis-tag already-shipped work (curator suggests non-blind critic establishes ship-state at adjudication) | curator_report.md:19 |
| R2 | Claim 704158cb: resolver output shape = literal `ep(Host,Port)` with no consumer change is design inference, not specified fact | escalations.md:7 |
| R3 | Claim 17b2a44a: dependency-absence inference — "nothing depends on the items" from absent recorded edges is stronger than the evidence proves | escalations.md:8 |
| R4 | Claim e3fec797: exhaustive-failure-path assertion exceeds finite branch inspection; final gate open | escalations.md:9 |
| R5 | Claim 33700bd9: atomic-file-rename precedent does NOT establish atomic swap of Windows environments/junctions without platform validation | escalations.md:10 |

### 064 codexreview run 20260803T163807Z (`reviews/064-durable-listener-service-box/20260803T163807Z/`, cycle result.json files + verdict.md)

| id | Ruling needed | Source |
|---|---|---|
| R6 | Per-link replay: one-shot/first-link-only vs every-link (confirmed exactly-once violations on second AddLink: reconnect, second dialer, RewireHandle:130) | cycles 1–5, LinkPump.cs:95 findings |
| R7 | Replay-timing contract drift: contract says replay-before-goal-arm + observer-after-replay; implementation replays per-link at establish with Replayed-flag idempotence | contracts/message-log-and-replay.md:36-42 vs Program.cs |
| R8 | Endpoint-occupied re-arm: spec.md:111-113 requires bounded retry; data-model.md:54-55 silently downgraded; unimplemented | cycle findings, Program.cs:184 |
| R9 | Engine idle-break deafness: InboundPumpWait=30s break makes an armed service deaf after a 30s lull (undurable inbox loss window); core glp_engine.cs (025 behavior) | claude c1 finding, glp_engine.cs:613-627 |
| R10 | Split-history replay after mid-run WAL fallback: fallback-only ops invisible to primary replay (contract silent; codex rates high) | ServiceWal.cs:124 findings; fixer adjudication #1 |
| R11 | Foreign-dot-collision retry policy (contract silent; current: loud NOT-persisted diagnostic, no retry under next dot) | fixer adjudication #3 |
| R12 | Quickstart sample suitability: quic_chat consumes exactly 3 terms → replay ≥3 satisfies+closes the advertised chat | codex c2 finding, quickstart.md:18-24 |

## Peer receipts on record (verified refs)

| stamp | source | claim | local verification state |
|---|---|---|---|
| 20260803T153205Z | ariellas | 064-post-wave-gap-closure seams 1–5 done (spec ceb61469 · plan a660e1e5 · tasks(41) 6a3cff24 · analyze 67a2e08a, pushed); implement next; touch-set csharp/{glp_link,glp_engine_host,glp_split_protocol} + glp_gleam/src/glp/link | receipt read; commits not yet fetched-verified (T021 pre-work) |
| 20260803T153920Z | ariellas (lead) | wave-6 carve-out ACCEPTED; v2026.08.03.2 uncontested (ls-remote re-verified); second-lander-rebases norm | consumed 2026-08-03; governs S4 + 064 ship |
| 20260803T205616Z | ariellas | roadmap-sync export (21 new journal lines) | imported this host (e21edf62), reconcile clean |
| 20260803T120302Z | ariellas | wave3-close broadcast ACK (1 of 2) | consumed; broadcast still OPEN on olamnit |

**Olamnit position**: wave3-close ACK OWED (chased 142207Z, unanswered at creation); no receipts
newer than status 140510Z.

## Expected future receipts (ledger gates watch for these)

- 064 ship receipt (v2026.08.03.2 tag) + close receipt — clears G1.
- ariellas US1/US2 implement/ship receipts — clear EXT.ariellas for T021/T022.
- ariellas US4 059-sweep receipts — feed T023 reconcile.
- Engineer ruling texts R1–R12 — clear G3.Rn rows individually.
- 065 story completions — clear G2.
