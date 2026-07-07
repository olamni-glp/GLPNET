# Contract: Compatibility Checking & Schema Evolution

Normative for FR-011 (research R8). Modes are the registry's existing
`GlpRuntime.WireRegistry.CompatMode` — consumed, never redefined (spec Assumptions).

## API

`CompatChecker.Check(oldDoc, newDoc, CompatMode) → CompatVerdict`
`SchemaLangRegistry.RegisterVersion(newDoc, artifacts) → RegistryRecord[] | NoCompatModeDeclaredError | RequiresOverride(CompatVerdict)`
`SchemaLangRegistry.RegisterVersionWithOverride(newDoc, artifacts, OverrideRecord) → RegistryRecord[]`

## Refusal law (clarification 3)

A type with **no declared compatibility mode** refuses both the check and the registration of a
new version with an explicit `NoCompatModeDeclaredError` — never a silently assumed default.
(Seeded kinds carry their shipped mode; 043-registered kinds declare one at first registration.)

## Construct-level rule table

Direction vocabulary: **backward** = new readers understand old writers (new schema must accept
all old-valid data); **forward** = old readers tolerate new writers (old schema must accept all
new-valid data). This matches the `CompatMode` XML-docs in `WireRegistry.cs`.

| Change in v(n+1) | Backward | Forward |
|---|---|---|
| Add element with `occurs` min = 0 (optional) | compatible | compatible |
| Add element with min ≥ 1 (mandatory) | **breaks** | compatible |
| Remove optional element | compatible | compatible |
| Remove mandatory element | compatible | **breaks** |
| Widen a facet (larger range/length, added enum member, superset pattern†) | compatible | **breaks** |
| Narrow a facet (smaller range/length, removed enum member, subset pattern†) | **breaks** | compatible |
| Widen `occurs` bounds | compatible | **breaks** |
| Narrow `occurs` bounds | **breaks** | compatible |
| Add a choice branch | compatible | **breaks** |
| Remove a choice branch | **breaks** | compatible |
| Change an element's type to a non-equivalent type | **breaks** | **breaks** |
| Reorder sequence elements | **breaks** | **breaks** |

† Pattern subset/superset is decided on the R6 NFAs (language inclusion on the restricted
subset); a pattern change whose inclusion cannot be established is conservatively **breaking**
in both directions, with the verdict saying so explicitly.

- **Full** = Backward ∧ Forward (additive-optional only — matches the shipped XML-doc).
- **Transitive** variants: the same check run against **every** stored version in the type's
  `VersionChain`, not only v(n); first failing pair is named in the verdict.

## Verdict & override

`CompatVerdict{mode, outcome, breaks: [BreakingConstruct{construct, location, direction,
rule-table row}]}` — every breaking construct is named (US4 AS-2; SC-005).

Registering an **incompatible** version requires an explicit `OverrideRecord{verdict,
acknowledger, reason}`; the record is stored on the resulting `RegistryRecord` and retrievable
with it. No incompatible version is ever registered silently (US4 AS-3).
