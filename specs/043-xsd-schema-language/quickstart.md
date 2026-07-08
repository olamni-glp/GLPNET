# Quickstart: 043 XSD-Style Schema Language

**Feature**: `043-xsd-schema-language` | net10.0, C#, xUnit — no new external dependencies.

## Build & test

```bash
dotnet build csharp/glp_schema_lang/GlpSchemaLang.csproj
dotnet test csharp/glp_schema_lang.tests
```

Substrate stays green (FR-012 — nothing under `glp_wire_registry`/`glp_crdtmsg` is modified):

```bash
dotnet test csharp/glp_wire_registry.tests
dotnet test csharp/glp_crdtmsg.tests
```

Baseline both suites green before the first 043 change and after every change (Test Protocol).

## End-to-end authoring walkthrough (SC-006 — no hand-written CDDL or functor registration)

1. Author a schema in the 043 DSL (`contracts/schema-dsl.md` example: `chat` schema with
   `UserName`/`Priority` facets, `Attachment`/`Body` composition, `chat_message` kind).
2. Validate: `SchemaValidator.Validate(text)` → `SchemaValidationResult` — `.Document` on
   success, else `.Errors` (ALL construct-located errors in one pass).
3. Lower + register (both return result unions — success value or structured error, never both):
   `var reg = new SchemaLangRegistry();            // seeded from WireRegistry + SchemaRegistry`
   `var art = Lowering.Lower(doc, reg).Artifacts;  // canonical CDDL + functor registrations (0x13+), or .Error`
   `var rec = reg.Register(doc, art, CompatMode.Full).Records;  // all-or-nothing, or .Error (collision)`
4. Inspect: the new `RegistryRecord` holds qmedit-family source, canonical CDDL, XSD source
   verbatim, sha256 hashes — side by side with the seeded 041 `crdt_message` entry.
5. Validate an instance:
   `InstanceValidator.Validate(reg, "chat_message", instance)` → `Pass` / located `Fail`;
   an unregistered functor throws `NoSchemaRegisteredError` (loud-fail law).
6. Lift it back: `Lifter.Lift(reg, "chat_message")` → faithful rendering, `Full` fidelity.
7. Evolve: author v2, `CompatChecker.Check(v1, v2, CompatMode.Full)` → verdict naming any
   breaking construct; register via `reg.RegisterVersion(v2, art2)` — an incompatible verdict
   comes back as `.RequiresOverride`, satisfied only by
   `reg.RegisterVersionWithOverride(v2, art2, new OverrideRecord(verdict, who, why))`; a type
   with no declared mode refuses with `NoCompatModeDeclaredError` (clarification 3).

## Consuming validation from other code

Map your decoded message to the neutral `InstanceValue` tree (data-model §4). The reference
adapter `GlpRuntime.CrdtMsg.Message → InstanceValue` lives in
`csharp/glp_schema_lang.tests/` (corpus reuse for SC-001/SC-003); production consumers write the
same mapping for their model — `glp_schema_lang` itself never references `glp_crdtmsg`.

## Key invariants to keep in mind

- Registration is all-or-nothing per schema document; collisions never overwrite (US1 AS-3).
- Lowering is deterministic — golden CDDL files under `csharp/glp_schema_lang.tests/golden/`.
- Evolution refuses without a declared compat mode (clarification 3) — declare one at first
  registration.
- The E9 static tables are read-only substrate; 043 writes only its overlay.
