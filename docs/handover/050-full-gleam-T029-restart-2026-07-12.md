# 050 full-Gleam — T029 restart note (2026-07-12)

> ## ✅ STATUS 2026-07-13 — **US1 + US2 DONE** (T013–T036 + PI:14). Next = **US3 corpus parity T037–T044**.
> - **US2 REPL COMPLETE (2026-07-13):** T033 results `e574c9b9` · T031/T032/T035/T036 REPL core `bdb4d709` · **T034 output capture** `96078234` (Gabi chose thread-as-DATA: `_output/1` kernel port + `formatGroundTerm` seam → `KSuccess`/`Reduced`/scheduler/`engine.run_with_limit_capturing` → REPL prints ahead of outcome; NOT the R4-excluded envelope field). REPL verified end-to-end via `gleam run` (`X := 2+3` → `X = 5`/`→ succeeds`; `emit(hello)` → `hello`/`→ succeeds`). native gleam **432/432**, warning-free.
> - **⚠ TWO THIN SPOTS deferred (need the trace-line/step machinery + Dart trace-format oracle):** (1) `:trace` toggles the flag + prints `Trace enabled/disabled` but does NOT yet emit per-reduction trace lines (Dart `debugTrace`); (2) facade `engine.step(Engine) -> #(Engine, Event)` not wired (the `scheduler.step` primitive + `StepOutcome` exist from T029 Slice 0; the Engine-value facade `step`/`Event` is not). Both are "reference `:trace` semantics" + the contract Engine surface — flag for US3 or a US2 polish pass; the corpus runner (T039) drives goals via `run`, not `step`, so US3 can proceed without them.
> - **Phase-A hardening CLOSED (2026-07-13):** **T025** engine-semantics tests `test(050)` @ **`64d0a2a3`** (three-phase HEAD→GUARD→BODY ordering; reactivate-exactly-once via `scheduler.step` drain; otherwise-after-FAILURE-not-suspension). **T026+T028 = ONE four-artifact PI:14 writer-MGU discharge** `proof(050)` @ **`06e0427f`**: adversarial suite `writer_mgu_adversarial_test.gleam` (10 tests, post-heap-STATE invariant) + INDEX.md OPEN→**proved** + PARITY-BAR FB-M1-14/15 + RISK-PROOF-writerMGU RESOLVED. **Lean `lake build` exit 0 re-verified on Olamnit** (elan 4.2.3 installed this session, Lean v4.30.0; `~/.elan/bin` shims, not on PATH). native gleam **392 / 392**, warning-free. **M1 proof gate PI:14 discharged.**
> - **Safe restart point.** Branch `050-full-gleam-combined`, tree **clean**, **pushed** @ **`06e0427f`**. Native gleam **392 / 392**, warning-free.
> - **NEXT = US2 REPL (Phase B), step 4 below:** T031 loop+`glp_gleam.gleam` main · T032 commands · T033 results via 038 builder · **T034 output_capture.gleam** (deferred capture, wired to REPL, NOT the R4-excluded envelope `captured`) · T035 scripted tests · T036 envelope-identity. Facade `step`/`Event` wires in here.
> - **T029 facade DONE — 3 slices (all committed+pushed):** Slice 0 scheduler refinement `13312dfb` · Slice 1 `engine.gleam` facade `8f5b7766` · Slice 2 goal-boot + `engine.run` `0f3817c4`. **T030 acceptance DONE** — smoke set via engine API, `X := 2+3` → Success `X=5` **verified byte-for-value identical vs the Dart REPL** (`X = 5 → succeeds`); suspension / SRSW-neg / type-neg / unknown-pred all as expected (see `baseline.md §T030`).
> - **Slice 0b (output capture) DEFERRED — Gabi-approved 2026-07-13.** No faithful oracle: `build_result_envelope` UNCALLED in glp_runtime/csharp; `captured` EXCLUDED from parity by **R4** (`ResultEnvelope.cs:183`; `golden_corpus_test.gleam:6`). It is scheduled as **US2 task T034** (`glp/engine/output_capture.gleam`) — do it there, wired to the REPL, never guessing envelope-`captured` bytes. All envelopes carry `captured=<<>>`.
> - **Facade `step`/`Event` DEFERRED to US2** (interactive stepping = REPL-session concern needing live run-state on the Engine; the faithful `scheduler.step` primitive it wraps is delivered + tested in Slice 0).
>
> ### ▶ NEW-SESSION START PROCEDURE (2026-07-13, Gabi-directed: do US1 hardening then US2 REPL in a fresh session)
> 1. **Mandatory reading** (CLAUDE.md order): CLAUDE.md → `docs/DISCIPLINE.md` → `docs/typed-glp-manual.md` → `docs/glp-cheat-sheet.md`, then STOP+ack. Then read THIS banner (the slice-plan BODY below is **T029 HISTORY — done**; do not re-do it).
> 2. **Verify baseline (objective):** `cd /d/bstdev/research/glp/glpnet && git fetch origin 050-full-gleam-combined` → HEAD must == origin @ **`f9521f43`** (or later if QUIC pushed — rebase; my commits are additive). Then `cd glp_gleam && gleam test` → expect **377/377**, warning-free.
> 3. **Phase A — US1 hardening (do first, in order):**
>    - **T025** `glp_gleam/test/glp/engine/runner_test.gleam` — engine semantics tests: three-phase HEAD/GUARD/BODY ordering, suspend/reactivate-**exactly-once** (FR-005 dedup — `scheduler.step`/`enqueue_wake` are the seams), `otherwise`-fires-**after-failure-not-suspension**. Parallel-safe [P].
>    - **T026 + T028 = ONE four-artifact discharge commit** (contracts/proof-obligations.md bookkeeping): T026 adversarial writer-MGU suite `glp_gleam/test/glp/engine/writer_mgu_adversarial_test.gleam` (reader/reader FAIL, writer/writer soft-fail, nested structures, tentative-HEAD paths) **+** T028 flip `docs/research/glp-gleam-baseline/pipelines/P4-faithfulness/PROOFS/INDEX.md` writer-mgu row OPEN→discharged with artifact links. Lean (T027) + PROOF.md (T028) already authored — this commit adds the tests + flips INDEX in ONE checkpoint.
> 4. **Phase B — US2 REPL (T031–T036):** loop+entry `repl/repl.gleam`+`glp_gleam.gleam` (scripted newline-fed stdin) · commands `repl/commands.gleam` (`load`/bare-path/goal/`:trace`/`:limit`/`:quit`) · results `repl/results.gleam` (deep-resolve via 038 builder) · **T034 output capture** `glp/engine/output_capture.gleam` — this is where the deferred `_output`/capture lands, wired to the REPL (NOT the R4-excluded envelope `captured`) · T035 scripted tests · T036 envelope-identity. This is where **facade `step`/`Event`** gets wired (needs live run-state on the Engine).
> 5. **Discipline:** contracts are `contracts/{gleam-instance-surface,corpus-parity,proof-obligations}.md`; commit per task staged BY NAME, `gleam format <paths>` (never bare), fetch+rebase before push (shared QUIC branch); frozen-semantics gap → STOP+escalate per §Signaling below. Known goal-boot MVP limits: single-atom only (conjunctions → REPL), improper-const list tails Error loudly.
> - **Resume:** `cd glp_gleam && gleam test` (expect 365/365) · `git fetch origin 050-full-gleam-combined` (compare HEAD vs origin; a concurrent QUIC session shares this branch — fetch+rebase before push) · read this doc's "## Slice plan" + "## Signaling protocol".
> - **Marathon resume:** `PYTHONUTF8=1 D:/bstdev/research/buildkit/.venv313/Scripts/buildkit-marathon.exe resume --feature 050` (run `mrun-56564f6cdca3` / T024 doc cites `mrun-d96119d59d07`; position derives from durable rows + tasks.md + commits — never a summary).

**Objective restart bootstrap** for continuing feature `050-full-gleam-combined` on
**Olamnit**. Source of truth = git + `specs/050-full-gleam-combined/tasks.md` T029/T030
+ the marathon rows + the contracts. This doc is the bootstrap + slice plan + the
design decisions LOCKED in the 2026-07-12 goal-boot/envelope discussion. Supersedes the
T024 restart note for "what's next" (that note stays valid for the guard/kernel internals).

## Where things stand (objective)

- Branch `050-full-gleam-combined`, tree clean, **pushed** @ **`5d353257`**.
- `5d353257` impl(050): T029 slice — `loader.compile_prelude`.
- Prior: `db455ed1` (T024 DONE restart banner) · `78dd7ef9` (T024 CLOSED: generic Guard opcode
  + native body kernels + shared `arith.gleam`).
- `tasks.md`: T013–T024 done. **T025/T026/T028/T029/T030 remain.** T029 = engine facade.
- Native gleam baseline: **365 / 365, warning-free** (`cd glp_gleam && gleam test`).

## What is DONE (so you don't re-do it)

- **`loader.compile_prelude(prelude_source) -> Result(BytecodeProgram, StagedError)`**
  (`glp_gleam/src/glp/compiler/loader.gleam`): parse → SRSW → PE → codegen, **type-check
  ELIDED**. This is the correct path for the prelude — Dart `GlpEngine._loadRootSelf`
  (`glp_engine.dart:230`) compiles `self.glp` via `GlpCompiler.compile` under default
  `CompileOptions{typeCheck:false}` (`compiler.dart:27`). self.glp is NEVER type-checked as a
  module: it calls host kernels (`_now/1`, `_add/3`, `_list_to_tuple/2`, …) deliberately
  absent from `builtinProcedures` (`prelude.dart:88` — the Gleam whitelist
  `analysis/prelude.gleam` is a faithful 1:1 of it), so a user-style type check spuriously
  rejects it (rejects at `_now/1`). Validated via a triage probe (removed): self.glp → 1430 ops.

## Design decisions LOCKED (2026-07-12 discussion — do NOT re-litigate)

### Goal-boot (CONFIRMED)
1. **MVP = single-atom goals only.** Port Dart `_setupArgument` (`glp_engine.dart:926-975`).
   DEFER `_setupConjunctionArg` (`:977`) + the conjunction argSlots block (`~:640-680`) to
   US2/REPL. T030's smoke set is all single-atom + two *load-time* negatives.
2. **Port the FULL ast→`terms.Term` builder** (Dart `_buildStructTerm`/`_buildListTerm`
   `:1028+`), NOT a const-only shortcut. Share ONE `var_name_to_id` across all goal args.
   Handle VarTerm/ConstTerm/StructTerm/nested-struct + lists-of-consts. DEFER
   structs-inside-lists (documented Dart REPL limitation, `docs/known-issues.md`; `:=` never
   hits it). No such builder exists in Gleam today — it is NEW code.
3. **`query_var_writers` = ordered first-occurrence `List(#(String,Int))`, writers only**
   (accumulate left-to-right — order is the parity invariant, NOT a dict; track only where
   `!is_reader`, Dart `if (!arg.isReader) queryVarWriters[baseName]=writerId`). For
   `X := 2+3` → `[("X", w)]` → envelope reports X=5.

### Envelope shaping (deferrals REJECTED — scheduler refinement MANDATORY)
The T022 scheduler does NOT yet hand the facade what `build_result_envelope` needs. Gabi
ruling 2026-07-12: **scheduler refinement is mandatory + critical** — a stubbed envelope
would poison US2/corpus-parity. All four capabilities below are IN SCOPE NOW (item 4 output
capture explicitly approved). **Sequencing** (Gabi 2026-07-12): caps 1-3 are the run/step
contract -> one combined Slice 0; cap 4 folds in IF `_output` exists, else spins out. VERIFIED
2026-07-12: Gleam `kernels.gleam` has only `_add/_sub/_mul/_div/_idiv/_mod/_neg` (T024) --
**`_output/1` is NOT ported** -> cap 4 becomes **Slice 0b** with the `_output` body-kernel port
(Dart oracle) as its prerequisite.

## Slice plan (execute in order; commit+push+trace per slice; gleam test green each side)

### Slice 0 — SCHEDULER REFINEMENT, caps 1-3 (MANDATORY, do FIRST)
`glp_gleam/src/glp/engine/scheduler.gleam`. Faithful port of the Dart scheduler
(`Scheduler.drainAsyncWithStatus`, driven by `glp_engine.dart runGoal:374-570` — locate the
`Scheduler` class from that call site; source wins over this map). Add:
1. **Faithful terminal status.** `RunStatus` must distinguish **Success** (boot goal reduced
   to completion) / **Failed** (boot goal's last clause-try failed, no commit) / **Suspended**
   (runnable queue drained, goals remain suspended) / OutOfFuel / Errored. TODAY `Quiescent`
   conflates Success+Failed — the core gap. The run must carry the boot goal's fate.
2. **Blocking-writer set exposure.** Store the suspend `on` set on the suspended `Activation`
   (currently discarded in `suspend_goal`) + aggregate a queryable blocking-writer set after
   run → `build_result_envelope`'s `blocking_readers` is exact.
3. **Real single-step + `Event`.** A `step` entry performing ONE reduction returning the
   per-step outcome (Reduced/Suspended/Failed + goal + woken/spawned), not a `fuel=1` black box.

### Slice 0b — OUTPUT CAPTURE, cap 4 (MANDATORY, in scope/approved; `_output`-dependent)
Spun out because **`_output/1` is NOT ported** (VERIFIED 2026-07-12 — Gleam `kernels.gleam`
has only the T024 arithmetic kernels). Prerequisite → then thread capture:
- **Port the `_output`/`'_output'/1` body kernel** into `kernels.gleam` (Dart body-kernel
  oracle; it is an already-approved system predicate — `self.glp procedure _output(_?)`,
  Dart `builtinProcedures` `_output/1` — so this is a faithful PORT, not a §1.14 language
  extension). If porting surfaces a frozen-semantics gap → STOP + escalate (Signaling).
- **Thread output-capture bytes** through the scheduler run → `build_result_envelope`'s
  `captured: BitArray` faithful (empty `<<>>` only when nothing was output). Verify against
  Dart's capture path (`glp_engine.dart runGoal` output collection).

### Slice 1 — engine.gleam facade
`glp_gleam/src/glp/engine.gleam` — replaces the 033 placeholder. Surface per
`contracts/gleam-instance-surface.md` §"Engine as typed value":
- `new() -> Engine` — reads `../programs/self.glp` (relative to glp_gleam/, the FFI-read CWD
  convention of `golden_corpus_test`) via `@external(erlang,"file","read_file")` (038 pattern),
  `loader.compile_prelude` → stores prelude program + prelude source string. A missing/broken
  self.glp panics LOUDLY (Dart `_loadRootSelf` throws StateError — trusted invariant, not a
  user diagnostic). ⚠️ open: `new()` is zero-arg per contract but path is CWD-relative —
  confirm with Gabi if a `new_with_prelude(source)` test seam is wanted.
- `load(Engine, source) -> Result(Engine, StagedError)` — `loader.load(source, prelude_source)`
  then `program.merge(user_prog, prelude_prog)` (Dart `program.merge(rootSelf)`,
  `glp_engine.dart:310` — prepend stdlib, user labels win). Store merged program.
- `run(Engine, goal) -> #(Engine, ResultEnvelope)` — see Slice 2.
- `step(Engine) -> #(Engine, Event)` — wraps Slice-0 single-step. `Event` is NEW (none exists).

### Slice 2 — goal-boot + run + T030
- Parse goal via `parser.parse_module(lexer.tokenize(goal_text))` → `procedures[0].clauses[0].head`
  (an `ast.Atom`; a goal parses as a unit-clause head — Dart `runGoal:505-518`). Only
  `parse_module` exists; there is NO `parse_goal`.
- Goal-boot per the CONFIRMED decisions above → arg registers (`program.XRegs`;
  slot i → register i) + ordered `query_var_writers`. Term facts: runtime
  `terms.Term` = `ConstTerm(Constant)|StructTerm(functor,args)|VarRef(addr)`; nil =
  `ConstTerm(ConstAtom("nil"))`, cons = `StructTerm(".",[h,t])`. `heap.allocate_variable(h)
  -> #(Heap, writer, reader)`; `heap.bind_writer(h, writer, value:terms.Term)` (unified —
  Dart bindWriterConst/bindWriterStruct both collapse here). Readers→`VarRef(readerId)` slot,
  writers→`VarRef(writerId)` slot; consts/structs bind-writer-then-pass-`VarRef(readerId)`.
- `scheduler.boot(procedure, entry_pc, regs)` (entry_pc via `program.label_pc(prog, "name/arity")`)
  → `scheduler.run` → map Slice-0 `RunStatus` to `result_envelope.ExecutionStatus`
  (Success/Suspended/Failed) → `result_envelope_builder.build_result_envelope(h,
  query_var_writers, status, blocking_readers, agent_id, captured, error)` → `ResultEnvelope`.
  ⚠️ `agent_id`: VERIFY Dart's value for parity (do not guess).
- **T030**: `X := 2+3` e2e through the engine API → envelope status Success, X=5, Dart-identical.
  Plus the suspension case (status Suspended, exact blocking set) and confirm the two load-time
  negatives (SRSW-neg, type-neg) still reject at `load` as `StagedError`. Record in
  `specs/050-full-gleam-combined/baseline.md`.

## Signaling protocol — WHEN + HOW to signal (frozen-semantics discipline)

### WHEN to STOP + escalate (do NOT proceed / do NOT work around)
1. **Frozen-semantics gap** — any `terms.Term`/opcode/heap op that cannot faithfully represent
   the Dart behavior (class of the earlier `ConstTerm(null)` void case). §1.14 / Constitution
   IV-a: propose-first, NEVER invent guard/kernel/term semantics.
2. **Core-GLP edit beyond the sanctioned additive changes** — the approved non-facade touches
   are the Slice-0 scheduler capabilities (status / `on`-set on Activation / single-step /
   output capture). Editing `runner.gleam` or `heap.gleam` *reduction logic* → STOP.
3. **Parity break** — T030's `:=` or the suspension case yields a non-Dart-identical
   `ResultEnvelope` (status, ordered bindings, blocking set, or encoded bytes). Parity is the
   acceptance gate.
4. **Undecided fork** — `agent_id` differs from Dart; a conjunction goal appears in the smoke
   set; `_output` kernel not yet ported (capture dependency); run-time `Failed` classification
   ambiguous against Dart.

### HOW to signal
- **STOP immediately, no workaround** (Bug Protocol / §1.2). Leave the tree **green at the last
  committed slice** (`gleam test` passing, staged BY NAME, pushed).
- **Language/semantics gap** → the GLP-bug format (Failing Goal / type+procedure decls /
  suspected clause), then WAIT.
- **Design fork** → concise free-text ≤2 sentences + the concrete Dart line ref.
- **Record** a marathon `trace --decision escalate --evidence …` (durable) + note it in this
  doc's STATUS banner.

### WHEN to proceed autonomously (no signal)
- Additive `engine.gleam` + the faithful goal-boot port + the four sanctioned Slice-0 scheduler
  capabilities.
- Routine progress signal at each slice boundary: commit + one-line marathon
  `trace --decision accept`.

## Environment (Olamnit)
- **NO WSL.** Native-Windows **gleam 1.17.0** via the **Bash tool** (Git Bash; NOT PowerShell
  bash = WSL). `gleam` on PATH.
- Build/test: `cd glp_gleam && gleam test` (baseline 365/365). Format specific files only:
  `gleam format <paths>` — **never bare `gleam format`**. `gleam run -m <module>` for a
  throwaway probe (remove after; do NOT leave probes in `src/`).
- The Bash tool CWD persists between calls — a `cd glp_gleam` bleeds into later calls; reset
  with an absolute `cd /d/bstdev/research/glp/glpnet` when running git/repo-root commands.
- Dart oracle for parity: `dart run glp_runtime/bin/glp_repl.dart` (dart at
  `C:\src\flutter\bin\cache\dart-sdk\bin`, not on PATH; prebuilt `glp_repl.exe` stale).
- Marathon CLI: `PYTHONUTF8=1 D:/bstdev/research/buildkit/.venv313/Scripts/buildkit-marathon.exe`.

## Discipline reminders
- **Fetch + rebase before working/pushing** — a concurrent QUIC session shares this branch (my
  gleam commits are additive, rebase cleanly). `git fetch origin 050-full-gleam-combined`,
  compare `HEAD` vs `origin/…`; fast-forward-push only when equal.
- Commit per slice, staged **BY NAME** (never `git add -A`), push each. Baseline gleam test
  green before + after.
- Frozen language semantics — any gap mid-port STOPs + escalates (see Signaling protocol).
- Contracts governing this work: `contracts/gleam-instance-surface.md` (surface),
  `contracts/corpus-parity.md` (goldens), `contracts/proof-obligations.md` (T026 seam).

## One-line resume
`cd glp_gleam && gleam test` → expect **365/365**, then read "## Slice plan" and start with
**Slice 0 (scheduler refinement)** — mandatory before the facade.
