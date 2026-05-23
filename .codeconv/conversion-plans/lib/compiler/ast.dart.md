---
path: lib/compiler/ast.dart
cycle_group_id: 1
scc_siblings: []
generated_at: 2026-05-21T14:47:00Z
source_sha256: a8a6493e11d47ec727c829d4d595f3b77f27ae5d7f95122c26a0090b3dec81d6
schema_version: 1
---

# Conversion Plan: lib/compiler/ast.dart

## 1. Source Analysis

Direct inspection of `glp_runtime_net/lib/compiler/ast.dart` (286 lines, sha256
`a8a6493e11d47ec727c829d4d595f3b77f27ae5d7f95122c26a0090b3dec81d6` — matches the
ratified convspec mirror).

Structural inventory (line-numbered against the actual source):

- L1 file-level doc comment `/// Abstract Syntax Tree nodes for GLP`.
- L3 single external import: `import '../analysis/type_checker/type_ast.dart'
  show TypeDef, ProcDecl;` — used only by `Module` (L252–L254).
- L5–L11 `enum CompileMode { user, system }` — plain tag enum with per-member
  doc comments (L7, L9). No members, no overrides.
- L13–L19 `abstract class AstNode` — empty marker base carrying `final int
  line; final int column;` (L15–L16) set by positional ctor `AstNode(this.line,
  this.column)` (L18). No methods, no equality override.
- L21–L29 `class Program extends AstNode` — `final List<Procedure>
  procedures;` (L23); ctor `Program(this.procedures, int line, int column) :
  super(line, column)` (L25); `toString` interpolating
  `procedures.length` (L28).
- L31–L44 `class Procedure extends AstNode` — fields `String name`, `int
  arity`, `List<Clause> clauses` (L33–L35); ctor positional (L37–L38);
  derived getter `String get signature => '$name/$arity';` (L40);
  `toString` (L43).
- L46–L61 `class Clause extends AstNode` — `final Atom head` (L48), `final
  List<Guard>? guards` (L49, nullable), `final List<Goal>? body` (L50,
  nullable); ctor `Clause(this.head, {this.guards, this.body, required int
  line, required int column}) : super(line, column)` (L52–L53) — Dart
  `required` on `line`/`column`, optional named `guards`/`body`;
  `toString` (L55–L60) branches on `guards != null && guards!.isNotEmpty`
  and same for `body!`.
- L63–L74 `class Atom extends AstNode` — `final String functor; final
  List<Term> args;` (L65–L66); positional ctor (L68); derived `int get arity
  => args.length;` (L70); `toString` `'$functor(${args.join(", ")})'` (L73).
- L76–L87 `class Goal extends AstNode` — SAME shape as `Atom` but is a
  CONCRETE (non-abstract) class extended by `RemoteGoal`/`SpawnGoal` below;
  `final String functor; final List<Term> args;` (L78–L79); positional ctor
  (L81); derived `arity` (L83); `toString` (L86).
- L89–L99 `class Guard extends AstNode` — `final String predicate`, `final
  List<Term> args`, `final bool negated` (L91–L93); ctor mixes positional +
  named-with-default: `Guard(this.predicate, this.args, int line, int column,
  {this.negated = false}) : super(line, column);` (L95); `toString` (L98)
  ternary on `negated`.
- L101–L104 `abstract class Term extends AstNode` — intermediate sum-type
  base with forwarding positional ctor.
- L106–L114 `class VarTerm extends Term` — `final String name; final bool
  isReader;` (L107–L108); positional ctor (L110); `toString` `isReader ?
  '$name?' : name` (L113).
- L116–L126 `class StructTerm extends Term` — `final String functor; final
  List<Term> args;` (L117–L118); positional ctor (L120); derived `arity`
  (L122); `toString` (L125).
- L128–L144 `class ListTerm extends Term` — `final Term? head; final Term?
  tail;` (L129–L130, NULLABLE pair); positional ctor (L134); derived `bool
  get isNil => head == null && tail == null;` (L136); three-branch
  `toString` (L139–L143): nil → `'[]'`, singleton (tail null) → `'[$head]'`,
  cons → `'[$head|$tail]'`.
- L146–L164 `class ConstTerm extends Term` — `final Object? value;` (L147);
  positional ctor (L149); `toString` (L152–L163) tests `value is String`,
  preserves already-quoted strings via `startsWith`/`endsWith` checks for
  both `"…"` and `'…'`, else wraps in `"…"`; non-string branch returns
  `value.toString()` (Dart `Null.toString()` returns `"null"`).
- L166–L174 `class UnderscoreTerm extends Term` — `final bool isReader;`
  (L168); ctor `UnderscoreTerm(int line, int column, {this.isReader =
  false}) : super(line, column);` (L170) — named-with-default after
  positional line/column; `toString` `isReader ? '_?' : '_'` (L173).
- L176–L178 section banner comment `// === Module System AST Nodes ===`.
- L180–L188 `class ModuleDeclaration extends AstNode` — `final String name;`
  (L182); positional ctor (L184); `toString` `'-module($name).'` (L187).
- L190–L191 verbatim removal-note comment: `// ExportDeclaration,
  ImportDeclaration, and ProcRef removed in Phase 1. // Visibility is now
  declared per-procedure via 'exported procedure'.`
- L193–L220 `class RemoteGoal extends Goal` — fields `final Term module;
  final Goal goal;` (L196–L197); ctor `RemoteGoal(this.module, this.goal,
  int line, int column) : super('#', [module, _goalToTerm(goal)], line,
  column);` (L199–L200) — synthesises super-call args from `'#'` plus a
  list `[module, lowered-goal]`; derived getter `String? get
  staticModuleName` (L203–L208) tests `module is ConstTerm` and returns
  `(module as ConstTerm).value as String` else `null`; derived `bool get
  isDynamic => module is VarTerm;` (L211); `toString` (L214); private
  static `static Term _goalToTerm(Goal g) => StructTerm(g.functor, g.args,
  g.line, g.column);` (L217–L219).
- L222–L240 `class SpawnGoal extends Goal` — fields `final Goal innerGoal;
  final String agentId;` (L227–L228); ctor `SpawnGoal(this.innerGoal,
  this.agentId, int line, int column) : super('@', [_goalToTerm(innerGoal),
  ConstTerm(agentId, line, column)], line, column);` (L230–L231) — same
  pattern; `toString` (L234); private static `_goalToTerm` (L237–L239,
  textually duplicate of `RemoteGoal`'s; intentional per-class private).
- L242–L247 section banner `// === Type Declarations (Yardeni-Shapiro
  syntax) ===` + the note that `TypeDef`/`ProcDecl` come from the imported
  `type_ast.dart`.
- L249–L285 `class Module extends AstNode` — six fields: `final
  ModuleDeclaration? declaration` (L250), `final List<TypeDef> typeDefs`
  (L251), `final List<ProcDecl> procDeclarations` (L252), `final
  List<ProcDecl> paramProcDecls` (L253), `final List<Procedure>
  procedures` (L254), `final CompileMode compileMode` (L255); ctor
  (L258–L267) all-named with `const []` defaults on the four list fields,
  `CompileMode.user` default on `compileMode`, and `required int line,
  required int column`; derived getter `String? get name =>
  declaration?.name;` (L269); derived `Set<String> get exportedSignatures`
  (L273–L281) returns a fresh `<String>{}` populated by iterating
  `procDeclarations` and adding `decl.key` for each `decl.exported`;
  `toString` (L284) interpolates `name ?? "_anonymous"` and
  `procedures.length`.

Equality semantics observed: NO class in this file overrides `==`/`hashCode`
— every node uses Dart default reference identity (load-bearing for
compiler-pass side tables keyed by AST-node identity).

Mutability semantics observed: every field is `final` (re-binding-immutable);
all `List<T>` fields are exposed as raw Dart lists (structurally mutable in
Dart but never mutated by code in this file). Nullable fields used as
discriminators: `Clause.guards`/`body`, `ListTerm.head`/`tail`,
`ConstTerm.value`, `Module.declaration`.

Total: 1 enum + 1 abstract empty base (`AstNode`) + 1 abstract intermediate
(`Term`) + 1 concrete-but-extended (`Goal`) + 13 sealed-style leaves
(`Program`, `Procedure`, `Clause`, `Atom`, `Guard`, `VarTerm`, `StructTerm`,
`ListTerm`, `ConstTerm`, `UnderscoreTerm`, `ModuleDeclaration`, `RemoteGoal`,
`SpawnGoal`, `Module`).

## 2. Dart → C#/.NET Conversion Plan

The convspec is the authoritative mapping. Each Dart construct below maps to
the C# decision mirrored verbatim from the convspec's `constructs:` array;
the eight `research_finding_id`s remain the load-bearing rationale.

1. **`enum CompileMode { user, system }`** → `public enum CompileMode { User,
   System }`. Plain value-type tag enum; declaration order preserved so
   `default(CompileMode) == User` matches the Dart `Module.compileMode =
   CompileMode.user` default. Per-member doc comments → C# XML-doc
   `<summary>`. Carry-forward research finding
   `rf-dart-plain-enum-to-csharp-enum`.

2. **`abstract class AstNode` (+ `final int line, column` + positional
   ctor)** → `public abstract class AstNode` with `public int Line { get; }`
   / `public int Column { get; }` read-only auto-properties set via a
   `protected AstNode(int line, int column) { Line = line; Column = column;
   }` ctor. NOT `sealed` (Microsoft Learn: "It's an error to use the
   abstract modifier with a sealed class."). Reference type (`class`, NEVER
   `struct`/`record struct`). Carry-forward research finding
   `rf-dart-abstract-marker-base-to-csharp-abstract-sealed-leaves`.

3. **`abstract class Term extends AstNode`** → `public abstract class Term :
   AstNode` with `protected Term(int line, int column) : base(line, column)
   { }`. NOT `sealed` (forbidden alongside `abstract`). Same research
   finding as (2).

4. **`class Goal extends AstNode` (CONCRETE base, extended by
   `RemoteGoal`/`SpawnGoal`)** → `public class Goal : AstNode` — NEITHER
   `abstract` NOR `sealed` (sealing it would forbid `RemoteGoal : Goal` /
   `SpawnGoal : Goal`, Microsoft Learn: "A sealed class cannot be
   inherited."). Read-only auto-properties `Functor` (string) and `Args`
   (`IReadOnlyList<Term>`); derived `Arity => Args.Count;`; positional ctor
   delegating `: base(line, column)`; `ToString()` override using
   `string.Join(", ", Args)`. Same research finding (2)
   plus the load-bearing carry-out for `Goal` documented in
   `rf-dart-subclass-static-helper-super-args-to-csharp-static-method-base-ctor`.

5. **`class Program extends AstNode`** → `public sealed class Program :
   AstNode` with `public IReadOnlyList<Procedure> Procedures { get; }` set
   via the ctor (ALIASED, NOT defensively copied — mirrors Dart
   `this.procedures = procedures`); positional ctor `: base(line, column)`;
   `public override string ToString() => $"Program({Procedures.Count}
   procedures)";`. NO equality override. Carry-forward
   `rf-dart-sumleaf-no-eq-to-csharp-class-no-record`.

6. **`class Procedure extends AstNode`** → `public sealed class Procedure :
   AstNode` with `Name` (string), `Arity` (int), `Clauses`
   (`IReadOnlyList<Clause>`); derived `public string Signature =>
   $"{Name}/{Arity}";`; `ToString()` override `$"Procedure({Signature},
   {Clauses.Count} clauses)"`. NO equality override. Same finding (5).

7. **`class Clause extends AstNode` (named+required+nullable ctor)** →
   `public sealed class Clause : AstNode` with `Atom Head { get; }`,
   `IReadOnlyList<Guard>? Guards { get; }`, `IReadOnlyList<Goal>? Body { get;
   }`. Ctor: `public Clause(Atom head, IReadOnlyList<Guard>? guards,
   IReadOnlyList<Goal>? body, int line, int column) : base(line, column) {
   Head = head; Guards = guards; Body = body; }` — Dart `required int line,
   required int column` → plain (non-default) positional C# parameters (the
   C# compiler enforces caller-supplies). Optional Dart `{this.guards,
   this.body}` (nullable, no default) → plain positional `null`able-typed
   parameters; defaults supplied by the C# caller pattern `new Clause(head,
   null, null, 1, 0)` OR via named arguments. `ToString()` preserves the
   three-state observable: `var guardsStr = Guards is { Count: > 0 } ? $" :-
   {string.Join(", ", Guards)}" : "";` (property pattern fuses non-null +
   non-empty); same for `Body`. Research finding
   `rf-dart-named-required-and-default-params-to-csharp-positional-default`.

8. **`class Atom extends AstNode`** → `public sealed class Atom : AstNode`
   with `Functor` (string), `Args` (`IReadOnlyList<Term>`); derived `public
   int Arity => Args.Count;`; `ToString()` override
   `$"{Functor}({string.Join(", ", Args)})"`. NO equality override. Same
   finding (5) plus carry-forward
   `rf-dart-string-interpolation-join-to-csharp-interpolation-string-join`.

9. **`class Guard extends AstNode` (positional + named-with-default ctor)** →
   `public sealed class Guard : AstNode` with `Predicate` (string), `Args`
   (`IReadOnlyList<Term>`), `Negated` (bool). Ctor: `public Guard(string
   predicate, IReadOnlyList<Term> args, int line, int column, bool negated =
   false) : base(line, column) { … }` — Dart's `{this.negated = false}`
   maps directly to a C# default-valued positional parameter, callable both
   positionally and by name. `ToString()` ternary: `Negated ?
   $"~{Predicate}({string.Join(", ", Args)})" :
   $"{Predicate}({string.Join(", ", Args)})";`. Same finding (7).

10. **`class VarTerm extends Term`** → `public sealed class VarTerm : Term`
    with `Name` (string), `IsReader` (bool); positional ctor; `ToString()`
    override `IsReader ? $"{Name}?" : Name;`. Same finding (5).

11. **`class StructTerm extends Term`** → `public sealed class StructTerm :
    Term` with `Functor` (string), `Args` (`IReadOnlyList<Term>`); derived
    `Arity => Args.Count;`; `ToString()` override
    `$"{Functor}({string.Join(", ", Args)})"`. Same findings (5), (8).

12. **`class ListTerm extends Term` (nullable-pair discrimination)** →
    `public sealed class ListTerm : Term` with `Term? Head { get; }` and
    `Term? Tail { get; }` (BOTH nullable, under NRT). Derived `public bool
    IsNil => Head is null && Tail is null;`. `ToString()` override:
    ```
    if (IsNil) return "[]";
    if (Tail is null) return $"[{Head}]";
    return $"[{Head}|{Tail}]";
    ```
    Three observable cases (nil / singleton / cons) preserved by null
    discrimination — NOT collapsed to a marker subtype. Research finding
    `rf-dart-null-discriminated-pair-to-csharp-nullable-reference`.

13. **`class ConstTerm extends Term` (polymorphic Object? value + special
    toString)** → `public sealed class ConstTerm : Term` with `public
    object? Value { get; }`. `ToString()` override uses declaration pattern
    + explicit ordinal comparison:
    ```
    public override string ToString()
    {
        if (Value is string s)
        {
            if ((s.StartsWith("\"", StringComparison.Ordinal) && s.EndsWith("\"", StringComparison.Ordinal))
                || (s.StartsWith("'", StringComparison.Ordinal) && s.EndsWith("'", StringComparison.Ordinal)))
            {
                return s;
            }
            return $"\"{s}\"";
        }
        return Value?.ToString() ?? "null";
    }
    ```
    `StringComparison.Ordinal` is load-bearing — C# `string.StartsWith(string)`
    defaults to `CurrentCulture` which would let culture-sensitive collation
    alter quote detection. `Value?.ToString() ?? "null"` matches Dart
    `value.toString()` on null (which yields `"null"`). Research finding
    `rf-dart-runtime-type-test-with-string-quote-detection-to-csharp-declaration-pattern-ordinal`.

14. **`class UnderscoreTerm extends Term`** → `public sealed class
    UnderscoreTerm : Term` with `IsReader` (bool). Ctor `public
    UnderscoreTerm(int line, int column, bool isReader = false) : base(line,
    column) { IsReader = isReader; }` — Dart `{this.isReader = false}` →
    C# default-valued positional parameter. `ToString()` override `IsReader
    ? "_?" : "_";`. Same finding (7).

15. **`class ModuleDeclaration extends AstNode`** → `public sealed class
    ModuleDeclaration : AstNode` with `Name` (string); positional ctor;
    `ToString()` override `$"-module({Name})."`. Same finding (5).

16. **`class RemoteGoal extends Goal` (synthesised super-call args)** →
    `public sealed class RemoteGoal : Goal` with `Term Module { get; }`,
    `Goal Goal { get; }`. Ctor:
    ```
    public RemoteGoal(Term module, Goal goal, int line, int column)
        : base("#", new Term[] { module, GoalToTerm(goal) }, line, column)
    {
        Module = module;
        Goal = goal;
    }
    ```
    Per-construction `new StructTerm(...)` allocation preserved (no caching;
    matches Dart semantics exactly). Derived getters:
    ```
    public string? StaticModuleName => Module is ConstTerm ct ? (string?)ct.Value : null;
    public bool IsDynamic => Module is VarTerm;
    ```
    (Declaration patterns fuse `is`+`as`; matches Dart `module is ConstTerm`
    +`(module as ConstTerm).value as String`.) `ToString()` override
    `$"{Module} # {Goal}"`. Private static `private static Term GoalToTerm(
    Goal g) => new StructTerm(g.Functor, g.Args, g.Line, g.Column);`.
    Research finding
    `rf-dart-subclass-static-helper-super-args-to-csharp-static-method-base-ctor`.

17. **`class SpawnGoal extends Goal`** → `public sealed class SpawnGoal :
    Goal` with `Goal InnerGoal { get; }`, `string AgentId { get; }`. Ctor:
    ```
    public SpawnGoal(Goal innerGoal, string agentId, int line, int column)
        : base("@", new Term[] { GoalToTerm(innerGoal), new ConstTerm(agentId, line, column) }, line, column)
    {
        InnerGoal = innerGoal;
        AgentId = agentId;
    }
    ```
    Per-construction `new StructTerm(...)` + `new ConstTerm(...)` allocation
    preserved. `ToString()` override `$"{InnerGoal}@{AgentId}"`. Private
    static `GoalToTerm` helper (textually duplicate of `RemoteGoal`'s —
    intentional, each class owns its private static per Dart source). Same
    finding (16).

18. **`class Module extends AstNode` (all-named with `const []` defaults +
    `required` line/column)** → `public sealed class Module : AstNode` with
    six read-only properties: `ModuleDeclaration? Declaration`,
    `IReadOnlyList<TypeDef> TypeDefs`, `IReadOnlyList<ProcDecl>
    ProcDeclarations`, `IReadOnlyList<ProcDecl> ParamProcDecls`,
    `IReadOnlyList<Procedure> Procedures`, `CompileMode CompileMode`. Ctor:
    ```
    public Module(
        ModuleDeclaration? declaration,
        IReadOnlyList<TypeDef>? typeDefs,
        IReadOnlyList<ProcDecl>? procDeclarations,
        IReadOnlyList<ProcDecl>? paramProcDecls,
        IReadOnlyList<Procedure>? procedures,
        CompileMode compileMode,
        int line,
        int column)
        : base(line, column)
    {
        Declaration = declaration;
        TypeDefs = typeDefs ?? System.Array.Empty<TypeDef>();
        ProcDeclarations = procDeclarations ?? System.Array.Empty<ProcDecl>();
        ParamProcDecls = paramProcDecls ?? System.Array.Empty<ProcDecl>();
        Procedures = procedures ?? System.Array.Empty<Procedure>();
        CompileMode = compileMode;
    }
    ```
    Dart `const []` defaults → C# nullable parameter + `?? System.Array.Empty<T>()`
    fallback (Microsoft Learn `Array.Empty<T>`: canonical immutable shared
    empty array, `IReadOnlyList<T>`-compatible, allocation-free). The naive
    alternative `new List<T>()` is illegal as a default-parameter value
    (must be compile-time constant); a shared `static readonly List<T> _empty`
    would alias mutable state. `required int line, required int column` →
    plain positional. `CompileMode = CompileMode.user` default → Dart caller
    pattern requires explicit supply OR a thin C# convenience overload — the
    spec preserves explicit supply; named arguments at C# call sites give
    equivalent ergonomics. Derived getter: `public string? Name =>
    Declaration?.Name;`. `ExportedSignatures`:
    ```
    public ISet<string> ExportedSignatures
    {
        get
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            foreach (var decl in ProcDeclarations)
            {
                if (decl.Exported) result.Add(decl.Key);
            }
            return result;
        }
    }
    ```
    Fresh `HashSet<string>` per call (matches Dart per-call allocation);
    `StringComparer.Ordinal` tightens equality so signature strings
    (`"foo/2"`) compare byte-exact. `ToString()` override
    `$"Module({Name ?? "_anonymous"}, {Procedures.Count} procedures)"` —
    null-coalescing operator `??` is identical in both languages. Research
    finding `rf-dart-const-empty-list-default-to-csharp-array-empty`.

19. **Doc comments / banner comments / removed-declarations note** → trivial
    1:1 carry-over. `///` Dart doc comments map to C# XML-doc `<summary>`
    comments; `//` Dart line comments stay as `//` C# line comments
    verbatim. The two-line removed-declarations note ("ExportDeclaration,
    ImportDeclaration, and ProcRef removed in Phase 1.") is preserved
    verbatim in the C# source — it documents an intentional absence and is
    informational only.

Closure of the sum-type hierarchy is expressed by (a) sealing every leaf
(`Program`, `Procedure`, `Clause`, `Atom`, `Guard`, `VarTerm`, `StructTerm`,
`ListTerm`, `ConstTerm`, `UnderscoreTerm`, `ModuleDeclaration`, `RemoteGoal`,
`SpawnGoal`, `Module`) and (b) requiring consumer switch expressions over
`AstNode` / `Term` / `Goal` to include a throwing discard arm — C# does NOT
compile-time-verify subtype exhaustiveness over a non-language-`sealed`
base. The base classes `AstNode`, `Term` (`abstract`, must NOT be sealed)
and `Goal` (`class`, must NOT be sealed since it is extended by
`RemoteGoal`/`SpawnGoal`) carry that limitation by design.

## 3. Decomposed Task Units

- **T1**: Emit `CompileMode` enum (User, System) with XML-doc per member.
- **T2**: Emit `AstNode` abstract base (Line/Column auto-properties +
  protected ctor).
- **T3**: Emit `Term` abstract intermediate base (protected ctor delegating
  to `AstNode`).
- **T4**: Emit `Goal` concrete (non-abstract, non-sealed) base (Functor,
  Args, Arity, ToString).
- **T5**: Emit `Program` sealed leaf (Procedures, ToString).
- **T6**: Emit `Procedure` sealed leaf (Name, Arity, Clauses, Signature
  derived, ToString).
- **T7**: Emit `Clause` sealed leaf (Head, nullable Guards, nullable Body,
  positional ctor with property-pattern ToString).
- **T8**: Emit `Atom` sealed leaf (Functor, Args, Arity derived, ToString
  with string.Join).
- **T9**: Emit `Guard` sealed leaf (Predicate, Args, Negated default false,
  ternary ToString).
- **T10**: Emit `VarTerm` sealed leaf (Name, IsReader, ToString).
- **T11**: Emit `StructTerm` sealed leaf (Functor, Args, Arity derived,
  ToString).
- **T12**: Emit `ListTerm` sealed leaf (nullable Head/Tail, IsNil derived,
  three-branch ToString).
- **T13**: Emit `ConstTerm` sealed leaf (object? Value, declaration-pattern
  + ordinal-comparison ToString, null-coalescing-to-"null").
- **T14**: Emit `UnderscoreTerm` sealed leaf (IsReader default false,
  ToString).
- **T15**: Emit `ModuleDeclaration` sealed leaf (Name, ToString
  `-module(<n>).`).
- **T16**: Emit `RemoteGoal` sealed leaf (Module, Goal; super("#",
  [module, GoalToTerm(goal)], …); StaticModuleName declaration-pattern
  getter; IsDynamic getter; private static GoalToTerm helper).
- **T17**: Emit `SpawnGoal` sealed leaf (InnerGoal, AgentId; super("@",
  [GoalToTerm(innerGoal), new ConstTerm(agentId, …)], …); private static
  GoalToTerm helper).
- **T18**: Emit `Module` sealed leaf (six properties; `const []` defaults
  via nullable params + `?? Array.Empty<T>()` fallback; required
  line/column via plain positional; Name derived from Declaration?.Name;
  ExportedSignatures fresh HashSet with StringComparer.Ordinal; ToString
  with null-coalescing "_anonymous").
- **T19**: Carry over Dart `///` doc comments → C# `<summary>`; carry over
  `//` banner and removed-declarations comments verbatim.

## 4. Research Findings

None required. The convspec's eight research findings
(`rf-dart-plain-enum-to-csharp-enum`,
`rf-dart-abstract-marker-base-to-csharp-abstract-sealed-leaves`,
`rf-dart-sumleaf-no-eq-to-csharp-class-no-record`,
`rf-dart-named-required-and-default-params-to-csharp-positional-default`,
`rf-dart-null-discriminated-pair-to-csharp-nullable-reference`,
`rf-dart-runtime-type-test-with-string-quote-detection-to-csharp-declaration-pattern-ordinal`,
`rf-dart-subclass-static-helper-super-args-to-csharp-static-method-base-ctor`,
`rf-dart-const-empty-list-default-to-csharp-array-empty`, and the
carry-forward
`rf-dart-string-interpolation-join-to-csharp-interpolation-string-join`)
cover every construct in §2. Each finding is grounded in WebFetched
Microsoft Learn / dart.dev pages cached at convspec-ratification time; no
new research lookup is needed for this plan.

## 5. Consistency Pass

Cross-checked the plan in §2 against the convspec's `constructs:` array,
the source file at the declared SHA, and CLAUDE.md / project conventions:

- **SHA match**: plan declares
  `a8a6493e11d47ec727c829d4d595f3b77f27ae5d7f95122c26a0090b3dec81d6`,
  identical to convspec `source_sha256` and to the live file hash recomputed
  in this session — fixed at ratification time.
- **Subclass discipline for sum-type closure**: every concrete leaf class
  in §2 is marked `sealed`; the three bases (`AstNode`, `Term`, `Goal`) are
  explicitly NOT `sealed`. `Goal` specifically is `class` (NEITHER
  `abstract` NOR `sealed`) — matches convspec construct 2's nuance
  ("`Goal` must NOT be made C# `sealed` even though it is a 'leaf' in the
  abstract hierarchy") and Microsoft Learn ("A sealed class cannot be
  inherited.") — derived from `rf-dart-abstract-marker-base-to-csharp-abstract-sealed-leaves`.
- **Equality**: §2.5–§2.18 each explicitly state "NO equality override" /
  default reference identity — matches convspec construct 3 / finding
  `rf-dart-sumleaf-no-eq-to-csharp-class-no-record`. No item silently
  emits a `record`.
- **Nullability boundaries**: §2.7 keeps `Guards`/`Body` as
  `IReadOnlyList<…>?`; §2.12 keeps `Head`/`Tail` as `Term?`; §2.13 keeps
  `Value` as `object?`; §2.18 keeps `Declaration` as
  `ModuleDeclaration?` — matches convspec verbatim. No collapse of `null`
  to `Array.Empty<T>()` for the four nullable-discriminator cases (that
  conversion is RESERVED for `Module`'s `const []` defaults only).
- **`required` / `const []` mapping**: §2.7, §2.9, §2.14, §2.18 use plain
  positional non-default parameters for Dart `required` and nullable
  parameters + `?? Array.Empty<T>()` for Dart `const []` — matches
  convspec constructs 4 and 8 / findings
  `rf-dart-named-required-and-default-params-to-csharp-positional-default`
  and `rf-dart-const-empty-list-default-to-csharp-array-empty`.
- **Ordinal string comparison**: §2.13 explicitly uses
  `StringComparison.Ordinal` on `StartsWith`/`EndsWith`; §2.18 explicitly
  uses `StringComparer.Ordinal` for the `HashSet<string>` — matches
  convspec construct 6 / finding
  `rf-dart-runtime-type-test-with-string-quote-detection-to-csharp-declaration-pattern-ordinal`.
- **List aliasing**: §2.5 and the convspec construct 3 both specify
  ALIASED (not defensively copied) backing lists, matching Dart's
  `this.procedures = procedures` semantics. Identity preservation across
  parser → AST → compiler-passes is load-bearing.
- **Per-construction allocation in `RemoteGoal`/`SpawnGoal`**: §2.16 and
  §2.17 both state "no caching; matches Dart semantics exactly" — matches
  convspec construct 7 nuance.
- **No new escalations**: every construct in §2 is verbatim-derivable from
  the convspec's `constructs:` array (with `escalations: []`) and the
  research findings; no decision in this plan goes beyond what the
  ratified convspec covers.
- **GLP semantics**: this file is pure AST — no GLP runtime/type/SRSW
  semantics in play; nothing to cross-check against `docs/typed-glp-manual.md`
  or `docs/glp-cheat-sheet.md` beyond confirming that AST nodes are
  data-only and identity-keyed.

All consistency checks pass; no gap requires escalation.

## 6. Escalations

None.
