# Phase 0 Research: Gleam Port Source & Toolchain / AtomVM Spike

**Feature**: 031-gleam-port-spike | **Date**: 2026-06-22

This file resolves the *how-do-we-run-the-spike* unknowns. It does **not** pre-empt the spike's findings — the dossier produced during `/bk-implement` is where verdicts get made with evidence. Each decision below is recorded as **Decision / Rationale / Alternatives considered**, per the plan workflow.

There were **no `[NEEDS CLARIFICATION]` markers** in `spec.md` (the requirements checklist confirms this); the three open execution choices it left to planning are the AtomVM-effort, dev-environment, and term-modelling decisions, resolved here from the spec's own Clarifications (2026-06-22) and Assumptions.

---

## R1 — Development environment: Windows-first, documented fallback

**Decision**: Attempt the full toolchain on the **primary Windows environment first**. Gleam and Erlang/OTP both ship first-class Windows binaries, so US2 (toolchain + BEAM smoke) is expected to succeed natively. Treat **AtomVM host bring-up** (US3) as the step most likely to require a fallback; if it (or anything else) cannot be stood up on Windows within budget, fall back to **WSL/Linux** (same machine) and, only if that also fails, the **sibling Mac**. The environment actually used is recorded in the toolchain inventory as a first-class field.

**Rationale**: Matches the spec Assumption ("targets the project's primary environment (Windows) first; … falling back to a documented alternative … is acceptable, and the environment used is recorded") and Edge Case ("toolchain will not install on the primary (Windows) environment → records this as a first-class finding, falls back …"). WSL is the lowest-friction fallback because AtomVM's prebuilt/generic host releases and source build are Linux-centric.

**Alternatives considered**: (a) WSL/Linux from the start — rejected: hides whether the primary environment is viable, which is itself a downstream-relevant finding (FR-007, dev-environment constraint). (b) Sibling Mac from the start — rejected: furthest from the daily environment; reserve as last resort.

---

## R2 — Toolchain acquisition & version pinning

**Decision**: Install via **official prebuilt binaries**, pinning **exact versions** at execution time and recording them. Acquisition order on Windows: Gleam from its official GitHub release (or `scoop`/`choco`); Erlang/OTP from the official installer (or `scoop`/`choco`). On the Linux/Mac fallback, prefer a version manager (`asdf` with the `gleam` + `erlang` plugins, or `mise`). Record the resolved `gleam --version`, `erl -version`/`erl +V`, `rebar3 version`, and OS/arch verbatim in the inventory.

**Rationale**: FR-003 requires "the exact tool versions and the reproducible setup, build, and run commands." Prebuilt binaries are the most reproducible and lowest-effort path; a version manager makes the fallback environment equally pin-able. Pinning is deferred to execution because "latest stable" must be the version actually observed, not a guess made at planning time.

**Alternatives considered**: (a) Build Gleam/Erlang from source — rejected: unnecessary effort; prebuilt binaries are authoritative. (b) Docker image — rejected for the primary path (adds a layer that obscures the native-Windows viability finding) but acceptable as an explicitly-noted Linux fallback if native install fails.

---

## R3 — AtomVM acquisition: effort-bounded, prebuilt-first (FR-005)

**Decision**: Pursue AtomVM in this strict order, stopping at the first that works:
1. **Prebuilt / generic host release** of AtomVM (its GitHub releases / generic_unix build).
2. **Time-boxed source build** of the AtomVM host (`generic_unix`) — only if no prebuilt host build runs. The time-box is **bounded** (target ≤ ~half a day of build/troubleshooting effort; the exact budget is recorded with the attempt).
3. If neither yields a runnable AtomVM host build within budget, **record the bring-up blocker** (the specific install/build failure) as the AtomVM matrix row's evidence — *not* merely a subset-limit guess.

No embedded hardware (ESP32 etc.) is in scope; the verdict is measured on a **host/generic build**, with any "would differ on real hardware" caveat noted but non-blocking.

**Rationale**: Verbatim from the 2026-06-22 clarification and FR-005: "prefer a prebuilt/generic AtomVM host release; only a time-boxed source-build attempt if no prebuilt works; if no AtomVM host build can be stood up within that budget, record the bring-up blocker … AtomVM bring-up is not unbounded spike scope." The Assumption confirms host/generic build (no hardware).

**Alternatives considered**: (a) ESP32/hardware flashing — rejected: out of scope, unbounded effort. (b) Skip AtomVM if no prebuilt — rejected: FR-005 mandates *attempting*, and even a bring-up blocker is a recorded verdict-with-evidence (US3 independent test).

---

## R4 — Representative GLP term & the unbound→bound demonstration (FR-004, FR-006)

**Decision**: The hello-GLP-term smoke models a **bounded but representative** term: one **compound/structure** with a ground argument and one **unbound logic-variable cell** (e.g., a `pair(label, Var)`-shaped value where `Var` starts unbound), plus exactly **one** unbound→bound transition where a separate **reader observes** the cell becoming bound.

Two modelling options exist for the mutable cell; the smoke uses the **process/state-holder model** (a BEAM process — a Gleam `gleam_otp`/`gleam_erlang` actor holding the cell's state, with a "reader" process that observes the value after a "writer" message binds it). A small **functional sibling** (the same one-bind transition expressed as immutable state threaded through a fold/pipeline) is included alongside it for contrast.

**Rationale**: FR-004 permits "a process/state-holder or a functional approach"; SC-006 requires the dossier to evidence **both** the mutable-heap/immutability mismatch **and** the FCP-concurrency/BEAM-process fit. The process/state-holder model is chosen as the primary because a BEAM-process cell directly exercises the concurrency/message-passing axis (giving SC-006's second finding running evidence), while still demonstrating the mutable-variable mechanism the architectural-fit risk is about. The functional sibling makes the immutability contrast explicit and cheap. The single bind is the **bounded** demonstration — full unification, suspension/reactivation scheduling, and bytecode execution remain out of scope (Assumptions).

**Alternatives considered**: (a) Functional-only — rejected as primary: weakest evidence for the BEAM-process fit (SC-006). (b) ETS / mutable table — rejected: ETS is exactly the kind of BEAM feature AtomVM may not fully support; using it would conflate the bind demo with an AtomVM-subset risk. The process/state-holder model leans on core BEAM message passing, which is the most portable substrate. (c) Full unification of two terms — rejected: explicitly out of scope, would balloon the spike.

---

## R5 — Source-basis comparison method (FR-001) and the discovered source asymmetry

**Decision**: The source-decision section ranks the three candidates against four stated criteria — **source health & currency**, **structural fit to Gleam**, **conversion effort**, and **divergence between the two sources** — using evidence gathered by inventorying every candidate root:
- **Dart**: `glp_runtime/` (the authoritative reference; ~151 `.dart` files under `lib/{analysis,bytecode,compiler,engine,link,lint,multiagent,runtime}`, last touched 2026-06-08).
- **C#**: this is **multi-rooted** and the spike must disentangle it — `glp_runtime_net/` (a hand-written .NET port with its own `glp_repl.exe`), `csharp/` (feature components only: `glp_il_codec` from 029, `glp_link` from 025), and `out/csharp/` (the **regenerable** `codeconv scaffold` mirror of the Dart input, ~90 `.cs`).

**Recorded planning observation (a hypothesis the spike must confirm, not a verdict)**: the C# surface appears **non-uniform** — a hand-port (`glp_runtime_net`) plus narrow feature modules (`csharp/`) plus a generated mirror (`out/csharp`) — whereas the Dart surface is a single coherent reference tree. This is precisely the "the two candidate sources have diverged" edge case the dossier must surface as a first-class criterion. The roadmap's "initial lean toward the C# source" is treated as a **prior to confirm or overturn by evidence**, not a foregone conclusion (Assumptions).

**Rationale**: FR-001 requires "the decision criteria and the rationale"; the edge cases require surfacing divergence "explicitly as a criterion … rather than assuming parity." Grounding the comparison in the actual repo layout (rather than the spec's shorthand "the `csharp/` runtime") prevents the spike from starting against the wrong root.

**Alternatives considered**: (a) Treat `csharp/` as "the C# runtime" per the spec's Dependencies shorthand — rejected: the repo shows `csharp/` is feature components, not a runtime; the hand-port lives in `glp_runtime_net/`. The spike must inventory all three roots and state which it treats as the C# candidate. (b) Compare only file counts — rejected: currency, structural fit, and divergence are not captured by counts; the criteria table needs qualitative evidence too.

---

## R6 — Architectural-fit axes to assess (FR-006, FR-007, SC-006)

**Decision**: The architectural-fit section assesses at least these axes, each with its bearing on the recommendation stated:
1. **Mutable heap / WAM-style cells vs Gleam immutability** — the top risk; backed by the **running** unbound→bound demonstration (R4), not analysis alone (SC-006 mandate). Options recorded: functional heap model vs process/state-holder ("variable = process") model.
2. **FCP concurrency / single-reader-single-writer & suspension-reactivation vs BEAM processes + message passing** — assessed as the top *opportunity* (BEAM's process model is a natural fit for FCP-style concurrency), with the bind demo's process model as running evidence.
3. **WAM-style bytecode execution & custom heap vs what AtomVM's BEAM/OTP subset supports** — feeds the AtomVM verdict and the downstream re-scope notes for the heavy features (bytecode runner F5, compiler/loader F6, link layer F9).

For FR-007 specifically: the section names which heavy downstream features each finding could force to **re-split or re-scope**, in roadmap-actionable form, and SC-005 requires every such feature to be named with a recommended re-scope (or explicitly confirmed unchanged).

**Rationale**: Directly from FR-006/FR-007 and SC-006. Anchoring the mutable-heap finding to the smoke (not prose) is the spec's explicit requirement and the reason the smoke exists.

**Alternatives considered**: Analysis-only architectural assessment — rejected: SC-006 forbids it for the mutable-heap finding.

---

## R7 — Evidence & reproducibility convention (FR-009, SC-002, SC-003)

**Decision**: Every "it works" / feasibility claim records **`command` → observed `output`** (with the exact tool versions and environment), or an **authoritative citation** (Gleam docs, Erlang/OTP docs, AtomVM docs/release notes) where a claim is documentary rather than observed. The toolchain inventory carries the canonical setup/build/run command block; the smoke's `README.md` carries the per-runtime observed output (BEAM result, AtomVM attempt result-or-blocker); the build-target matrix's every cell cites one of these. **No matrix cell is left "unknown" without a recorded reason** (SC-003).

**Rationale**: FR-009 ("backed by reproducible evidence — a command plus its observed output, or a citation … rather than by assertion"), SC-002 (second-person reproducibility), SC-003 (no unexplained "unknown").

**Alternatives considered**: Asserted verdicts with prose justification — rejected outright by FR-009.

---

## R8 — Downstream consumability (FR-008, SC-004)

**Decision**: The dossier ends with a **"For downstream features" handoff block** stating, in a form F2/F3 can consume without re-litigation: (a) the chosen **source basis**; (b) the assumed **`glp_gleam/` project layout/conventions** (Gleam project shape, module naming, where the subtree will live); and (c) the **toolchain versions** F2/F3 will build against. These mirror the smoke's own project conventions so the langpair/scaffold features inherit a proven layout.

**Rationale**: FR-008 enumerates exactly these three handoff items; SC-004 requires "zero re-litigation of the source decision after ratification."

**Alternatives considered**: Leave layout/conventions to F3 — rejected: FR-008 requires this spike to *state* them so F2/F3 can start.

---

## Resolved unknowns summary

| Unknown (from Technical Context) | Resolution |
|---|---|
| Dev environment | R1 — Windows-first, WSL/Linux→Mac fallback, env recorded |
| Toolchain versions | R2 — pinned at execution from official prebuilt binaries |
| AtomVM acquisition & effort | R3 — prebuilt → time-boxed source → record blocker (bounded) |
| Term modelling / mutable cell | R4 — process/state-holder primary + functional sibling; one bind |
| Source candidates & method | R5 — inventory all roots; 4 criteria; surface divergence |
| Architectural-fit axes | R6 — heap/immutability (smoke-backed), concurrency/BEAM, AtomVM-subset |
| Evidence standard | R7 — command+output or citation; no unexplained "unknown" |
| Downstream handoff | R8 — source basis + layout + versions block |

All resolved — ready for Phase 1.
