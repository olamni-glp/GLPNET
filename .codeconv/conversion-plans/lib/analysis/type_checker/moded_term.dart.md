---
path: lib/analysis/type_checker/moded_term.dart
cycle_group_id: 13
scc_siblings: []
generated_at: 2026-05-21T14:46:28Z
source_sha256: e1f9f5809ff29101ca4c63e08173c7db6d02257c350d132b6e55e90c4f790fe2
schema_version: 1
---

# Conversion Plan: lib/analysis/type_checker/moded_term.dart

## 1. Source Analysis

Direct inspection of `glp_runtime_net/lib/analysis/type_checker/moded_term.dart`
(501 lines, sha256 `e1f9f580…f790fe2`). The file implements GLP's *moded
term* algebra (Definition 4.2 of the GLP paper, spec `docs/modules/moded-
term.md v0.5`). Exactly one import: `mode.dart` (provides `Mode` enum +
`Mode.flip` extension).

Structural inventory (verbatim from source):

- **`abstract class ModedTerm`** (lines 22–28): two abstract members —
  `Mode get mode` (getter) and `T accept<T>(ModedTermVisitor<T> visitor)`.
- **`class ModedCompound extends ModedTerm`** (lines 33–76):
  - Fields: `final Mode mode` (override), `final String functor`,
    `final int arity`, `final List<ModedTerm> args`.
  - Primary ctor: `ModedCompound(this.mode, this.functor, this.arity, this.args)`.
  - Factory: `ModedCompound.listCons(Mode, ModedTerm, ModedTerm)` → returns
    `ModedCompound(mode, '[|]', 2, [head, tail])`.
  - Getter: `bool get isListCons => functor == '[|]' && arity == 2`.
  - `accept<T>` → `visitor.visitCompound(this)`.
  - `toString()`: mode-glyph (`↓`/`↑`) + functor/list-cons-aware
    formatting.
  - `==`/`hashCode`: hand-written, uses `_listEquals(args, other.args)` +
    `Object.hash(mode, functor, arity, Object.hashAll(args))`.
- **`class ModedConstant extends ModedTerm`** (lines 81–134):
  - Fields: `final Mode mode` (override), `final Object value`.
  - Primary ctor + factory `ModedConstant.nil(Mode)` → `ModedConstant(mode, '[]')`.
  - Six classification getters: `isNil` (`value == '[]'`), `isInteger`
    (`value is int`), `isReal` (`value is double`), `isNumeric` (`value is num`),
    `isString` (String + quote-prefix/suffix sniffing for `"`/`'`), `isAtom`
    (String && !isString).
  - `accept<T>` → `visitor.visitConstant(this)`.
  - `toString()`: mode-glyph + value.
  - `==`/`hashCode`: `mode == other.mode && value == other.value` /
    `Object.hash(mode, value)`.
- **`class ModedVariable extends ModedTerm`** (lines 145–195):
  - Fields: `final String name`, `final bool isReader`,
    `final Mode _structuralMode` (library-private leading-underscore).
  - Primary ctor: positional `name` + required-named
    `{required this.isReader, required Mode structuralMode}` with init-list
    `: _structuralMode = structuralMode`.
  - Factories: `ModedVariable.reader(name, {required structuralMode})`,
    `ModedVariable.writer(name, {required structuralMode})`.
  - `mode` getter → `_structuralMode`.
  - `implicitMode` getter → `isReader ? Mode.consume : Mode.produce`.
  - `isModeConsistent` getter → `implicitMode == _structuralMode`.
  - `isWriter` getter → `!isReader`.
  - `accept<T>` → `visitor.visitVariable(this)`.
  - `toString()` → `isReader ? '$name?' : name`.
  - `==`/`hashCode`: three-field equality / `Object.hash(name, isReader,
    _structuralMode)`.
- **`abstract class ModedTermVisitor<T>`** (lines 198–202): three abstract
  methods `visitCompound`, `visitConstant`, `visitVariable` returning `T`.
  No fields, no concrete methods — pure structural contract.
- **`class ModedPath`** (lines 214–245):
  - Field: `final List<PathStep> steps`.
  - Primary ctor: `ModedPath(this.steps)`.
  - Getters: `root` (`steps.first`), `leaf` (`steps.last`), `isInputPath`
    (`root.mode == Mode.consume`), `isOutputPath`, `length` (`steps.length`).
  - `toString()`: `steps.map(... '({symbol}, {argIndex}, {mode})').join(' → ')`.
  - `==`/`hashCode`: `_listEquals(steps, other.steps)` / `Object.hashAll(steps)`.
- **`class PathStep`** (lines 248–296):
  - Fields: `final String symbol`, `final int argIndex`, `final Mode mode`,
    `final bool isVariable`, `final bool isReader`.
  - Primary ctor: required-named `symbol`/`argIndex`/`mode` +
    optional-named `isVariable = false`/`isReader = false`.
  - Getter: `isWriter` → `isVariable && !isReader`.
  - `toString()`: `'($symbol, $argIndex, $modeStr)'`.
  - `==`/`hashCode`: five-field equality / `Object.hash(symbol, argIndex,
    mode, isVariable, isReader)`.
- **Library-private visitor classes** (leading `_`):
  - `class _IsConsumedVisitor implements ModedTermVisitor<bool>` (lines
    310–327): stateless; `visitCompound` short-circuits on
    `term.mode != Mode.consume`, else `term.args.every((arg) =>
    arg.accept(this))`.
  - `class _IsProducedVisitor implements ModedTermVisitor<bool>` (lines
    336–353): symmetric, with `Mode.produce`.
  - `class _DualVisitor implements ModedTermVisitor<ModedTerm>` (lines
    409–430): builds a *fresh* dual sub-tree — `visitCompound` returns
    `ModedCompound(mode.flip, functor, arity, args.map((arg) =>
    dual(arg)).toList())` (NEW list, NEW nodes throughout — contrast
    `TypeRef.dual()` in type_ast.dart which aliases its list).
- **Top-level functions**:
  - `bool isConsumed(ModedTerm t)` (line 306), `bool isProduced(ModedTerm t)`
    (line 332), `bool isIO(ModedTerm t)` (line 369) — public.
  - `bool _allPathsValidIO(ModedTerm t, Mode parentMode)` (line 378) —
    private; `if (t is ModedCompound) return t.args.every(...)`, else leaf
    OK.
  - `ModedTerm dual(ModedTerm t)` (line 405), `Set<ModedPath> paths(ModedTerm t)`
    (line 440) — public.
  - `void _extractPaths(ModedTerm t, List<PathStep> prefix, Set<ModedPath>
    result)` (line 457), `String _symbolOf(ModedTerm t)` (line 478) —
    private; `_symbolOf` ends with `throw ArgumentError('Unknown moded term
    type: $t')` at unreachable arm.
  - `bool _listEquals<T>(List<T> a, List<T> b)` (line 494) — generic
    private helper.

Aliasing semantics (load-bearing): `_extractPaths` constructs path prefixes
via `[...prefix, childStep]` (fresh list per recursion), but `_DualVisitor
.visitCompound` constructs an entirely fresh sub-tree (the args list and
every element are newly allocated by recursive `dual`) — NO aliasing
between dual input and output.

Unicode glyphs in source: `↓` (U+2193, line 56/124/280/452), `↑` (U+2191,
implicit complementary path), `→` (U+2192, line 236). These are the GLP
mode notation and must round-trip verbatim through the C# port.

## 2. Dart → C#/.NET Conversion Plan

Mirrors the ratified convspec's twelve constructs in order. Target file
emits to `lib/analysis/type_checker/moded_term.cs`.

### C1 — Abstract closed-sum base with visitor dispatch
(convspec construct `dart.abstract_base_class.closed_sum_with_visitor_dispatch`,
finding `rf-dart-abstract-ast-base-to-csharp-abstract-sealed-leaves`,
CACHED from `type_ast.dart`)

**Dart**: `abstract class ModedTerm { Mode get mode; T accept<T>
(ModedTermVisitor<T> visitor); }` plus three concrete subclasses overriding
`accept` to dispatch via the visitor.

**C#**: Emit `public abstract class ModedTerm` declaring
`public abstract Mode Mode { get; }` (abstract read-only auto-property) and
`public abstract T Accept<T>(IModedTermVisitor<T> visitor)`. Three concrete
leaves become `public sealed class ModedCompound : ModedTerm`,
`public sealed class ModedConstant : ModedTerm`, `public sealed class
ModedVariable : ModedTerm`, each overriding `Mode` and `Accept<T>` to call
`visitor.VisitCompound(this)` / `VisitConstant(this)` / `VisitVariable(this)`.
The base is **NOT** marked `sealed` (Microsoft Learn: "It's an error to use
the abstract modifier with a sealed class"). Closure is expressed by
sealing the three leaves and by throwing-default-arm discards at every
consumer type-switch site. All three are reference types (`class`, never
`struct`/`record struct`) — preserves shared-sub-tree aliasing
`_extractPaths` relies on.

### C2 — Visitor as pure-contract interface with covariant T
(convspec construct `dart.visitor_pattern.generic_double_dispatch_abstract_interface`,
finding `rf-dart-abstract-class-pure-contract-to-csharp-interface`)

**Dart**: `abstract class ModedTermVisitor<T> { T visitCompound
(ModedCompound term); T visitConstant(ModedConstant term); T visitVariable
(ModedVariable term); }`.

**C#**: Emit `public interface IModedTermVisitor<out T> { T
VisitCompound(ModedCompound term); T VisitConstant(ModedConstant term); T
VisitVariable(ModedVariable term); }`. The `out T` covariance marker is
permissible because `T` only appears in return position. Dart `abstract
class` carrying *no state* + *no concrete methods* maps to C# `interface`,
not `abstract class`. The `I`-prefix follows .NET naming convention.

### C3 — Library-private visitor implementations
(convspec construct `dart.private_class.leading_underscore_visitor_impl`,
finding `rf-dart-library-private-underscore-to-csharp-file-or-internal`)

**Dart**: Three leading-underscore classes `_IsConsumedVisitor`,
`_IsProducedVisitor`, `_DualVisitor` (library-private scope).

**C#**: Emit as `file sealed class IsConsumedVisitor :
IModedTermVisitor<bool>` (C# 11+ `file` modifier — closest match to Dart
library-private), OR `internal sealed class …` fallback for pre-C#-11
targets. The codegen stage picks one project-wide policy. All three are
stateless ⇒ may share a `private static readonly … Instance = new();`
singleton for devirtualisation (optimisation, no semantic shift). Each
`visitCompound` / `visitConstant` / `visitVariable` body translates
verbatim (with member-name PascalCase).

### C4 — `Object`-typed value field with type-pattern classification
(convspec construct `dart.field.object_typed_value_holding_int_double_string`,
finding `rf-dart-object-union-int-double-string-to-csharp-object-with-type-patterns`)

**Dart**: `class ModedConstant { ... final Object value; bool get isInteger
=> value is int; bool get isReal => value is double; bool get isNumeric =>
value is num; bool get isString {...}; bool get isAtom {...}; }`.

**C#**: `public object Value { get; }` (non-nullable `object`, NOT
`object?` — Dart field is `Object`, not `Object?`). Classification
getters:

- `public bool IsInteger => Value is int;`
- `public bool IsReal => Value is double;`
- `public bool IsNumeric => Value is int or double;` (disjunctive type
  pattern; Dart `num` is closed over `int|double` only — exact match).
- `public bool IsString { get { if (Value is not string s) return false;
  return (s.StartsWith("\"") && s.EndsWith("\"")) || (s.StartsWith("'") &&
  s.EndsWith("'")); } }` (character-level sniffing preserved verbatim, no
  Regex).
- `public bool IsAtom => Value is string && !IsString;`
- `public bool IsNil => Equals(Value, "[]");` (uses `object.Equals` so the
  boxed string `"[]"` dispatches to `String.Equals` for value equality).

NOT mapped to `IConvertible` / `INumber<T>` / `OneOf<int,double,string>` —
Dart `num`/`Object` semantics are exactly the C# disjunctive type test.

### C5 — Manual `==`/`hashCode` (no collection fields)
(convspec construct `dart.value_class.manual_eq_hashcode_no_collection_member`,
finding `rf-dart-value-class-manual-eq-to-csharp-iequatable-objectequals`)

Applies to `ModedConstant`, `ModedVariable`, `PathStep`.

**C#**: Each implements `IEquatable<T>` plus:
- `public bool Equals(T? other)` — hand-written, comparing all fields.
- `public override bool Equals(object? obj) => obj is T t && Equals(t);`
- `public override int GetHashCode() => HashCode.Combine(...);`
- `public static bool operator ==(T? a, T? b) => EqualityComparer<T>
  .Default.Equals(a, b);`
- `public static bool operator !=(T? a, T? b) => !(a == b);`

For `ModedConstant`: `Equals(other) => Mode == other.Mode &&
object.Equals(Value, other.Value)` — using the **static** `object.Equals`
to dispatch through boxed `int`/`double`/`string` value-equality (NOT
`ReferenceEquals`, NOT `==` on `object`). `GetHashCode()` returns
`HashCode.Combine(Mode, Value)`.

For `ModedVariable`: three-field equality on `Name`, `IsReader`,
`structuralModeField` (the private backing field for the overridden `Mode`
property).

For `PathStep`: five-field equality on `Symbol`, `ArgIndex`, `Mode`,
`IsVariable`, `IsReader`.

NOT positional `record` — record value-equality on an `object` field
defaults to reference equality, which would silently regress `ModedConstant
==` for boxed scalars.

### C6 — Manual `==`/`hashCode` with `List<>` field (element equality)
(convspec construct `dart.value_class.manual_eq_with_list_field_element_equality`,
finding `rf-dart-list-element-value-equality-to-csharp-sequenceequal`,
CACHED from `type_ast.dart`)

Applies to `ModedCompound` (field `Args`) and `ModedPath` (field `Steps`).

**C#**: Plain `sealed class` (NOT `record`) implementing `IEquatable<T>`:
- `Equals(other) => Mode == other.Mode && Functor == other.Functor &&
  Arity == other.Arity && Args.SequenceEqual(other.Args);` for
  `ModedCompound`.
- `Equals(other) => Steps.SequenceEqual(other.Steps);` for `ModedPath`.
- `GetHashCode()` uses a `HashCode` accumulator: `var hc = new
  HashCode(); hc.Add(Mode); hc.Add(Functor); hc.Add(Arity); foreach (var
  a in Args) hc.Add(a); return hc.ToHashCode();` (mirrors Dart's
  `Object.hash(...) + Object.hashAll(args)` combined into one consistent
  hash).
- `Args` / `Steps` exposed as `IReadOnlyList<ModedTerm>` /
  `IReadOnlyList<PathStep>` for the public surface (mirroring Dart's
  `final` discipline — discourages external mutation; underlying storage
  is `List<T>`). The list reference is stored as-passed, NOT defensively
  cloned (mirrors Dart aliasing semantics).

Positional `record` REJECTED — record value-equality on `List<>` members
is reference equality (cached Microsoft Learn citation from
`type_ast.dart`).

### C7 — `HashSet<ModedPath>` for returned path set
(convspec construct `dart.set_collection.unordered_value_equal_path_collection`,
finding `rf-dart-set-of-value-types-to-csharp-hashset-uses-equatable`)

**Dart**: `Set<ModedPath> paths(ModedTerm t) { final result = <ModedPath>{};
... result.add(ModedPath(prefix)); return result; }`.

**C#**: Return `HashSet<ModedPath>` (publicly typed as
`IReadOnlySet<ModedPath>` if desired). Deduplication relies on
`ModedPath`'s hand-written `IEquatable<ModedPath>` (C6). NOT `FrozenSet`
(mutated during traversal) and NOT `ImmutableHashSet` (copy-on-write
overhead). The Dart `Set` is `LinkedHashSet` (insertion-ordered); C#
`HashSet` is not — divergence is benign for documented callers, recorded
per SC-006 (no caller depends on order).

### C8 — Factory constructors as static methods
(convspec construct `dart.factory_constructor.named_convenience_no_caching`,
finding `rf-dart-factory-ctor-const-default-to-csharp-static-factory`,
CACHED from `type_ast.dart`)

**Dart**: `factory ModedCompound.listCons(...)`, `factory ModedConstant
.nil(...)`, `factory ModedVariable.reader(...)`, `factory ModedVariable
.writer(...)`.

**C#**: Each maps to a `public static <Class> <Name>(...)` static method on
the class returning a `new` instance via the primary constructor:

- `public static ModedCompound ListCons(Mode mode, ModedTerm head,
  ModedTerm tail) => new ModedCompound(mode, "[|]", 2, new List<ModedTerm>
  { head, tail });`
- `public static ModedConstant Nil(Mode mode) => new ModedConstant(mode,
  "[]");`
- `public static ModedVariable Reader(string name, Mode structuralMode) =>
  new ModedVariable(name, isReader: true, structuralMode: structuralMode);`
- `public static ModedVariable Writer(string name, Mode structuralMode) =>
  new ModedVariable(name, isReader: false, structuralMode: structuralMode);`

No caching, no subtype-return, no `this` access — pure convenience
constructors. The list literal `[head, tail]` maps to `new List<ModedTerm>
{ head, tail }` (or C# 12 collection expression `[head, tail]`).

### C9 — Named-required parameters → positional ctor + named-arg call style
(convspec construct `dart.named_required_parameters.required_kwargs`,
finding `rf-dart-named-required-params-to-csharp-named-positional`)

**Dart**: `ModedVariable(this.name, {required this.isReader, required Mode
structuralMode}) : _structuralMode = structuralMode;` and `PathStep
({required String symbol, required int argIndex, required Mode mode, bool
isVariable = false, bool isReader = false})`.

**C#**:
- `public ModedVariable(string name, bool isReader, Mode structuralMode) {
  Name = name; IsReader = isReader; structuralModeField = structuralMode; }`
- `public PathStep(string symbol, int argIndex, Mode mode, bool isVariable
  = false, bool isReader = false) { Symbol = symbol; ArgIndex = argIndex;
  Mode = mode; IsVariable = isVariable; IsReader = isReader; }`

Call sites preserve readability via C# named-argument syntax (`new
ModedVariable("X", isReader: true, structuralMode: Mode.Consume)`).
C# 11's `required` modifier applies only to **members**, not ctor
parameters; positional parameters without defaults ARE compile-time
required, matching Dart semantics. `_structuralMode` becomes `private
readonly Mode structuralModeField` with a public `override Mode Mode =>
structuralModeField;`.

### C10 — Top-level functions on a host static class
(convspec construct `dart.toplevel_function.top_level_helper_pure`,
finding `rf-dart-top-level-function-to-csharp-static-method`,
CACHED from `mode.dart`)

**Dart**: Eight top-level functions (`isConsumed`, `isProduced`, `isIO`,
`dual`, `paths`, `_allPathsValidIO`, `_extractPaths`, `_symbolOf`,
`_listEquals`).

**C#**: Emit on host `public static class ModedTermOps` (name avoids the
`ModedTerm` clash, mirroring the `Mode`/`ModeOps` precedent from
`mode.dart`):
- `public static bool IsConsumed(ModedTerm t) => t.Accept(new
  IsConsumedVisitor());` (or singleton `IsConsumedVisitor.Instance`).
- `public static bool IsProduced(ModedTerm t) => …;`
- `public static bool IsIO(ModedTerm t) { if (t.Mode != Mode.Consume)
  return false; return AllPathsValidIO(t, Mode.Consume); }`
- `public static ModedTerm Dual(ModedTerm t) => t.Accept(new
  DualVisitor());`
- `public static HashSet<ModedPath> Paths(ModedTerm t) {...}`
- `private static bool AllPathsValidIO(ModedTerm t, Mode parentMode) {...}`
- `private static void ExtractPaths(ModedTerm t, List<PathStep> prefix,
  HashSet<ModedPath> result) {...}`
- `private static string SymbolOf(ModedTerm t) => t switch { ModedCompound
  c => $"{c.Functor}/{c.Arity}", ModedConstant c => c.Value.ToString()!,
  ModedVariable v => v.IsReader ? $"{v.Name}?" : v.Name, _ => throw new
  ArgumentException($"Unknown moded term type: {t}") };`
- `internal static bool ListEquals<T>(IReadOnlyList<T> a, IReadOnlyList<T>
  b) {...}` — or use `SequenceEqual` directly.

`private static` for helpers (file/internal policy per C3). The throwing
arm in `SymbolOf` preserves Dart totality at runtime since C# does not
compile-time-verify subtype exhaustiveness over the non-language-sealed
`ModedTerm` base.

### C11 — `List.map(...).toList()` → LINQ `Select(...).ToList()`
(convspec construct `dart.list_aliasing.shallow_copy_in_dual_traversal`,
finding `rf-dart-list-map-tolist-to-csharp-linq-select-tolist`)

**Dart**: `term.args.map((arg) => dual(arg)).toList()` in
`_DualVisitor.visitCompound`.

**C#**: `term.Args.Select(arg => ModedTermOps.Dual(arg)).ToList()` — eager
materialisation matching Dart's eager `.toList()`. Crucially this allocates
a **fresh** backing list AND each element is a freshly-allocated dual
node, so the entire dual sub-tree is structurally fresh — NO aliasing
between input and output (contrast with `TypeRef.dual()` in `type_ast.dart`
which DOES share its `typeArgs` list reference). Each file's semantics
preserved verbatim per the cached finding.

### C12 — `is`-pattern + type-switch
(convspec construct `dart.is_pattern_in_function_body.type_test_with_member_access`,
finding `rf-dart-extension-is-as-to-csharp-type-pattern-switch`,
CACHED from `type_ast.dart`)

**Dart**: `if (t is ModedCompound) { return t.args.every((arg) =>
_allPathsValidIO(arg, currentMode)); }` (flow-type-promotion);
`t is ModedVariable ? t.isReader : false` (conditional cast).

**C#**:
- `if (t is ModedCompound c) { return c.Args.All(arg => AllPathsValidIO
  (arg, currentMode)); }` — declaration pattern fuses `is`-test and bind.
- `t is ModedVariable v ? v.IsReader : false` — declaration pattern in
  ternary.
- `SymbolOf` uses a single `switch` expression with throwing discard arm
  (see C10).

### C13 — `iterable.every(p)` → LINQ `iter.All(p)`
(convspec construct `dart.list_every.short_circuit_universal_quantifier`,
finding `rf-dart-iterable-every-to-csharp-linq-all`)

**Dart**: `term.args.every((arg) => arg.accept(this))` in
`_IsConsumedVisitor` / `_IsProducedVisitor`; `term.args.every((arg) =>
_allPathsValidIO(arg, currentMode))` in `_allPathsValidIO`.

**C#**: `term.Args.All(arg => arg.Accept(this))` /
`term.Args.All(arg => AllPathsValidIO(arg, currentMode))`. Short-circuit
on first `false`; empty-sequence vacuous-truth (`true`) preserved
identically.

### C14 — String interpolation + Unicode arrow glyphs
(convspec construct `dart.string_concatenation.unicode_arrow_glyphs`,
finding `rf-dart-string-interp-unicode-to-csharp-interpolated-string-utf8`)

**Dart**: `'$modeStr$functor(${args.join(', ')})'`,
`'(${s.symbol}, ${s.argIndex}, ${s.mode})'`, mode glyphs `'↓'` / `'↑'` /
join separator `' → '`.

**C#**: `$"{modeStr}{Functor}({string.Join(", ", Args)})"`,
`$"({s.Symbol}, {s.ArgIndex}, {s.Mode})"`. Mode glyphs preserved verbatim
as `"↓"` / `"↑"` / `" → "` in UTF-8 C# source files (Roslyn accepts
Unicode literals; .NET SDK defaults to UTF-8 source encoding). Dart
`<list>.join(sep)` maps to `string.Join(sep, <enumerable>)`. The
`Args.join(', ')` chain implicitly stringifies each element via
`ModedTerm.ToString()` — preserved exactly in C# (`string.Join` on a
non-`string` `IEnumerable` calls `ToString()` per element). NO transcoding
to `↓`/`↑`/`→` escapes — glyphs are semantically
load-bearing.

### C15 — `toString()` overrides
(convspec construct `dart.toString_override.debug_formatting`,
finding `rf-dart-tostring-override-to-csharp-tostring-override`)

**Dart**: `@override String toString()` on `ModedCompound`, `ModedConstant`,
`ModedVariable`, `ModedPath`, `PathStep`.

**C#**: `public override string ToString()` on all five classes; bodies
use C# interpolated strings (C14). NO `[DebuggerDisplay]` attribute —
preserves source's `ToString()` choice as the debug representation.

## 3. Decomposed Task Units

- **T1**: Emit `public abstract class ModedTerm` (abstract `Mode` property + abstract `Accept<T>(IModedTermVisitor<T>)`).
- **T2**: Emit `public interface IModedTermVisitor<out T>` with `VisitCompound`/`VisitConstant`/`VisitVariable`.
- **T3**: Emit `public sealed class ModedCompound : ModedTerm` (fields, primary ctor, `ListCons` static factory, `IsListCons` getter, `Accept<T>`, `ToString`, `IEquatable<ModedCompound>` with `SequenceEqual`+`HashCode` accumulator, `==`/`!=`).
- **T4**: Emit `public sealed class ModedConstant : ModedTerm` (fields, primary ctor, `Nil` static factory, six classification getters via type patterns, `Accept<T>`, `ToString`, `IEquatable<ModedConstant>` with `object.Equals`-on-Value).
- **T5**: Emit `public sealed class ModedVariable : ModedTerm` (positional ctor, private `structuralModeField`, `Reader`/`Writer` static factories, `Mode` override, `ImplicitMode`, `IsModeConsistent`, `IsWriter`, `Accept<T>`, `ToString`, `IEquatable<ModedVariable>`).
- **T6**: Emit `public sealed class ModedPath` (`Steps : IReadOnlyList<PathStep>`, `Root`/`Leaf`/`IsInputPath`/`IsOutputPath`/`Length` getters, `ToString` with `→` separator, `IEquatable<ModedPath>` with `SequenceEqual`).
- **T7**: Emit `public sealed class PathStep` (positional ctor with `isVariable=false`/`isReader=false` defaults, `IsWriter` getter, `ToString`, `IEquatable<PathStep>`).
- **T8**: Emit `file sealed class IsConsumedVisitor : IModedTermVisitor<bool>` (with project-policy `file`/`internal` choice; optional `Instance` singleton).
- **T9**: Emit `file sealed class IsProducedVisitor : IModedTermVisitor<bool>`.
- **T10**: Emit `file sealed class DualVisitor : IModedTermVisitor<ModedTerm>` (uses `Select(arg => ModedTermOps.Dual(arg)).ToList()`).
- **T11**: Emit `public static class ModedTermOps` with public methods `IsConsumed`, `IsProduced`, `IsIO`, `Dual`, `Paths` and private helpers `AllPathsValidIO`, `ExtractPaths`, `SymbolOf` (with throwing discard arm `throw new ArgumentException(...)` preserving Dart totality).
- **T12**: Ensure UTF-8 source encoding for the emitted `.cs` file (Unicode glyphs `↓`/`↑`/`→` preserved verbatim).
- **T13**: Wire `using System.Linq;` (for `All`/`Select`/`ToList`/`SequenceEqual`) and `using System.Collections.Generic;` (for `List<T>`/`HashSet<T>`/`IReadOnlyList<T>`).

## 4. Research Findings

None required — all twelve constructs are resolved by the convspec's
ratified findings (seven first-seen against Microsoft Learn citations,
five CACHED from sibling files `mode.dart`, `type_ast.dart`, `prelude.dart`
per FR-024). No `WebSearch`/`WebFetch`/`Agent` calls needed; no new
research gaps surfaced during plan derivation.

## 5. Consistency Pass

Cross-checked plan §2 (C1–C15) against convspec §constructs (twelve
entries) and §conversion_units (eleven entries):

- **C1 ↔ convspec `dart.abstract_base_class.closed_sum_with_visitor_dispatch`
  + unit `abstract class ModedTerm`** — fixed — derived from convspec
  target_decision verbatim (abstract base, three sealed leaves, throwing
  discard arms).
- **C2 ↔ convspec `dart.visitor_pattern.generic_double_dispatch_abstract_interface`
  + unit `interface IModedTermVisitor<out T>`** — fixed — derived from
  convspec (interface, `out T` covariance, `I`-prefix).
- **C3 ↔ convspec `dart.private_class.leading_underscore_visitor_impl` +
  units `file sealed class IsConsumedVisitor`/`IsProducedVisitor`/
  `DualVisitor`** — fixed — derived from convspec (file/internal project
  policy, sealed for devirtualisation, optional singleton).
- **C4 ↔ convspec `dart.field.object_typed_value_holding_int_double_string`
  + unit `ModedConstant` classification getters** — fixed — derived from
  convspec (`object` non-nullable, `is int or double` disjunctive pattern,
  verbatim quote-sniffing for `IsString`).
- **C5 ↔ convspec `dart.value_class.manual_eq_hashcode_no_collection_member`
  + units `ModedConstant`/`ModedVariable`/`PathStep`** — fixed — derived
  from convspec (`IEquatable<T>` + manual `Equals`/`GetHashCode` + `==`/
  `!=` operators; `object.Equals(Value, other.Value)` for boxed-scalar
  dispatch; NOT `record`).
- **C6 ↔ convspec `dart.value_class.manual_eq_with_list_field_element_equality`
  + units `ModedCompound`/`ModedPath`** — fixed — derived from convspec
  (`SequenceEqual` on list fields, `HashCode` accumulator,
  `IReadOnlyList<>` public surface, NOT `record`, cached finding).
- **C7 ↔ convspec `dart.set_collection.unordered_value_equal_path_collection`
  + unit `Paths` returning `HashSet<ModedPath>`** — fixed — derived from
  convspec (`HashSet<T>` uses `IEquatable<T>`, NOT `FrozenSet`/`ImmutableHashSet`,
  ordering divergence benign).
- **C8 ↔ convspec `dart.factory_constructor.named_convenience_no_caching` +
  static factories `ListCons`/`Nil`/`Reader`/`Writer`** — fixed — derived
  from convspec cached finding (no caching, no subtype-return).
- **C9 ↔ convspec `dart.named_required_parameters.required_kwargs` + ctors
  of `ModedVariable`/`PathStep`** — fixed — derived from convspec
  (positional ctor + named-arg call style; defaults for optional;
  `_structuralMode` → `private readonly Mode structuralModeField`).
- **C10 ↔ convspec `dart.toplevel_function.top_level_helper_pure` + unit
  `static class ModedTermOps`** — fixed — derived from convspec cached
  finding (host class name avoids `ModedTerm` clash, public/private split,
  throwing arm in `SymbolOf`).
- **C11 ↔ convspec `dart.list_aliasing.shallow_copy_in_dual_traversal`
  inside `DualVisitor`** — fixed — derived from convspec (LINQ
  `Select(...).ToList()`, fresh sub-tree, no aliasing — explicit contrast
  with `TypeRef.dual()`).
- **C12 ↔ convspec `dart.is_pattern_in_function_body.type_test_with_member_access`
  in `AllPathsValidIO`/`ExtractPaths`/`Paths`/`SymbolOf`** — fixed —
  derived from convspec cached finding (declaration patterns; switch
  expression with throwing discard arm in `SymbolOf`).
- **C13 ↔ convspec `dart.list_every.short_circuit_universal_quantifier`
  inside visitors and `AllPathsValidIO`** — fixed — derived from convspec
  (`iter.All(p)` LINQ; vacuous-truth on empty preserved).
- **C14 ↔ convspec `dart.string_concatenation.unicode_arrow_glyphs` in
  every `ToString`** — fixed — derived from convspec (`$"…{X}…"`
  interpolation, `string.Join`, UTF-8 source preserves `↓`/`↑`/`→`
  glyphs verbatim).
- **C15 ↔ convspec `dart.toString_override.debug_formatting` on five
  classes** — fixed — derived from convspec (`public override string
  ToString()` 1:1; no `[DebuggerDisplay]`).

Conversion-unit coverage: all eleven convspec units (`ModedTerm`,
`ModedCompound`, `ModedConstant`, `ModedVariable`, `IModedTermVisitor<out
T>`, `IsConsumedVisitor`, `IsProducedVisitor`, `DualVisitor`, `ModedPath`,
`PathStep`, `ModedTermOps`) appear as explicit task units T1–T11. T12
covers the UTF-8 source-encoding constraint; T13 covers the LINQ/generics
using-directives.

Tombstone metadata cross-check: tombstone says `cycle_group_id: 6`, task
header says `cycle_group_id: 13`. Task header is authoritative per the
spawn contract — front-matter set to `13` with no escalation (purely a
header/tombstone-drift artefact; the conversion plan content is identical
either way).

No gaps surfaced. Convspec `escalations: []` confirmed; all twelve
constructs and all nuances (SC-006 explicitly addressed list of 9 items)
are mirrored in the plan.

## 6. Escalations

None.
