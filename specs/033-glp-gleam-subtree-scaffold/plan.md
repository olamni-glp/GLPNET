# Implementation Plan: glp_gleam subtree scaffold

**Branch**: `033-glp-gleam-subtree-scaffold` | **Date**: 2026-06-24 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/033-glp-gleam-subtree-scaffold/spec.md`

## Summary

Stand up `glp_gleam/` — a new, repo-root, **committed hand-authored** Gleam project skeleton
(sibling to `glp_runtime/` and `glp_runtime_net/`) that **builds to Erlang/BEAM and tests green
while containing no ported GLP runtime semantics** ("empty-but-building"). It is the buildable
home and known-good baseline the heavy downstream port features (F4–F9) land in.

Technical approach: replicate the F1 spike's **proven** standard-Gleam-project shape
(`docs/research/gleam-atomvm/hello-glp-term/`: `gleam.toml` + committed `manifest.toml` + `src/` +
`test/` + `.gitignore`), but namespaced as `glp_gleam` with one **empty-but-building placeholder
module per authoritative Dart subsystem** under `src/glp/` (1:1 with `glp_runtime/lib/`), a single
gleeunit smoke test, and a **local WSL-runnable smoke script** that gates `gleam build --target
erlang` + `gleam test`. Dependencies are pinned to the F1-ratified versions via a **committed
`manifest.toml`** (locking `gleam_stdlib` 1.0.3 / `gleam_erlang` 1.3.0 / `gleeunit` 1.11.0; the
disallowed `gleam_otp` is excluded by omission and verified absent transitively). Recognition by the
Dart→Gleam conversion data flow (FR-008) is achieved **purely through existing configuration**
(`codeconv init` → `codeconv.workspace_settings`; the `dart_gleam` langpair already exists from F2),
with **zero edits to any inventory/structure stage tool** (init/discover/scaffold/mirror).

## Technical Context

**Language/Version**: Gleam **1.17.0**, compiled to the **Erlang/BEAM** target; on Erlang/OTP
**25.3.2.8** (ERTS 13.2.2.5); Gleam built-in build tool + `rebar3` **3.19.0**.
**Primary Dependencies**: `gleam_stdlib` **1.0.3**, `gleam_erlang` **1.3.0** (runtime);
`gleeunit` **1.11.0** (dev/test). **Excluded by mandate**: `gleam_otp` (its `proc_lib` use is
outside AtomVM's BEAM subset — F1 §3).
**Storage**: N/A (build plumbing; no database, no migration).
**Testing**: `gleeunit` via `gleam test --target erlang` (≥1 passing test, 0 failures).
**Target Platform**: Erlang/BEAM (AtomVM-compatible subset; explicit AtomVM/JS targeting is
deferred to the heavy port features). **Build/test environment**: WSL Ubuntu 24.04 with the pinned
toolchain (verified reachable from this repo: `gleam 1.17.0`, OTP `25`, `rebar3` present).
**Project Type**: Gleam project **scaffold/skeleton** (a new repo-root subtree).
**Performance Goals**: N/A — the deliverable is an empty-but-building skeleton, not a runtime.
**Constraints**: build **and** test green on an empty module; `gleam_otp` absent from the committed
lock (SC-004); every module/namespace identifier a legal Gleam module-path segment (FR-006);
**additive-only** — zero change to any existing subtree's build/test/behavior (FR-009) and zero
codeconv stage-tool source change (FR-008/SC-006); committed dependency lock, ignored build
artifacts (FR-005/FR-010); the build runs **only under WSL** (Gleam is not installed Windows-native
in this environment).
**Scale/Scope**: one Gleam project; 8 subsystem placeholder modules; 1 smoke test; 1 smoke script;
~14 committed files. S-effort, low-risk (roadmap sizing).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

Constitution v1.1.0. This feature is build-plumbing/scaffolding — it ports **no** GLP semantics and
introduces **no** GLP clauses, no DB migration, no LM path, and no new PGLite cluster. Each
principle evaluated against this feature's `spec.md` (and this `plan.md`):

| # | Principle | Verdict | Basis |
|---|-----------|---------|-------|
| I | Spec-First | **PASS** | `spec.md` exists, is clarified (3 Q/A 2026-06-24), and is consistent with its authoritative sources (F1 dossier §6; F2 spec). This plan derives from it. |
| II | Bug-Protocol / No-Workarounds | **PASS (N/A)** | No bug being routed around; no try/catch "robustness". |
| III | SRSW inviolable | **PASS (N/A)** | No GLP clauses authored. Zero `skipSRSW` tokens in the artifacts. |
| IV-a | Language Authority | **PASS (N/A)** | No GLP guard/predicate/kernel/directive/type-system change (spec Assumption "No GLP language change"). |
| IV-b | Preserve Working Internals | **PASS (N/A)** | Removes/modifies no existing code (`_ClauseVar`/`_TentativeStruct` untouched). Additive-only (FR-009). |
| V | Claude-Only LM / No External API | **PASS (N/A)** | No LM-in-the-loop path. Zero `OPENAI_API_KEY`/`litellm`/`openai` tokens. |
| VI-a | Additive, idempotent, single-head migrations | **PASS (N/A)** | No DB migration; head stays `0010`. |
| VI-b | Single PGLite cluster | **PASS (N/A)** | No new cluster; uses no working-data cluster. |
| VII | Test-Gated, Commit-Scoped Shipping | **PASS** | Plan commits only feature-033 files by name; ships via buildkit GitFlow (not hand-merge). The new WSL smoke gate (FR-007) extends the test discipline. |
| VIII | Single Source of Truth & Traceability | **PASS** | `spec.md` references the F1 dossier and F2 spec rather than duplicating them; traced roadmap (F3) → pipeline (033) → tasks. |

**Result: no violations.** Complexity Tracking is therefore empty.

## Project Structure

### Documentation (this feature)

```text
specs/033-glp-gleam-subtree-scaffold/
├── spec.md              # Feature spec (input)
├── plan.md              # This file (/bk-plan output)
├── research.md          # Phase 0 output — decisions R-001..R-006
├── data-model.md        # Phase 1 output — entities & validation rules
├── quickstart.md        # Phase 1 output — build/test the subtree (SC-001/SC-002)
├── contracts/           # Phase 1 output — layout / build-test-smoke / deps / recognition
│   ├── project-layout.md
│   ├── build-test-smoke.md
│   ├── dependency-lock.md
│   └── conversion-recognition.md
└── checklists/          # (pre-existing, from /bk-specify)
```

### Source Code (repository root)

A single new repo-root subtree; no other source tree is touched (FR-009).

```text
glp_gleam/                         # NEW — committed, hand-authored Gleam project (FR-001)
├── gleam.toml                     # project metadata + dep ranges (FR-001/FR-005)
├── manifest.toml                  # COMMITTED lock — pins exact versions (FR-005/FR-010)
├── .gitignore                     # *.beam, *.ez, /build, erl_crash.dump (FR-010)
├── README.md                      # one-screen: purpose + build/test commands + dossier pointer
├── smoke.sh                       # local WSL gate: toolchain check + build + test (FR-007)
├── src/
│   └── glp/                       # source namespace (dossier §6)
│       ├── analysis.gleam         # ┐
│       ├── bytecode.gleam         # │
│       ├── compiler.gleam         # │ one empty-but-building placeholder per
│       ├── engine.gleam           # │ authoritative Dart subsystem — 1:1 with
│       ├── link.gleam             # │ glp_runtime/lib/ (FR-004/SC-003)
│       ├── lint.gleam             # │
│       ├── multiagent.gleam       # │
│       └── runtime.gleam          # ┘
└── test/
    └── glp_gleam_test.gleam       # gleeunit smoke: ≥1 passing test (FR-003/SC-002)
```

Existing siblings (read-only context, unchanged): `glp_runtime/` (Dart source-of-truth),
`glp_runtime_net/` (gitignored Dart mirror INPUT), `out/csharp/` (committed C# mirror output),
`codeconv/` (the conversion toolchain; the `dart_gleam` langpair already lives at
`codeconv/src/codeconv/langpairs/dart_gleam/`).

**Structure Decision**: A standard single-project Gleam layout placed at the repo root as a
first-class sibling subtree, exactly as dossier §6 prescribes ("Place `glp_gleam/` as a repo-root
subtree, sibling to `glp_runtime/` and `glp_runtime_net/`"). The 8 placeholder modules sit under the
`glp` source namespace (`src/glp/<subsystem>.gleam`) so each downstream port maps 1:1 onto a Dart
subsystem with no restructuring.

## Complexity Tracking

> No Constitution Check violations — this table is intentionally empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| _(none)_  | —          | —                                   |
