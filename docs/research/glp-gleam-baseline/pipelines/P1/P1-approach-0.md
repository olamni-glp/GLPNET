# Gleam/AtomVM Baseline — Gleam-Native Fastest Path

## A. END-STATE ARCHITECTURE

**Guiding principle:** the engine-separation dossier (A1) builds by hand what BEAM gives free. On AtomVM we delete the host/transport/mailbox/process-split machinery and keep only GLP semantics + the GLP TLV wire. Concurrency primitive throughout: **raw `erlang:spawn` externals + `gleam_erlang` Subjects** (no `gleam_otp`/`proc_lib`); monitors via `erlang:monitor` (present on AtomVM).

**M1 — single combined instance (two BEAM processes, one node):**
- **Engine process** — sole owner of F4's immutable threaded heap, F5's scheduler/goal-queue/suspension index, three-phase HEAD→GUARD→BODY runner, deref, writer-MGU. Single-owner heap ⇒ zero concurrency hazard, no locks. All sequential BEAM code ⇒ AtomVM-safe. Scheduler **dedupes activations by `goal_id`** (carried 034 obligation).
- **Front-end (REPL) process** — owns console I/O, read-dispatch loop, colon-commands, all result display. Sends **source text** (goal/load/source) to the engine over a Subject; receives a **result-envelope ADT** (status / bindings / writer-id map / suspended-goal detail / captured output / errors) as an ordinary BEAM message.
- **The "process split" (#13) is native** — BEAM message-passing IS the seam; no TCP, no `FrameCodec`, no result-codec. BEAM copies messages, so the envelope must be **deep-resolved server-side** to self-contained ground terms (no engine-heap `VarRef` escapes) — that is the only piece of #11/#10 we keep, folded into the engine's result producer.
- **Supervision/liveness (#30)** — a ~30-line monitor-loop process (`spawn`+`monitor`) that restarts the engine on a crash signal; on plain BEAM this is an OTP supervisor, on AtomVM the hand-rolled equivalent. No systemd/SCM/heartbeat-file host.

**M2 — multi-instance linked topology:**
- Each GLP instance = an engine process (eventually its own AtomVM/BEAM node). A **link = one sender + one receiver process per direction**; **per-link FIFO is free** (BEAM guarantees message order between two processes) — satisfying the Lemma-5.7 precondition without a reliability reorder buffer on the in-node path.
- **Wire format = the GLP big-endian recursive TLV** (corpus 13), NOT `term_to_binary` — byte-parity with C#/Dart is mandatory. Term tags 1/2/3, const subtypes, **per-variable polarity byte + paired-reader localId**, varint codec, `gid=creator:localId`. This codec is the one place C#-era artifacts (wire shapes, adversarial corpus) are reused for cross-runtime interop.
- **Globalize/localize on the `known/1` seam**, per-hop, minting a fresh local pair per global name. **Distributed unification = deferred local `assign`** via the existing single-instance writer-MGU; deref/WxW/path-compression stay strictly local (never cross the wire).
- **Fault model:** `erlang:monitor` is the *mechanism* only — it generates a bound `tempFail`/`permFail` term on a monitor stream. BEAM's auto-propagating `link`/`EXIT` is **forbidden** (violates fault-as-data). Reliability sublayer (seq/dedup, epoch/fencing, idempotent redelivery) is **GLP-semantic, hand-built**.
- **Transport seam** (`open`/`send-bytes`/`recv-bytes`/`close+fault`): in-node loopback (two processes) first to hit SC-001 byte-identical split; real socket via AtomVM `gen_tcp` externals after.
- Optional: the `serve/2`+`mwm` GLP control program as the in-GLP persistent mailbox — nice-to-have, since BEAM already gives multi-client fan-in.

## B. ALIGNMENT RUBRIC

Apply in order; first match wins:

1. **keep** — already shipped infra/contracts; consumed, not re-run.
2. **aligned** — a Gleam feature delivering M1/M2 faithful semantics on the critical path. Keep as-is.
3. **supersede-by-beam** — its deliverable is an OS/transport/host/process-split/scheduler mechanism BEAM provides natively. Do **not** port; absorb the requirement into a BEAM primitive.
4. **fold-into-gleam** — a C#-prep whose *contract* (envelope shape, output sink, deep-resolve) ports but has no standalone existence on BEAM. Merge into the relevant Gleam feature.
5. **keep-cross-runtime** — needed only because C#↔Gleam interop demands byte-parity (wire format, parity corpus).
6. **realign(defer)** — real future value (persistence/resume) but off the M1/M2 faithful-semantics path; re-express Gleam-native later.
7. **drop** — alternative substrate or research spike orthogonal to the Gleam baseline.

## C. PER-FEATURE DISPOSITION

- #2 engine-review-and-design-dossier => **keep** — shipped; contracts harvested, nothing to port.
- #11 result-envelope-and-deep-resolve => **fold-into-gleam** — envelope ADT + deep-resolve into engine/REPL.
- #10 structured-output-capture-seam => **fold-into-gleam** — capturable output sink inside REPL.
- #4 il-codec-spike => **drop** — C#-only; M1/M2 carry source text, not IL.
- #15 result-codec-and-framecodec-ride => **supersede-by-beam** — BEAM messages carry the envelope, no codec.
- #13 repl-engine-process-split-mvp => **supersede-by-beam** — two BEAM processes, no TCP split.
- #20 engine-state-snapshot-and-persistence-api => **realign(defer)** — off faithful-semantics path; Gleam-native snapshot later.
- #30 liveness-crash-restart-host => **supersede-by-beam** — native supervision/monitor, tiny restart loop.
- #18 restore-and-resume-with-link-reestablish => **realign(defer)** — depends on persistence; restart = supervisor.
- #21 multi-accept-transport-extension => **supersede-by-beam** — process-per-accept is native, no multi-accept loop.
- #23 compiled-il-on-the-wire-and-factor-out-compiler => **drop** — compiler stays engine-side; source on wire.
- #17 antlr4-shared-grammar-spike => **drop** — Gleam owns its parser (F6); orthogonal.
- #29 multi-client-control-program-in-glp => **realign(defer)** — GLP serve/2 optional; BEAM gives multi-client free.
- #28 cpp-engine-feasibility => **drop** — alternative substrate, off Gleam baseline.
- #33 many-instances-shared-static-memory-cooperative-scheduling => **supersede-by-beam** — lightweight processes + scheduler already native.
- #16 research-programme-and-llvm-feasibility => **drop** — non-gating research, off path.
- #26 glp-gleam-bytecode-runner => **aligned (critical)** — F5; the M1 gate, promote+specify.
- #27 glp-gleam-compiler-and-loader => **aligned (critical)** — F6; loader spawn via raw `erlang:spawn`.
- #6 glp-gleam-repl => **aligned (critical, M1)** — F7; front-end+engine as two processes.
- #8 glp-test-corpus-port-and-runner => **aligned (critical)** — F8; M1 faithfulness gate vs Dart goldens.
- #36 glp-gleam-link-layer => **aligned (critical, M2)** — F9; GLP TLV wire + globalize/localize + fault-as-data.
- #5 cross-runtime-csharp-gleam-distributed-tests => **keep-cross-runtime (capstone)** — M2 gate; C# wire-parity corpus.
- #030 marathon-refinement => **keep** — shipped harness; drive the heavy F5/F9 marathons.

## D. FASTEST ORDERED SEQUENCE

**Step 0 — roadmap hygiene (minutes, non-gating):** mark #4, #2, #030 `released`; promote #26 `refined`→`specified`. Fold #11+#10 into #6's spec; close #13/#15/#21/#30 as superseded-by-BEAM (record rationale); park #20/#18/#29 deferred; drop #4/#23/#17/#28/#33/#16.

**CRITICAL PATH to M1:**
1. **#26 (F5 bytecode-runner)** ★ — three-phase HEAD/GUARD/BODY over F4's immutable heap; scheduler consuming activations; **dedupe by `goal_id`**; carry 034 fixes (self-bind⇒Unbound, forward-to-terminal suspension-drop). Heavy/marathon.
2. **#27 (F6 compiler+loader)** ★ — SRSW→PE→typecheck→compile→load; loader process-spawn via raw `erlang:spawn`. Reuse C#/Dart golden bytecode for parity.
3. **#6 (F7 REPL)** ★ — load/goal/`:trace`/`:limit`; engine + front-end as two BEAM processes over Subjects; **absorbs #11 deep-resolve + envelope ADT and #10 output sink**. ⇒ **M1 single combined instance reached.**
4. **#8 (F8 test-corpus)** ★ — port the A4 parity corpus (PC-1…PC-15); 100% agreement with recorded Dart outcomes, green on BEAM, no `gleam_otp`. ⇒ **M1 LOCKED (faithfulness certified).**

**CRITICAL PATH to M2:**
5. **#36 (F9 link-layer)** ★ — GLP big-endian TLV codec (byte-parity, validated on the adversarial corpus) + globalize/localize on `known/1` + distributed-unify-as-local-assign + `erlang:monitor`-backed fault-as-data + reliability sublayer; **in-node loopback transport first** (hit SC-001 byte-identical split), then `gen_tcp`. Heavy/marathon.
6. **#5 (F10 cross-runtime C#↔Gleam)** ★ — one role-parameterized program split across a C# instance and a Gleam instance over one wire; byte-identical format, behaviour-identical reliability, identical invariants both runtimes. ⇒ **M2 capstone reached.**

**Net new work vs the C# dossier:** only F5–F10 (six Gleam features) + the one TLV codec. Everything in the engine-separation epic's MVP/follow-up tail (host, multi-accept, FrameCodec ride, process-split-over-TCP, persistence) is either provided by BEAM or deferred — the fastest path to a self-sufficient Gleam/AtomVM GLP doing both single-instance and linked-distribution work.