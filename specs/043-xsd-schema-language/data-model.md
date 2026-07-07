# Phase 1 Data Model: XSD-Style Schema Language over the Functor Registry

**Feature**: `043-xsd-schema-language` | **Date**: 2026-07-07
All entities are immutable C# records in `GlpRuntime.SchemaLang` unless noted. No database —
the only store is the in-memory seeded overlay (research R2).

## 1. Schema document (AST)

| Entity | Fields | Notes |
|---|---|---|
| `SchemaDocument` | `Name`, `Version:int`, `Types: IReadOnlyList<NamedType>`, `Messages: IReadOnlyList<MessageDecl>`, `Source:string` | one authored unit; `Source` retained verbatim for FR-004 |
| `NamedType` (abstract) | `Name`, `Location` | name unique per document (FR-002) |
| `SimpleType : NamedType` | `Base: PrimitiveKind`, `Facets: IReadOnlyList<Facet>` | primitive + facets |
| `ComplexType : NamedType` | `Composition: Composition` | sequence or choice |
| `Composition` | `Kind: {Sequence, Choice}`, `Elements: IReadOnlyList<ElementDecl>` | element names unique within one composition (FR-002) |
| `ElementDecl` | `Name`, `TypeRef: TypeRef`, `Occurs: Occurs`, `Location` | named slot |
| `TypeRef` | `Name` (named type) **or** `Primitive: PrimitiveKind` | must resolve (FR-002) |
| `Occurs` | `Min:int`, `Max:int?` (`null` = unbounded `*`) | default `1..1`; `0..1` = optional |
| `PrimitiveKind` | `Int, Str, Bytes, Bool` | aligns with qmedit/CDDL primitives (R5); no standalone symbol — symbolic constants are str-base enum members (analyze I2: str and symbol would both lower to `tstr`, breaking deterministic lift) |
| `MessageDecl` | `Functor`, `Body: Composition`, `Location` | one per message kind; lowers to one functor registration |

### Facets (on `SimpleType` only)

| Facet | Applies to | Schema-validation rule |
|---|---|---|
| `MinValue(long)` / `MaxValue(long)` | Int | `min ≤ max` |
| `MinLength(int)` / `MaxLength(int)` | Str, Bytes | `0 ≤ minLength ≤ maxLength` |
| `Pattern(string)` | Str | parses in the restricted regex subset (R6); NFA non-empty |
| `Enumeration(IReadOnlyList<string>)` | Str, Int | non-empty; members distinct; each member satisfies co-facets |

**Validation rules (FR-002)**: every `TypeRef.Name` resolves within the document; type names
unique; element names unique per composition; facet consistency per table; the type-reference
graph is a DAG — any cycle (incl. self-reference) is rejected with an error naming the full
cycle path (clarification 2). Composition nesting is by named-type reference only, so DAG-ness
bounds all recursion (edge case: bounded, deterministic).

## 2. Registry overlay (research R2)

| Entity | Fields | Notes |
|---|---|---|
| `SchemaLangRegistry` (class, instance) | seed = `WireRegistry.All` + `SchemaRegistry` forms; `Overlay: ordered list<RegistryRecord>` | the one mutable object; registration is append-only within an instance |
| `RegistryRecord` | `PayloadType:byte`, `Functor`, `CompatMode` (nullable **only** for seeded legacy rows — new registrations must declare), `QmeditDsl`, `Cddl`, `XsdSource:string?`, `SchemaName`, `Version:int`, `CddlSha256`, `QmeditSha256` | FR-004: XSD source stored alongside dual-DSL forms; hashes for drift (R9) |
| `VersionChain` | per `(SchemaName, Functor)`: ordered `RegistryRecord` versions | transitive modes check the whole chain (R8) |

Collision law (US1 AS-3): registering a functor or payload-type already present in seed ∪
overlay with a different shape ⇒ `LoweringError(collision)`; nothing is written (all-or-nothing
per schema document — FR-003 edge case "not partially registered").

## 3. Lowering artifact set (FR-003/FR-005)

| Entity | Fields |
|---|---|
| `LoweringArtifactSet` | `Cddl:string` (canonical, R5), `Registrations: IReadOnlyList<FunctorRegistration>` |
| `FunctorRegistration` | `Functor`, `PayloadType:byte` (allocated per R3), `CompatMode` |

Deterministic: same `SchemaDocument` ⇒ byte-identical `LoweringArtifactSet` (golden tests).
Unlowerable construct ⇒ `LoweringError` listing every offending construct; nothing registered.

## 4. Instance validation (FR-006..008)

| Entity | Fields | Notes |
|---|---|---|
| `InstanceValue` (union) | `Int(long)`, `Str(string)`, `Bytes(byte[])`, `Bool(bool)`, `List(IReadOnlyList<InstanceValue>)`, `Struct(name, ordered fields name→InstanceValue)` | neutral ground-term tree (R7); adapters map decoded symbolic enums to `Str` (or `Int` for numeric enums) |
| `ValidationVerdict` | `Pass` **or** `Fail(Violations: IReadOnlyList<Violation>)` | never silent |
| `Violation` | `ConstructKind: {Element, Facet, Composition, Kind}`, `ConstructName`, `SchemaLocation`, `InstancePath` (e.g. `header.policy.targets[2]`), `Message` | FR-006 localization |

Unknown message kind ⇒ **not** a `Fail` verdict but an explicit `NoSchemaRegisteredError`
(FR-008 — distinct from instance-invalid).

## 5. Lift & fidelity (FR-009/FR-010/FR-013)

| Entity | Fields |
|---|---|
| `LiftResult` | `Rendering: SchemaDocument?`, `Fidelity: FidelityReport`, `Drift: DriftReport?` |
| `FidelityReport` | `Outcome: {Full, Partial}`, `Unexpressible: IReadOnlyList<UnexpressibleConstruct>` |
| `UnexpressibleConstruct` | `CddlConstruct:string` (verbatim), `Location`, `Reason` |
| `DriftReport` | `Form: {Cddl, Qmedit}`, `StoredSha256`, `CurrentSha256` | present iff stored hash ≠ current (R9); lift renders current registry truth |

## 6. Compatibility & evolution (FR-011)

| Entity | Fields |
|---|---|
| `CompatVerdict` | `Mode: CompatMode` (the declared mode checked), `Outcome: {Compatible, Incompatible}`, `Breaks: IReadOnlyList<BreakingConstruct>` |
| `BreakingConstruct` | `Construct`, `Location`, `Direction: {Backward, Forward}`, `Rule` (the R8 table row violated) |
| `OverrideRecord` | `Verdict: CompatVerdict`, `Acknowledger:string`, `Reason:string` | required to register an incompatible version (US4 AS-3); stored on the `RegistryRecord` |
| `NoCompatModeDeclaredError` | `Functor` | refusal when the type has no declared mode (clarification 3) |

## 7. Error model (R12, FR-014)

All five operation classes return/raise structured records carrying
`{Operation, Construct, Location, Message}`:
`SchemaValidationError` (list-of, from document validation), `LoweringError`,
`NoSchemaRegisteredError`, `LiftError`, `NoCompatModeDeclaredError`. `CompatMode` reuses
`GlpRuntime.WireRegistry.CompatMode` — not redefined (spec Assumptions).

## State transitions

```
authored text ──parse──► SchemaDocument ──validate──► valid | SchemaValidationError*
valid doc ──lower──► LoweringArtifactSet ──register(all-or-nothing)──► RegistryRecord(s)
                                          └─collision/unlowerable──► LoweringError (nothing written)
registry entry ──lift──► LiftResult{rendering|partial, fidelity, drift?}
(v_n, v_{n+1}, declared mode) ──check──► CompatVerdict ──register──► ok | requires OverrideRecord
(no declared mode) ──check/register──► NoCompatModeDeclaredError (refusal)
```
