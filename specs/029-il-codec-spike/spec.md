# Feature Specification: IL/Bytecode Round-Trip Codec Spike

**Feature Branch**: `029-il-codec-spike`
**Created**: 2026-06-11
**Status**: Draft
**Input**: User description: "IL/bytecode round-trip codec spike — an EXPERIMENT (throwaway-or-keep) that proves a BytecodeProgram <-> bytes round-trip codec via compile -> encode -> decode -> execute-equivalence."

## Context (non-normative)

This feature is **#4 (EXPERIMENT)** of the epic
*separation-of-REPL-front-end-from-engine-execution-scheduler*. Its single job is to
**de-risk the hardest unknown** in that epic: no serialization codec exists today for a
compiled GLP program, yet two later features depend on one — #7 (engine-state snapshot +
persistence) and #11 (compiled-IL-on-the-wire). This spike proves the round-trip is
achievable and pins down its correctness contract before any feature commits to it.

The authoritative design source is the dossier `specs/026-engine-review-dossier/` and the
reconciliation seed `docs/research/repl-engine-separation/reconciliation/4-il-codec-spike.md`,
which already enumerate the design forks (U1–U4), tensions (T1–T3), code evidence
(`file:line`), and recommendations. This spec adopts those recommendations as its baseline
and surfaces only the genuinely scope-defining forks as clarifications.

As an EXPERIMENT, the deliverable is **throwaway-or-keep**: a working codec plus its
verification harness, kept only if it passes; the value is the proof and the pinned
contract, not a production-blessed component. It changes **no** runtime, scheduler, or REPL
semantics — it adds a new, self-contained codec and its tests.

## Clarifications

### Session 2026-06-11

- Q: Heap-embedded `ModuleTerm` scope (FR-009) — defer to #7, or in scope now? → A: Both in scope, phased — implement per-module `BytecodeProgram` round-trip first (a), then heap-embedded `ModuleTerm` traversal (b). (Owner override of the seed's defer-to-#7 recommendation; accepts the seed's T1 higher-risk note about coupling to the not-yet-started heap-snapshot layout.)
- Q: Equivalence definition (FR-002) — execute-equiv only, structural identity, or hybrid? → A: Structural identity (seed T3 Option 2) — decode reproduces exact opcode objects (family + `IsReader` + operands + order); execute-equivalence remains an independent gate (FR-003).
- Q: Formal Lean 4 round-trip proof (FR-010 / SC-007 / A6) — in scope, or defer? → A: In scope (seed Option C) — Lean 4 `decode∘encode = id` over a simplified model (v1 family + ground constants), sorry-free, driven via Lean-LSP-MCP with no external LM API.

**Analyze remediations folded (2026-06-11)** — see `analysis.md` / `tasks.md`: F2 empty program is execute-equivalence-exempt (structural-identity only — FR-003/SC-002/Edge Cases); F3 corpus floor ≥10 concrete programs made explicit (data-model + T013); F1 discriminant-table completeness asserted by reflection (T029) closing the corpus-only-coverage gap; F4 baseline-green-before-work (T000); F5 prerequisite verification before their phases (T031/T032/T033).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A downstream-feature author trusts the round-trip is sound (Priority: P1)

An engineer about to build #7 (persistence) or #11 (compiled-IL-on-the-wire) needs to know,
before they start, that a compiled GLP program can be turned into bytes and back without
losing or corrupting anything that affects execution. They run the spike's verification
harness and see that every program in the corpus round-trips and executes identically.

**Why this priority**: This is the entire reason the spike exists. If the round-trip is not
demonstrably sound, every downstream feature inherits an unproven foundation and must
re-litigate the codec design. Delivering this one story delivers the spike's whole value.

**Independent Test**: Compile a representative corpus of GLP programs, run each through
encode → decode, and assert the decoded program both (a) matches the original by the agreed
equivalence definition and (b) executes to the same result. Passing the harness on the
corpus is the deliverable; it can be demonstrated with no other feature present.

**Acceptance Scenarios**:

1. **Given** a compiled program containing only v1 (`IOp`) opcodes, **When** it is encoded
   and decoded, **Then** the decoded program satisfies the equivalence definition and
   executes to the same `ExecutionResult` (status + bindings + error) as the original.
2. **Given** a compiled program containing only v2 (`IOpV2`) opcodes (each carrying its
   reader/writer polarity), **When** it is encoded and decoded, **Then** the reader/writer
   polarity of every opcode is preserved and execution is equivalent.
3. **Given** a program that mixes v1 and v2 opcodes in one instruction list, **When** it is
   round-tripped, **Then** no opcode is dropped, reordered across a phase boundary, or
   misclassified, and execution is equivalent.
4. **Given** a program with a recursive ground constant (a structured term embedded as an
   opcode operand), **When** it is round-tripped, **Then** the full constant tree is
   reconstructed identically.
5. **Given** a program whose execution reaches a suspended state, **When** the round-tripped
   program is executed, **Then** it also reaches the suspended state (suspension is
   preserved, not silently collapsed to success or failure).

### User Story 2 - The codec's correctness contract is pinned for reuse (Priority: P2)

The author of #7/#11 needs the codec to come with an explicit, locatable statement of *what
it guarantees* — which opcode families, which constant types, which metadata (variable map,
labels), and which GLP design properties (reader/writer polarity, phase ordering,
suspension, committed-choice boundary) it preserves — so they can build on it without
re-deriving the guarantees from the code.

**Why this priority**: A passing harness with no documented contract forces every consumer
to reverse-engineer the guarantees. Pinning the contract is what makes the spike's result
*reusable* rather than merely *encouraging*.

**Independent Test**: Open the spike's deliverable and confirm it states, for each opcode
family and constant type, whether it is covered; and that each preserved GLP property maps
to a specific gate in the harness.

**Acceptance Scenarios**:

1. **Given** the spike deliverable, **When** a consumer looks for the set of supported
   constant operand types, **Then** they find an explicit, complete whitelist with the
   behavior on an out-of-whitelist type defined (not silent corruption).
2. **Given** the spike deliverable, **When** a consumer asks "does this preserve
   single-reader/single-writer polarity?", **Then** they find a named gate that verifies it.

### User Story 3 - The verification establishes a formal/empirical confidence bar (Priority: P3)

Beyond passing example tests, the spike establishes *how confident* we are that the round-trip
is identity — via a coverage gate (every opcode class and constant type is exercised) and,
where in scope, a mechanized proof of the round-trip property over a simplified model.

**Why this priority**: Confidence-raising beyond the example corpus is valuable but
secondary to demonstrating the round-trip works at all (P1) and documenting it (P2). It is
the part most reasonable to descope if effort is constrained.

**Independent Test**: Inspect the coverage report (every concrete opcode class and constant
type exercised at least once) and, if in scope, the mechanized proof artifact for the
simplified model.

**Acceptance Scenarios**:

1. **Given** the encoder/decoder, **When** the coverage gate runs, **Then** every concrete
   opcode class in both families is exercised by at least one encode + decode.
2. **Given** the simplified codec model (if the formal gate is in scope), **When** the proof
   is checked, **Then** the round-trip-is-identity theorem holds with no unproven gaps.

### Edge Cases

- **Empty program**: a program with no instructions must round-trip to an equally empty
  program; it is verified by structural identity only and is **exempt from the
  execute-equivalence gate** (executing an empty program has no defined goal/result).
  (Analyze remediation F2, 2026-06-11.)
- **Obsolete opcodes**: legacy programs may contain opcodes marked obsolete; the codec's
  behavior on them (round-trip exactly, normalize, or reject) must be defined, not accidental.
- **Derived label table**: the label index is reconstructable from the instruction list; the
  codec must produce a program whose label lookups behave identically whether the table is
  carried or recomputed.
- **Unknown opcode subtype**: if an opcode subtype outside the known families ever appears,
  the codec must fail loudly rather than silently drop it.
- **Out-of-whitelist constant value**: a constant operand of a type not in the supported set
  must be rejected loudly, never silently dropped or stringified into a non-round-trippable form.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The codec MUST encode a compiled program (its ordered instruction list, both
  opcode families, label markers, and associated variable map) to a byte payload, and decode
  that payload back to a program.
- **FR-002**: A decoded program MUST satisfy **structural identity** relative to the original:
  decode reproduces the exact opcode objects — same opcode family (v1 `IOp` / v2 `IOpV2`),
  same `IsReader` polarity, same operands, and same instruction order. (Execute-equivalence is
  additionally and independently required by FR-003.)
- **FR-003**: For every program in the verification corpus, executing the decoded program
  MUST produce the same `ExecutionResult` — identical status, bindings, and error — as
  executing the original. (The **empty program is exempt** — it has no defined goal/result —
  and is verified by structural identity (FR-002) only.)
- **FR-004**: The codec MUST preserve each opcode's **reader/writer polarity** (SRSW), the
  **three-phase HEAD → GUARD → BODY ordering**, the **committed-choice (Commit) position**,
  the **suspension opcodes**, and the **three-valued-unification opcodes**, each verified by a
  named gate in the harness.
- **FR-005**: The codec MUST reconstruct **recursive ground constant operands** (structured
  terms embedded as opcode operands) identically, walking the constant tree to arbitrary depth.
- **FR-006**: The codec MUST support an explicit **whitelist of constant operand value
  types**; encountering a value type outside the whitelist MUST fail loudly (no silent drop
  or lossy coercion).
- **FR-007**: The verification corpus MUST contain at least 10 compiled programs covering:
  v1-only, v2-only, mixed v1/v2, recursive-constant, label-bearing, empty,
  suspension-reaching, and heap-embedded-`ModuleTerm` cases.
- **FR-008**: The codec MUST exercise a **coverage gate**: every concrete opcode class in
  both families is encoded and decoded by at least one corpus case.
- **FR-009**: The codec's **scope** covers BOTH top-level per-module compiled programs AND
  heap-embedded compiled programs (`ModuleTerm`-embedded `BytecodeProgram`s reachable as heap
  data), delivered in two phases: (a) per-module `BytecodeProgram` round-trip first, then
  (b) heap-embedded `ModuleTerm` traversal — the codec MUST locate and round-trip
  `ModuleTerm`-embedded programs by walking the heap. (Phase b couples to the heap-snapshot
  layout that #7 owns; this is an accepted risk per the seed's T1.)
- **FR-010**: A **formal round-trip-is-identity proof** (`decode∘encode = id`) over a
  simplified codec model (v1 opcode family + ground constants) MUST be delivered in **Lean 4**,
  sorry-free, driven via Lean-LSP-MCP with no external LM API (per Assumption A6).
- **FR-011**: The deliverable MUST state the codec's **correctness contract**: covered
  opcode families, covered constant types, carried metadata, preserved GLP properties, and
  the behavior on each edge case above — locatable without reading the implementation.
- **FR-012**: The spike MUST NOT alter runtime, scheduler, compiler, or REPL execution
  semantics; it adds only the codec and its verification harness.

### Key Entities *(include if feature involves data)*

- **Compiled program**: the unit being serialized — an ordered list of opcodes (two
  families), a derived label index, and the metadata needed to interpret it (variable map).
- **Opcode**: a single instruction; carries a family identity and, for the newer family, a
  reader/writer polarity. The codec must distinguish every concrete kind unambiguously.
- **Constant operand**: a value embedded in an opcode; may be a primitive or a recursive
  structured term; constrained to the supported whitelist.
- **Variable map**: the name → register-slot metadata accompanying a compiled program; its
  scope (per-module) is fixed by Assumption A2.
- **Byte payload**: the serialized form; rides the existing transport framing as an opaque
  payload distinguished by a payload-type marker (Assumption A4).
- **Verification corpus**: the set of compiled GLP programs the harness round-trips and
  execute-compares.
- **Equivalence gate / coverage gate / formal model**: the verification artifacts that turn
  "it seemed to work" into a pinned, reusable contract.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of the verification corpus (≥10 programs) round-trips and passes the
  agreed equivalence definition.
- **SC-002**: 100% of the verification corpus — **excluding the empty program, which is
  structural-identity-only (FR-002)** — executes to an identical result (status + bindings +
  error) before and after round-trip.
- **SC-003**: 100% of concrete opcode classes across both families are exercised by the
  coverage gate.
- **SC-004**: Zero silent failures — every unsupported opcode subtype or out-of-whitelist
  constant value produces a loud, attributable error rather than a corrupted round-trip.
- **SC-005**: Each of the five preserved GLP properties (reader/writer polarity, phase
  ordering, Commit position, suspension, three-valued unification) is covered by at least one
  named, passing gate.
- **SC-006**: The codec's correctness contract is documented such that a downstream-feature
  author can determine coverage of any opcode family or constant type without reading the
  implementation.
- **SC-007**: The round-trip-is-identity theorem is mechanically checked in Lean 4 over the
  simplified model (v1 family + ground constants) with no unproven gaps (zero `sorry`).

## Assumptions

These resolve the seed's lower-impact forks via its stated recommendations, so they do not
consume clarification budget. Any may be revisited in `/buildkit-clarify`.

- **A1 (codec target — U1)**: The codec targets the **raw per-module compiled program**
  (full label table preserved), not the post-merge combined program. This is the choice the
  persistence consumer (#7) needs; the engine's merge/filter step is re-applied on load.
- **A2 (variable-map scope — U2)**: The codec carries the **per-module variable map**
  alongside each program. Goal-level variable maps belong to the result-envelope work (#2),
  not this spike.
- **A3 (obsolete opcodes — U3)**: Obsolete opcodes are **round-tripped exactly** (preserved
  with a discriminant), not normalized or rejected, so legacy programs serialize losslessly.
  Revisit if exact preservation proves disproportionate.
- **A4 (transport framing — T2)**: The payload is distinguished by a **payload-type byte in
  the payload header**, leaving the existing transport frame-kind contract (whole/fragment)
  untouched. No new frame-kind values are introduced (avoids touching the feature-025 tested
  path).
- **A5 (Dart byte-parity)**: Cross-runtime byte-parity with a Dart mirror is **out of scope**
  for this spike and deferred to #11. This spike establishes the contract in one runtime.
- **A6 (formal tool — Q3 resolved: in scope)**: The formal proof uses **Lean 4** as the
  primary tool, started from a simplified model (one opcode family, ground constants), driven
  without any external LM API per the project's no-API rule.
- **A7 (corpus source)**: The verification corpus is drawn from the existing `programs/`
  GLP sources compiled by the standard pipeline; no new GLP language constructs are invented
  for the spike.
- **A8 (throwaway-or-keep)**: The codec is kept only if it passes all in-scope gates; a
  failing spike still delivers value by pinning *why* the round-trip is hard and what the
  contract must say.

## Dependencies

- **Depends on**: `engine-review-and-design-dossier` (#1) — the dossier
  (`specs/026-engine-review-dossier/`) is the authoritative design source and is complete.
- **Feeds**: #7 (engine-state snapshot + persistence) and #11 (compiled-IL-on-the-wire),
  which consume the codec and its pinned contract.
