# 050 full-Gleam — T021 restart note (2026-07-12)

**Objective restart bootstrap** for continuing feature `050-full-gleam-combined` on
**Olamnit** at **T021 slice 21c**. The source of truth is git + `specs/050-full-gleam-combined/tasks.md`
checkboxes/◇-notes — this doc is the bootstrap + next-slice plan, not a work ledger.

## Where things stand (objective)

- Branch **`050-full-gleam-combined`**, tree clean, **pushed** to origin @ **`d38d65ae`**.
- Recent commits: `9ae0979c` T019 codegen · **`75f35a70` T020 loader** · **`d38d65ae` T021 slice 21a/b**.
- `tasks.md`: Phases 1–2 ✅; US1 T013–T020 ✅; T027 ✅ (Lean PI:14); **T021 IN PROGRESS**
  (◇-note on the T021 line records the slice status). T021 is the only open in-flight task.
- Native gleam test baseline: **339 / 339, warning-free** (`gleam test` in `glp_gleam/`).

## What T021 slice 21a/b delivered (`d38d65ae`)

`glp_gleam/src/glp/engine/runner.gleam` — immutable recursive stepper porting Dart
`runWithStatus`/`RunnerContext`. Implemented: control spine (Label/Nop/ClauseTry/ClauseNext/
TryNextClause/Commit/Proceed/Halt/NoMoreClauses + `find_next_clause_try` forward scan),
HEAD-constant path (`HeadConstant`/`HeadNil`: σ̂w writer-bind / Si reader-defer / ground compare),
two-phase Si-resolution `Commit` applying σ̂w via `heap.bind_writer`/`bind_writer_to_var`,
`no_more_clauses` suspend-or-fail. **Unported opcodes → `RunnerError(Unimplemented)`** (surfaced).
Tests: `test/glp/engine/runner_test.gleam` runs `flip` end-to-end (clause-1 bind, clause-2 via
soft-fail, suspend-on-unbound-reader).

**Documented adaptation:** Si/U and `Suspended(on:)` carry **writer** addresses (the Gleam
foundation is writer-keyed — `heap.suspend_on_writer`/`bind_writer` reactivate on writer bind;
`deref` of an unbound reader yields its terminal writer). Same observable behaviour as the
Dart's reader-addr-then-map model.

## NEXT — resume here

1. **T021 slice 21c** (HEAD structures + clause-var unify): `HeadStructure`/`HeadList`/
   `UnifyVariable`/`UnifyConstant`/`UnifyVoid`/`Push`/`Pop` (the `_TentativeStruct` + S/mode
   machinery) and `GetVariable`/`GetValue`. **This is the writer-MGU crux** (PI:14): reader×reader→
   fail, writer×writer→soft-fail, `_TentativeStruct`→`StructTerm` conversion at Commit incl.
   `_ClauseVar` resolution. Add adversarial gleeunit for those failure paths (feeds T026).
2. **T021 slice 21d** (BODY): `PutVariable`/`PutConstant`/`PutStructure`/`PutList`/`SetVariable`/
   `SetConstant`/`Spawn`/`Requeue` + commit-woken/reactivation wiring; run a body-bearing goal
   (e.g. `merge/3`) end-to-end. Close T021.
3. Then **T022** scheduler (`engine/scheduler.gleam`), **T023** guards, **T024** kernels,
   T025/T026 tests, T028 prose proof, T029 engine facade, T030 US1 acceptance.

**Porting reference (READ FIRST for 21c/21d):**
`docs/research/glp-gleam-baseline/runner-dart-architecture-map.md` — the verified structural map
of Dart `runner.dart` (all handlers, line-cited). It is a navigation aid: **verify each handler
against `glp_runtime/lib/bytecode/runner.dart` at port time** before porting (frozen semantics —
gaps STOP and escalate).

## Environment (Olamnit — differs from earlier handovers)

- **NO WSL** on Olamnit. Gabi-approved: **native-Windows gleam 1.17.0 exclusively.**
- `gleam` binary: `C:\Users\smbuser\AppData\Local\Microsoft\WinGet\Packages\Gleam.Gleam_Microsoft.Winget.Source_8wekyb3d8bbwe\gleam` (on PATH via the **Bash tool**, which is Git Bash here — NOT PowerShell `bash`, which is WSL).
- Build/test: `cd glp_gleam && gleam test`. Format specific files only: `gleam format <paths>` — **never bare `gleam format`** (would reformat the whole tree, incl. other sessions' files).
- Dart oracle (for parity questions): `dart run glp_runtime/bin/glp_repl.dart` (dart at
  `C:\src\flutter\bin\cache\dart-sdk\bin`, not on PATH; prebuilt `glp_repl.exe` is stale/unbootable).
- Concurrent QUIC session pushes to this same branch — **fetch + rebase before working/pushing**
  (my gleam commits are additive, no overlap; rebase cleanly).

## Discipline reminders (from CLAUDE.md / handover)

- Commit per slice, staged **by name** (never `git add -A`), push each. Marathon-style checkpoints.
- Frozen language semantics — any gap found mid-port STOPs and escalates (Constitution IV-a).
- Preserve/faithfully port `_ClauseVar` + `_TentativeStruct` (CLAUDE.md: critical — do not botch).
- Marathon run (Olamnit-local): `mrun-56564f6cdca3` — position truth is tasks.md + commits, not
  the marathon alone. Sidecar `implement` stage was `start`ed and left open (US1 not done); do
  **not** run `sidecar complete implement` until US1 is finished.
