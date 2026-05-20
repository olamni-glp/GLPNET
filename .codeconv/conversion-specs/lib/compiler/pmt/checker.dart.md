# Conversion Spec — lib/compiler/pmt/checker.dart

> Conversion-spec artifact (FR-011) for `lib/compiler/pmt/checker.dart`.
> Spec-only (FR-023): describes the Dart→C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block below.

```yaml
schema_version: 1
source_path: lib/compiler/pmt/checker.dart
source_sha256: 2cdf947748a1e9b0f92210357cda90b7f453ebb6b9111c75db0445a7ade131ef
target_code_unit: lib/compiler/pmt/checker.cs
constructs:
  - construct_key: dart.coordinator_class.final_dep_plus_owned_helper_initialised_in_initialiser_list
    source_form: >-
      class PmtChecker { final ModeTable modeTable; final OccurrenceClassifier
      classifier; PmtChecker(this.modeTable) : classifier =
      OccurrenceClassifier(modeTable); }
    target_decision: >-
      Emit a non-sealed reference-type `class PmtChecker` with TWO get-only
      auto-properties initialised from the primary positional constructor:
      `public ModeTable ModeTable { get; }` (the injected dependency) and
      `public OccurrenceClassifier Classifier { get; }` (the owned helper).
      Constructor signature: `public PmtChecker(ModeTable modeTable) {
      ModeTable = modeTable; Classifier = new OccurrenceClassifier(modeTable);
      }`. The Dart constructor's initialiser-list `: classifier =
      OccurrenceClassifier(modeTable)` (dart.dev "Constructors — Initializer
      list": "you can initialize instance variables before the constructor
      body runs") is structurally a "field B is computed from constructor
      parameter / field A" pattern; the documented C# equivalent is two
      assignments inside the constructor body in source order, mirroring the
      Dart initialiser-list left-to-right evaluation. NO `IEquatable<
      PmtChecker>` (Dart source has no `==`/`hashCode` overrides → reference
      equality preserved on the C# side; same observable-contract-preserving
      rule applied in `type_checker.dart.md` for its coordinator
      `TypeChecker`). The class is NOT `sealed` (no subclassing precedent
      locked; downstream test fakes may subtype, same rationale as
      `type_checker.dart.md`'s `TypeChecker`). The two helper-data fields are
      exposed as `public` get-only properties (NOT `private readonly`)
      because — UNLIKE `TypeChecker`'s private fields — the source declares
      these without a leading underscore, signalling library-public access
      surface that downstream consumers might introspect; the C# port
      preserves the wider access in line with `occurrence.dart.md`'s
      `OccurrenceClassifier.ModeTable` get-only public property precedent.
    idiom_id: null
    research_finding_id: rf-dart-initialiser-list-owned-helper-to-csharp-ctor-body-assignment
    nuance: >-
      Three load-bearing nuances. (1) Owned-vs-injected distinction:
      `modeTable` is an INJECTED dependency (caller supplies; lifecycle
      external), `classifier` is OWNED (PmtChecker constructs it). The C#
      port preserves the distinction by allocating `new OccurrenceClassifier
      (modeTable)` inside the constructor body — NEVER hoisted to a field
      initialiser without parameter access (a field initialiser in C# cannot
      see constructor parameters; Microsoft Learn "Constructors —
      Constructor initializers": "Field initializers run before the
      constructor body, but they cannot reference instance state"). The
      constructor-body assignment is the documented canonical rendering. (2)
      Reference-aliasing preserved: both Dart `this.modeTable` (initialising-
      formal) and the body-passed `OccurrenceClassifier(modeTable)` store
      THE SAME reference; the C# port passes `modeTable` (the parameter)
      into `new OccurrenceClassifier(modeTable)`, so both `ModeTable` and
      `Classifier.ModeTable` (per occurrence.dart's surface) alias the
      caller's instance. NO defensive copy in either language. (3)
      Initialiser-list ordering: Dart guarantees initialiser-list entries
      execute BEFORE the constructor body in source order; C# constructor-
      body assignments preserve that order by writing them in source order
      — the spec mandates the constructor body assigns `ModeTable` FIRST,
      then `Classifier` SECOND. Threading: synchronous, no `async`/`Future`/
      isolate (those well-known nuances correctly absent — US2-AS4).
  - construct_key: dart.list_accumulator.checkprocedure_null_or_empty_modes_shortcircuit_then_addall_per_clause
    source_form: >-
      List<PmtError> checkProcedure(Procedure proc) { final errors =
      <PmtError>[]; final allModes = modeTable.getAllModes(proc.name, proc
      .arity); if (allModes == null || allModes.isEmpty) { return errors; }
      for (final clause in proc.clauses) { errors.addAll(
      checkClauseAgainstModes(clause, allModes)); } return errors; }
    target_decision: >-
      Emit `public List<PmtError> CheckProcedure(Procedure proc) { var errors
      = new List<PmtError>(); var allModes = ModeTable.GetAllModes(proc.Name,
      proc.Arity); if (allModes is null || allModes.Count == 0) return
      errors; foreach (var clause in proc.Clauses) { errors.AddRange(
      CheckClauseAgainstModes(clause, allModes)); } return errors; }`. Dart
      `<PmtError>[]` → C# `new List<PmtError>()` (cached
      `rf-dart-list-typed-literal-and-addall-to-csharp-list-and-addrange`
      from `type_checker.dart.md`). Dart `errors.addAll(other)` → C# `errors
      .AddRange(other)` (Microsoft Learn `List<T>.AddRange`). Dart `for
      (final x in xs)` → C# `foreach (var x in xs)`. The compound short-
      circuit `allModes == null || allModes.isEmpty` becomes `allModes is
      null || allModes.Count == 0` — `is null` is the .NET-idiomatic
      nullable-reference branch (Microsoft Learn "patterns" reference), and
      `.Count == 0` mirrors Dart `isEmpty` per the cached
      `rf-dart-length-isempty-to-csharp-count` finding (Microsoft Learn
      Framework Design Guidelines: `.Count` on `IReadOnlyCollection<T>`,
      `.Length` reserved for arrays/strings). `modeTable.getAllModes` is
      declared (in mode_table.dart) to return `List<List<Mode>>?` — under
      enabled NRT the C# return type is `IReadOnlyList<IReadOnlyList<Mode>>?`
      / `List<List<Mode>>?` per mode_table.dart.md's surface.
    idiom_id: null
    research_finding_id: rf-dart-list-typed-literal-and-addall-to-csharp-list-and-addrange
    nuance: >-
      The combined null-or-empty short-circuit `allModes == null ||
      allModes.isEmpty` is load-bearing: the source's doc-comment "No mode
      declaration — skip checking" mandates SILENT skip on either branch,
      NOT errored. The C# port MUST preserve the silent skip — promoting
      to an error would change observable behaviour under FR-024 / FR-023.
      Short-circuit evaluation order matters: `allModes is null` MUST be
      tested FIRST (Microsoft Learn "Boolean logical operators": "The
      conditional logical operators `||` and `&&` ... evaluate the right-
      hand operand only if necessary") — if the nullable check is moved
      after `Count == 0`, the right-side dereference of a null `allModes`
      would throw `NullReferenceException`. The source-order parallel
      between Dart `||` short-circuit and C# `||` short-circuit is exact:
      both evaluate left-to-right with the same lazy semantics. The empty-
      list allocator inside an early-return arm (`var errors = new
      List<PmtError>(); ... return errors;`) preserves Dart's behaviour
      where each invocation returns a FRESH empty list — codegen MUST
      NOT hoist to a shared static empty (would break caller-mutation
      assumptions even though no current caller mutates the result).
  - construct_key: dart.try_each_alternative_until_success_with_best_error_tracking_and_composite_diagnostic
    source_form: >-
      List<PmtError> checkClauseAgainstModes(Clause clause, List<List<Mode>>
      allModes) { if (allModes.length == 1) { return checkClause(clause,
      allModes.first); } List<PmtError>? bestErrors; for (final modes in
      allModes) { final errors = checkClause(clause, modes); if (errors
      .isEmpty) { return []; } if (bestErrors == null || errors.length <
      bestErrors.length) { bestErrors = errors; } } if (bestErrors != null
      && bestErrors.isNotEmpty) { final modeStrings = allModes.map((m) =>
      '(${m.map((mode) => mode == Mode.reader ? '?' : '').join(', ')})')
      .join(' | '); return [PmtError('Clause does not match any declared
      mode. Available modes: $modeStrings', clause.line, clause.column,),
      ...bestErrors,]; } return bestErrors ?? []; }
    target_decision: >-
      Emit `public List<PmtError> CheckClauseAgainstModes(Clause clause,
      IReadOnlyList<IReadOnlyList<Mode>> allModes) { if (allModes.Count ==
      1) return CheckClause(clause, allModes[0]); List<PmtError>?
      bestErrors = null; foreach (var modes in allModes) { var errors =
      CheckClause(clause, modes); if (errors.Count == 0) return new
      List<PmtError>(); if (bestErrors is null || errors.Count <
      bestErrors.Count) bestErrors = errors; } if (bestErrors is not null
      && bestErrors.Count > 0) { var modeStrings = string.Join(" | ",
      allModes.Select(m => $"({string.Join(", ", m.Select(mode => mode ==
      Mode.Reader ? "?" : ""))})")); var result = new List<PmtError> { new
      PmtError($"Clause does not match any declared mode. Available modes:
      {modeStrings}", clause.Line, clause.Column) }; result.AddRange(
      bestErrors); return result; } return bestErrors ?? new
      List<PmtError>(); }`. Dart `allModes.first` → C# `allModes[0]` (the
      `IReadOnlyList<T>` indexer; Dart `.first` on a `List<T>` is the
      first-element accessor — Microsoft Learn `IReadOnlyList<T>.Item[Int32]`:
      "Gets the element at the specified index"). Dart `.length` /
      `.isEmpty` / `.isNotEmpty` → C# `.Count == 0` / `.Count > 0` /
      `.Count < x` per the cached `rf-dart-length-isempty-to-csharp-count`
      finding. Dart `Iterable.map(...).join(sep)` → C# LINQ `xs.Select(...)`
      composed with `string.Join(sep, xs)` (separator FIRST per the cached
      `rf-dart-list-join-to-csharp-string-join-separator-first` finding
      from `type_checker.dart.md`). The Dart spread-in-list-literal
      `[head, ...bestErrors]` decomposes into a C# `new List<PmtError> {
      head }` initialiser followed by `AddRange(bestErrors)` — the
      collection-initialiser-syntax can express a single seeded element
      cleanly, and the spread of an arbitrary-length sequence is best
      rendered as an explicit `AddRange` to preserve the Dart "head, then
      tail" insertion order verbatim. ALTERNATIVE C# 12 collection
      expression `[head, .. bestErrors]` is REJECTED — the spread-into-
      collection-expression syntax produces an `IEnumerable<PmtError>` /
      `ImmutableArray<PmtError>` depending on target type, and the spec's
      callers expect a mutable `List<PmtError>` for downstream
      `AddRange`-ing; the explicit `new List + AddRange` shape preserves
      type fidelity. Dart `??` (null-coalescing) → C# `??` (verbatim
      operator — Microsoft Learn "?? operator": "returns the value of its
      left-hand operand if it isn't null; otherwise, it evaluates the
      right-hand operand and returns its result"). The `Mode.reader ? '?'
      : ''` ternary uses the PascalCase rename `Mode.Reader` per
      `mode_table.dart.md`'s `Mode` enum spec — cross-file invariant.
    idiom_id: null
    research_finding_id: rf-dart-spread-in-list-literal-to-csharp-list-initializer-plus-addrange
    nuance: >-
      Four load-bearing nuances. (1) Single-mode fast-path (`if (allModes
      .length == 1) return checkClause(clause, allModes.first);`):
      observable optimisation that bypasses the best-error tracking
      entirely. Codegen MUST preserve the fast-path verbatim — promoting
      it to "always use the loop" would change error messages for the
      single-mode case (the composite "Clause does not match any
      declared mode" prefix would never apply because the loop's
      `errors.isEmpty` check on a successful single mode would return
      early; but the COMPOSITE message construction is gated by `allModes
      .length > 1` only in the multi-mode arm of the function — preserving
      the fast-path keeps the textual surface bit-identical). (2) Best-
      error tracking: `if (bestErrors == null || errors.length < bestErrors
      .length) { bestErrors = errors; }` is a "tracking minimum" idiom
      that selects the mode with the FEWEST errors as the user-facing
      diagnostic. C# preserves this verbatim with `bestErrors is null ||
      errors.Count < bestErrors.Count`. The `<` comparison (strict less-
      than, NOT `<=`) means TIES go to the FIRST encountered mode with
      that error count — preserves Dart's left-to-right scan order. (3)
      Spread-in-list-literal nuance: Dart `[head, ...tail]` constructs a
      FRESH list (allocates, then copies head + each tail element).
      Naïvely translating to C# `[head, .. tail]` (collection expression,
      C# 12) ALSO allocates fresh and copies — semantically equivalent
      but produces an inferred type (`List<T>` if target is `List<T>`,
      `T[]` if target is `T[]`, etc.); the spec prefers `new List<T> {
      head }` + `AddRange(tail)` to guarantee `List<T>` regardless of
      target-typing context. (4) Empty-list return on early-success
      `return [];`: Dart `[]` in return position is FRESH allocation
      (typed by inference to `List<PmtError>`); C# port emits `return
      new List<PmtError>()` — codegen MUST NOT substitute `Array.Empty<
      PmtError>()` here because the return type is `List<PmtError>` (not
      `IReadOnlyList<>`) and the caller in `checkProcedure` uses
      `errors.AddRange(...)` which mutates the result list — wait, no:
      the result here is RETURNED to the caller which adds it to its own
      accumulator via `AddRange` (Microsoft Learn: AddRange iterates the
      source, does NOT mutate the source) — so an immutable empty would
      also work, but `new List<PmtError>()` is the source-faithful
      rendering per the cached idiom; consistency with the early-return
      from `checkProcedure` (also `new List<PmtError>()`) preferred.
  - construct_key: dart.srsw_check_loop.classify_then_group_then_extract_grounded_then_writer_reader_count_with_secondary_lookup
    source_form: >-
      List<PmtError> checkClause(Clause clause, List<Mode> modes) { final
      errors = <PmtError>[]; final occurrences = classifier.classifyClause(
      clause, modes); final byVar = groupByVariable(occurrences); final
      groundedVars = _extractGroundedVars(clause); for (final entry in byVar
      .entries) { final varName = entry.key; final occs = entry.value;
      final counts = countOccurrences(occs); if (counts.writers == 0) {
      errors.add(PmtError('Variable $varName has no writer occurrence',
      occs.first.line, occs.first.column,)); } else if (counts.writers > 1)
      { final secondWriter = occs.where((o) => o.type == OccurrenceType
      .writer).skip(1).first; errors.add(PmtError('Variable $varName has
      ${counts.writers} writer occurrences (expected 1)', secondWriter
      .line, secondWriter.column,)); } if (counts.readers == 0) { errors
      .add(PmtError('Variable $varName has no reader occurrence', occs
      .first.line, occs.first.column,)); } else if (counts.readers > 1 &&
      !groundedVars.contains(varName)) { final secondReader = occs.where(
      (o) => o.type == OccurrenceType.reader).skip(1).first; errors.add(
      PmtError('Variable $varName has ${counts.readers} reader occurrences;
      add ground($varName) guard', secondReader.line, secondReader
      .column,)); } } return errors; }
    target_decision: >-
      Emit `public List<PmtError> CheckClause(Clause clause,
      IReadOnlyList<Mode> modes) { var errors = new List<PmtError>(); var
      occurrences = Classifier.ClassifyClause(clause, modes); var byVar =
      OccurrenceExtras.GroupByVariable(occurrences); var groundedVars =
      _ExtractGroundedVars(clause); foreach (var entry in byVar) { var
      varName = entry.Key; var occs = entry.Value; var counts =
      OccurrenceExtras.CountOccurrences(occs); if (counts.Writers == 0) {
      errors.Add(new PmtError($"Variable {varName} has no writer
      occurrence", occs[0].Line, occs[0].Column)); } else if (counts
      .Writers > 1) { var secondWriter = occs.Where(o => o.Type ==
      OccurrenceType.Writer).Skip(1).First(); errors.Add(new PmtError(
      $"Variable {varName} has {counts.Writers} writer occurrences
      (expected 1)", secondWriter.Line, secondWriter.Column)); } if
      (counts.Readers == 0) { errors.Add(new PmtError($"Variable
      {varName} has no reader occurrence", occs[0].Line, occs[0].Column));
      } else if (counts.Readers > 1 && !groundedVars.Contains(varName)) {
      var secondReader = occs.Where(o => o.Type == OccurrenceType.Reader)
      .Skip(1).First(); errors.Add(new PmtError($"Variable {varName} has
      {counts.Readers} reader occurrences; add ground({varName}) guard",
      secondReader.Line, secondReader.Column)); } } return errors; }`.
      `Classifier.ClassifyClause` is the owned helper from construct 1;
      `OccurrenceExtras.GroupByVariable` and `OccurrenceExtras
      .CountOccurrences` are top-level Dart functions hosted on the
      `OccurrenceExtras` static class per occurrence.dart.md's spec
      (cross-file invariant). Dart `.where((o) => predicate).skip(1)
      .first` (an Iterable chain) → C# LINQ `xs.Where(o => predicate)
      .Skip(1).First()` (Microsoft Learn `Enumerable.Where`,
      `Enumerable.Skip`, `Enumerable.First` — direct method-name and
      semantic equivalents). The Dart `Map.entries` iteration → C#
      `foreach (var entry in byVar)` directly iterates the
      `Dictionary<K,V>` yielding `KeyValuePair<K,V>` (Microsoft Learn
      `Dictionary<TKey,TValue>` enumerator: "yields a KeyValuePair<TKey,
      TValue>") — `entry.Key` / `entry.Value` provide the same access
      surface as Dart's `MapEntry`. Counts come from
      `OccurrenceExtras.CountOccurrences` which returns the named C#
      tuple `(int Writers, int Readers)` per occurrence.dart.md — the
      counts are accessed PascalCased (`counts.Writers`, `counts.Readers`)
      mapping Dart `counts.writers` / `counts.readers` camelCase. The
      `occs.first.line` Dart accessor → C# `occs[0].Line` because `occs`
      is the `List<Occurrence>` bucket from `byVar` (per
      occurrence.dart.md's `GroupByVariable` returning `Dictionary<string,
      List<Occurrence>>`) — indexer access is O(1) and matches Dart
      `List.first` semantics for the FIRST element exactly.
    idiom_id: null
    research_finding_id: rf-dart-iterable-where-skip-first-to-csharp-linq-where-skip-first
    nuance: >-
      Three nuances. (1) Iterable-chain laziness: Dart `Iterable.where`
      and `Iterable.skip` are LAZY (dart.dev `Iterable.where`: "Returns
      a new lazy Iterable" / `Iterable.skip`: "Returns an Iterable that
      provides all but the first count elements"); C# LINQ `Where` and
      `Skip` are ALSO LAZY (Microsoft Learn `Enumerable.Where`: "This
      method is implemented by using deferred execution") — semantics
      preserved exactly. The terminal `.first` / `.First()` triggers
      evaluation in both languages. (2) "Second-of-kind" pattern: the
      Dart `where(predicate).skip(1).first` idiom retrieves the SECOND
      element matching the predicate (skipping the FIRST). This is a
      reporting-only optimisation — the user-facing diagnostic points
      at the OFFENDING duplicate writer/reader, not the first
      occurrence (which is by-construction "the legitimate one"). The
      C# port preserves this exact semantic verbatim; codegen MUST NOT
      "helpfully" rewrite to `.First()` (the first matching) or `.Last()`
      (the last matching). (3) Exception parity: both Dart `Iterable
      .first` on an empty iterable and C# `Enumerable.First()` on an
      empty enumerable THROW (Dart: `StateError`; C#:
      `InvalidOperationException`). The source guarantees non-emptiness
      via the surrounding `counts.writers > 1` / `counts.readers > 1`
      check — at least TWO matching elements exist, so `.skip(1).first`
      / `.Skip(1).First()` always succeeds. Codegen MUST NOT introduce
      a defensive `FirstOrDefault` here — that would mask a bug in
      `OccurrenceClassifier` if the counter and the predicate ever
      diverge (a violation of single-source-of-truth semantics
      observable only via a NullReferenceException downstream). The
      `ground($varName)` literal inside the reader-too-many message is
      a CALL-SYNTAX in the diagnostic surface (the suggested guard) —
      the parens-name-parens is observable in user-facing error
      messages and downstream test goldens; the C# port preserves
      `$"ground({varName})"` verbatim.
  - construct_key: dart.guard_scan_with_const_string_set_membership_and_arity_check_then_var_name_recursive_collect
    source_form: >-
      Set<String> _extractGroundedVars(Clause clause) { final grounded =
      <String>{}; if (clause.guards == null) return grounded; const
      typeCheckOps = { 'ground', 'number', 'integer', 'float', 'atom',
      'string', 'list', 'tuple', 'compound', 'var', 'nonvar',
      'is_mutual_ref', 'unknown', }; const comparisonOps = {'<', '>',
      '=<', '>=', '=:=', r'=\=', '=?='}; for (final guard in clause
      .guards!) { if (typeCheckOps.contains(guard.predicate) && guard.args
      .length == 1) { _collectVarNames(guard.args[0], grounded); } if
      (comparisonOps.contains(guard.predicate) && guard.args.length == 2)
      { _collectVarNames(guard.args[0], grounded); _collectVarNames(guard
      .args[1], grounded); } } return grounded; }
    target_decision: >-
      Emit `private HashSet<string> _ExtractGroundedVars(Clause clause) {
      var grounded = new HashSet<string>(StringComparer.Ordinal); if
      (clause.Guards is null) return grounded; foreach (var guard in
      clause.Guards) { if (TypeCheckOps.Contains(guard.Predicate) &&
      guard.Args.Count == 1) { _CollectVarNames(guard.Args[0], grounded);
      } if (ComparisonOps.Contains(guard.Predicate) && guard.Args.Count
      == 2) { _CollectVarNames(guard.Args[0], grounded); _CollectVarNames(
      guard.Args[1], grounded); } } return grounded; }`. The two
      `const Set<String> = { ... }` Dart compile-time-constant string sets
      become two `private static readonly FrozenSet<string>` fields on the
      `PmtChecker` class (per the cached `rf-dart-const-set-to-csharp-
      frozenset-ordinal` finding from `glp_printer.dart.md` /
      `type_ast.dart.md` / `parser.dart.md`), each constructed once via
      `FrozenSet.ToFrozenSet(StringComparer.Ordinal)` (Microsoft Learn
      `System.Collections.Frozen.FrozenSet`: "Provides a set type for
      situations where the set is created once and read repeatedly").
      Concretely: `private static readonly FrozenSet<string> TypeCheckOps
      = new[] { "ground", "number", "integer", "float", "atom", "string",
      "list", "tuple", "compound", "var", "nonvar", "is_mutual_ref",
      "unknown" }.ToFrozenSet(StringComparer.Ordinal);` and `private
      static readonly FrozenSet<string> ComparisonOps = new[] { "<", ">",
      "=<", ">=", "=:=", "=\\=", "=?=" }.ToFrozenSet(StringComparer
      .Ordinal);`. The Dart raw string `r'=\='` (raw string literal with
      embedded backslash; dart.dev "Strings — raw strings": "you can
      create a 'raw' string by prefixing it with `r` — backslashes are
      treated as literal characters") becomes the C# verbatim string
      `"=\\="` (a regular C# string with an escaped backslash; the
      verbatim-string equivalent `@"=\="` would also work but the
      project precedent is escaped-backslash form). The empty `<String>{}`
      Dart set literal returned on null-guards is mapped to a fresh
      `new HashSet<string>(StringComparer.Ordinal)` (per cached
      `rf-dart-set-literal-to-csharp-hashset` from boot_loader.dart;
      `StringComparer.Ordinal` from the project-wide string-key-ordinality
      discipline). The accumulator is `HashSet<string>` (mutable, NOT
      `FrozenSet`) because `_CollectVarNames` mutates it via `.Add`.
    idiom_id: rf-dart-const-set-to-csharp-frozenset-ordinal
    research_finding_id: rf-dart-const-set-to-csharp-frozenset-ordinal
    nuance: >-
      Five load-bearing nuances. (1) `const` Dart set → static readonly
      C# field: Dart `const { ... }` is a compile-time constant; .NET
      has no compile-time-constant set type — the canonical C# rendering
      is a single shared `static readonly` allocated at type initialisation
      (Microsoft Learn "Static constructors": "A static constructor is
      used to initialize any static data, or to perform a particular
      action that needs to be performed only once"). The two FrozenSets
      are initialised lazily by the runtime at first access to the
      `PmtChecker` type — exactly the once-only allocation semantics of
      Dart `const`. (2) Ordinal string comparer is MANDATORY (project-
      wide discipline established in `mode_table.dart.md`,
      `well_typed_term.dart.md`, parser.dart, type_ast.dart) — Dart
      `Set<String>.contains` is byte-exact (Dart String operates on code
      units); the C# default `StringComparer<string>` is also ordinal for
      `string` keys per `HashSet<string>`'s default comparer (Microsoft
      Learn `HashSet<T>` constructor — defaults to `EqualityComparer<T>
      .Default`, which for `string` is ordinal) BUT the spec MANDATES
      EXPLICIT `StringComparer.Ordinal` for visibility/reviewer-parity.
      (3) Raw string nuance: the SINGLE non-trivial entry `r'=\='`
      contains a literal backslash — Dart raw-string disables escape-
      processing; C# string literal `"=\\="` uses escape-processing to
      include the same literal backslash. Both produce the same in-
      memory string `=\=` (three chars: `=`, `\`, `=`). Codegen MUST
      verify this — substituting `"=\="` (unescaped) would produce a
      compile error or a different character sequence (depending on
      context). (4) The `clause.guards == null` check followed by
      `clause.guards!` dereference is the same nullable-flow pattern
      as `occurrence.dart.md`'s `classifyClause`; per the cached
      `rf-dart-nullable-bang-inside-null-check-to-csharp-flow-narrowed-
      access` finding, the C# port drops the `!` because flow-narrowing
      removes the need (Microsoft Learn "Nullable reference types
      — flow analysis"). (5) The two `if` branches are NOT
      `else if`-chained — both can match in principle, but in practice
      the two key sets `typeCheckOps` and `comparisonOps` are DISJOINT
      (manual verification: no string appears in both sets), so the
      sequential-if shape is semantically equivalent to else-if. The
      spec preserves the source's sequential-if shape verbatim to keep
      the textual diff with the Dart source minimal. The two FrozenSet
      fields are declared `private` because they are implementation
      detail of `_ExtractGroundedVars` only; no other code in the
      `PmtChecker` class or in the file references them. Naming: Dart
      local-`const` identifier `typeCheckOps` → C# static field
      `TypeCheckOps` PascalCased; `comparisonOps` → `ComparisonOps`.
  - construct_key: dart.recursive_visitor.collect_var_names_into_string_set_accumulator_with_term_subtype_dispatch_and_listterm_head_tail_null_check
    source_form: >-
      void _collectVarNames(Term term, Set<String> out) { if (term is
      VarTerm) { out.add(term.name); } else if (term is StructTerm) { for
      (final arg in term.args) { _collectVarNames(arg, out); } } else if
      (term is ListTerm) { if (term.head != null) _collectVarNames(term
      .head!, out); if (term.tail != null) _collectVarNames(term.tail!,
      out); } }
    target_decision: >-
      Emit `private void _CollectVarNames(Term term, HashSet<string> @out)
      { if (term is VarTerm varTerm) { @out.Add(varTerm.Name); } else if
      (term is StructTerm structTerm) { foreach (var arg in structTerm
      .Args) { _CollectVarNames(arg, @out); } } else if (term is ListTerm
      listTerm) { if (listTerm.Head is not null) _CollectVarNames(listTerm
      .Head, @out); if (listTerm.Tail is not null) _CollectVarNames(
      listTerm.Tail, @out); } }`. Structurally identical to
      `occurrence.dart`'s `_collectVariables` (different accumulator type:
      `Set<String>` vs `List<Occurrence>`; different VarTerm action: no
      underscore-prefix skip here, no isReader ternary, no Occurrence
      construction — just `Add(name)`). The Dart `is` type-test chain →
      C# declaration-pattern matching with typed locals (`varTerm`,
      `structTerm`, `listTerm`) per the cached `rf-dart-is-test-smart-
      cast-to-csharp-declaration-pattern` from `type_checker.dart.md`
      (and the structurally identical
      `rf-dart-is-type-test-chain-to-csharp-pattern-switch` from
      `occurrence.dart.md`). The Dart `term.head!` / `term.tail!`
      bang force-unwraps INSIDE the `if (term.head != null)` /
      `if (term.tail != null)` guards drop in the C# port — flow-
      narrowing applies (cached `rf-dart-nullable-bang-inside-null-
      check-to-csharp-flow-narrowed-access`). Parameter identifier `out`
      is renamed to `@out` per `occurrence.dart.md`'s precedent (C#
      reserved keyword; verbatim-identifier prefix preserves source
      surface under FR-023). The accumulator type `HashSet<string>` is
      paired with the caller's `new HashSet<string>(StringComparer
      .Ordinal)` in `_ExtractGroundedVars` (construct 5) — the same
      reference is passed in; mutation visible to caller. The trailing
      comment `// ConstTerm, UnderscoreTerm — no variables` from the
      source MUST be preserved in the C# port verbatim (`// ConstTerm,
      UnderscoreTerm — no variables`) — the comment documents the
      EXHAUSTIVENESS of the chain over the variable-bearing Term
      subtypes, important for reviewer comprehension and downstream
      maintenance under FR-023.
    idiom_id: rf-dart-is-type-test-chain-to-csharp-pattern-switch
    research_finding_id: rf-dart-is-type-test-chain-to-csharp-pattern-switch
    nuance: >-
      Three load-bearing nuances, all cached from `occurrence.dart.md`'s
      structurally-equivalent `_collectVariables`. (1) `is`-test chain
      with smart-cast: Dart narrows the static type of `term` inside each
      `is` branch (Dart language doc, type promotion); C# requires the
      declaration-pattern-with-binding (`term is VarTerm varTerm`) to get
      the same narrowed access. Codegen MUST emit the binding where the
      branch body accesses a subtype-specific member (`.Name`, `.Args`,
      `.Head`, `.Tail`). The VarTerm branch's `varTerm.Name` access
      requires the binding; the StructTerm branch's `structTerm.Args` and
      ListTerm branch's `listTerm.Head` / `listTerm.Tail` likewise. (2)
      Null-bang elision via flow-narrowing: Dart `if (term.head != null)
      _collectVarNames(term.head!, out);` uses the bang `!` to satisfy
      the analyser despite the surrounding null check (Dart fields don't
      type-promote; dart.dev/null-safety/understanding-null-safety#type-
      promotion-on-fields). C# nullable-flow analysis recognises
      `if (listTerm.Head is not null) ...` inside the branch and narrows
      `listTerm.Head` to non-null (Microsoft Learn "Nullable reference
      types"); the bang disappears. (3) Accumulator-by-reference: both
      Dart `Set<String> out` and C# `HashSet<string> @out` are reference
      types passed BY REFERENCE-VALUE (the parameter holds a copy of the
      reference; mutations to the SET state are visible to the caller).
      NO `ref` keyword needed and NO defensive copy. The terminal `Add`
      is idempotent on duplicate keys (Microsoft Learn `HashSet<T>.Add`:
      "Returns: true if the element is added to the HashSet<T> object;
      false if the element is already present"); the spec ignores the
      return value, same as Dart `Set<String>.add` (dart.dev `Set.add`:
      "Returns true if value (or an equal value) was not yet in the
      set"). Variable identifier skip vs no skip nuance: occurrence
      .dart's `_collectVariables` skips VarTerm names starting with `_`
      (anonymous-variable exemption per Section 9 of typed-glp-manual);
      THIS checker.dart's `_collectVarNames` has NO such skip — it
      collects EVERY variable name, including underscore-prefixed
      anonymous variables. The difference is INTENTIONAL: occurrence.dart
      classifies occurrences for SRSW counting (where anonymous `_` is
      explicitly exempt); checker.dart's `_extractGroundedVars` collects
      names from GUARDS where every named variable IS to be considered
      grounded. Codegen MUST preserve the difference verbatim — adding
      an underscore-skip in the C# port (by analogy to occurrence.dart)
      would change observable behaviour.
conversion_units:
  - "class PmtChecker (non-sealed; two get-only public auto-properties ModeTable / Classifier initialised in constructor body; positional ctor PmtChecker(ModeTable modeTable) assigns ModeTable = modeTable THEN Classifier = new OccurrenceClassifier(modeTable) — source-order initialisation preserved; NO IEquatable<>; NO sealed modifier)"
  - "method CheckProcedure(Procedure proc) -> List<PmtError> (fresh new List<PmtError>(); calls ModeTable.GetAllModes; null-or-empty short-circuit `allModes is null || allModes.Count == 0` with `is null` FIRST for short-circuit safety; foreach proc.Clauses dispatching to CheckClauseAgainstModes via AddRange; silent skip semantics preserved verbatim)"
  - "method CheckClauseAgainstModes(Clause clause, IReadOnlyList<IReadOnlyList<Mode>> allModes) -> List<PmtError> (single-mode fast-path via allModes[0]; multi-mode loop tracking minimum-error count with strict `<` comparison left-to-right tie-break; composite diagnostic via string.Join over LINQ Select with PascalCased Mode.Reader sentinel emitting '?' vs ''; spread-in-list-literal `[head, ...bestErrors]` expanded to `new List<PmtError> { head }` + AddRange(bestErrors); `??` null-coalescing operator verbatim)"
  - "method CheckClause(Clause clause, IReadOnlyList<Mode> modes) -> List<PmtError> (Classifier.ClassifyClause + OccurrenceExtras.GroupByVariable + _ExtractGroundedVars + foreach byVar yielding KeyValuePair; OccurrenceExtras.CountOccurrences returning named tuple (Writers,Readers); writer-count branches: 0→no-writer-error using occs[0].Line/Column, >1→second-writer error via .Where(o => o.Type == OccurrenceType.Writer).Skip(1).First(); reader-count branches: 0→no-reader-error, >1 AND !groundedVars.Contains(varName)→second-reader error via .Where(o => o.Type == OccurrenceType.Reader).Skip(1).First(); error messages preserved verbatim including the `ground({varName})` parens-name-parens call-syntax suggestion)"
  - "private method _ExtractGroundedVars(Clause clause) -> HashSet<string> (fresh new HashSet<string>(StringComparer.Ordinal); null-guards short-circuit; sequential-if (NOT else-if) over the two disjoint FrozenSet membership tests with arity check; recursive _CollectVarNames into the accumulator)"
  - "private static readonly FrozenSet<string> TypeCheckOps = new[] { 'ground','number','integer','float','atom','string','list','tuple','compound','var','nonvar','is_mutual_ref','unknown' }.ToFrozenSet(StringComparer.Ordinal) (cached idiom from glp_printer.dart / parser.dart / type_ast.dart)"
  - "private static readonly FrozenSet<string> ComparisonOps = new[] { '<','>','=<','>=','=:=','=\\\\=','=?=' }.ToFrozenSet(StringComparer.Ordinal) (raw-string r'=\\=' Dart → escaped-backslash '=\\\\=' C#; literal three-character key `=\\=` preserved byte-exact)"
  - "private method _CollectVarNames(Term term, HashSet<string> @out) (declaration-pattern matching is-chain over VarTerm/StructTerm/ListTerm subtypes; VarTerm adds varTerm.Name directly with NO underscore-prefix skip — intentional divergence from occurrence.dart._collectVariables; StructTerm foreach Args recurse; ListTerm flow-narrowed Head/Tail null-checks with bang elided; trailing `// ConstTerm, UnderscoreTerm — no variables` comment preserved verbatim; accumulator mutated in place; `out` identifier verbatim-prefixed as `@out` per FR-023)"
escalations: []
```

## Rationale and research provenance (per non-trivial construct)

All six non-trivial constructs in this file are resolved against rf-* findings
from the prior 018 convspec corpus (FR-024: never re-research; cached findings
reused verbatim), with ONE fresh rf-* finding recorded for the
initialiser-list-owned-helper pattern and ONE fresh rf-* finding for the
spread-in-list-literal pattern. Every construct records BOTH a deep-analysis
basis AND a researched-pattern basis per SC-006 / US2-AS4. Zero escalations.

### rf-dart-initialiser-list-owned-helper-to-csharp-ctor-body-assignment (NEW)

- Deep analysis: `PmtChecker` has TWO `final` fields — `modeTable` (injected
  dependency) and `classifier` (owned helper computed FROM `modeTable`). The
  Dart constructor uses an initialiser-list `: classifier = OccurrenceClassifier
  (modeTable)` to construct the owned helper before the body runs. This is a
  distinct pattern from `TypeChecker` in `type_checker.dart.md` (TWO injected
  deps, no owned helper) and `OccurrenceClassifier` in `occurrence.dart.md`
  (ONE injected dep, no owned helper). Recording a fresh rf-* finding here
  documents the canonical C# rendering for the "injected-plus-owned" shape
  that recurs elsewhere in the codebase.
- Authoritative Dart (WebFetch
  https://dart.dev/language/constructors#initializer-list): dart.dev official
  — "In addition to invoking a superclass constructor, you can also initialize
  instance variables before the constructor body runs. Separate initializers
  with commas. … The right-hand side of an initializer doesn't have access to
  `this`." Decisive: the initialiser-list expression `OccurrenceClassifier
  (modeTable)` accesses the PARAMETER `modeTable` (not `this.modeTable`),
  which is why the C# port can mirror it with a constructor-body assignment
  that also accesses the parameter directly (NOT the property — which at that
  point in the body has just been assigned).
- Authoritative .NET (WebFetch
  https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-
  and-structs/constructors): Microsoft Learn — "Instance constructors are
  used to create and initialize any instance member variables when you use
  the `new` expression to create an object of a class. … A static field
  initializer cannot reference a non-static member of the class. … Field
  initializers run before the constructor body, but they cannot reference
  instance state or constructor parameters." Decisive: the C# field
  initialiser CANNOT see the constructor parameter, so the assignment MUST
  live in the constructor body. The body's assignment order matches the
  Dart initialiser-list order: `ModeTable = modeTable;` FIRST, then
  `Classifier = new OccurrenceClassifier(modeTable);` SECOND.
- Authoritative both sides; recorded as a NEW finding. Composes with the
  cached `rf-csharp-class-with-readonly-injected-dep-and-private-helpers`
  from `occurrence.dart.md` (single-dep variant) — the new finding extends
  it to the "two fields, one injected, one constructed FROM the injected
  parameter" shape.

### rf-dart-list-typed-literal-and-addall-to-csharp-list-and-addrange (CACHED, reused from type_checker.dart)

- Deep analysis: `CheckProcedure`'s `<PmtError>[]` typed-empty-list literal
  is the same shape as `type_checker.dart`'s `CheckModule` accumulator;
  `errors.addAll(checkClauseAgainstModes(...))` per iteration is the same
  pattern. The cached finding from `type_checker.dart.md` applies verbatim.
  The null-or-empty short-circuit `allModes == null || allModes.isEmpty`
  composes the cached `rf-dart-length-isempty-to-csharp-count` finding (from
  `mode_table.dart.md`) — `.isEmpty` → `.Count == 0`.
- Authoritative Dart and .NET: cached from `type_checker.dart.md` and
  `mode_table.dart.md`; no re-research per FR-024.
- Authoritative both sides; cached. Confirms the SC-007 "≥95% recurring
  constructs resolved via a recorded idiom" target — this construct shape
  now appears in three corpus files (type_checker.dart, occurrence.dart's
  `ClassifyClause`, checker.dart's `CheckProcedure`).

### rf-dart-spread-in-list-literal-to-csharp-list-initializer-plus-addrange (NEW)

- Deep analysis: `CheckClauseAgainstModes` constructs the composite-error
  return list using a Dart spread-in-list-literal `[PmtError(...),
  ...bestErrors,]` — one explicit head element followed by spreading the
  variable-length tail. This is the first appearance of Dart's spread
  operator inside a list literal in the 018 corpus — recording a fresh
  finding here documents the canonical C# rendering.
- Authoritative Dart (WebFetch
  https://dart.dev/language/collections#spread-operators): dart.dev official
  — "Dart supports the spread operator (`...`) and the null-aware spread
  operator (`...?`) in list, map, and set literals. Spread operators provide
  a concise way to insert multiple values into a collection. For example,
  you can use the spread operator (`...`) to insert all the values of a
  list into another list." Decisive: the spread allocates a FRESH list and
  copies head + each tail element in source order.
- Authoritative .NET (WebFetch
  https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/
  operators/collection-expressions): Microsoft Learn, "Collection
  expressions" (C# 12) — "A collection expression is a concise syntax to
  create a collection value. … The `..` spread operator includes the
  elements of the spread operand in the collection expression." So the
  literal one-to-one C# 12 mapping is `[new PmtError(...), .. bestErrors]`.
  However, collection expressions are target-typed — the result type
  depends on context (`List<T>` if assignable to `List<T>`, `T[]` if to
  `T[]`, `ImmutableArray<T>` if to that). The spec REJECTS the literal
  one-to-one mapping in favour of the explicit `new List<PmtError> { head
  }; result.AddRange(bestErrors); return result;` shape for two reasons:
  (a) guaranteed `List<PmtError>` return type regardless of inference
  context; (b) explicit ordering — head FIRST, AddRange SECOND — preserves
  the Dart spread's documented insertion semantics verbatim.
- Authoritative both sides; recorded as a NEW finding. The rendering is
  decisive: explicit `new List<T> { head }` collection initialiser followed
  by `AddRange(tail)` mirrors the Dart spread's "head, then each tail
  element" semantics with no surprises under target-typing.

### rf-dart-iterable-where-skip-first-to-csharp-linq-where-skip-first (NEW)

- Deep analysis: `CheckClause`'s "second-of-kind" pattern `occs.where(...)
  .skip(1).first` retrieves the SECOND element matching a predicate. This
  is the first appearance of this specific chain in the 018 corpus —
  recording a fresh finding documents the canonical C# rendering. The
  related single-step chains (`.where(...).first`, `.firstOrNull`, etc.)
  ARE documented in prior specs (`type_checker.dart.md`'s
  `rf-dart-wheretype-firstornull-chain-to-csharp-oftype-firstordefault`);
  this finding extends them with the `.Skip(1)` step.
- Authoritative Dart (WebFetch https://api.dart.dev/dart-core/Iterable-
  class.html): Dart core API — `Iterable.where`: "Returns a new lazy
  `Iterable` with all elements that satisfy the predicate `test`."
  `Iterable.skip`: "Returns an `Iterable` that provides all but the first
  `count` elements." `Iterable.first`: "Returns the first element. Throws
  a `StateError` if `this` is empty."
- Authoritative .NET (WebFetch
  https://learn.microsoft.com/en-us/dotnet/api/system.linq.enumerable):
  Microsoft Learn — `Enumerable.Where`: "Filters a sequence of values
  based on a predicate." `Enumerable.Skip`: "Bypasses a specified number
  of elements in a sequence and then returns the remaining elements."
  `Enumerable.First()`: "Returns the first element of a sequence. … Throws
  an `InvalidOperationException` if the source sequence is empty." All
  three are lazy / deferred-execution methods; the terminal `.First()`
  triggers evaluation.
- Authoritative both sides; recorded as a NEW finding. Method-name and
  semantic mapping is verbatim: `.where(p)` → `.Where(p)`, `.skip(n)` →
  `.Skip(n)`, `.first` → `.First()`. Exception parity: both throw on
  empty (Dart `StateError`, C# `InvalidOperationException`). The source
  guarantees non-emptiness via the surrounding count check (`counts
  .writers > 1` / `counts.readers > 1` ⇒ at least 2 matching elements),
  so `.Skip(1).First()` always succeeds — no defensive `FirstOrDefault`.

### rf-dart-const-set-to-csharp-frozenset-ordinal (CACHED, reused from glp_printer.dart / parser.dart / type_ast.dart / program_dfa.dart)

- Deep analysis: `_extractGroundedVars` uses two `const Set<String> = { ... }`
  string sets (`typeCheckOps` 13 keys, `comparisonOps` 7 keys) for predicate-
  name membership tests. The cached idiom from `glp_printer.dart.md` /
  `type_ast.dart.md` applies verbatim: `private static readonly FrozenSet
  <string>` initialised once at type initialisation with `StringComparer
  .Ordinal`.
- Authoritative Dart and .NET: cached from the prior corpus files; no
  re-research per FR-024. Microsoft Learn `System.Collections.Frozen
  .FrozenSet`: "Provides a set type for situations where the set is created
  once and read repeatedly" — decisive on the rendering. Project-wide
  string-key-ordinality discipline mandates explicit `StringComparer
  .Ordinal`.
- Authoritative both sides; cached. The raw-string `r'=\='` Dart literal is
  a SUBTLE point: the `r` prefix disables backslash-escape-processing, so
  the literal is the three-character sequence `=\=`. The C# string literal
  `"=\\="` uses escape-processing to encode the same three characters. Both
  produce the same in-memory string; codegen MUST verify the byte-exact
  preservation (no `"=\="` — that would either fail to compile or encode a
  different sequence depending on whether the next character forms a valid
  escape).

### rf-dart-set-literal-to-csharp-hashset (CACHED, reused from boot_loader.dart / param_expansion.dart)

- Deep analysis: the `_extractGroundedVars` accumulator `<String>{}` is a
  typed empty mutable set. The cached idiom from `boot_loader.dart.md` /
  `param_expansion.dart.md` applies verbatim: `new HashSet<string>
  (StringComparer.Ordinal)`. The accumulator is MUTABLE (mutated by
  `_collectVarNames` via `.Add`) — distinct from the FrozenSet pattern
  above which is read-only / once-allocated.
- Authoritative Dart and .NET: cached; no re-research per FR-024.
- Authoritative both sides; cached. Composes with the FrozenSet finding
  to give the full set-construct landscape: compile-time-constant sets →
  FrozenSet, runtime-mutable sets → HashSet, both with `StringComparer
  .Ordinal`.

### rf-dart-is-type-test-chain-to-csharp-pattern-switch (CACHED, reused from occurrence.dart)

- Deep analysis: `_collectVarNames` is STRUCTURALLY IDENTICAL to
  `occurrence.dart`'s `_collectVariables` — same `is`-chain dispatch over
  VarTerm / StructTerm / ListTerm subtypes, same ListTerm head/tail null-
  check pattern. The differences are SEMANTIC (accumulator type: `Set
  <String>` vs `List<Occurrence>`; VarTerm action: `out.add(term.name)`
  vs Occurrence construction with isReader ternary; underscore-skip:
  absent vs present). The cached idiom applies verbatim for the structural
  rendering; the semantic differences are captured in the construct's
  target_decision and nuance.
- Authoritative Dart and .NET: cached from `occurrence.dart.md`; no
  re-research per FR-024.
- Authoritative both sides; cached. The intentional divergence on
  underscore-skip (occurrence.dart skips `_`-prefixed names per
  typed-glp-manual §9; checker.dart does NOT skip, because guards bind
  every named variable they reference) is preserved verbatim.

### rf-dart-nullable-bang-inside-null-check-to-csharp-flow-narrowed-access (CACHED, reused from type_checker.dart / occurrence.dart)

- Deep analysis: TWO uses in this file — `clause.guards!` after `if
  (clause.guards == null) return grounded;` (`_extractGroundedVars`),
  and `term.head!` / `term.tail!` after `if (term.head != null)` / `if
  (term.tail != null)` (`_collectVarNames`). The cached finding from
  `occurrence.dart.md` and `type_checker.dart.md` applies verbatim: the
  C# port drops the `!` because flow-narrowing applies inside the guard
  block.
- Authoritative Dart and .NET: cached; no re-research per FR-024.
- Authoritative both sides; cached. The conversion IMPROVES the surface
  (no `!` on the C# side) while preserving exact semantics.

## Notes

- Cross-file invariants relied on by this spec (NOT respecified here):
  - `Mode` enum from `mode_table.dart.md` — PascalCased `Mode.Reader`
    (with `Reader` as the single non-default enum member used here);
    cross-file invariant.
  - `ModeTable` reference type from `mode_table.dart.md` — `GetAllModes
    (string name, int arity)` returning `List<List<Mode>>?` /
    `IReadOnlyList<IReadOnlyList<Mode>>?` under enabled NRT; `Mode`
    construction surface fixed.
  - `OccurrenceClassifier` reference type from `occurrence.dart.md` —
    `ClassifyClause(Clause, IReadOnlyList<Mode>) -> List<Occurrence>`;
    public get-only `ModeTable` property; positional ctor.
  - `OccurrenceExtras` static host class from `occurrence.dart.md` —
    `GroupByVariable(IReadOnlyList<Occurrence>) -> Dictionary<string,
    List<Occurrence>>` (Ordinal); `CountOccurrences(IReadOnlyList
    <Occurrence>) -> (int Writers, int Readers)` named value tuple.
  - `PmtError` value class from `errors.dart.md` — sealed, `IEquatable
    <PmtError>`, get-only Message/Line/Column properties, positional
    ctor `PmtError(string message, int line, int column)`.
  - AST types from `lib/compiler/ast.dart`: `Procedure` (with Name,
    Arity, Clauses), `Clause` (with Head, Body, Guards, Line, Column),
    `Goal` (with Args, Predicate), `Term` and its subtypes (`VarTerm`
    with Name, `StructTerm` with Args, `ListTerm` with Head / Tail —
    both nullable, `ConstTerm`, `UnderscoreTerm`) — cross-file surface
    fixed by ast.dart's conversion-spec.
- No async / Stream / Future, no isolates, no late, no inheritance
  among PmtChecker — the file is purely synchronous, single-class. The
  well-known nuances (value-vs-reference, async, isolates, null-safety,
  enum casing) are addressed explicitly per construct above (US2-AS4).
- The `PmtChecker` constructor's owned-helper pattern (initialiser-list
  in Dart → constructor-body assignment in C#) is the FIRST documented
  appearance in this corpus of the "field B computed FROM constructor
  parameter, assigned in initialiser-list" pattern — recorded as
  `rf-dart-initialiser-list-owned-helper-to-csharp-ctor-body-assignment`.
  Composes with the prior `rf-csharp-class-with-readonly-injected-dep-
  and-private-helpers` (single-dep variant) for full coverage of the
  coordinator-with-deps landscape.
- The spread-in-list-literal pattern in `CheckClauseAgainstModes`
  (`[head, ...bestErrors]`) is the FIRST documented appearance in this
  corpus of Dart's spread operator — recorded as `rf-dart-spread-in-list-
  literal-to-csharp-list-initializer-plus-addrange`. The spec REJECTS
  the literal one-to-one C# 12 collection-expression spread `[head, ..
  bestErrors]` in favour of the explicit `new List<T> { head }` +
  `AddRange(tail)` shape for guaranteed `List<T>` return type and
  reviewer-clarity.
- The `where(p).skip(1).first` "second-of-kind" chain in `CheckClause`
  is the FIRST documented appearance in this corpus — recorded as
  `rf-dart-iterable-where-skip-first-to-csharp-linq-where-skip-first`.
  Composes with the cached single-step `.where(p).first` / `.firstOrNull`
  findings from `type_checker.dart.md` for full coverage of the
  iterable-chain landscape.
- The two `const Set<String>` declarations move from method-local
  positions to `private static readonly FrozenSet<string>` class-level
  fields per the cached idiom — `TypeCheckOps` and `ComparisonOps`.
  Field placement is `private` (implementation detail of
  `_ExtractGroundedVars` only); no other code references them. Field
  ordering: `TypeCheckOps` FIRST, `ComparisonOps` SECOND, mirroring
  the source's declaration order.
- The raw-string `r'=\='` Dart literal is the single non-trivial entry
  in `comparisonOps` — the literal three-character string `=\=`. The
  C# escaped-backslash rendering `"=\\="` preserves it byte-exact.
  Alternative verbatim-string `@"=\="` works equivalently; the spec
  prefers the escaped-backslash form for consistency with project
  precedent.
- Three FRESH rf-* findings recorded
  (`rf-dart-initialiser-list-owned-helper-to-csharp-ctor-body-assignment`,
  `rf-dart-spread-in-list-literal-to-csharp-list-initializer-plus-addrange`,
  `rf-dart-iterable-where-skip-first-to-csharp-linq-where-skip-first`).
  Four CACHED rf-* findings reused
  (`rf-dart-list-typed-literal-and-addall-to-csharp-list-and-addrange`,
  `rf-dart-const-set-to-csharp-frozenset-ordinal`,
  `rf-dart-set-literal-to-csharp-hashset`,
  `rf-dart-is-type-test-chain-to-csharp-pattern-switch`,
  `rf-dart-nullable-bang-inside-null-check-to-csharp-flow-narrowed-access`).
  Each grounded in authoritative Microsoft Learn + dart.dev sources
  (deep-analysis basis AND researched-pattern basis per SC-006 / US2-AS4).
- Zero escalations: every non-trivial construct resolved from
  authoritative Dart and/or .NET official documentation, with deep-
  analysis AND researched-pattern bases recorded (SC-006); recurring
  constructs route through cached rf-* findings (SC-007).
