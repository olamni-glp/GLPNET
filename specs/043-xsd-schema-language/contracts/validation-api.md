# Contract: Validation APIs (schema documents + message instances)

Normative for FR-002, FR-006, FR-007, FR-008, FR-014 (research R6/R7/R12).

## Schema-document validation

`SchemaValidator.Validate(text) → SchemaDocument | SchemaValidationError[]`

- Runs parse + all well-formedness rules (schema-dsl.md §Well-formedness).
- **Every** error carries `{construct, line:col, message}`; all errors in one pass are reported
  (not first-error-only) — the SC-002 seeded-defect suite (≥20 cases) asserts naming + location.
- Cycle errors name the full cycle path (clarification 2).

## Instance validation

`InstanceValidator.Validate(registry, functor, InstanceValue) → ValidationVerdict`
throws `NoSchemaRegisteredError` when `functor` resolves to no registered schema (FR-008 —
an error, distinct from a Fail verdict; never a silent pass).

Check order (makes FR-007 hold by construction):
1. **Kind resolution** via the overlay registry (seed ∪ overlay).
2. **Structure**: the instance conforms to the lowered composition — element presence,
   sequence order, exactly-one choice branch, occurs bounds.
3. **Facets**: value constraints (narrowing only — a facet can only reject values the
   structural layer accepted, never accept values it rejected).

Verdict: `Pass`, or `Fail([Violation{constructKind, constructName, schemaLocation,
instancePath, message}])` — every failure names the violated element/facet AND its path in the
instance (US2 AS-2). Validation is iterative over the instance tree with the schema DAG bound
as the recursion bound — bounded and deterministic on attacker-supplied instances (edge case).

## Agreement law (FR-007 / SC-003)

For every shape expressible at both levels: `registry-level reject ⇒ XSD-level reject`.
Test harness: run the shared corpus (041 `SampleMessages` conforming set + derived
non-conforming mutations) through `MessageCodec.Decode + DecodeGuard.Check` (registry level)
and through `InstanceValidator` (XSD level, via the tests-side `Message → InstanceValue`
adapter); assert zero polarity contradictions.

## Neutral instance form

`InstanceValue` (data-model §4) is the only validation input type. The
`GlpRuntime.CrdtMsg.Message → InstanceValue` adapter lives in the tests project (corpus reuse);
production consumers map their decoded form the same way (documented in quickstart).
No new wire formats or codecs (spec Assumptions).
