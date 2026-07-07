# Contract: Lowering (SchemaDocument → CDDL + functor registrations)

Normative mapping for FR-003/FR-004/FR-005 (research R3/R5). API:
`Lowering.Lower(SchemaDocument) → LoweringArtifactSet | LoweringError`
`SchemaLangRegistry.Register(SchemaDocument, LoweringArtifactSet, CompatMode per message) → RegistryRecord[] | LoweringError`

## Construct mapping (canonical CDDL emitter)

| DSL construct | CDDL |
|---|---|
| `message f { sequence {…} }` | `f-message = { fields… }` (rule name = functor with `_`→`-`) |
| `type T { sequence {…} }` | `t = { fields… }` (rule name lowercased, `_`→`-`) |
| `type T { choice {…} }` | `t = branch1 // branch2 // …` each branch a one-entry map `{ name: type }` |
| element `e: T` (1..1) | `e: t,` |
| `e: T occurs 0..1` / `e?` | `? e: t,` |
| `e: T occurs a..b` | `e: [a*b t],` |
| `e: T occurs a..*` | `e: [a* t],` |
| `e: [T]` | `e: [* t],` |
| `int` / `str` / `bytes` / `bool` / `symbol` | `int` (`uint` when `min ≥ 0`) / `tstr` / `bstr` / `bool` / `tstr` |
| `min a` + `max b` | base becomes `a..b` range type |
| `minLength a` / `maxLength b` | `.size (a..b)` control (defaulting an absent bound to `0` / no upper) |
| `pattern "p"` | `.regexp "p"` control |
| `enum(s1, s2, …)` on symbol/str | `&( s1: 0, s2: 1, … )` (indices = declaration order — matches shipped `crdt-model`) |
| `enum(i1, i2, …)` on int | `i1 / i2 / …` |
| simple type `T` with facets | one named rule `t = <faceted base>` referenced by name |

Canonical form: rule order = message rules in declaration order, then named-type rules in
declaration order; 2-space indent; one field per line inside maps; trailing commas as in the
shipped `crdt_message` artifact. **Same document ⇒ byte-identical CDDL** (FR-005; golden test).

## Functor registrations

One `FunctorRegistration` per `message` declaration: `{functor, payload_type, compat_mode}`.
Payload types allocated deterministically: lowest free byte ≥ 0x13 (`MessagingBase + 1`) in
seed ∪ overlay, in message declaration order (R3). The declared `CompatMode` is mandatory at
registration (clarification 3 — no default is ever assumed).

## Registration laws

1. **All-or-nothing**: any collision or unlowerable construct registers NOTHING (spec edge case).
2. **Collision** (US1 AS-3): functor or payload-type already in seed ∪ overlay with a different
   shape ⇒ `LoweringError(collision, functor/byte, existing-entry identity)`. Never overwrite.
3. **Stored forms** (FR-004): each `RegistryRecord` stores `{qmedit (the 043 source is the
   qmedit-family form), cddl, xsd_source = the verbatim document text, sha256 hashes}` so all
   representations are retrievable together, side by side with 041-authored entries.
4. **Unlowerable** (edge case): a construct combination the CDDL/functor layer cannot carry ⇒
   `LoweringError(unlowerable, [constructs…])` listing every offending construct.
5. E9 substrate untouched: `WireRegistry` / `SchemaRegistry` static tables are never modified
   (FR-012); registration writes only the overlay.
