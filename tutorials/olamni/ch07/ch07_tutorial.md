# Chapter 7 — Module System

**Date**: 2026-05-04 · **Branch**: `008-tutorial-ch07` · **Charter**: [`../charter.md`](../charter.md) §2.2

Chapter 7 introduces the GLP module system: how a multi-module project (a directory of `.glp` files with cross-module imports and ancestor-scoped types) becomes a single loaded artefact at the REPL prompt, with the algorithmic helpers and protocol entry points all callable by their bare names. This chapter's runnable substrate is the §7.7 Child-Safe Social Graph (CSSG) validation example at `programs/cssg_modules/` — four modules (`agent.glp`, `ui/mediator.glp`, `ui/actors.glp`, `boot.glp`, plus the shared `self.glp`) implementing seven plays that walk the protocol's accept/reject decision tree.

## Exercises — one play per exercise

Each of the seven plays is its own exercise. Each exercise is a multi-goal interactive REPL walkthrough that recreates each component of the play's body individually with constructed inputs, observes the bindings produced at each step, then runs the full play and walks through the resulting tagged-output stream line by line.

- [`exercise-01/`](exercise-01/ex-01-tutorial.md) — **fplay1**: cold-call befriending + friend-mediated introduction (both accept). Three pairs become friends and exchange greetings via Bob's introduction. 25-line tagged choreography.
- [`exercise-02/`](exercise-02/ex-02-tutorial.md) — **fplay2**: Charlie rejects the introduction. Alice's independent accept is overridden by Charlie's reject; Alice receives a `rejected(charlie)` notification. 20-line tagged stream.
- [`exercise-03/`](exercise-03/ex-03-tutorial.md) — **fplay3**: both Alice and Charlie reject the introduction. Neither side receives a `rejected(...)` notification because each rejected on their own. 19-line tagged stream.
- [`exercise-04/`](exercise-04/ex-04-tutorial.md) — **fplay4**: CSSG parent-mediated child introduction (all four accept). Carol and Dave become friends through both parents' approvals + both children's consents. 18-line tagged stream.
- [`exercise-05/`](exercise-05/ex-05-tutorial.md) — **fplay5**: Bob rejects the child introduction (parental veto). The protocol blocks at the parental approval step; Dave never receives the request. 11-line tagged stream.
- [`exercise-06/`](exercise-06/ex-06-tutorial.md) — **fplay6**: Carol rejects the child introduction (child veto on the initiator's side). Bob approves but Carol blocks on her own side; Dave still receives the request via Bob's forwarded approval and accepts, then learns of Carol's reject. 13-line tagged stream.
- [`exercise-07/`](exercise-07/ex-07-tutorial.md) — **fplay7**: Dave rejects the child introduction. Symmetric mirror of fplay6, with Carol receiving the `rejected(dave)` notification. 13-line tagged stream.

## Open the REPL

```bash
cd D:/bstdev/research/GLP/GLP
"/c/Users/gavri/dart-sdk/bin/dart" run glp_runtime/.dart_tool/repl.dill
```

(On macOS / Linux the dart binary is just `dart`.) At the `GLP>` prompt, paste the project directory:

```
GLP> D:/bstdev/research/GLP/GLP/programs/cssg_modules
✓ Loaded project: D:/bstdev/research/GLP/GLP/programs/cssg_modules
```

A single `✓ Loaded project:` line covers SRSW analysis + partial evaluation + type checking + bytecode compilation across all four modules and resolves the `imported procedure` declarations between them. Every exercise opens with this same load step.

## Exercise status

- exercise-01: approved 2026-05-04
- exercise-02: approved 2026-05-04
- exercise-03: approved 2026-05-04
- exercise-04: approved 2026-05-04
- exercise-05: approved 2026-05-04
- exercise-06: approved 2026-05-04
- exercise-07: approved 2026-05-04

## Predecessors

- ch01 — Introduction (Fair Stream Merger)
- ch02 — Logic Programs and Linear Logic
- ch03 — GLP Core
- ch04 — Basic Concurrent Programming
- ch05 — Types and Modes
- ch06 — Typed Programming

## Successors

- ch08 — The Grassroots Social Graph (planned; will use ch07's module system at scale)
- ch09–ch13 (planned)

## Stale prior-implementation artefacts (preserved per no-removal directive)

The prior ch07 implementation at `26e01792` (2026-05-02) and `f094f9db` (2026-05-03) created the following additional artefacts that the v2026.05.04 remediation supersedes but preserves. These are NOT part of this chapter's runnable content:

- `exercise-08/` through `exercise-12/` — directories with prior tutorial content (cluster B exercises in the abandoned cluster A/B framing); superseded by ex-01..ex-07's one-play-per-exercise structure. Disposition pending.
- `simple-multimodule/` and `cssg-modules/` subdirectories — derivative copies of `programs/cssg_modules/`; the canonical project is now the chapter's single source of truth.
- `glp_multiagent/lib/main_olamni_ch07_simple_multimodule.dart` and `main_olamni_ch07_cssg.dart` — Flutter pairings tied to the derivative subdirectories; build-verified but not used by the current exercise set.
- `test/run_all_tests.sh` Section R — tests the cluster A/B file copies via byte-equivalence diffs; redundant given the canonical project is the chapter's source. Disposition pending.
- `specs/008-tutorial-ch07/` — spec/plan/research artefacts for the prior implementation; preserved as record of the rejected approach.
- `ch07-sources.md`, `ch07-specification-input-prompt.md`, `spec-rev-eng-input/` — prior implementation's artefacts; preserved as record.

The git history at `26e01792` and `f094f9db` carries the full prior content. The new exercise standard validated by the project owner on 2026-05-04 is captured in `<user-memory-dir>/tutorial_exercise_standard.md`.
