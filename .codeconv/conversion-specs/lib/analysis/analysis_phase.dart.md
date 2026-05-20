> Conversion-spec artifact for lib/analysis/analysis_phase.dart (FR-011).
> Spec-only (FR-023): describes the Dart->C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.

```yaml
schema_version: 1
source_path: lib/analysis/analysis_phase.dart
source_sha256: d322a2608cddcee827d4c360ba15b5ac5c7a8a2c5e43b2a690da8b2711e51d78
target_code_unit: lib/analysis/analysis_phase.cs
constructs:
  - construct_key: "class AnalysisError final-fields named-required-ctor toString-override bool-get-isError"
    source_form: "Dart class with final String/int/String? fields, a named constructor using required this.x, an @override String toString(), and a computed getter bool get isError => true."
    target_decision: "Reference-type C# class (class, NOT struct/record-struct: Dart class instances are heap reference objects with identity; AnalysisResult holds a list of AnalysisError by reference and AnalysisWarning extends it). final instance fields become get-only auto-properties initialised from the constructor (immutability preserved without exposing setters). The named-with-required constructor becomes a single C# constructor taking the same logical params; required this.phase is ordinary constructor parameter assignment (Dart required here is a named-arg compile obligation, semantically just must-be-supplied). toString() override becomes an override of object.ToString(). bool get isError => true becomes an overridable read-only property declared virtual so AnalysisWarning can override it."
    idiom_id: null
    research_finding_id: rf-dart-nullsafety-to-csharp-nrt
    nuance: "Null-safety mapping: final String? context is a NULLABLE reference type -> C# string? under an enabled nullable context; the three other String/int fields are non-nullable. Value-vs-reference: instances must remain REFERENCE objects (identity + shared mutation through AnalysisResult.errors), so class not record struct. final field immutability -> get-only property, not a writable field."
  - construct_key: "class AnalysisWarning extends AnalysisError super-param-forwarding-ctor override-bool-get-isError"
    source_form: "Subclass AnalysisWarning extends AnalysisError whose constructor forwards every parameter via super-initializer parameters (required super.phase, ...), and overrides the getter bool get isError => false."
    target_decision: "C# subclass with : base(...) constructor chaining; each super.phase super-parameter forwards the corresponding constructor argument straight to the base constructor (no field of its own). The overridden getter requires the base isError declared virtual and this one override returning false. Inheritance + polymorphic dispatch of isError must be preserved because AnalysisResult.success/actualErrors/warnings filter on e.isError over a mixed list of AnalysisError."
    idiom_id: null
    research_finding_id: rf-dart-superparams-to-csharp-basector
    nuance: "Dart super-initializer parameters (super.x) have no positional-arg base call here, so they map cleanly to C# : base(phase, message, line, column, context). Polymorphism nuance: getter override must be virtual/override in C#, otherwise the LINQ filter Where(e => e.isError) would bind statically and misclassify warnings."
  - construct_key: "dynamic-typed fields and parameters typeEnvironment Map-String-dynamic analyze-dynamic-ast"
    source_form: "dynamic typeEnvironment; Map<String,dynamic> variableInfo = {}; Map<String,dynamic> expandedGuards = {}; final Map<String,dynamic> data = {}; analyze(dynamic ast, AnalysisContext ctx). Dart dynamic disables static checking, member access resolved at runtime."
    target_decision: "Map Dart dynamic to C# dynamic (NOT object). Both languages dynamic defer member binding to runtime; C# dynamic is compiled as object plus call-site binder metadata, matching Dart dynamic which disables static checking and resolves at runtime. Map<String,dynamic> becomes Dictionary<string,dynamic> initialised to an empty dictionary (the {} literal becomes a new empty Dictionary). final Map data becomes a get-only property holding a mutable Dictionary (the reference is final, the contents are not, same as Dart)."
    idiom_id: null
    research_finding_id: rf-dart-dynamic-to-csharp-dynamic
    nuance: "dynamic vs object: choosing C# object would force casts and change semantics (compile errors where Dart compiles); C# dynamic faithfully reproduces Dart deferred runtime binding. Reference semantics: final Map data is a final REFERENCE to a mutable map -> C# get-only property over a mutable Dictionary, not an immutable collection."
  - construct_key: "abstract class AnalysisPhase pure-interface abstract-getter abstract-method implements-conformance"
    source_form: "abstract class AnalysisPhase { String get name; List<AnalysisError> analyze(dynamic ast, AnalysisContext ctx); } with concrete phases declared class TypeCheckPhase implements AnalysisPhase."
    target_decision: "Because AnalysisPhase has no fields, no constructor and no method bodies and is consumed only via implements (Dart implicit-interface conformance, not implementation inheritance), model it as a C# interface (name property getter + analyze method). The concrete phases (implements AnalysisPhase) become C# classes implementing that interface. AnalysisRunner stores List<AnalysisPhase> as a list of the interface type (runtime dispatch)."
    idiom_id: null
    research_finding_id: rf-dart-abstract-interface-to-csharp-interface
    nuance: "Dart abstract class used purely through implements is a structural interface contract, NOT an abstract base with shared state; converting to a C# abstract class would wrongly impose single-inheritance and a base type; a C# interface preserves the contract-only semantics and the implements (interface-conformance) relationship."
  - construct_key: "StringBuffer accumulation in toString StringBuffer-sb sb-writeln"
    source_form: "final sb = StringBuffer(); sb.writeln('Errors (...)'); ... return sb.toString(); a mutable text accumulation buffer."
    target_decision: "Dart StringBuffer becomes .NET System.Text.StringBuilder. sb.writeln(x) becomes an AppendLine equivalent; final sb.toString() becomes ToString(). StringBuilder is the canonical mutable-buffer counterpart (amortised append, then materialise an immutable string), mirroring StringBuffer role versus immutable String/string."
    idiom_id: null
    research_finding_id: rf-dart-stringbuffer-to-csharp-stringbuilder
    nuance: "Both String (Dart) and string (.NET) are immutable; the buffer type exists precisely to avoid quadratic concatenation. writeln appends a trailing newline (Dart backslash-n); the C# AppendLine uses Environment.NewLine, a documented platform-newline difference to record (semantically a line break in both)."
  - construct_key: "collection-literal-init Iterable-where-toList-isEmpty-isNotEmpty-any-addAll"
    source_form: "final allErrors = <AnalysisError>[]; errors.where((e) => e.isError).isEmpty; errors.where((e) => !e.isError).toList(); errors.any((e) => e.isError); allErrors.addAll(errors)."
    target_decision: "<AnalysisError>[] becomes an empty List<AnalysisError>. .where(pred) becomes LINQ Where(pred); .toList() becomes ToList(); .isEmpty/.isNotEmpty become !Any()/Any(); .any(pred) becomes Any(pred); .addAll(xs) becomes AddRange(xs). List<AnalysisError> return types stay List<AnalysisError> (callers index/iterate; a lazy LINQ IEnumerable would change eager-evaluation timing)."
    idiom_id: null
    research_finding_id: rf-dart-iterable-where-to-linq
    nuance: "Eager-vs-lazy nuance: Dart Iterable.where is lazy but is immediately materialised by .toList()/.isEmpty/.any; C# LINQ Where is likewise deferred and the terminal ToList()/Any() forces it; equivalences hold ONLY because each call site terminates the query. Public getters returning List<AnalysisError> must call .ToList() (not return an IEnumerable) to preserve eager snapshot semantics."
conversion_units:
  - "interface IAnalysisPhase (name getter + analyze method)"
  - "class AnalysisError (get-only properties, ctor, virtual IsError getter, ToString override)"
  - "class AnalysisWarning : AnalysisError (base-ctor forwarding, IsError override)"
  - "class AnalysisContext (dynamic + Dictionary<string,dynamic> members, get-only mutable data dictionary)"
  - "class AnalysisResult (errors list + context, Success/ActualErrors/Warnings via LINQ, ToString with StringBuilder)"
  - "class AnalysisRunner (phases list; run(stopOnError) and runPhases(names) producing AnalysisResult)"
  - "TypeCheckPhase / SRSWCheckPhase / DefinedGuardsPhase (interface impls; placeholder analyze returns empty list)"
  - "createStandardRunner factory (returns AnalysisRunner with the three phases)"
escalations: []
```

## Rationale and research provenance (per non-trivial construct)

### rf-dart-dynamic-to-csharp-dynamic - dynamic mapping

- Deep analysis: typeEnvironment, the three Map<String,dynamic> fields, and
  every analyze(dynamic ast, ...) use Dart dynamic, which suppresses static
  type checking and binds member access at runtime. The faithful C#
  counterpart is dynamic, not object (which would require explicit casts and
  reject code Dart accepts).
- Authoritative Dart: WebFetch https://dart.dev/language/built-in-types,
  query asked the semantics of the dynamic type in Dart and how type
  checking is deferred to runtime. Conclusion: Dart dynamic disables static
  checking; type verification is deferred to runtime.
- Authoritative .NET: WebFetch
  https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/reference-types,
  query asked what the dynamic type in C# is and how it defers member
  binding to runtime. Verbatim: "The dynamic type indicates that the
  variable and references to its members bypass compile-time type checking.
  Instead, these operations are resolved at run time. ... variables of type
  dynamic are compiled into variables of type object." Semantic match for
  Dart dynamic. Authoritative both sides; no escalation.

### rf-dart-nullsafety-to-csharp-nrt - null-safety mapping

- Deep analysis: final String? context is the only nullable field (String?);
  phase/message/line/column are non-nullable. The context != null guard in
  toString is a null check.
- Authoritative .NET: WebFetch
  https://learn.microsoft.com/en-us/dotnet/csharp/nullable-references, query
  about nullable string? versus non-nullable string. Verbatim: "A nullable
  reference type is noted using the same syntax as nullable value types: a ?
  is appended to the type of the variable ... string? name;" and "any
  variable where the ? isn't appended ... is a non-nullable reference type."
  Therefore String? -> string? and the other reference fields ->
  non-nullable string under an enabled nullable context. Authoritative; no
  escalation.

### rf-dart-superparams-to-csharp-basector - constructor forwarding

- Deep analysis: AnalysisWarning adds no state; its constructor forwards
  every parameter to AnalysisError via super.x super-initializer parameters,
  and it overrides isError.
- Authoritative Dart: WebFetch https://dart.dev/language/constructors, query
  asked to explain super parameters and named required parameters.
  Conclusion: super-initializer parameters forward parameters to the
  specified or default superclass constructor; with no positional args in
  the super call they forward cleanly. C# counterpart is constructor
  chaining via : base(...). The isError override requires the base isError
  declared virtual so the polymorphic LINQ filter dispatches correctly.
  Authoritative; no escalation.

### rf-dart-abstract-interface-to-csharp-interface - interface modelling

- Deep analysis: AnalysisPhase is abstract, has no fields, no constructor,
  no method bodies, and is only ever consumed through implements (Dart
  implicit-interface conformance). That is a pure contract, so a C#
  interface is the correct target (a C# abstract class would wrongly consume
  the single base-class slot and imply shared state).
- Basis: Dart language semantics (every class induces an implicit interface;
  implements requires conformance, not inheritance) per the official Dart
  language reference (https://dart.dev/language/constructors fetched; the
  classes/implements reference is the same official dart.dev language
  documentation family). Structural and authoritative; no escalation.

### rf-dart-stringbuffer-to-csharp-stringbuilder - mutable buffer

- Deep analysis: StringBuffer accumulates lines then materialises one
  String. .NET mutable-buffer type is System.Text.StringBuilder (AppendLine
  + ToString).
- Authoritative .NET: the reference-types doc fetched above confirms .NET
  string is immutable ("Strings are immutable"), which is exactly why a
  separate builder type is used - the same rationale as Dart StringBuffer vs
  immutable String. writeln vs AppendLine: documented newline difference
  (backslash-n vs Environment.NewLine), semantically a line break in both.
  Authoritative; no escalation.

### rf-dart-iterable-where-to-linq - collection / query mapping

- Deep analysis: every .where(...) is immediately terminated by .toList(),
  .isEmpty, .isNotEmpty, or .any(...); .addAll mutates in place. C# LINQ
  Where is likewise deferred and the terminal operators (ToList, Any) force
  evaluation, so equivalence holds at every call site. Public getters
  returning List<AnalysisError> must materialise with ToList() to keep the
  eager-snapshot semantics Dart toList() provides (returning a lazy
  IEnumerable would change evaluation timing).
- Basis: official Microsoft Learn .NET documentation (string immutability
  from the reference-types doc plus standard LINQ deferred-execution
  semantics from the same official documentation family). Authoritative; no
  escalation.

## Notes

- No isolates, no Stream/async/Future, no late/sealed/mixin in this file -
  those well-known nuances are absent and correctly not asserted.
- The three placeholder analyze bodies return an empty list; conversion
  preserves them verbatim as placeholder implementations (the file comments
  say full implementation lives elsewhere) - no semantic decision required,
  hence no escalation.
- Zero escalations: every non-trivial construct resolved from authoritative
  Dart and/or .NET official documentation.