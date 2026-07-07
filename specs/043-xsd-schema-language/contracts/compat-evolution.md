# Contract: Compatibility Checking & Schema Evolution

Normative for FR-011 (research R8). Modes are the registry's existing
`GlpRuntime.WireRegistry.CompatMode` — consumed, never redefined (spec Assumptions).

## API

`CompatChecker.Check(oldDoc, newDoc, CompatMode) → CompatVerdict`
`SchemaLangRegistry.RegisterVersion(newDoc, artifacts) → RegistryRecord[] | NoCompatModeDeclaredError | RequiresOverride(CompatVerdict)`
`SchemaLangRegistry.RegisterVersionWithOverride(newDoc, artifacts, OverrideRecord) → RegistryRecord[]`

All three registry entry points refuse an UNVALIDATED document with its schema-error list
before any comparison (lowering.md registration law 0): the common-element comparison never
resolves added elements or brand-new kinds, so entry validation is the only gate keeping an
invalid document out of the version chains.

## Refusal law (clarification 3)

A type with **no declared compatibility mode** refuses both the check and the registration of a
new version with an explicit `NoCompatModeDeclaredError` — never a silently assumed default.
(Seeded kinds carry their shipped mode; 043-registered kinds declare one at first registration.)

## Construct-level rule table

Direction vocabulary: **backward** = new readers understand old writers (new schema must accept
all old-valid data); **forward** = old readers tolerate new writers (old schema must accept all
new-valid data). This matches the `CompatMode` XML-docs in `WireRegistry.cs`.

**Closed-world law**: 043 instance validation is closed-world (validation-api.md — an element
not declared in the composition is a violation). Therefore removing **any** element is
backward-breaking: old-valid instances that carry the removed element are rejected by v(n+1).
This matches spec US4 acceptance scenario 2 (removal of a mandatory element under backward mode
⇒ incompatible); Avro-style ignore-unknown-field semantics do NOT apply here.

| Change in v(n+1) | Backward | Forward |
|---|---|---|
| Add element with `occurs` min = 0 (optional) | compatible | compatible |
| Add element with min ≥ 1 (mandatory) | **breaks** | compatible |
| Remove optional element | **breaks** (closed-world) | compatible |
| Remove mandatory element | **breaks** (closed-world) | **breaks** |
| Widen a facet (larger range/length, added enum member, superset pattern†) | compatible | **breaks** |
| Narrow a facet (smaller range/length, removed enum member, subset pattern†) | **breaks** | compatible |
| Widen `occurs` bounds (within one representation‡) | compatible | **breaks** |
| Narrow `occurs` bounds (within one representation‡) | **breaks** | compatible |
| Change `occurs` across the scalar/list representation boundary‡ | **breaks** | **breaks** |
| Add a choice branch | compatible | **breaks** |
| Remove a choice branch | **breaks** | compatible |
| Change an element's type to a non-equivalent type | **breaks** | **breaks** |
| Reorder sequence elements | **breaks** | **breaks** |

† Pattern subset/superset is decided on the R6 NFAs (language inclusion on the restricted
subset); a pattern change whose inclusion cannot be established is conservatively **breaking**
in both directions, with the verdict saying so explicitly.

‡ **Representation-shift law**: instance validation (validation-api.md) represents an element
with occurs `1..1` or `0..1` as a **scalar** value and an element with any other bounds as a
**list**. An occurs change where exactly one side is in {`1..1`, `0..1`} therefore changes the
value representation (scalar ↔ list): every old-valid instance is rejected by the new schema
and every new-valid instance by the old one, so it is breaking in **both** directions
regardless of whether the numeric bounds widened or narrowed, and the verdict names the rule
"occurs representation shift". The widen/narrow rows apply only to changes that stay within
one representation.

- **Full** = Backward ∧ Forward — under the closed-world law this reduces to add-optional-only
  plus facet/occurs changes that are neither widening nor narrowing (matches the shipped
  XML-doc: "additive-only evolution").
- **Transitive** variants: the same check run against **every** stored version in the type's
  `VersionChain`, not only v(n); first failing pair is named in the verdict.

## Verdict & override

`CompatVerdict{mode, outcome, breaks: [BreakingConstruct{construct, location, direction,
rule-table row}]}` — every breaking construct is named (US4 AS-2; SC-005).

Registering an **incompatible** version requires an explicit `OverrideRecord{verdict,
acknowledger, reason}`; the record is stored on the resulting `RegistryRecord` and retrievable
with it. No incompatible version is ever registered silently (US4 AS-3).
