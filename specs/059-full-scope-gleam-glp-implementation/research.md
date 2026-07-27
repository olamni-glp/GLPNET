<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Phase 0 Research — Full-scope Gleam GLP implementation

This file **composes** the already-adjudicated authoritative inputs; it does not re-derive them
(spec Assumptions; FR-001/FR-013). Authoritative sources referenced throughout:

- `docs/research/fullscope-gleam/gap-inventory-2026-07-19.md` — 154 capabilities (44 delivered / 9 partial / 99 gap-class).
- `docs/research/fullscope-gleam/feature-outline-plan-FINAL-2026-07-20.md` — 90 WPs, waves 1–5, deps, restart-safe acceptance evidence.
- `docs/research/fullscope-gleam/phase2-verify/rulings.md` — binding engineer rulings G1–G5, G3-A.
- `docs/research/fullscope-gleam/frozen-interface-register.md` — the live wave-1 register.

Every decision below is stated as **Decision / Rationale / Alternatives considered**. No NEEDS
CLARIFICATION remains open: the one still-open *escalation* is resolved here **as to how it is
handled** (blocked-until-ruled), which is the spec-mandated disposition, not a blank.

---

## D1 — Parity governance (which runtime is normative on divergence)

- **Decision**: The Dart/C# reference **v2.16** behavior governs wherever the Gleam instance and the
  reference diverge (G4). This explicitly includes the `UnifyConstant` ground-struct-literal case,
  whose golden pin fixes the reference behavior byte-for-byte.
- **Rationale**: Full-scope parity is defined against a single normative oracle; without a fixed
  authority, "parity" decays into two drifting implementations. G4 is a binding ruling.
- **Alternatives considered**: (a) Gleam-defines-its-own-semantics — rejected, breaks the parity
  contract and the reference corpus. (b) Case-by-case adjudication — rejected as non-mechanical and
  non-restart-safe; divergence must be a halt+escalate drift finding, not a per-case judgement.

## D2 — Open escalation `rule-quic-sideprocess-relay` (still OPEN)

- **Decision**: Recorded **OPEN** in the escalation register with a **due-before gate = its wave-4 QUIC
  build WP**. Every dependent wave-4 WP is **blocked** until the engineer rules it; it is never
  re-scoped or worked around (FR-011, spec Edge Cases).
- **Rationale**: The relay boundary (Gleam-driven QUIC vs a C#-side-process relay) changes the wave-4
  build surface; starting dependents before the ruling would build against an unratified boundary.
- **Alternatives considered**: (a) Assume Gleam-native QUIC and proceed — rejected, pre-empts an
  engineer-only decision. (b) Assume side-process relay — same defect, opposite direction.

## D3 — Open escalation `rule-embeddability-api-yngenios-wiring` (RESOLVED)

- **Decision**: **RESOLVED 2026-07-20 — Option C, full wiring** (spec Clarifications). The Gleam GLP
  engine is embedded as the controller across all four spec-056 services (S1 storage, S2 network, S3
  kv, spine) with the fabric's own tests passing against it — not a contract-plus-stub boundary.
- **Rationale**: "Complete inside the yngenios architecture" (G3-A) is only satisfied when the fabric
  actually runs on the embedded engine. This makes yngenios a runtime integration dependency.
- **Alternatives considered**: Option A/B (stub/contract-only boundary) — rejected by the ruling.

## D4 — Store-kernel scope (owner-only escalation)

- **Decision**: Whether object persistence is expressed as `store_put`/`store_get` **kernels** on the
  engine vs a **host-owned log** remains **escalated to the engineer** and is never resolved by the
  team (FR-010). Wave-4 wiring proceeds on the shared mailbox binding without pre-empting this.
- **Rationale**: It touches the language/kernel surface (IV-a Language Authority) and the spec-056
  seam ownership — both owner-gated.
- **Alternatives considered**: Team-chooses-kernels or team-chooses-host-log — both rejected as
  language-authority violations.

## D5 — Freeze-first foundation (why wave 1 precedes everything)

- **Decision**: Freeze the delivered 44 capabilities behind a **frozen-interface register** + make the
  three pinned suites **grow-only tripwires** before any verify/close/build work (wave 1, P1).
- **Rationale**: Every later wave builds on the delivered foundation; without the freeze+guard layer,
  parallel WPs silently drift it and all parity claims decay (spec US1).
- **Alternatives considered**: Build-then-stabilize — rejected; the plan's dependency spine heads with
  the freeze because drift is unrecoverable after the fact.

## D6 — Verify-before-close (99 gap-class + 9 partial)

- **Decision**: Every one of the 97 unconfirmed-gap capabilities gets a committed DELIVERED/ABSENT/
  PARTIAL **verdict with runnable evidence** before any paired close work; an ABSENT verdict activates
  its paired close WP (FR-004, SC-002).
- **Rationale**: Prevents building against records that don't match code (several M1 verdicts already
  found subsystems ABSENT). Verify-first is spec-first made mechanical.
- **Alternatives considered**: Close-by-record without re-verification — rejected; records drift.

## D7 — Build/test topology

- **Decision**: **Windows-native build + WSL test** topology (recorded). Gleam builds on Windows
  (winget Gleam + Erlang/OTP 29); Profile-C QUIC (quicer NIF) is **WSL-only and environment-fragile**;
  AtomVM stays a **gated manual probe** with its recorded procedure.
- **Rationale**: Matches the recorded, reproducible topology; a Profile-C failure must be classified
  environment-vs-absence before any scope conclusion (spec Edge Cases).
- **Alternatives considered**: Windows-only QUIC — rejected (no MSVC/quicer on host); treating a WSL
  build-hook failure as capability-absence — rejected, it is an environment classification.

## D8 — Absent subsystems to build (wave 4)

- **Decision**: The wholly-absent subsystems to build to reference parity are: **multiagent runtime**
  (G2 — largest absent subsystem; `_send`/`_now` kernels, agent runtime, boot loader, global send),
  **QUIC-WS mesh** (G3 — Gleam instance as mesh controller; C# peers eligible), **FE/BE process split**
  (kill-restart, snapshot/restore, two concurrent clients), and **embeddability service-box** (D3).
- **Rationale**: These are the "full scope" bulk beyond the delivered 44; each gates a success
  criterion (SC-005..SC-008).
- **Alternatives considered**: Deferring multiagent/mesh — rejected by G2/G3 as in-scope mandatory.

## D9 — Scope-exit discipline

- **Decision**: Scope exits happen **only by recorded engineer ruling**. The G5 dispositions cover the
  8 filed proposals; any new out-of-scope proposal follows the same rule-request path (FR-012, SC-003,
  SC-009). Zero silent exits.
- **Rationale**: Single source of truth + traceability (Constitution VIII); the coverage union must
  reach a terminal disposition for all 154 detail_ids + open-items.
- **Alternatives considered**: Team-dropped scope — rejected as an ungoverned exit.

---

## Open-items ledger (carried into tasks)

| Item | Status | Gate-before | Disposition rule |
|---|---|---|---|
| `rule-quic-sideprocess-relay` | OPEN | wave-4 QUIC build WP | dependents blocked until ruled (D2) |
| `rule-embeddability-api-yngenios-wiring` | RESOLVED (2026-07-20) | — | full wiring, Option C (D3) |
| store-kernel scope | ESCALATED (owner-only) | wave-4 wiring | never team-resolved (D4) |
| Profile-C QUIC env | ENV-FRAGILE | wave-4 accept | classify env-vs-absence (D7) |
| `UnifyConstant` ground-struct-literal | PINNED | wave-1 freeze | reference v2.16 golden pin (D1) |
