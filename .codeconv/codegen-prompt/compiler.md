```yaml
generated_at: '2026-06-03T00:00:00Z'
metric_score: null
model: claude-in-session
optimizer: seed-authored
provenance_note: >-
  Authored seed for the `compiler` subsystem (lib/compiler/, lib/analysis/,
  lib/lint/), descended from _base.md. Idioms from the 2026-05-28 bulk drive
  (TypeDef/LookupType naming; pmt/mode_declaration resolution, commits
  3a18e6f3/d6d442ad/dd5ad5f).
schema_version: 1
seed_from: _base.md
source: bulk-drive-idioms
subsystem: compiler
```

Convert one Dart source in `lib/compiler/`, `lib/analysis/`, or `lib/lint/`
(the compiler proper + the static-analysis tower: type-checker, partial
evaluator, SRSW/PMT, lint) to real, compilable C#/.NET 10. Emit REAL C# ONLY.
Honor the shared base discipline.

## Naming idioms (ratified KB)

- `getType` → `LookupType` (the project-wide `getX`→`LookupX` rename); apply at
  both the definition and every call site.
- `TypeDefinition` → `TypeDef` (the emitted type name; the plan prose may say
  `TypeDefinition` but the built `ast.cs` exposes `TypeDef`). Read the built
  `ast.cs` — do not trust the plan's name over what was emitted.
- Keep `*Error` names verbatim.

## `pmt/mode_declaration` (a dependency that was absent from Dart)

`ModeDeclaration` + `ModedArg` are NOT defined in glpnet's Dart sources
(referenced from `pmt/{mode_table,type_checker}.dart` but never defined; the
canonical shape lives in the sibling GLP repo). The resolution created
`out/csharp/lib/compiler/pmt/mode_declaration.cs` with:
`ModedArg(IsReader, TypeName, TypeParams)`,
`ModeDeclaration(Signature, Args, TypeName)` + a computed `Predicate` property,
and a `Module.ModeDeclarations()` extension. Use those exact shapes; do NOT
add them to `ast.cs` (keeps the already-built `ast.cs` untouched).

## Semantics

The analysis tower emits / shapes the bytecode spine. Preserve the
type-checker, partial-evaluator, SRSW, and PMT semantics exactly — these
decide what bytecode the runner later executes, so a divergence here surfaces
downstream as a trace divergence. Escalate rather than approximate a checking
rule.
