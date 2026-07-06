# Feature Specification: Higher-Level XML-Schema-Style Schema Language over the Functor Registry

**Feature Branch**: `043-xsd-schema-language`
**Created**: 2026-07-06
**Status**: Draft
**Input**: User description: "Higher-level XML-Schema-style schema language over the functor registry"

## Context

Feature 041 shipped the E9-ruled experimental functor registry (041 FR-032/FR-033): the MVP
schema is a ground GLP term with a registered functor per message kind, and the registry hosts a
dual-DSL representation — schemas authored in the qmedit plaintext DSL, agentically translated to
CDDL (the formally registered artifact) and back, both forms stored. That layer covers message
*shapes*. What it deliberately deferred (041 spec "out of scope", E9 addendum; roadmap capture
2026-07-04, Gabi verbatim: "higher-level schema based on xmlschema-style schema language DEFERRED
for later but captured NOW - CRITICAL and MANDATORY") is a *rich* schema layer above it: named
reusable types, value facets, and structural composition in the style of XML Schema — authorable,
validatable, and translatable down to the registry's existing CDDL + functor registrations.

This feature adds that layer. It sits strictly ABOVE the E9 core: the functor registry and its
dual-DSL round-trip remain the authoritative registration substrate, unchanged.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Author a rich schema and land it in the registry (Priority: P1)

A schema author writes a message schema in the new XSD-style language: named simple types with
facets (numeric ranges, string length bounds, patterns, enumerations), complex types composed from
sequences and choices of elements with optionality and repetition counts, and reuse of previously
defined named types. The system validates the schema document itself and, on success, lowers it
into the registry's existing forms — a CDDL registered artifact plus the functor registration(s)
for each message kind — so a schema authored at the XSD level lands in the same registry, side by
side with entries authored directly in qmedit DSL.

**Why this priority**: Authoring-to-registration is the entire reason the layer exists; without
lowering into the E9 registry the language is an island. Every other story consumes its output.

**Independent Test**: Author one schema exercising each construct class (facets, sequence, choice,
optionality, repetition, type reuse), lower it, and confirm the registry now holds a CDDL artifact
and functor registration(s) for it that the existing registry tooling accepts.

**Acceptance Scenarios**:

1. **Given** a well-formed schema document using named types, facets, and composition, **When** the
   author submits it, **Then** the system reports the schema valid and produces the lowered CDDL
   artifact and functor registration(s), all recorded in the registry with the XSD-level source
   retained alongside the existing dual-DSL forms.
2. **Given** a schema document with a defect (an undefined type reference, a contradictory facet
   pair such as a minimum above a maximum, or a duplicate element name), **When** the author submits
   it, **Then** the system rejects it with an error naming the offending construct and its location,
   and registers nothing.
3. **Given** a schema whose lowering would collide with an existing registration (same message-kind
   functor already registered with a different shape), **When** the author submits it, **Then** the
   system reports the collision explicitly and registers nothing — never silently overwrites.

---

### User Story 2 - Validate message instances against an XSD-level schema (Priority: P2)

An implementer checks a concrete message instance against a registered XSD-level schema and gets a
verdict. On failure the verdict pinpoints which element or facet was violated, not merely
"invalid".

**Why this priority**: Validation is how the richer constraints (facets, composition) pay off at
runtime and in tests; it is the first consumer of registered schemas but requires US1's output.

**Independent Test**: Take one registered schema and a corpus of conforming and non-conforming
instances; confirm every verdict is correct and every failure names the violated construct.

**Acceptance Scenarios**:

1. **Given** a registered schema and a conforming message instance, **When** validation runs,
   **Then** the verdict is pass.
2. **Given** a registered schema and an instance violating a facet (e.g. out-of-range value) or the
   composition (e.g. missing mandatory element, wrong branch arity), **When** validation runs,
   **Then** the verdict is fail and identifies the violated element/facet and its location in the
   instance.
3. **Given** an instance whose message kind has no registered schema, **When** validation is
   requested, **Then** the system reports "no schema registered for this kind" as an explicit error
   — never a silent pass (the loud-fail law of the corpus applies to schema resolution too).

---

### User Story 3 - Lift an existing registry entry into the XSD-level view (Priority: P3)

A design-team member opens an existing registry entry (registered CDDL + functor form, possibly
authored pre-043 in qmedit DSL) and views it lifted into the XSD-style representation. Constructs
the richer language can express are shown natively; constructs it cannot faithfully express are
explicitly reported as unexpressible rather than silently approximated.

**Why this priority**: Lift closes the round-trip and makes the new layer a lens over the whole
existing registry, but the layer is already useful for new schemas without it.

**Independent Test**: Lift every entry registered by the 041 MVP set; confirm each yields either a
faithful XSD-level rendering or an explicit per-construct unexpressibility report.

**Acceptance Scenarios**:

1. **Given** a registry entry whose shape is expressible in the XSD-style language, **When** it is
   lifted, **Then** the rendering, lowered again, is semantically equivalent to the original entry
   (accepts and rejects the same instances).
2. **Given** a registry entry using a construct outside the language's expressible set, **When** it
   is lifted, **Then** the output marks exactly the unexpressible constructs and the fidelity report
   says the lift is partial — no silent approximation.

---

### User Story 4 - Evolve a schema under a compatibility mode (Priority: P3)

A schema author submits a new version of an already-registered schema together with the registry's
declared compatibility mode for that type (the Confluent-style backward / forward / full /
transitive modes the unified registry carries). The system reports a compatibility verdict —
which mode(s) the evolution satisfies and, on violation, which construct breaks it — before
anything is registered.

**Why this priority**: Evolution is where schema registries earn their keep long-term, but it
presupposes registered v1 schemas (US1) and a validation notion (US2).

**Independent Test**: Run a curated evolution suite (field additions, removals, facet widenings/
narrowings, choice-branch changes) and confirm each case gets the expected verdict.

**Acceptance Scenarios**:

1. **Given** a registered schema v1 and a v2 that only adds optional elements, **When** the author
   requests a compatibility check under backward mode, **Then** the verdict is compatible.
2. **Given** a v2 that removes a mandatory element or narrows a facet, **When** checked under
   backward mode, **Then** the verdict is incompatible and names the breaking construct.
3. **Given** an incompatible v2, **When** the author nevertheless asks to register it, **Then** the
   system requires an explicit override acknowledgement and records verdict + override; it never
   registers an incompatible version silently.

---

### Edge Cases

- Cyclic type references (type A composed of B, B of A): must be detected and either supported
  with well-defined semantics or rejected with a precise error — never a hang or stack overflow.
- Facet contradictions (min > max, empty enumeration, pattern that matches nothing) rejected at
  schema-validation time, not discovered at instance-validation time.
- Name collisions: two named types with the same name in one schema document; a schema name
  colliding with an already-registered one.
- Lowering a construct combination the CDDL/functor layer cannot carry: explicit unlowerable
  error listing the construct(s) — the schema is not partially registered.
- A registry entry edited out-of-band (directly at the CDDL/functor level) after being registered
  via the XSD layer: the lift view must show the current registry truth and flag that the stored
  XSD-level source no longer matches (drift detection), not show the stale source as current.
- Very large schema documents and instance payloads: bounded, deterministic behavior (no
  unbounded recursion into attacker-supplied instance structure during validation).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide a schema language with, at minimum: primitive value types;
  user-named simple types constrained by facets (numeric minimum/maximum, string length bounds,
  pattern, enumeration); complex types composed of ordered sequences and exclusive choices of
  named elements; per-element optionality and repetition bounds; and reuse of named types across
  definitions within a schema document.
- **FR-002**: The system MUST validate a schema document itself (well-formedness of definitions,
  resolvability of every type reference, facet consistency, name uniqueness) and reject invalid
  documents with errors naming the offending construct and its location.
- **FR-003**: The system MUST lower a valid schema document into the registry's existing
  registered forms — a CDDL artifact and the functor registration(s) per message kind — such that
  the lowered entries are accepted by the existing registry tooling unchanged.
- **FR-004**: The XSD-level source form MUST be stored in the registry alongside the existing
  dual-DSL forms for entries authored through this layer, so that all stored representations of an
  entry remain retrievable together.
- **FR-005**: Lowering MUST be deterministic: the same schema document always produces the same
  lowered artifacts.
- **FR-006**: The system MUST validate message instances against a registered schema, returning
  pass, or fail with the violated element/facet identified and located in the instance.
- **FR-007**: Instance-validation verdicts MUST agree with the underlying registry-level (CDDL /
  functor) validation for every shape expressible at both levels: nothing the lower layer rejects
  may pass the XSD layer, and facet/composition constraints may only narrow, never widen, the
  accepted set.
- **FR-008**: Requesting validation for a message kind with no registered schema MUST produce an
  explicit "no schema registered" error, never a silent pass or silent default.
- **FR-009**: The system MUST lift an existing registry entry into the XSD-style representation
  where expressible, and for partial lifts produce a fidelity report marking exactly the
  unexpressible constructs; lifts are never silently approximated.
- **FR-010**: Lower-then-lift of a schema authored in the new language MUST reproduce a
  semantically equivalent schema (same accept/reject behavior over instances) or state precisely
  where equivalence is lost.
- **FR-011**: The system MUST evaluate a proposed new version of a registered schema against the
  type's declared compatibility mode (backward, forward, full, transitive variants) and report a
  verdict naming any breaking construct; registering an incompatible version MUST require an
  explicit recorded override.
- **FR-012**: The XSD layer MUST NOT modify the E9 core's semantics or data: the functor registry
  and its dual-DSL (qmedit ↔ CDDL) round-trip remain authoritative and continue to work unchanged
  for entries never touched by this layer.
- **FR-013**: If a registry entry with stored XSD-level source is later changed at the registry
  level out-of-band, the system MUST detect and surface the divergence when the entry is viewed or
  re-lowered, rather than presenting the stale XSD-level source as current.
- **FR-014**: All schema-language errors (authoring, lowering, validation, lift, compatibility)
  MUST be reported explicitly with construct-level localization; no operation in this feature may
  fall back silently on error.

### Key Entities

- **Schema document**: an authored unit in the XSD-style language; contains named type
  definitions and message-kind declarations; has an identity and a version.
- **Named type**: a simple type (primitive + facets) or complex type (composition of elements);
  referenced by name within a schema document.
- **Element declaration**: a named slot inside a complex type with a type reference, optionality,
  and repetition bounds.
- **Facet**: a value constraint on a simple type (range, length, pattern, enumeration).
- **Registry entry**: the existing E9 registration unit (functor + CDDL, plus qmedit form);
  extended by this feature with an optional stored XSD-level source form.
- **Lowering artifact set**: the CDDL artifact + functor registration(s) produced from one schema
  document.
- **Fidelity report**: the per-construct expressibility outcome of a lift (full / partial with
  named unexpressible constructs).
- **Compatibility verdict**: the outcome of checking a proposed version against a declared
  compatibility mode, including any breaking constructs and any recorded override.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Every message kind registered by the 041 MVP set can be re-expressed in the new
  language, and each re-expression lowers to registry entries that accept and reject the same
  instance corpus as the original entries (zero divergence over the corpus).
- **SC-002**: 100% of a seeded-defect suite of invalid schema documents (at least 20 cases
  covering unresolved references, facet contradictions, name collisions, and cycle errors) is
  rejected with an error that names the offending construct and location.
- **SC-003**: On a shared corpus of conforming and non-conforming instances, XSD-level validation
  verdicts show zero contradictions with registry-level validation (nothing rejected below passes
  above), and every failure verdict identifies the violated construct.
- **SC-004**: 100% of lifted 041-MVP registry entries yield either a faithful rendering (proven by
  lower-then-compare equivalence over the instance corpus) or an explicit fidelity report naming
  the unexpressible constructs — zero silent approximations.
- **SC-005**: A curated schema-evolution suite (at least 10 cases spanning additions, removals,
  facet narrowing/widening, and choice changes) receives the expected compatibility verdict in
  100% of cases, and no incompatible version can be registered without a recorded override.
- **SC-006**: A schema author can author and register a new message schema end-to-end without
  hand-writing any CDDL or functor registration (task completed entirely at the XSD level in a
  scripted walkthrough).

## Assumptions

- The 041 functor registry and its dual-DSL (qmedit ↔ CDDL) round-trip are the substrate and
  remain authoritative; this feature layers on top and changes none of their semantics (E9 stays
  settled; 041's shipped behavior is untouched).
- "XML-Schema-style" is taken as concept-level fidelity — named types, facets, composition,
  optionality/cardinality, versioned evolution — not a commitment to XML as the authoring
  syntax; the concrete notation is chosen at design time in keeping with the corpus's plaintext
  authoring discipline (qmedit family). If Gabi intends literal XML/XSD syntax, that surfaces in
  clarification.
- Instance validation (US2) operates on message instances as the registry layer already
  understands them; this feature adds no new wire formats or codecs.
- Schema-driven code generation and verified parser generation remain out of scope (that is
  BB-SCH-3, explicitly post-MVP in the synthesis).
- The GLP language itself is not modified: no new guards, directives, or type-system features are
  proposed, so the DISCIPLINE §1.14 language-authority gate is not triggered. If design later
  suggests a GLP-side surface, it stops for propose-first approval.
- Compatibility modes and their meanings are those the unified registry already declares
  (Confluent-style backward / forward / full / transitive); this feature consumes them rather than
  redefining them.
- The agentic-translation path the E9 ruling established may assist lift/lower tooling, but every
  contract in this spec (validation, determinism, fidelity reporting) holds deterministically and
  is verified by tests, not delegated to model judgement.
