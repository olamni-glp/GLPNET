# 050 full-Gleam — T024/T029 restart note (2026-07-12)

> ## ⚠️ STATUS 2026-07-12 — **T024 DONE**, RESUME AT **T029**
> - **Safe restart point.** Branch `050-full-gleam-combined`, tree **clean**, **pushed** @ **`78dd7ef9`** (HEAD == origin). Native gleam **365 / 365**, warning-free.
> - **T024 CLOSED** (generic `Guard` opcode + native body kernels + shared `arith.gleam`): commit `78dd7ef9`. Marathon **`mrun-d96119d59d07`** discharge item T024 **satisfied (1/6)**.
> - **NEXT = T029** engine facade (see "## NEXT — T029" below) → T030 acceptance; plus T025/T026 suites + T028 INDEX flip. T024/T023 sections below are now HISTORICAL (kept for the runner/guard internals).
> - **Resume:** `cd glp_gleam && gleam test` (expect 365/365) · `git fetch origin 050-full-gleam-combined` (compare HEAD vs origin; a concurrent QUIC session shares this branch) · read tasks.md T024 ◇-note + this doc's "## NEXT — T029".
> - **Marathon resume:** `PYTHONUTF8=1 D:/bstdev/research/buildkit/.venv313/Scripts/buildkit-marathon.exe resume --feature 050` (position derives from durable rows; do not trust a summary).

**Objective restart bootstrap** for continuing feature `050-full-gleam-combined` on
**Olamnit**. Source of truth is git + `specs/050-full-gleam-combined/tasks.md`
checkboxes/◇-notes + the marathon rows — this doc is the bootstrap + next-slice plan,
not a work ledger. Supersedes the T021 restart note for "what's next" (that note
remains valid for the runner internals).

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

## NEXT — T029 (resume here) — engine facade + REAL PRELUDE THREAD

`glp_gleam/src/glp/engine.gleam` (`new`/`load`/`run`/`step` — opaque `Engine` wrapping
`loader.load` + `scheduler`; replaces the 033 placeholder). The scheduler IS already the
engine (T022), so the facade is thin — EXCEPT for the one real piece of work T024 surfaced:

🔴 **The prelude gap (the crux of T029).** `loader.load(source, prelude_source)` today
compiles **only `source`**; `prelude_source` feeds (a) PE unit-clause unfolding (so `=`,
`send`, `receive` unfold) and (b) type-check declarations — but the prelude's **multi-clause
procedures are NOT compiled into the program**. So `Spawn(":=/2")`, `Spawn("merge/3")`, etc.
miss `label_pc`, and (for `:=`) fall to the kernel dispatch which correctly reports
`unresolved` (`:=` is not a kernel — it is self.glp GLP clauses that CALL the `'_add'`…
kernels). T029 must **thread + compile `programs/self.glp`** into the engine's program so
`:=`/`=`/`merge`/`send`/`receive`/etc. resolve at runtime. Design decisions to make:
- WHERE prelude compilation happens: extend `loader.load` to also compile the prelude's
  clauses and merge their labels into the program, OR do it in the engine facade
  (read self.glp from disk once, compile, merge). The loader module header (lines 22-27)
  says "the engine facade (T029) owns reading programs/self.glp from disk" — so the facade
  reads the file; the compilation-into-the-program is the new plumbing.
- self.glp is large — expect the FIRST full-prelude compile to surface gaps (opcodes/parse
  cases the corpus didn't hit). Port each faithfully; STOP + escalate on any frozen-semantics
  gap (Constitution IV-a / §1.14). Assess scope first: try compiling self.glp through the
  pipeline and triage what fails before committing to the facade shape.
- Boot: goal-term → argument registers (Dart `CallEnv`/`argSlots`); the scheduler's `boot`
  already seeds a RunQueue — the facade wraps `loader` + `scheduler.new`/`boot`/`run`.

## After T029 → MVP close (US1)
- **T030** MVP acceptance: smoke set — one suspension, one SRSW-neg, one type-neg (the
  negatives are load-time rejections that already work), **plus a real `:=` arithmetic goal**
  (the end-to-end validation of T024's kernels, now reachable once T029 threads the prelude) —
  via the engine API, Dart-identical outcomes → `specs/050-full-gleam-combined/baseline.md`.
- **T025/T026** engine semantics + adversarial writer-MGU suites (`runner_test.gleam`,
  `writer_mgu_adversarial_test.gleam`); **T028** prose PROOF + flip the flip INDEX row
  OPEN→discharged (co-committed with T026's four-artifact discharge per
  `contracts/proof-obligations.md`).
- These four (T025/T026/T028/T030) are the remaining marathon discharge items after T029.

## T024 evidence (HISTORICAL — done @ 78dd7ef9)
- New: `glp_gleam/src/glp/engine/arith.gleam` (shared NumV core), `…/engine/kernels.gleam`
  (native `_add/_sub/_mul/_div/_idiv/_mod/_neg`), `test/glp/engine/arith_guards_kernels_test.gleam`
  (15 tests). Modified: `…/engine/runner.gleam` (generic `Guard` opcode + `spawn()` kernel dispatch).
- `wait`/`wait_until` surfaced as `Unimplemented` (effectful timers, out of pure-engine MVP).
- The `## NEXT — T024` section below is now HISTORICAL; it documents the guard/kernel internals.

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
