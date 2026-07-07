# Phase 0 Research: XSD-Style Schema Language over the Functor Registry

**Feature**: `043-xsd-schema-language` | **Date**: 2026-07-07
**Input**: spec.md (3 clarifications already encoded); verified substrate read of
`csharp/glp_wire_registry/WireRegistry.cs` + `SchemaRegistry.cs` and the 041/042 artifacts.

All spec-level unknowns were resolved in `/bk-clarify` (plaintext qmedit-family DSL, cycles
rejected, evolution refuses without a declared mode). The decisions below resolve the remaining
*technical* unknowns. Per the plan-stage guidance: simplest design that satisfies the spec;
constraints and rejected alternatives explicit.

## R1 — Host language & project placement

**Decision**: C# / net10.0, new hand-authored library project `csharp/glp_schema_lang/`
(`GlpSchemaLang.csproj`, namespace `GlpRuntime.SchemaLang`, `AssemblyName glp_schema_lang`),
with `csharp/glp_schema_lang.tests/` (xUnit 2.9.3 set, `InternalsVisibleTo`). Production project
references **only** `glp_wire_registry`. The tests project additionally references `glp_crdtmsg`
to reuse the golden instance corpus (`SampleMessages`).
**Rationale**: the substrate (registry, codecs, corpus) is entirely C#/net10.0; the whole 041
family follows the flat-under-`csharp/`, no-`.sln`, clobber-safe convention (outside `out/csharp/`
and `glp_runtime_net/`). Keeping the production reference set to the zero-dependency registry leaf
keeps the dependency arrow pointing the same way as `glp_result_codec` → `glp_wire_registry`.
**Alternatives rejected**: implementing inside `glp_wire_registry` (bloats the byte-parity-critical
leaf that `glp_result_codec` depends on); GLP implementation (no spec need, and DISCIPLINE §1.12
GLP-first applies to channel/suspension logic, not a parser/validator library; no GLP language
surface is touched — spec Assumptions); Python/codeconv (wrong toolchain — codeconv is the
conversion harness, not the messaging substrate).

## R2 — Registration substrate: additive seeded overlay, E9 tables untouched

**Problem**: `WireRegistry` and `SchemaRegistry` are static, compile-time-closed tables with no
`Register(...)` API. 043 must let authored schemas "land in the same registry" (US1) without
modifying E9 core semantics or data (FR-012).
**Decision**: an instance-based, in-memory overlay store `SchemaLangRegistry` in
`glp_schema_lang`, **seeded** at construction from `WireRegistry.All` + `SchemaRegistry` forms.
Registration writes overlay rows `{payload_type, functor, compat_mode, qmedit, cddl, xsd_source,
version, cddl_sha256}`; lookups consult overlay + seed as one logical registry; collision checks
(functor and payload-type) run against both (US1 AS-3). The static E9 files are not edited.
**Rationale**: FR-012 verbatim — "the functor registry and its dual-DSL round-trip remain
authoritative and continue to work unchanged". An overlay is the smallest additive construction
that satisfies "same registry, side by side" while leaving the shipped table byte-identical.
In-memory matches the substrate's own persistence level (the 041 registry has no DB/files), so no
new persistence surface (Constitution VI-a/VI-b untouched).
**Alternatives rejected**: making `WireRegistry` mutable (changes E9 core, risks the
byte-parity leaf, violates FR-012's spirit); PGLite/`.pgdb` persistence (substrate has none —
adding a DB is scope creep and drags in bridge/migration surface for zero spec requirement;
FR-004 "stored in the registry" is satisfied by the registry abstraction 041 itself uses);
source-generation of new `WireRegistry` rows (turns registration into a build step — US1 requires
submit-time validation + registration verdicts).

## R3 — Payload-type byte allocation

**Decision**: the overlay allocates payload-type bytes deterministically: the lowest free byte
≥ `PayloadType.MessagingBase + 1` (0x13) not present in seed ∪ overlay, assigned in the schema
document's message-kind declaration order. Explicit collision error if a requested functor or an
exhausted byte space would conflict (never silent).
**Rationale**: 041 FR-034 gives the registry ownership of the payloadType byte space;
`0x12+` is the documented messaging range and 0x12 is taken. Lowest-free-in-declaration-order is
deterministic (FR-005) and needs no configuration.
**Alternatives rejected**: hash-derived bytes (collision-prone in an 8-bit space, not obviously
deterministic to a reader); author-chosen bytes (invites collisions; the registry owns the space).

## R4 — Authoring DSL: qmedit-family surface, named types + facets added

**Decision**: the 043 language is a superset-styled sibling of the stored qmedit form (same
lexical family: `{}` blocks, `name: type` fields, `?` optional suffix, `[T]` arrays, `enum(...)`,
`//` comments), adding: `schema <name> version <n>` header, `type <Name>: <base> { <facets> }`
(named simple types), `type <Name> { sequence { … } }` / `{ choice { … } }` (named complex
types), `occurs <min>..<max|*>` per element, and `message <functor> { … }` (message kinds).
Full grammar is a Phase 1 contract (`contracts/schema-dsl.md`).
**Rationale**: the clarification pins "plaintext DSL in the qmedit family carrying the XSD
concepts"; matching the stored `crdt_message` qmedit surface keeps the two authoring layers
visually one family (US1 "side by side").
**Alternatives rejected**: literal XSD/XML (explicitly clarified out — no XML anywhere);
a JSON/YAML schema carrier (not the qmedit family; worse to hand-author).

## R5 — Lowering target: deterministic CDDL emitter over a fixed subset

**Decision**: a canonical CDDL pretty-printer emitting exactly the idioms the shipped
`crdt_message` CDDL uses, extended with facet controls: maps `name = { k: t, ? k: t }`; arrays
`[* t]` / `[a*b t]` (occurs bounds); enumerations `&( sym: 0, … )`; primitives int→`uint`/`int`,
str→`tstr`, bytes→`bstr`, bool→`bool`; facets → CDDL controls `.size` (length bounds),
`.regexp` (pattern), range `lo..hi` (numeric min/max). Canonical form: rule order = declaration
order, message-kind rule first, fixed 2-space indent, trailing commas as in the shipped artifact.
One CDDL artifact per schema document; one functor registration per `message` declaration.
**Rationale**: FR-003 says lowered entries must be accepted by existing registry tooling
unchanged — the existing "tooling" stores CDDL text verbatim, so alignment with the shipped
idioms is the compatibility bar; a fixed emitter with canonical ordering gives FR-005 determinism
by construction (golden-file tests pin it).
**Alternatives rejected**: emitting via a general CDDL library (none present; zero-dependency
discipline); non-canonical emission (breaks FR-005 and golden tests).

## R6 — Facet consistency & the pattern facet: restricted regex subset with decidable emptiness

**Decision**: schema-validation-time facet checks: numeric `min ≤ max`; `minLength ≤ maxLength`;
enumeration non-empty and every member satisfies the co-facets; pattern must (a) parse in a
**restricted regex subset** — literals, character classes/ranges, `.`, grouping, alternation,
`* + ? {n,m}` quantifiers, implicit full anchoring — and (b) be non-empty, checked by compiling
to a small NFA and testing accept-state reachability. Patterns using constructs outside the
subset are a schema-validation error naming the construct (loud, never silently accepted).
Instance-time pattern matching runs on the same NFA (linear-time simulation, no backtracking).
**Rationale**: the spec's edge cases demand "pattern that matches nothing" be rejected at
schema-validation time and demand bounded, deterministic instance validation. Emptiness is
decidable for pure regular expressions; a hand-rolled NFA over a fixed subset keeps both checks
exact, bounded, and dependency-free. .NET `Regex` emptiness is not checkable and backtracking
breaks the boundedness edge case.
**Alternatives rejected**: full .NET `Regex` (undecidable emptiness check, unbounded
backtracking); skipping the emptiness check (violates the edge case / FR-002 facet consistency).

## R7 — Instance representation & registry-level agreement (FR-006/FR-007/FR-008)

**Decision**: a neutral ground-term tree `InstanceValue` (Int, Str, Bytes, Bool, Symbol, List,
Struct-with-named-fields) in `glp_schema_lang` is the validation input. The tests project
carries the adapter `GlpRuntime.CrdtMsg.Message → InstanceValue` for corpus reuse (SC-001/003);
the adapter seam is documented in quickstart so future runtimes map their decoded form the same
way. Instance validation resolves the message kind through the overlay registry first —
unknown kind ⇒ explicit `no schema registered` error (FR-008) — then checks lowered-shape
conformance (structure), then facets (narrowing only). SC-003's zero-contradiction check runs
the corpus through both the registry-level path (`MessageCodec.Decode` + `DecodeGuard`) and the
XSD-level validator and diffs verdict polarity.
**Rationale**: the spec pins "message instances as the registry layer already understands them"
and "no new wire formats or codecs" — a neutral term tree adapts what codecs already decode
without touching them; ordering registry-resolution → structure → facets makes FR-007's
"narrow, never widen" hold by construction.
**Alternatives rejected**: validating raw wire bytes (duplicates codecs — forbidden by the
no-new-codecs assumption); coupling `glp_schema_lang` to `glp_crdtmsg`'s `Message` record in
production (inverts the dependency discipline of R1).

## R8 — Compatibility-mode semantics: construct-level rule table

**Decision**: evolution checking compares old/new schema ASTs construct-by-construct under a
fixed rule table (Confluent-style semantics, `contracts/compat-evolution.md` is normative):
**backward** (new reader, old data): adding elements requires `occurs` min 0 (optional);
removing an element is compatible; facet **widening** is compatible, **narrowing** breaks;
choice-branch removal breaks, branch addition is compatible. **forward** (old reader, new data):
the mirror rules. **full** = backward ∧ forward (additive-optional only). **transitive**
variants: the same check run against **every** stored prior version in the type's chain, not
just the latest. Verdicts name each breaking construct with its location. Registration of an
incompatible version requires an explicit recorded override `{verdict, acknowledger, reason}`
(US4 AS-3); a type with **no declared mode refuses** the check/registration with an explicit
error (clarification 3; FR-011). Seeded kinds carry their shipped `CompatMode`; new
registrations must declare one.
**Rationale**: the spec consumes the registry's declared Confluent-style modes rather than
redefining them; a deterministic construct-level table is testable by the SC-005 curated suite
and needs no instance-set reasoning.
**Alternatives rejected**: semantic instance-set inclusion checking (equivalent to language
inclusion — heavier than the spec requires and hard to localize errors from); default-assuming
backward when no mode is declared (explicitly clarified out).

## R9 — Drift detection (FR-013)

**Decision**: at registration the overlay stores `sha256(cddl)` (and `sha256(qmedit)`) of the
lowered forms. On lift/view/re-lower of an entry with stored XSD source, the current registry
forms are re-hashed and compared; mismatch ⇒ the result carries an explicit drift flag naming
which form diverged, and the lift renders **current registry truth**, never the stale XSD source.
**Rationale**: hash-compare is the smallest deterministic divergence detector; rendering current
truth satisfies the edge case verbatim.
**Alternatives rejected**: textual diff at view time only (equivalent but weaker as a stored
record); forbidding out-of-band edits (can't — E9 layer stays authoritative and independently
editable, FR-012).

## R10 — Lift: recursive-descent CDDL-subset parser + fidelity report

**Decision**: lift parses the entry's registered CDDL with a hand-written recursive-descent
parser covering exactly the R5 emitter subset **plus** the shipped `crdt_message` idioms.
Constructs outside the expressible set produce per-construct `FidelityReport` entries and a
partial-lift verdict (FR-009) — never approximation. Entries with no CDDL artifact at all
(seeded `il_program`, `result_envelope`) lift to an explicit whole-entry "no CDDL artifact —
byte+functor registration only" partial report. Lift(lower(doc)) equivalence (FR-010) is tested
by structural AST comparison plus accept/reject agreement over the instance corpus (SC-004).
**Rationale**: parsing only the subset we emit plus the one shipped artifact keeps the parser
small and the expressible set honest; the two legacy kinds prove the partial-lift path on real
registry content.
**Alternatives rejected**: full RFC 8610 CDDL parser (large surface, zero spec need); lifting
from the qmedit form instead of CDDL (CDDL is the *registered* artifact — E9; qmedit is the
human surface).

## R11 — Determinism discipline (FR-005)

**Decision**: lowering/lift/compat are pure functions of their inputs — no timestamps, no
randomness, no dictionary-order iteration (explicit ordered collections only); canonical emitter
(R5); golden-file tests assert byte-identical CDDL for the walkthrough schema and re-run lowering
twice per test to assert self-identity.
**Rationale**: FR-005 verbatim; golden files make regressions build-detectable, same as the
registry's own SC-010 style.

## R12 — Error model

**Decision**: one structured error/verdict family (records, not bare exceptions) for all five
operation classes (authoring/schema-validation, lowering, instance validation, lift,
compatibility): `{operation, construct, location (line:col or instance path), message}`.
Loud-fail everywhere; nothing falls back silently (FR-014). Exceptions remain only for
programming errors (e.g. overlay misuse), mirroring `WireRegistryException` style.
**Rationale**: FR-002/FR-006/FR-009/FR-011/FR-014 all demand construct-level localization —
that is a data-shape requirement, so verdicts are data. The substrate's exception-only style is
insufficient for "identify the violated element/facet and its location" and the spec's
Key Entities (fidelity report, compatibility verdict) are records.

## Out of scope (re-affirmed)

Schema-driven codegen / verified parsers (BB-SCH-3, post-MVP); GLP language changes (none —
§1.14 gate not triggered; any future GLP-side surface stops for propose-first approval); new
wire formats or codecs; persistence beyond the substrate's in-memory level.
