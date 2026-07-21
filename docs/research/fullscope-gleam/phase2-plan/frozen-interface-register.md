# Frozen-Interface Register — feature 059 full-scope Gleam GLP

**Status**: ACTIVE from wave 1 · **Created**: 2026-07-20 · **Feature**: `059-full-scope-gleam-glp-implementation` · **Plan**: `docs/research/fullscope-gleam/feature-outline-plan-FINAL-2026-07-20.md` (wave 1) · **Marathon**: `mrun-8bda036d9e9b`

## Freeze baseline

- **Baseline commit**: `49b523420d745875c67207417adf56c8a5537331`
- **Measured 2026-07-20** at that commit (not asserted — run outputs recorded below):
  - Gleam gleeunit (WSL, `cd glp_gleam && gleam test`): **463 passed, no failures** ← grow-only floor
  - Dart unified REPL suite (`bash test/run_all_tests.sh`): see entry `suite-baseline`
  - C# reference suites (`dotnet test`): see entry `suite-baseline`

## How this register binds

Every entry below pins a **delivered** interface that wave-2..5 work packages build ON. The rule (plan E3a, FR-002):

1. No WP may change a pinned interface or a protected test file without a **rule-request ruling** recorded in `docs/research/fullscope-gleam/phase2-verify/rulings.md`.
2. Verification of any entry is `git diff --exit-code <protected test paths> <baseline commit>` (empty) **plus** its named suite green.
3. A shrinking or reddening suite is the drift this register exists to make loud — it blocks dependent WPs, it is never worked around.

**Register-path note (plan defect resolved at wave 1):** the FINAL plan's blind builders named two paths for this register — `docs/research/fullscope-gleam/phase2-plan/frozen-interface-register.md` (11 acceptance references) and `docs/research/fullscope-gleam/frozen-interface-register.md` (6). This file is the single authoritative register; the other path is a pointer to it, so both acceptance checks verify from a fresh session. No entry content is duplicated.

---

## Entry `runtime-term-heap`  (WP `freeze-runtime-term-heap`)

Bottom layer of the dependency spine. **Pinned public signatures** at the baseline commit:

`glp_gleam/src/glp/runtime/terms.gleam`
```gleam
pub type Constant
pub type Term
pub fn nil() -> Term
pub fn cons(head: Term, tail: Term) -> Term
```

`glp_gleam/src/glp/runtime/heap.gleam`
```gleam
pub type Cell            pub type CellTag        pub type DerefResult      pub type HeapError
pub fn tag(cell: Cell) -> CellTag
pub fn new() -> Heap
pub fn allocate_variable(heap: Heap) -> #(Heap, Int, Int)
pub fn paired_reader(heap: Heap, writer: Int) -> Int
pub fn paired_writer(heap: Heap, reader: Int) -> Result(Int, Nil)
pub fn is_writer(heap: Heap, addr: Int) -> Bool
pub fn is_reader(heap: Heap, addr: Int) -> Bool
pub fn is_value(heap: Heap, addr: Int) -> Bool
pub fn deref(heap: Heap, addr: Int) -> Result(#(Heap, DerefResult), HeapError)
pub fn bind_writer(heap: Heap, writer: Int, value: Term) -> Result(#(Heap, List(GoalRef)), HeapError)
pub fn bind_writer_to_var(heap: Heap, writer: Int, reader: Int) -> Result(#(Heap, List(GoalRef)), HeapError)
pub fn suspend_on_writer(heap: Heap, writer: Int, susp: Suspension) -> Result(Heap, HeapError)
```

`glp_gleam/src/glp/runtime/unify.gleam`
```gleam
pub type UnifyOutcome
pub fn unify(h: Heap, a: Term, b: Term) -> Result(UnifyOutcome, HeapError)
```

`glp_gleam/src/glp/runtime/suspension.gleam`
```gleam
pub type Suspension      pub type GoalRef        pub type Waiter
pub fn new_table() -> SuspensionTable
pub fn suspend(table: SuspensionTable, writer: Int, waiter: Waiter) -> SuspensionTable
pub fn consume(table: SuspensionTable, writer: Int) -> #(SuspensionTable, Set(Waiter))
pub fn waiters_on(table: SuspensionTable, writer: Int) -> Set(Waiter)
```

Semantics pinned with the signatures: paired writer/reader allocation; deref with path compression; writer-MGU (binds only writers, never readers, never writer-writer; σ̂ applied atomically at Commit); three-valued Success/Suspend/Fail verdict table; opaque SuspensionTable.

**Protected tests**: `glp_gleam/test/glp/runtime/{terms,heap,unify,suspension}_test.gleam`, `glp_gleam/test/glp/engine/writer_mgu_adversarial_test.gleam`

---

## Entry `engine-execution`  (WP `freeze-engine-execution`, depends on `runtime-term-heap`)

Three-phase HEAD/GUARD/BODY clause execution with tentative-structure and clause-variable state (`engine/runner.gleam`); suspension-aware scheduler run loop with run queue, goal store, blocking-reader table, faithful terminal statuses (`engine/scheduler.gleam`); `(goal_id, suspension_generation)` reactivation dedup with stale-wake dropping; the documented writer-address Si/U adaptation; the `StepOutcome` single-reduction seam (idle/reduced/suspended/failed/errored) — the step seam is the attachment point for host-driven stepping in wave-4 embeddability.

**Carve-out (escalate-if-hit, not a licence to patch)**: the surfaced-unimplemented WRITE-mode void slot → `ConstTerm(null)` frozen-semantics gap. A WP hitting it escalates; it is never patched ad hoc.

**Protected tests**: `glp_gleam/test/glp/engine/{runner,scheduler,dedup_key,step}_test.gleam`

---

## Entry `engine-facade`  (WP `freeze-engine-facade`, depends on `engine-execution`)

Engine-as-typed-value facade (`glp_gleam/src/glp/engine.gleam`): construct; load with untype-checked `self.glp` prelude boot+merge; one-shot run to a `ResultEnvelope`; interactive start/step; **zero global state**; plus `_output/1` output-as-captured-data. This facade is the named yngenios-embeddability anchor — wave-4 `build-yngenios-embeddability` layers the host surface on it and `build-fe-be-process-split` wraps the BE process behind it.

**Protected tests**: `glp_gleam/test/glp/engine_test.gleam`, `glp_gleam/test/glp/engine/output_capture_test.gleam`

---

## Entry `embeddability-api`  (WP `freeze-embeddability-api`)

The engine-value surface and its **explicit prelude-injection seam** (`engine.gleam:107-110`) — the delivered half of embeddability. Under the 2026-07-20 clarification (full yngenios wiring), this is the surface the four spec-056 services drive through their mailbox binding; it is frozen so the wiring adapts to it rather than reshaping it.

**Protected tests**: `glp_gleam/test/glp/engine_test.gleam`

---

## Entry `compiler-pipeline`  (WP `freeze-compiler-pipeline`)

Single-entry unskippable load pipeline (`compiler/loader.gleam`): parse → SRSW → partial-eval → type-check → v2.16 codegen, with Dart-identical error text and positions, stage-attributed later-stages-do-not-run diagnostics, the sanctioned SRSW relaxations (incl. ground/1 D6, no escape mechanism), byte-identical-message moded type checking, and the pinned merge/3 codegen stream with its one documented semantically-neutral ground-list divergence.

**Protected tests**: `glp_gleam/test/glp/parser/parser_test.gleam`, `test/glp/analysis/{srsw,type_checker}_test.gleam`, `test/glp/compiler/{partial_eval,codegen,loader}_test.gleam`

---

## Entry `bytecode-isa`  (WP `freeze-bytecode-isa`)

The v2.16 opcode union including reference-live spec-gap opcodes with mnemonic and reader/writer-flip table (`bytecode/opcodes.gleam`), and the BytecodeProgram model — label indexing, prelude-in-front merge, guard-spec table, disassembly, X registers (`bytecode/program.gleam`). No build WP may extend or reorder it without a rule-request.

**Protected tests**: `glp_gleam/test/glp/bytecode/opcodes_test.gleam`

---

## Entry `bytecode-runner`  (WP `freeze-bytecode-runner`)

The delivered production-emitted opcode surface executed by `engine/runner.gleam` (HEAD/GUARD/BODY families, Commit, Spawn), so the wave-3 opcode close adds Requeue/Allocate/Deallocate **without** altering delivered opcode behavior.

**Carve-out**: ruling **G4** (parity governs) makes the reference v2.16 ground-struct-literal behavior normative for `UnifyConstant`; the wave-3 close changes the Gleam emission to match the reference and pins it with a golden. That is a ruled, scoped exception to this freeze — recorded here so it is not a silent edit.

**Protected tests**: `glp_gleam/test/glp/engine/runner_test.gleam`

---

## Entry `guard-kernel`  (WP `freeze-guard-kernel`)

The delivered pure three-valued guard set: `ground`/`known`/`otherwise`/`=?=`, arithmetic and standard-order comparisons, type tests, `@<`. Timer guards `wait`/`wait_until` are **not** delivered (unimplemented faults) and are wave-3 close work — this freeze pins only what exists.

**Protected tests**: `glp_gleam/test/glp/engine/guards_test.gleam`, `glp_gleam/test/glp/engine/arith_guards_kernels_test.gleam`

---

## Entry `body-kernel`  (WP `freeze-body-kernel`)

The standalone-engine body-kernel registry surface (`engine/kernels.gleam:20-97`): arithmetic, math, conversion, univ, mutual-reference stream append, `_output`. The `_now`/`_send` kernels are unregistered in the standalone engine — wave-3 close extends this registry **without changing** it.

**Protected tests**: `glp_gleam/test/glp/engine/arith_guards_kernels_test.gleam`

---

## Entry `codec-envelope`  (WP `freeze-codec-envelope`)

The ED-1 result seam as ONE wire contract: byte-parity term codec with global variable identity replacing heap addresses; the `0x01`/`0x11` result envelope (status, bindings, var-to-writer, suspended, captured, error; canonical order-preserving); depth-bounded deep-resolve builder with truncation and circular markers; loud-fail rejection discipline; suspended-status reporting by global var ids; in-process/wire byte-identity. **This contract IS the FE/BE process-boundary payload** for `build-fe-be-process-split` and the value surface for `build-yngenios-embeddability`.

**Frozen deferral**: the `captured` field stays always-empty per the recorded owner-approved deferral. Any FE output-streaming need in the split arrives as a rule-request to unfreeze that field — never a silent envelope extension.

**Protected tests**: `glp_gleam/test/glp/codec/{result_envelope_codec,golden_corpus,loud_fail_fuzz,cyclic_term,deref_fidelity,suspended_acceptance}_test.gleam`, `glp_gleam/test/glp/repl/envelope_identity_test.gleam` (also carries the `guard-fe-be-envelope-seam` golden seam corpus) + its checked-in golden data `glp_gleam/test/glp/repl/envelope_seam_golden.hex`

---

## Entry `repl-surface`  (WP `freeze-repl-surface`, depends on `codec-envelope`)

The test-visible REPL surface: scripted EOF-terminating stdin loop over a threaded Session entered via `gleam run`; reference command set (load, bare `.glp` paths, dotted goals, `:trace`, `:limit` incl. exhaustion, `:quit`) with Dart-parity parse semantics; reference-shape bindings/status rendering from the ResultEnvelope; arity-stripped reader-marked trace lines. This is the FE-parity reference the split front end must stay byte-comparable to.

**Protected tests**: `glp_gleam/test/glp/repl/{repl,results}_test.gleam`, `glp_gleam/test/glp/engine/goal_format_test.gleam`

---

## Entry `link-wire`  (WP `freeze-link-wire`)

Link wire formats at Dart/C# byte parity: fixed 22-byte big-endian frame header with Whole/Fragment kinds, 64 MiB cap, MTU fragmentation, errors-as-data (`link/reliability/frame_codec.gleam`); pure-Gleam reflected-`0xEDB88320` CRC-32 with canonical vector `compute("123456789") == 0xCBF43926` (`link/reliability/crc32.gleam`); 4-byte big-endian TCP length-prefix framing with FrameCodec payloads riding opaquely.

**Scope honesty**: this pins the *format*, not the deferred deep adversarial frame matrix (T053), which is wave-2/3 work.

**Protected tests**: `glp_gleam/test/glp/link/frame_codec_test.gleam`, `glp_gleam/test/glp/link/tcp_test.gleam`

---

## Entry `link-transport-seam`  (WP `freeze-link-transport-seam`, depends on `link-wire`)

The transport seam and its two delivered leaves: in-BEAM loopback (hub+channel process rendezvous, FIFO exactly-once, close-drain, fault-on-send-after-close, no `gleam_otp`) and raw-TCP (passive-mode `gen_tcp` FFI, one persistent duplex socket per bilateral link, role-order-independent connect retry). **The seam is where `build-fe-be-process-split` adds its FE/BE transport as a new peer leaf** without touching the frozen ones; loopback is that build's hermetic test substrate.

**Scope honesty**: the full loopback semantics matrix is deferred (T056), so this freeze holds only the observable semantics the existing smoke tests pin — a known-thin but honest guarantee.

**Protected tests**: `glp_gleam/test/glp/link/loopback_test.gleam`, `glp_gleam/test/glp/link/tcp_test.gleam` (incl. 3 real-socket smoke tests)

---

## Entry `link-layer`  (WP `freeze-link-layer`)

The below-GLP link seam: endpoint vtable and constructor surface, scheme/address/id/options/fault types (`link/seam/endpoint.gleam:39-47`). The GLP-facing link primitives, fault-as-data decoration, and sequence/dedup are **gaps**, not frozen — wave-3 closes them onto this seam per the 025 contracts verbatim.

**Protected tests**: `glp_gleam/test/glp/link/loopback_test.gleam`

---

## Entry `module-system`  (WP `freeze-module-system`)

The delivered module-system half: declaration parsing and export/import flags (`parser/parser.gleam:2198-2254`) and Distribute/Transmit code generation (`compiler/codegen.gleam:460-464`). Runtime module-RPC execution is **unimplemented** (faults) — wave-3 close work, which must not reshape these parse/codegen surfaces.

**Protected tests**: whole-suite (no module-RPC-specific test exists at baseline — recorded as a known-thin pin, per the plan's own note)

---

## Entry `atomvm-policy`  (WP `freeze-platform-atomvm-policy`)

Feature-wide constraint, not merely an interface: **no OTP-abstraction package anywhere in the tree**, plain `spawn` and Subjects only, enforced by the `deps_policy` tripwire. This binds every wave-4 build WP — the FE/BE process split on BEAM must be built inside this constraint. Relaxing it requires a rule-request ruling, never a quiet dependency addition.

**Protected tests**: `glp_gleam/test/glp/deps_policy_test.gleam`

---

## Entry `suite-baseline`  (WP `guard-suite-gleam` + `guard-suite-dart-reference` + `guard-suite-csharp-reference`)

The grow-only floor. **Measured at baseline commit `49b523420d745875c67207417adf56c8a5537331`, 2026-07-20:**

| Suite | Command | Baseline result |
|---|---|---|
| Gleam gleeunit | WSL: `cd glp_gleam && gleam test` | **463 passed, no failures** |
| Dart unified REPL | `DART=/c/Users/gavri/dart-sdk/bin/dart.exe bash test/run_all_tests.sh` | **532 / 532** after the wave-1 AOT-smoke harness fix (see note) |
| C# link reference | `dotnet test csharp/glp_link.tests` | **147 passed, 0 failed, 0 skipped** |
| C# result-codec reference | `dotnet test csharp/glp_result_codec/tests` | **131 passed, 0 failed, 0 skipped** |

**Invocation note (wave-1 correction):** the plan's acceptance text reads `dotnet test csharp/glp_link.tests` *and* the result-codec project as one line; `dotnet test` accepts **one project per invocation** (MSBUILD error MSB1008 otherwise). They are two commands, recorded as two rows above.

**Grow-only rule**: the Gleam count may only grow across waves, never shrink; no test may be skipped or modified without a rule-request. One red test blocks all wave-4 build WPs — deliberate, since a shrinking or reddening suite is exactly the drift this guard exists to make loud.

**Degrade-loudly rule** (C#): if the .NET toolchain is unavailable on a runner, the guard degrades with a **recorded gap**, never a silent skip.

**Wave-1 Dart-oracle harness fix (engineer-approved, 2026-07-20).** The baseline first measured 531/532: Section Q's AOT-smoke check "AOT exe loads self.glp from correct path" asserted the regex `glp[/\\]programs[/\\]self.glp`, which only matches a repo literally named `glp` (the sibling Mac repo). This repo is `glpnet`, so the exe's correct load line (`…\glp\glpnet\programs\self.glp`) failed the string match — a harness false-negative, not a runtime regression: the exe loaded self.glp correctly and all 8 functional AOT checks (ex-02 `:=` arithmetic → Sum=21, ex-03 `now/1`+`_output`+binding) passed. Per the bug protocol this was reported to the engineer, who approved the one-line fix; the regex is now `glp\(net\)\?[/\\]programs[/\\]self.glp` in `test/run_aot_smoke.sh`. AOT smoke → 9/9, full suite → 532/532. The pinned floor is 532; the runtime was never touched.

---

## Open items carried by wave 1 (not resolved here)

- **`guard-atomvm-gated-probe`** — ✅ RESOLVED (2026-07-21). The gated codec entries were executed on a genuine **Node AtomVM wrapper** (`atomvm/AtomVM@v0.7.0-alpha.1`, Node/WASM target, sha256-verified — the released 0.7.x line of the `0.7.999` snapshot commit `99a80ba7` used; installed to `C:\Users\gavri\tools\atomvm\`) under Windows Node v22, against `glp_gleam` beams built `gleam build --target erlang` on OTP-27. Measured output is **byte-identical** to the runbook's pinned expected table and to the prior 0.7.999 run, with all three round-trips `true`: int64-max `<<2,255,255,255,255,255,255,255,127>>`, int64-min `<<2,0,0,0,0,0,0,0,128>>`, float-Pi `<<3,24,45,68,84,251,33,9,64>>` (see `atomvm-probe-runbook.md` → *Measured verdicts — 2026-07-21*). Items 1–2 of the runbook are now **measured** (was "expected"); item 3 (`git diff --exit-code` of the probe source vs baseline `49b5234`) stays green. The gated entries **remain EXCLUDED** from the byte-final goldens — this records the AtomVM verdict, it does not promote them (promotion would need a `codec-envelope` rule-request). Refutation discipline honored: the default-present native AtomVM 0.6.6 (WSL) cannot run the codec (`erlang:list_to_bitstring/1` BIF gap → CRASH, not a `false`) and was classified **environment**, which is why the 0.7.x wrapper was installed. The probe still runs outside `gleam test` (full-OTP `gleam test` is explicitly **not** an AtomVM-faithfulness signal), so this remains a human-in-the-loop checkpoint guard by design — now with a measured reference verdict rather than an open gap.
- **`guard-fe-be-envelope-seam`** — ✅ RESOLVED (commit `3ea7dde9`, 2026-07-20). The ED-1 seam bytes are pinned to a checked-in golden corpus `glp_gleam/test/glp/repl/envelope_seam_golden.hex`, enforced by two tests added to `envelope_identity_test.gleam`: `encode_reproduces_pinned_golden_test` (per corpus goal, engine→`encode` bytes == golden **and** `decode`(golden) == the engine envelope) and `golden_covers_seam_corpus_test` (golden name-set == corpus — no silent add/drop). Corpus = 4 deterministic engine-produced envelopes: `success_arith_bind` (`X:=2+3`→X=5), `success_arith_mul` (`X:=6*7`→X=42), `failure_unknown_pred_1` (`no_such_pred(1)`), `failure_unknown_pred_2` (`no_such_pred(1,2)`). Binary `-` in `:=` was deliberately excluded — it parse-fails today (the known `runtime-arithmetic-expression` gap; freezing a gap would be wrong — reported per bug protocol). Verified 2026-07-20: `gleam test` → **465 passed** (floor grows 463→465); flipping any golden byte fails the suite (464/1, `should.equal` panic); a missing file panics the `read_file` assert. The FE/BE seam is now byte-frozen before the wave-4 split rides it.
- **`rule-quic-sideprocess-relay`** (OPEN ESCALATION, engineer) — the Profile-A QUIC relay (`gleam_quic/src/glpq_ffi.erl`) has **zero tests** (`gleam_quic/test` is empty) and therefore sits outside every entry above. It is the one silent-drift hole in the delivered foundation. Due before any wave-4 WP depends on it; under the full-wiring clarification the yngenios **S2 network** service rides that QUIC path. Raised with Olamnit as ask 4 of COOP seq 27.
