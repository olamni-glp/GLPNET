# Feature Specification: glp_gleam subtree scaffold

**Feature Branch**: `033-glp-gleam-subtree-scaffold`
**Created**: 2026-06-24
**Status**: Draft
**Input**: User description: "glp_gleam subtree scaffold"

**Epic**: Gleam AtomVM (`gleam-atomvm`) — feature F3 (roadmap rank 3), blocked-by F1 `gleam-port-source-and-toolchain-spike` (shipped `v2026.06.22.1`) and F2 `codeconv-gleam-langpair` (shipped `v2026.06.24.1`).
**Authoritative references**:
- F1 handoff: `docs/research/gleam-atomvm/dossier.md` §6 ("Downstream handoff for F2/F3") — prescribes the `glp_gleam/` project layout, conventions, and pinned toolchain versions; explicitly states "F3 creates this subtree — not this spike".
- F2 spec: `specs/032-codeconv-gleam-langpair/spec.md` — the Dart→Gleam langpair is layout-agnostic and defers the `glp_gleam/` project layout to F3 (FR-003, Clarifications 2026-06-22).

## Clarifications

### Session 2026-06-24

- Q: Is `glp_gleam/` committed hand-authored source (the F4+ port destination, like `out/csharp/` is committed/reviewable) or a gitignored regenerable subtree the codeconv mirror produces (like `glp_runtime_net/`)? → A: **Committed, hand-authored source** — the reviewable home the downstream port lands in; the codeconv Dart→Gleam mirror sits *alongside* it and does not generate it. Only build/output artifacts inside the subtree are gitignored. (Resolves the roadmap brief's "codeconv mirror INPUT; mirroring glp_runtime_net" wording in favour of the dossier's "committed manifest / F3 creates this subtree".)
- Q: What shape is the "CI smoke" given the repo has no CI infrastructure (no GitHub Actions; everything gates locally via the bash REPL suite + codeconv pytest + buildkit preflight; Gleam builds only under WSL per F1)? → A: **A local, WSL-runnable smoke script** (`gleam build --target erlang` + `gleam test`) wired into the existing local-gate convention — NOT a new remote CI pipeline. Standing up remote CI (e.g. GitHub Actions) exceeds F3's S-effort/low-risk sizing and can be a later feature.
- Q: Placeholder modules for all 8 authoritative Dart subsystems, or the 6 named "e.g." in dossier §6? → A: **All 8** (`analysis`, `bytecode`, `compiler`, `engine`, `link`, `lint`, `multiagent`, `runtime`) — a clean 1:1 with the Dart source-of-truth so downstream ports never need to add structure.

## User Scenarios & Testing *(mandatory)*

The "users" are the GLP maintainers driving the Gleam port (the roadmap's "port effort"). F3 stands up the **empty-but-building** `glp_gleam/` project skeleton into which the heavy downstream port features (F4 core terms/heap, F5 runner, F6 compiler/loader, F7 REPL, F8 test corpus, F9 link layer) land. It ports **no** runtime semantics itself — it is the buildable home and green baseline everything else builds on.

### User Story 1 - A buildable, testable Gleam subtree exists (Priority: P1)

A maintainer checks out the feature branch on a machine with the pinned toolchain, builds the new `glp_gleam/` subtree to the Erlang/BEAM target, and runs its test suite — both succeed even though no GLP runtime code has been ported yet.

**Why this priority**: This is the literal acceptance gate the roadmap states — "gleam build->erlang and gleam test green on an empty module." Without a building skeleton and a green baseline, no downstream port feature has a place to land or a known-good starting point. Delivering just this story is the viable MVP.

**Independent Test**: From a fresh checkout, run the documented build command targeting Erlang/BEAM and the documented test command; both complete with zero errors and at least one passing test, with no ported runtime code present.

**Acceptance Scenarios**:

1. **Given** a fresh checkout of the feature branch on the pinned toolchain, **When** the maintainer builds `glp_gleam` to the Erlang/BEAM target, **Then** the build completes with zero errors.
2. **Given** the built subtree, **When** the maintainer runs its test suite, **Then** the smoke test passes (at least one test, zero failures).
3. **Given** the committed dependency lock, **When** dependency resolution runs, **Then** it resolves exactly the F1-pinned versions and does not pull in the disallowed OTP-actor library.

---

### User Story 2 - The skeleton mirrors the authoritative Dart subsystem structure (Priority: P2)

The source layout provides one placeholder module per authoritative Dart runtime subsystem, organized under the project's source namespace, so that each downstream port maps onto exactly one module with no restructuring.

**Why this priority**: Correct, 1:1-with-Dart structure makes the heavy downstream ports mechanical instead of corrective and keeps the Gleam tree aligned with the Dart source-of-truth. It builds directly on US1's buildable project.

**Independent Test**: Enumerate the subtree's modules and confirm there is exactly one placeholder module per authoritative Dart subsystem under the project namespace, and that each placeholder compiles as part of the US1 build.

**Acceptance Scenarios**:

1. **Given** the authoritative Dart subsystem set (`analysis`, `bytecode`, `compiler`, `engine`, `link`, `lint`, `multiagent`, `runtime`), **When** the skeleton's module layout is inspected, **Then** there is a corresponding placeholder Gleam module for each subsystem under the project's source namespace.
2. **Given** each placeholder module, **When** the subtree builds, **Then** every placeholder compiles cleanly (empty-but-building).
3. **Given** the project metadata, **When** it is inspected, **Then** the project name, source namespace, and repo-root placement (sibling to `glp_runtime/` and `glp_runtime_net/`) follow the dossier §6 conventions.

---

### User Story 3 - A smoke gate exists and the conversion tooling recognizes the subtree (Priority: P3)

A local, WSL-runnable smoke script builds and tests `glp_gleam`, wired into the repo's existing local-gate convention (the bash REPL suite / codeconv pytest / buildkit preflight), and the subtree is positioned/tracked as a first-class subtree of the Dart→Gleam conversion data flow (sibling to `glp_runtime/` and `glp_runtime_net/`) — without modifying any codeconv inventory/structure stage tool.

**Why this priority**: The smoke gate protects the green baseline from regressions as the heavy ports land, and a recognized placement gives the F2 langpair's output a home. Valuable, but the build/test gate (US1) already delivers the core value, so this is P3.

**Independent Test**: Run the smoke script under WSL after a change under `glp_gleam/` and confirm it builds to the Erlang/BEAM target and runs the test suite, gating on green; confirm the conversion pipeline recognizes the subtree with no stage-tool source change.

**Acceptance Scenarios**:

1. **Given** a change under `glp_gleam/`, **When** the smoke script runs (under WSL, on demand or via the repo's existing local gate), **Then** it builds the subtree to the Erlang/BEAM target and runs its tests, and gates on a green result.
2. **Given** the Dart→Gleam conversion pipeline, **When** it targets the Gleam subtree, **Then** `glp_gleam/` is recognized as a first-class conversion subtree without any inventory/structure stage-tool source change (preserving F2's plugin boundary).

---

### Edge Cases

- **Toolchain absent or wrong version**: the build environment lacks Gleam/Erlang, or has non-pinned versions. The smoke must fail loudly with an actionable message naming the required versions — never silently pass against an unexpected toolchain.
- **Disallowed dependency creeps in**: a transitive pull of the OTP-actor library (`gleam_otp`, whose `proc_lib` use is outside AtomVM's subset) would erode AtomVM viability. The committed lock/manifest must keep it absent.
- **Placeholder imported but unused**: an empty subsystem placeholder that nothing references yet must still compile (placeholder discipline — empty-but-building, not empty-and-dangling).
- **Illegal module/namespace segment**: any segment that is not a legal Gleam module path identifier must be avoided in the skeleton itself, consistent with the F2 langpair's normalization rules — the skeleton never contains an illegal path.
- **Wrong build target**: building to the JavaScript target (only partially viable per F1) is out of scope; the smoke gates only the Erlang/BEAM target.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The repository MUST contain a new `glp_gleam/` subtree at the repository root, a sibling of `glp_runtime/` and `glp_runtime_net/`, holding a standard Gleam project: project-metadata file, a committed dependency lock/manifest, a source directory, and a test directory.
- **FR-002**: The subtree MUST build successfully to the Erlang/BEAM target using the pinned toolchain, with no ported GLP runtime semantics present ("empty-but-building").
- **FR-003**: The subtree MUST include a runnable test suite with at least one passing smoke test (≥1 test, 0 failures) that exercises the build-and-run path.
- **FR-004**: The source layout MUST provide a placeholder module for each authoritative Dart runtime subsystem — `analysis`, `bytecode`, `compiler`, `engine`, `link`, `lint`, `multiagent`, `runtime` — organized under the project's source namespace, so downstream ports map 1:1 onto the Dart source-of-truth.
- **FR-005**: The dependency set MUST be pinned to the F1-ratified versions (Gleam 1.17.0, Erlang/OTP 25.3.2.8, and the named library dependencies `gleam_stdlib`, `gleam_erlang`, `gleeunit`) and MUST exclude the OTP-actor library (`gleam_otp`); the dependency lock/manifest MUST be committed for reproducible builds.
- **FR-006**: All module and namespace identifiers in the skeleton MUST be legal Gleam module path segments, consistent with the F2 langpair's normalization rules — the skeleton itself MUST NOT contain an illegal path.
- **FR-007**: A **local, WSL-runnable smoke script** MUST build the subtree to the Erlang/BEAM target and run its test suite, returning a green/red result, and MUST be wired into the repo's existing local-gate convention (the bash REPL suite / codeconv pytest / buildkit preflight). Standing up a remote CI pipeline (e.g. GitHub Actions) is OUT OF SCOPE for F3 (the repo has no CI infrastructure today; that is a candidate later feature).
- **FR-008**: The subtree MUST be positioned and tracked as a first-class subtree of the Dart→Gleam conversion data flow (the codeconv mirror, mirroring how `glp_runtime_net`/`out/csharp` participate for the C# pipeline), such that downstream codeconv stages recognize it WITHOUT modifying any inventory/structure stage-tool source (init, discover, scaffold, mirror) — preserving F2's plugin boundary.
- **FR-009**: Creating the subtree MUST be additive only — it MUST NOT change the build, test, or behavior of any existing subtree (`glp_runtime/`, `glp_runtime_net/`, `out/csharp/`, `codeconv/`).
- **FR-010**: Committed state MUST exclude build/output artifacts (compiled BEAM, build caches) via appropriate ignore rules, committing only source, project metadata, and the dependency lock.

### Key Entities *(include if feature involves data)*

- **`glp_gleam` subtree**: the new repo-root Gleam project — the buildable home and green baseline for the entire downstream port.
- **Project metadata + dependency lock**: the pinned, committed build configuration that makes builds reproducible and keeps the disallowed dependency out.
- **Subsystem placeholder modules**: one per authoritative Dart subsystem; the destinations into which F4+ ports land.
- **Smoke test**: the minimal green build-and-run proof.
- **Smoke script**: the local WSL-runnable build-and-test gate (FR-007) protecting the green baseline.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: From a clean checkout on the pinned toolchain, a single documented build command produces a successful Erlang/BEAM build of `glp_gleam` with zero errors.
- **SC-002**: A single documented test command runs the subtree's suite green — at least one test, zero failures.
- **SC-003**: 100% of the 8 authoritative Dart subsystems have a corresponding placeholder module in the skeleton, and 100% of those placeholders compile.
- **SC-004**: Dependency resolution uses only the pinned versions, and the disallowed OTP-actor dependency is absent from the committed lock (0 occurrences).
- **SC-005**: The local WSL-runnable smoke script gates a change under `glp_gleam/` on a green Erlang/BEAM build plus passing tests, and is wired into the repo's existing local-gate convention.
- **SC-006**: Introducing the subtree changes zero existing-subtree build/test outcomes and zero codeconv stage-tool source files.

## Assumptions

- **Committed source, not regenerable snapshot** *(decided — Clarifications 2026-06-24)*. `glp_gleam/` is hand-authored, **committed** source (like `out/csharp/` is committed and reviewable), not gitignored like the regenerable `glp_runtime_net/`. The dossier prescribes a *committed* manifest, and a hand-built skeleton is reviewable source. Build artifacts inside it are ignored (FR-010).
- **Build/test runtime is plain Erlang/BEAM.** Plain BEAM is the F1-proven test runtime and the build/test target for this skeleton. AtomVM-specific build/CI is out of scope for F3 — F1 already proved AtomVM viability, and AtomVM targeting lands with the heavy runtime features. The JavaScript target is out of scope (only partially viable per F1).
- **Project name and namespace.** The project is named for the Gleam port (working name `glp_gleam`) with modules under a `glp` source namespace (e.g. `src/glp/<subsystem>`), per dossier §6. Exact names are an implementation detail confirmed at plan time and need only satisfy FR-006.
- **"Stage sidecars / codeconv mirror INPUT" scope** *(narrowed — Clarifications 2026-06-24)*. The committed-vs-regenerable question is now decided (committed; the mirror sits alongside, does not generate the subtree). F3 establishes the subtree as a *recognized* conversion subtree at the lightweight level the pipeline needs to see it (placement plus any per-subtree tracking that mirrors how `glp_runtime_net`/`out/csharp` participate), WITHOUT stage-tool edits (F2 boundary). Pipeline integration beyond "recognized + build/test green" is deferred to the heavy port features; F3's hard gate remains build-and-test green on an empty module via the local WSL smoke script.
- **Dev environment.** Linux/WSL Ubuntu with the pinned toolchain, per F1 (native-Windows Gleam is viable for a developer with admin rights but was not exercised in the spike); the smoke script (FR-007) runs under WSL with the same pinned toolchain.
- **No GLP language change.** This is project scaffolding and build plumbing — no new GLP primitives, guards, or runtime semantics are involved (those are F4+).
- **Dependency on F1 and F2.** F3 consumes F1's ratified source-basis decision (Dart `glp_runtime/`), pinned toolchain, and proven Gleam project conventions, and is positioned as the layout target the F2 Dart→Gleam langpair deliberately left to F3.
