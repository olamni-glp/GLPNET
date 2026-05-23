---
path: lib/bytecode/opcodes_v2.dart
cycle_group_id: 20
scc_siblings: []
generated_at: 2026-05-21T14:25:51Z
source_sha256: c8549ccea9fbe836a1804e62b0164ac312889f3602144e9403938f9aaca206d6
schema_version: 1
---

# Conversion Plan: lib/bytecode/opcodes_v2.dart

## 1. Source Analysis

The file `lib/bytecode/opcodes_v2.dart` (156 LOC, no `_`-prefixed declarations,
no imports) defines the v2 unified instruction-set IR used by the GLP bytecode
runtime. Direct inspection of the source establishes:

- **File-level**: one `library;` directive (unnamed library marker — no name body,
  no `library foo;` form). No `import`, `export`, `part`, or `part of`. No
  top-level functions, variables, typedefs, or extensions. Three banner-comment
  separators (`// ====…====` blocks) demarcate HEAD PHASE, STRUCTURE TRAVERSAL,
  BODY PHASE, and GUARD PHASE sections — pure decoration. The trailing banner
  contains a normative note: "V1 migration functions removed - codegen now emits
  V2 directly", confirming opcodes_v2.dart is the live v2 IR (no v1↔v2 bridge
  remains in this file).
- **Marker base**: one `abstract class OpV2 {}` — empty body, no members, no
  `sealed`/`base`/`final` class modifier, no `extends`/`implements`/`with`. Used
  exclusively via `implements OpV2` by every v2 opcode class. Doc comment
  ("v2 instructions implement this to distinguish them from v1 Op") makes the
  v1/v2 type-disjointness normative.
- **Opcode IR nodes** (eight reference classes, all `implements OpV2`,
  no inheritance, no overridden `==`/`hashCode`):
  1. `HeadVariable(int varIndex, {required bool isReader})` — HEAD-phase
     unified writer/reader match, mnemonic `'head_reader'` / `'head_writer'`.
  2. `GetVariable(int varIndex, int argSlot, {required bool isReader})` —
     HEAD-phase argument-register first-occurrence load, mnemonic
     `'get_reader_variable'` / `'get_writer_variable'`.
  3. `GetValue(int varIndex, int argSlot, {required bool isReader})` —
     HEAD-phase argument-register subsequent-occurrence unify, mnemonic
     `'get_reader_value'` / `'get_writer_value'`.
  4. `UnifyVariable(int varIndex, {required bool isReader})` —
     structure-traversal match at current `S` cursor, mnemonic
     `'unify_reader'` / `'unify_writer'`.
  5. `PutVariable(int varIndex, int argSlot, {required bool isReader})` —
     BODY-phase argument-register store, mnemonic `'put_reader'` /
     `'put_writer'`.
  6. `SetVariable(int varIndex, {required bool isReader})` — BODY-phase
     WRITE-mode structure subterm build, mnemonic `'set_reader'` /
     `'set_writer'`.
  7. `Unknown(int varIndex)` — GUARD-phase unbound-test, fixed mnemonic
     `'unknown'`, **no `isReader` field** (sole arity-1 shape with no mode).
- **Common shape**: every class has all-`final` fields (compile-time immutable),
  a single positional+named-required constructor of the form
  `Ctor(this.fieldA, ..., {required this.isReader})` (Unknown omits the named
  block), one `String get mnemonic => ...;` getter (constant string on Unknown,
  ternary on `isReader` everywhere else), and one
  `@override String toString() => '<interpolation>';`. ToString shape varies
  only with arity: `'$mnemonic($varIndex)'` (arity-1) vs
  `'$mnemonic(X$varIndex, A$argSlot)'` (arity-2). The `X`/`A` register-prefix
  literals are debug-output conventions and must be preserved verbatim.
- **What is ABSENT** (and therefore not asserted in this plan): no Stream /
  Future / async / await, no isolates, no generics, no `late` / `mixin` /
  `extension`, no `sealed` class modifiers, no `enum`, no opcode integer codes
  (opcode identity is the class type itself, not an int — so the "enum vs
  const int codes" nuance does NOT apply here), no `const` constructors or
  collections, no bitwise / shift / arithmetic / overflow path, no nullable
  types (`?`), no equality / hashCode overrides, no inheritance among the
  opcode classes themselves.
- **Caller surface** (from tombstone, confirms reference-identity is
  load-bearing): `lib/bytecode/asm.dart`, `lib/bytecode/runner.dart`,
  `lib/compiler/codegen.dart` — assembler builds instances, runner pattern-
  switches on instance type, codegen emits instances into instruction lists.
  All three uses require these IR nodes to be reference types with stable
  identity.

## 2. Dart → C#/.NET Conversion Plan

This section mirrors the ratified per-construct decisions in
`.codeconv/conversion-specs/lib/bytecode/opcodes_v2.dart.md` verbatim;
each item is keyed to its convspec `construct_key`.

### 2.1 `library;` directive (construct_key: dart.library_directive.named_library)

**Decision**: Elide — emit NOTHING to C#. Do not translate to `namespace`,
`using`, attribute, or comment marker. Mirrors convspec
`rf-dart-library-directive-elided`. The C# namespace for the produced
`opcodes_v2.cs` is chosen at the codegen / project-assembly level (NOT by any
token in this Dart file). No leading-underscore declarations exist in this file,
so eliding the `library;` directive changes zero visibility semantics.

### 2.2 `abstract class OpV2 {}` (construct_key: dart.abstract_class.empty_marker_base_non_sealed_implemented)

**Decision**: Emit as an empty C# marker `interface IOpV2`. Each v2 opcode
class implements this interface. Mirrors convspec
`rf-dart-abstract-marker-to-csharp-interface`. CRITICAL constraints (load-bearing):

- Do NOT mark `IOpV2` (or any opcode class) `sealed`, `abstract`, or use
  exhaustive pattern-matching attributes. Dart `abstract class OpV2` is NOT
  declared `sealed`, so the source provides no exhaustiveness guarantee; the
  v2 interpreter must already have a default / fallback dispatch path, and
  introducing C# exhaustiveness would manufacture a guarantee the source never
  had (and could mask an unhandled-opcode bug).
- `IOpV2` MUST be disjoint from the v1 `IOp` interface (no shared base, no
  conversion). The doc comment makes the v1/v2 separation normative.
- Do NOT emit as a C# `abstract class` base — Dart `implements` here is
  implicit-interface conformance with zero inherited state, and an
  `abstract class` would consume the C# single-base-class slot.

### 2.3 Opcode IR node classes (construct_key: dart.data_class.final_fields_positional_and_named_required_ctor)

**Decision**: Each Dart class becomes a C# reference `class` (NOT `struct`,
NOT `record struct`) implementing `IOpV2`. Mirrors convspec
`rf-dart-required-named-param-to-csharp-required-arg`. Per-class member layout:

| Dart class       | Arity | C# class shape                                                                                  |
|------------------|-------|--------------------------------------------------------------------------------------------------|
| `HeadVariable`   | 2     | `class HeadVariable : IOpV2 { public long VarIndex {get;} public bool IsReader {get;} ctor(long, bool) }` |
| `GetVariable`    | 3     | `class GetVariable : IOpV2 { public long VarIndex {get;} public long ArgSlot {get;} public bool IsReader {get;} ctor(long, long, bool) }` |
| `GetValue`       | 3     | `class GetValue : IOpV2 { public long VarIndex {get;} public long ArgSlot {get;} public bool IsReader {get;} ctor(long, long, bool) }` |
| `UnifyVariable`  | 2     | `class UnifyVariable : IOpV2 { public long VarIndex {get;} public bool IsReader {get;} ctor(long, bool) }` |
| `PutVariable`    | 3     | `class PutVariable : IOpV2 { public long VarIndex {get;} public long ArgSlot {get;} public bool IsReader {get;} ctor(long, long, bool) }` |
| `SetVariable`    | 2     | `class SetVariable : IOpV2 { public long VarIndex {get;} public bool IsReader {get;} ctor(long, bool) }` |
| `Unknown`        | 1     | `class Unknown : IOpV2 { public long VarIndex {get;} ctor(long) }` (NO `IsReader`)               |

Reference-type constraint is load-bearing: the v2 interpreter, assembler, and
codegen all hold these instances in instruction lists, pattern-switch on
runtime type, and rely on reference identity (no overridden `==`). A
`record struct` would change equality to structural value-equality and
introduce copy semantics on every queue/store — forbidden.

### 2.4 `final` field → get-only auto-property (subsumed by 2.3)

**Decision**: Every Dart `final` field maps to a C# get-only auto-property,
NOT a writable field, NOT a `readonly` field, NOT an `init`-settable
property. Compile-time immutability is preserved; the constructor is the only
write site. Mirrors convspec construct decision under
`dart.data_class.final_fields_positional_and_named_required_ctor`.

### 2.5 `{required this.isReader}` (construct_key: dart.data_class.final_fields_positional_and_named_required_ctor, nuance: required-named)

**Decision**: Map to a regular C# constructor parameter `bool isReader`
(positional in the ctor signature, no default value). C# named-argument
call syntax (`new HeadVariable(varIndex, isReader: true)`) preserves the
exact Dart call shape at use sites. Mirrors convspec
`rf-dart-required-named-param-to-csharp-required-arg`. EXPLICITLY FORBIDDEN:
defaulted optional parameter (`bool isReader = false`) — would silently relax
mandatoriness (semantic drift, FR-013 territory). C# 11 `required` member
modifier is NOT used here because the field is set via a constructor parameter,
not via an initialiser.

### 2.6 `final bool isReader;` field type (construct_key: dart.bool.field)

**Decision**: Dart `bool` ⇔ C# `bool` (`System.Boolean`). Identical
two-valued true/false semantics; non-nullable on both sides (no `?` in the
Dart source). Mirrors convspec `rf-dart-bool-to-csharp-bool`. No boxing
concern (stored as a property on a reference class). Boolean operators
(`?:` used in mnemonic ternaries) behave identically.

### 2.7 `final int varIndex; final int argSlot;` field type (construct_key: dart.int.fixed_width_index_and_arity_field)

**Decision**: Dart `int` ⇒ C# `long` (`System.Int64`). NOT C# `int`/`Int32`,
NOT C# `uint`. Mirrors convspec `rf-dart-int-to-csharp-long-width`. Rationale
(verbatim from convspec): Dart native `int` is 64-bit signed; C# `int` is only
32-bit (would silently narrow); C# `uint` would change signedness AND overflow
behaviour. Codegen MAY down-map an individual provably-bounded field to `int`
only with a recorded per-field justification; the default in this plan is
`long`. This file performs no arithmetic / shift / bitwise / overflow, so the
checked/unchecked context and shift-sign hazards do not arise in
`opcodes_v2.cs` (they belong to the v2 interpreter, register allocator, and
codegen — not this IR-definition file).

### 2.8 `String get mnemonic => ...;` (construct_key: dart.getter_expression_body.string_ternary)

**Decision**: Each Dart `String get mnemonic => expr;` maps to a C# read-only
expression-bodied property `public string Mnemonic => expr;`. NOT a method —
mapping to `Mnemonic()` would change call-site syntax. Mirrors convspec
`rf-dart-getter-to-csharp-property`. Body translation:

- Constant case (`Unknown`): `public string Mnemonic => "unknown";`.
- Ternary case (all others): `public string Mnemonic => IsReader ? "<reader>"
  : "<writer>";` with the exact Dart literals preserved verbatim
  (`head_reader`/`head_writer`, `get_reader_variable`/`get_writer_variable`,
  `get_reader_value`/`get_writer_value`, `unify_reader`/`unify_writer`,
  `put_reader`/`put_writer`, `set_reader`/`set_writer`). Byte-identical to
  Dart output — load-bearing for any debug-log diff against existing v1/v2
  baselines.

### 2.9 `@override String toString()` (construct_key: dart.tostring_override.string_interpolation)

**Decision**: Each Dart `@override String toString() => '...';` maps to
`public override string ToString() => $"...";` overriding
`System.Object.ToString`. NOT a C# extension method (cannot override a virtual).
Mirrors convspec `rf-dart-tostring-interp-to-csharp-tostring-interp`.
Interpolation translation (literal punctuation, `X`/`A` register prefixes,
commas, spaces, parens — ALL preserved verbatim):

- Arity-1 (`HeadVariable`, `UnifyVariable`, `SetVariable`, `Unknown`):
  `=> $"{Mnemonic}({VarIndex})";`.
- Arity-2 (`GetVariable`, `GetValue`, `PutVariable`):
  `=> $"{Mnemonic}(X{VarIndex}, A{ArgSlot})";`.

Both Dart `int.toString()` and C# interpolation of `long` are
culture-invariant by default — debug output is byte-stable across both
languages without any explicit `:N` / `IFormatProvider` argument.

### 2.10 Banner comments + `///` doc comments (trivial)

**Decision**: `///` triple-slash Dart doc comments map mechanically to C#
XML-doc `///` comments (preserved on the corresponding C# type / member).
`// ====…====` decorative banner separators map to plain C# `//` comments and
preserve the HEAD / STRUCTURE / BODY / GUARD section structure. The
"V1 migration functions removed" trailing banner is a historical note; carry
it through as a `//` comment. These are non-construct elements — no separate
construct decision, no research.

### 2.11 `@override` annotation (subsumed by 2.9)

**Decision**: Dart `@override` on `toString()` is subsumed by the C# `override`
modifier on `ToString()`. No separate emission.

## 3. Decomposed Task Units

Each task unit is the smallest independently-actionable C# emission. Order
follows source-file order; arity-2/3 classes are batched only where their
emission templates are identical except for the mnemonic strings (T4–T9 share
one template family but emit distinct types — they remain separate units to
preserve per-class verification).

- **T1 — Elide `library;` directive.** Done-when: the produced `opcodes_v2.cs`
  contains no token traceable to Dart `library;` (no `namespace` decision, no
  `using`, no marker comment derived from it).
- **T2 — Emit `IOpV2` empty marker interface.** Done-when: `opcodes_v2.cs`
  contains a single `public interface IOpV2 { }` with no members, no
  modifiers (`sealed`/`partial`/etc.), and no inheritance from `IOp`.
- **T3 — Emit `HeadVariable : IOpV2` class.** Done-when: class has
  `long VarIndex` + `bool IsReader` get-only properties, a positional ctor
  `HeadVariable(long varIndex, bool isReader)`, `string Mnemonic =>
  IsReader ? "head_reader" : "head_writer";`, and `override string ToString
  () => $"{Mnemonic}({VarIndex})";`.
- **T4 — Emit `GetVariable : IOpV2` class.** Done-when: class has
  `long VarIndex`, `long ArgSlot`, `bool IsReader` get-only properties, a
  positional ctor `(long varIndex, long argSlot, bool isReader)`,
  `string Mnemonic => IsReader ? "get_reader_variable" :
  "get_writer_variable";`, and `override string ToString() =>
  $"{Mnemonic}(X{VarIndex}, A{ArgSlot})";`.
- **T5 — Emit `GetValue : IOpV2` class.** Done-when: identical shape to T4 with
  mnemonic strings `"get_reader_value"` / `"get_writer_value"`.
- **T6 — Emit `UnifyVariable : IOpV2` class.** Done-when: identical shape to T3
  with mnemonic strings `"unify_reader"` / `"unify_writer"`.
- **T7 — Emit `PutVariable : IOpV2` class.** Done-when: identical shape to T4
  with mnemonic strings `"put_reader"` / `"put_writer"`.
- **T8 — Emit `SetVariable : IOpV2` class.** Done-when: identical shape to T3
  with mnemonic strings `"set_reader"` / `"set_writer"`.
- **T9 — Emit `Unknown : IOpV2` class.** Done-when: class has `long VarIndex`
  get-only property only (NO `IsReader`), a positional ctor `(long varIndex)`,
  `string Mnemonic => "unknown";` (constant — not a ternary), and
  `override string ToString() => $"unknown(X{VarIndex})";` (NOTE: Dart
  source uses `'unknown(X$varIndex)'`, i.e. the `unknown` literal is hard-coded
  alongside `Mnemonic` for this single class — preserve that verbatim).
- **T10 — Carry through XML-doc and banner comments.** Done-when: each Dart
  `///` doc block is reproduced as a C# `///` doc block on the corresponding
  C# type/member, and the four section banners (HEAD PHASE / STRUCTURE
  TRAVERSAL / BODY PHASE / GUARD PHASE) plus the trailing "V1 migration
  functions removed" note are reproduced as `//` comments.

## 4. Research Findings

None required. Every construct in this file is fully decided by the ratified
convspec at `.codeconv/conversion-specs/lib/bytecode/opcodes_v2.dart.md`,
which itself cites authoritative Dart (dart.dev, api.dart.dev) and .NET
(learn.microsoft.com) documentation per research-finding ID
(`rf-dart-library-directive-elided`, `rf-dart-abstract-marker-to-csharp-interface`,
`rf-dart-required-named-param-to-csharp-required-arg`,
`rf-dart-bool-to-csharp-bool`, `rf-dart-int-to-csharp-long-width`,
`rf-dart-getter-to-csharp-property`,
`rf-dart-tostring-interp-to-csharp-tostring-interp`). No idiom KB lookup is
required (convspec records `idiom_id: null` for every construct — each
mapping is direct from authoritative docs, not via a reusable idiom). Web
research is forbidden in this planning stage and no escalation for `research
unavailable` is needed.

## 5. Consistency Pass

Cross-check across §1 (source analysis), §2 (per-construct decisions), §3
(task units), §4 (research), the ratified convspec, and the 012/015 contracts:

- **§1 ↔ §2 construct coverage.** Eight Dart top-level classes in §1
  (`OpV2`, `HeadVariable`, `GetVariable`, `GetValue`, `UnifyVariable`,
  `PutVariable`, `SetVariable`, `Unknown`) each map to a §2 decision
  (2.2 for the marker + 2.3 for the seven IR nodes, with field-level
  decisions 2.4–2.7 and member-level decisions 2.8–2.9 covering every member).
  No Dart construct in §1 lacks a §2 decision — consistent.
- **§2 ↔ §3 task coverage.** Each §2 decision has at least one §3 task: 2.1→T1,
  2.2→T2, 2.3+2.4+2.5+2.6+2.7+2.8+2.9 are realised across T3–T9 (one task per
  emitted class), 2.10→T10, 2.11 subsumed into T3–T9's ToString lines. No §2
  decision is orphaned — consistent.
- **§2 ↔ convspec construct keys.** Every §2 subsection cites its convspec
  `construct_key` verbatim; the seven non-trivial construct keys in the
  convspec map to §2.1, §2.2, §2.3 (covers fields + ctor + nuance), §2.6,
  §2.7, §2.8, §2.9 (consistent — fixed — derived from the convspec
  `constructs` list and per-construct `target_decision` text).
- **Reference-vs-value-type consistency.** §1 (caller surface — assembler /
  runner / codegen hold instances by reference), §2.3 (reference class, NOT
  struct), and the convspec nuance ("must remain reference classes
  (identity-bearing IR nodes the v2 interpreter holds in instruction lists /
  continuations); a record struct would change equality to structural
  value-equality and introduce copy semantics on every queue/store") are
  mutually reinforcing — consistent.
- **Integer width consistency.** §2.7 mandates `long` as the default mapping
  for every Dart `int` field; convspec
  `rf-dart-int-to-csharp-long-width` records the same default with an
  explicit codegen escape valve (per-field down-map to `int` only with
  recorded justification). No field in this file is asserted as
  provably-bounded in the convspec, so the plan default is `long` for all
  five `int` fields (varIndex on every class, argSlot on the three arity-3
  classes) — consistent.
- **Exhaustiveness / sealed.** §1 (Dart source has no `sealed`), §2.2
  ("Do NOT mark `IOpV2` (or any opcode class) `sealed`"), and the convspec
  nuance ("OpV2 is not sealed => no such guarantee in the source.
  ... Do NOT manufacture a closed/exhaustive hierarchy") are
  mutually consistent — consistent.
- **v1/v2 separation.** §1 (doc-comment normative), §2.2 ("disjoint from
  the v1 `IOp` interface"), and convspec
  ("Disjoint from `IOp` (per the existing opcodes.dart convspec)") are
  consistent. The plan does not require reading `opcodes.dart`'s convspec —
  the local convspec already records the disjointness decision.
- **Required-named parameter handling.** §2.5 maps Dart `{required this.isReader}`
  to a regular C# ctor parameter with no default; convspec
  `rf-dart-required-named-param-to-csharp-required-arg` records the same
  decision and explicitly forbids defaulted optional parameters. Consistent.
- **§4 ↔ §6.** §4 records "no research required"; §6 records no escalations.
  This is consistent because the convspec already resolved every construct
  from authoritative docs (convspec `escalations: []`), so the plan inherits
  zero open questions. Consistent.
- **scc_siblings empty.** Front-matter `scc_siblings: []`; the task brief
  forbids `## 7.` for singleton cycle groups; this artefact has no `## 7.`.
  Consistent.
- **Tombstone metadata.** The tombstone records `cycle_group_id: 21`; the
  task brief instructs `cycle_group_id: 20`. The plan front-matter follows
  the task brief verbatim (`cycle_group_id: 20`) — fixed — derived from the
  explicit task-brief instruction taking precedence over tombstone
  metadata for the plan artefact. No escalation required.

No gaps remain after this pass.

## 6. Escalations

None.
