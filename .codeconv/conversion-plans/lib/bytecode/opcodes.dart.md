---
path: lib/bytecode/opcodes.dart
cycle_group_id: 19
scc_siblings: []
generated_at: 2026-05-21T14:25:34Z
source_sha256: 41a0a75b2c8fbb8b1009c868fc85301b0d4d3233350840485ca5c35d2cc9dabe
schema_version: 1
---

# Conversion Plan: lib/bytecode/opcodes.dart

## 1. Source Analysis

`lib/bytecode/opcodes.dart` defines the GLP bytecode IR (Op kinds) consumed by
the runner / asm / codegen / linter / tests (per tombstone callers: `asm.dart`,
`runner.dart`, `compiler/codegen.dart`, `lint/linter.dart`, three test files).
The file has zero imports; it is a self-contained leaf in the depgraph (its
`dependencies: []` confirms it sits at topo level 0).

Construct inventory (grounded in the 384-line source):

- 1 non-function typedef: `typedef LabelName = String;` (line 1).
- 1 empty marker `abstract class Op {}` (line 4) — NOT `sealed`. It is the
  contract that every opcode class declares via `implements Op` (never
  `extends`). It has no members and no shared state.
- ~50 opcode classes implementing `Op`:
  - Empty marker classes (no fields, no ctor body): `ClauseTry`, `GuardFail`,
    `Commit`, `TryNextClause`, `NoMoreClauses`, `SuspendEnd`, `Proceed`,
    `Otherwise`, `Deallocate`, `Nop`, `Halt`.
  - Single-field IR nodes with a positional initialising-formal constructor:
    `Label(LabelName name)`, `ClauseNext(LabelName label)`,
    `UnionSiAndGoto(LabelName label)` (@deprecated),
    `ResetAndGoto(LabelName label)` (@deprecated),
    `PutNil(int argSlot)`, `PutList(int argSlot)`, `HeadNil(int argSlot)`,
    `HeadList(int argSlot)`, `UnifyConstant(Object? value)`,
    `HeadBindWriter(int writerId)`, `GuardNeedReader(int readerId)`,
    `HeadBindWriterArg(int slot)`, `GuardNeedReaderArg(int slot)`,
    `TailStep(LabelName label)`, `Allocate(int slots)`,
    `Push(int regIndex)`, `Pop(int regIndex)`, `SetConstant(Object? value)`.
  - Two-field IR nodes: `BodySetConst(int writerId, Object? value)`,
    `PutConstant(Object? value, int argSlot)`,
    `PutBoundConst(Object? value, int argSlot)`,
    `PutBoundNil(int argSlot)` (single — listed above),
    `HeadConstant(Object? value, int argSlot)`,
    `GetVariable(int varIndex, int argSlot)`,
    `GetValue(int varIndex, int argSlot)`,
    `RequireWriterArg(int slot, LabelName failLabel)`,
    `RequireReaderArg(int slot, LabelName failLabel)`,
    `BodySetConstArg(int slot, Object? value)`,
    `UnifyStructure(String functor, int arity)`,
    `Spawn(LabelName procedureLabel, int arity)`,
    `Requeue(LabelName procedureLabel, int arity)`.
  - Three-field IR nodes: `PutStructure(String functor, int arity, int argSlot)`,
    `HeadStructure(String functor, int arity, int argSlot)`,
    `BodySetStructConstArgs(int writerId, String functor, List<Object?> constArgs)`,
    `Distribute(int importIndex, String functor, int arity)`,
    `Transmit(int moduleVarIndex, String functor, int arity)`.
  - Named optional initialising-formal default: `UnifyVoid({this.count = 1})`.
  - Named optional initialising-formal default of bool:
    `Guard(this.procedureLabel, this.arity, {this.negated = false})`,
    `Ground(this.varIndex, {this.negated = false})`,
    `Known(this.varIndex, {this.negated = false})`,
    `NoReaders(this.varIndex, {this.negated = false})`,
    `GroundEqual(this.leftVarIndex, this.rightVarIndex, {this.negated = false})`.
- 2 `@deprecated` class annotations (`UnionSiAndGoto`, `ResetAndGoto`,
  lines 32 and 37) — preserved for backward-compat per source comment.
- 6 `@override String toString()` debug overrides using string interpolation:
  `Push` (`'Push(X$regIndex)'`), `Pop` (`'Pop(X$regIndex)'`),
  `UnifyStructure` (`'UnifyStructure($functor, $arity)'`),
  `GroundEqual` (ternary returning two interpolated forms),
  `Distribute` (`'Distribute([$importIndex] $functor/$arity)'`),
  `Transmit` (`'Transmit(X$moduleVarIndex, $functor/$arity)'`).
- ALL fields are `final` (write-once). No mutable state. No methods beyond
  the six `toString` overrides. No async/Future/Stream/isolate. No mixin /
  extension / sealed / late / generics-with-bounds. No bitwise/shift/
  arithmetic. Identity-bearing reference IR nodes (the runner stores them by
  reference).

The ratified convspec covers every construct above with zero escalations
(7 idioms, 0 open items). This plan mirrors those decisions verbatim.

## 2. Dart → C#/.NET Conversion Plan

Each non-trivial Dart construct mapped to its C#/.NET equivalent. Mirrors the
ratified convspec (no contradictions, no new design decisions).

| # | Dart construct | C#/.NET target | Source: convspec construct_key / RF |
|---|---|---|---|
| C1 | `typedef LabelName = String;` | File-scoped alias `using LabelName = System.String;` (NOT a wrapper struct — preserves transparent assignability across all LabelName-typed fields). | `dart.typedef.string_alias` / `rf-dart-typedef-string-to-csharp-using-alias` |
| C2 | `abstract class Op {}` (empty, non-sealed marker, used only via `implements Op`) | Empty marker `interface IOp { }` — plain (NOT `sealed`); MUST NOT manufacture exhaustiveness. Reference-only (consumers store/queue/switch instances by reference). | `dart.abstract_class.empty_marker_base_non_sealed_implemented` / `rf-dart-abstract-marker-to-csharp-interface` |
| C3 | Immutable IR data class pattern `class X implements Op { final T f; X(this.f); }` | C# reference `class X : IOp` with get-only auto-properties initialised in one positional constructor mirroring `this.field` binding order. NEVER record struct (would change equality + introduce copy semantics on store/queue). Empty marker opcodes (`ClauseTry`, `GuardFail`, `Commit`, `SuspendEnd`, `Proceed`, `Deallocate`, `Nop`, `Halt`, `TryNextClause`, `NoMoreClauses`, `Otherwise`) become parameterless `class : IOp {}`. | `dart.data_class.final_fields_positional_ctor` / `rf-dart-final-field-class-to-csharp-getonly-class` |
| C4 | Dart `int` fields (register indices, slots, arities, counts, 1-based import indices) | C# `long` (System.Int64) — type-faithful 64-bit mapping. NOT `int` (would silently narrow Dart's 64-bit native semantics). NOT `uint` (Dart `int` is signed). Per-field down-map to `int` is permitted only with a recorded justification (bounded range proof) — default is `long`. No arithmetic/bitwise/shift/overflow path exists in this file (pure storage + interpolation), so signed-shift / checked / overflow nuances are not exercised here and are deliberately not asserted. | `dart.int.fixed_width_index_and_arity_field` / `rf-dart-int-to-csharp-long-width` |
| C5 | `final Object? value;` and `final List<Object?> constArgs;` | C# `object?` (nullable top type, under enabled nullable context) and `List<object?>`. NOT `object` (NRT would assert non-null). Boxing of value-type payloads stored in `object?` is semantically transparent for storage/equality in this file (no arithmetic on the boxed value) and is recorded so consumers preserve boxed-equality rather than unbox prematurely. | `dart.nullable_object_field.Object_question` / `rf-dart-objectq-to-csharp-objectq` |
| C6 | Named optional initialising formal with constant default: `UnifyVoid({this.count = 1})`; `Guard(... , {this.negated = false})`; same for `Ground`, `Known`, `NoReaders`, `GroundEqual` | C# constructor with an optional parameter and same literal default — e.g. `UnifyVoid(long count = 1)`, `Guard(LabelName procedureLabel, long arity, bool negated = false)`. C# named-argument call syntax (`new Guard(label, arity, negated: true)`) preserves Dart's `Guard(label, arity, negated: true)` call shape. Defaults remain compile-time constants in both languages (no drift). | `dart.named_optional_param_with_default.this_field` / `rf-dart-named-default-param-to-csharp-optional-arg` |
| C7 | `@deprecated class UnionSiAndGoto …` and `@deprecated class ResetAndGoto …` | `[System.Obsolete]` on the corresponding C# class. Classes are PRESERVED (NOT deleted) — source comment mandates retention for backward compat with existing tests. Body still emitted via C3 (each carries `final LabelName label` + positional ctor). | `dart.deprecated_annotation_on_type` / `rf-dart-deprecated-to-csharp-obsolete` |
| C8 | `@override String toString() => '…$x…';` (Push, Pop, UnifyStructure, GroundEqual ternary, Distribute, Transmit) | `public override string ToString() => $"…{X}…";` overriding `System.Object.ToString`. Interpolation `$id` → `{Id}`, `${expr}` → `{expr}`. Literal punctuation (`X`, `/`, `[`, `]`, `(`, `)`, `,`, space) preserved verbatim so debug output is byte-identical. GroundEqual's `negated ? '~(...)' : '...'` becomes a C# conditional expression returning two interpolated strings. Classes without a toString override INHERIT default `object.ToString` — DO NOT synthesise one. | `dart.tostring_override.string_interpolation` / `rf-dart-tostring-interp-to-csharp-tostring-interp` |

Concrete target shape (mirrors the convspec `conversion_units` block; this is
the IR-unit list a downstream codegen consumes — no compilable C# here per
FR-023):

- File-scoped alias: `using LabelName = System.String;` (top of `opcodes.cs`).
- `interface IOp { }` (empty, non-sealed marker; no exhaustiveness).
- `class Label : IOp` — `LabelName Name`.
- Empty marker classes implementing `IOp`: `ClauseTry`, `GuardFail`, `Commit`,
  `SuspendEnd`, `Proceed`, `Deallocate`, `Nop`, `Halt`, `TryNextClause`,
  `NoMoreClauses`, `Otherwise`.
- `class ClauseNext : IOp` — `LabelName Label`.
- `[Obsolete] class UnionSiAndGoto : IOp` — `LabelName Label`.
- `[Obsolete] class ResetAndGoto : IOp` — `LabelName Label`.
- `class BodySetConst : IOp` — `long WriterId, object? Value`.
- `class BodySetStructConstArgs : IOp` — `long WriterId, string Functor,
  List<object?> ConstArgs`.
- `class PutConstant : IOp` — `object? Value, long ArgSlot`.
- `class PutStructure : IOp` — `string Functor, long Arity, long ArgSlot`.
- `class SetConstant : IOp` — `object? Value`.
- `class PutNil : IOp` / `class PutList : IOp` — `long ArgSlot`.
- `class PutBoundConst : IOp` — `object? Value, long ArgSlot`.
- `class PutBoundNil : IOp` — `long ArgSlot`.
- `class HeadConstant : IOp` — `object? Value, long ArgSlot`.
- `class HeadStructure : IOp` — `string Functor, long Arity, long ArgSlot`.
- `class UnifyConstant : IOp` — `object? Value`.
- `class HeadNil : IOp` / `class HeadList : IOp` — `long ArgSlot`.
- `class UnifyVoid : IOp` — `long Count` (ctor `long count = 1`).
- `class GetVariable : IOp` / `class GetValue : IOp` — `long VarIndex,
  long ArgSlot`.
- `class Push : IOp` / `class Pop : IOp` — `long RegIndex`, with `override
  string ToString() => $"Push(X{RegIndex})";` / same for Pop.
- `class UnifyStructure : IOp` — `string Functor, long Arity`, with
  `override string ToString() => $"UnifyStructure({Functor}, {Arity})";`.
- `class Guard : IOp` — `LabelName ProcedureLabel, long Arity, bool Negated
  = false`.
- `class Ground : IOp` / `class Known : IOp` / `class NoReaders : IOp` —
  `long VarIndex, bool Negated = false`.
- `class GroundEqual : IOp` — `long LeftVarIndex, long RightVarIndex,
  bool Negated = false`, with `override string ToString() => Negated ?
  $"~(X{LeftVarIndex} =?= X{RightVarIndex})" : $"X{LeftVarIndex} =?=
  X{RightVarIndex}";`.
- `class HeadBindWriter : IOp` — `long WriterId`.
- `class GuardNeedReader : IOp` — `long ReaderId`.
- `class RequireWriterArg : IOp` / `class RequireReaderArg : IOp` —
  `long Slot, LabelName FailLabel`.
- `class HeadBindWriterArg : IOp` / `class GuardNeedReaderArg : IOp` —
  `long Slot`.
- `class BodySetConstArg : IOp` — `long Slot, object? Value`.
- `class TailStep : IOp` — `LabelName Label`.
- `class Spawn : IOp` / `class Requeue : IOp` — `LabelName ProcedureLabel,
  long Arity`.
- `class Allocate : IOp` — `long Slots`.
- `class Distribute : IOp` — `long ImportIndex, string Functor, long Arity`,
  with `override string ToString() => $"Distribute([{ImportIndex}] {Functor}/
  {Arity})";`.
- `class Transmit : IOp` — `long ModuleVarIndex, string Functor, long Arity`,
  with `override string ToString() => $"Transmit(X{ModuleVarIndex}, {Functor}/
  {Arity})";`.

Working directory / target path: `lib/bytecode/opcodes.cs` (subtree-relative,
per tombstone `target_path`).

## 3. Decomposed Task Units

Each unit has a single one-line definition-of-done. Units are dependency-
ordered: T1 (alias) and T2 (marker interface) must precede T3+ (every opcode
class references both). The remaining T-units are independent and can be
emitted in any order within the same target file.

- **T1**: Emit file-scoped `using LabelName = System.String;` at the top of
  `lib/bytecode/opcodes.cs`. DoD: alias declared exactly once at file scope;
  every later opcode class field typed `LabelName` resolves to `string`.
- **T2**: Emit `interface IOp { }` — plain, non-sealed, empty marker. DoD:
  interface compiles, has no members, and is NOT `sealed`; no exhaustiveness
  semantics introduced.
- **T3**: Emit the 11 empty marker opcode classes (`ClauseTry`, `GuardFail`,
  `Commit`, `SuspendEnd`, `Proceed`, `Deallocate`, `Nop`, `Halt`,
  `TryNextClause`, `NoMoreClauses`, `Otherwise`) as parameterless reference
  classes implementing `IOp`. DoD: each is `class X : IOp { }` with no fields,
  no constructor body, no methods.
- **T4**: Emit `class Label : IOp` with `LabelName Name { get; }` and a
  single positional constructor binding it. DoD: get-only property, no setter.
- **T5**: Emit `class ClauseNext : IOp` with `LabelName Label { get; }` +
  positional ctor. DoD: get-only property, no setter.
- **T6**: Emit `[Obsolete] class UnionSiAndGoto : IOp` and `[Obsolete] class
  ResetAndGoto : IOp` — each with `LabelName Label { get; }` + positional
  ctor. DoD: `[System.Obsolete]` attribute present; classes preserved (NOT
  deleted).
- **T7**: Emit the single-`object?`-payload opcode classes: `SetConstant`
  (`object? Value`), `UnifyConstant` (`object? Value`). DoD: nullable
  annotation present; one positional ctor binds the get-only property.
- **T8**: Emit the single-`long`-slot opcode classes: `PutNil`, `PutList`,
  `PutBoundNil`, `HeadNil`, `HeadList` (each `long ArgSlot`); `HeadBindWriter`
  (`long WriterId`); `GuardNeedReader` (`long ReaderId`);
  `HeadBindWriterArg`, `GuardNeedReaderArg` (each `long Slot`); `Allocate`
  (`long Slots`); `TailStep` (`LabelName Label`). DoD: each get-only property
  typed `long` (or `LabelName`); one positional ctor.
- **T9**: Emit two-field opcode classes without ToString: `BodySetConst`
  (`long WriterId, object? Value`), `PutConstant` (`object? Value, long
  ArgSlot`), `PutBoundConst` (`object? Value, long ArgSlot`), `HeadConstant`
  (`object? Value, long ArgSlot`), `GetVariable` (`long VarIndex, long
  ArgSlot`), `GetValue` (`long VarIndex, long ArgSlot`), `RequireWriterArg`
  (`long Slot, LabelName FailLabel`), `RequireReaderArg` (`long Slot,
  LabelName FailLabel`), `BodySetConstArg` (`long Slot, object? Value`),
  `Spawn` (`LabelName ProcedureLabel, long Arity`), `Requeue` (`LabelName
  ProcedureLabel, long Arity`). DoD: ctor parameter order matches Dart source
  order; each property get-only.
- **T10**: Emit three-field opcode classes without ToString: `PutStructure`
  (`string Functor, long Arity, long ArgSlot`), `HeadStructure` (`string
  Functor, long Arity, long ArgSlot`), `BodySetStructConstArgs` (`long
  WriterId, string Functor, List<object?> ConstArgs`). DoD: ctor parameter
  order matches Dart source order; `List<object?>` for the constArgs field.
- **T11**: Emit `class UnifyVoid : IOp` with `long Count { get; }` and
  constructor `UnifyVoid(long count = 1)`. DoD: optional parameter default
  literal `1`; named-argument call `new UnifyVoid(count: 5)` is valid C#.
- **T12**: Emit `class Guard : IOp` with `LabelName ProcedureLabel { get; },
  long Arity { get; }, bool Negated { get; }` and constructor `Guard(
  LabelName procedureLabel, long arity, bool negated = false)`. DoD: positional
  args mandatory; `negated` defaults to `false`.
- **T13**: Emit `Ground`, `Known`, `NoReaders` — each `class : IOp` with
  `long VarIndex { get; }, bool Negated { get; }` and ctor `(long varIndex,
  bool negated = false)`. DoD: defaults match Dart `false`.
- **T14**: Emit `class GroundEqual : IOp` with `long LeftVarIndex { get; },
  long RightVarIndex { get; }, bool Negated { get; }` + ctor `(long
  leftVarIndex, long rightVarIndex, bool negated = false)` AND override
  `string ToString() => Negated ? $"~(X{LeftVarIndex} =?= X{RightVarIndex})"
  : $"X{LeftVarIndex} =?= X{RightVarIndex}";`. DoD: byte-identical debug
  string vs Dart source for both branches.
- **T15**: Emit `class Push : IOp` with `long RegIndex` + ctor +
  `override string ToString() => $"Push(X{RegIndex})";`. Same for `class Pop`
  (`$"Pop(X{RegIndex})"`). DoD: both ToStrings byte-identical to Dart.
- **T16**: Emit `class UnifyStructure : IOp` with `string Functor, long
  Arity` + ctor + `override string ToString() => $"UnifyStructure({Functor},
  {Arity})";`. DoD: byte-identical to Dart.
- **T17**: Emit `class Distribute : IOp` with `long ImportIndex, string
  Functor, long Arity` + ctor + `override string ToString() => $"Distribute(
  [{ImportIndex}] {Functor}/{Arity})";`. DoD: byte-identical to Dart
  (including the literal `[`, `]`, `/` punctuation).
- **T18**: Emit `class Transmit : IOp` with `long ModuleVarIndex, string
  Functor, long Arity` + ctor + `override string ToString() => $"Transmit(
  X{ModuleVarIndex}, {Functor}/{Arity})";`. DoD: byte-identical to Dart
  (including the literal `X` prefix).

## 4. Research Findings

None required — every construct's mapping is verbatim-derivable from the
ratified per-construct decisions in `.codeconv/conversion-specs/lib/bytecode/
opcodes.dart.md` (7 research findings already cited there:
`rf-dart-typedef-string-to-csharp-using-alias`,
`rf-dart-abstract-marker-to-csharp-interface`,
`rf-dart-final-field-class-to-csharp-getonly-class`,
`rf-dart-int-to-csharp-long-width`,
`rf-dart-objectq-to-csharp-objectq`,
`rf-dart-named-default-param-to-csharp-optional-arg`,
`rf-dart-deprecated-to-csharp-obsolete`,
`rf-dart-tostring-interp-to-csharp-tostring-interp`), each grounded in
authoritative Dart (dart.dev) and .NET (learn.microsoft.com) documentation.
No new web/research needed; no `research unavailable` escalation required.

## 5. Consistency Pass

Cross-checks performed (each finding is either auto-fixed-with-citation or
escalated; per workflow only verbatim-derivable auto-fixes are permitted —
no new design decisions, no scope changes):

- **§2 vs convspec construct coverage** — every one of the 8 convspec
  construct_keys (typedef, abstract marker, data class, int, Object?, named
  default, deprecated, toString) appears in §2 as rows C1–C8 with the
  matching `rf-…` provenance. No gap.
- **§2 vs §3 (task-unit coverage)** — every conversion unit listed in
  `conversion_units` (alias + IOp + ~50 classes) is covered by exactly one
  of T1–T18. Mapping: T1=alias, T2=IOp, T3=11 empty markers, T4=Label,
  T5=ClauseNext, T6=2 @deprecated, T7=SetConstant/UnifyConstant,
  T8=single-field opcodes (incl. legacy HeadBindWriter/GuardNeedReader and
  Allocate/TailStep), T9=two-field opcodes, T10=three-field opcodes,
  T11=UnifyVoid, T12=Guard, T13=Ground/Known/NoReaders, T14=GroundEqual,
  T15=Push/Pop, T16=UnifyStructure, T17=Distribute, T18=Transmit. No
  unmapped opcode class.
- **Source line scan vs construct list** — re-walked the Dart file lines
  1–384 and matched every class definition into the T-unit map. Total
  opcode classes counted: 11 empty + 17 single-field + 11 two-field + 5
  three-field + 1 named-default (UnifyVoid) + 1 Guard + 3 (Ground/Known/
  NoReaders) + 1 GroundEqual + 2 ToString-only (Push, Pop) + 1 ToString
  (UnifyStructure) + 1 ToString (Distribute) + 1 ToString (Transmit) ≈ 50,
  matching the convspec's "~50 opcode classes" statement. No missed class.
- **Width policy uniformity (C4)** — every Dart `int` field in this file
  maps to C# `long` per the rf-int-to-long convspec decision; the plan
  applies this uniformly across T4–T18 without per-field exception. Fixed —
  derived from convspec `dart.int.fixed_width_index_and_arity_field`.
- **Nullable-policy uniformity (C5)** — every Dart `Object?` field maps to
  C# `object?`, every `List<Object?>` to `List<object?>`. No field is
  silently promoted to non-nullable. Fixed — derived from convspec
  `dart.nullable_object_field.Object_question`.
- **Marker-interface non-sealedness (C2)** — §3 T2 DoD explicitly forbids
  `sealed`/exhaustiveness, matching the convspec nuance. Fixed — derived
  from convspec `dart.abstract_class.empty_marker_base_non_sealed_implemented`.
- **Reference-vs-record-struct (C3)** — §3 T3–T18 DoDs require `class`
  (reference) targets, never record struct. Fixed — derived from convspec
  `dart.data_class.final_fields_positional_ctor` nuance.
- **Deprecated preservation (C7)** — §3 T6 DoD requires preserving both
  `[Obsolete]` classes (no deletion), matching the source comment ("Legacy
  instructions … kept for backward compatibility with existing tests").
  Fixed — derived from convspec `dart.deprecated_annotation_on_type`.
- **ToString byte-identity (C8)** — §3 T14/T15/T16/T17/T18 DoDs each
  mandate byte-identical debug output vs the Dart source string. No
  drift permitted. Fixed — derived from convspec
  `dart.tostring_override.string_interpolation`.
- **Singleton cycle group** — `scc_siblings: []`, so no §7. The tombstone
  `cycle_group_id` is 20, prompt-supplied `cycle_group_id` is 19; per
  workflow the front-matter MUST use the prompt-supplied value (19) — fixed,
  derived from the explicit prompt instruction.
- **Caller-contract sanity** — the five callers in the tombstone
  (`lib/bytecode/asm.dart`, `lib/bytecode/runner.dart`,
  `lib/compiler/codegen.dart`, `lib/lint/linter.dart`, 3 test files) consume
  `Op`-typed values and pattern-switch on concrete subtypes. The plan's
  reference-class + interface-marker shape preserves both reference identity
  (for instruction queues/stores in `runner.dart`) and runtime-type-switch
  ergonomics (`is X x` / pattern matching). No contract breakage. Fixed —
  derived from tombstone `callers` list + convspec C2/C3 nuances.

No unresolved gap; no item required to be deferred to §6.

## 6. Escalations

None.
