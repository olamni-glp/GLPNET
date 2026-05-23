# Aggregated Conversion-Plan Escalations

Open escalations across all conversion-plan artefacts. Each
MUST be resolved by the engineer before the corresponding file
is converted (FR-016/FR-017). Planning may proceed; conversion
is blocked until these are resolved.

Total open escalations: 2

## bin/glp_repl.dart.md — E1: Convspec absent — `bin/glp_repl.dart` has no ratified conversion spec

- **File(s)**: bin/glp_repl.dart
- **Observed**: `Glob .codeconv/conversion-specs/bin/glp_repl.dart.md` returns no files. This file was the lone `blocked_on_deps=1` in the 018 live-pass inventory (see CLAUDE.md commit `12a468f5` aggregate: "scaffolded=128 specced=1 blocked_on_deps=1 (bin/glp_repl.dart)"). Plan was produced best-effort from the .dart source plus the convspecs of its five dependencies (`glp_engine`, `scheduler`, `terms`, `boot_loader`, `isolate_manager` — all ratified); §2 maps every Dart construct to a C#/.NET construct using only ratified dependency APIs plus standard .NET BCL idioms; the single non-obvious choice — `Platform.script.resolve(...)` → `AppContext.BaseDirectory` — is the standard pattern.
- **Why not pre-specified+incremental**: The `/codeconv-planagents` workflow's authority chain (R1) is plan-derived-from-convspec; bypassing convspec for one file is a workflow-level choice, not a verbatim-derivable per-construct fix. Whether to accept the plan as-is or to generate a retroactive convspec is a process decision, not encoded in spec / 012/015 contract / CLAUDE.md.
- **Decision required**: Accept this best-effort plan as-is and proceed to scaffold (plan is fully self-contained), OR generate a retroactive convspec for `bin/glp_repl.dart` to formally close the 018 `blocked_on_deps` slot before scaffolding?
- **Source**: [bin/glp_repl.dart.md#e1](bin/glp_repl.dart.md#e1)

## lib/analysis/type_checker/type_ast.dart.md — E1: `TypeEnvironment.GetType(string)` name collides with `object.GetType()`

- **File(s)**: lib/analysis/type_checker/type_ast.dart
- **Observed**: Dart source line 305 defines `TypeDef? getType(String name) => types[name];`. The natural .NET-PascalCase rename is `GetType(string name)`, but `object` already exposes a parameterless `Type GetType()` method (overloaded on arity, so it would technically compile — `env.GetType(name)` resolves to the new overload — but it permanently shadows `env.GetType()` on the typed receiver, and obscures runtime-type introspection at every call site).
- **Why not pre-specified+incremental**: convspec is silent on this collision (it lists `GetType` in `conversion_units` without flagging it); no written project-wide convention in CLAUDE.md or 018 plan addresses C# `object` member shadowing; the options (`LookupType` / `FindType` / `TryGetType(out)` / accept the shadowing) are all defensible and would each propagate to callers in ten lib files — a small but real cross-file API decision that should be made once, project-wide.
- **Decision required**: Should `TypeDef? getType(String name)` be renamed in the C# port to avoid shadowing `object.GetType()` (and if so, to what — `LookupType` / `FindType` / `TryGetType(name, out var td)`), or is the shadowing accepted (keeping `GetType(string)` as the literal PascalCase translation, accepting that `env.GetType()` callers will need `((object)env).GetType()`)? The same question implicitly applies to all other Dart members named `getX` that translate to `GetX` and may collide with framework members — recording a project-wide rule here would avoid recurrence.
- **Source**: [lib/analysis/type_checker/type_ast.dart.md#e1](lib/analysis/type_checker/type_ast.dart.md#e1)

