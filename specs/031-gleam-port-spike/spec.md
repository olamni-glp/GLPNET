# Feature Specification: Gleam Port — Source & Toolchain / AtomVM Feasibility Spike

**Feature Branch**: `031-gleam-port-spike`  
**Created**: 2026-06-19  
**Status**: Draft  
**Input**: User description: "Spike to decide the Gleam port: evaluate porting GLP from the Dart vs the C# source (or replicating both file-by-file), stand up the Gleam + Erlang/OTP toolchain, and measure AtomVM BEAM-subset feasibility. Deliver a decision dossier (source language + build-target matrix Erlang/AtomVM/JS) and a hello-GLP-term smoke that compiles a Gleam module to BEAM and runs on Erlang (and AtomVM if viable). Output feeds codeconv-gleam-langpair and the glp_gleam subtree."

## Context *(why this feature exists)*

This is feature **F1** of the `gleam-atomvm` epic — the first, blocking step toward porting the GLP implementation to **Gleam** (a statically-typed functional language that compiles to Erlang/BEAM and to JavaScript) so it can ultimately run on **AtomVM** (a BEAM implementation for constrained/embedded targets), with plain Erlang/BEAM as the test runtime. The port will flow through the existing `codeconv` conversion pipeline into a new `glp_gleam/` subtree mirroring `glp_runtime_net`, and the epic's capstone is cross-runtime **C#↔Gleam** distributed tests.

This spike is a **research and decision feature**, not a production build. It exists to retire the largest unknowns before any porting code is written:

1. **Which source?** The port can start from the Dart source, the C# source, or a file-by-file replication of both. The downstream langpair (`codeconv-gleam-langpair`) and subtree-scaffold (`glp-gleam-subtree-scaffold`) features cannot begin until this is settled.
2. **Is the toolchain real here?** A Gleam + Erlang/OTP toolchain must be provably stood up and demonstrated end-to-end before committing the roadmap to it.
3. **Is AtomVM viable?** AtomVM runs only a subset of BEAM and OTP. A full interpreter + custom-heap port may exceed that subset. The spike measures this so heavy downstream features (bytecode runner, compiler/loader, link layer) can be re-split or re-scoped if needed.

The spike's deliverable is a **decision dossier** plus a **hello-GLP-term smoke**, whose outputs feed `codeconv-gleam-langpair` and the `glp_gleam` subtree.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Ratifiable source-language decision & go/no-go (Priority: P1)

The decision-maker needs an evidence-backed dossier that recommends exactly one **source basis** for the port — the Dart source, the C# source, or file-by-file replication of both — together with a clear go / no-go / go-with-revisions recommendation for the epic. The recommendation must rest on stated criteria (source health and currency, structural fit to Gleam, conversion effort, divergence between the two sources) and on the architectural-fit assessment between GLP's runtime model (mutable heap, WAM-style execution, suspension/reactivation) and Gleam's immutable/functional model. Downstream features must be able to start without re-opening this question.

**Why this priority**: Every other epic feature (F2–F10) hard-depends on knowing the source. Without a ratifiable decision the epic cannot proceed at all. This is the MVP slice — a dossier that settles the source question and gives a go/no-go is, by itself, a viable result of the spike.

**Independent Test**: A reviewer reads only the dossier and can (a) state the recommended source basis and its one-sentence rationale, (b) see the criteria table that produced it, (c) see the architectural-fit risk findings, and (d) act on the go/no-go — ratify, reject, or request revisions — without consulting any other document.

**Acceptance Scenarios**:

1. **Given** the dossier is complete, **When** a reviewer reads the source-decision section, **Then** it names exactly one recommended source basis with a ranked criteria comparison and an explicit rationale.
2. **Given** the dossier is complete, **When** a reviewer reads the architectural-fit section, **Then** it identifies the highest-risk mismatches between the GLP runtime model and Gleam's immutable model and states how each affects the recommendation.
3. **Given** the dossier is complete, **When** a reviewer reaches its conclusion, **Then** it carries a single go / no-go / go-with-revisions verdict, and any "go-with-revisions" verdict enumerates the required roadmap changes.

---

### User Story 2 - Toolchain stood up + hello-GLP-term runs on Erlang/BEAM (Priority: P1)

The team needs proof that the Gleam + Erlang/OTP toolchain works end-to-end on the project's development environment. The spike stands up the toolchain and delivers a minimal "hello-GLP-term" Gleam module that constructs a representative GLP term, compiles to BEAM, and runs on Erlang producing a correct, observable result. The exact toolchain versions and the reproducible setup/build/run commands are recorded.

**Why this priority**: A recommendation to adopt Gleam is not credible without a demonstrated working toolchain and a running artifact. This slice converts the toolchain from "claimed feasible" to "observed working," and it is independently valuable even if AtomVM later proves infeasible (plain BEAM is the test runtime).

**Independent Test**: On a clean checkout, a second person follows the recorded commands, the hello-GLP-term module compiles to BEAM, runs on Erlang, and emits the same expected term result.

**Acceptance Scenarios**:

1. **Given** the recorded setup steps, **When** they are followed on the dev environment, **Then** the Gleam compiler and Erlang/OTP runtime are present and their versions are recorded.
2. **Given** the hello-GLP-term Gleam module, **When** it is compiled and run on Erlang/BEAM, **Then** it produces the documented, correct representation of the chosen GLP term (including at least one compound/structure term and an unbound-variable analogue).
3. **Given** the recorded build/run commands, **When** they are re-run from a clean state, **Then** the same observable result is reproduced.

---

### User Story 3 - AtomVM BEAM-subset feasibility verdict (Priority: P2)

The team needs to know how much of the planned GLP runtime can run on AtomVM given its BEAM/OTP-subset limits. The spike attempts to run the hello-GLP-term smoke on an AtomVM host build and records the outcome; where AtomVM cannot run it, the dossier records the specific subset limitation that blocked it. The AtomVM row of the build-target matrix receives a verdict backed by evidence.

**Why this priority**: AtomVM is the epic's ultimate target, but the *test* runtime is plain BEAM, so the epic can proceed even if AtomVM is only partially viable — the finding changes scope, not viability. Hence P2: important and decision-shaping, but not blocking the MVP.

**Independent Test**: The build-target matrix's AtomVM row carries a verdict (viable / partially viable / not viable) plus evidence — either an observed smoke result on an AtomVM host build, or the named BEAM/OTP-subset limitation that prevented it.

**Acceptance Scenarios**:

1. **Given** an AtomVM host build, **When** the hello-GLP-term smoke is attempted on it, **Then** the outcome (success, partial, or failure) is recorded with the observed output or error.
2. **Given** AtomVM cannot run the smoke, **When** the dossier records the AtomVM verdict, **Then** it names the specific subset limitation responsible and its implication for the heavy downstream features (bytecode runner, compiler/loader, link layer).

---

### User Story 4 - Complete build-target matrix incl. JavaScript fallback (Priority: P3)

The team needs the full build-target matrix — **Erlang/BEAM, AtomVM, JavaScript** — with a feasibility verdict and supporting evidence for every target, so alternatives are on record. Gleam's JavaScript backend is evaluated as a fallback/secondary target.

**Why this priority**: The JS backend is a hedge, not the goal; completing the matrix improves the dossier's durability and de-risks a future pivot, but adds no value to the immediate go/no-go beyond what US1–US3 already provide.

**Independent Test**: Every target row in the build-target matrix has a verdict and at least one piece of supporting evidence (a command + observed output, or a citation to an authoritative source); no cell is left "unknown" without a recorded reason.

**Acceptance Scenarios**:

1. **Given** the matrix, **When** a reviewer inspects each of the three target rows, **Then** each has a verdict and supporting evidence.
2. **Given** the JavaScript target, **When** its row is read, **Then** it states whether JS is a viable fallback for GLP and what it would cost relative to the BEAM path.

---

### Edge Cases

- **Toolchain will not install on the primary (Windows) environment** → the spike records this as a first-class finding, falls back to a documented alternative environment (WSL/Linux or the sibling Mac), and the dossier flags the development-environment constraint for downstream features.
- **AtomVM cannot run the smoke at all** → the matrix records "not viable (subset)", the epic continues on plain BEAM as the test runtime, and the dossier proposes re-scoping AtomVM as a later/optional target.
- **The two candidate sources (Dart, C#) have diverged** (e.g., a fix present in one but not the other) → the dossier surfaces the divergence explicitly as a criterion in the source decision rather than assuming parity.
- **GLP's mutable heap / WAM-style unification does not map cleanly onto Gleam's immutability** → recorded as a top architectural-fit risk with options (functional heap model, process/state-holder model), feeding a possible re-split of heavy downstream features.
- **A target appears viable but only on real embedded hardware, not a host build** → the matrix distinguishes "viable on host build" from "viable on target hardware" and records which was actually tested.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The spike MUST produce a written decision dossier that recommends exactly one source basis for the port — Dart source, C# source, or file-by-file replication of both — and records the decision criteria and the rationale for the recommendation.
- **FR-002**: The dossier MUST include a build-target matrix evaluating each candidate runtime target — Erlang/BEAM, AtomVM, and JavaScript — with a per-target feasibility verdict (viable / partially viable / not viable) and supporting evidence for each verdict.
- **FR-003**: The spike MUST stand up a working Gleam + Erlang/OTP toolchain on a documented development environment and record the exact tool versions and the reproducible setup, build, and run commands.
- **FR-004**: The spike MUST deliver a "hello-GLP-term" smoke — a minimal Gleam module that constructs a representative GLP term (including at least one compound/structure term and an unbound-variable analogue), compiles to BEAM, and runs on Erlang producing an observable, correct result.
- **FR-005**: The spike MUST attempt to run the hello-GLP-term smoke on an AtomVM host build and record the outcome; if AtomVM cannot run it, the dossier MUST record the specific BEAM/OTP-subset limitation that blocked it.
- **FR-006**: The dossier MUST assess the architectural fit between the GLP runtime model (mutable heap, WAM-style execution, suspension/reactivation, single-reader/single-writer unification) and Gleam's immutable/functional model, identifying the highest-risk porting concerns and the opportunities (e.g., the concurrency model's fit to BEAM processes and message passing).
- **FR-007**: The spike MUST identify and record the porting risks or limits that could force a re-split or re-scope of the heavy downstream features (bytecode runner, compiler/loader, link layer), in a form the roadmap can act on.
- **FR-008**: The dossier outputs MUST be consumable by the downstream langpair feature and subtree-scaffold feature — i.e., it MUST state the chosen source basis, the target `glp_gleam/` project layout/conventions to assume, and the toolchain versions those features will build against.
- **FR-009**: Every feasibility verdict and "it works" claim in the dossier MUST be backed by reproducible evidence — a command plus its observed output, or a citation to an authoritative source — rather than by assertion alone.
- **FR-010**: The dossier MUST conclude with a single go / no-go / go-with-revisions recommendation for the Gleam port epic; a "go-with-revisions" recommendation MUST enumerate the specific roadmap changes required.
- **FR-011**: The spike MUST NOT modify the existing GLP runtime, programs, or the roadmap's downstream feature definitions; its only durable outputs are the dossier, the hello-GLP-term smoke artifact, and the recorded toolchain inventory. (Final ratification of the source decision and any roadmap changes is the engineer's, informed by the dossier.)

### Key Entities

- **Decision Dossier**: The spike's primary deliverable. Captures the source-language recommendation + criteria, the build-target matrix, the architectural-fit assessment, the downstream-feature risk/re-scope notes, and the final go/no-go verdict.
- **Build-Target Matrix**: A table of {Erlang/BEAM, AtomVM, JavaScript} × {verdict, evidence, constraints, host-vs-hardware caveat}.
- **Hello-GLP-Term Smoke**: The minimal Gleam module representing a GLP term, plus its recorded compile + run evidence on each runtime attempted.
- **Source-Language Candidates**: The Dart source, the C# source, and the both-file-by-file option — the three alternatives the decision ranks.
- **Toolchain Inventory**: The recorded set of tools (Gleam compiler, Erlang/OTP, AtomVM host build, build tooling) with exact versions, install steps, and the development environment they were verified on.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A reviewer can state the recommended source basis and its rationale in one sentence after reading only the dossier — no other document required.
- **SC-002**: The hello-GLP-term smoke is reproducible: following the recorded commands on a clean checkout produces the same observed result for a second person.
- **SC-003**: Every target in the build-target matrix has a verdict and at least one piece of supporting evidence; no cell is left "unknown" without a recorded reason.
- **SC-004**: The downstream langpair and subtree-scaffold features can begin without re-opening the source-language question — zero re-litigation of the source decision after ratification.
- **SC-005**: The dossier ends with exactly one go / no-go / go-with-revisions verdict, and every heavy downstream feature whose scope the spike's findings affect is named with its recommended re-scope (or explicitly confirmed unchanged).
- **SC-006**: The architectural-fit assessment identifies at least the mutable-heap/immutability mismatch and the FCP-concurrency/BEAM-process fit, each with its bearing on the recommendation stated.

## Assumptions

- **Decision authority**: The dossier *recommends*; the engineer ratifies. The spike does not unilaterally commit the roadmap to a source basis — it produces the evidence and recommendation, and final ratification (and any roadmap edits) follow review. (Consistent with the project's discipline on scope/framing decisions.)
- **Development environment**: The spike targets the project's primary environment (Windows) first; if the Gleam/Erlang/AtomVM toolchain proves infeasible there, falling back to a documented alternative (WSL/Linux or the sibling Mac) is acceptable, and the environment used is recorded as part of the toolchain inventory.
- **AtomVM target**: "AtomVM feasibility" is measured against an AtomVM **host/generic build** — no embedded hardware (ESP32 etc.) is required for this spike. Where a verdict would differ on real hardware, that distinction is noted but not blocking.
- **Representative term**: "Hello-GLP-term" is a minimal but representative term exercising the crux of the runtime model — at least one compound/structure term and an unbound-variable analogue — not the full term universe.
- **Source leaning**: The roadmap records an initial lean toward the C# source; the spike treats this as a prior to be confirmed or overturned by evidence, not a foregone conclusion.
- **Scope of the smoke**: The smoke proves the *toolchain and term-representation path*, not a working interpreter; performance, full unification, and bytecode execution are explicitly out of scope for this spike and belong to later features.
- **No production code**: This spike produces a dossier, a throwaway-grade smoke artifact, and a toolchain inventory — not the `glp_gleam/` runtime, the langpair, or the subtree scaffold (those are F2/F3).

## Dependencies

- **Upstream**: None blocking — this is F1, the head of the `gleam-atomvm` epic dependency chain.
- **Downstream consumers**: `codeconv-gleam-langpair` (F2) and `glp-gleam-subtree-scaffold` (F3) consume this spike's source-basis decision, toolchain versions, and target project conventions. The heavy features (bytecode runner F5, compiler/loader F6, link layer F9) consume the architectural-fit risk findings and any re-scope recommendations.
- **Reference inputs**: The existing Dart source (`glp_runtime/`) and C# source (the `csharp/` runtime), and the roadmap entry for the `gleam-atomvm` epic.
