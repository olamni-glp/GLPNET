# Implementation Plan: Wave 1 Consolidated — GLP Policy-Guard + HTTP3/QUIC-WS Link Full Acceptance

**Branch**: `049-wave1-guard-link-acceptance` | **Date**: 2026-07-08 | **Spec**: `specs/049-wave1-guard-link-acceptance/spec.md`
**Input**: Feature specification from `/specs/049-wave1-guard-link-acceptance/spec.md`

## Summary

Two consolidated deliverables. **(A)** Implement the §1.14-approved GLP guard `satisfiable(Policy?, Reachable?)` with three-valued Success/Suspend/Fail semantics as an alternative crdtmsg routing evaluator — staged form (a) → form (b) with mandatory (a) ≡ (b) equivalence and 100% decision parity against the shipped, untouched C# `PolicyMatcher` (contract C23). **(B)** Complete the 036 link's deferred acceptance: Erlang/Gleam Profile C in-process QUIC (delegated to gavri), two-host LAN end-to-end (this host + gavri), marathon durability verification on a real persisted run, and the four carried codexreview fixes (FR-015).

**Planning-time discovery (load-bearing)**: the typed-glp-manual §8 defined-guard mechanism named by the form-(a) ruling is a **compile-time single-unit-clause substitution** (`glp_runtime/lib/compiler/partial_evaluator.dart` Stage 1: exactly one clause, no guards, no body; non-reducible calls are a CompileError "Defined guards must be fully reducible at compile time"). A recursive, runtime, three-valued satisfiability test over runtime list data **cannot** be expressed by that mechanism as it stands. The plan therefore hard-gates all guard code behind a **realization-confirmation checkpoint with Gabi** (research.md R1) — the recorded ruling's signature/semantics/staging stand; only the concrete form-(a) mechanism needs his ruling. Everything in Deliverable B proceeds in parallel, unblocked.

## Technical Context

**Language/Version**: GLP (typed, SRSW) on the Dart `glp_runtime` (Dart SDK at `C:\src\flutter\bin\cache\dart-sdk\bin`, not on PATH — use the Bash tool); Dart 3.x for compiler/runtime changes (form (b), and form (a) if a compiler extension is ruled); C# / .NET 8 (`csharp/glp_crdtmsg`, `csharp/glp_quick_host` — the crdtmsg matcher is read-only reference); Python 3.11 (`glp_quick` control plane; buildkit CLIs via `D:\bstdev\research\buildkit\.venv313`); Erlang/OTP 28 + Gleam + rebar3 + `quicer` NIF (Profile C — provisioned on **gavri**, not this host).
**Primary Dependencies**: `glp_runtime` REPL pipeline (SRSW → PE → type check → compile → execute); shipped 036 artifacts unchanged (`glp-quick` CLI, `csharp/glp_quick_host`, cert workflow); `buildkit-marathon` + deploy-home PGlite catalog (verified, not modified); `PolicyMatcher.cs` (reference evaluator, not modified).
**Storage**: acceptance evidence as files under `specs/049-wave1-guard-link-acceptance/evidence/{guard,gavri,two-host,marathon}/`; marathon durable rows live in the out-of-repo deploy-home catalog (constitution VI-b exemption).
**Testing**: unified REPL suite `bash test/run_all_tests.sh` (baseline **524/525**; the 1 failure is a pre-existing, unrelated AOT-smoke case); `dotnet test` for `glp_crdtmsg.tests` (104 xUnit) and `glp_quick_host`; `pytest` for `glp_quick` (18); gleam/erlang tests on gavri.
**Target Platform**: Windows 11 (this host, Olamnit, LAN 192.168.0.143) + gavri (second LAN host, BEAM toolchain).
**Project Type**: multi-workstream — language-runtime extension (gated) + network-link acceptance + harness verification.
**Performance Goals**: N/A — correctness/acceptance feature. Pass criteria are the 036 quickstart SC lines and 100% vector parity.
**Constraints**: DISCIPLINE §1.14 hard gate (no guard code before the realization confirmation is recorded); ship gate = ALL FOUR user stories (Clarifications); `PolicyMatcher.cs` MUST NOT change (FR-006); shipped 036 protocol substrate unchanged; commit-scoped staging (no `git add -A`).
**Scale/Scope**: 1 guard (2 forms) + shared decision-vector set (~12–20 vectors) + 4 carried fixes + 3 acceptance campaigns (Profile C, two-host, marathon) + 1 delegation artifact.

## Constitution Check

*GATE: evaluated against constitution v1.1.0 before Phase 0; re-checked after Phase 1.*

| Principle | Verdict | Evidence in this plan |
|---|---|---|
| I. Spec-First | PASS | Proposal file quoted as the authoritative design artifact; the §8-mechanism inconsistency is routed to a spec/ruling clarification (R1) instead of code-first improvisation. |
| II. Bug-Protocol / No-Workarounds | PASS | Divergences ((a)≠(b), guard≠matcher) are defined as defects reported via the bug protocol before any fix (spec Edge Cases); FR-015 fixes are spec'd fixes with regression, not workarounds. |
| III. SRSW inviolable | PASS | All new GLP test programs are typed with procedure declarations and pass the SRSW stage of the REPL pipeline; no escape hatch is proposed. |
| IV-a. Language Authority | PASS (central) | FR-001 ruling recorded 2026-07-08; this plan adds a **realization-confirmation checkpoint** (R1) before any guard implementation/compile/run — strictly tighter than the gate, never looser. |
| IV-b. Preserve Working Internals | PASS | Form-(b)/compiler work is additive (new guard entry + dispatch); no removal of `_ClauseVar`/`_TentativeStruct`/fallback branches. |
| V. Claude-only LM | PASS | No LM-in-the-loop component in this feature. |
| VI-a. Additive-only migrations | PASS | No schema/migration changes. |
| VI-b. Single PGLite cluster | PASS | No new cluster; marathon per-run store is the recorded v1.1.0 exemption and is only read/verified. |
| VII. Test-gated, commit-scoped | PASS | Baseline suites green before change (524/525 REPL, C# suites), re-run after every change; ship via `buildkit ship` GitFlow. |
| VIII. SSOT & traceability | PASS | Guard design SSOT = `programs/crdtmsg/policy-guard-proposal.glp`; link acceptance SSOT = `specs/036-http3-quic-ws-link/quickstart.md`; wave→origin traceability table in spec; FR-014 roadmap advance at close. |

**Post-Phase-1 re-check**: PASS — design artifacts add no violations; the decision-vector contract keeps `PolicyMatcher.cs` untouched (vectors are consumed by a new test file, not by edits to the matcher).

## Project Structure

### Documentation (this feature)

```text
specs/049-wave1-guard-link-acceptance/
├── plan.md                          # This file
├── research.md                      # Phase 0 — R1..R6 decisions
├── data-model.md                    # Phase 1 — entities
├── quickstart.md                    # Phase 1 — per-deliverable verification runbook
├── contracts/
│   ├── guard-three-valued.md        # C24 successor: signature, outcomes, parity, equivalence
│   ├── decision-vectors.md          # shared vector schema + seed vector set
│   ├── gavri-delegation.md          # US2+US3 delegation contract (branch, evidence, done)
│   └── acceptance-evidence.md       # evidence record format (FR-013)
├── gavri-task-prompt.md             # FR-016 artifact (created by tasks/implement)
├── evidence/                        # FR-013 acceptance evidence (created during implement)
│   ├── guard/  ├── gavri/  ├── two-host/  └── marathon/
└── tasks.md                         # Phase 2 (/bk-tasks — NOT created by /bk-plan)
```

### Source Code (repository root)

```text
programs/crdtmsg/
├── policy-guard-proposal.glp        # SSOT design artifact (read-only until confirmation)
└── policy_guard.glp                 # form-(a) guard + types (NEW, gated on R1 confirmation)
programs/tests/typed/
└── policy_guard_*.glp               # FR-007 worked-example + vector regression programs (NEW, gated)

glp_runtime/lib/compiler/
├── partial_evaluator.dart           # form-(a) seat IF a PE/compiler extension is ruled (gated)
└── analyzer.dart                    # form-(b) guard registration (gated; additive)
glp_runtime/lib/bytecode/runner.dart # form-(b) three-valued evaluation (gated; additive)

csharp/glp_crdtmsg.tests/
└── PolicyVectorParityTests.cs       # NEW: drives PolicyMatcher.Evaluate over the shared vectors
csharp/glp_crdtmsg/route/PolicyMatcher.cs   # READ-ONLY (FR-006)

csharp/glp_quick_host/Program.cs     # FR-015 #3: duplicate endpoint_id eviction guard
glp_quick/demo.py                    # FR-015 #5: None recv → SC-001 FAIL record
glp_quick/stacks/csharp.py           # FR-015 #6: stdout reader attaches before readiness wait
gleam_quic/src/glpq_ffi.erl          # FR-015 #7: length-framed read (>1 MiB envelopes)

test/run_all_tests.sh                # unified suite — new Section A/B entries for guard programs
```

**Structure Decision**: no new projects. Deliverable A lives in `programs/crdtmsg` + `programs/tests/typed` (+ `glp_runtime` compiler/runtime only as ruled); parity tests live beside the existing xUnit suite; Deliverable B edits are point fixes in the four shipped 036 files plus evidence/documentation under this feature dir. US2+US3 execute on gavri on a branch off this feature branch per the delegation contract.

## Complexity Tracking

No constitution violations to justify — table intentionally empty.
