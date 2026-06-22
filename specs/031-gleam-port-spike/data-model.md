# Phase 1 Data Model: Spike Deliverable Entities

**Feature**: 031-gleam-port-spike | **Date**: 2026-06-22

For a research/decision spike the "data model" is the set of **deliverable entities** the dossier and smoke are made of, their fields, their relationships, and the **validation rules** (= the FR/SC each must satisfy). These map 1:1 to the spec's Key Entities. No database, no runtime types — these govern the *documents and the smoke project*.

---

## E1 — Decision Dossier

The spike's primary deliverable (`docs/research/gleam-atomvm/dossier.md`).

| Field | Description | Source req |
|---|---|---|
| `source_recommendation` | Exactly **one** of {Dart, C#, file-by-file replication of both}. | FR-001, SC-001 |
| `source_criteria_table` | Ranked comparison over {source health & currency, structural fit to Gleam, conversion effort, divergence}. | FR-001 |
| `source_rationale` | One-sentence ratifiable rationale + supporting detail. | FR-001, SC-001 |
| `build_target_matrix` | Embedded **E2**. | FR-002 |
| `architectural_fit` | Highest-risk mismatches + opportunities, each with bearing on the recommendation. | FR-006, SC-006 |
| `downstream_rescope_notes` | Each heavy feature (F5 bytecode runner, F6 compiler/loader, F9 link layer) named with re-scope or "unchanged". | FR-007, SC-005 |
| `downstream_handoff` | Source basis + assumed `glp_gleam/` layout/conventions + toolchain versions for F2/F3. | FR-008, SC-004 |
| `verdict` | Exactly **one** of {go, no-go, go-with-revisions}; if go-with-revisions → enumerated roadmap changes. | FR-010, SC-005 |

**Validation rules**:
- Exactly one `source_recommendation`; exactly one `verdict`. (SC-001, SC-005)
- `architectural_fit` includes **at least** the mutable-heap/immutability mismatch **and** the FCP-concurrency/BEAM-process fit; the mutable-heap finding cites the running unbound→bound demonstration of **E3**, not analysis alone. (SC-006)
- Every claim is backed by command+output or citation (FR-009).
- A reviewer reading **only** this document can act on it. (SC-001, US1 independent test)

**States**: `draft → evidence-complete → verdict-set → ratified(by engineer)`. The spike delivers up to `verdict-set`; **ratification is the engineer's**, not the spike's (Assumptions, FR-011).

**Relationships**: embeds **E2**; cites **E3** (smoke) and **E5** (inventory); ranks **E4** (candidates).

---

## E2 — Build-Target Matrix

A table embedded in the dossier (FR-002 says the dossier "MUST include" it).

| Row (target) | `verdict` | `evidence` | `constraints` | `host_vs_hardware` |
|---|---|---|---|---|
| **Erlang/BEAM** | viable / partially / not | command+output | — | host build |
| **AtomVM** | viable / partially / not | smoke result **or** named subset limitation **or** bring-up blocker | OTP/BEAM-subset limits | host/generic build (no hardware) |
| **JavaScript** | viable / partially / not | command+output **or** citation | cost relative to BEAM path | N/A |

**Validation rules**:
- Every row has a `verdict` **and** ≥1 piece of `evidence`; no cell "unknown" without a recorded reason. (SC-003, FR-002)
- AtomVM `verdict` is backed by an observed smoke result, a named BEAM/OTP-subset limitation, **or** a recorded bring-up blocker (FR-005).
- JS row states whether JS is a viable fallback and its cost relative to the BEAM path. (US4 acceptance #2)
- `host_vs_hardware` distinguishes "viable on host build" from "viable on target hardware" and records which was tested. (Edge case)

**Verdict enum**: `viable | partially viable | not viable`.

---

## E3 — Hello-GLP-Term Smoke

The self-contained Gleam project (`docs/research/gleam-atomvm/hello-glp-term/`).

| Field | Description | Source req |
|---|---|---|
| `gleam_module` | Constructs a representative GLP term: ≥1 compound/structure + 1 unbound-variable analogue. | FR-004 |
| `bind_demonstration` | Exactly **one** unbound→bound transition with a **reader observing** the bound value (process/state-holder primary; functional sibling for contrast). | FR-004, R4 |
| `beam_evidence` | Recorded compile-to-BEAM + run-on-Erlang command and observed output (the documented, correct term representation). | FR-004, US2, SC-002 |
| `atomvm_evidence` | Recorded AtomVM attempt result: success / partial / failure-with-output, or the bring-up blocker. | FR-005, US3 |
| `reproduce_commands` | The exact setup/build/run commands a second person follows. | SC-002 |

**Validation rules**:
- The constructed term includes **at least one compound/structure term and an unbound-variable analogue**. (FR-004, US2 acceptance #2)
- Exactly **one** unbound→bound bind is demonstrated, observed by a reader. (FR-004)
- Out of scope (MUST NOT attempt): full unification, suspension/reactivation scheduling, bytecode execution, performance. (Assumptions)
- Reproducible: same commands on a clean checkout → same observed result for a second person. (SC-002)

**States**: `compiles → runs-on-BEAM(observed) → atomvm-attempted(observed|blocked)`.

**Relationships**: provides the running evidence for **E1**'s `architectural_fit` (mutable-heap & concurrency findings) and the BEAM/AtomVM rows of **E2**.

---

## E4 — Source-Language Candidates

The three ranked alternatives (records inside the dossier's criteria table, not a separate file).

| Candidate | Root(s) in repo | Notes for the spike to confirm |
|---|---|---|
| **Dart** | `glp_runtime/` (`lib/{analysis,bytecode,compiler,engine,link,lint,multiagent,runtime}`) | Authoritative reference; single coherent tree; current to 2026-06-08. |
| **C#** | `glp_runtime_net/` (hand-port + own REPL) · `csharp/` (feature modules: il_codec, link) · `out/csharp/` (regenerable scaffold mirror) | **Multi-rooted** — the spike must state which root it treats as *the* C# candidate and surface the divergence. |
| **File-by-file replication of both** | derived | Cost/benefit of mirroring both sources into Gleam. |

**Validation rules**:
- Each candidate scored on all four criteria (R5). Divergence between Dart and C# is surfaced **explicitly** as a criterion, not assumed parity. (Edge case, FR-001)
- The roadmap's C#-lean is a confirmable/overturnable prior, not a foregone conclusion. (Assumptions)

---

## E5 — Toolchain Inventory

The recorded tool set (`docs/research/gleam-atomvm/toolchain-inventory.md`).

| Field | Description | Source req |
|---|---|---|
| `gleam_version` | Exact `gleam --version`. | FR-003 |
| `erlang_otp_version` | Exact OTP/`erl` version. | FR-003 |
| `atomvm_build` | AtomVM host build identity (prebuilt release tag or source-build commit), or the bring-up blocker. | FR-003, FR-005 |
| `build_tooling` | `rebar3` / Gleam build tool versions; JS backend toolchain if exercised. | FR-003 |
| `environment` | OS/arch actually verified on (Windows / WSL-Linux / Mac), incl. any fallback that was needed. | FR-003, R1, Edge case |
| `setup_build_run_commands` | The reproducible command block. | FR-003, SC-002 |

**Validation rules**:
- All version fields are **exact** (no "latest"); the environment field records the actual environment, including any fallback. (FR-003, R1)
- The command block reproduces the smoke for a second person. (SC-002)

**Relationships**: supplies the toolchain versions to **E1**'s `downstream_handoff`; underwrites **E2**'s and **E3**'s evidence.

---

## Cross-entity invariants

- **No production artifacts**: none of these entities is the `glp_gleam/` subtree, the langpair, or any runtime change. The only durable files are E1, E3, E5 under `docs/research/gleam-atomvm/`. (FR-011)
- **Recommend-don't-ratify**: E1 reaches `verdict-set`; the engineer ratifies. (Assumptions, FR-011)
- **Evidence everywhere**: every verdict/claim in E1·E2 traces to E3 (observed) or E5 (versions/commands) or an authoritative citation. (FR-009)
