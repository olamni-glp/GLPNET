> Conversion-spec artifact for lib/runtime/suspend_ops.dart (FR-011).
> Spec-only (FR-023): describes the Dart->C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.

```yaml
schema_version: 1
source_path: lib/runtime/suspend_ops.dart
source_sha256: b557f12dac0174dffbd0ebd4fc417e345711aed7c8ea434784d4e64ac7288069
target_code_unit: lib/runtime/suspend_ops.cs
constructs:
  - construct_key: "dart.import_directive.relative-same-package.machine_state"
    source_form: >-
      `import 'machine_state.dart';` -- a relative import of the
      same-package sibling library `lib/runtime/machine_state.dart`.
      No `show`/`hide` clause. Brings the typedefs `GoalId` (= `int`)
      and `Pc` (= `int`) into scope (consumed as the parameter types
      `goalId`/`kappa` of `suspendGoalFCP` / `suspendGoal`). Identical
      directive form to the import treated in suspend.dart.md /
      hanger.dart.md / fairness.dart.md.
    target_decision: >-
      NO standalone target artefact for the import; the converted
      `lib/runtime/suspend_ops.cs` adds a `using` directive that names
      the .NET namespace hosting the converted `machine_state.cs`
      (where the ported `GoalId`/`Pc` global-using aliases live, per
      the convspec at .codeconv/conversion-specs/lib/runtime/
      machine_state.dart.md construct "typedef opaque-int-identifier
      GoalId Pc ReaderId WriterId"). The namespace name is decided by
      the downstream depgraph/namespace step, not this spec. The Dart
      relative-import is NOT a 1:1 file-to-file `using`: in .NET the
      import unit is the namespace, not the file. Codegen MUST NOT
      emit a textual relative-path `using` (e.g. `using ./machine_state
      .cs`) -- that is not valid C#. The consumed aliases `GoalId`/
      `Pc` are reached transparently as `int` (or whichever width
      machine_state.dart.md pins) once the global-using directives are
      in scope. Idiom reused verbatim from suspend.dart.md /
      hanger.dart.md.
    idiom_id: null
    research_finding_id: rf-dart-relative-import-to-csharp-namespace-using
    nuance: >-
      Import-unit nuance: Dart imports a *library/file*; C# imports a
      *namespace*. The 1:1 mapping is "each Dart import line -> one C#
      `using <namespace>;` line that resolves to the namespace of the
      converted target file"; the depgraph/namespace stage owns the
      filename->namespace mapping. Show/hide nuance: ABSENT (no
      `show`/`hide`). Value-vs-reference / null-safety / async /
      Stream / isolate: NOT APPLICABLE -- a directive declares no
      values/types and has no runtime form.
  - construct_key: "dart.import_directive.relative-same-package.heap_fcp"
    source_form: >-
      `import 'heap_fcp.dart';` -- a relative import of the
      same-package sibling library `lib/runtime/heap_fcp.dart`.
      No `show`/`hide` clause. Brings the `HeapFCP` reference-type
      class (the master runtime-state heap), the `VarRef` discriminator
      (via re-export from terms.dart), and the heap methods
      `derefAddr` / `suspendOnWriter` into scope. Consumed by the
      parameter `heap` of `suspendGoalFCP` and by the dispatch logic
      of `_suspendOnVariable`.
    target_decision: >-
      NO standalone target artefact; same treatment as the
      machine_state import above. Codegen emits a `using` of the
      namespace hosting the converted `heap_fcp.cs` (per heap_fcp.dart
      .md). The two referenced public methods `derefAddr` and
      `suspendOnWriter` map to PascalCase `DerefAddr` and
      `SuspendOnWriter` on the converted `HeapFCP` class -- both
      already pinned in heap_fcp.dart.md (construct rows
      `dart.deref_addr.large_switch_with_visited_cycle_detection...`
      and `dart.suspend_on_writer.three_branch_promotion_writer_content
      _or_throw`). This spec consumes those decisions, does NOT
      re-derive them.
    idiom_id: null
    research_finding_id: rf-dart-relative-import-to-csharp-namespace-using
    nuance: >-
      Same import-unit / show-hide-absent / no-runtime-form profile as
      the machine_state import above. Cross-file dependency nuance: the
      method-name decisions (`DerefAddr`, `SuspendOnWriter`) are
      load-bearing here and are pinned in heap_fcp.dart.md; this spec
      does not duplicate them.
  - construct_key: "dart.import_directive.relative-same-package.suspension"
    source_form: >-
      `import 'suspension.dart';` -- a relative import of the
      same-package sibling library `lib/runtime/suspension.dart`. No
      `show`/`hide` clause. Brings the `SuspensionRecord` (shared
      reference-type holding `(goalId, resumePC)` + disarm state) and
      `SuspensionListNode` (per-cell wrapper around a `SuspensionRecord
      ` reference, with mutable `next`) into scope. Consumed by the
      `new SuspensionRecord(goalId, kappa)` allocation in
      `suspendGoalFCP` and by the `new SuspensionListNode(record)`
      allocation in `_suspendOnVariable`.
    target_decision: >-
      NO standalone target artefact; same treatment as above. Codegen
      emits a `using` of the namespace hosting the converted
      `suspension.cs` (per suspension.dart.md, which pins both classes
      as reference types via `rf-dart-shared-mutable-record-by-reference
      -to-csharp-class`). Crucially, this spec REUSES the
      reference-identity decision from suspension.dart.md -- the
      sharing semantics ("ONE shared record, MANY list-node wrappers")
      depend on `SuspensionRecord` being a reference type so that
      `disarm()` propagates to every wrapper. Codegen MUST NOT
      re-derive this as a record/struct.
    idiom_id: null
    research_finding_id: rf-dart-relative-import-to-csharp-namespace-using
    nuance: >-
      Reference-identity load-bearing nuance (carry-forward from
      suspension.dart.md): the FCP "ONE shared record" idiom depends
      on `SuspensionRecord` being a C# reference-type class (NOT a
      `record`/`record class`/`struct`/`record struct`) so that all
      list-node wrappers see the same disarm-state. This spec does NOT
      re-decide that mapping -- it inherits it from suspension.dart.md.
      Show/hide nuance: ABSENT.
  - construct_key: "dart.import_directive.relative-same-package.terms"
    source_form: >-
      `import 'terms.dart';` -- a relative import of the same-package
      sibling library `lib/runtime/terms.dart`. No `show`/`hide`
      clause. Brings the `Term` hierarchy root and the `VarRef` sealed
      leaf into scope. Only `VarRef` is referenced in this file (in
      the `if (result is VarRef)` type-test inside `_suspendOnVariable`).
    target_decision: >-
      NO standalone target artefact; same treatment as above. Codegen
      emits a `using` of the namespace hosting the converted
      `terms.cs` (per terms.dart.md, which pins `VarRef` as a sealed
      reference-type class with `IEquatable<VarRef>` and structural
      `addr`-based `==` -- but here only the IDENTITY of the type for
      a `is VarRef varRef` pattern-match is consumed). The `result.addr`
      access maps to the `VarRef.Addr` property pinned in terms.dart
      .md.
    idiom_id: null
    research_finding_id: rf-dart-relative-import-to-csharp-namespace-using
    nuance: >-
      Same profile as the other relative imports. Cross-file
      dependency nuance: this file only consumes `VarRef` as a TYPE
      DISCRIMINATOR (via `is VarRef`) and reads `result.addr` -- both
      decisions pinned in terms.dart.md. No re-derivation here.
  - construct_key: "dart.import_directive.package-internal-with-show.variable_table-VariableEntry"
    source_form: >-
      `import 'package:glp_runtime/multiagent/variable_table.dart'
      show VariableEntry;` -- a `package:`-URI import (NOT a
      relative-path import) that crosses the lib/runtime/ -> lib/
      multiagent/ subtree boundary, with a `show VariableEntry`
      narrowing clause restricting the imported public surface to the
      single identifier `VariableEntry`. Consumed by the `if (result
      is VariableEntry)` type-test inside `_suspendOnVariable`,
      followed by the field accesses `result.suspensions` (read/write)
      to mutate the per-imported-reader suspension chain.
    target_decision: >-
      NO standalone target artefact for the import; the converted
      `lib/runtime/suspend_ops.cs` adds a `using` directive that names
      the .NET namespace hosting the converted `variable_table.cs`
      (where the ported `VariableEntry` reference-type class lives,
      per the convspec at .codeconv/conversion-specs/lib/multiagent/
      variable_table.dart.md construct "class VariableEntry mutable-
      state-holder mixed-final-nonfinal-fields nullable-term nullable-
      suspension-list named-ctor-default-fallback toString-override").
      The `package:`-URI mapping is the same `using <namespace>;` form
      as the relative-import mapping ABOVE -- once the depgraph/
      namespace step assigns a namespace to the multiagent subtree,
      both forms collapse to a single `using` directive. The Dart
      `show VariableEntry` narrowing clause has NO .NET counterpart
      because C# `using` imports a namespace's full publicly-visible
      surface; this loss of narrowing is documented as a nuance, NOT
      escalated -- well-precedented per goal_queue.dart.md /
      suspend.dart.md / fairness.dart.md (the `show`-no-counterpart
      precedent). Idiom reused verbatim: same `rf-dart-relative-import-
      to-csharp-namespace-using` family -- the `package:`-prefix is a
      Dart-URI-resolution detail that does not change the .NET-side
      mapping (also corroborated by test/multiagent/boot_loader_test
      .dart.md and test/multiagent/globalize_test.dart.md, which spec
      the same `package:glp_runtime/...` -> `using <namespace>;`
      mapping under the related `rf-dart-internal-package-import-to-
      csharp-using` / `rf-dart-same-package-import-to-csharp-using`
      idioms).
    idiom_id: null
    research_finding_id: rf-dart-relative-import-to-csharp-namespace-using
    nuance: >-
      PACKAGE-URI-VS-RELATIVE nuance (explicitly addressed): Dart
      distinguishes `package:glp_runtime/...` URIs from relative
      `'../...'`/`'./...'` paths -- they share the same underlying
      package-internal-import semantics but differ in resolution
      mechanism (URI vs filesystem-relative). The .NET counterpart is
      identical on both sides: `using <namespace>;` where the
      namespace is the converted-file's containing namespace.
      SHOW-CLAUSE-NO-COUNTERPART nuance (LOAD-BEARING, explicitly
      addressed, NOT glossed): Dart `show VariableEntry` narrows the
      imported public surface to a single identifier; C# `using
      <namespace>;` has no per-symbol narrowing form (there is `using
      <alias> = <fullyqualifiedtype>;` but that ALIASES, not
      narrows). The narrowing is therefore LOST in the conversion;
      this is faithful per the well-established convspec precedent
      (suspend.dart.md / fairness.dart.md / goal_queue.dart.md
      `rf-dart-export-directive-to-csharp-using-alias` discussion).
      Practical impact: zero -- the .NET compiler still resolves only
      the identifiers actually USED in the file; unused identifiers
      from the namespace are not loaded into anything observable.
      Value-vs-reference: NOT APPLICABLE at the directive level
      (`VariableEntry` itself is a reference type per variable_table
      .dart.md). Null-safety / async / Stream / isolate: NOT
      APPLICABLE.
  - construct_key: "dart.utility_class.static_only_holder.SuspendOps"
    source_form: >-
      `class SuspendOps { static void suspendGoalFCP({...}) {...}
      static void _suspendOnVariable(...) {...} static void
      suspendGoal({...}) {...} }` -- a Dart class containing exactly
      three static members (two public -- `suspendGoalFCP`,
      `suspendGoal` -- and one underscore-private --
      `_suspendOnVariable`), no fields, no instance constructor, no
      instance members. Used as a namespacing container for the FCP
      "suspension" / goal-suspension operations. Identical class-shape
      to `AbandonOps` (abandon.dart.md) and `CommitOps` (commit.dart
      .md).
    target_decision: >-
      Emit a C# `public static class SuspendOps` (sealed, abstract by
      virtue of `static`, cannot be instantiated -- the .NET
      counterpart of a Dart "static-methods-only" holder class per
      the `rf-dart-static-only-holder-to-csharp-static-class` idiom
      established in abandon.dart.md and reused verbatim in commit
      .dart.md). Containing the three converted static members (two
      public `SuspendGoalFCP` and `SuspendGoal` (deprecated stub); one
      private `SuspendOnVariable`). A non-static class with all-static
      members is REJECTED here because (a) the Dart source's class is
      callable only via `SuspendOps.suspendGoalFCP(...)` (never
      instantiated) and (b) `static class` makes the no-instantiation
      contract a compile-time guarantee on the .NET side, matching
      the source's design intent. Do NOT emit free-floating static
      functions at namespace scope: C# does not permit top-level free
      functions outside a type, and the Dart source explicitly groups
      via the class identifier `SuspendOps` that the conversion
      preserves as a callable identifier.
    idiom_id: null
    research_finding_id: rf-dart-static-only-holder-to-csharp-static-class
    nuance: >-
      Static-class contract: in Dart, a class with only static members
      is still instantiable by convention only (`SuspendOps()` would
      compile); C# `static class` makes the no-instantiation a
      compile-time invariant and also makes the class implicitly
      sealed -- both invariants are desirable here (the Dart source
      never instantiates `SuspendOps` and has no subclasses).
      Value-vs-reference: not applicable at the type level (no
      instances). Async / Stream / Future / isolate: ABSENT -- every
      member is synchronous. Null-safety at the type level: not
      applicable. The narrowing is strictly correct here -- same
      reasoning as abandon.dart.md and commit.dart.md.
  - construct_key: "dart.static_method.named_required_params.suspendGoalFCP-shared-record-fanout"
    source_form: >-
      `static void suspendGoalFCP({required HeapFCP heap, required
      int goalId, required int kappa, required Set<int>
      readerVarIds}) { final sharedRecord = SuspensionRecord(goalId,
      kappa); for (final addr in readerVarIds) { _suspendOnVariable(
      heap, addr, sharedRecord); } }` -- public static method with
      FOUR named-required parameters. The body does THE LOAD-BEARING
      FCP-EXACT IDIOM: allocate ONE shared `SuspensionRecord` and
      hand the SAME reference to every address-specific suspension
      attachment. The single-instance invariant is what makes
      `disarm()` propagate atomically across all suspension wrappers
      when the goal eventually resumes -- without it, double-
      activation occurs.
    target_decision: >-
      Emit a `public static void SuspendGoalFCP(HeapFCP heap, long
      goalId, long kappa, ISet<long> readerVarIds) { var sharedRecord
      = new SuspensionRecord(goalId, kappa); foreach (var addr in
      readerVarIds) { SuspendOnVariable(heap, addr, sharedRecord); }
      }` method on `public static class SuspendOps`. The Dart
      `{required HeapFCP heap, required int goalId, required int
      kappa, required Set<int> readerVarIds}` named-required
      parameters map to plain C# positional parameters (no defaults)
      -- per the project-recurring idiom `rf-dart-named-required-
      ctor-with-defaults-to-csharp-positional-ctor-with-defaults`
      reused from abandon.dart.md / commit.dart.md / machine_state
      .dart.md: C# has no per-parameter `required` keyword for
      methods (the C# 11 `required` modifier is for properties only),
      and a positional parameter without a default IS required by
      the compiler; call-site readability is preserved by C#
      named-argument syntax (`SuspendOps.SuspendGoalFCP(heap: h,
      goalId: g, kappa: k, readerVarIds: s)`). Each Dart `int`
      widens to `long` per the recurring `rf-dart-int-to-csharp-long-
      width` idiom (cells.dart.md precedent). The `Set<int>` Dart
      parameter widens to `ISet<long>` (interface contract over
      `HashSet<long>` -- per the heap_fcp.dart.md precedent
      `dart.deref_addr.large_switch_with_visited_cycle_detection
      _wxw_violation_and_three_tag_cases` row, which maps Dart
      `Set<int>` to .NET `HashSet<int>` for an INTERNAL variable but
      uses the concrete; here, because the set is a PARAMETER
      RECEIVED from a caller and never mutated in this body, the
      faithful interface-narrowing is `ISet<long>` -- read-only
      iteration via `foreach` is satisfied by `ISet<T> : IEnumerable
      <T>`; the caller may pass `HashSet<long>` or any other
      `ISet<long>` implementation. Codegen MAY narrow further to
      `IEnumerable<long>` if no `ISet`-specific member is used --
      observation confirms only `foreach` is used, so
      `IEnumerable<long>` would also be valid; spec default =
      `ISet<long>` to preserve the source's set-semantics
      DOCUMENTATION (the source's type name says "this is a set of
      distinct ids"). The `final sharedRecord = SuspensionRecord(
      goalId, kappa)` line maps to `var sharedRecord = new
      SuspensionRecord(goalId, kappa)` -- Dart's implicit-`new` ctor
      call becomes the explicit `new` (Microsoft Learn: C# requires
      `new` for constructor calls); `final` -> `var` because the
      LOCAL is never reassigned (C# `var` infers the type and is
      mutable by default; in C# 12 `readonly var` exists for true
      finality but is rarely used for one-shot locals). The `for
      (final addr in readerVarIds)` -> `foreach (var addr in
      readerVarIds)` per the project-recurring iteration idiom
      (commit.dart.md / heap_fcp.dart.md precedents).
    idiom_id: null
    research_finding_id: rf-dart-named-required-ctor-with-defaults-to-csharp-positional-ctor-with-defaults
    nuance: >-
      LOAD-BEARING REFERENCE-SHARING nuance (explicitly addressed, NOT
      glossed): the FCP-exact design hinges on ONE `SuspensionRecord`
      reference being shared across all `SuspensionListNode` wrappers
      added during the loop. Because `SuspensionRecord` is a C#
      reference type (decided in suspension.dart.md), the assignment
      `var sharedRecord = new SuspensionRecord(...)` and the
      subsequent pass-by-value of `sharedRecord` to every
      `SuspendOnVariable(...)` call hand THE SAME REFERENCE to each
      callee -- this is the semantically-required behaviour. Codegen
      MUST NOT change `SuspensionRecord` to a value type (`struct`/
      `record struct`) here -- doing so would copy the record on each
      method-call and destroy the shared-disarm invariant. (This
      cross-cuts suspension.dart.md and commit.dart.md, which
      establish the same invariant.) Named-required nuance: Dart
      `{required ...}` -> positional C# parameters (call-site named-
      argument syntax preserved). Width nuance: `int` -> `long`
      (project recurrence). Set-parameter nuance: Dart `Set<int>` ->
      `ISet<long>` for a read-only parameter (interface-narrowing
      preserves the set-semantics documentation; codegen MAY narrow
      to `IEnumerable<long>` if downstream call sites confirm no set-
      specific use). Null-safety: under enabled NRT all four
      parameters are non-nullable (Dart `required` named parameters
      are non-nullable by their type). Async / Stream / Future /
      isolate: ABSENT -- synchronous fan-out. Iteration-order nuance:
      Dart `Set<int>` (the literal `{}` for `int` -- a `LinkedHashSet`
      backing the literal) iterates in insertion order; .NET
      `HashSet<long>` does NOT guarantee insertion order; .NET
      `ISet<long>` makes no order guarantee at all. For this method
      the iteration ORDER is observably IRRELEVANT (each
      `SuspendOnVariable` mutates a DIFFERENT cell's chain; the
      shared-record reference is the same regardless of order) --
      recorded as a nuance, NOT an escalation. WxW-violation, error
      paths: NONE in this method (the called `_suspendOnVariable`
      may throw via downstream `SuspendOnWriter`, but those throws
      are pinned in heap_fcp.dart.md and propagate transparently).
  - construct_key: "dart.static_method.private.suspendOnVariable-three-branch-dispatch-on-deref-result"
    source_form: >-
      `static void _suspendOnVariable(HeapFCP heap, int addr,
      SuspensionRecord record) { final result = heap.derefAddr(addr);
      if (result is VariableEntry) { final node = SuspensionListNode(
      record); node.next = result.suspensions; result.suspensions =
      node; return; } if (result is VarRef) { final writerAddr =
      result.addr; heap.suspendOnWriter(writerAddr, record); return;
      } /* else: already bound to ground -- no suspension needed (no-op) */ }`
      -- leading-underscore PRIVATE static method. Three positional
      parameters (heap, addr, record). Body: dereference once via
      `heap.derefAddr(addr)`, then dispatch on the runtime type of
      the deref result: (a) `VariableEntry` -> imported-reader path:
      mutate the entry's `suspensions` linked-list head in place
      (cons new node onto the existing chain); (b) `VarRef` -> local-
      reader-or-writer path: extract the underlying writer address
      and delegate to `heap.suspendOnWriter(writerAddr, record)`;
      (c) anything else (a `Term` ground value) -> silent no-op
      (comment: "Already bound to ground - no suspension needed
      (This shouldn't normally happen if we're suspending on unbound
      vars)").
    target_decision: >-
      Emit a `private static void SuspendOnVariable(HeapFCP heap,
      long addr, SuspensionRecord record)` on `public static class
      SuspendOps`. Leading underscore DROPPED in .NET -- the
      `private` modifier is the canonical visibility marker; private
      static methods use PascalCase like public methods per Microsoft
      Learn .NET naming guidelines (underscore prefix is reserved
      for private FIELDS, not methods); this matches the heap_fcp
      .dart.md precedent on `_ForwardSuspensions` -> `ForwardSuspensions`.
      Body: `var result = heap.DerefAddr(addr); if (result is
      VariableEntry entry) { var node = new SuspensionListNode(record);
      node.Next = entry.Suspensions; entry.Suspensions = node;
      return; } if (result is VarRef varRef) { var writerAddr = varRef
      .Addr; heap.SuspendOnWriter(writerAddr, record); return; } /* else:
      already bound to ground -- no suspension needed (no-op) */`.
      The two `is`-type-tests become C# type-patterns that bind a
      typed local in one step (`is VariableEntry entry` / `is VarRef
      varRef`) -- Microsoft Learn pattern matching, the canonical
      counterpart of Dart's `is`-promotion (heap_fcp.dart.md
      precedent on `is VarRef varRef`, line 233 of that spec).
      `heap.derefAddr(addr)` -> `heap.DerefAddr(addr)` per the
      method-name decision pinned in heap_fcp.dart.md (construct row
      `dart.deref_addr.large_switch_with_visited_cycle_detection_wxw
      _violation_and_three_tag_cases`); `heap.suspendOnWriter(...)`
      -> `heap.SuspendOnWriter(...)` per the method-name decision
      pinned in heap_fcp.dart.md (construct row `dart.suspend_on_
      writer.three_branch_promotion_writer_content_or_throw`); these
      are CONSUMED, not re-derived. `result.suspensions` /
      `result.addr` -> `entry.Suspensions` / `varRef.Addr` per
      variable_table.dart.md and terms.dart.md respectively
      (consumed). The cons-list-prepend mutation pattern (`node.Next
      = entry.Suspensions; entry.Suspensions = node`) is identical
      to the heap_fcp.dart.md `SuspendOnWriter` body (LOAD-BEARING
      O(1) head-insert; ORDER MATTERS -- `Next` is set BEFORE
      `Suspensions` is updated, otherwise the previous chain head is
      lost). The third arm (already-bound-to-ground) is a SILENT
      no-op -- preserved exactly: codegen MUST NOT add a `throw`
      here; the source comment explicitly anticipates the "shouldn't
      normally happen" case but accepts a no-op (NOT an error) as
      faithful behaviour.
    idiom_id: null
    research_finding_id: rf-dart-shared-mutable-record-by-reference-to-csharp-class
    nuance: >-
      THREE-BRANCH-DISPATCH nuance (LOAD-BEARING, explicitly
      addressed, NOT glossed): this method is the dispatcher that
      decides WHERE the suspension lives -- on the `VariableEntry`
      for imported readers, on the WriterContent (via
      `SuspendOnWriter`) for local readers/writers, or nowhere (for
      already-ground bindings). The same logical dispatch shape is
      preserved EXACTLY in C# via two `is`-type-pattern checks plus a
      fall-through no-op; this is the heap-pointer-architecture v3.0
      contract ("suspensions are stored on WRITER cells (not reader
      cells); for imported readers, suspensions are stored in
      VariableEntry"). VARIABLE-ENTRY-MUTATION nuance (LOAD-BEARING,
      carry-forward from variable_table.dart.md): mutating `entry
      .Suspensions = node` propagates to every observer of the entry
      because `VariableEntry` is a reference-type class (NOT a
      record/struct). The `Suspensions` property MUST be `{ get;
      set; }` -- exactly as pinned in variable_table.dart.md. LINKED-
      LIST-PREPEND nuance: see heap_fcp.dart.md `SuspendOnWriter`
      row -- the same canonical O(1) head-insert pattern; order
      matters. SHARED-RECORD nuance (LOAD-BEARING, carry-forward from
      suspendGoalFCP above and suspension.dart.md): the `record`
      parameter is passed BY REFERENCE (because `SuspensionRecord`
      is a reference type) -- multiple wrappers across multiple
      cells will share the SAME record; do NOT clone. PATTERN-
      VARIABLE nuance: `is VariableEntry entry` (C# 7+ type pattern)
      and `is VarRef varRef` (likewise) bind a typed local in one
      step -- Dart `is`-promotion's faithful .NET counterpart. NO-OP-
      FALL-THROUGH nuance: the third arm is silent on purpose -- the
      source's comment documents the rationale; codegen MUST NOT add
      a `throw` (would change semantics). Width nuance: `addr` is
      `int` -> `long` (project recurrence). Null-safety: under
      enabled NRT `heap` and `record` are non-nullable; `addr` is a
      non-nullable value type; `heap.DerefAddr(addr)` returns
      `object` (per heap_fcp.dart.md decision) -- the `is`-pattern
      checks ARE the null-safety mechanism (if `result` were null,
      both `is` checks would fail and the silent no-op arm would
      handle it; .NET `is`-checks against a null reference always
      return false per Microsoft Learn pattern matching). Async /
      Stream / Future / isolate: ABSENT.
  - construct_key: "dart.static_method.deprecated_stub.suspendGoal-named-required-throws-unimplemented"
    source_form: >-
      `static void suspendGoal({required int goalId, required int
      kappa, required Set<int> readerVarIds}) { throw
      UnimplementedError('Legacy suspendGoal deprecated - use
      suspendGoalFCP'); }` -- a public static method preserved as a
      DEPRECATED stub: same name as a pre-FCP API, three named-
      required parameters, body throws `UnimplementedError` with a
      human-readable redirect message. The doc-comment immediately
      above is `/// Legacy version (deprecated)`.
    target_decision: >-
      Emit a `public static void SuspendGoal(long goalId, long kappa,
      ISet<long> readerVarIds) { throw new NotImplementedException(
      "Legacy suspendGoal deprecated - use suspendGoalFCP"); }` on
      `public static class SuspendOps`. Direct project-precedent
      reuse from abandon.dart.md / boot_loader.dart.md: `UnimplementedError`
      -> `NotImplementedException` per `rf-dart-unimplemented-error-
      to-csharp-notimplemented` (INTENT-based mapping: both signal
      "intentionally not implemented in this layer"; .NET has no
      `Error` vs `Exception` hierarchy split). Named-required ->
      positional per `rf-dart-named-required-ctor-with-defaults-to-
      csharp-positional-ctor-with-defaults` (call-site named-
      argument syntax preserved). `int` -> `long` per `rf-dart-int-
      to-csharp-long-width`. `Set<int>` -> `ISet<long>` (parameter
      narrowing, same reasoning as `SuspendGoalFCP`). DEPRECATED
      marking: emit `[System.Obsolete("Legacy SuspendGoal deprecated -
      use SuspendGoalFCP")]` attribute on the C# method (Microsoft
      Learn `System.ObsoleteAttribute`: "Marks the program elements
      that are no longer in use") -- captures the Dart doc-comment's
      `(deprecated)` marker as a compile-time warning, faithful to
      the source's intent. This is a TIGHTER contract than the Dart
      source's plain comment (which is documentation-only); the
      narrowing is correct because the source's `throw
      UnimplementedError(...)` body ALREADY signals "do not call
      this", and the `[Obsolete]` attribute makes that signal
      compile-time-visible. Codegen MAY add a TODO-comment to remove
      the stub when the legacy callers are migrated.
    idiom_id: null
    research_finding_id: rf-dart-unimplemented-error-to-csharp-notimplemented
    nuance: >-
      DEPRECATION-MARKER nuance (explicitly addressed): Dart has no
      built-in `@deprecated` annotation usage here -- the
      "deprecated" semantics live entirely in the doc-comment and
      the body's `UnimplementedError` throw. The .NET counterpart
      `[Obsolete]` attribute LIFTS this documentation-only signal to
      a compile-time warning. This is a TIGHTER contract on the .NET
      side (the source's `@deprecated`-equivalent is a `meta`-package
      annotation that Dart can also surface as a compile-time
      warning -- the source author CHOSE not to use it, but the
      `(deprecated)` parenthetical doc-comment + `UnimplementedError`
      body together signal the same intent). Project precedent
      reused verbatim from abandon.dart.md (same `UnimplementedError`
      -> `NotImplementedException` mapping, same named-required ->
      positional translation, same `int` -> `long` widening). Set-
      parameter nuance: identical to `SuspendGoalFCP` above (ISet<long>
      for a read-only parameter). Null-safety: parameters non-
      nullable (Dart `required`); return is `void`. Async / Stream /
      Future / isolate: ABSENT. Exception-class nuance: Dart
      `UnimplementedError` extends `Error` (programming-defect
      signal); .NET `NotImplementedException` extends `SystemException`
      (also a defect signal). The mapping is by INTENT, not by
      class-hierarchy (no `Error` vs `Exception` split in .NET) --
      same reasoning as abandon.dart.md (the load-bearing
      `rf-dart-unimplemented-error-to-csharp-notimplemented`
      decision).
  - construct_key: "dart.docblock_triple_slash.class-and-method-doc-comments"
    source_form: >-
      Seven `///` triple-slash doc-comment blocks on the
      class/methods/parameters: the multi-line class header (`/// Suspension
      operations using FCP-exact shared suspension records` + `/// `
      + `/// Per heap-pointer-architecture-spec.md v3.0:` + `/// -
      Suspensions are stored on WRITER cells (not reader cells)` +
      `/// - For imported readers, suspensions are stored in
      VariableEntry`); the `suspendGoalFCP` header (`/// FCP-exact
      suspension: create ONE shared record, add to each variable's
      writer` + `/// ` + `/// Parameters:` + `/// - heap: The heap`
      + `/// - goalId: Goal to suspend` + `/// - kappa: Resume PC
      (restart at clause 1)` + `/// - readerVarIds: Set of addresses
      to suspend on (can be writer or reader addresses)`); the
      `_suspendOnVariable` header (`/// Add suspension to a variable
      (follows chain to find final unbound writer)`); the
      `suspendGoal` header (`/// Legacy version (deprecated)`); and
      one in-body comment (`// Already bound to ground - no
      suspension needed` + `// (This shouldn't normally happen if
      we're suspending on unbound vars)`).
    target_decision: >-
      Map to C# XML-doc comments on the class and each method:
      `/// <summary>Suspension operations using FCP-exact shared
      suspension records.</summary>/// <remarks>Per
      heap-pointer-architecture-spec.md v3.0: Suspensions are stored
      on WRITER cells (not reader cells). For imported readers,
      suspensions are stored in VariableEntry.</remarks>` on the
      class; `<summary>` + `<param>` tags per parameter on
      `SuspendGoalFCP` (using the Dart parameter-list bullet items
      as the `<param>` body text -- the Dart `/// Parameters: - heap:
      ...` block maps to one `<param name="heap">The heap</param>`
      per parameter); `<summary>` on `SuspendOnVariable`; `<summary>`
      on `SuspendGoal` plus the `[Obsolete(...)]` attribute (the
      "(deprecated)" doc-tag is consumed by the attribute). The
      in-body `// Already bound to ground...` comment is preserved
      verbatim as a `// ...` C# single-line comment (no XML-doc form
      needed -- it is a body comment, not a member doc-comment).
      Trivial mechanical mapping; the documented FCP invariants are
      preserved exactly for any future maintainer.
    idiom_id: null
    research_finding_id: null
    nuance: trivial
    trivial: true
conversion_units:
  - "using directives: using of the namespaces hosting the converted machine_state.cs / heap_fcp.cs / suspension.cs / terms.cs / variable_table.cs (project-wide namespace decision; deferred to the depgraph/namespace step); show clause on the VariableEntry import has NO .NET counterpart and is dropped (well-precedented per goal_queue / suspend / fairness specs)"
  - "public static class SuspendOps (sealed/abstract by virtue of `static`; no instances; namespacing holder for FCP suspension operations)"
  - "public static void SuspendGoalFCP(HeapFCP heap, long goalId, long kappa, ISet<long> readerVarIds) -- four positional parameters (named-required call style preserved via C# named arguments); body allocates ONE shared `new SuspensionRecord(goalId, kappa)` then foreach-iterates `readerVarIds` calling `SuspendOnVariable(heap, addr, sharedRecord)` -- the SHARED-REFERENCE invariant (one record, many wrappers) is load-bearing"
  - "private static void SuspendOnVariable(HeapFCP heap, long addr, SuspensionRecord record) -- private helper (leading underscore dropped; PascalCase); body derefs once via `heap.DerefAddr(addr)`, dispatches via two `is`-type-patterns (`is VariableEntry entry` -> mutate `entry.Suspensions` linked-list head; `is VarRef varRef` -> delegate to `heap.SuspendOnWriter(varRef.Addr, record)`), with a silent fall-through no-op for the ground-bound case"
  - "[System.Obsolete(\"Legacy SuspendGoal deprecated - use SuspendGoalFCP\")] public static void SuspendGoal(long goalId, long kappa, ISet<long> readerVarIds) -- deprecated stub; body throws new NotImplementedException(\"Legacy suspendGoal deprecated - use suspendGoalFCP\") (UnimplementedError -> NotImplementedException per `rf-dart-unimplemented-error-to-csharp-notimplemented` from abandon.dart.md)"
  - "doc-comments -> /// <summary>/<remarks>/<param> XML-doc on class and methods; in-body comments preserved as `//` C# single-line comments"
escalations: []
```

## Rationale and research provenance (per non-trivial construct)

### rf-dart-relative-import-to-csharp-namespace-using -- relative + package-internal imports (cache hit)

- **Deep analysis.** Five import directives in this file: four relative-
  same-package imports (`machine_state.dart`, `heap_fcp.dart`,
  `suspension.dart`, `terms.dart`) and one `package:`-URI internal-package
  import with a `show VariableEntry` narrowing clause (`package:glp_runtime/
  multiagent/variable_table.dart`). Each Dart import line maps to ONE C#
  `using <namespace>;` where the namespace is the converted-file's
  containing namespace; the depgraph/namespace step owns the
  filename->namespace mapping. The `show` clause has NO .NET counterpart
  and is dropped (well-established precedent).
- **Authoritative Dart.** `https://dart.dev/language/libraries#import-and-export`
  documents both relative imports (`import 'sibling.dart';`) and
  `package:`-URI imports (`import 'package:foo/bar.dart';`) as the two
  forms of library imports, and documents `show`/`hide` as per-symbol
  narrowing.
- **Authoritative .NET.** `https://learn.microsoft.com/en-us/dotnet/csharp/
  language-reference/keywords/using-directive` -- the `using <namespace>;`
  directive imports a namespace's types into scope; there is no per-symbol
  narrowing form. Cache hit on the existing
  `rf-dart-relative-import-to-csharp-namespace-using` idiom established
  in fairness.dart.md (relative imports), reused verbatim in
  suspend.dart.md and hanger.dart.md, and the parallel
  `rf-dart-internal-package-import-to-csharp-using` /
  `rf-dart-same-package-import-to-csharp-using` idioms used by the
  test/multiagent specs for `package:` URIs -- the conversion shape is
  identical on both sides.
- **Conclusion.** All five imports collapse to `using` directives in the
  converted `suspend_ops.cs`; the `show VariableEntry` clause is faithfully
  dropped (no .NET counterpart) -- not an escalation, just a recognised
  expressivity loss. Authoritative both sides; reuse, no escalation.

### rf-dart-static-only-holder-to-csharp-static-class -- SuspendOps utility class (cache hit)

- **Deep analysis.** `SuspendOps` is a Dart class containing exactly three
  `static` members (two public, one private), no fields, no instance
  constructor, no instance members. The class identifier is used purely as
  a namespace (`SuspendOps.suspendGoalFCP(...)`); the class is never
  instantiated. The source's intent ("this is a holder for FCP suspension
  operations, not a stateful object") is preserved exactly by C#'s
  `static class` keyword, which compile-time-enforces the no-instantiation
  contract.
- **Authoritative .NET.** `https://learn.microsoft.com/en-us/dotnet/csharp/
  programming-guide/classes-and-structs/static-classes-and-static-class-
  members` -- verbatim: "A static class is basically the same as a non-
  static class, but there's one difference: a static class can't be
  instantiated. ... Because there's no instance variable, you access the
  members of a static class by using the class name itself." Also:
  "Static classes are sealed and therefore cannot be inherited." Both
  invariants are desirable here.
- **Authoritative Dart.** `https://dart.dev/language/classes` -- Dart has
  no `static class` keyword; the source follows the conventional Dart
  idiom "class with only static members, never instantiated", which the
  conversion lifts to a compile-time invariant on the .NET side.
- **Conclusion.** Cache hit on the `rf-dart-static-only-holder-to-csharp-
  static-class` idiom established in abandon.dart.md and reused verbatim
  in commit.dart.md. Authoritative both sides; reuse, no escalation.

### rf-dart-named-required-ctor-with-defaults-to-csharp-positional-ctor-with-defaults -- SuspendGoalFCP / SuspendGoal named-required parameters (cache hit)

- **Deep analysis.** Both `suspendGoalFCP` (four named-required) and
  `suspendGoal` (three named-required) use the Dart `{required ...}`
  named-parameter form. C# has no per-parameter `required` keyword for
  methods (the C# 11 `required` modifier is for properties only); a
  positional parameter without a default IS required by the compiler.
- **Authoritative .NET.** `https://learn.microsoft.com/en-us/dotnet/csharp/
  programming-guide/classes-and-structs/named-and-optional-arguments` --
  verbatim: "Named arguments enable you to specify an argument for a
  parameter by matching the argument with its name rather than with its
  position in the parameter list." Call-site readability preserved via
  named-argument syntax (`SuspendOps.SuspendGoalFCP(heap: h, goalId: g,
  kappa: k, readerVarIds: s)`).
- **Authoritative Dart.** `https://dart.dev/language/functions#named-
  parameters` -- documents `{required Type name}` as the form for
  named-required parameters.
- **Conclusion.** Cache hit on the `rf-dart-named-required-ctor-with-
  defaults-to-csharp-positional-ctor-with-defaults` idiom established
  in machine_state.dart.md and reused in abandon.dart.md, commit.dart.md,
  heap_fcp.dart.md. Authoritative both sides; reuse, no escalation.

### rf-dart-shared-mutable-record-by-reference-to-csharp-class -- SuspensionRecord reference-sharing + VariableEntry mutation + linked-list prepend (cache hit)

- **Deep analysis.** The FCP-exact suspension design hinges on ONE
  `SuspensionRecord` reference being shared across all `SuspensionListNode`
  wrappers attached to suspended-variable cells. `suspendGoalFCP` allocates
  the record ONCE and hands the same reference to every
  `_suspendOnVariable` call; each call wraps it in a fresh
  `SuspensionListNode` and prepends to either the `VariableEntry
  .suspensions` chain (imported readers) or the writer's suspension chain
  (local readers, via `suspendOnWriter`). Disarming the record propagates
  to every wrapper because the wrappers share the record by reference.
- **Authoritative .NET.** `https://learn.microsoft.com/en-us/dotnet/
  csharp/fundamentals/types/classes` -- verbatim: "A class is a reference
  type. When an object of the class is created, the variable to which the
  object is assigned holds only a reference to that memory." This is the
  semantic foundation for the "ONE shared record, MANY wrappers" idiom.
  Also `https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/
  types/records` -- records' value-equality semantics would be WRONG here
  (the sharing depends on REFERENCE identity, not value identity).
- **Authoritative Dart.** `https://dart.dev/language/classes` -- Dart
  classes are reference types; sharing by reference is the default
  semantics matched by the source.
- **Conclusion.** Cache hit on the `rf-dart-shared-mutable-record-by-
  reference-to-csharp-class` idiom established in suspension.dart.md
  (where `SuspensionRecord` and `SuspensionListNode` are pinned as
  reference-type classes), reused in variable_table.dart.md (where
  `VariableEntry` is pinned likewise), and reused in heap_fcp.dart.md
  (the `SuspendOnWriter` / `SuspendOnReader` / `_ForwardSuspensions`
  bodies all rely on the same invariant). This file does NOT re-decide
  the mapping -- it CONSUMES the upstream decisions. Authoritative both
  sides; reuse, no escalation. The linked-list-prepend pattern
  (`node.Next = chain.Head; chain.Head = node;`) is the standard O(1)
  head-insert; codegen MUST preserve the assignment order (Next BEFORE
  Head) per the heap_fcp.dart.md precedent.

### rf-dart-unimplemented-error-to-csharp-notimplemented -- SuspendGoal deprecated stub (cache hit)

- **Deep analysis.** `suspendGoal` is preserved as a deprecated stub
  satisfying the pre-FCP API surface but throws `UnimplementedError`
  with a redirect message pointing to the FCP-exact replacement. The
  doc-comment "(deprecated)" marker captures the design intent.
- **Authoritative Dart.** `https://api.dart.dev/dart-core/
  UnimplementedError-class.html` -- verbatim: "Thrown by operations that
  have not been implemented yet." Extends `Error` (programming-defect
  signal).
- **Authoritative .NET.** `https://learn.microsoft.com/en-us/dotnet/api/
  system.notimplementedexception` -- verbatim: "The exception that is
  thrown when a requested method or operation is not implemented." Also
  `https://learn.microsoft.com/en-us/dotnet/api/system.obsoleteattribute`
  -- verbatim: "Marks the program elements that are no longer in use."
  The `[Obsolete]` attribute lifts the Dart doc-comment "(deprecated)"
  marker to a compile-time warning -- a tighter, faithful contract.
- **Conclusion.** Cache hit on the `rf-dart-unimplemented-error-to-csharp-
  notimplemented` idiom established in abandon.dart.md and reused in
  boot_loader.dart.md. INTENT-based mapping (both signal "intentionally
  not implemented in this layer"); .NET has no `Error` vs `Exception`
  hierarchy split. Authoritative both sides; reuse, no escalation. The
  `[Obsolete]` attribute is an ADDITION (not in the source) but is a
  strictly-correct narrowing: the Dart `(deprecated)` doc-comment +
  `UnimplementedError` body together unambiguously signal "do not call
  this", and the .NET attribute makes that signal compile-time-visible.

### rf-dart-int-to-csharp-long-width -- int -> long width widening (cache hit)

- **Deep analysis.** Every `int` in this file (`goalId`, `kappa`, `addr`,
  the `Set<int>` element type) maps to `long` in C# -- the recurring
  numeric-width convention pinned in cells.dart.md.
- **Authoritative Dart.** `https://dart.dev/language/built-in-types#numbers`
  -- Dart `int` is a 64-bit signed integer on native VM (no width parameter
  in the language; the spec mandates "fixed-width integers" with at least
  64 bits of precision).
- **Authoritative .NET.** `https://learn.microsoft.com/en-us/dotnet/csharp/
  language-reference/builtin-types/integral-numeric-types` -- `int` is
  32-bit, `long` is 64-bit; the faithful counterpart of Dart `int` is
  `long` to preserve the 64-bit precision.
- **Conclusion.** Cache hit on `rf-dart-int-to-csharp-long-width` established
  in cells.dart.md and reused throughout the corpus (abandon.dart.md,
  heap_fcp.dart.md, machine_state.dart.md, etc.). Authoritative both sides;
  reuse, no escalation.

## Notes

- **File-absent nuances** (deliberately not asserted): no `Stream`/
  `Future`/async/`isolate`, no `late`, no `mixin`, no `extension`, no
  generics-with-bounds, no `sealed` class introductions (the C# `static`
  modifier on `SuspendOps` already implies sealed), no bitwise/shift
  operations, no nullable-of-value-type scenarios, no value-equality
  contract, no `IDisposable`/resource-management, no LINQ surface (the
  body uses a plain `foreach` over a `Set<int>` -- no Where/Select needed).
- **Cross-file dependencies consumed (not re-derived) per FR-024 / SC-007.**
  This spec REUSES decisions from suspension.dart.md (`SuspensionRecord`
  + `SuspensionListNode` reference-type), variable_table.dart.md
  (`VariableEntry` reference-type + mutable `Suspensions` property),
  heap_fcp.dart.md (`HeapFCP.DerefAddr` / `HeapFCP.SuspendOnWriter`
  method names + return-type contracts), terms.dart.md (`VarRef` sealed
  reference-type + `Addr` property), machine_state.dart.md (`GoalId` /
  `Pc` global-using aliases). The convspec for `suspend_ops.dart` is
  a CONSUMER of those upstream decisions, not an originator.
- **Load-bearing semantic decisions (4 non-trivial constructs).**
  (1) `SuspendOps` -> `static class SuspendOps` (no-instantiation
  compile-time invariant; abandon.dart.md / commit.dart.md precedent).
  (2) `suspendGoalFCP` -> `SuspendGoalFCP` with shared-reference
  fan-out invariant (ONE `SuspensionRecord`, MANY `SuspensionListNode`
  wrappers; depends on `SuspensionRecord` being a reference-type class
  per suspension.dart.md).
  (3) `_suspendOnVariable` -> `private static void SuspendOnVariable`
  with three-branch dispatch (`is VariableEntry entry` / `is VarRef
  varRef` / silent no-op for ground); leading underscore dropped per
  heap_fcp.dart.md `_ForwardSuspensions` precedent; mutates `entry
  .Suspensions` in place using O(1) head-insert (order matters).
  (4) `suspendGoal` -> deprecated stub: `[Obsolete]` + `throw new
  NotImplementedException(...)` (abandon.dart.md precedent for
  `UnimplementedError` -> `NotImplementedException`); `[Obsolete]`
  attribute is a tighter narrowing than the source's doc-comment
  marker -- strictly correct.
- **All 5 non-trivial construct rows cite both deep-analysis AND
  authoritative-doc bases (Microsoft Learn for .NET, dart.dev / api.dart.dev
  for Dart) per SC-006. The doc-comment row is the only `trivial: true`
  construct.** The five imports are non-trivial-but-cache-hit (they consume
  the same `rf-dart-relative-import-to-csharp-namespace-using` idiom
  established by fairness.dart.md / suspend.dart.md / hanger.dart.md /
  variable_table.dart.md); they remain rows (not `trivial: true`) because
  the `show`-clause loss is a load-bearing nuance worth documenting per
  construct.
- **Zero escalations**: every non-trivial construct resolves from
  authoritative Dart and .NET documentation, with five established
  project idioms (`rf-dart-relative-import-to-csharp-namespace-using`,
  `rf-dart-static-only-holder-to-csharp-static-class`, `rf-dart-named-
  required-ctor-with-defaults-to-csharp-positional-ctor-with-defaults`,
  `rf-dart-shared-mutable-record-by-reference-to-csharp-class`, and
  `rf-dart-unimplemented-error-to-csharp-notimplemented`, plus the
  width-widening `rf-dart-int-to-csharp-long-width`) reused per SC-007.
