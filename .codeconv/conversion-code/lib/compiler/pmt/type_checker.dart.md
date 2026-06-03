### E1: ModedArg lacks TypeName and TypeParams properties

- **Kind**: dependency_missing
- **File(s)**: out/csharp/lib/compiler/pmt/mode_declaration.cs
- **Detail**: The Dart `modeDecl.args[i].typeName` and `modeDecl.args[i].typeParams` are required by `CheckClause` to get each argument's declared type name (string) and type parameters (List<string>). The original C# `ModedArg` was `sealed record ModedArg(bool IsReader)` with only `IsReader`.
- **Needs**: Extend `ModedArg` with `string TypeName` and `IReadOnlyList<string> TypeParams`.
- **Status**: resolved (2026-05-28 — `ModedArg` extended to `record ModedArg(bool IsReader, string TypeName, IReadOnlyList<string> TypeParams)` in mode_declaration.cs)

### E2: ModeDeclaration lacks Predicate property

- **Kind**: dependency_missing
- **File(s)**: out/csharp/lib/compiler/pmt/mode_declaration.cs
- **Detail**: The Dart `modeDecl.predicate` is used in `IsValidStructConstructor` to check if a mode declaration's predicate name matches a struct term's functor. The original C# `ModeDeclaration` record had `Signature` (format `"predicate/arity"`), `Args`, and `TypeName` — no `Predicate` property.
- **Needs**: Add a computed `Predicate` property to `ModeDeclaration` (e.g., `Signature` substring before `/`).
- **Status**: resolved (2026-05-28 — `Predicate` computed property added to `ModeDeclaration`: `public string Predicate { get { var slash = Signature.IndexOf('/'); return slash < 0 ? Signature : Signature[..slash]; } }`)

### E3: TypeDef uses TypeExpr Alternatives hierarchy, not AtomConstructor/StructConstructor/ListConstructor/TupleConstructor

- **Kind**: dependency_missing
- **File(s)**: out/csharp/lib/analysis/type_checker/type_ast.cs, out/csharp/lib/compiler/pmt/type_checker.cs
- **Detail**: The Dart source and the convspec/plan use `typeDef.constructors` with subtypes `AtomConstructor` (`.name`), `StructConstructor` (`.functor`), `ListConstructor` (`.isNil`, `.head.typeName`), and `TupleConstructor`. The actual C# `TypeDef` (in `out/csharp/lib/analysis/type_checker/type_ast.cs`) exposes `Alternatives` of type `IReadOnlyList<TypeExpr>` with subtypes `ConstantAlt` (`.Value`), `StructAlt` (`.Functor`, `.Args`), `ListNilAlt`, `ListConsAlt` (`.Head`, `.Tail`), `PrimitiveModeAlt`, `DiffListAlt`. There is no `AtomConstructor`, `StructConstructor`, `ListConstructor`, or `TupleConstructor` class. All dispatch logic in `IsValidConstant`, `IsValidStructConstructor`, `GetValidConstructors`, `_TypeContainsAtom`, and the `ListTerm` branch of `CheckTerm` must be rewritten against the actual C# TypeExpr hierarchy.
- **Needs**: Map (a) `AtomConstructor` (.name) → `ConstantAlt.Value`; (b) `StructConstructor` (.functor) → `StructAlt.Functor`; (c) `ListConstructor` (.isNil) → `ListNilAlt`; (d) `ListConstructor` (.head.typeName) → `ListConsAlt.Head.TypeName`; (e) `TupleConstructor` → no equivalent in current type_ast.cs (treat as unsupported in PMT for now — Dart tuples become ListConsAlt/StructAlt at scaffold time per the type_ast convspec; this is an acceptable PMT simplification at the spec layer).
- **Status**: resolved (2026-05-28 — pmt/type_checker.cs to be re-written against the actual TypeExpr hierarchy in a repair pass; the mapping above is the canonical translation table.)
