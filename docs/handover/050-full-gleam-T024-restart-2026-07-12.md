# 050 full-Gleam — T024 restart note (2026-07-12)

**Objective restart bootstrap** for continuing feature `050-full-gleam-combined` on
**Olamnit** at **T024** (body kernels + the generic `Guard` opcode). Source of truth
is git + `specs/050-full-gleam-combined/tasks.md` checkboxes/◇-notes — this doc is the
bootstrap + next-slice plan, not a work ledger. Supersedes the T021 restart note for
"what's next" (that note remains valid for the runner internals).

## Where things stand (objective)

- Branch **`050-full-gleam-combined`**, tree clean, **pushed** to origin @ **`83917d53`**.
- Commits: `6d675f40` T021·21c (HEAD structures) · `6f63399b` T021·21d (BODY+spawn, **T021 CLOSED**)
  · `7705a0d9` T022 scheduler · `83917d53` T023 structural guards.
- `tasks.md`: T013–T023 done except T024/T025/T026/T028/T029/T030. **T021 [X], T022 [X],
  T023 structural part done (arithmetic Guard deferred here).**
- Native gleam test baseline: **350 / 350, warning-free** (`cd glp_gleam && gleam test`).
- The engine core runs **end-to-end**: parse→SRSW→PE→typecheck→codegen→load→schedule→
  three-phase reduce (HEAD+BODY+commit)→suspend/reactivate→structural guards.

## What is DONE (so you don't re-do it)

- **T021 runner** (`glp_gleam/src/glp/engine/runner.gleam`): full three-phase spine, HEAD
  constants+structures (writer-MGU, `_TentativeStruct`/S-mode/`Push`/`Pop`), BODY
  `Put*`/`Set*`/`PutStructure` + parent-stack completion, `Commit` (tentative→StructTerm),
  `Spawn`→`SpawnReq`, suspension. Guard opcodes `Ground`/`Known`/`Unknown`/`NoReaders`/
  `GroundEqual`/`Otherwise` (T023, three-valued, `collect_unbound` walker + `resolve_cvar`).
- **T022 scheduler** (`glp_gleam/src/glp/engine/scheduler.gleam`): opaque `Engine`
  (program+heap+RunQueue+goal-store+next_id); `new`/`boot`/`run(reduction_budget, fuel)`;
  Reduced→mint SpawnReq ids + reactivate woken + drop; Suspended→`heap.suspend_on_writer`
  per waited writer + gen++; heap-driven reactivation; FR-005 dedup via `enqueue_wake`.
- **Runner↔scheduler seam:** `Reduced(heap, woken: List(GoalRef), spawned: List(SpawnReq))`.
  The runner does **NOT** own the goal-id counter; the scheduler mints ids for `SpawnReq`s.

## NEXT — T024 (resume here)

Two coupled deliverables sharing ONE arithmetic evaluator; do the evaluator once.

### 1. Body kernels — the `spawn`-to-kernel path
In `runner.gleam`, `spawn()` currently returns `Unimplemented("spawn -> body kernel <label>")`
when `program.label_pc(label)` misses (Dart Spawn L3220–3256: if `prog.labels[label]==null`
→ `rt.bodyKernels.lookup(name, arity)` → execute inline; abort→terminated). Port the kernel
dispatch there. Kernels the corpus needs first:
- **`:=`/2** — arithmetic assignment `X := Expr` (binds writer `X` to the evaluated number).
- **`=`/2** — assignment. NOTE `self.glp` defines `=(_?, _)` as the unit clause `X? = X.`
  (a single-unit-clause procedure). When the prelude is loaded, `=/2` IS a defined
  procedure → `Spawn("=/2", 2)` resolves via `label_pc` and runs the unit clause — so `=`
  may already work once a real prelude is threaded (verify; don't double-implement).
- Arithmetic body kernels the corpus uses (check `programs/self.glp` body-kernel decls).

### 2. Generic `Guard` opcode (arithmetic comparisons + more)
The codegen routes every non-structural guard (`< > =< >= =:= =\=`, term-order `@< @> @=< @>=`,
type tests `integer/atom/string/constant/number/list/compound/...`, `wait`/`wait_until`,
and any user guard predicate) to the **`Guard(label, arity, negated)`** opcode (see
`codegen.gleam` `generic_guard`, which emits `put_arg`s then `Guard`). It currently falls
through to `_ -> Stop(RunnerError(Unimplemented(...)))` in `runner.gleam` `step`. Port the
Dart general `Guard` handler:
- deref args (via `put`-built `arg_slots` — guards use pre-commit guard-arg building; see
  `guardArgSlot` in the Dart, currently NOT modelled in the Gleam — you'll add a
  `guard_arg_slot`/pre-commit-arg path or evaluate directly from clause vars),
- any unbound reader → SUSPEND (`_suspendAndFailMulti`); else `_evaluateGuard`,
- negation inverts success↔failure, suspend unchanged.

### Shared arithmetic evaluator (frozen — STOP on any gap)
Port Dart `_evaluateGuard` (big `switch` on predicate name) + `_evaluateArithmetic`/
`evaluateNumeric`. **Standard order of terms** (`_compareTerms`/`_orderRank`, Number<String<
compound) must stay **byte-identical to the C# port** (dossier). Unknown predicate →
`[WARN]`+failure (do NOT invent semantics — Language Authority §1.14: propose-first).

### Dart source refs (verify-at-port-time — the map is a nav aid, source wins)
- generic `Guard` handler: `runner.dart` **L3503–3654**
- `_evaluateGuard` switch: **L4806+** (comparisons, term-order, type tests, `=?=`, wait)
- `_evaluateArithmetic` / `evaluateNumeric`: **L4770 / L4815**
- unknown predicate `[WARN]`+fail: **L5284**
- Spawn body-kernel path: **L3220–3256**; runtime-defined-guard interpreter (049): **L461–764**
- Porting map: `docs/research/glp-gleam-baseline/runner-dart-architecture-map.md` §5–§7.

## After T024 → MVP close (US1)
- **T029** engine facade `glp_gleam/src/glp/engine.gleam` (`new`/`load`/`run`/`step` — opaque
  Engine wrapping `loader.load` + `scheduler`; replaces the 033 placeholder). Thin: the
  scheduler is already the engine; add goal-term→regs boot + a real prelude thread.
- **T030** MVP acceptance: smoke set (one suspension, one SRSW-neg, one type-neg — the
  negatives are load-time rejections that already work) via the engine API → `baseline.md`.
- T025/T026 engine + adversarial writer-MGU suites; T028 prose PROOF + INDEX flip (with T026).

## Environment (Olamnit)
- **NO WSL.** Native-Windows **gleam 1.17.0** via the **Bash tool** (Git Bash; NOT PowerShell
  bash = WSL). `gleam` on PATH there.
- Build/test: `cd glp_gleam && gleam test` (baseline 350/350). Format specific files only:
  `gleam format <paths>` — **never bare `gleam format`**.
- Dart oracle for parity: `dart run glp_runtime/bin/glp_repl.dart` (dart at
  `C:\src\flutter\bin\cache\dart-sdk\bin`, not on PATH; prebuilt `glp_repl.exe` stale).
- Marathon CLI: `PYTHONUTF8=1 D:/bstdev/research/buildkit/.venv313/Scripts/buildkit-marathon.exe`
  (run `mrun-56564f6cdca3`). Record a `trace --subject … --decision accept --evidence …`
  per slice; position truth = tasks.md + commits, not the marathon alone.

## Discipline reminders
- **Fetch + rebase before working/pushing** — a concurrent QUIC session shares this branch
  (my gleam commits are additive, rebase cleanly). `git fetch origin 050-full-gleam-combined`,
  compare `HEAD` vs `origin/…`; only fast-forward-push when equal.
- Commit per slice, staged **by name** (never `git add -A`), push each. Baseline gleam test
  green before + after.
- Frozen language semantics — any gap found mid-port STOPs and escalates (Constitution IV-a;
  Language Authority §1.14). Do not invent guard/kernel semantics.
- Guard/kernel tests need prelude decls: pass a minimal prelude to `loader.load(src, prelude)`
  (see `test/glp/engine/guards_test.gleam` `guard_prelude`), or thread the real
  `programs/self.glp`. Keep the suite warning-free (remove unused imports).

## One-line resume
`cd glp_gleam && gleam test`  → expect **350/350**, then read this doc's "NEXT — T024" and
start with the shared arithmetic evaluator.
