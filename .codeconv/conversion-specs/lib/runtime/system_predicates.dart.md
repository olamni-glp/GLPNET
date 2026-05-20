# Conversion Spec — lib/runtime/system_predicates.dart

> Conversion-spec artifact for lib/runtime/system_predicates.dart (FR-011).
> Spec-only (FR-023): describes the Dart→C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.
>
> System-predicate execution infrastructure for GLP — the registry +
> call-context types that back the `execute` bytecode opcode. The file
> declares (1) one tag-only enum `SystemResult { success, failure,
> suspend }`, (2) one mutable call-context class `SystemCall` with two
> `final` fields plus an inline-initialised `final Set<int>` and one
> read-only auto-property, (3) one function-type `typedef
> SystemPredicate = SystemResult Function(GlpRuntime, SystemCall)`, and
> (4) one registry class `SystemPredicateRegistry` keyed by predicate
> name (NOT name/arity as in body_kernels — name-only here) with
> `register` / `lookup` / `has` / `names` surface.
>
> The load-bearing nuances exercised by THIS file are: (a) Dart
> `typedef` of a function type → C# named `delegate` (carry-forward
> from body_kernels.dart.md — named-type identity required because
> the typedef name appears in the registry signature
> `Map<String, SystemPredicate>`); (b) Dart `Map<String, X>` →
> C# `Dictionary<string, X>` with the Map-indexer DIVERGENCE handled
> via `TryGetValue` (carry-forward from body_kernels.dart.md /
> suspension.dart.md / cells.dart.md — Dart indexer returns null,
> C# indexer throws); (c) Dart plain enum → C# enum with PascalCased
> tag names (carry-forward from machine_state.dart.md / cells.dart.md
> / body_kernels.dart.md); (d) Dart `Set<int>` initialised inline to
> `{}` + mutated by the predicate-callee → C# `HashSet<long>`
> initialised inline + exposed as a mutable get-only property (the
> set ITSELF is `final` rebind-final but contents-mutable, identical
> "rebind-final body-mutable" semantics to Dart `final List<T> args =
> []` from external_io.dart.md); (e) Dart `Iterable<String> get
> names => _predicates.keys` → C# `IEnumerable<string> Names =>
> _predicates.Keys` (carry-forward from body_kernels.dart.md). NO
> `async`/`await`/`Future`/`Stream`/`Isolate` surface; the file does
> NOT exercise the Stream → IAsyncEnumerable nuance (correctly NOT
> asserted, per the external_io.dart.md / boot_loader.dart.md
> discipline). NO `dart:io` API surface either; the doc-comment
> mention of "I/O operations (file, terminal, network)" describes
> downstream predicate IMPLEMENTATIONS, not THIS file (whose surface
> is purely the registry + call-context plumbing).

```yaml
schema_version: 1
source_path: lib/runtime/system_predicates.dart
source_sha256: ec6e1f4d6555f57c8b7450418b64282524e86e8b2ba6d06323047da3c7a64b05
target_code_unit: lib/runtime/system_predicates.cs
constructs:
  - construct_key: dart.import_directive.package_internal_to_using_namespace
    source_form: >-
      "Two relative imports: `import 'runtime.dart';` (brings
      `GlpRuntime` — the runtime façade referenced by the
      `SystemPredicate` typedef's first parameter) and
      `import 'terms.dart';` (commented in the doc as the source of
      `Term`/`VarRef`/`ConstTerm`/`StructTerm`, transitively
      relevant to predicate IMPLEMENTATIONS but only structurally
      used here as a sibling-file dependency). No `show`/`hide`
      narrowing."
    target_decision: >-
      Each Dart relative import becomes a .NET `using` directive
      naming the namespace of the corresponding converted file:
      a single `using <root>.Runtime;` covers both sibling-file
      imports (`runtime.cs` and `terms.cs` both target the same
      `lib/runtime/` namespace). Carry-forward of the idiom
      recorded in external_io.dart.md / heap_fcp.dart.md.
    idiom_id: null
    research_finding_id: rf-dart-import-relative-to-csharp-using-namespace
    nuance: >-
      Compilation-unit nuance: Dart resolves package imports by
      URI; .NET resolves type references by assembly + namespace.
      The `show` allow-list has no parallel — does not arise
      here. No value-vs-reference, null-safety, or async surface
      implicated by import directives. FR-024 cache hit on the
      carry-forward idiom — do NOT re-research.

  - construct_key: dart.enum.three_member_marker_tag_acronymed_members
    source_form: >-
      "enum SystemResult { success, failure, suspend } — a plain
      Dart enum with three tag-only members (no constructor, no
      fields, no methods, no associated values). Each tag carries
      a triple-slash doc comment ('Predicate succeeded - continue
      execution' / 'Predicate failed - try next clause or fail
      goal' / 'Predicate suspended - waiting for readers to be
      bound')."
    target_decision: >-
      Emit a C# `public enum SystemResult { Success, Failure,
      Suspend }` in the same namespace as the converted file.
      Three enum members PascalCased (Dart `success`/`failure`/
      `suspend` → C# `Success`/`Failure`/`Suspend`) — faithful
      one-to-one to the Dart shape. No backing-type override
      (default `int` is correct — the enum has only three tags
      and no code observes ordinals). NO `record`, NO
      sealed-class discriminated-union — the enum is genuinely
      payload-free. The three triple-slash doc comments are
      preserved verbatim as C# XML-doc comments
      (`/// <summary>Predicate succeeded - continue execution
      </summary>` etc.). The hyphens in the doc-comment text
      (`-`) are preserved byte-identically — they are prose, not
      typographic en-dashes.
    idiom_id: null
    research_finding_id: rf-dart-enum-plain-to-csharp-enum
    nuance: >-
      Plain-enum nuance (carry-forward idiom from machine_state
      .dart.md / cells.dart.md / body_kernels.dart.md / heap_fcp
      .dart.md): a payload-free Dart `enum { a, b, c }` is
      shape-identical to a C# `enum { A, B, C }`. The faithful
      render preserves: declaration order (so default-assigned
      underlying values match: `Success = 0`, `Failure = 1`,
      `Suspend = 2`), case style (PascalCased), and the closed
      three-tag exhaustiveness (a `default:` arm at switch sites
      throws because the source compares with `==` and treats
      the enum as closed — no code observes ordinals or arrays
      of all values). Value-vs-reference: enums are value types
      in both languages, identical. Null-safety: an enum field
      is non-nullable by default in both languages (no `?`
      annotation in the source); the call-site fields holding
      `SystemResult` will be non-nullable. No async/Stream/
      isolate surface. FR-024 cache hit on the carry-forward
      idiom.

  - construct_key: dart.mutable_callcontext_class.final_string_final_list_object_questionmark_final_set_inline_init_positional_ctor
    source_form: >-
      "class SystemCall { final String name; final List<Object?>
      args; final Set<int> suspendedReaders = {}; SystemCall(this
      .name, this.args); } — a mutable call-context class: two
      `final` fields (`name` and `args`) bound by the positional
      constructor `SystemCall(this.name, this.args)`, plus a
      third `final` field `suspendedReaders` initialised inline
      to an empty growable `Set<int>` (`{}` is set-literal,
      inferred as `<int>{}` from the declared field type). No
      `==`/`hashCode` override (default reference identity).
      No `toString()` override. No mutator methods on the
      class itself — the suspendedReaders set is mutated by the
      predicate callee that receives the SystemCall as a
      parameter (per the class's leading doc comment: 'collects
      suspended readers if predicate blocks')."
    target_decision: >-
      Emit a reference `class SystemCall` (NOT `record`, NOT
      `struct`) with: a get-only auto-property
      `public string Name { get; }` (Dart `final String name`
      mapped to read-only auto-property — same convention as
      `ExternalChannel.Name` from external_io.dart.md), a
      get-only auto-property
      `public IReadOnlyList<object?> Args { get; }` (Dart
      `final List<Object?> args` — the `final` rebinds the
      reference, NOT the contents; the property type
      `IReadOnlyList<object?>` records the read-only-view
      invariant the registry-using predicates rely on, matching
      the `args` aliasing-not-copying convention from
      external_io.dart.md / body_kernels.dart.md). For the
      inline-initialised mutable set
      `final Set<int> suspendedReaders = {}`: emit a
      get-only auto-property
      `public ISet<long> SuspendedReaders { get; } = new HashSet
      <long>();` — the property is GET-ONLY at the public
      surface (the reference cannot be rebound, matching Dart
      `final`) but the SET ITSELF is mutable (callees `.Add(id)`
      to record suspended-reader addresses; same "rebind-final,
      contents-mutable" semantics as Dart `final Set<int> x =
      {}`). The exposed surface is `ISet<long>` (NOT
      `IReadOnlySet<long>`) because the contract REQUIRES
      callees to mutate the set — exposing `IReadOnlySet` would
      misrepresent the mutability contract (Microsoft Learn
      `ISet<T>` — "defines a collection that contains no
      duplicate elements, and whose elements are in no
      particular order. … Methods that modify the collection
      include `Add`, `Remove`, `Clear`, `ExceptWith`,
      `IntersectWith`, `SymmetricExceptWith`, `UnionWith`").
      Constructor: positional `public SystemCall(string name,
      IReadOnlyList<object?> args) { Name = name; Args = args;
      }` — Dart `this.name`/`this.args` initialising-formal
      parameter shorthand expands to two explicit constructor-
      body assignments in C# (C# has no initialising-formal
      shorthand; Microsoft Learn "Instance constructors" —
      explicit assignment is the faithful render). NOT a
      `record class SystemCall(string Name, IReadOnlyList<object
      ?> Args)` — REJECTED because (a) the Dart source has NO
      `==`/`hashCode` override, so record-synthesised
      structural equality would be a behavioural addition; (b)
      records cannot easily express a non-positional inline-
      initialised mutable third field (`SuspendedReaders`)
      without breaking the positional shape; (c) two
      `SystemCall` instances with coincidentally identical
      `Name`/`Args` are NOT the same call (they belong to
      different goals on the scheduler queue) — reference
      identity is the correct equality. NOT a `struct` — the
      instance is referenced by the predicate callee through
      the `Action`-like delegate interface and mutated through
      the captured reference; a struct would silently copy on
      every method-argument pass and break the `SuspendedReaders
      .Add(id)` propagation back to the caller.
    idiom_id: null
    research_finding_id: rf-dart-mutable-callcontext-class-final-fields-with-inline-set
    nuance: >-
      Value-vs-reference nuance (LOAD-BEARING, explicitly
      addressed): `SystemCall` MUST be a reference type
      (`class`, NOT `record`, NOT `record class`, NOT `struct`,
      NOT `record struct`). Reasoning: (i) the instance is
      passed to the `SystemPredicate` delegate as a single
      parameter; the predicate body MUTATES `call
      .suspendedReaders` via `.add(addr)` to record which heap
      readers caused the suspension; this mutation MUST be
      visible to the caller that constructed the SystemCall —
      reference semantics REQUIRED. (ii) The leading doc comment
      says explicitly: 'Contains arguments and collects
      suspended readers if predicate blocks' — the "collects"
      verb is the load-bearing mutability contract. (iii) Two
      separate predicate calls with coincidentally identical
      `name`+`args` are NOT the same call; reference identity
      is the correct equality (matching the `GoalState` /
      `OutputObserver` / `InputInjector` reference-identity
      discipline from machine_state.dart.md / external_io.dart
      .md). Rebind-final-vs-contents-mutable nuance (LOAD-
      BEARING, explicitly addressed and carry-forward from
      external_io.dart.md / body_kernels.dart.md): Dart
      `final Set<int> suspendedReaders = {}` means the
      REFERENCE is write-once (cannot reassign the field to a
      different set), but the SET CONTENTS are freely mutable
      (`.add` / `.remove` / `.clear` permitted). C# get-only
      auto-property with `= new HashSet<long>()` initialiser
      preserves both halves exactly: the property has no setter
      (reference is write-once), but the returned `ISet<long>`
      reference exposes the mutating surface. Null-safety
      nuance: `final String name` (Dart non-nullable) →
      `public string Name { get; }` (non-nullable under
      enabled NRT); `final List<Object?> args` (Dart non-
      nullable list of nullable `Object?` elements) →
      `IReadOnlyList<object?> Args { get; }` (non-nullable
      list, nullable element — faithful 1:1 to Dart `List<Object
      ?>` shape); `final Set<int> suspendedReaders` (Dart non-
      nullable set of non-nullable ints) → `ISet<long>
      SuspendedReaders { get; }` (non-nullable set, non-
      nullable element). Int-width nuance (carry-forward from
      terms.dart.md / external_io.dart.md): Dart `int` (heap-
      reader addresses stored in the set) → C# `long` — heap
      reader IDs share the same width discipline as VarRef.Addr
      and CurrentReaderId/CurrentWriterId from external_io.dart
      .md. List-vs-IReadOnlyList nuance (carry-forward from
      external_io.dart.md / body_kernels.dart.md): the public
      surface `IReadOnlyList<object?> Args` records the
      "predicates iterate args without mutating" invariant; the
      constructor accepts an `IReadOnlyList<object?>` and the
      reference is aliased (NOT defensively copied) to match
      Dart `this.args = args` semantics. Set-vs-IReadOnlySet
      nuance (LOAD-BEARING, NEW): `SuspendedReaders` is
      EXPLICITLY exposed as `ISet<long>` (mutable surface), NOT
      `IReadOnlySet<long>` — because the predicate callee
      MUTATES the set; exposing a read-only surface would
      misrepresent the contract and force callees to cast.
      Async/Stream/isolate: ABSENT — no async surface; the
      class is a plain synchronous call-context holder.

  - construct_key: dart.typedef.function_signature_two_arg_returning_enum
    source_form: >-
      "typedef SystemPredicate = SystemResult Function(GlpRuntime
      rt, SystemCall call); — a Dart function-type alias
      declaring the predicate-function signature. Two parameters
      (`GlpRuntime` reference + `SystemCall` reference), one
      return value (`SystemResult` enum). The triple-slash doc
      comment is multi-paragraph: it documents the parameters,
      the return, and the side effects ('Can modify: Writer
      bindings (via rt.bindWriter), call.suspendedReaders (if
      suspending)')."
    target_decision: >-
      Emit a C# `public delegate SystemResult SystemPredicate(
      GlpRuntime rt, SystemCall call);` in the same namespace.
      Dart function-type `typedef` → C# `delegate` (carry-
      forward from body_kernels.dart.md
      `rf-dart-typedef-function-to-csharp-delegate` — the C#
      delegate is the .NET first-class equivalent of a function
      reference, and a NAMED delegate is the faithful render of
      a NAMED Dart typedef). Parameter shape: `GlpRuntime rt`
      preserved verbatim; `SystemCall call` preserved verbatim
      (NOT replaced with `ref SystemCall call` because
      `SystemCall` is a REFERENCE type and the mutation surface
      is already through the captured reference — adding `ref`
      would be over-translation). Return type `SystemResult`
      faithful one-to-one. The multi-paragraph triple-slash doc
      comment is preserved verbatim as a C# XML-doc comment on
      the delegate declaration (`/// <summary>System predicate
      function signature</summary>\n/// <remarks>… Can modify:
      Writer bindings (via rt.BindWriter), call.SuspendedReaders
      (if suspending) …</remarks>`). ALTERNATIVE REJECTED:
      `using SystemPredicate = System.Func<GlpRuntime,
      SystemCall, SystemResult>;` would be a STRUCTURAL alias,
      NOT a named type with its own identity — the Dart typedef
      has its OWN type identity (used as the value type of the
      registry `Map<String, SystemPredicate>` and as the
      parameter type of `register`/`lookup` — those signatures
      must carry the NAMED type to give faithful diagnostic
      messages on lookup failure or method-group conversion
      mismatches). The named `delegate` form is the faithful
      render of Dart's named `typedef` (Microsoft Learn
      "delegate (C# Reference)" — "a type that represents
      references to methods with a particular parameter list
      and return type").
    idiom_id: null
    research_finding_id: rf-dart-typedef-function-to-csharp-delegate
    nuance: >-
      Typedef-function nuance (carry-forward from body_kernels
      .dart.md): Dart `typedef X = R Function(A a, B b);`
      declares a named function-type alias. C# has TWO candidate
      renders: (a) a named `delegate R X(A a, B b);` declaration,
      OR (b) a `using X = System.Func<A, B, R>;` alias. The
      faithful render is (a) `delegate` because Dart `typedef`
      of a function type is semantically a NAMED type with its
      own identity (not a structural alias); .NET `delegate` is
      the same — named type with its own identity, distinct
      from `Func<A, B, R>` even at identical shape. Microsoft
      Learn "delegate (C# Reference)" pins delegates as "a type
      that represents references to methods with a particular
      parameter list and return type". Nullability nuance: the
      Dart parameters `GlpRuntime rt` / `SystemCall call` are
      both NON-nullable reference types; the C# delegate
      parameter types are non-nullable `GlpRuntime rt` /
      `SystemCall call` under enabled NRT — faithful 1:1.
      Return value `SystemResult` is a value-type enum, non-
      nullable in both languages. Reference-vs-value: a C#
      `delegate` is a reference type (heap-allocated multicast
      delegate); a Dart function tear-off is also a reference
      (closure object). Method-group nuance: when callers
      `register("name", somePredicateFn)`, the C# bare-name
      `SomePredicateFn` is converted to the `SystemPredicate`
      delegate per Microsoft Learn "Delegate Compatibility — A
      method group can be assigned to a delegate of a matching
      signature." Faithful 1:1 to Dart function tear-off. No
      async/Stream/isolate surface. FR-024 cache hit on the
      carry-forward idiom.

  - construct_key: dart.registry_class.map_keyed_by_name_only_register_lookup_has_names
    source_form: >-
      "class SystemPredicateRegistry { final Map<String,
      SystemPredicate> _predicates = {}; void register(String
      name, SystemPredicate predicate) { _predicates[name] =
      predicate; } SystemPredicate? lookup(String name) =>
      _predicates[name]; bool has(String name) =>
      _predicates.containsKey(name); Iterable<String> get names
      => _predicates.keys; } — a registry class with one
      inline-initialised `final` Map field, three single-line
      methods (`register` returning void, `lookup` returning
      `SystemPredicate?` via the Map indexer, `has` returning
      `bool` via `containsKey`), and one `Iterable<String> get
      names` accessor returning `_predicates.keys`. NO `==` /
      `hashCode` override; default reference identity. NO
      `toString` override."
    target_decision: >-
      Emit a reference `class SystemPredicateRegistry` (NOT
      `record`, NOT `struct`) with: one `private readonly`
      backing field `private readonly Dictionary<string,
      SystemPredicate> _predicates = new Dictionary<string,
      SystemPredicate>();` (Dart `final` field initialised
      inline to `{}` → C# `readonly Dictionary` initialised
      inline to `new Dictionary<...>()`; the Dart `final` field
      with inline `{}` is "rebind-final, contents-mutable" —
      `readonly` in C# carries identical semantics, blocking
      reassignment of the field but permitting `Add`/`Remove`/
      indexer-set on the dictionary contents — carry-forward
      from body_kernels.dart.md `BodyKernelRegistry`).
      `Register(string name, SystemPredicate predicate)`
      returning `void`: body `_predicates[name] = predicate;`
      (Dart indexer-set → C# indexer-set; identical semantics —
      both languages add-or-overwrite). NOTE that the key is
      THE BARE NAME (NOT `$"{name}/{arity}"` as in
      body_kernels.dart.md — system predicates are looked up by
      name only; arity is enforced by the predicate's
      validation of `call.args.Count`, not by the registry key).
      `Lookup(string name)` returning `SystemPredicate?`:
      Dart `_predicates[name]` returns `null` when the key is
      missing (Dart `Map` semantics); C# `Dictionary` indexer
      THROWS `KeyNotFoundException` when the key is missing —
      DIVERGENCE — so the faithful render uses
      `_predicates.TryGetValue(name, out var p) ? p : null;`
      (Microsoft Learn `Dictionary<TKey,TValue>.TryGetValue` —
      "Gets the value associated with the specified key. …
      Returns true if the dictionary contains an element with
      the specified key; otherwise, false."). The return type
      `SystemPredicate?` is nullable because delegates are
      reference types and the `?` records the nullable-return
      invariant. `Has(string name)` →
      `_predicates.ContainsKey(name);` (Dart `containsKey` →
      C# `ContainsKey` byte-identical semantics). `Names` get-
      only property returning `IEnumerable<string>` (Dart
      `Iterable<String> get names => _predicates.keys;` → C#
      `public IEnumerable<string> Names => _predicates.Keys;`
      — expression-bodied getter; `Dictionary<TKey,TValue>.Keys`
      returns a `Dictionary<TKey,TValue>.KeyCollection`
      assignable to `IEnumerable<TKey>` per Microsoft Learn).
      All four members are expression-bodied where the Dart
      source uses `=>` arrow shorthand (`Lookup` /  `Has` /
      `Names`); `Register` has a block body in both languages
      (it is a void mutation). NOT a `record class` — the
      registry has mutable internal state (the dictionary
      contents); record-synthesised structural equality would
      compare the dictionary REFERENCE (since `Dictionary<>`
      members compare by reference in records), giving
      reference-identity semantics anyway — but the synthesis
      adds `EqualityContract` + `with`-expression baggage not
      in the Dart source.
    idiom_id: null
    research_finding_id: rf-dart-map-to-csharp-dictionary
    nuance: >-
      Map-indexer nuance (LOAD-BEARING DIVERGENCE, carry-forward
      from body_kernels.dart.md / suspension.dart.md / cells
      .dart.md): Dart `Map<K, V>` indexer returns `null` on
      missing key; C# `Dictionary<K, V>` indexer THROWS
      `KeyNotFoundException`. The faithful render for
      `Lookup(name)` MUST use `TryGetValue` (NOT the indexer),
      preserving the Dart-return-null semantic. Microsoft Learn
      `Dictionary<TKey, TValue>.TryGetValue` documents the
      exact replacement idiom. Inline-init nuance: Dart `final
      Map _predicates = {};` is "the reference is final, the
      map contents are mutable" — C# `readonly Dictionary` is
      identical (the `readonly` modifier prevents field
      reassignment but does NOT prevent calls to `Add`/`Remove`
      /indexer-set). Key-shape nuance (DIFFERENTIATING from
      body_kernels.dart.md): this registry keys by the BARE
      predicate name (NOT `$"{name}/{arity}"`) — the registry
      is name-only because system predicates dispatch on name
      alone (arity validation happens inside each predicate
      against `call.args.Count`). No string-interpolation
      composition required here. Reference-identity nuance:
      `SystemPredicateRegistry` is a reference-type `class` —
      the runtime holds a SINGLE registry instance and registers
      predicates against it at boot; two distinct registries
      are not the same registry; reference identity is the
      correct equality (matching `BodyKernelRegistry` from
      body_kernels.dart.md). Iterable-vs-IEnumerable nuance
      (carry-forward from body_kernels.dart.md): Dart
      `Iterable<String>` → C# `IEnumerable<string>` — both lazy
      by default; iterating `Dictionary<,>.Keys` enumerates
      without copying. No async/Stream/isolate surface. FR-024
      cache hit on the carry-forward idiom.

  - construct_key: dart.library_level_doc_comment.multi_paragraph_top_of_file_no_library_directive
    source_form: >-
      "Top-of-file 10-line triple-slash doc-comment block
      ('System predicate execution infrastructure for GLP / /
      System predicates are external functions (implemented in
      Dart) that can be called from GLP programs via the
      `execute` instruction. They provide: / - I/O operations
      (file, terminal, network) / - Arithmetic evaluation / -
      System information (time, IDs, etc.) / - Any operation
      requiring side effects or host interaction / / Inspired
      by FCP's execute mechanism but adapted for Dart.'). NO
      `library;` directive follows the doc-comment block — the
      file is implicit-library Dart (no name)."
    target_decision: >-
      No direct .NET counterpart — .NET's compilation-unit /
      namespace model has no `library` concept. The library-
      level doc-comment block becomes a file-header XML-doc
      comment on the namespace declaration mirroring `lib/
      runtime/` (multi-paragraph `<summary>` + `<remarks>`).
      The four bullet-list items (I/O / Arithmetic / System
      information / Any operation requiring side effects)
      become a `<list type="bullet">` in the XML-doc — Microsoft
      Learn "Recommended XML tags for C# documentation comments"
      documents `<list>` as the canonical way to render bulleted
      lists in XML-doc. The Dart-style `-` prefix on each list
      item is replaced by `<item><description>…</description>
      </item>` per the C# XML-doc convention. The "Inspired by
      FCP's execute mechanism but adapted for Dart" line is
      preserved as a `<remarks>` paragraph (load-bearing
      provenance citation). Carry-forward of the idiom recorded
      in external_io.dart.md / heap_fcp.dart.md / suspension
      .dart.md.
    idiom_id: null
    research_finding_id: rf-dart-library-directive-to-csharp-namespace-elision
    nuance: >-
      Compilation-unit nuance only; no value/reference, null-
      safety, or async surface implicated. The file does NOT
      use a `library;` directive — the file is implicit-library
      Dart — so there is no directive to elide here, just the
      doc-comment block above the implicit library. FR-024
      cache hit on the carry-forward idiom — do NOT re-research.
      Dart's prose mention of "I/O operations (file, terminal,
      network)" describes downstream predicate IMPLEMENTATIONS
      (NOT this file) — this file does NOT exercise `dart:io`
      surface, and the well-known `dart:io` → `System.IO` /
      `System.Console` nuance is correctly NOT asserted here
      (per the external_io.dart.md / boot_loader.dart.md
      "absent-nuance is noise" discipline).
conversion_units:
  - "using directives: a single `using <root>.Runtime;` covers terms.cs / runtime.cs (sibling-file imports collapse to one namespace)"
  - "file-header XML-doc on the namespace declaration: multi-paragraph <summary> + <list type='bullet'> + <remarks>Inspired by FCP's execute mechanism but adapted for Dart.</remarks> (library-directive elision; doc-comment block preserved)"
  - "public enum SystemResult { Success, Failure, Suspend } (plain tag-only enum; PascalCased members; default int underlying type; three triple-slash doc comments preserved as XML-doc /// <summary>...</summary>)"
  - "public sealed class SystemCall (reference type, NOT record, NOT struct)"
  - "  property: string Name { get; }                       // Dart `final String name`"
  - "  property: IReadOnlyList<object?> Args { get; }        // Dart `final List<Object?> args` — read-only-view surface, alias-not-copy"
  - "  property: ISet<long> SuspendedReaders { get; } = new HashSet<long>();   // Dart `final Set<int> suspendedReaders = {}` — rebind-final reference, contents-mutable surface (callees `.Add(id)`)"
  - "  ctor: public SystemCall(string name, IReadOnlyList<object?> args) { Name = name; Args = args; }   // Dart positional `SystemCall(this.name, this.args)` expanded to explicit assignments"
  - "public delegate SystemResult SystemPredicate(GlpRuntime rt, SystemCall call);   // Dart `typedef SystemPredicate = SystemResult Function(GlpRuntime rt, SystemCall call);` — NAMED delegate (NOT Func<,,,> structural alias); multi-paragraph XML-doc preserved verbatim"
  - "public sealed class SystemPredicateRegistry (reference type)"
  - "  field: private readonly Dictionary<string, SystemPredicate> _predicates = new Dictionary<string, SystemPredicate>();   // Dart `final Map<String, SystemPredicate> _predicates = {}`"
  - "  public void Register(string name, SystemPredicate predicate) { _predicates[name] = predicate; }   // block-body; indexer-set"
  - "  public SystemPredicate? Lookup(string name) => _predicates.TryGetValue(name, out var p) ? p : null;   // TryGetValue (NOT indexer — Dart returns null, C# throws)"
  - "  public bool Has(string name) => _predicates.ContainsKey(name);   // Dart containsKey → C# ContainsKey 1:1"
  - "  public IEnumerable<string> Names => _predicates.Keys;   // Dart Iterable<String> → C# IEnumerable<string>"
escalations: []
```

## Rationale and research provenance (per non-trivial construct)

### rf-dart-import-relative-to-csharp-using-namespace — relative imports → using directives (carry-forward idiom, reuse)

- Deep analysis. Two relative imports: `import 'runtime.dart';` and `import 'terms.dart';`. Both target sibling files in `lib/runtime/`; both use bare-import surface (no `show` / `hide` narrowing). The `runtime.dart` import brings `GlpRuntime` (the runtime façade type used by the `SystemPredicate` delegate's first parameter); the `terms.dart` import is structurally present for sibling-file dependency completeness (predicate IMPLEMENTATIONS — not in THIS file — use `Term`/`VarRef`/`ConstTerm`/`StructTerm`).
- Authoritative Dart (cached). dart.dev / language / libraries — `import 'relative.dart';` resolves by file path; the imported library's full public surface is available.
- Authoritative .NET (cached). Microsoft Learn "using directive" — `using <Namespace>;` makes the namespace's full public surface available; no per-symbol filtering.
- Conclusion. A single `using <root>.Runtime;` directive covers both sibling-file imports (`runtime.cs` and `terms.cs` both target the same `lib/runtime/` namespace). FR-024 cache hit (carry-forward from external_io.dart.md / heap_fcp.dart.md); no new research.

### rf-dart-enum-plain-to-csharp-enum — `SystemResult` plain enum (carry-forward idiom, reuse)

- Deep analysis. `enum SystemResult { success, failure, suspend }` is a plain tag-only enum: no constructor, no fields, no methods, no associated values. Used as the return value of every `SystemPredicate` callee and compared by `==`; ordinal values are never observed by the source. Each of the three tags has a triple-slash doc comment.
- Authoritative Dart (cached). dart.dev / language / enums — defines the plain `enum` form as a closed, fixed set of constants; enhanced enums add members, which this enum does NOT have.
- Authoritative .NET (cached). Microsoft Learn `enum` (built-in type) — defines `enum` as a value type with named constants, default underlying type `int` — the structurally exact target. Microsoft Learn "Names of classes, structs, and interfaces — Names of enumerations" prescribes PascalCase enum members.
- Conclusion. `public enum SystemResult { Success, Failure, Suspend }`. Three triple-slash doc comments preserved as `/// <summary>...</summary>` XML-doc per Microsoft Learn "Recommended XML tags for C# documentation comments". FR-024 cache hit (carry-forward from machine_state.dart.md `GoalStatus`, cells.dart.md, body_kernels.dart.md `BodyKernelResult`, heap_fcp.dart.md `CellTag`); no new research.

### rf-dart-mutable-callcontext-class-final-fields-with-inline-set — `SystemCall` mutable call-context (NEW finding, LOAD-BEARING)

- Deep analysis. `SystemCall` has two `final` fields bound by a positional initialising-formal constructor (`this.name`, `this.args`) plus a THIRD `final` field initialised inline to an empty set (`Set<int> suspendedReaders = {}`). The class has NO `==`/`hashCode` override (default reference identity) and NO `toString()`. The class's leading doc comment is explicit about the mutability contract: "Contains arguments and collects suspended readers if predicate blocks" — the verb "collects" pins `suspendedReaders` as a mutable accumulator that the `SystemPredicate` callee writes into. The class is constructed by the `Execute` opcode (per the doc comment referencing the `execute` instruction), the resulting instance is passed to the predicate callee, the predicate either mutates `suspendedReaders` (if `SystemResult.suspend` is returned) or leaves it empty (if `SystemResult.success`/`failure`), and the caller then inspects the mutated set.
- Authoritative Dart. WebFetch `https://dart.dev/language/constructors` — "Initializing formal parameters" — `this.name` shorthand assigns to the field of the same name. Inline field initialisers: "Instance variables can be initialized in their declaration" (dart.dev / language / classes); `final Set<int> x = {}` declares a write-once reference holding a fresh empty growable Set.
- Authoritative .NET. WebFetch `https://learn.microsoft.com/en-us/dotnet/csharp/properties#auto-implemented-properties` — "Auto-implemented properties make property-declaration more concise when no additional logic is required in the property accessors. … You can also initialize auto-implemented properties similarly to fields: `public string FirstName { get; } = string.Empty;`." Get-only auto-properties initialised at declaration are the .NET-canonical idiom for "write-once reference, contents-mutable" — the property has no setter (reference is write-once via the constructor or the initialiser), but the runtime object returned through the getter exposes whatever mutating surface its type provides. WebFetch `https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.iset-1` — "Represents a generic collection of objects that is maintained in some specific order. … Methods that modify the collection include `Add`, `Remove`, `Clear`, `ExceptWith`, `IntersectWith`, `SymmetricExceptWith`, `UnionWith`." `ISet<T>` is the canonical .NET interface for a mutable set. WebFetch `https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.hashset-1` — `HashSet<T>` "Represents a set of values" with O(1) `Add`/`Remove`/`Contains` and is the standard concrete implementation of `ISet<T>`.
- Conclusion. Reference `class SystemCall` (NOT `record` — would synthesise structural equality the Dart source lacks; would also need to ignore the inline-initialised `SuspendedReaders` to keep a positional shape, which is not faithful). NOT `struct` — would break mutation propagation of `SuspendedReaders.Add(id)` back to the caller. Three properties: `string Name { get; }`, `IReadOnlyList<object?> Args { get; }`, `ISet<long> SuspendedReaders { get; } = new HashSet<long>();`. The `ISet<long>` (NOT `IReadOnlySet<long>`) is load-bearing — the callee mutates the set. The `HashSet<long>` (NOT `SortedSet<long>` / `ImmutableHashSet<long>` / `ConcurrentSet<long>`) faithfully matches Dart `Set<int>` semantics: unordered, hash-based, mutable, single-threaded. The constructor body explicitly assigns `Name` and `Args` (Dart `this.x` initialising-formal shorthand has no C# counterpart — explicit assignment is the faithful render per Microsoft Learn "Instance constructors"). Authoritative both sides; no escalation. NEW idiom registered.

### rf-dart-typedef-function-to-csharp-delegate — `SystemPredicate` function-type typedef (carry-forward idiom, reuse)

- Deep analysis. `typedef SystemPredicate = SystemResult Function(GlpRuntime rt, SystemCall call);` declares a NAMED function-type alias. The name `SystemPredicate` appears in the `SystemPredicateRegistry._predicates` Map signature (`Map<String, SystemPredicate>`), in the `register(String name, SystemPredicate predicate)` parameter, and in the `lookup` return shape (`SystemPredicate?`). The typedef has its own type identity (Dart programs can dispatch on it). The multi-paragraph doc comment documents the parameters, the return, and the side effects ("Can modify: Writer bindings (via rt.bindWriter), call.suspendedReaders (if suspending)").
- Authoritative Dart (cached). dart.dev / language / typedefs — "A typedef, or function-type alias, gives a function type a name that you can use when declaring fields and return types."
- Authoritative .NET (cached). Microsoft Learn "delegate (C# reference)" — "A delegate is a type that represents references to methods with a particular parameter list and return type. When you instantiate a delegate, you can associate its instance with any method with a compatible signature and return type." A named `delegate` declaration is the C# first-class equivalent of a Dart function typedef.
- Conclusion. `public delegate SystemResult SystemPredicate(GlpRuntime rt, SystemCall call);` in the same namespace. REJECT `using SystemPredicate = System.Func<GlpRuntime, SystemCall, SystemResult>;` — that would be a structural alias (no own identity); the registry signature `Dictionary<string, SystemPredicate>` would be byte-equivalent but lookup-failure diagnostics would print `System.Func<...>` instead of `SystemPredicate`, hurting reviewability. FR-024 cache hit (carry-forward from body_kernels.dart.md `rf-dart-typedef-function-to-csharp-delegate` for `BodyKernel`); no new research.

### rf-dart-map-to-csharp-dictionary — `SystemPredicateRegistry` registry Map (carry-forward idiom, reuse)

- Deep analysis. `Map<String, SystemPredicate> _predicates = {}` — a Dart Map keyed by the BARE predicate name (NOT `name/arity` as in body_kernels.dart.md — system predicates dispatch on name alone; arity validation is internal to each predicate against `call.args.length`). Operations exercised: indexer set (`_predicates[name] = predicate`), indexer get (`_predicates[name]` — returns `null` on miss per Dart Map semantics), `containsKey`, and `keys` iterable. The indexer-get semantics DIVERGE between Dart and C#: Dart returns null; C# `Dictionary` indexer THROWS `KeyNotFoundException`.
- Authoritative Dart (cached). dart.dev / language / collections — `Map<K, V>` is the abstract map interface; literal `{}` produces a `LinkedHashMap<K, V>` (insertion-ordered hash map). api.dart.dev `Map.operator[]` — "Returns the value for the given `key` or `null` if `key` is not in the map."
- Authoritative .NET (cached). Microsoft Learn `Dictionary<TKey,TValue>.TryGetValue` — "Gets the value associated with the specified key. … Returns true if the dictionary contains an element with the specified key; otherwise, false. … This method combines the functionality of the `ContainsKey` method and the `Item[TKey]` property." Microsoft Learn `Dictionary<TKey,TValue>.Item[TKey]` — "Gets or sets the value associated with the specified key. … `KeyNotFoundException`: The property is retrieved and `key` does not exist in the collection." Confirms the indexer-throws contract.
- Conclusion. Use `Dictionary<string, SystemPredicate>` for the storage; use `TryGetValue` for the `Lookup` method that must return `null` on miss. Use the indexer (`_predicates[name] = predicate`) for `Register` because add-or-overwrite is the desired contract on both sides. `ContainsKey` and `Keys` map 1:1. Key shape is the bare name (no `$"{name}/{arity}"` composition — DIFFERENTIATING from body_kernels.dart.md `BodyKernelRegistry`). `Names` exposed as `IEnumerable<string>` per the Iterable→IEnumerable carry-forward. FR-024 cache hit (carry-forward from body_kernels.dart.md `rf-dart-map-to-csharp-dictionary` for `BodyKernelRegistry`, plus suspension.dart.md and cells.dart.md); no new research.

### rf-dart-library-directive-to-csharp-namespace-elision — top-of-file doc-comment block (carry-forward idiom, reuse)

- Deep analysis. The file opens with a 10-line triple-slash doc-comment block describing the file's role (system-predicate execution infrastructure), the kinds of predicates expected (I/O / arithmetic / system information / side-effecting host interaction), and the FCP-inspiration provenance. The file does NOT use an explicit `library;` directive — Dart's implicit-library form is sufficient because the file is not referenced by `import 'package:...' show ...` with a library name.
- Authoritative Dart (cached). dart.dev / language / libraries — `library` directives are optional; the implicit library is the default; doc-comment blocks above the first declaration are library-level documentation.
- Authoritative .NET (cached). Microsoft Learn "Namespaces" — .NET groups compilation by namespace, not by Dart-library. Microsoft Learn "Recommended XML tags for C# documentation comments" — `<summary>`, `<remarks>`, `<list>` are the canonical doc-comment forms; bulleted lists use `<list type="bullet">` with `<item><description>…</description></item>` entries.
- Conclusion. No `library;` directive to elide (none present). The doc-comment block becomes a file-header XML-doc on the namespace declaration mirroring `lib/runtime/` — a multi-paragraph `<summary>` followed by a `<list type="bullet">` rendering of the four bullet items and a `<remarks>` paragraph for the FCP-inspiration note. FR-024 cache hit (carry-forward from external_io.dart.md / heap_fcp.dart.md / suspension.dart.md / variable_table.dart.md); no new research.

## Notes

- This file does NOT exercise the well-known `dart:io` → `System.IO` / `System.Console` nuance. The doc-comment phrase "I/O operations (file, terminal, network)" describes what downstream PREDICATE IMPLEMENTATIONS provide — NOT what THIS file does. THIS file is purely registry + call-context plumbing; no `stdin` / `stdout` / `File` / `Platform` / `Process` / `Directory` / `Encoding` references appear. Asserting an absent nuance would be noise (same discipline as external_io.dart.md / boot_loader.dart.md).
- This file does NOT exercise `async` / `await` / `Future` / `Stream` / `Isolate` / `Completer`. Predicate dispatch is SYNCHRONOUS — the `SystemPredicate` delegate is called from the bytecode runner and returns `SystemResult` immediately (success / failure / suspend). The well-known `Stream` → `IAsyncEnumerable` nuance is correctly NOT asserted here because the CODE does not exercise it. The "suspend" tag is NOT async — it is a synchronous return value telling the scheduler to park the goal until the readers in `call.suspendedReaders` bind.
- Load-bearing semantic decisions for THIS file: (a) Dart `typedef SystemPredicate = …` → C# `delegate SystemPredicate` (NAMED type, NOT `Func<…>` structural alias — the typedef name appears in the registry signature and the lookup-failure diagnostic surface); (b) Dart `Map<String, SystemPredicate>` indexer-get → C# `Dictionary<string, SystemPredicate>.TryGetValue` (NOT the indexer — Dart returns null on miss, C# throws); (c) Dart `final Set<int> suspendedReaders = {}` → C# `ISet<long> SuspendedReaders { get; } = new HashSet<long>();` — the property is GET-ONLY (reference is write-once) but the SET ITSELF is mutable (`.Add(id)` permitted; `ISet<long>` exposes the mutating surface — `IReadOnlySet<long>` would misrepresent the contract); (d) Dart `final List<Object?> args` → C# `IReadOnlyList<object?> Args { get; }` — read-only-view surface, alias-not-copy (carry-forward from external_io.dart.md / body_kernels.dart.md); (e) `SystemCall` and `SystemPredicateRegistry` MUST be reference types (`class`, NOT `record` / `record class` / `struct` / `record struct`) — both mutate internal state (the set, the dictionary), reference identity is correct, and synthesised structural equality would be a behavioural addition.
- Trivial / non-construct elements: triple-slash doc comments (`///`) map mechanically to C# XML-doc comments (`///`); the multi-paragraph documentation on `SystemPredicate` (parameters / return / side-effects) is preserved verbatim as multi-paragraph XML-doc with `<param>` / `<returns>` / `<remarks>` tags per Microsoft Learn "Recommended XML tags for C# documentation comments".
- Zero escalations: every non-trivial construct resolved from authoritative Dart (dart.dev / api.dart.dev) and/or .NET (learn.microsoft.com) official documentation. Five carry-forward idioms reused verbatim (`rf-dart-import-relative-to-csharp-using-namespace`, `rf-dart-enum-plain-to-csharp-enum`, `rf-dart-typedef-function-to-csharp-delegate`, `rf-dart-map-to-csharp-dictionary`, `rf-dart-library-directive-to-csharp-namespace-elision`) and ONE new idiom registered (`rf-dart-mutable-callcontext-class-final-fields-with-inline-set` — the `SystemCall` shape: two final fields bound by initialising-formal positional ctor + one inline-initialised `final Set<int>` that the callee mutates, faithful to C# `class` with `ISet<long>` get-only property defaulted to `new HashSet<long>()`). FR-009/FR-010 quality bar satisfied: every non-trivial construct has BOTH a deep-analysis basis AND a researched-pattern basis (or an explicit carry-forward `research_finding_id`).
