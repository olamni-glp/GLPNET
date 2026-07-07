# Contract: Lowering (SchemaDocument → CDDL + functor registrations)

Normative mapping for FR-003/FR-004/FR-005 (research R3/R5). API:
`Lowering.Lower(SchemaDocument) → LoweringArtifactSet | LoweringError`
`SchemaLangRegistry.Register(SchemaDocument, LoweringArtifactSet, one CompatMode per document — stamped on every registration it produces) → RegistryRecord[] | LoweringError`

## Construct mapping (canonical CDDL emitter)

| DSL construct | CDDL |
|---|---|
| `message f { sequence {…} }` | `f = { fields… }` (rule name = functor with `_`→`-`, e.g. `my_kind` → `my-kind`) |
| `type T { sequence {…} }` | `t = { fields… }` (rule name lowercased, `_`→`-`) |
| `type T { choice {…} }` | `t = branch1 // branch2 // …` each branch a one-entry map `{ name: type }` |
| element `e: T` (1..1) | `e: t,` |
| `e: T occurs 0..1` / `e?` | `? e: t,` |
| `e: T occurs a..b` | `e: [a*b t],` |
| `e: T occurs a..*` | `e: [a* t],` |
| `e: [T]` | `e: [* t],` |
| `int` / `str` / `bytes` / `bool` | `int` (`uint` when `min == 0` and no `max` — any other lone `min` widens and is unlowerable, FR-007) / `tstr` / `bstr` / `bool` |
| `min a` + `max b` | base becomes `a..b` range type |
| `minLength a` / `maxLength b` | `.size (a..b)` control (defaulting an absent bound to `0` / no upper) |
| `pattern "p"` | `.regexp "p"` control (`\` and `"` in the pattern text are escaped as `\\` / `\"` in the emitted string literal — the lift parser unescapes symmetrically; text without them emits byte-identically) |
| `enum(s1, s2, …)` on str (ident members) | `&( s1: 0, s2: 1, … )` (indices = declaration order — matches shipped `crdt-model`) |
| `enum(i1, i2, …)` on int | `i1 / i2 / …` |
| simple type `T` with facets | one named rule `t = <faceted base>` referenced by name |

Canonical form: rule order = message rules in declaration order, then named-type rules in
declaration order; 2-space indent; one field per line inside maps; trailing commas as in the
shipped `crdt_message` artifact. **Same document ⇒ byte-identical CDDL** (FR-005; golden test).

## Functor registrations

One `FunctorRegistration` per `message` declaration: `{functor, payload_type, compat_mode}`.
Payload types allocated deterministically: a functor already registered in seed ∪ overlay
**reuses** its registered byte (a kind keeps its payload-type byte across versions,
compat-evolution.md) — so in a version document only the genuinely new functors consume free
bytes, and a pure version bump allocates none; a new functor takes the lowest free byte ≥ 0x13
(`MessagingBase + 1`) in seed ∪ overlay, in message declaration order (R3). The declared
`CompatMode` is mandatory at registration (clarification 3 — no default is ever assumed).

## Registration laws

0. **Validated documents only** (FR-002/FR-014): every registry entry point that accepts a
   document (`Register`, `CheckVersion`, `RegisterVersion`, `RegisterVersionWithOverride`)
   re-validates it; an invalid document refuses with the full schema-error list and writes
   nothing — no unvalidated document ever reaches the overlay, and the error attribution names
   schema validation, never a broken registry invariant.
1. **All-or-nothing**: any collision or unlowerable construct registers NOTHING (spec edge case).
2. **Collision** (US1 AS-3): functor, payload-type, or schema name already in seed ∪ overlay
   ⇒ `LoweringError(collision, functor/byte/name, existing-entry identity)`. Never overwrite —
   even a byte-identical re-registration refuses. The schema-name clause applies to FIRST
   registration only — `RegisterVersion` legitimately re-uses the schema name to extend its
   version chains.
3. **Stored forms** (FR-004): each `RegistryRecord` stores `{qmedit, cddl, xsd_source, sha256
   hashes}` so all representations are retrievable together, side by side with 041-authored
   entries. For 043-authored entries the 043 document text IS the qmedit-family authoring form:
   one verbatim text is stored and exposed under both the `QmeditDsl` and `XsdSource` retrieval
   keys (analyze A2 — no lossy facet-less plain-qmedit rendering is synthesized). For seeded
   041 entries the two keys differ (`QmeditDsl` = the 041 form, `XsdSource` = null until lifted
   and re-registered through this layer).
4. **Unlowerable** (edge case): a construct combination the CDDL/functor layer cannot carry ⇒
   `LoweringError(unlowerable, [constructs…])` listing every offending construct.
5. E9 substrate untouched: `WireRegistry` / `SchemaRegistry` static tables are never modified
   (FR-012); registration writes only the overlay.
