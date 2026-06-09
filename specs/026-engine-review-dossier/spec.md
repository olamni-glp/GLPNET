# Feature Specification: Engine Review + Refactoring Design Dossier

**Feature Branch**: `026-engine-review-dossier`
**Created**: 2026-06-09
**Status**: Draft
**Input**: User description: "Engine review + refactoring design dossier"

## Context (non-normative)

This feature is **marathon step 2 (the refactoring design) + step 3 (turn the design
into pipeline-ready features)** of the epic
*separation-of-REPL-front-end-from-engine-execution-scheduler*. Marathon step 1 — the
comprehensive, read-only, multi-agent engine review — is already complete and recorded in
`docs/research/repl-engine-separation/investigation.md`. The owner requirements are in
`docs/research/repl-engine-separation/requirements.md`; the marathon framing is in
`docs/research/repl-engine-separation/feature-definition.md` §8.

The deliverable of this feature is a single **authoritative design dossier**: the decision-final
refactoring design that every successor feature (the result-envelope work, the wire codecs, the
process-split MVP, the persistence/liveness/resume work, and the downstream experiments) **cites
as its source of truth**. It is a documentation/design deliverable produced by reading the
existing code and the step-1 review — it changes **no** engine, runtime, or REPL code.

## Clarifications

### Session 2026-06-09

- Q: Should the dossier resolve each open design question itself, or present options for the owner to decide? → A: Present options — the dossier presents substantive options with their consequences for each genuine design/scope fork (and may include a recommendation), but the **owner makes the decision**; the dossier does not unilaterally settle forks.
- Q: What quality bar must each presented option meet? → A: Each option must be **fully researched** — grounded in cited code evidence (`file:line`) and/or established prior art — and explained **concisely** (option + consequences + trade-off in a few lines, not a narrative).
- Q: Where does creating the actual buildkit-roadmap / pipeline entries for the successor features (2–16) fall? → A: Option B — this feature authors the breakdown as dossier content **and**, after the owner approves the dossier, seeds features 2–16 into `buildkit-roadmap` (one candidate each, with kind/scope/why/depends-on); specifying the first successor feature is a separate later step.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A successor-feature author finds a decision-final design to build against (Priority: P1)

An engineer about to specify or implement a downstream feature (e.g. the engine→client result
envelope, or the heap-snapshot persistence API) opens the dossier and finds the relevant design
area covered completely — the contract shape, the pointer to the code it touches, and either the
forced design (where no genuine alternative exists) or the fully-researched options with their
consequences and any advisory recommendation (where a real choice exists) — without having to
re-derive it from source.

**Why this priority**: The whole reason this feature exists is to remove design ambiguity before
any code is written downstream. If the design decisions are not decision-final and locatable, the
dossier delivers no value and every later feature re-opens the same questions.

**Independent Test**: Pick any of the named design areas — (a) the front-end/engine **seam
contract**, (b) the **binary wire shapes** (client→engine and the net-new engine→client result
envelope), (c) the **control-program startup + client model**, (d) the **long-running /
liveness / crash / restart model**, (e) the **persistent-vs-ephemeral state model** with the
DB-abstraction, bootstrap, and restore-and-resume, (f) the **mailbox decision**, (g) the
**MVP slice** — and confirm the dossier covers it with the code locations it affects and either a
forced design or fully-researched options-with-consequences (plus any advisory recommendation).
Delivers value the moment any one area is decision-ready.

**Acceptance Scenarios**:

1. **Given** a downstream author needs the engine→client result-envelope shape, **When** they
   read the dossier's wire-shapes section, **Then** they find the complete field set (status,
   bindings, var-name→writer mapping, suspended-goal detail, captured/streamed output, errors,
   and how an unbound variable in a suspended result is encoded), the chosen codec/transport
   reuse decision, and the rationale — with no need to read engine source to proceed.
2. **Given** a reviewer asks "why this design and not the alternative," **When** they read any
   design area, **Then** the dossier records the considered alternatives and the reason the
   chosen one was selected.
3. **Given** a design area depends on a net-new capability (a capability that does not exist in
   the code today), **When** the reviewer reads that area, **Then** the dossier explicitly flags
   it as net-new and names the substrate to be reused or built.

### User Story 2 - The requirement/code premise mismatches are reconciled (Priority: P1)

An engineer who internalised the original owner requirements discovers that two premises do not
match the code as-built (the requirement that the parser/compiler lives in the front-end and that
the wire carries compiled IL; and the requirement that the engine "generates new IL at runtime").
The dossier states, for each, the as-built reality, the resolving decision, and the consequence,
so no successor feature is planned on a false premise.

**Why this priority**: A design built on a false premise mis-scopes every dependent feature. The
roadmap brief names "resolve requirement/code premise mismatches" as a core deliverable, so this
is co-equal with the design itself.

**Independent Test**: For each of the two premise mismatches identified in the step-1 review
(compiler location; runtime-IL generation), confirm the dossier states (i) what the requirement
assumed, (ii) what the code actually does and where, (iii) the decision that reconciles them, and
(iv) the downstream consequence (e.g. which features inherit the refactor vs. which carry source
text for the MVP).

**Acceptance Scenarios**:

1. **Given** the "compiled-IL-on-the-wire / parser-in-front-end" premise, **When** the reader
   consults the reconciliation, **Then** the dossier states that the compiler is engine-internal
   as-built, names the decision (MVP carries source text; compiler relocation is a deliberate
   follow-up), and identifies which successor features that decision splits.
2. **Given** the "engine generates new IL at runtime" premise, **When** the reader consults the
   reconciliation, **Then** the dossier states that no bytecode is synthesised at runtime,
   explains the actual mechanism (runtime goal-term assembly + dispatch against pre-compiled
   bytecode circulating as heap data), and states the consequence for the persistence design.

### User Story 3 - Every open design question is presented as options for the owner to decide (Priority: P2)

A reviewer of the design needs each previously-open design question (the set surfaced by step 1
as "for design/specify to resolve") presented as substantive options with their consequences — so
the owner can decide each fork deliberately, rather than the fork migrating into a later feature
to be decided ad hoc.

**Why this priority**: Open questions left as bare forks migrate into downstream features as
hidden scope and inconsistent decisions. Surfacing each as decision-ready options is what makes
the dossier the place the owner settles the epic's direction — but it ranks just below the core
design and premise reconciliation, and the decisions themselves are the owner's to make at the
approval gate.

**Independent Test**: Enumerate the open questions from the step-1 review and confirm each is
presented in the dossier as 2–5 substantive options with consequences (and, where appropriate, a
recommendation), framed for an owner decision (compiler-location; output streaming vs. terminal
envelope; encoding of unbound/mutual-ref/module-term bindings and whether
suspended-goal/blocking-reader detail round-trips; var-name→writer identity scheme; which durable
store underlies engine state and what counts as "full current state"; snapshot granularity and
consistency point; where the snapshot/resume driver lives under the dependency-direction rule;
whether the store is source-of-truth for code or `.glp` is re-loaded; in-flight-request loss vs.
replay).

**Acceptance Scenarios**:

1. **Given** any open question from the step-1 review, **When** the reader looks it up in the
   dossier, **Then** it is presented as distinct options with their consequences, framed for an
   owner decision.
2. **Given** the dossier includes a recommendation for an option, **When** the reader reads it,
   **Then** the recommendation is clearly marked as advisory and the decision is explicitly left
   to the owner — the dossier does not record the fork as already settled.

### User Story 4 - The epic feature breakdown is authored and seeded into the roadmap (Priority: P2)

A planner needs the ordered breakdown of successor features — each tagged as preparatory,
experiment, MVP, or follow-up, with scope, rationale, and dependencies — authored in the dossier
and, once the owner approves the dossier, captured as `buildkit-roadmap` candidates so the epic is
queued and ready to be drawn into the pipeline one well-scoped feature at a time, with no forward
dependency surprises.

**Why this priority**: Marathon step 3 is "turn the design into pipeline-ready features." The
dossier must both author the breakdown and (post-approval) seed the roadmap so the next stage of
work is queued. It ranks P2 because it depends on the design (P1) being settled first.

**Independent Test**: Confirm the dossier contains an ordered list of successor features where
each entry has a kind (prep/experiment/MVP/follow-up), a one-line scope, a "why," and an explicit
`depends-on` set, the ordering contains no entry that depends on a later entry, and — after
dossier approval — each entry exists as a `buildkit-roadmap` candidate.

**Acceptance Scenarios**:

1. **Given** the feature breakdown, **When** a planner reads any entry, **Then** it states kind,
   scope, why, and depends-on, and traces back to the dossier section that motivates it.
2. **Given** the full ordering, **When** a reviewer checks dependencies, **Then** no feature
   depends on a feature listed after it (the order is a valid topological order).
3. **Given** the candidate MVP slice(s), **When** a planner reads them, **Then** each lists
   exactly which net-new capabilities it depends on and which it explicitly defers.
4. **Given** the owner has approved the dossier, **When** the planner queries `buildkit-roadmap`,
   **Then** each successor feature (2–16) is present as a candidate with kind/scope/why/depends-on,
   and no successor feature has been specified yet.

### Edge Cases

- **What happens when the step-1 review and the as-built code disagree at dossier-writing time?**
  The code is re-read and the dossier records the current reality; a stale review claim is
  corrected, not propagated.
- **What happens when a design decision cannot be made without owner input?** It is presented as
  fully-researched options with their consequences (and, where appropriate, an advisory
  recommendation), framed for the owner to decide — never silently defaulted nor left blank. (Per
  the Session 2026-06-09 clarification, this is the dossier's default mode for every genuine fork,
  not just hard cases.)
- **What happens when a successor feature would have no dossier section to cite?** That is a gap:
  the breakdown entry must point to a dossier section, so a missing section is surfaced and added
  rather than the feature being left ungrounded.
- **How is a net-new capability (no existing code) distinguished from a refactor of existing
  code?** Each design area marks whether it reuses an existing substrate, refactors existing code,
  or is entirely net-new, so downstream effort is not under-estimated.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The feature MUST produce a single authoritative design dossier document that
  successor features cite as the source of truth for the REPL↔engine refactoring design.
- **FR-002**: The dossier MUST specify the front-end/engine **seam contract** — what crosses in
  each direction, and which components the engine currently computes but drops at the result
  boundary that must be promoted across the seam.
- **FR-003**: The dossier MUST specify the **binary wire shapes** for both directions: the
  client→engine payload, and the net-new engine→client **result envelope**, including how a
  suspended result containing unbound variables is represented.
- **FR-004**: The dossier MUST record a **reuse decision** for the wire — which existing
  transport/framing/codec substrates are reused as-is, and which payload codecs are net-new — with
  the rationale for each.
- **FR-005**: The dossier MUST specify the **control-program startup + client model**: how the
  engine accepts clients, that the front-end is just one kind of client, and the
  single-engine/multi-client implications (including any capability that is a hard prerequisite
  for multiple clients).
- **FR-006**: The dossier MUST specify the **long-running / liveness / crash-signal / restart
  model** — how the engine signals liveness to its host OS, how it signals an unrecoverable state,
  and how it is supervised and restarted.
- **FR-007**: The dossier MUST specify the **persistent-vs-ephemeral state model**: a
  classification of every significant piece of engine state as persistent or ephemeral, the
  re-establish-from-definition rule for ephemeral resources, the **DB-abstraction API shape**
  (database underneath, hidden behind an API), the **bootstrap** behaviour, and the
  **restore-and-resume** behaviour.
- **FR-008**: The dossier MUST record the **mailbox decision** — the choice between an OS-level
  mailbox and a GLP-language mailbox for the MVP and for the longer-term target — with rationale.
- **FR-009**: The dossier MUST present the candidate bounded **MVP slice(s)** with their
  consequences, each naming exactly which net-new capabilities it depends on and which it
  explicitly defers; it MAY mark one as the advisory recommendation, but the choice of MVP slice
  is the owner's decision.
- **FR-010**: The dossier MUST reconcile each requirement/code **premise mismatch** identified by
  the step-1 review (at minimum: compiler location, and runtime-IL generation), stating the
  requirement's assumption, the as-built reality with code locations, the resolving decision, and
  the downstream consequence.
- **FR-011**: The dossier MUST present each **open design question** surfaced by the step-1 review
  as 2–5 distinct, mutually-exclusive options with their consequences, framed for an owner
  decision; it MAY mark one option as the advisory recommendation, but MUST NOT record any such
  fork as already settled — the decision is the owner's.
- **FR-012**: The dossier MUST present an ordered **epic feature breakdown** in which every entry
  has a kind (prep/experiment/MVP/follow-up), a scope, a rationale, and an explicit `depends-on`
  set, and the ordering is a valid topological order (no entry depends on a later entry).
- **FR-013**: Every successor-feature entry in the breakdown MUST cite at least one dossier
  section that motivates it; a breakdown entry with no citable section is a gap to be closed by
  adding the section.
- **FR-014**: For each design area, the dossier MUST distinguish whether it **reuses** an existing
  substrate, **refactors** existing code, or is **net-new**, so downstream effort is not
  under-estimated, and MUST cite the relevant code locations.
- **FR-015**: The feature MUST be **read-only with respect to engine/runtime/REPL code** — it
  produces and updates documents only and changes no executable code.
- **FR-016**: Where the as-built code contradicts a claim inherited from the step-1 review, the
  dossier MUST record the re-verified current reality rather than the stale claim.
- **FR-017**: The dossier MUST identify the design's top risks and, for each, the mitigation
  reflected in the design or the breakdown ordering.
- **FR-018**: Every option the dossier presents for an owner decision (open questions, design
  forks, MVP slice, mailbox, premise-reconciliation choices) MUST be **fully researched** —
  grounded in cited code evidence (`file:line`) and/or established prior art — and explained
  **concisely**: the option, its consequences, and its trade-off stated in a few lines, not a
  narrative.
- **FR-019**: After the owner approves the dossier, the feature MUST seed the successor-feature
  breakdown (features 2–16) into `buildkit-roadmap` as one candidate per successor (carrying kind,
  scope, why, and `depends-on`), and MUST NOT specify, plan, or implement any successor feature —
  drawing the first successor into the pipeline is a separate, later step.

### Key Entities

- **Design Dossier**: the authoritative output document; sections cover the seam contract, wire
  shapes, control-program/client model, liveness/crash/restart model, persistent-vs-ephemeral
  state + DB-abstraction + bootstrap + restore-and-resume, mailbox decision, MVP slice, premise
  reconciliations, open-question option sets, feature breakdown, and risks.
- **Decision Point**: a design area presented as either a forced design (no genuine alternative)
  or a set of fully-researched options with consequences and an optional advisory recommendation
  (where a real choice exists); carries reuse/refactor/net-new classification and affected code
  locations. The owner makes the choice; the dossier does not settle it unilaterally.
- **Premise Reconciliation**: a record pairing an original requirement premise with the as-built
  reality, the available reconciling option(s) with consequences, and the downstream consequence —
  presented for the owner's decision where a genuine choice exists.
- **Open-Question Option Set**: a previously-open design fork presented as 2–5 mutually-exclusive,
  fully-researched options with their consequences and an optional advisory recommendation, framed
  for an owner decision.
- **Successor-Feature Entry**: one item in the epic breakdown — kind, scope, why, `depends-on`,
  and a citation to the motivating dossier section.
- **Source Inputs (read-only)**: the step-1 review (`investigation.md`), the owner requirements
  (`requirements.md`), and the marathon framing (`feature-definition.md`), plus the C# reference
  implementation and the feature-025 link layer the design grounds itself in.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All seven named design areas (seam contract; wire shapes; control-program/client
  model; liveness/crash/restart; persistent-vs-ephemeral + DB-abstraction + bootstrap + resume;
  mailbox decision; MVP slice) are present and each is covered by either a forced design or
  fully-researched options-with-consequences (plus any advisory recommendation) — seven of seven,
  none left uncovered.
- **SC-002**: Both requirement/code premise mismatches (compiler location; runtime-IL generation)
  are reconciled with as-built code locations — two of two.
- **SC-003**: 100% of the open design questions surfaced by the step-1 review are presented in the
  dossier as 2–5 fully-researched, mutually-exclusive options with their consequences (and any
  advisory recommendation), framed for an owner decision.
- **SC-009**: 100% of options presented for an owner decision are grounded in cited evidence
  (`file:line` and/or named prior art) and stated concisely (option + consequences + trade-off in
  a few lines, not a narrative).
- **SC-010**: After the owner approves the dossier, every successor-feature entry (features 2–16)
  exists as a `buildkit-roadmap` candidate carrying kind, scope, why, and `depends-on`, and zero
  successor features have been specified/planned/implemented by this feature.
- **SC-004**: 100% of successor-feature entries in the breakdown carry kind, scope, why, and
  `depends-on`, and 100% cite a motivating dossier section; the dependency ordering has zero
  forward dependencies.
- **SC-005**: A reviewer can locate the design decision behind any wire-crossing component by
  reading the dossier alone, without consulting engine source code.
- **SC-006**: The feature changes zero lines of engine, runtime, or REPL executable code (the diff
  touches documents only).
- **SC-007**: The recommended MVP slice explicitly enumerates the net-new capabilities it depends
  on and the capabilities it defers, with no unstated dependency.
- **SC-008**: Every design area is tagged reuse / refactor / net-new and cites at least one code
  location.

## Assumptions

- **Documentation-only, read-only.** The dossier is produced by reading the step-1 review and the
  existing code; it modifies no engine/runtime/REPL code. (Per `requirements.md`: "output is a
  design + feasibility report … not an implementation.")
- **Present-options, owner decides.** The dossier presents each genuine design/scope fork as
  fully-researched options with their consequences (and may mark one as an advisory
  recommendation), but does not unilaterally settle forks — the owner makes the decisions at the
  marathon approval gate. Options must be grounded in cited evidence and explained concisely
  (FR-011, FR-018).
- **C#-first reference.** The design is grounded in the C# reference implementation (`out/csharp`)
  and the feature-025 link layer, cross-checked against the Dart source; a Dart mirror is noted
  where parity constraints apply but is not the primary subject (per `requirements.md` §0 and
  `feature-definition.md`).
- **Dossier supersedes the design content sketched in the step-1 review.** `investigation.md`
  remains the read-only step-1 review of record; this feature consolidates and finalises its
  design/recommendation content into the authoritative dossier, re-verifying claims against
  current code.
- **Breakdown authored here AND seeded into the roadmap (post-approval).** This feature delivers
  the authoritative, ordered successor-feature breakdown *as dossier content* and, once the owner
  approves the dossier, seeds features 2–16 into `buildkit-roadmap` as candidates (FR-019). It does
  **not** specify, plan, or implement any successor feature — drawing the first successor into the
  pipeline is a separate, later step.
- **Output location.** The dossier lives under `docs/research/repl-engine-separation/` alongside
  its source inputs, unless the owner designates a different authoritative path.
