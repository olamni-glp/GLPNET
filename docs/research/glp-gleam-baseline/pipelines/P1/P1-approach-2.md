## A. END-STATE ARCHITECTURE (Gleam/AtomVM baseline)

**M1 — single combined instance.** One self-sufficient Gleam GLP node. Layering follows FR-057: a pure-compute **engine core** (F4 `runtime/` immutable threaded heap + `unify` + `suspension`, then F5 `bytecode-runner` owning the scheduler + HEAD/GUARD/BODY three-phase, then F6 `compiler+loader`) with **zero** reference to console, transport, or link layers. A **thin REPL front-end** (F7) owns only the read-dispatch loop, colon-commands, and result display; it calls the engine in-process (no wire, no envelope codec). The result-envelope field-set (status/bindings/writer-id-map/suspended-detail/captured-output) becomes the engine's *return type* and the REPL's deep-resolver — folded in, not a separate process boundary. Concurrency substrate: the runner is plain sequential BEAM code (AtomVM-safe); any cell/goal spawning uses **raw `erlang:spawn` externals + `gleam_erlang` Subjects**, never `gleam_otp`/`proc_lib`. The scheduler **dedupes activations by `goal_id`** (carried-forward 034 obligation — the immutable value-copy heap does not preserve the cross-writer single-fire guard).

**M2 — multi-instance linked topology.** Two+ Gleam nodes, each a complete M1 engine, joined by **global links — never remote pointers**. Per link: one BEAM process pair (in-process Subject loopback for SC-001; later TCP/CoAP/BLE transport behind a `open/send-bytes/recv-bytes/close+fault` seam). Erlang's per-process FIFO supplies the per-link ordering the commutativity theorem requires. Distributed bind = an **assignment message** (`_w(p,i):=T`) applied by an ordinary local `assign`; writer-MGU, three-valued suspend/reactivate, deref, WxW, path-compression all stay **purely local** — deref never crosses the wire. **globalize/localize** mint a fresh local pair per global name on every hop, guarded by `known/1`. Wire = the GLP **big-endian recursive TLV** (term tags, const subtypes, per-variable polarity byte + paired-reader-localId, varint codec) — **NOT** BEAM term-to-binary (that breaks byte-parity). The multi-client server is the **GLP control program** (`serve/2` + `mwm` fan-in + `Link` mailboxes from `self.glp`) running on the Gleam engine — portable as-is. **Supervision/liveness/crash-restart map onto OTP supervision trees on plain BEAM** (the test runtime); on AtomVM, raw-spawn + a fault-monitor process. Critically, BEAM `link`/`EXIT` auto-propagation is **forbidden** as a failure path — `monitor` is used only to *generate a bound `tempFail`/`permFail` term* on a monitor stream (fault-as-data over `ok/tempFail/permFail`); plus epoch/fencing + global-name idempotency for split-brain.

## B. ALIGNMENT RUBRIC

For each feature ask, in order:

1. **On the parity path?** Does it produce or unblock the EVIDENCE that proves M1 (ported corpus green on BEAM) or M2 (byte-identical split + cross-runtime round-trip)? If core to that chain → **aligned**.
2. **Right substrate, wrong runtime?** Does BEAM/OTP supply the capability natively (process split, multi-accept, supervision, crash-restart, mailbox)? → **supersede-by-beam**.
3. **C# engine-separation artifact whose *contract* (not code) the Gleam path needs?** → **fold-into-gleam** (absorb the field-set/seam into a Gleam feature; no standalone feature).
4. **Needs re-scoping to the Gleam process model** (drop async/await/in-place mutation) but still required → **realign**.
5. **Cross-runtime interop gate** (Gleam as a third link end) → **keep-cross-runtime**.
6. **Does it accelerate the AtomVM baseline?** If a spike (cpp/llvm/antlr/mlir/shared-static-memory/IL-codec/persistence) does NOT shorten the path to M1/M2 evidence → **drop** (off critical path; revisit only post-baseline).

## C. PER-FEATURE DISPOSITION

- #2 engine-review-and-design-dossier => **aligned** — shipped; portable contracts feed Gleam seams.
- #11 result-envelope-and-deep-resolve => **fold-into-gleam** — envelope field-set becomes engine return type.
- #10 structured-output-capture-seam => **fold-into-gleam** — output capture is a thin REPL concern.
- #4 il-codec-spike => **drop** — C#-only; wire carries source/TLV, not IL.
- #15 result-codec-and-framecodec-ride => **drop** — C# process-split codec; BEAM uses native processes.
- #13 repl-engine-process-split-mvp => **supersede-by-beam** — engine/REPL split is native BEAM processes.
- #20 engine-state-snapshot-and-persistence-api => **drop** — persistence off the parity path; defer.
- #30 liveness-crash-restart-host => **supersede-by-beam** — OTP supervision trees supply this natively.
- #18 restore-and-resume-with-link-reestablish => **drop** — resume/persistence off parity path; defer.
- #21 multi-accept-transport-extension => **supersede-by-beam** — BEAM processes accept concurrently natively.
- #23 compiled-il-on-the-wire-and-factor-out-compiler => **drop** — not on Gleam parity path.
- #17 antlr4-shared-grammar-spike => **drop** — #27 ports the compiler directly; no acceleration.
- #29 multi-client-control-program-in-glp => **realign** — GLP `serve/2` loop; fold into #36 for M2.
- #28 cpp-engine-feasibility => **drop** — does not accelerate AtomVM baseline.
- #33 many-instances-shared-static-memory-cooperative-scheduling => **drop** — BEAM supplies scheduling/footprint.
- #16 research-programme-and-llvm-feasibility => **drop** — non-gating research; off path.
- #26 glp-gleam-bytecode-runner => **aligned** — CRITICAL M1 gate; promote+specify now.
- #27 glp-gleam-compiler-and-loader => **aligned** — CRITICAL M1; pure-pipeline port.
- #6 glp-gleam-repl => **aligned** — the M1 single-instance milestone.
- #8 glp-test-corpus-port-and-runner => **aligned** — the M1 faithfulness proof.
- #36 glp-gleam-link-layer => **realign** — CRITICAL M2; re-scope to Gleam process model.
- #5 cross-runtime-csharp-gleam-distributed-tests => **keep-cross-runtime** — the M2 capstone gate.
- #030 marathon-refinement => **aligned** — shipped infra; drives heavy F5/F9 marathons.

## D. FASTEST ORDERED SEQUENCE

Everything dropped/superseded above leaves a clean spine. **Critical path is the whole spine** (no parallel shortcut buys M1/M2 evidence faster); fold-ins ride inside their host feature.

**M1 — single-instance parity (CRITICAL PATH):**

1. **#26 bytecode-runner** ⚠️CRITICAL — promote `refined`→`specified`, run under #030 marathon. Ports the WAM runner over F4's immutable heap; owns scheduler + HEAD/GUARD/BODY three-phase; enforces **`goal_id` activation dedupe** and the suspension-DROP/self-bind-⇒-Unbound fixes. Gate: PC-2/3/11/14.
2. **#27 compiler+loader** ⚠️CRITICAL — SRSW→PE→typecheck→compile→load; reuse golden bytecode from Dart/C# for parity; loader spawn uses raw `erlang:spawn`.
3. **#6 Gleam REPL** ⚠️CRITICAL — **M1 milestone**. Thin front-end + in-process engine; **folds in #11** (envelope return type + deep-resolve) and **#10** (output capture).
4. **#8 test-corpus port** ⚠️CRITICAL — **M1 PROVEN**. 100% agreement with recorded Dart outcomes, green on BEAM, no `gleam_otp`. *This green is M1.*

**M2 — multi-instance linked parity (CRITICAL PATH):**

5. **#36 link-layer** ⚠️CRITICAL — re-scoped, marathon under #030. Build in evidence order:
   - 5a. **TLV wire codec** (byte-for-byte big-endian) + **globalize/localize** on the `known/1` seam + global-writers routing.
   - 5b. **Single in-process/loopback transport** + distributed-bind-as-assignment → hit **SC-001 byte-identical split** first.
   - 5c. **Reliability sublayer + fault-as-data monitor** (`ok/tempFail/permFail`) + epoch/fencing; **folds in #29** (`serve/2`+`mwm` GLP control program) for N-client.
6. **#5 cross-runtime C#↔Gleam distributed tests** ⚠️CRITICAL — **M2 capstone PROVEN**. Real transport, executed round-trip; Gleam as a third byte-parity link end (Dart↔Gleam, C#↔Gleam); full adversarial corpus identical verdicts. *This green is M2.*

**Infra (continuous, non-gating):** **#030 marathon** wraps #26 and #36 (the two heavy lifts) for durable cross-session checkpointing.

**Net:** four features to M1 (#26→#27→#6→#8), two to M2 (#36→#5). All thirteen C#/spike features are off the path — dropped, superseded by BEAM/OTP, or folded into the six Gleam features as contracts. Parity evidence, not feature count, defines the baseline.