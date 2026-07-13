# Feature 050-full-gleam-combined — T018 Handover (gavri → Olamnit)

**Date:** 2026-07-12
**Author:** Claude session on gavri (192.168.0.108)
**Status:** In Progress — T018 chunked, chunks A+B done, next unit = program_dfa
**Purpose:** gavri is switching to the two-host QUIC acceptance run; Olamnit continues this feature.

---

## Session bootstrap on Olamnit

1. Mandatory reading per CLAUDE.md, in order: `CLAUDE.md`, `docs/DISCIPLINE.md`, `docs/typed-glp-manual.md`, `docs/glp-cheat-sheet.md`.
2. `git fetch origin && git checkout 050-full-gleam-combined` — pushed through `2b56ad71`. Everything feature-load-bearing is committed; Olamnit needs nothing else from gavri for this feature.
3. Spec dir: `specs/050-full-gleam-combined/` — **`tasks.md` checkboxes + the commit trail are the position truth.** Read `plan.md` for tech stack and structure.

### Machine-local state that does NOT travel (important)

- **Marathon run `mrun-a78ed68b1fca`** lives in gavri's deploy-home catalog (`C:\Users\gavri\AppData\Local\buildkit\deploy-home\targets\85444bd44ab0\`). `buildkit-marathon resume` on Olamnit will NOT find it. Continue from `tasks.md`; escalate to Gabi how the marathon trail should be recorded (retroactively on gavri, or start without trail on Olamnit). Do not pick a side silently.
- **Pipeline/sidecar state** is machine-local too. On gavri the implement stage rows record complete (2026-07-11T17:19Z); each bk-implement leg re-opens with `python -m buildkit_cli.pipeline.sidecar start implement --force` (set `PYTHONUTF8=1`). If Olamnit's sidecar answers differently, surface it to Gabi rather than working around.
- gavri leftovers (NOT needed by Olamnit): uncommitted `claude.manifest.json` auto-churn, 8 EOL-only `M` entries with empty diffs, untracked `glp_gleam/lean/DistDerefConvergence/lake-manifest.json` (T027 lake build artifact), `stash@{0}` = T017 accidental WSL format drift.

---

## Position

Done, with commits: Phase 1 `b8ff02b0` · Phase 2 `576e00dc` · T013 lexer `c7965ba7` · T014 parser `90668f28` · T015+T016 parser tests + SRSW `cc0b3eb7` · T017 partial evaluator `fa65106b` · T027 Lean writer-MGU proof + T028 PROOF.md `8e08f4ab` (T028 checkbox stays OPEN — INDEX.md flip deferred to the T026 four-artifact discharge commit per contract).

**T018 IN PROGRESS** (checkbox stays open until all units land):
- Chunk A `c00547ee`: `analysis/type_checker/mode.gleam`, TypeEnvironment ported INTO `analysis/type_ast.gleam`, `analysis/prelude.gleam` predefined-type/procedure + builtin-goal sets. WSL gleam test 221/221.
- Chunk B `2b56ad71`: `analysis/type_checker/param_expansion.gleam` full port (insertion-ordered instantiation worklist threaded through substitution), Dart `TypeExpr.toString` port in type_ast (`Stream<Msg>` canonical names), ProcDecl copies drop `isBuiltin` like Dart. WSL gleam test 227/227.

**T018 REMAINING, in dependency order** (Dart originals in `glp_runtime/lib/analysis/type_checker/`, Gleam targets under `glp_gleam/src/glp/analysis/type_checker/`):
1. `program_dfa.dart` (~25K) ← **NEXT UNIT (chunk C)**
2. `subtyping.dart` (~4K)
3. `moded_term.dart` (~15K) / `moded_head.dart` (~17K)
4. `well_typed_term.dart` (~16K) / `well_typed_clause.dart` (~36K)
5. `type_environment_builder.dart` (~22K)
6. `clause_validation.dart` (~2K)
7. `type_checker.dart` checkModule entry (~22K) + REPL-verified message conformance

After T018: T019 codegen (Analyzer STEP 3 reduce-gen + STEP 4 annotate live there) → T020 loader → T021–T024 engine → T025–T026 (INDEX flip + T028 close) → T029 facade → T030 acceptance.

---

## Porting conventions established (follow exactly)

- Error/diagnostic message strings must be **byte-identical to the Dart REPL** (`dart run glp_runtime/bin/glp_repl.dart` — the pre-built `glp_repl.exe` is stale and cannot even boot; do not use it). Verify each checker unit's error channels against the Dart oracle.
- `compiler/partial_eval.gleam` ports BOTH live Dart PE copies: `partial_evaluator.dart`'s runs in GlpEngine.loadSource BEFORE typecheck (049 guard admission, `PE<n>` fresh names, only bare `_` anonymous, Remote/Spawn preserved, phase 'partial_evaluator' = NO [category] prefix); `analyzer.dart`'s embedded copy runs in Analyzer.analyze STEP 2 feeding codegen (`PE_<n>`, all `_`-prefixed anonymous, no admission, Remote/Spawn flattened, phase 'analyzer' = `[semantic]` prefix). T020 loader must call each where Dart does. `unfoldReduceCalls` is DEAD in Dart — intentionally not ported.
- `parser/ast.gleam` has `term_to_string`/`const_value_to_string` (Dart toString ports — atoms render double-quoted in diagnostics); reuse, don't duplicate.
- REPL scratch programs for oracle checks: a `procedure` decl must be IMMEDIATELY followed by its clauses (helpers go before the decl).
- Commit per chunk, staged by name, single-line message in the existing style: `impl(050): T018 chunk C - program_dfa.gleam port (WSL gleam test N/N)`. Push each chunk.

## Test protocol / environment

- 🔴 `gleam test` = **WSL only**. `glp_gleam/build/` is SHARED by native-Windows gleam and WSL gleam and their artifacts are incompatible — delete the gitignored `glp_gleam/build/` before switching platforms.
- 🔴 NEVER run bare `gleam format` under WSL (it rewrites committed files CRLF→LF + format drift; T067 owns the format pass).
- Gleam `string.to_graphemes` yields CRLF as ONE grapheme `"\r\n"` — lexer handles it (regression test exists); keep this in mind for any new string-walking code.
- Green counts: WSL gleam test 227/227 at `2b56ad71`. Dart suite baseline 528/529 — Section Q AOT smoke ex-01 is a PRE-EXISTING reported failure (stale AOT exe suspicion), NOT 050-caused; do not obscure it.
- gavri-specific PATH quirks (adjust for Olamnit's own install locations): tool shells lack the BEAM toolchain; gavri prepends winget Gleam + `C:\Program Files\Erlang OTP\bin` + rebar3. Dart suite on gavri: `DART=/c/Users/gavri/dart-sdk/bin/dart.exe bash test/run_all_tests.sh`.
- Lean convention: lakefile + `lean-toolchain` pin `leanprover/lean4:v4.30.0`, no mathlib.

## Open items already reported to Gabi (do not self-resolve)

- Spec gap: v2.16 bytecode doc lacks sections for 6 live opcodes (NoReaders/GroundEqual/PutBoundConst/PutBoundNil/Distribute/Transmit).
- Pre-existing Dart baseline failure above.

## Next concrete action

Port `glp_runtime/lib/analysis/type_checker/program_dfa.dart` → `glp_gleam/src/glp/analysis/type_checker/program_dfa.gleam` + tests, WSL gleam test green, commit as T018 chunk C. Then continue the dependency order above.
