# Phase 0 Research: Wave 3 — Full Gleam chain

**Feature**: `060-wave3-full-gleam-chain` | **Date**: 2026-07-27

All `NEEDS CLARIFICATION` markers from the spec were resolved at `/bk-clarify` (see `spec.md` → Clarifications → Session 2026-07-27). This document records those decisions plus the source-level gap analysis that the plan rests on.

---

## D1. Shared grammar — out of scope

- **Decision**: The Gleam runtime keeps its own front end (`glp/parser/{lexer,parser,ast}.gleam`). No ANTLR4 or other shared grammar artifact is produced in wave 3.
- **Rationale**: Feature 059's compiler verification recorded the ANTLR path as **superseded under the G5 ruling**, and recorded `parser-recursive-descent`, `compile-mode`, and `strict-gate` as DELIVERED. Introducing a generator now would be rework against a working front end, and would put a code-generation step between the language definition and three runtimes.
- **Alternatives considered**: (a) Generate all three front ends from one grammar — rejected: 059 already ruled it superseded, and two of the three front ends are already written and passing. (b) Treat the Dart parser as a normative reference implementation and port it literally — rejected: couples Gleam's structure to Dart's, and conformance already gives the guarantee without the coupling.
- **Consequence**: Cross-runtime syntax agreement becomes an *observable* (FR-002, proven by the corpus) rather than a *construction* (proven by shared generation). Any accept/reject divergence is a named conformance failure.

## D2. Acceptance transports — loopback + TCP only

- **Decision**: Wave 3 acceptance runs over loopback and TCP. QUIC/WebSocket and ZMQ remain reachable behind the seam but are not proven.
- **Rationale**: The seam (`glp/link/seam/*`) and both acceptance transports already exist. 059 recorded engine-side QUIC-WS as **ABSENT** (T055, no `quic_ws.gleam`), and Profile-C QUIC acceptance as **ENV-BLOCKED, not code-absent** — the WSL `quicer 0.2.15` build hook fails and there is no MSVC on this Windows host. Making a known-blocked dependency an acceptance gate would block the wave on a toolchain problem unrelated to its goal.
- **Alternatives considered**: (a) Include ZMQ, since the leaf landed in 059 — rejected: its runtime is WSL-provisioned via `profile_zmq/`, so it imports the same environment fragility for no acceptance value. (b) Include QUIC/WS — rejected: engine side does not exist yet; that is its own feature.
- **Consequence**: FR-025 requires the seam to stay open — the other transports must remain selectable without link-layer code changes, so the deferred work stays cheap.

## D3. Execution target — BEAM, AtomVM deferred

- **Decision**: Full BEAM is the wave-3 acceptance target. AtomVM is deferred to a follow-on, with FR-032 forbidding constructs known to be unavailable there.
- **Rationale**: AtomVM toolchain instability is the wave's **primary recorded risk**. The prerequisite `m2-0-verify-erlang-monitor-atomvm` is delivered and 059 recorded `platform-atomvm` as DELIVERED-by-construction, so the path is not lost by deferring. Gating a seven-item consolidation on the least stable dependency inverts the risk ordering.
- **Alternatives considered**: (a) AtomVM as a hard gate — rejected: dominates the schedule for a capability none of the five user stories needs. (b) Drop AtomVM compatibility entirely — rejected: would silently foreclose the deferred work; hence FR-032 keeps the constraint without the gate.
- **Consequence**: `atomvm_gated_probe.gleam` stays as the compatibility canary. Any deliberate BEAM-only construct must be recorded with its reason.

## D4. Corpus goldens — the 44 are out-of-scope, regeneration is in-scope

- **Decision**: The 44 corpus cases whose reference goldens are missing are declared out-of-scope with the reason `golden missing — 059 T051 drift`; they may not be counted as passes. Regenerating them is wave-3 work (FR-018b, SC-010).
- **Rationale**: Feature 059 commit `c7d65a13` recorded T051 parity as **HALT/ESCALATE — corpus rc=44, 44 missing goldens = evidence-reproducibility drift, engineer decision needed**. Wave 3's entire correctness claim (SC-001) rests on the corpus, so inheriting an unexplained 44-case hole would make the claim meaningless. Counting them as passes would be exactly the "robustness as workaround" Principle II forbids.
- **Alternatives considered**: (a) Count them as passes pending investigation — rejected: violates Principle II and would corrupt SC-001. (b) Delete them from the corpus — rejected: destroys evidence of a known drift. (c) Block the wave until regenerated — rejected: the other four user stories do not depend on them.
- **Consequence**: The corpus report must carry an explicit out-of-scope bucket with reasons (FR-017, FR-018), and `record_dart_goldens.sh` becomes a wave-3 instrument, not just a maintenance script.

---

## Source-level gap analysis

Verified against `glp_gleam/src/` on 2026-07-27. "Exists" means the module is present; the gap is what 059 verification recorded as ABSENT or PARTIAL inside it.

| # | Module | State | Gap to close | Story |
|---|---|---|---|---|
| G1 | `glp/compiler/loader.gleam` | exists | module **static linking** and **dynamic dispatch** — 059: `Unimplemented distribute` | US1 |
| G2 | `glp/compiler/partial_eval.gleam` | exists | `reduce` metainterpreter PARTIAL — blocked on a missing `_copy/2` | US1 |
| G3 | `glp/lint.gleam` | placeholder | bytecode lint ABSENT | US1 |
| G4 | `glp/repl/commands.gleam` | exists | `:boot` and `:bytecode` ABSENT (`:trace`, `:limit` DELIVERED) | US2 |
| G5 | `test/parity/*.sh` | exists | 44 missing goldens; runner must emit explicit out-of-scope bucket | US3 |
| G6 | `glp/engine.gleam` | PARTIAL | composition root has kernels compiled in, **no transport injection seam** | US4 |
| G7 | `glp/link/*` | PARTIAL | inbound pump, link acceptance, capability gate, instance network join ABSENT (T050–T058) | US4 |
| G8 | `glp/link/reliability/frame_codec.gleam` | PARTIAL | FrameCodec + CRC floor only; ordering/rehydration guarantees not established | US4 |
| G9 | `glp/mad/*.gleam` | ABSENT | multiagent boot loader empty; named reference plays malformed on **both** runtimes (`|` type-alt) | US4/US5 |
| G10 | cross-runtime suite | absent | no C#↔Gleam distributed suite exists yet | US5 |

**Note on G9**: 059 recorded the malformed named-reference plays as failing on *both* runtimes. That is a shared defect, not a Gleam gap — under Principle II it must be reported and specified before being "fixed" on one side.

## Non-regression baseline

- Gleam: **465 green** (recorded in 059 as the floor; raised from 463).
- Repo REPL suite: `bash test/run_all_tests.sh` must stay green (SC-009).
- Both must be captured *before* the first wave-3 code change, per Principle VII.
