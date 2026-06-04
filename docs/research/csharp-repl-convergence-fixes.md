# C# REPL ↔ Dart convergence fixes (feature 023, 2026-06-04)

**Why this file (A5 follow-up).** While implementing `/glptutorial-run` (023) we found the
**mandated C# GLP REPL** (the Dart→C# conversion under `out/csharp/`) could not run most
tutorial programs — a cascade of Dart→C# *conversion regressions*, each masked behind the
prior. Gabi approved **Option C**: hand-converge `out/csharp` to the authoritative Dart
source-of-truth (`D:/bstdev/research/glp/GLP/glp_runtime/`), verified by a deep per-golden
sweep. These fixes were applied **by hand** to the committed `out/csharp` tree.

**`out/csharp` is codegen-generated** (codeconv Dart→C# pipeline; currently *held*). When any
of these files is regenerated, the convspec/plan/codegen for it MUST encode the idiom below,
or the bug returns. This file is the checklist for that.

Verification: empirical sweep of all 38 implemented examples (ch01–06 + ch07/01–07) drove the
C# REPL vs each golden, cross-checked against the live Dart oracle. Result after these fixes:
runtime semantics clean — 0 C#-bugs, 0 regressions; remaining non-matches are corpus/cosmetic
(see end).

## Fixes (each = converge C# to the authoritative Dart behavior)

| # | File (out/csharp) | Divergence | Fix (match Dart) | Commit |
|---|---|---|---|---|
| 1 | `bin/glp_repl.cs` | hardcoded `../../programs/self.glp` overshot the exe layout → `self.glp` never found → **empty prelude** (every prelude type `UnknownType`) | port Dart `_resolveRootSelfGlpPath()` — walk up from `AppContext.BaseDirectory` to the ancestor containing `programs/self.glp`; throw if absent; print resolved path | c2737113 |
| 2 | `lib/analysis/type_checker/prelude.cs` | `PredefinedProcedureNames`/`BuiltinProcedures` missing `tuple`,`is_list`,`tuple/1`,`is_list/1` (clause-less builtins rejected: "has no clauses") | add the 4 entries to match `prelude.dart` | c2737113 |
| 3 | `lib/analysis/type_checker/program_dfa.cs` | `Any` builtin type entirely absent (states/automata/`IsAnyType`/leaf arms) → `UnknownTypeError: Any` | add `Any`/`Any?` states+automata, `IsAnyType`, `IsPrimitiveType` incl. Any, acceptedPrimitives incl. `"Any"`, the two leaf-consistency Any arms — per `program_dfa.dart` | aa2e958f |
| 4 | `lib/bytecode/runner.cs` (guard dispatch) | `is_list`/`tuple` not handled → `[WARN] Unknown guard predicate` + wrong result | add `case "is_list":`→shared with `list`, `case "tuple":`→shared with `compound` (per `runner.dart`) | 0013d1c0 |
| 5 | `lib/bytecode/runner.cs` (HEAD/UNIFY constant match) | type-strict `object.Equals(ct.Value, op.Value)` → `Equals(double 0.0, long 0)` is false → recursive base clause (e.g. `producer([],0)`) never matched an arithmetic-produced count → tail left open → spurious `→ failed` | `NumEquals(a,b)` helper (numeric cross-type compare like Dart `num==`; else `object.Equals`) at all 8 constant-match sites | 2c5a2224 |
| 6 | `lib/analysis/type_checker/moded_term.cs` | `ModedPath.ToString()` rendered mode via default enum `.ToString()` → PascalCase `Input`/`Output` | use `Mode.AsModeString()` (lowercase `input`/`output`) — the helper that already documents "Do NOT use mode.ToString()" | 7b7942de |
| 7 | `lib/runtime/body_kernels.cs` + `bin/glp_repl.cs` (printer) | arithmetic widened int→double; printer dropped `.0` from whole doubles | int-preserving `GetNumeric`/`NumAdd/Sub/Mul/Neg/Abs`/`EvaluateArithmeticNum` (`/` stays double, per Dart `num`); printer `FormatDartDouble` (whole double → "N.0") | (this commit) |

Final sweep after all 7 fixes: **33/38 MATCH, 0 C#-runtime bugs, 0 regressions.** The 5 non-MATCH
are corpus/cosmetic only (ch04/07 spec-violation, ch04/08 stale golden, ch02/01+ch05/06+ch05/07
residual error-text) — see "Deferred" below.

## Root primitive-type seeding (reference — was the load-bearing mechanism)
`Constant`/`Stream`/`Channel`/etc. are NOT hardcoded; they are parsed from `programs/self.glp`
into the prelude `TypeEnvironment` via the engine ctor's `SetPreludeEnvironmentSource(self.glp)`
→ `BuildPreludeEnvironment()`, used by both single-file (`LoadSource`) and project (`LoadProject`)
paths through the shared `ancestorScope ?? BuildPreludeEnvironment()` funnel. Only
`Integer/Real/Number/String` are hardcoded (`TypeRef.Builtins`); `Any` is a hardcoded DFA state.
Fix #1 is what makes self.glp actually load — without it, the whole prelude env is empty.

## Deferred / not-changed (decision 3 → fold here; decision 2 → 023 `propose`)
- **Printer/error-text residuals (cosmetic):** bytecode dump bool `False`→`false` (ch05/06, a
  *deferred* bytecode-dump shape); the `Exception:` prefix on type-error messages; ch02/01's
  final SRSW line missing ` at Line 0, Column 0`. → converge at next regen or normalize in 023's
  load-failure comparison.
- **`glp_engine.cs` `_LoadRootSelf`** silently swallows a missing/failed prelude where Dart throws
  (`StateError`). Latent (fix #1 makes self.glp resolve); converge for loud-failure fidelity.
- **`project_linker.cs`** file-discovery/ancestor-sort order differs from Dart (only matters for
  multi-module collisions; not hit by the corpus).
- **Corpus-golden issues (NOT C# bugs) → 023 `propose`:** ch04/07 uses a 2-clause `natural_number/1`
  as a guard (spec-invalid per manual §8; C# + current Dart both reject; golden's `✓ Loaded` is from
  a stale Dart exe) — flag spec-violation. ch04/08 flatten golden predates the `is_list` fix (C# now
  equals live Dart `F=[5,4,3,2,1]`) — re-capture.
