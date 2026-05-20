# Conversion Spec — lib/compiler/ast.dart

```yaml
schema_version: 1
source_path: lib/compiler/ast.dart
source_sha256: a8a6493e11d47ec727c829d4d595f3b77f27ae5d7f95122c26a0090b3dec81d6
target_code_unit: lib/compiler/ast.cs
constructs:
  - construct_key: dart.enum.plain_two_member_no_members
    source_form: >-
      enum CompileMode { user, system }
    target_decision: >-
      Plain C# `public enum CompileMode { User, System }`. Two-member tag enum
      with NO methods, fields, or `toString` override on the Dart side, so a
      1:1 value-type mapping applies. Declaration order is preserved so
      `default(CompileMode) == User` matches the Dart-source default (`user` is
      the first enum constant AND the `Module.compileMode` default — both must
      remain ordinal `0`). Doc-comments on each enum constant carry over as C#
      XML-doc `<summary>` comments on each member (informational, no
      behavioural impact).
    idiom_id: null
    research_finding_id: rf-dart-plain-enum-to-csharp-enum
    nuance: >-
      Dart and C# enums are both value types compared by value/ordinal, so
      equality semantics are preserved with no boxing/reference hazard. This
      enum carries no behaviour, so the enhanced-enum nuance (cf. mode.dart)
      does NOT apply here. The load-bearing detail is preserving declaration
      order so the C# default value coincides with the Dart default field
      initialiser (`this.compileMode = CompileMode.user` on `Module`).

  - construct_key: dart.abstract_base_class.ast_hierarchy_with_positional_line_column_fields
    source_form: >-
      abstract class AstNode { final int line; final int column;
      AstNode(this.line, this.column); }  + sixteen concrete subclasses
      across two layers (Program/Procedure/Clause/Atom/Goal/Guard/Module/
      ModuleDeclaration; abstract Term + VarTerm/StructTerm/ListTerm/
      ConstTerm/UnderscoreTerm; RemoteGoal/SpawnGoal extend Goal)
    target_decision: >-
      `public abstract class AstNode` carrying two `public int Line { get; }`
      / `public int Column { get; }` read-only auto-properties set via a
      protected constructor `protected AstNode(int line, int column) { Line =
      line; Column = column; }`. Each concrete leaf becomes `public sealed
      class <Name> : AstNode` (or `: Term`, `: Goal` for the inner layers).
      Although Dart `abstract class` is OPEN (any library may extend it),
      every consumer in this file and the surrounding compiler enumerates the
      concrete subtypes by `is`-test / type-switch (`module is ConstTerm`,
      `staticModuleName`/`isDynamic` on `RemoteGoal`), so the hierarchy is
      treated as a closed algebraic sum type. The C# `abstract` modifier is
      applied to `AstNode` (and to the inner `Term` base); the C# `sealed`
      modifier is NOT applied to either base — Microsoft Learn: "It's an
      error to use the abstract modifier with a sealed class." Closure is
      expressed by (a) sealing the LEAVES and (b) exhaustive type-pattern
      `switch` (with a throwing discard arm) in consumers. Two-level
      abstraction is preserved verbatim: `AstNode` → `Term` (abstract,
      intermediate) → `VarTerm`/`StructTerm`/`ListTerm`/`ConstTerm`/
      `UnderscoreTerm` (sealed leaves); `AstNode` → `Goal` (NON-abstract
      leaf — instantiable directly) → `RemoteGoal`/`SpawnGoal` (sealed
      sub-leaves that extend `Goal`). NOTE on `Goal`: in Dart `Goal` is a
      *concrete* (non-abstract) class with two sealed-style subclasses
      (`RemoteGoal`/`SpawnGoal`) that extend it AND pass a synthesised
      functor/args through `super`. The C# mapping must therefore keep
      `Goal` as a `public class Goal : AstNode` (NEITHER `abstract` NOR
      `sealed`) so it can both be instantiated for plain calls and be
      subclassed by `RemoteGoal`/`SpawnGoal`.
    idiom_id: null
    research_finding_id: rf-dart-abstract-marker-base-to-csharp-abstract-sealed-leaves
    nuance: >-
      Three intertwined nuances. (1) Reference-vs-value: AST nodes are
      reference types in both Dart (every `class` is a reference) and C#
      (`class`, NEVER `struct`/`record struct`) — shared sub-tree aliasing
      and identity must survive transformation passes (an `Atom` may appear
      in a `Clause.head` while its `Term` args are independently inspected;
      identity is the only way to attach side-tables to nodes during
      compilation). (2) Sealed-base illegality: Microsoft Learn forbids
      `abstract sealed`, so the customary "exhaustive sum type" idiom cannot
      put `sealed` on `AstNode` or `Term`; closure is shifted to (a) `sealed`
      on each leaf and (b) exhaustive type-pattern switch in consumers with a
      throwing default arm to preserve Dart's closed-set totality (C# does
      NOT compile-time-verify subtype exhaustiveness over a
      non-language-sealed base). (3) Position metadata is intentionally
      reference-counted via the heap-allocated `AstNode` — line/column travel
      with the node identity. This file has a SECOND non-abstract base
      (`Goal`) that is itself extended by `RemoteGoal`/`SpawnGoal`; that base
      must NOT be made C# `sealed` even though it is a "leaf" in the abstract
      hierarchy — sealing it would prevent the legal Dart inheritance
      `class RemoteGoal extends Goal`.

  - construct_key: dart.ast_leaf.value_carrying_no_eq_override_reference_identity
    source_form: >-
      class Program extends AstNode { final List<Procedure> procedures;
      Program(this.procedures, int line, int column) : super(line, column);
      @override String toString() => 'Program(${procedures.length} procedures)'; }
      // Same shape: Atom (functor + args + arity getter); Goal (functor + args +
      arity getter); Procedure (name + arity + clauses + signature getter);
      Guard (predicate + args + negated bool with named-default); Clause (head
      + nullable guards + nullable body, named-with-required line/column);
      ModuleDeclaration (name only); VarTerm (name + isReader); StructTerm
      (functor + args + arity getter); ConstTerm (Object? value + special
      toString); UnderscoreTerm (isReader, named-default).
    target_decision: >-
      Each carries its data fields as `public` read-only auto-properties set
      via the constructor; each becomes a `public sealed class <Name> :
      <Base>` with a `: base(line, column)` ctor delegation. Equality is
      DELIBERATELY NOT overridden — NONE of these Dart classes carries an
      `==`/`hashCode` override, so two `Atom("foo", [x], 0, 0)` instances are
      NOT equal in Dart (reference identity). C# classes therefore keep
      default `object.Equals` reference identity. Explicitly REJECTED:
      emitting any of these as a `record` (would synthesise structural
      equality and silently change semantics during compiler-pass identity
      tracking — Microsoft Learn Records: synthesized equality "uses the
      declared data members"). Lists (`procedures`, `args`, `clauses`,
      etc.) are exposed as `IReadOnlyList<T>` properties to mirror Dart's
      `final List<T>` field (the list reference is rebind-final; the list
      itself is structurally mutable in Dart but not mutated in practice on
      these nodes). The backing list is ALIASED, NOT defensively copied, to
      match Dart `this.procedures = procedures` semantics where the AST node
      shares the parser's list identity. Derived getters
      (`Procedure.signature`, `Atom.arity`, `Goal.arity`, `StructTerm.arity`)
      become `public string Signature => $"{Name}/{Arity}";` /
      `public int Arity => Args.Count;` expression-bodied properties.
    idiom_id: null
    research_finding_id: rf-dart-sumleaf-no-eq-to-csharp-class-no-record
    nuance: >-
      Value-vs-reference nuance applied uniformly across the AST. Compiler
      passes routinely use `IdentityHashMap`-style side tables keyed by AST
      node identity (Dart `Map<AstNode, T>` with the default identity-ish
      hashCode of `Object`); mapping any of these to a C# `record` would
      silently introduce structural equality, breaking those side tables and
      causing unrelated-but-structurally-equal nodes to collide. The spec
      mandates `sealed class` (NOT `record`/`record struct`) so equality
      remains reference identity matching Dart. The position-info pair
      (`Line`/`Column`) is *intentionally* part of every AST node's identity-
      free metadata, not a key — this is why no node here overrides
      equality. `Object?` (Dart `Object? value` on `ConstTerm`) → `object?`
      (C# NRT) preserves the Dart `Object?` nullable-of-top mapping.

  - construct_key: dart.ast_leaf.named_constructor_params_required_and_default_with_nullable_collections
    source_form: >-
      Clause(this.head, {this.guards, this.body, required int line, required
      int column}) : super(line, column);
      // also: Guard(this.predicate, this.args, int line, int column,
      {this.negated = false}) : super(line, column);
      // and:  UnderscoreTerm(int line, int column, {this.isReader = false})
      : super(line, column);
    target_decision: >-
      Map Dart `{this.guards, this.body, required int line, required int
      column}` to a C# constructor with all parameters positional + optional
      defaults: `public Clause(Atom head, IReadOnlyList<Guard>? guards, IReadOnlyList<Goal>? body, int line, int column) : base(line, column) { Head = head; Guards = guards; Body = body; }`.
      Dart's `required` on a named parameter compels callers to supply
      `line`/`column`; C# has no parameter-level `required` keyword
      pre-C#11, so the spec mandates plain positional (non-default)
      parameters for `line`/`column` — the C# compiler then enforces
      supplying them at every call site (functionally identical to Dart's
      `required`). The optional `guards`/`body` (Dart `List<Guard>?` /
      `List<Goal>?` — nullable, no default) become C# `IReadOnlyList<Guard>?
      guards = null` / `IReadOnlyList<Goal>? body = null` with explicit
      `= null` so callers can omit them positionally OR pass them by name
      (`new Clause(head, guards: gs, body: bs, line: 1, column: 0)`).
      `Guard.negated = false` / `UnderscoreTerm.isReader = false` map to C#
      `bool negated = false` / `bool isReader = false` default-valued
      positional parameters (Microsoft Learn named/optional arguments: a
      default-valued positional parameter is callable both positionally and
      by name). NULLABILITY: Dart `List<Guard>?` → C# `IReadOnlyList<Guard>?`
      under NRT; the `null` case (clause with no guard section) is preserved
      faithfully — distinct from "empty list" (a clause WITH a `:-` but no
      guards). The `Clause.toString` code-path branches on `guards != null
      && guards.isNotEmpty` — the C# spec keeps the SAME branch
      (`Guards is { Count: > 0 }` declaration-pattern) so the same three
      observable outputs survive: no `:-` (null), no `:-` (empty), `:- ...`
      (non-empty).
    idiom_id: null
    research_finding_id: rf-dart-named-required-and-default-params-to-csharp-positional-default
    nuance: >-
      Three nuances. (1) Dart `required` on a named parameter has NO direct
      C# pre-C#11 equivalent; the established idiom is plain (non-default)
      positional parameters — the C# compiler then statically requires the
      caller to provide them, functionally identical to Dart `required`. (2)
      Dart `List<T>?` (nullable list) is observationally distinct from an
      empty `List<T>` and the source's `toString` discriminates the two
      (`guards != null && guards.isNotEmpty`); C# `IReadOnlyList<T>?` under
      NRT preserves the same three-state observable (null / empty /
      non-empty) — the spec must NOT collapse `null` to an empty list (would
      drop the no-`:-` rendering). (3) Default `bool` arguments
      (`negated = false`, `isReader = false`) are 1:1 between Dart named-
      with-default and C# default-positional; both are callable by name at
      the call site so existing call patterns survive.

  - construct_key: dart.ast_leaf.discriminated_nullable_pair_with_derived_predicate
    source_form: >-
      class ListTerm extends Term { final Term? head; final Term? tail;
      ListTerm(this.head, this.tail, int line, int column) : super(line,
      column); bool get isNil => head == null && tail == null; @override
      String toString() { if (isNil) return '[]'; if (tail == null) return
      '[$head]'; return '[$head|$tail]'; } }
    target_decision: >-
      `public sealed class ListTerm : Term` with read-only nullable
      properties `public Term? Head { get; }` and `public Term? Tail { get;
      }`, both set via the constructor; derived `public bool IsNil => Head
      is null && Tail is null;` expression-bodied property; `ToString()`
      override that branches identically:
      `if (IsNil) return "[]"; if (Tail is null) return $"[{Head}]";
      return $"[{Head}|{Tail}]";`. `Term?` → `Term?` under C# NRT preserves
      the three encoded list cases: `[]` (both null), `[h]` (head non-null,
      tail null = end-of-list singleton), `[h|t]` (cons cell). NULLABILITY
      is load-bearing here and intentional: the spec MUST keep `Head` and
      `Tail` as `Term?` (NOT `Term` non-null with a sentinel) — the three
      cases are discriminated by the `null` pattern, not by a marker leaf.
      `Term` is reference-typed (a `class`), so `Term?` is a proper NRT
      nullable reference (Microsoft Learn nullable reference types: `T?`
      on a reference type tracks possible null without changing CLR
      representation). Equality NOT overridden — Dart source has no `==`
      override; reference identity is preserved.
    idiom_id: null
    research_finding_id: rf-dart-null-discriminated-pair-to-csharp-nullable-reference
    nuance: >-
      The three observable list cases (nil / singleton / cons) are encoded
      via the `Head`/`Tail` nullable pair. A naive refactor to a marker
      subtype (`NilListTerm`) would change the hierarchy shape and break
      callers that compare `head` / `tail` directly; the spec preserves the
      Dart shape verbatim. `Term?` is the canonical NRT mapping for
      "possibly-absent reference"; the static-analyser's flow tracking on
      `if (Tail is null) ... else use Tail`-style branches gives the same
      null-safety C# enforcement as Dart sound null safety. Reference
      semantics: a `ListTerm` may share its `Head` or `Tail` with other
      AST nodes — C# class reference semantics preserve that aliasing.

  - construct_key: dart.ast_leaf.const_term_polymorphic_value_with_branching_string_quoting_tostring
    source_form: >-
      class ConstTerm extends Term { final Object? value; ConstTerm(this.value,
      int line, int column) : super(line, column); @override String toString()
      { if (value is String) { final s = value as String; if ((s.startsWith('"')
      && s.endsWith('"')) || (s.startsWith("'") && s.endsWith("'"))) return s;
      return '"$value"'; } return value.toString(); } }
    target_decision: >-
      `public sealed class ConstTerm : Term` with a read-only nullable
      polymorphic-payload property `public object? Value { get; }` set via
      the constructor. `ToString()` override branches on the runtime type of
      `Value`: `if (Value is string s) { … } else return Value?.ToString() ?? "null";`.
      The Dart `value is String` test maps to C# declaration pattern
      `Value is string s` (fuses test + cast, NO double-evaluation, NO
      InvalidCast risk). The two `startsWith`/`endsWith` checks become
      `s.StartsWith('"', StringComparison.Ordinal) && s.EndsWith('"',
      StringComparison.Ordinal)` and the single-quote variant — explicit
      ordinal comparison so quoting recognition is byte-exact, not culture-
      sensitive. The `'"$value"'` interpolated fallback becomes `$"\"{s}\""`.
      The non-string branch is `Value?.ToString() ?? "null"` (NOT a plain
      `Value.ToString()` — `Value` may be null per `object? value`; the
      Dart `value.toString()` on null yields `"null"` via Dart's
      `null.toString()` extension behaviour). This is the exact same
      `Const(null)`-rendering subtlety addressed by terms.dart's
      `rf-dart-string-interpolation-join-to-csharp-interpolation-string-join`
      finding, carried forward.
    idiom_id: null
    research_finding_id: rf-dart-runtime-type-test-with-string-quote-detection-to-csharp-declaration-pattern-ordinal
    nuance: >-
      Three nuances. (1) Runtime-type-test idiom: Dart `value is String` +
      `value as String` is the canonical "test then cast" pair; C#
      declaration pattern `Value is string s` fuses the two into one
      construct (Microsoft Learn pattern-matching: declaration patterns
      "test the type of the variable, and assign it to a new variable"). (2)
      String-comparison ordinal-vs-culture nuance: `startsWith('"')` /
      `endsWith('"')` in Dart compare code units (Dart `String.startsWith`
      is by code-unit prefix); C# `string.StartsWith(string)` DEFAULTS to
      `CurrentCulture` — which would let culture-sensitive collation alter
      quote detection (catastrophic for an AST-pretty-printer). The spec
      mandates explicit `StringComparison.Ordinal` (Microsoft Learn
      string-comparison best practices: ordinal "performs a byte-by-byte
      comparison of the binary values" — the only correct choice for code-
      point quote recognition). (3) `Value?.ToString() ?? "null"` matches
      Dart `value.toString()` when `value` is null — Dart's `null` has a
      `toString` extension that returns `"null"`, C# requires the explicit
      null-coalescing.

  - construct_key: dart.subclass_synthesizing_super_args_for_codified_dispatch
    source_form: >-
      class RemoteGoal extends Goal { final Term module; final Goal goal;
      RemoteGoal(this.module, this.goal, int line, int column) : super('#',
      [module, _goalToTerm(goal)], line, column); static Term
      _goalToTerm(Goal g) { return StructTerm(g.functor, g.args, g.line,
      g.column); } } // same shape SpawnGoal('@', [_goalToTerm(innerGoal),
      ConstTerm(agentId, line, column)], ...);
    target_decision: >-
      `public sealed class RemoteGoal : Goal` (and the symmetric
      `SpawnGoal`). The Dart subclass passes a *synthesised* `(functor,
      args)` pair through `super(...)` — `'#'` plus a list containing the
      module term and the inner goal lowered to a `StructTerm` via a private
      static `_goalToTerm`. The C# spec preserves this verbatim: a `private
      static Term GoalToTerm(Goal g) => new StructTerm(g.Functor, g.Args,
      g.Line, g.Column);` static helper, with the ctor body invoking the
      base ctor `: base("#", new Term[] { module, GoalToTerm(goal) }, line,
      column)`. The `'#'`/`'@'` string literals are preserved as exact
      ordinal `string` literals (NOT `char`, NOT culture-folded). The
      `_goalToTerm` lowering allocates a NEW `StructTerm` per `RemoteGoal`/
      `SpawnGoal` construction — preserved exactly (no shared/cached
      lowering; new allocation each time, matching Dart's `StructTerm(...)`
      semantics). Both subclasses also expose Goal-specific accessors:
      `RemoteGoal.staticModuleName` (returns the module name iff the module
      is a `ConstTerm`, else `null`) → `public string? StaticModuleName =>
      Module is ConstTerm ct ? (string?)ct.Value : null;` declaration-
      pattern fused; `RemoteGoal.isDynamic` (true iff module is a `VarTerm`)
      → `public bool IsDynamic => Module is VarTerm;`. NOTE: `Goal` itself
      must NOT be C# `sealed` (it is a *concrete extensible* base in Dart);
      `RemoteGoal`/`SpawnGoal` ARE `sealed` (no further subclassing in this
      hierarchy).
    idiom_id: null
    research_finding_id: rf-dart-subclass-static-helper-super-args-to-csharp-static-method-base-ctor
    nuance: >-
      Subclass-super-args nuance. The Dart pattern `super(functor, [arg0,
      arg1], line, column)` with a private `static` helper to lower a
      sibling AST node into the args list maps 1:1 to C# `: base(...)` with
      a `private static` helper. The load-bearing decision is keeping `Goal`
      non-`sealed` — C# `sealed` on the parent class would forbid the
      subclass declaration `: Goal` (Microsoft Learn sealed: "A sealed class
      cannot be inherited.") The Dart `is`-check accessors (`isDynamic`,
      `staticModuleName`) use the same fused-test-and-cast pattern as
      `ConstTerm.toString` — C# declaration patterns provide the exact
      mapping with `?:` returning a nullable value for the negative arm. The
      lowering allocation cost (a new `StructTerm` per construction) is
      preserved without caching to match Dart's per-call semantics exactly;
      changing that would be a target-side optimisation outside the
      conversion contract.

  - construct_key: dart.aggregate_class.named_constructor_with_const_empty_list_defaults_and_required_named_params
    source_form: >-
      class Module extends AstNode { final ModuleDeclaration? declaration;
      final List<TypeDef> typeDefs; final List<ProcDecl> procDeclarations;
      final List<ProcDecl> paramProcDecls; final List<Procedure> procedures;
      final CompileMode compileMode; Module({this.declaration, this.typeDefs =
      const [], this.procDeclarations = const [], this.paramProcDecls = const
      [], this.procedures = const [], this.compileMode = CompileMode.user,
      required int line, required int column}) : super(line, column); String?
      get name => declaration?.name; Set<String> get exportedSignatures {
      final result = <String>{}; for (final decl in procDeclarations) { if
      (decl.exported) result.add(decl.key); } return result; } }
    target_decision: >-
      `public sealed class Module : AstNode` with read-only properties
      `public ModuleDeclaration? Declaration { get; }`, `public
      IReadOnlyList<TypeDef> TypeDefs { get; }`, `public IReadOnlyList<ProcDecl>
      ProcDeclarations { get; }`, `public IReadOnlyList<ProcDecl>
      ParamProcDecls { get; }`, `public IReadOnlyList<Procedure> Procedures {
      get; }`, `public CompileMode CompileMode { get; }`. Constructor: all
      parameters keyword-able via default values plus required `line`/
      `column`. The Dart `const []` defaults for the four list fields must
      become an IMMUTABLE empty list per default (never a shared mutable
      static `List<T>` — would alias mutable state across module
      instances): emit constructor body
      `TypeDefs = typeDefs ?? System.Array.Empty<TypeDef>();` for each list
      parameter (signature `IReadOnlyList<TypeDef>? typeDefs = null` ...
      same for the others), where `System.Array.Empty<T>()` returns a
      shared, allocation-free, immutable empty `T[]` that is safely re-used.
      `required int line, required int column` map to plain (non-default)
      positional parameters — the C# compiler enforces caller-supplies them,
      identical to Dart `required`. Derived getter `name` → `public string?
      Name => Declaration?.Name;` (null-conditional access propagates the
      `null` from a missing declaration). `exportedSignatures` →
      `public ISet<string> ExportedSignatures { get { var result = new
      HashSet<string>(StringComparer.Ordinal); foreach (var decl in
      ProcDeclarations) if (decl.Exported) result.Add(decl.Key); return
      result; } }` — explicit `StringComparer.Ordinal` so signature
      membership is byte-exact like Dart `Set<String>` (cf. type_ast.dart's
      `rf-dart-const-set-to-csharp-frozenset-ordinal` finding, applied to a
      Dart `<String>{}` literal rather than a `const {...}` literal — the
      same ordinal-comparer requirement applies).
    idiom_id: null
    research_finding_id: rf-dart-const-empty-list-default-to-csharp-array-empty
    nuance: >-
      Three nuances. (1) `const []` immutable default vs C# mutable
      static-shared default. A NAIVE C# default `IReadOnlyList<T> typeDefs
      = new List<T>()` is illegal (default parameters must be compile-time
      constants); a NAIVE shared `private static readonly List<T> _empty =
      new();` would alias *mutable* state if any caller down-casts and
      mutates (catastrophic if the AST is treated as immutable elsewhere).
      `System.Array.Empty<T>()` returns the canonical immutable shared empty
      array — Microsoft Learn `Array.Empty<T>`: "Returns an empty array …
      successive calls to this method on the same type may, but are not
      required to, return the same instance." It is `IReadOnlyList<T>`-
      compatible and safely re-used. (2) `required` named-parameter mapping
      is the same as on `Clause`/`Guard` above — plain positional
      (non-default) parameters statically enforce caller-supplies. (3)
      `exportedSignatures` returns a fresh mutable `HashSet<string>` per
      call in Dart; the C# spec preserves the per-call allocation (do NOT
      cache) but tightens equality to `StringComparer.Ordinal` so signature
      strings (`"foo/2"`) compare exactly byte-for-byte — preventing any
      future culture-sensitive locale from altering export membership.

  - construct_key: dart.tostring_interpolation_with_collection_join_and_branching
    source_form: >-
      @override String toString() => 'Program(${procedures.length} procedures)';
      // and: 'Procedure($signature, ${clauses.length} clauses)';
      // and: 'Module(${name ?? "_anonymous"}, ${procedures.length} procedures)';
      // and: '$functor(${args.join(", ")})'; (Atom, Goal, StructTerm)
      // and: negated ? '~$predicate(${args.join(", ")})' : '$predicate(...)';
      // and: Clause.toString with nullable-aware ' :- ${guards!.join(", ")}'
    target_decision: >-
      Every `toString` override maps to `public override string ToString()`
      returning the same interpolated string with `$"…"`. The `args.join(",
      ")` calls become `string.Join(", ", Args)` (Microsoft Learn
      `String.Join<T>(string?, IEnumerable<T>)` — element `ToString` per
      item, ordinal separator, no surrounding brackets — matches Dart
      `Iterable.join` exactly). `Module.name ?? "_anonymous"` (Dart null-
      coalescing) → C# `Name ?? "_anonymous"` (identical operator).
      `Clause.toString` keeps its branching shape: `var guardsStr = Guards
      is { Count: > 0 } ? $" :- {string.Join(", ", Guards)}" : "";` (the
      C# property pattern `is { Count: > 0 }` is the established idiom for
      "non-null AND non-empty" in one declaration pattern — Microsoft Learn
      pattern-matching property patterns). `Guard.toString` ternary
      `Negated ? $"~{Predicate}({string.Join(", ", Args)})" : $"{Predicate}
      ({string.Join(", ", Args)})";` mirrors Dart 1:1. The `Procedure
      .signature` derived getter (`'$name/$arity'`) renders identically as
      `$"{Name}/{Arity}"`.
    idiom_id: null
    research_finding_id: rf-dart-string-interpolation-join-to-csharp-interpolation-string-join
    nuance: >-
      Trivial-but-explicit: Dart's `'$x'` / `'${expr}'` and C#'s `$"{X}"`
      are 1:1 interpolation primitives (Microsoft Learn interpolated
      strings). `args.join(", ")` vs `string.Join(", ", Args)` — both emit
      element-`ToString` separated by the given string with no surrounding
      brackets. The Dart `'!'`-suffix on `guards!.join(", ")` (nullable-
      assertion operator: "I know this is non-null") becomes the C#
      property-pattern guard `Guards is { Count: > 0 }` which establishes
      non-null AND non-empty in one declaration; the alternative
      `Guards!.Any()` would force the null-forgiving operator which the
      Dart `!` already documents — using a pattern is cleaner and matches
      the Dart logical intent. For `ConstTerm.toString`, see the separate
      `rf-dart-runtime-type-test-with-string-quote-detection-to-csharp-declaration-pattern-ordinal`
      finding above (special-case branch).

conversion_units:
  - enum CompileMode { User, System } (1:1 plain enum; declaration order preserved so default == User)
  - abstract class AstNode (Line/Column read-only int props; protected ctor accepting line/column)
  - sealed class Program : AstNode (IReadOnlyList<Procedure> Procedures; ToString override; default reference identity equality)
  - sealed class Procedure : AstNode (string Name; int Arity; IReadOnlyList<Clause> Clauses; Signature derived getter; ToString override)
  - sealed class Clause : AstNode (Atom Head; IReadOnlyList<Guard>? Guards; IReadOnlyList<Goal>? Body; required line/column via plain positional; ToString override with property-pattern non-null+non-empty branching)
  - sealed class Atom : AstNode (string Functor; IReadOnlyList<Term> Args; Arity derived getter; ToString override using string.Join(", ", Args))
  - class Goal : AstNode (NON-sealed, NON-abstract — extensible base for RemoteGoal/SpawnGoal; string Functor; IReadOnlyList<Term> Args; Arity derived getter; ToString override)
  - sealed class Guard : AstNode (string Predicate; IReadOnlyList<Term> Args; bool Negated with default false; ToString override with ternary on Negated)
  - abstract class Term : AstNode (protected ctor — intermediate sum-type base; CANNOT be C# sealed since it is C# abstract)
  - sealed class VarTerm : Term (string Name; bool IsReader; ToString override returning Name + optional "?")
  - sealed class StructTerm : Term (string Functor; IReadOnlyList<Term> Args; Arity derived getter; ToString override using string.Join(", ", Args))
  - sealed class ListTerm : Term (Term? Head; Term? Tail; IsNil derived getter; ToString override with three-branch nil/singleton/cons rendering preserving null-discriminated cases)
  - sealed class ConstTerm : Term (object? Value polymorphic payload; ToString override with declaration-pattern Value-is-string branch + ordinal quote detection + null-coalescing-to-"null" non-string branch)
  - sealed class UnderscoreTerm : Term (bool IsReader with default false via positional default; ToString override returning "_" or "_?")
  - sealed class ModuleDeclaration : AstNode (string Name; ToString override returning "-module(<name>).")
  - sealed class RemoteGoal : Goal (Term Module; Goal Goal; super("#", [module, GoalToTerm(goal)], line, column); StaticModuleName declaration-pattern getter; IsDynamic getter; private static GoalToTerm helper)
  - sealed class SpawnGoal : Goal (Goal InnerGoal; string AgentId; super("@", [GoalToTerm(innerGoal), new ConstTerm(agentId, line, column)], line, column); private static GoalToTerm helper)
  - sealed class Module : AstNode (ModuleDeclaration? Declaration; IReadOnlyList<TypeDef> TypeDefs; IReadOnlyList<ProcDecl> ProcDeclarations; IReadOnlyList<ProcDecl> ParamProcDecls; IReadOnlyList<Procedure> Procedures; CompileMode CompileMode; Name derived from Declaration?.Name; ExportedSignatures fresh HashSet with StringComparer.Ordinal; required line/column via plain positional; const-[] defaults via System.Array.Empty<T>())

escalations: []
```

## Rationale & Research Provenance

This file is the GLP compiler AST: a closed sum-type hierarchy rooted at an
empty marker base `AstNode` (line/column metadata only), with two layers of
intermediate abstraction (`AstNode` → `Term` for expression nodes; `AstNode` →
`Goal` for predicate-call nodes, where `Goal` is a CONCRETE base that is
subclassed by `RemoteGoal`/`SpawnGoal`). Sixteen concrete leaf classes plus
one enum (`CompileMode`) and one aggregate (`Module`). The non-trivial
decisions all turn on Dart→C# *semantics* — sum-type closure without
`abstract sealed`, reference-vs-value identity at every leaf (NO node overrides
`==`/`hashCode`, intentional — compiler passes rely on identity-keyed side
tables), nullable-discriminated `ListTerm`, `required` named-parameter
mapping, `const []` immutable default-list mapping, ordinal string comparison
in `ConstTerm.toString` quote detection, and synthesised `super` arguments on
`RemoteGoal`/`SpawnGoal`. Several research findings carry forward verbatim
from `terms.dart` and `type_ast.dart` (same hierarchy shape, same idioms);
two are new to this file (`required`-named-param mapping and
`const []`/`Array.Empty<T>()` empty-list default).

### rf-dart-plain-enum-to-csharp-enum (carry-forward)

**Deep analysis.** `CompileMode { user, system }` is a pure tag enum with no
members and is also the type of `Module.compileMode` (defaulting to `user`).
Member order must be preserved so `default(CompileMode) == User` matches the
Dart-side default.

**Research (authoritative).** WebFetch
`https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/enum`
(cached from `type_ast.dart` / `mode.dart`) — Microsoft Learn: an
enumeration type "is a value type defined by a set of named constants of the
underlying integral numeric type." Plain Dart and plain C# enums are both
value types compared by value. Verbatim query: "C# enum value type named
constants ordinal default".

**Conclusion.** 1:1 plain `enum CompileMode { User, System }`. The
enhanced-enum nuance from `mode.dart` does NOT apply (no behaviour); the
only load-bearing requirement is declaration order so the default value
coincides with the Dart field initialiser.

### rf-dart-abstract-marker-base-to-csharp-abstract-sealed-leaves (carry-forward, two-level)

**Deep analysis.** `AstNode` is an empty open base with two `final`
positional fields (`line`, `column`) and a forwarding constructor — same
shape as `terms.dart`'s `Term`. It is *open* in Dart (not `sealed class`),
but every consumer enumerates the concrete leaves by `is`-check, so it is
used as a closed sum type. UNIQUE TO THIS FILE: a *second* intermediate
abstract base `Term` (inside `AstNode`) AND a NON-abstract concrete base
`Goal` that is itself extended by `RemoteGoal`/`SpawnGoal`. The hierarchy
shape is therefore `AstNode → {Program, Procedure, Clause, Atom, Goal*,
Guard, Term*, ModuleDeclaration, Module}` where `Term*` is itself abstract
with five sealed leaves and `Goal*` is *concrete* with two sealed sub-leaves.

**Research (authoritative).** WebFetch
`https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/sealed`
(cached from `terms.dart` / `type_ast.dart`) — Microsoft Learn, decisive:
*"It's an error to use the abstract modifier with a sealed class, because
an abstract class must be inherited by a class that provides an
implementation of the abstract methods or properties."* AND *"A sealed
class cannot be inherited."* — load-bearing for `Goal` specifically: making
`Goal` `sealed` would forbid `class RemoteGoal extends Goal`, which is the
exact Dart construct. Verbatim queries: "C# sealed abstract class
hierarchy"; "C# sealed class inheritance forbidden".

**Conclusion.** `abstract class AstNode` + `abstract class Term : AstNode`
+ `class Goal : AstNode` (NEITHER abstract NOR sealed) + sealed leaves
elsewhere. AST nodes stay reference types (`class`, NEVER `struct`/`record
struct`) so shared sub-tree aliasing and identity (load-bearing for
compiler-pass side tables) are preserved. Totality of Dart's closed-set
consumers is preserved by a throwing default arm in C# `switch`
expressions, since C# does NOT compile-time-verify subtype exhaustiveness
over a non-language-sealed base.

### rf-dart-sumleaf-no-eq-to-csharp-class-no-record (carry-forward, expanded)

**Deep analysis.** NONE of the sixteen concrete AST classes overrides
`==`/`hashCode`. Two `Atom("foo", [x], 0, 0)` instances are NOT equal in
Dart (reference identity). This is INTENTIONAL — compiler passes use
identity-keyed maps to attach type info, mode info, and bytecode addresses
to each AST node; structural equality would cause collisions between
structurally-equal-but-distinct nodes (two `VarTerm("X", false, ...)`
instances at different source positions must be distinct keys).

**Research (authoritative).** WebFetch
`https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/record`
(cached from `terms.dart` / `type_ast.dart`) — Microsoft Learn: synthesised
record equality "uses the declared data members." A positional `record
Atom(string Functor, IReadOnlyList<Term> Args, int Line, int Column)` would
make two `Atom("foo", [x], 1, 0)` and `Atom("foo", [x], 1, 0)` equal —
silently breaking identity-keyed compiler side tables. Verbatim query: "C#
record value equality versus class reference equality default identity-
keyed side table".

**Conclusion.** Every concrete AST class is `public sealed class … :
<Base>` (NEVER `record`); default reference-identity equality preserved
across the entire hierarchy. The asymmetry vs `type_ast.dart`'s `TypeRef`
(which DID override `==`) is preserved — no AST node in `ast.dart` is
intended to be structurally equated.

### rf-dart-named-required-and-default-params-to-csharp-positional-default

**Deep analysis.** `Clause`, `Guard`, `UnderscoreTerm`, and `Module` use
Dart named parameters in three flavours: (a) `required int line, required
int column` (must be supplied by caller, no default), (b) `{this.guards,
this.body}` (nullable, no default — caller may omit, becomes `null`), (c)
`{this.negated = false, this.isReader = false}` (optional with default).
The Dart `required` keyword is a *compile-time* enforcement of caller-
supplies; C# pre-C#11 has no parameter-level `required` modifier.

**Research (authoritative).** WebFetch
`https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/named-and-optional-arguments`
— Microsoft Learn: *"Named arguments enable you to specify an argument for
a parameter by matching the argument with its name"* and *"Optional
arguments enable you to omit arguments for some parameters"*; a default-
valued positional parameter is callable both positionally AND by name. A
plain (non-default) positional parameter is *compile-time required* — the
caller must supply it. WebFetch `https://dart.dev/language/functions`
(cached) — dart.dev official: Dart `required` on a named parameter
"compels" the caller; the converse, named parameter without `required` and
without a default, is implicitly nullable. Verbatim queries: "C# parameter
required equivalent positional"; "Dart required named parameter C# mapping".

**Conclusion.** Map Dart `required int line` to a plain positional
(non-default) C# parameter `int line` — the compiler enforces caller-
supplies, functionally identical. Map `{this.guards, this.body}` (nullable,
no default) to C# `IReadOnlyList<Guard>? guards = null,
IReadOnlyList<Goal>? body = null` (explicit `= null` so they remain
omissible). Map `{this.negated = false}` to C# `bool negated = false`. All
three are callable by name at the call site (`new Clause(head, guards: gs,
line: 1, column: 0)`), reproducing every legal Dart call site.

### rf-dart-null-discriminated-pair-to-csharp-nullable-reference

**Deep analysis.** `ListTerm.head` and `ListTerm.tail` are both `Term?`;
the three observable list cases (nil / singleton / cons) are encoded as
`(null, null)` / `(non-null, null)` / `(non-null, non-null)`. A derived
`isNil` getter and a three-way `toString` switch consume that encoding. The
nullable-pair shape is the established Dart way to represent
Lisp-style cons/nil in a single class without a marker subtype.

**Research (authoritative).** WebFetch
`https://learn.microsoft.com/en-us/dotnet/csharp/nullable-references`
— Microsoft Learn nullable reference types: `T?` on a reference type
"declares a variable that may be null" and propagates flow-analysis
through `is null` / `is not null` checks. WebFetch
`https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/functional/pattern-matching`
— Microsoft Learn: declaration patterns and the `is null` / `is not null`
forms enable flow-aware nullable-state tracking. Verbatim queries: "C#
nullable reference types flow analysis is null"; "Dart null-discriminated
pair encoding C# mapping".

**Conclusion.** `Term?` properties for `Head` and `Tail` preserve the
three-case discrimination by null. C# NRT gives the same null-safety
enforcement as Dart sound null safety; `ToString` keeps its three-branch
shape (`if (IsNil) return "[]"; if (Tail is null) return $"[{Head}]";
return $"[{Head}|{Tail}]";`). Reference-typed `Term` means a `ListTerm`
may share `Head`/`Tail` references with other AST nodes — preserved.

### rf-dart-runtime-type-test-with-string-quote-detection-to-csharp-declaration-pattern-ordinal

**Deep analysis.** `ConstTerm.toString` performs a `value is String` test,
then if the string is already wrapped in `"…"` or `'…'` it is returned as
is; otherwise it is wrapped in `"…"`. The non-string branch falls back to
`value.toString()`. Two subtleties: (1) `startsWith`/`endsWith` must be
exact-string (code-point) prefix/suffix matching, not culture-sensitive;
(2) `value.toString()` on null in Dart returns `"null"` (Dart's
`Null.toString` returns the literal "null").

**Research (authoritative).** WebFetch
`https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/functional/pattern-matching`
(cached) — Microsoft Learn: declaration patterns "test the type of the
variable, and assign it to a new variable", fusing `is` test + cast into
one construct (avoiding the Dart `is X`+`as X` double pattern). WebFetch
`https://learn.microsoft.com/en-us/dotnet/standard/base-types/best-practices-strings`
— Microsoft Learn string-comparison best practices: *"Use ordinal
comparisons for identifiers, paths, and other non-linguistic data"* and
explicit `StringComparison.Ordinal` "performs a byte-by-byte comparison of
the binary values" — the only correct choice for code-point quote
recognition in an AST pretty-printer. C# `string.StartsWith(string)`
default is `CurrentCulture` which would let locale alter quote detection
(catastrophic). Verbatim queries: "C# string StartsWith Ordinal comparison
default culture"; "C# null object ToString interpolation rendering".

**Conclusion.** `Value is string s` declaration pattern; explicit
`StringComparison.Ordinal` on both `StartsWith` and `EndsWith` calls;
fallback `Value?.ToString() ?? "null"` (NOT plain `{Value}` in
interpolation — would render empty for null, contradicting Dart's "null"
literal). Carries forward the `null`-rendering subtlety addressed by
`terms.dart`'s `rf-dart-string-interpolation-join-...` finding.

### rf-dart-subclass-static-helper-super-args-to-csharp-static-method-base-ctor

**Deep analysis.** `RemoteGoal` and `SpawnGoal` extend the CONCRETE
`Goal` base by synthesising the `(functor, args)` super-call arguments
from their own fields: `RemoteGoal` constructs `super("#", [module,
_goalToTerm(goal)], ...)` and `SpawnGoal` constructs `super("@",
[_goalToTerm(innerGoal), ConstTerm(agentId, line, column)], ...)`. The
private `static _goalToTerm` lowers a sibling `Goal` to a `StructTerm` —
i.e. the subclass exposes itself in two views simultaneously: as a
concrete `Goal` with synthesised functor/args (for uniform dispatch by
code that iterates over `body: List<Goal>`) AND as the richer subclass
with module/inner-goal fields (for code that pattern-matches on the
specific subclass).

**Research (authoritative).** WebFetch
`https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/sealed`
(cached) — Microsoft Learn: *"A sealed class cannot be inherited."* —
load-bearing: `Goal` CANNOT be C# `sealed` because `RemoteGoal` /
`SpawnGoal` extend it. WebFetch
`https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/using-constructors`
— Microsoft Learn: a constructor can invoke `: base(...)` to forward
synthesised arguments to the base constructor; static helpers may be
called inside the `: base(...)` initializer. Verbatim queries: "C# base
constructor forwarded arguments static helper"; "C# concrete class
inheritable not sealed".

**Conclusion.** `Goal` is `public class Goal : AstNode` (NOT `sealed`,
NOT `abstract`). `RemoteGoal`/`SpawnGoal` are `public sealed class … :
Goal` with a `private static Term GoalToTerm(Goal g)` helper and the
ctor body `: base("#", new Term[] { module, GoalToTerm(goal) }, line,
column)`. `StaticModuleName` and `IsDynamic` use declaration patterns
(`Module is ConstTerm ct`, `Module is VarTerm`) — fused test + cast,
preserving the Dart `is`+`as` intent in one construct. Per-construction
allocation of the lowered `StructTerm` is preserved (no caching) to match
Dart semantics exactly.

### rf-dart-const-empty-list-default-to-csharp-array-empty

**Deep analysis.** `Module`'s named-parameter ctor uses Dart `const []`
defaults for four `List<T>` fields (`typeDefs`, `procDeclarations`,
`paramProcDecls`, `procedures`). In Dart, `const []` is a compile-time
canonicalised immutable empty list — re-used safely across all callers
that omit the parameter. C# has TWO failure modes if mapped naively: (a)
a default parameter value like `new List<T>()` is illegal (default
parameters must be compile-time constants); (b) a shared `static readonly
List<T> _empty = new();` would alias *mutable* state across instances if
any caller obtained the reference and downcast.

**Research (authoritative).** WebFetch
`https://learn.microsoft.com/en-us/dotnet/api/system.array.empty`
— Microsoft Learn: *"Returns an empty array … Successive calls to this
method on the same type may, but are not required to, return the same
instance"* — the canonical immutable shared empty `T[]`, `IReadOnlyList<T>`-
compatible, allocation-free, safely re-used. WebFetch
`https://dart.dev/language/constructors` (cached) — dart.dev: const
constructors and `const` collection literals "create compile-time
constants" — `const []` is an immutable singleton-per-type-arg in Dart.
Verbatim query: "C# Array.Empty<T> immutable shared empty IReadOnlyList
default parameter".

**Conclusion.** Map `const []` defaults to nullable parameters
`IReadOnlyList<T>? typeDefs = null` plus a ctor body `TypeDefs = typeDefs
?? System.Array.Empty<TypeDef>();` — the `Array.Empty<T>()` call returns
the canonical immutable empty array, NEVER a shared mutable `List<>`.
This preserves Dart's "every default-using `Module` shares the SAME
empty-list instance, safely" semantics while remaining compatible with
C#'s default-parameter compile-time-constant constraint.

### rf-dart-string-interpolation-join-to-csharp-interpolation-string-join (carry-forward)

**Deep analysis.** `Program.toString`, `Procedure.toString`,
`Module.toString`, `Atom.toString`, `Goal.toString`, `StructTerm.toString`,
`Guard.toString`, and `Clause.toString` all use Dart string interpolation
with `args.join(", ")` (or the analogous `procedures.length`,
`clauses.length`). The only subtlety beyond
`terms.dart` is `Module.toString`'s `name ?? "_anonymous"` (Dart null-
coalescing) — identical operator in C#.

**Research (authoritative).** WebFetch
`https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/tokens/interpolated`
(cached) — Microsoft Learn interpolated strings: `$"{expr}"` "is replaced
by the result of the corresponding expression's `ToString` method".
WebFetch `https://learn.microsoft.com/en-us/dotnet/api/system.string.join`
(cached) — Microsoft Learn `String.Join<T>(string?, IEnumerable<T>)`:
"concatenates the members of a collection, using the specified separator
between each member" — element `ToString` per item, ordinal join, no
surrounding brackets — matches Dart `Iterable.join` exactly. Verbatim
queries: "C# String.Join IEnumerable element ToString separator"; "C#
null-coalescing operator interpolation".

**Conclusion.** All `toString` overrides map 1:1 with `$"…"` and
`string.Join(", ", …)`. The `Clause.toString` nullable-branching pattern
maps to C# property pattern `Guards is { Count: > 0 }` for the combined
"non-null AND non-empty" check. `Module.toString`'s `name ?? "_anonymous"`
uses the identical C# null-coalescing operator.

### Trivial constructs

Doc-comments (`///`) on `CompileMode.user`, `CompileMode.system`,
`ModuleDeclaration`, `RemoteGoal`, `SpawnGoal`, and the various module-
system members map mechanically to C# XML-doc `<summary>` comments and
carry NO behavioural conversion decision (informational only — trivial,
no research). Section banner `// =====` comments are preserved verbatim
as C# `//` comments. The TWO removed-declarations note
("ExportDeclaration, ImportDeclaration, and ProcRef removed in Phase 1.
Visibility is now declared per-procedure via 'exported procedure'.") is
preserved verbatim as a `//` comment in the C# source — it explains an
intentional absence and is load-bearing for human reviewers but has no
codegen impact. All non-trivial constructs carry both a deep-analysis
basis and an authoritative `research_finding_id` above.
