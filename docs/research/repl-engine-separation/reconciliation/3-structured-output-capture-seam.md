# Seed Reconciliation Memo — #3 structured-output-capture-seam

**Date:** 2026-06-09
**Feature id:** structured-output-capture-seam
**Dossier entry:** §11 #3 (PREP)
**Memo path:** docs/research/repl-engine-separation/reconciliation/3-structured-output-capture-seam.md
**Brief source:** buildkit-roadmap brief structured-output-capture-seam (read-only)
**Depends on:** #1 engine-review-and-design-dossier

---

## Dossier cross-references

- §1.3 — "captured/streamed output" row in the wire-crossing table: `OutputKernel` at `body_kernels.cs:959`, `OutputCallback` at `runtime.cs:135`, `TraceSink` at `scheduler.cs:138`
- §0.4 — classification table row: "captured/streamed output" — no explicit row (output capture is embedded inside the §1.3 "leaks to promote" narrative); the seed is motivated by that narrative
- §2.3 — result envelope `captured/streamed output` field: must route via `OutputCallback`/`TraceSink` onto the wire
- §8.1 — Slice A MVP net-new deps: "result-leak closures — promote … captured output … routing output via `OutputCallback`/`TraceSink`"
- §10.2 — open fork: streaming vs terminal envelope (affects how the routed output rides the wire; this seed routes to the hooks, not onto the wire — §10.2 is deferred to #5)
- §12 — no dedicated risk row for this seed; the risk context is implicit in the §1.3 "leak" description

---

## Seed-vs-dossier-vs-code

### Stored roadmap profile

Notes field: "PREP. Route all Console.WriteLine/trace through OutputCallback/TraceSink so output is capturable/streamable before a process split. depends-on: #1. (§7 #3)"

The roadmap note references §7 #3 which is an earlier internal numbering (the dossier's §11 uses the same entry at #3). WSJF=3.6, RICE=2400. Effort=M (S-to-M is plausible given the scope clarification below).

### What the dossier says

Dossier §1.3: the `OutputKernel` (`body_kernels.cs:959`) emits via `Console.WriteLine` as a side-effect; `OutputCallback` (`runtime.cs:135`) already exists but the REPL does NOT set it. `TraceSink` (`scheduler.cs:138`) also exists on `Scheduler` but `GlpEngine` never wires it.

The dossier frames the scope as "route all Console.WriteLine/trace through OutputCallback/TraceSink." The seed stores this verbatim.

### What the code actually shows (as-built verification)

**OutputCallback** (`runtime.cs:135`): `Action<string>? OutputCallback { get; set; }` — the hook exists and `OutputKernel` at `body_kernels.cs:940-959` already uses it (`callback != null ? callback(formatted) : Console.WriteLine(formatted)`). The hook is wired by the multiagent layer only (`agent_runtime.cs:244`: `_runtime.OutputCallback = text => _Output(...)`). The REPL and GlpEngine itself do NOT set it.

**TraceSink** (`scheduler.cs:138`): `Action<string>? TraceSink { get; set; }` — the hook exists and `Scheduler.Trace()` at `scheduler.cs:399-405` already routes through it. BUT `GlpEngine` instantiates `Scheduler` at `glp_engine.cs:535` WITHOUT passing a `TraceSink` argument. So for every REPL-driven goal execution, trace goes straight to `Console.WriteLine`.

**Console.WriteLine calls in engine library (out/csharp/lib/) — full inventory:**

| File | Count | Nature |
|---|---|---|
| `body_kernels.cs` | 47 | 1 = `OutputKernel` fallback (behind `OutputCallback`); 46 = `[ABORT]` / arithmetic error diagnostic paths |
| `runner.cs` | 11 | All `[ERROR]` / `[MODULE]` / `[WARN]` diagnostic paths, NOT behind any sink |
| `scheduler.cs` | 5 | 3 in comments; 1 = `Trace()` helper (behind `TraceSink`); 1 = `[DEBUG]` timer-wait path (NOT behind `TraceSink`) |
| `glp_engine.cs` | 1 | `[TYPE WARNING]` non-strict-mode (NOT behind any sink) |
| `compiler.cs` | 3 | `[TYPE ERROR]`, `[TYPE WARNING]`, `[TYPE CHECK]` diagnostic paths (NOT behind any sink) |
| `codegen.cs` | 3 | Bytecode dump (debug flag guard on `foo/1` only, clearly a dev artifact) |
| `runtime.cs` | 1 | In docstring comment only — not a real call |
| `isolate_manager.cs` | 11 | Multiagent layer; TraceSink IS used but wired to `Console.WriteLine` itself |

**MISSED by dossier (scope extension):** The dossier describes the scope as "OutputCallback/TraceSink" and focuses on the two seams. As-built, there are 3 additional classes of not-yet-capturable output:

1. **`runner.cs` ERROR/MODULE/WARN paths** (11 calls) — completely outside any sink; these are engine-internal diagnostics that will escape as raw `Console.WriteLine` after a process split.
2. **`scheduler.cs` [DEBUG] timer-wait path** (1 call at ~`:631`) — outside `TraceSink`.
3. **`compiler.cs` / `glp_engine.cs` type-error/warning paths** (4 calls) — outside any sink. These fire during `LoadSource`/`LoadFile`, which is engine-side after the split; a remote client gets no structured notification, only a Console leak.
4. **`codegen.cs` debug dump** (3 calls) — guarded by `proc.Signature == "foo/1"`, clearly a leftover dev artifact; should be removed rather than routed.

The dossier's stated scope ("Route all Console.WriteLine/trace through OutputCallback/TraceSink") technically covers all of these, but the as-built code reveals more sites than the two hooks imply. The scope is correctly stated but the implementation size is underestimated if interpreted as "just wire GlpEngine to pass TraceSink to Scheduler."

**GlpEngine TraceSink wiring gap** (missed by dossier): `GlpEngine` never sets `scheduler.TraceSink`. This is the single highest-value change for this seed — it ensures every `DebugTrace`-on trace line goes through the wire rather than console. The fix is in `glp_engine.cs:535` (Scheduler construction). The engine needs either a `TraceSink` property or it synthesises one from an `OutputCallback`-style seam.

**isolate_manager.cs TraceSink wires to Console.WriteLine** (`isolate_manager.cs:551,554`): this is in the multiagent layer which is above the engine seam; it is a minor scope question whether this seed covers the multiagent layer or only the engine/compiler/runner/scheduler.

---

## Classification check

**Kind PREP — correct.** This is a pure refactoring foundation: redirect side-channel Console output into injectable hooks so downstream features (#5 result-codec, #6 MVP split) can capture output programmatically. No new GLP language semantics, no wire codec, no IL. The seed does not touch grammar, IL, or the wire format.

**Code supports scope:** confirmed at `runtime.cs:135` (`OutputCallback`), `scheduler.cs:138` (`TraceSink`), `body_kernels.cs:940-959` (OutputKernel already behind callback), `glp_engine.cs:535` (Scheduler not wired with TraceSink — the primary gap). The scope is correct; the implementation surface is larger than a two-line fix.

---

## Tensions

### T1: Scope boundary — "all Console.WriteLine" vs only the two user-output hooks

**Evidence:** The dossier §1.3 says "output must route via `GlpRuntimeEngine.OutputCallback` (`runtime.cs:135`) + `Scheduler.TraceSink` (`scheduler.cs:138`) onto the wire." This implies only these two hooks. But as-built there are ~25 additional `Console.WriteLine` calls in `runner.cs`, `compiler.cs`, `glp_engine.cs`, and `scheduler.cs` debug paths that are engine-diagnostic (not user-program output) and are completely outside any sink.

**Options:**
1. Narrow scope: route ONLY the two user-visible output channels (`OutputCallback` for `_output/1`; `TraceSink` for trace lines) and leave diagnostic `[ERROR]`/`[ABORT]`/`[TYPE ERROR]` calls as console (they are internal fault signals, not program output). Document this explicitly in the spec.
2. Broad scope: introduce a third `DiagnosticSink` (or reuse `TraceSink`) to route all engine-internal error/warning diagnostics, making the engine fully silent on `Console` after the split.
3. Phased scope: route the two hooks (Opt 1) in this seed; add `DiagnosticSink` as a separate follow-up aligned with #5 (result envelope has an `errors` field that can carry structured diagnostics).

### T2: GlpEngine does not wire TraceSink to the Scheduler it creates

**Evidence:** `glp_engine.cs:535` — `new Scheduler(rt: _runtime, runners: {...})` with no `traceSink` argument. So `GlpEngine.DebugTrace = true` produces console output that escapes process isolation. The `TraceSink` seam exists on `Scheduler` but is unreachable from `GlpEngine`'s public API.

**Options:**
1. Add `public Action<string>? TraceSink { get; set; }` to `GlpEngine` and pass it when constructing `Scheduler`. Minimal change; aligns with `OutputCallback` style.
2. Unify: make `GlpEngine.OutputCallback` serve double duty (output + trace), collapsing two seams into one. Risks mixing user-program output and debug trace — a confusion the dossier explicitly avoids.
3. Do not expose `TraceSink` from `GlpEngine`; instead document that the `DebugTrace` path is a dev-time feature and is acceptable console output. This punts the problem but conflicts with Slice A needing clean capture.

### T3: codegen.cs debug dump for `foo/1` — route or remove?

**Evidence:** `codegen.cs:182,215,217` — three `Console.WriteLine` calls guarded by `proc.Signature == "foo/1"`, clearly a development artifact. After a process split this is noise for any program that happens to define `foo/1`.

**Options:**
1. Remove the guard entirely (the hardcoded `foo/1` guard is obviously not production).
2. Route through a `DiagnosticSink` or logging abstraction (over-engineers a dev artifact).
3. Convert to a conditional compiler-debug flag (e.g., `#if DEBUG`) rather than a runtime predicate check.

---

## Under-specifications

### U1: Which "output" channels are in scope

**Why it matters:** the seed title says "Console.WriteLine/trace" but the dossier anchors to exactly two hooks. A specification that lists only two hooks leaves 25+ diagnostic calls unaddressed. The implementer will either discover them mid-work (scope creep) or leave them (the process split still leaks diagnostics).

**Options:** narrow to two hooks + formal `out-of-scope` list for diagnostics; broad to three hooks (user-output, trace, diagnostic); phased (two hooks now, structured-errors in #5).

### U2: Where does `GlpEngine` expose a TraceSink setter

**Why it matters:** the engine is the public API. The REPL/client needs to be able to say "all trace output goes here." Currently there is no such API on `GlpEngine`. Without it, the process-split client cannot capture trace even after this seed.

**Options:** add `TraceSink` property to `GlpEngine` (mirroring `OutputCallback`); add a unified `OutputSink` covering both; leave `TraceSink` internal (only tests wire it directly to `Scheduler`).

### U3: Dart mirror parity

**Why it matters:** `FrameCodec` carries a byte-parity note (`FrameCodec.cs:31-32`, FR-060/061). The Dart `GlpRuntimeEngine` (`glp_runtime/lib/runtime/runtime.dart`) already has an `outputCallback` field (a Dart closure). If this seed changes the C# `OutputCallback`/`TraceSink` shape or wiring, the Dart mirror must track it to maintain convergence. The dossier §2.5 warns about cross-runtime byte-parity for new codecs; the same convergence principle applies to hook shape.

**Options:** explicitly verify Dart mirror alignment as a gate on this seed; treat Dart parity as out-of-scope (hook shape is not byte-level); add a Dart convergence note to the spec.

---

## GEPA/DSPy refinement

### Applicability: **methodological**

This seed is a systems/C# refactoring — there is no LLM-codegen program to optimize. GEPA/DSPy applies as an iterate-against-a-metric discipline: each candidate implementation (hook wiring) is evaluated against the metric combination; the refinement loop drives toward full capture coverage with zero regressions.

### Seed definition

Route all user-program output (`_output/1` kernel → `OutputCallback`) and scheduler trace (`Scheduler.Trace()` → `TraceSink`) through injectable callbacks so they are capturable/streamable by any host. Wire `GlpEngine` to expose and pass `TraceSink` to its internal `Scheduler`. Ensure zero `Console.WriteLine` calls in the engine library produce user-visible or trace content after the hooks are wired. Engine-internal diagnostic calls (`[ABORT]`, `[ERROR]`, `[TYPE ERROR]`) are classified separately (T1/U1).

### Metrics combination

| # | Name | Kind | Tool / Harness | Threshold |
|---|---|---|---|---|
| P1 | REPL test suite green | pragmatic | `bash test/run_all_tests.sh` | 384/384 (no regressions) |
| P2 | Output-capture regression: all `_output/1` calls appear in callback, none on Console | pragmatic | `out/csharp/test/multiagent/output_kernel_test.cs` + new test: wire `OutputCallback`, assert `Console` empty | 100% captured; 0 escapes |
| P3 | Trace-capture regression: all scheduler trace lines appear in `TraceSink` when wired | pragmatic | New C# unit test: set `GlpEngine.TraceSink`, run a traced goal, assert callback received all lines and Console empty | 100% captured; 0 escapes |
| P4 | Console.WriteLine count in `out/csharp/lib/` — user-output + trace categories reduced to zero | pragmatic | `grep -c 'Console\.Write' out/csharp/lib/runtime/body_kernels.cs` (OutputKernel path) + `out/csharp/lib/runtime/scheduler.cs` (Trace path) + `glp_engine.cs` TraceSink wiring verified | 0 unguarded Console calls for user-output/trace categories |
| F1 | Type/SRSW conformance preserved (no new type violations introduced) | formal | In-repo type-checker gate (`test/run_all_tests.sh` Section B/C) | 0 new type/SRSW failures |

**Note on formal metrics:** this seed does NOT touch the GLP language, wire IL, or grammar. It is a pure C# hook-wiring refactoring. The MANDATORY formal metric for language-touching or wire-touching seeds (ANTLR4 grammar verifier, mechanized semantics, byte-parity) does NOT apply here. The one formal metric (F1) is the type/SRSW conformance gate enforced by the existing in-repo checker — appropriate because the changes must not alter compiled GLP program behaviour.

### Interactive spec step

At the start of `/buildkit-specify structured-output-capture-seam`, the owner confirms:
1. Scope boundary: T1 option (narrow/broad/phased) — critical for implementation size.
2. Whether `GlpEngine` gets a `TraceSink` property (T2 option 1 recommended).
3. Dart mirror: convergence check required as a spec gate or deferred?
4. Metric P2/P3: confirm the test harness approach (new C# unit test vs extending existing `output_kernel_test.cs`).
5. Whether `codegen.cs` `foo/1` artifact is in-scope for removal.

### Refinement loop

Seed → candidate implementation → evaluate P1 (REPL suite) + P2 (OutputCallback capture test) + P3 (TraceSink capture test) + P4 (Console.Write grep count) + F1 (type/SRSW gate) → identify residual escape paths → refine wiring → repeat. Terminate when P1-P4 all pass and F1 shows 0 new failures. Claude-run, no external API.

---

## Formal tooling

### Lean 4 evaluation

This seed is a C# hook-wiring refactoring with no new GLP language semantics, no IL changes, and no wire format. Lean 4 is excellent for mechanized GLP semantics and IL verification but those subjects are not in scope here. For a "route Console output through an Action callback" refactoring, Lean 4 proof obligations would be trivially dischargeable (a callback wrapper is referentially transparent) — there is nothing to prove beyond "the callback is called iff the original Console.WriteLine was called." No mechanized proof is needed.

### Rocq evaluation

Same reasoning as Lean 4. Rocq's prior art in verified compiler/critical-software proofs is relevant for the later IL-touching seeds (#4, #5, #11) but not for a callback-wiring refactoring. No Rocq proof obligation arises here.

### Primary: `n/a`

No mechanized proof needed. The correctness of "OutputCallback is called instead of Console.WriteLine" is verified adequately by the pragmatic capture tests (P2, P3) and the REPL suite (P1). A formal proof would be trivially reducible to the definition of C# delegate invocation — proof overhead exceeds value.

**alternative_when:** If the scope is extended (T1 Opt 2) to include a `DiagnosticSink` that carries structured semantic-error information back to the client — and if that structured error encoding touches the wire format — then a byte-parity or round-trip formal metric becomes relevant and Lean 4 (via Lean-LSP-MCP) should be revisited for the extended scope's wire-touching component. Otherwise "none."

### IL verification: `n/a`

No IL or wire format is modified. The seed operates entirely at the C# layer above bytecode execution.

---

## Shapiro criteria preserved

This seed is a pure output-routing refactoring. It must preserve the following Shapiro/GLP design criteria:

1. **Committed-choice concurrency**: the `OutputKernel` fires as a body kernel inside a committed reduction step. Routing its output through a callback must not introduce any blocking, re-entrancy, or shared state that could deadlock the single-threaded drain loop. The callback MUST be invoked synchronously (no async/await in the callback path) — the existing `Action<string>` signature enforces this. Preserve: invocation is fire-and-forget, synchronous, non-suspending.

2. **SRSW (Single-Reader/Single-Writer)**: `OutputCallback` and `TraceSink` are injected sinks, not GLP variables. They do not participate in the heap's SRSW binding discipline. Preserve: no new GLP variables introduced; no SRSW-relevant changes.

3. **Suspension correctness**: a goal emitting output via `_output/1` has already ensured its argument is ground (`send_to_user/1` precondition in `self.glp`). The output callback does not change the suspension decision — a suspended goal does not reach `OutputKernel`. Preserve: the suspension/reactivation logic is untouched.

4. **Monotone variable binding**: the callback wiring is outside the heap's variable-binding machinery. No bindings are added or removed by this seed. Preserve: monotone binding invariant is structurally unaffected.

5. **Embedded-switch role (Shapiro/§3.5 anchor)**: the embedded GLP engine acts as a switch for external connectivity and internal OS/actor actions (QHSM/HSM). For that role, ALL output from the engine — user-program output and scheduler trace — must be capturable so the host process can route it (to a UI, a wire, a log) rather than losing it to the OS console. This seed directly enables that embedded-switch contract. A QHSM actor receiving output from a GLP goal MUST receive it via the registered callback, not via a console side-channel.

---

## Recommendation

Implement this seed as specified (PREP, depends on #1 only). The primary change is:

1. Wire `GlpEngine` to pass `TraceSink` to the `Scheduler` it constructs at `glp_engine.cs:535`.
2. Add `public Action<string>? TraceSink { get; set; }` to `GlpEngine` (mirroring `OutputCallback`).
3. Decide the scope boundary (T1) before implementation — recommended: narrow scope (user-output + trace only; diagnostic `[ABORT]`/`[ERROR]` paths are out-of-scope for this seed; open a separate issue or defer to #5's structured errors field).
4. Remove the `codegen.cs` `foo/1` debug artifact (T3 Opt 1) while in the area — it is clearly not production code.
5. Verify Dart mirror parity for the `OutputCallback` hook shape (U3).

The seed is small-to-medium effort, genuinely prerequisite for #5 (result codec must be able to capture output for the wire envelope), and low risk — it modifies the wiring of existing hooks, not the hooks themselves.

---

## Options for owner

1. **Narrow scope (recommended):** Route only `OutputCallback` (user output) and `TraceSink` (trace) through hooks; classify `[ABORT]`/`[ERROR]`/`[TYPE ERROR]` as engine-diagnostic console output, explicitly out-of-scope. Size: S. Unblocks #5 without additional dependency.

2. **Broad scope:** Route all `Console.WriteLine` calls in `out/csharp/lib/` through sinks (add `DiagnosticSink`). Size: M-L. More complete but delays #5 and introduces a new hook not designed in the dossier.

3. **Phased (narrow now + structured errors in #5):** Implement narrow scope here; the #5 result envelope's `errors` field absorbs compiler/type-error diagnostics as structured data, closing the diagnostic-escape path without a new `DiagnosticSink` hook. This is the cleanest layering — recommended if broad scope is desired eventually.

---

## Open questions

1. Does `GlpEngine` need a `TraceSink` property, or is trace-capture a test/host-only concern that callers handle by directly accessing the `Scheduler`? (The public API does not expose the `Scheduler` today — `TraceSink` on `GlpEngine` is necessary for the process-split client.)
2. Should the Dart mirror (`glp_runtime/lib/runtime/runtime.dart`) be updated in the same feature or as a follow-up? The Dart `outputCallback` already exists; the convergence check is whether `traceSink` exists there too.
3. Is the `codegen.cs` `foo/1` debug artifact tracked anywhere, or should it be removed as part of this seed? (No existing issue found.)
4. Does the process-split client (feature #6) need a `TraceSink`-over-wire protocol, or is trace output treated as part of the `captured/streamed output` field in the §2.3 result envelope? This determines whether the output streaming fork (§10.2) applies to trace as well as user output.

---

## External refs

- `out/csharp/lib/runtime/runtime.cs:135` — `OutputCallback` hook
- `out/csharp/lib/runtime/scheduler.cs:138` — `TraceSink` hook; `:399-405` — `Trace()` helper
- `out/csharp/lib/runtime/body_kernels.cs:940-959` — `OutputKernel` (already behind `OutputCallback`)
- `out/csharp/lib/engine/glp_engine.cs:535-538` — Scheduler construction (TraceSink NOT wired)
- `out/csharp/lib/engine/glp_engine.cs:294` — `[TYPE WARNING]` Console.WriteLine leak
- `out/csharp/lib/compiler/compiler.cs:91,102,112` — type error/warning Console.WriteLine leaks
- `out/csharp/lib/compiler/codegen.cs:182,215,217` — `foo/1` debug artifact
- `out/csharp/lib/bytecode/runner.cs:1137,1143,1149,1156,3018,3251,3261,3400,3405,3411,5643` — ERROR/WARN Console leaks
- `out/csharp/lib/multiagent/agent_runtime.cs:244` — multiagent layer wires `OutputCallback` (already done)
- `out/csharp/lib/multiagent/isolate_manager.cs:551,554` — multiagent layer wires TraceSink to Console.WriteLine (partial)
- Design dossier: `docs/research/repl-engine-separation/design-dossier.md` §1.3, §2.3, §8.1, §10.2
- Methodology: `docs/research/repl-engine-separation/reconciliation/SEED-RECONCILIATION-BRIEF.md` §2, §3, §3.5
